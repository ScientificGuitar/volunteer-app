using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RosterlyApi.Entities;
using Xunit;

namespace RosterlyApi.Tests;

public class CascadeDeleteTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CascadeDeleteTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteEvent_CascadesToSlotsAndSignups()
    {
        var orgId = await SeedOrgAsync("Cascade Org");

        // Create event with slot
        var createEvt = await _client.PostAsJsonAsync($"/api/organizations/{orgId}/events", new
        {
            title = "Cascade Event",
            date = "2026-07-12",
            slots = new[] { new { label = "Cascade Slot", startTime = "08:00", endTime = "09:00", capacity = 5 } }
        });
        var evt = await createEvt.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var eventId = evt.GetProperty("id").GetGuid();

        // Create invite link and signup
        var linkResp = await _client.PostAsJsonAsync($"/api/organizations/{orgId}/invite-links", new { });
        var link = await linkResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var code = link.GetProperty("code").GetString()!;

        var roster = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/organizations/{orgId}/roster?weekStart=2026-07-06", _jsonOptions);
        var slotId = roster.EnumerateArray().First()
            .GetProperty("slots").EnumerateArray().First()
            .GetProperty("id").GetGuid();

        await _client.PostAsJsonAsync($"/api/invite/{code}/signups", new { slotId, volunteerName = "Bob" });

        // Delete the event
        var deleteResp = await _client.DeleteAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Verify slots and signups are gone
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Events.AnyAsync(e => e.Id == eventId));
        Assert.False(await db.TimeSlots.AnyAsync(s => s.EventId == eventId));
        Assert.False(await db.Signups.AnyAsync(s => s.TimeSlotId == slotId));
    }

    [Fact]
    public async Task DeleteOrganization_CascadesToEventsAndSlots()
    {
        var orgId = await SeedOrgAsync("Cascade Org 2");

        await _client.PostAsJsonAsync($"/api/organizations/{orgId}/events", new
        {
            title = "Doomed Event",
            date = "2026-07-12",
            slots = new[] { new { label = "Doomed Slot", startTime = "08:00", endTime = "09:00", capacity = 2 } }
        });

        // Create invite link
        await _client.PostAsJsonAsync($"/api/organizations/{orgId}/invite-links", new { });

        // Use raw DB to delete the organization (API doesn't expose org deletion yet)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var org = await db.Organizations
                .Include(o => o.Events).ThenInclude(e => e.TimeSlots)
                .Include(o => o.InviteLinks)
                .FirstAsync(o => o.Id == orgId);
            db.Organizations.Remove(org);
            await db.SaveChangesAsync();
        }

        // Verify cascade: nothing should remain
        using var checkScope = _factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await checkDb.Organizations.AnyAsync(o => o.Id == orgId));
        Assert.False(await checkDb.Events.AnyAsync(e => e.OrganizationId == orgId));
        Assert.False(await checkDb.TimeSlots.AnyAsync(s => s.Event != null && s.Event.OrganizationId == orgId));
        Assert.False(await checkDb.InviteLinks.AnyAsync(l => l.OrganizationId == orgId));
    }

    // --- Helpers ---

    private async Task<Guid> SeedOrgAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            ClerkUserId = TestAuthHandler.TestUserId,
            CreatedAt = DateTime.UtcNow
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        return org.Id;
    }
}
