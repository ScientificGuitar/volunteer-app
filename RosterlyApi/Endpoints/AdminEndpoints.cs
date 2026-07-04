using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RosterlyApi.Entities;

namespace RosterlyApi.Endpoints;

public static class AdminEndpoints
{
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api").RequireAuthorization();

        admin.MapPost("/organizations", CreateOrganization);
        admin.MapGet("/organizations/{id}", GetOrganization);

        admin.MapPost("/organizations/{orgId}/events", CreateEvent);
        admin.MapGet("/organizations/{orgId}/events", ListEvents);
        admin.MapPut("/events/{id}", UpdateEvent);
        admin.MapDelete("/events/{id}", DeleteEvent);

        admin.MapPost("/events/{eventId}/slots", CreateSlot);
        admin.MapPut("/events/{eventId}/slots/{slotId}", UpdateSlot);
        admin.MapDelete("/events/{eventId}/slots/{slotId}", DeleteSlot);

        admin.MapGet("/organizations/{orgId}/roster", GetRoster);

        admin.MapDelete("/signups/{id}", DeleteSignup);

        admin.MapGet("/events/{id}", GetEvent);

        admin.MapPost("/organizations/{orgId}/invite-links", CreateInviteLink);

        return app;
    }

    private static string GetUserId(HttpContext http) =>
        http.User.FindFirstValue("sub") ?? throw new UnauthorizedAccessException();

    private static async Task<Organization?> GetOwnedOrganization(AppDbContext db, Guid orgId, string userId, CancellationToken ct)
    {
        return await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.ClerkUserId == userId, ct);
    }

    // --- Organization ---

    private static async Task<IResult> CreateOrganization(CreateOrganizationRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ClerkUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        db.Organizations.Add(org);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/organizations/{org.Id}", new OrganizationResponse(org.Id, org.Name, org.CreatedAt));
    }

    private static async Task<IResult> GetOrganization(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);

        var org = await db.Organizations
            .Include(o => o.InviteLinks)
            .FirstOrDefaultAsync(o => o.Id == id && o.ClerkUserId == userId, ct);

        if (org is null) return Results.NotFound();

        return Results.Ok(new OrganizationDetailResponse(
            org.Id,
            org.Name,
            org.CreatedAt,
            org.InviteLinks.Select(il => new InviteLinkResponse(il.Id, il.Code, il.IsActive, il.CreatedAt))
        ));
    }

    // --- Events ---

    private static async Task<IResult> CreateEvent(Guid orgId, CreateEventRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        var org = await GetOwnedOrganization(db, orgId, userId, ct);
        if (org is null) return Results.NotFound();

        var evt = new Event
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Title = request.Title,
            Description = request.Description,
            Date = request.Date,
            CreatedAt = DateTime.UtcNow
        };

        db.Events.Add(evt);

        if (request.Slots is not null)
        {
            foreach (var s in request.Slots)
            {
                db.TimeSlots.Add(new TimeSlot
                {
                    Id = Guid.NewGuid(),
                    EventId = evt.Id,
                    Label = s.Label,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Capacity = s.Capacity,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/events/{evt.Id}", new EventResponse(
            evt.Id, evt.OrganizationId, evt.Title, evt.Description, evt.Date, evt.CreatedAt
        ));
    }

    private static async Task<IResult> ListEvents(Guid orgId, DateOnly from, DateOnly to, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);

        var orgExists = await db.Organizations.AnyAsync(o => o.Id == orgId && o.ClerkUserId == userId, ct);
        if (!orgExists) return Results.NotFound();

        var events = await db.Events
            .Where(e => e.OrganizationId == orgId && e.Date >= from && e.Date <= to)
            .Include(e => e.TimeSlots).ThenInclude(s => s.Signups)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

        return Results.Ok(events.Select(e => new EventWithSlotsResponse(
            e.Id, e.OrganizationId, e.Title, e.Description, e.Date, e.CreatedAt,
            e.TimeSlots.Select(s => new TimeSlotResponse(s.Id, s.EventId, s.Label, s.StartTime, s.EndTime, s.Capacity, s.Signups.Count))
        )));
    }

    private static async Task<IResult> UpdateEvent(Guid id, UpdateEventRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        var evt = await db.Events
            .Include(e => e.Organization)
            .FirstOrDefaultAsync(e => e.Id == id && e.Organization.ClerkUserId == userId, ct);

        if (evt is null) return Results.NotFound();

        if (request.Title is not null) evt.Title = request.Title;
        if (request.Description is not null) evt.Description = request.Description;
        if (request.Date is not null) evt.Date = request.Date.Value;

        await db.SaveChangesAsync(ct);

        return Results.Ok(new EventResponse(evt.Id, evt.OrganizationId, evt.Title, evt.Description, evt.Date, evt.CreatedAt));
    }

    private static async Task<IResult> DeleteEvent(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        var evt = await db.Events
            .Include(e => e.Organization)
            .FirstOrDefaultAsync(e => e.Id == id && e.Organization.ClerkUserId == userId, ct);

        if (evt is null) return Results.NotFound();

        db.Events.Remove(evt);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // --- Time Slots ---

    private static async Task<IResult> CreateSlot(Guid eventId, CreateSlotRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        var evt = await db.Events
            .Include(e => e.Organization)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.Organization.ClerkUserId == userId, ct);

        if (evt is null) return Results.NotFound();

        var slot = new TimeSlot
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Label = request.Label,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Capacity = request.Capacity,
            CreatedAt = DateTime.UtcNow
        };

        db.TimeSlots.Add(slot);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/events/{eventId}/slots/{slot.Id}", new TimeSlotResponse(slot.Id, slot.EventId, slot.Label, slot.StartTime, slot.EndTime, slot.Capacity, 0));
    }

    private static async Task<IResult> UpdateSlot(Guid eventId, Guid slotId, UpdateSlotRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        var slot = await db.TimeSlots
            .Include(s => s.Event).ThenInclude(e => e.Organization)
            .FirstOrDefaultAsync(s => s.Id == slotId && s.EventId == eventId && s.Event.Organization.ClerkUserId == userId, ct);

        if (slot is null) return Results.NotFound();

        if (request.Label is not null) slot.Label = request.Label;
        if (request.StartTime is not null) slot.StartTime = request.StartTime.Value;
        if (request.EndTime is not null) slot.EndTime = request.EndTime.Value;
        if (request.Capacity is not null) slot.Capacity = request.Capacity.Value;

        await db.SaveChangesAsync(ct);

        var signupCount = await db.Signups.CountAsync(s => s.TimeSlotId == slotId, ct);
        return Results.Ok(new TimeSlotResponse(slot.Id, slot.EventId, slot.Label, slot.StartTime, slot.EndTime, slot.Capacity, signupCount));
    }

    private static async Task<IResult> DeleteSlot(Guid eventId, Guid slotId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        var slot = await db.TimeSlots
            .Include(s => s.Event).ThenInclude(e => e.Organization)
            .FirstOrDefaultAsync(s => s.Id == slotId && s.EventId == eventId && s.Event.Organization.ClerkUserId == userId, ct);

        if (slot is null) return Results.NotFound();

        db.TimeSlots.Remove(slot);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // --- Roster ---

    private static async Task<IResult> GetRoster(Guid orgId, DateOnly weekStart, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        var orgExists = await db.Organizations.AnyAsync(o => o.Id == orgId && o.ClerkUserId == userId, ct);
        if (!orgExists) return Results.NotFound();

        var weekEnd = weekStart.AddDays(6);

        var events = await db.Events
            .Where(e => e.OrganizationId == orgId && e.Date >= weekStart && e.Date <= weekEnd)
            .Include(e => e.TimeSlots).ThenInclude(s => s.Signups)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

        return Results.Ok(events.Select(e => new RosterEventResponse(
            e.Id, e.Title, e.Description, e.Date,
            e.TimeSlots.Select(s => new RosterSlotResponse(
                s.Id, s.Label, s.StartTime, s.EndTime, s.Capacity,
                s.Signups.Select(su => new SignupResponse(su.Id, su.TimeSlotId, su.VolunteerName, su.CreatedAt))
            ))
        )));
    }

    // --- Signups ---

    private static async Task<IResult> DeleteSignup(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        var signup = await db.Signups
            .Include(s => s.TimeSlot).ThenInclude(s => s.Event).ThenInclude(e => e.Organization)
            .FirstOrDefaultAsync(s => s.Id == id && s.TimeSlot.Event.Organization.ClerkUserId == userId, ct);

        if (signup is null) return Results.NotFound();

        db.Signups.Remove(signup);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // --- Event Detail ---

    private static async Task<IResult> GetEvent(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        var evt = await db.Events
            .Include(e => e.Organization)
            .Include(e => e.TimeSlots).ThenInclude(s => s.Signups)
            .FirstOrDefaultAsync(e => e.Id == id && e.Organization.ClerkUserId == userId, ct);

        if (evt is null) return Results.NotFound();

        return Results.Ok(new RosterEventResponse(
            evt.Id, evt.Title, evt.Description, evt.Date,
            evt.TimeSlots.Select(s => new RosterSlotResponse(
                s.Id, s.Label, s.StartTime, s.EndTime, s.Capacity,
                s.Signups.Select(su => new SignupResponse(su.Id, su.TimeSlotId, su.VolunteerName, su.CreatedAt))
            ))
        ));
    }

    // --- Invite Links ---

    private static async Task<IResult> CreateInviteLink(Guid orgId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = GetUserId(http);
        var org = await GetOwnedOrganization(db, orgId, userId, ct);
        if (org is null) return Results.NotFound();

        var link = new InviteLink
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Code = GenerateInviteCode(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.InviteLinks.Add(link);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new InviteLinkResponse(link.Id, link.Code, link.IsActive, link.CreatedAt));
    }

    private static string GenerateInviteCode()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 8).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}

