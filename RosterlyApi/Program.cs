using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RosterlyApi.Endpoints;
using RosterlyApi.Validation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure()
    ));

var clerkIssuer = builder.Configuration["Clerk:Issuer"]
    ?? throw new InvalidOperationException("Clerk:Issuer is not configured");

var clerkAuthorizedPartiesRaw = builder.Configuration["Clerk:AuthorizedParties"]
    ?? throw new InvalidOperationException("Clerk:AuthorizedParties is not configured");

var clerkAuthorizedParties = clerkAuthorizedPartiesRaw
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(p => p.TrimEnd('/'))
    .ToArray();

if (clerkAuthorizedParties.Length == 0)
    throw new InvalidOperationException("Clerk:AuthorizedParties must contain at least one party");

foreach (var party in clerkAuthorizedParties)
{
    if (!Uri.TryCreate(party, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "http" && uri.Scheme != "https"))
    {
        throw new InvalidOperationException($"Invalid authorized party URI: '{party}'");
    }
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = clerkIssuer;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = clerkIssuer,
            ValidateAudience = false,
            ValidateLifetime = true
        };
        options.Events = new()
        {
            OnTokenValidated = ctx =>
            {
                var azp = ctx.Principal?.FindFirst("azp")?.Value?.TrimEnd('/');
                if (azp is null || !clerkAuthorizedParties.Contains(azp, StringComparer.OrdinalIgnoreCase))
                {
                    ctx.Fail("Unauthorized authorized party.");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:80",
                "http://localhost"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("StartupMigration");
        logger.LogCritical(ex, "Database migration failed at startup; exiting.");
        throw;
    }
}

app.UseCors("AllowFrontend");

app.UseExceptionHandler(eh => eh.Run(async ctx =>
{
    var feature = ctx.Features.Get<IExceptionHandlerFeature>();
    var ex = feature?.Error;

    var (status, title) = ex switch
    {
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
        BadHttpRequestException bad => (bad.StatusCode, "Bad request"),
        DbUpdateException dbEx when DbConflictDetector.IsConflict(dbEx) => (StatusCodes.Status409Conflict, "Database conflict"),
        DbUpdateException dbEx when DbConflictDetector.IsClientReferenceError(dbEx) => (StatusCodes.Status400BadRequest, "Bad request"),
        DbUpdateException => (StatusCodes.Status500InternalServerError, "Server error"),
        _ => (StatusCodes.Status500InternalServerError, "Server error")
    };

    var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("GlobalExceptionHandler");
    if (status == StatusCodes.Status500InternalServerError)
        logger.LogError(ex, "Unhandled exception");
    else
        logger.LogWarning(ex, "Request failed with {Status}", status);

    ctx.Response.StatusCode = status;
    ctx.Response.ContentType = "application/problem+json";
    await ctx.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = status,
        Title = title,
        Type = status switch
        {
            StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            _ => "about:blank"
        },
        Instance = ctx.Request.Path
    });
}));

app.MapHealthChecks("/health");

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/api/user/me", async (HttpContext http, AppDbContext db) =>
{
    var userId = http.User.FindFirst("sub")?.Value;
    if (userId is null) return Results.Unauthorized();

    var org = await db.Organizations.FirstOrDefaultAsync(o => o.ClerkUserId == userId);

    return Results.Ok(new UserMeResponse(
        userId,
        org is null ? null : new UserMeOrganization(org.Id, org.Name)
    ));
})
.RequireAuthorization()
.Produces<UserMeResponse>()
.Produces(401);

app.MapAdminEndpoints();
app.MapPublicEndpoints();

app.Run();

public record UserMeResponse(string UserId, UserMeOrganization? Organization);
public record UserMeOrganization(Guid Id, string Name);

public partial class Program { }
