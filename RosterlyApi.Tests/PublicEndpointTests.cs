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
    public async Task GetInvitePage_ValidCode_ReturnsOrgAndEvents()
    {
        var (orgId, code) = await SeedInviteLinkAsync("Public Org");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Create an event for today
        await _adminClient.PostAsJsonAsync($"/api/organizations/{orgId}/events", new
        {
            title = "Today's Event",
            date = today.ToString("yyyy-MM-dd"),
            slots = new[]
            {
                new { label = "Slot A", startTime = "08:00", endTime = "09:00", capacity = 2 },
                new { label = "Slot B", startTime = "09:00", endTime = "10:00", capacity = 3 }
            }
        });

        var response = await _publicClient.GetAsync($"/api/invite/{code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("Public Org", body.GetProperty("organizationName").GetString());

        var events = body.GetProperty("events").EnumerateArray().ToList();
        Assert.NotEmpty(events);
        var firstEvent = events.First(e => e.GetProperty("title").GetString() == "Today's Event");
        var slots = firstEvent.GetProperty("slots").EnumerateArray().ToList();
        Assert.Equal(2, slots.Count);
        Assert.False(slots[0].GetProperty("isFull").GetBoolean());
    }

    [Fact]
    public async Task GetInvitePage_InvalidCode_Returns404()
    {
        var response = await _publicClient.GetAsync("/api/invite/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInvitePage_ExcludesPastEvents()
    {
        var (orgId, code) = await SeedInviteLinkAsync("Past Org");

        // Create a past event
        await _adminClient.PostAsJsonAsync($"/api/organizations/{orgId}/events", new
        {
            title = "Past Event",
            date = "2025-01-01"
        });

        var response = await _publicClient.GetAsync($"/api/invite/{code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var events = body.GetProperty("events").EnumerateArray().ToList();
        Assert.DoesNotContain(events, e => e.GetProperty("title").GetString() == "Past Event");
    }

    [Fact]
    public async Task CreateSignup_ValidRequest_Returns201()
    {
        var (_, code) = await SeedInviteLinkAsync("Signup Org");

        var getPage = await _publicClient.GetAsync($"/api/invite/{code}");
        var page = await getPage.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var firstEvent = page.GetProperty("events").EnumerateArray().First();
        var firstSlot = firstEvent.GetProperty("slots").EnumerateArray().First();
        var slotId = firstSlot.GetProperty("id").GetGuid();

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
        var (_, code) = await SeedInviteLinkAsync("Full Slot Org");

        var getPage = await _publicClient.GetAsync($"/api/invite/{code}");
        var page = await getPage.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var slot = page.GetProperty("events").EnumerateArray().First()
            .GetProperty("slots").EnumerateArray().First();
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
    public async Task CreateSignup_WrongOrgSlot_Returns404()
    {
        var (orgId, code) = await SeedInviteLinkAsync("Wrong Slot Org");

        // Create another org with its own slot
        var otherResp = await _adminClient.PostAsJsonAsync("/api/organizations", new { name = "Other Org" });
        var otherOrg = await otherResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var otherOrgId = otherOrg.GetProperty("id").GetGuid();

        await _adminClient.PostAsJsonAsync($"/api/organizations/{otherOrgId}/events", new
        {
            title = "Other Event",
            date = "2026-07-12",
            slots = new[] { new { label = "Other Slot", startTime = "08:00", endTime = "09:00", capacity = 5 } }
        });

        // Get the other slot ID via roster
        var roster = await _adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/organizations/{otherOrgId}/roster?weekStart=2026-07-06", _jsonOptions);
        var otherSlotId = roster.EnumerateArray().First()
            .GetProperty("slots").EnumerateArray().First()
            .GetProperty("id").GetGuid();

        // Try to sign up for the other org's slot using first org's invite code
        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId = otherSlotId,
            volunteerName = "Sneaky"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Helpers ---

    private async Task<(Guid orgId, string code)> SeedInviteLinkAsync(string name)
    {
        var resp = await _adminClient.PostAsJsonAsync("/api/organizations", new { name });
        var org = await resp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var orgId = org.GetProperty("id").GetGuid();

        // Create a future event with a slot so the invite page has data
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        await _adminClient.PostAsJsonAsync($"/api/organizations/{orgId}/events", new
        {
            title = "Future Event",
            date = futureDate.ToString("yyyy-MM-dd"),
            slots = new[]
            {
                new { label = "Slot 1", startTime = "08:00", endTime = "09:00", capacity = 3 }
            }
        });

        var linkResp = await _adminClient.PostAsJsonAsync($"/api/organizations/{orgId}/invite-links", new { });
        var link = await linkResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var code = link.GetProperty("code").GetString()!;

        return (orgId, code);
    }
}
