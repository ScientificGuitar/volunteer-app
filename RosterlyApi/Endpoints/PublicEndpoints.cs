using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using RosterlyApi.Data;
using RosterlyApi.Entities;
using RosterlyApi.Validation;

namespace RosterlyApi.Endpoints;

public static class PublicEndpoints
{
    public static WebApplication MapPublicEndpoints(this WebApplication app)
    {
        var pub = app.MapGroup("/api/invite").AddEndpointFilter<ValidateDtoFilter>();

        pub.MapGet("/{code}", GetInvitePage)
            .Produces<InvitePageResponse>()
            .Produces(404);
        pub.MapPost("/{code}/signups", CreateSignup)
            .RequireRateLimiting("signup")
            .Produces<PublicSignupResponse>(201)
            .Produces(400)
            .Produces(404)
            .Produces(409);

        return app;
    }

    private static async Task<IResult> GetInvitePage(string code, AppDbContext db, CancellationToken ct)
    {
        var link = await db.InviteLinks
            .Include(l => l.Event!)
                .ThenInclude(e => e.Organization)
            .Include(l => l.Event!)
                .ThenInclude(e => e.TimeSlots)
                    .ThenInclude(s => s.Signups)
            .FirstOrDefaultAsync(l => l.Code == code && l.IsActive
                && (!l.ExpiresAt.HasValue || l.ExpiresAt.Value > DateTime.UtcNow), ct);

        if (link is null || link.Event is null)
            return Results.NotFound(new { error = "Invite link not found or expired" });

        var evt = link.Event;

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
                        s.Id, s.Label, evt.Date.ToDateTime(s.StartTime), evt.Date.ToDateTime(s.EndTime), s.Capacity, s.Signups.Count, s.Signups.Count >= s.Capacity
                    ))
            )
        ));
    }

    private static async Task<IResult> CreateSignup(string code, PublicSignupRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var link = await db.InviteLinks
            .FirstOrDefaultAsync(l => l.Code == code && l.IsActive
                && (!l.ExpiresAt.HasValue || l.ExpiresAt.Value > DateTime.UtcNow), ct);

        if (link is null || link.EventId is null)
            return Results.NotFound(new { error = "Invite link not found or expired" });

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var tx = await db.Database.BeginTransactionAsync(ct);

            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            var dbTx = db.Database.CurrentTransaction?.GetDbTransaction();

            await using var lockCmd = conn.CreateCommand();
            if (dbTx is not null) lockCmd.Transaction = dbTx;
            lockCmd.CommandText = """
                SELECT pg_advisory_xact_lock(
                    ('x' || left(replace(@slotId::text, '-', ''), 16))::bit(64)::bigint
                )
                """;
            var lockSlotParam = lockCmd.CreateParameter();
            lockSlotParam.ParameterName = "slotId";
            lockSlotParam.Value = request.SlotId;
            lockCmd.Parameters.Add(lockSlotParam);
            await lockCmd.ExecuteScalarAsync(ct);

            await using var cmd = conn.CreateCommand();
            if (dbTx is not null) cmd.Transaction = dbTx;
            cmd.CommandText = """
                SELECT t."Capacity",
                       (SELECT COUNT(*) FROM "Signups" WHERE "TimeSlotId" = t."Id") AS cnt
                FROM "TimeSlots" t
                WHERE t."Id" = @slotId AND t."EventId" = @eventId
                """;
            var slotParam = cmd.CreateParameter();
            slotParam.ParameterName = "slotId";
            slotParam.Value = request.SlotId;
            var eventParam = cmd.CreateParameter();
            eventParam.ParameterName = "eventId";
            eventParam.Value = link.EventId;
            cmd.Parameters.Add(slotParam);
            cmd.Parameters.Add(eventParam);

            int capacity;
            long signupCount;
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                    return Results.NotFound(new { error = "Time slot not found" });

                capacity = reader.GetInt32(0);
                signupCount = reader.GetInt64(1);
            }

            if (signupCount >= capacity)
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

public record PublicSignupRequest(
    Guid SlotId,
    [property: Required, NotWhitespace, StringLength(200)] string VolunteerName)
    : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SlotId == Guid.Empty)
        {
            yield return new ValidationResult(
                "SlotId is required.",
                new[] { nameof(SlotId) });
        }
    }
}

// --- Response DTOs ---

public record InvitePageResponse(Guid OrganizationId, string OrganizationName, EventPublicResponse Event);

public record EventPublicResponse(Guid Id, string Title, string? Description, DateOnly Date, IEnumerable<SlotAvailabilityResponse> Slots);

public record SlotAvailabilityResponse(Guid Id, string Label, DateTime StartTime, DateTime EndTime, int Capacity, int SignupCount, bool IsFull);

public record PublicSignupResponse(Guid Id, Guid SlotId, string VolunteerName, DateTime CreatedAt);
