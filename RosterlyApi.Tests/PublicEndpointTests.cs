using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RosterlyApi.Entities;
using Xunit;

namespace RosterlyApi.Tests;

public class PublicEndpointTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _publicClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PublicEndpointTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _adminClient = factory.CreateClient();
        _publicClient = factory.CreateClient();
    }

    [Fact]
    public async Task GetInvitePage_ValidCode_ReturnsOrgAndEvent()
    {
        var (orgId, eventId, code) = await SeedInviteLinkAsync("Public Org");

        var response = await _publicClient.GetAsync($"/api/invite/{code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("Public Org", body.GetProperty("organizationName").GetString());

        var evt = body.GetProperty("event");
        Assert.Equal(eventId, evt.GetProperty("id").GetGuid());
        Assert.Equal("Future Event", evt.GetProperty("title").GetString());

        var slots = evt.GetProperty("slots").EnumerateArray().ToList();
        Assert.Single(slots);
        Assert.False(slots[0].GetProperty("isFull").GetBoolean());
    }

    [Fact]
    public async Task GetInvitePage_InvalidCode_Returns404()
    {
        var response = await _publicClient.GetAsync("/api/invite/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInvitePage_RevokedLink_Returns404()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Revoked Org");

        Guid linkId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            linkId = (await db.InviteLinks.FirstAsync(l => l.Code == code)).Id;
        }

        var revokeResp = await _adminClient.PutAsync($"/api/invite-links/{linkId}/revoke", null);
        Assert.Equal(HttpStatusCode.NoContent, revokeResp.StatusCode);

        var response = await _publicClient.GetAsync($"/api/invite/{code}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSignup_ValidRequest_Returns201()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Signup Org");

        var getPage = await _publicClient.GetAsync($"/api/invite/{code}");
        var page = await getPage.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var slot = page.GetProperty("event").GetProperty("slots").EnumerateArray().First();
        var slotId = slot.GetProperty("id").GetGuid();

        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Alice"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("Alice", body.GetProperty("volunteerName").GetString());
        Assert.Equal(slotId, body.GetProperty("slotId").GetGuid());
    }

    [Fact]
    public async Task CreateSignup_SlotFull_Returns409()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Full Slot Org");

        var getPage = await _publicClient.GetAsync($"/api/invite/{code}");
        var page = await getPage.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var slot = page.GetProperty("event").GetProperty("slots").EnumerateArray().First();
        var slotId = slot.GetProperty("id").GetGuid();
        var capacity = slot.GetProperty("capacity").GetInt32();

        // Fill the slot to capacity
        for (int i = 0; i < capacity; i++)
        {
            var signupResp = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
            {
                slotId,
                volunteerName = $"Volunteer {i + 1}"
            });
            Assert.Equal(HttpStatusCode.Created, signupResp.StatusCode);
        }

        // One more should fail
        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Extra Person"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateSignup_InvalidCode_Returns404()
    {
        var response = await _publicClient.PostAsJsonAsync("/api/invite/badcode/signups", new
        {
            slotId = Guid.NewGuid(),
            volunteerName = "Nobody"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSignup_SlotFromDifferentEvent_Returns404()
    {
        var (orgId, eventId, code) = await SeedInviteLinkAsync("Scoped Org");

        // Create a second event under the same org with its own slot, then look up its slot id
        var otherEventResp = await _adminClient.PostAsJsonAsync($"/api/organizations/{orgId}/events", new
        {
            title = "Other Event",
            date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd"),
            slots = new[] { new { label = "Other Slot", startTime = "08:00", endTime = "09:00", capacity = 5 } }
        });
        var otherEvent = await otherEventResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var otherEventId = otherEvent.GetProperty("id").GetGuid();
        Assert.NotEqual(eventId, otherEventId);

        var otherSlotId = (await _adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/events/{otherEventId}", _jsonOptions))
            .GetProperty("slots").EnumerateArray().First()
            .GetProperty("id").GetGuid();

        // Try to sign up for the other event's slot using the original invite code
        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId = otherSlotId,
            volunteerName = "Sneaky"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Helpers ---

    private async Task<(Guid orgId, Guid eventId, string code)> SeedInviteLinkAsync(string name)
    {
        var resp = await _adminClient.PostAsJsonAsync("/api/organizations", new { name });
        var org = await resp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var orgId = org.GetProperty("id").GetGuid();

        // Create a future event with a slot so the invite page has data
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var evtResp = await _adminClient.PostAsJsonAsync($"/api/organizations/{orgId}/events", new
        {
            title = "Future Event",
            date = futureDate.ToString("yyyy-MM-dd"),
            slots = new[]
            {
                new { label = "Slot 1", startTime = "08:00", endTime = "09:00", capacity = 3 }
            }
        });
        var evt = await evtResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var eventId = evt.GetProperty("id").GetGuid();

        var linkResp = await _adminClient.PostAsJsonAsync($"/api/events/{eventId}/invite-links", new { });
        var link = await linkResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var code = link.GetProperty("code").GetString()!;

        return (orgId, eventId, code);
    }
}