// --- Request DTOs ---

public record CreateOrganizationRequest(string Name);

public record CreateEventRequest(string Title, string? Description, DateOnly Date, List<CreateSlotRequest>? Slots);

public record UpdateEventRequest(string? Title, string? Description, DateOnly? Date);

public record CreateSlotRequest(string Label, TimeOnly StartTime, TimeOnly EndTime, int Capacity);

public record UpdateSlotRequest(string? Label, TimeOnly? StartTime, TimeOnly? EndTime, int? Capacity);

// --- Response DTOs ---

public record OrganizationResponse(Guid Id, string Name, DateTime CreatedAt);

public record OrganizationDetailResponse(Guid Id, string Name, DateTime CreatedAt, IEnumerable<InviteLinkResponse> InviteLinks);

public record InviteLinkResponse(Guid Id, string Code, bool IsActive, DateTime CreatedAt);

public record EventResponse(Guid Id, Guid OrganizationId, string Title, string? Description, DateOnly Date, DateTime CreatedAt);

public record EventWithSlotsResponse(Guid Id, Guid OrganizationId, string Title, string? Description, DateOnly Date, DateTime CreatedAt, IEnumerable<TimeSlotResponse> Slots);

public record TimeSlotResponse(Guid Id, Guid EventId, string Label, TimeOnly StartTime, TimeOnly EndTime, int Capacity, int SignupCount);

public record RosterEventResponse(Guid Id, string Title, string? Description, DateOnly Date, IEnumerable<RosterSlotResponse> Slots);

public record RosterSlotResponse(Guid Id, string Label, TimeOnly StartTime, TimeOnly EndTime, int Capacity, IEnumerable<SignupResponse> Signups);

public record SignupResponse(Guid Id, Guid TimeSlotId, string VolunteerName, DateTime CreatedAt);
