using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using RosterlyApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var clerkIssuer = builder.Configuration["Clerk:Issuer"]
    ?? throw new InvalidOperationException("Clerk:Issuer is not configured");

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
    });

builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

app.MapGet("/api/user/me", async (HttpContext http, AppDbContext db) =>
{
    var userId = http.User.FindFirst("sub")?.Value;
    if (userId is null) return Results.Unauthorized();

    var org = await db.Organizations.FirstOrDefaultAsync(o => o.ClerkUserId == userId);

    return Results.Ok(new
    {
        UserId = userId,
        Organization = org is null ? null : new { org.Id, org.Name }
    });
}).RequireAuthorization();

app.MapAdminEndpoints();
app.MapPublicEndpoints();

app.Run();

public partial class Program { }
