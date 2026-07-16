using Microsoft.EntityFrameworkCore;
using RosterlyApi.Entities;

namespace RosterlyApi.Endpoints;

public static class PublicEndpoints
{
    public static WebApplication MapPublicEndpoints(this WebApplication app)
    {
        var pub = app.MapGroup("/api/invite");

        pub.MapGet("/{code}", GetInvitePage);
        pub.MapPost("/{code}/signups", CreateSignup);

        return app;
    }

    private static async Task<IResult> GetInvitePage(string code, AppDbContext db, CancellationToken ct)
    {
        var link = await db.InviteLinks
            .FirstOrDefaultAsync(l => l.Code == code && l.IsActive, ct);

        if (link is null || link.EventId is null)
            return Results.NotFound(new { error = "Invite link not found or expired" });

        var evt = await db.Events
            .Include(e => e.Organization)
            .Include(e => e.TimeSlots).ThenInclude(s => s.Signups)
            .FirstOrDefaultAsync(e => e.Id == link.EventId, ct);

        if (evt is null)
            return Results.NotFound(new { error = "Invite link not found or expired" });

        return Results.Ok(new InvitePageResponse(
            evt.OrganizationId,
            evt.Organization.Name,
            new EventPublicResponse(
                evt.Id,
                evt.Title,
                evt.Description,
                evt.Date,
                evt.TimeSlots
                    .OrderBy(s => s.StartTime)
                    .Select(s => new SlotAvailabilityResponse(
                        s.Id, s.Label, s.StartTime, s.EndTime, s.Capacity, s.Signups.Count, s.Signups.Count >= s.Capacity
                    ))
            )
        ));
    }

    private static async Task<IResult> CreateSignup(string code, PublicSignupRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var link = await db.InviteLinks
            .FirstOrDefaultAsync(l => l.Code == code && l.IsActive, ct);

        if (link is null || link.EventId is null)
            return Results.NotFound(new { error = "Invite link not found or expired" });

        var slot = await db.TimeSlots
            .FirstOrDefaultAsync(s => s.Id == request.SlotId && s.EventId == link.EventId, ct);

        if (slot is null)
            return Results.NotFound(new { error = "Time slot not found" });

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var tx = await db.Database.BeginTransactionAsync(ct);

            var signupCount = await db.Signups.CountAsync(s => s.TimeSlotId == request.SlotId, ct);

            if (signupCount >= slot.Capacity)
                return Results.Conflict(new { error = "This time slot is full" });

            var signup = new Signup
            {
                Id = Guid.NewGuid(),
                TimeSlotId = request.SlotId,
                VolunteerName = request.VolunteerName,
                CreatedAt = DateTime.UtcNow
            };

            db.Signups.Add(signup);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Created($"/api/invite/{code}/signups/{signup.Id}", new PublicSignupResponse(
                signup.Id, signup.TimeSlotId, signup.VolunteerName, signup.CreatedAt
            ));
        });
    }
}

// --- Request DTOs ---

public record PublicSignupRequest(Guid SlotId, string VolunteerName);

// --- Response DTOs ---

public record InvitePageResponse(Guid OrganizationId, string OrganizationName, EventPublicResponse Event);

public record EventPublicResponse(Guid Id, string Title, string? Description, DateOnly Date, IEnumerable<SlotAvailabilityResponse> Slots);

public record SlotAvailabilityResponse(Guid Id, string Label, TimeOnly StartTime, TimeOnly EndTime, int Capacity, int SignupCount, bool IsFull);

public record PublicSignupResponse(Guid Id, Guid SlotId, string VolunteerName, DateTime CreatedAt);
