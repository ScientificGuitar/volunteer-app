using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

using RosterlyApi.Data;
using RosterlyApi.Entities;
using RosterlyApi.Services;
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
        pub.MapPost("/{code}/signups/resend", ResendSignup)
            .RequireRateLimiting("signup")
            .Produces(200)
            .Produces(400)
            .Produces(404)
            .Produces(409);

        var manage = app.MapGroup("/api/signup/manage");

        manage.MapGet("/{token}", GetSignupDetails)
            .Produces<SignupManageResponse>()
            .Produces(404);
        manage.MapPost("/{token}/cancel", CancelSignup)
            .Produces(200)
            .Produces(404);

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
                evt.Location,
                evt.Date,
                evt.TimeSlots
                    .OrderBy(s => s.StartTime)
                    .Select(s => new SlotAvailabilityResponse(
                        s.Id, s.Label, s.StartTime, s.EndTime, s.Capacity,
                        s.Signups.Count(sg => sg.Status != SignupStatus.Cancelled),
                        s.Signups.Count(sg => sg.Status != SignupStatus.Cancelled) >= s.Capacity
                    ))
            )
        ));
    }

    private static async Task<IResult> CreateSignup(
        string code,
        PublicSignupRequest request,
        AppDbContext db,
        EmailOutboxService outbox,
        IOptions<EmailOptions> emailOptions,
        CancellationToken ct)
    {
        var link = await db.InviteLinks
            .FirstOrDefaultAsync(l => l.Code == code && l.IsActive
                && (!l.ExpiresAt.HasValue || l.ExpiresAt.Value > DateTime.UtcNow), ct);

        if (link is null || link.EventId is null)
            return Results.NotFound(new { error = "Invite link not found or expired" });

        var email = NormalizeEmail(request.Email);

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

            var existingStatus = await db.Signups
                .Where(s => s.TimeSlotId == request.SlotId
                    && s.Email == email
                    && s.Status != SignupStatus.Cancelled)
                .Select(s => (SignupStatus?)s.Status)
                .FirstOrDefaultAsync(ct);

            if (existingStatus == SignupStatus.Pending)
                return Results.Conflict(new
                {
                    error = "You already have a pending signup for this slot. Check your email to confirm.",
                    code = "duplicate_pending"
                });

            if (existingStatus == SignupStatus.Confirmed)
                return Results.Conflict(new
                {
                    error = "You're already confirmed for this slot.",
                    code = "duplicate_confirmed"
                });

            await using var cmd = conn.CreateCommand();
            if (dbTx is not null) cmd.Transaction = dbTx;
            cmd.CommandText = """
                SELECT t."Capacity",
                       (SELECT COUNT(*) FROM "Signups" WHERE "TimeSlotId" = t."Id" AND "Status" <> 'Cancelled') AS cnt
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

            var rawToken = TokenService.GenerateToken();

            var signup = new Signup
            {
                Id = Guid.NewGuid(),
                TimeSlotId = request.SlotId,
                VolunteerName = request.VolunteerName,
                Email = email,
                Status = SignupStatus.Pending,
                ManagementTokenHash = TokenService.HashToken(rawToken),
                CreatedAt = DateTime.UtcNow
            };

            db.Signups.Add(signup);

            var slot = await db.TimeSlots
                .Include(s => s.Event!)
                    .ThenInclude(e => e.Organization)
                .FirstOrDefaultAsync(s => s.Id == request.SlotId, ct);

            if (slot is null || slot.Event is null)
                return Results.NotFound(new { error = "Time slot not found" });

            await EnqueueConfirmationEmail(db, outbox, emailOptions, signup, slot, rawToken, ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Created($"/api/invite/{code}/signups/{signup.Id}", new PublicSignupResponse(
                signup.Id, signup.TimeSlotId, signup.VolunteerName, signup.Email, signup.CreatedAt
            ));
        });
    }

    private static async Task<IResult> GetSignupDetails(string token, AppDbContext db, CancellationToken ct)
    {
        var hash = TokenService.HashToken(token);
        var signup = await db.Signups
            .Include(s => s.TimeSlot)
                .ThenInclude(t => t.Event)
                    .ThenInclude(e => e.Organization)
            .FirstOrDefaultAsync(s => s.ManagementTokenHash == hash, ct);

        if (signup is null || signup.TimeSlot.Event is null)
            return Results.NotFound(new { error = "Signup link not found" });

        if (signup.Status == SignupStatus.Pending)
        {
            signup.Status = SignupStatus.Confirmed;
            signup.ConfirmedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var evt = signup.TimeSlot.Event;
        return Results.Ok(new SignupManageResponse(
            signup.Id,
            signup.VolunteerName,
            signup.Email,
            signup.Status.ToString(),
            signup.ConfirmedAt,
            evt.Organization.Name,
            evt.Title,
            evt.Location,
            evt.Date,
            signup.TimeSlot.Label,
            signup.TimeSlot.StartTime,
            signup.TimeSlot.EndTime
        ));
    }

    private static async Task<IResult> CancelSignup(string token, AppDbContext db, CancellationToken ct)
    {
        var hash = TokenService.HashToken(token);
        var signup = await db.Signups
            .FirstOrDefaultAsync(s => s.ManagementTokenHash == hash, ct);

        if (signup is null)
            return Results.NotFound(new { error = "Signup link not found" });

        if (signup.Status != SignupStatus.Cancelled)
        {
            signup.Status = SignupStatus.Cancelled;
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok();
    }

    private static async Task<IResult> ResendSignup(
        string code,
        ResendSignupRequest request,
        AppDbContext db,
        EmailOutboxService outbox,
        IOptions<EmailOptions> emailOptions,
        CancellationToken ct)
    {
        var link = await db.InviteLinks
            .FirstOrDefaultAsync(l => l.Code == code && l.IsActive
                && (!l.ExpiresAt.HasValue || l.ExpiresAt.Value > DateTime.UtcNow), ct);

        if (link is null || link.EventId is null)
            return Results.NotFound(new { error = "Invite link not found or expired" });

        var email = NormalizeEmail(request.Email);

        var signup = await db.Signups
            .Include(s => s.TimeSlot)
                .ThenInclude(t => t.Event!)
                    .ThenInclude(e => e.Organization)
            .FirstOrDefaultAsync(s => s.TimeSlotId == request.SlotId
                && s.Email == email
                && s.TimeSlot.EventId == link.EventId, ct);

        if (signup is null)
            return Results.NotFound(new { error = "No pending signup found for this email and slot" });

        if (signup.Status == SignupStatus.Confirmed)
            return Results.Conflict(new { error = "You're already confirmed for this slot." });

        if (signup.Status == SignupStatus.Cancelled)
            return Results.NotFound(new { error = "No pending signup found for this email and slot" });

        var rawToken = TokenService.GenerateToken();
        signup.ManagementTokenHash = TokenService.HashToken(rawToken);

        await EnqueueConfirmationEmail(db, outbox, emailOptions, signup, signup.TimeSlot, rawToken, ct);

        return Results.Ok();
    }

    private static async Task EnqueueConfirmationEmail(
        AppDbContext db,
        EmailOutboxService outbox,
        IOptions<EmailOptions> emailOptions,
        Signup signup,
        TimeSlot slot,
        string rawToken,
        CancellationToken ct)
    {
        var manageUrl = $"{emailOptions.Value.BaseUrl.TrimEnd('/')}/signup/manage/{rawToken}";
        var evt = slot.Event;
        var links = CalendarInviteBuilder.BuildLinks(
            evt.Title,
            evt.Description,
            evt.Location,
            evt.Date,
            slot.StartTime,
            slot.EndTime,
            slot.Label,
            manageUrl);
        var ics = CalendarInviteBuilder.BuildIcs(
            evt.Title,
            evt.Description,
            evt.Location,
            evt.Date,
            slot.StartTime,
            slot.EndTime,
            slot.Label,
            manageUrl,
            signup.Id.ToString());
        var (subject, html, text) = EmailTemplates.BuildSignupConfirmation(
            signup.VolunteerName,
            slot.Event.Organization.Name,
            slot.Event.Title,
            slot.Event.Date,
            slot.StartTime,
            slot.EndTime,
            manageUrl,
            evt.Location,
            links,
            hasCalendarAttachment: true);

        var attachment = new EmailAttachment(
            CalendarInviteBuilder.IcsFileName(evt.Title),
            "text/calendar",
            System.Text.Encoding.UTF8.GetBytes(ics));

        await outbox.EnqueueAsync(signup.Email, subject, html, text, attachment, ct);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

// --- Request DTOs ---

public record PublicSignupRequest(
    Guid SlotId,
    [property: Required, NotWhitespace, StringLength(200)] string VolunteerName,
    [property: Required, EmailAddress, StringLength(320)] string Email)
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

public record ResendSignupRequest(
    [property: Required] Guid SlotId,
    [property: Required, EmailAddress, StringLength(320)] string Email)
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

public record EventPublicResponse(Guid Id, string Title, string? Description, string? Location, DateOnly Date, IEnumerable<SlotAvailabilityResponse> Slots);

public record SlotAvailabilityResponse(Guid Id, string Label, TimeOnly StartTime, TimeOnly EndTime, int Capacity, int SignupCount, bool IsFull);

public record PublicSignupResponse(Guid Id, Guid SlotId, string VolunteerName, string Email, DateTime CreatedAt);

public record SignupManageResponse(
    Guid SignupId,
    string VolunteerName,
    string Email,
    string Status,
    DateTime? ConfirmedAt,
    string OrganizationName,
    string EventTitle,
    string? EventLocation,
    DateOnly EventDate,
    string SlotLabel,
    TimeOnly StartTime,
    TimeOnly EndTime);