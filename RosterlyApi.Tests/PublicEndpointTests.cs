using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RosterlyApi.Data;
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
            volunteerName = "Alice",
            email = "alice@example.com"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("Alice", body.GetProperty("volunteerName").GetString());
        Assert.Equal("alice@example.com", body.GetProperty("email").GetString());
        Assert.Equal(slotId, body.GetProperty("slotId").GetGuid());
    }

    [Fact]
    public async Task CreateSignup_MissingEmail_Returns400()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Missing Email Org");

        var getPage = await _publicClient.GetAsync($"/api/invite/{code}");
        var page = await getPage.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var slotId = page.GetProperty("event").GetProperty("slots").EnumerateArray().First().GetProperty("id").GetGuid();

        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "No Email"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSignup_InvalidEmail_Returns400()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Invalid Email Org");

        var getPage = await _publicClient.GetAsync($"/api/invite/{code}");
        var page = await getPage.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var slotId = page.GetProperty("event").GetProperty("slots").EnumerateArray().First().GetProperty("id").GetGuid();

        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Bad Email",
            email = "not-an-email"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSignup_ValidRequest_StoresPendingWithTokenHash()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Token Org");

        var getPage = await _publicClient.GetAsync($"/api/invite/{code}");
        var page = await getPage.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var slotId = page.GetProperty("event").GetProperty("slots").EnumerateArray().First().GetProperty("id").GetGuid();

        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Token User",
            email = "token@example.com"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var signup = await db.Signups.SingleAsync(s => s.Email == "token@example.com");

        Assert.Equal(SignupStatus.Pending, signup.Status);
        Assert.False(string.IsNullOrEmpty(signup.ManagementTokenHash));
        Assert.Equal(64, signup.ManagementTokenHash.Length);

        // A confirmation email should have been enqueued to the outbox
        var message = await db.EmailMessages.SingleAsync(m => m.To == "token@example.com");
        Assert.False(message.Sent);
        Assert.Contains("/signup/manage/", message.HtmlBody);
    }

    [Fact]
    public async Task GetSignupDetails_ValidToken_AutoConfirmsAndReturnsDetails()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Manage Org");

        var getPage = await _publicClient.GetAsync($"/api/invite/{code}");
        var page = await getPage.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var slotId = page.GetProperty("event").GetProperty("slots").EnumerateArray().First().GetProperty("id").GetGuid();

        var resp = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Manager",
            email = "manager@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        string rawToken;
        Guid signupId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var signup = await db.Signups.SingleAsync(s => s.Email == "manager@example.com");
            signupId = signup.Id;
            var message = await db.EmailMessages.SingleAsync(m => m.To == "manager@example.com");
            rawToken = message.HtmlBody
                .Substring(message.HtmlBody.IndexOf("/signup/manage/", StringComparison.Ordinal) + "/signup/manage/".Length)
                .Split('"')[0];
        }

        var manageResp = await _publicClient.GetAsync($"/api/signup/manage/{rawToken}");
        Assert.Equal(HttpStatusCode.OK, manageResp.StatusCode);
        var body = await manageResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        Assert.Equal(signupId, body.GetProperty("signupId").GetGuid());
        Assert.Equal("Manager", body.GetProperty("volunteerName").GetString());
        Assert.Equal("manager@example.com", body.GetProperty("email").GetString());
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.Equal("Manage Org", body.GetProperty("organizationName").GetString());
        Assert.Equal("Future Event", body.GetProperty("eventTitle").GetString());
        Assert.Equal("Slot 1", body.GetProperty("slotLabel").GetString());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var confirmed = await verifyDb.Signups.SingleAsync(s => s.Id == signupId);
        Assert.Equal(SignupStatus.Confirmed, confirmed.Status);
        Assert.NotNull(confirmed.ConfirmedAt);
    }

    [Fact]
    public async Task CancelSignup_ValidToken_MarksCancelled()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Cancel Org");

        var getPage = await _publicClient.GetAsync($"/api/invite/{code}");
        var page = await getPage.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var slotId = page.GetProperty("event").GetProperty("slots").EnumerateArray().First().GetProperty("id").GetGuid();

        var resp = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Canceller",
            email = "cancel@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        string rawToken;
        Guid signupId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var signup = await db.Signups.SingleAsync(s => s.Email == "cancel@example.com");
            signupId = signup.Id;
            var message = await db.EmailMessages.SingleAsync(m => m.To == "cancel@example.com");
            rawToken = message.HtmlBody
                .Substring(message.HtmlBody.IndexOf("/signup/manage/", StringComparison.Ordinal) + "/signup/manage/".Length)
                .Split('"')[0];
        }

        var cancelResp = await _publicClient.PostAsync($"/api/signup/manage/{rawToken}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cancelled = await verifyDb.Signups.SingleAsync(s => s.Id == signupId);
        Assert.Equal(SignupStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task GetSignupDetails_InvalidToken_Returns404()
    {
        var response = await _publicClient.GetAsync("/api/signup/manage/not-a-real-token");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
                volunteerName = $"Volunteer {i + 1}",
                email = $"volunteer{i + 1}@example.com"
            });
            Assert.Equal(HttpStatusCode.Created, signupResp.StatusCode);
        }

        // One more should fail
        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Extra Person",
            email = "extra@example.com"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateSignup_InvalidCode_Returns404()
    {
        var response = await _publicClient.PostAsJsonAsync("/api/invite/badcode/signups", new
        {
            slotId = Guid.NewGuid(),
            volunteerName = "Nobody",
            email = "nobody@example.com"
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
            volunteerName = "Sneaky",
            email = "sneaky@example.com"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSignup_DuplicatePendingSameEmailAndSlot_Returns409WithCode()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Dup Pending Org");

        var page = await GetInvitePageJsonAsync(code);
        var slotId = page.Slots.First().Id;

        var first = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Dup User",
            email = "dup@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Dup User",
            email = "dup@example.com"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("duplicate_pending", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateSignup_DuplicateConfirmedSameEmailAndSlot_Returns409WithCode()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Dup Confirmed Org");

        var page = await GetInvitePageJsonAsync(code);
        var slotId = page.Slots.First().Id;

        var first = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Confirmed Dup",
            email = "confdup@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Confirm it by hitting the manage page
        string rawToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            rawToken = ExtractManageToken(db, "confdup@example.com");
        }
        var confirm = await _publicClient.GetAsync($"/api/signup/manage/{rawToken}");
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Confirmed Dup",
            email = "confdup@example.com"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("duplicate_confirmed", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateSignup_CaseInsensitiveDuplicate_Returns409()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Case Org");

        var page = await GetInvitePageJsonAsync(code);
        var slotId = page.Slots.First().Id;

        var first = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Case User",
            email = "Case@Example.COM"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Case User",
            email = "case@example.com"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateSignup_SameEmailDifferentSlot_Returns201()
    {
        var (orgId, eventId, code) = await SeedInviteLinkAsync("Multi Slot Org");

        var slotResp = await _adminClient.PostAsJsonAsync($"/api/events/{eventId}/slots", new
        {
            label = "Slot 2",
            startTime = "10:00",
            endTime = "11:00",
            capacity = 3
        });
        Assert.Equal(HttpStatusCode.Created, slotResp.StatusCode);

        var page = await GetInvitePageJsonAsync(code);
        Assert.Equal(2, page.Slots.Length);
        var slot1 = page.Slots[0].Id;
        var slot2 = page.Slots[1].Id;

        var first = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId = slot1,
            volunteerName = "Multi",
            email = "multi@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId = slot2,
            volunteerName = "Multi",
            email = "multi@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task CreateSignup_ReSignupAfterCancel_Returns201()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Re Signup Org");

        var page = await GetInvitePageJsonAsync(code);
        var slotId = page.Slots.First().Id;

        var first = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Re User",
            email = "re@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        string rawToken;
        Guid cancelledId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            cancelledId = db.Signups.Single(s => s.Email == "re@example.com").Id;
            rawToken = ExtractManageToken(db, "re@example.com");
        }

        var cancel = await _publicClient.PostAsync($"/api/signup/manage/{rawToken}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var again = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Re User",
            email = "re@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await verifyDb.Signups.Where(s => s.Email == "re@example.com").ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, s => s.Id == cancelledId && s.Status == SignupStatus.Cancelled);
        Assert.Contains(rows, s => s.Status == SignupStatus.Pending);
    }

    [Fact]
    public async Task ResendSignup_Pending_Returns200RotatesTokenAndEnqueuesEmail()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Resend Org");

        var page = await GetInvitePageJsonAsync(code);
        var slotId = page.Slots.First().Id;

        var first = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "Resend User",
            email = "resend@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        string originalHash;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            originalHash = db.Signups.Single(s => s.Email == "resend@example.com").ManagementTokenHash;
        }

        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups/resend", new
        {
            slotId,
            email = "resend@example.com"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var signup = await verifyDb.Signups.SingleAsync(s => s.Email == "resend@example.com");
        Assert.NotEqual(originalHash, signup.ManagementTokenHash);
        Assert.Equal(SignupStatus.Pending, signup.Status);

        var emails = await verifyDb.EmailMessages.Where(m => m.To == "resend@example.com").ToListAsync();
        Assert.Equal(2, emails.Count);
    }

    [Fact]
    public async Task ResendSignup_Confirmed_Returns409()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Resend Confirmed Org");

        var page = await GetInvitePageJsonAsync(code);
        var slotId = page.Slots.First().Id;

        var first = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups", new
        {
            slotId,
            volunteerName = "RC User",
            email = "rc@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        string rawToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            rawToken = ExtractManageToken(db, "rc@example.com");
        }
        var confirm = await _publicClient.GetAsync($"/api/signup/manage/{rawToken}");
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups/resend", new
        {
            slotId,
            email = "rc@example.com"
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ResendSignup_UnknownEmail_Returns404()
    {
        var (_, _, code) = await SeedInviteLinkAsync("Resend Missing Org");

        var page = await GetInvitePageJsonAsync(code);
        var slotId = page.Slots.First().Id;

        var response = await _publicClient.PostAsJsonAsync($"/api/invite/{code}/signups/resend", new
        {
            slotId,
            email = "nobody@example.com"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Helpers ---

    private sealed record InviteSlotTestData(Guid Id);

    private sealed record InvitePageTestData(InviteSlotTestData[] Slots);

    private async Task<InvitePageTestData> GetInvitePageJsonAsync(string code)
    {
        var resp = await _publicClient.GetAsync($"/api/invite/{code}");
        var page = await resp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var slots = page.GetProperty("event").GetProperty("slots").EnumerateArray()
            .Select(s => new InviteSlotTestData(s.GetProperty("id").GetGuid()))
            .ToArray();
        return new InvitePageTestData(slots);
    }

    private static string ExtractManageToken(AppDbContext db, string email)
    {
        var message = db.EmailMessages.Single(m => m.To == email);
        const string marker = "/signup/manage/";
        var start = message.HtmlBody.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return message.HtmlBody.Substring(start).Split('"')[0];
    }

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
