using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RivianMate.Api.Components;
using RivianMate.Api.Middleware;
using RivianMate.Api.Services;
using RivianMate.Api.Services.Jobs;
using RivianMate.Core.Entities;
using RivianMate.Core.Interfaces;
using RivianMate.Infrastructure.Data;
using RivianMate.Infrastructure.Nhtsa;
using RivianMate.Infrastructure.Rivian;

var builder = WebApplication.CreateBuilder(args);

// === Current User Accessor (for ownership validation) ===
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

// === Database ===
// In Development with DatabaseProvider=Sqlite, uses local SQLite file
// Otherwise uses PostgreSQL with connection string priority:
// 1. DATABASE_URL env var (Docker/Unraid)
// 2. Build from POSTGRES_* env vars
// 3. ConnectionStrings__DefaultConnection env var
// 4. appsettings.json

var useSqlite = builder.Configuration["DatabaseProvider"]?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

string GetPostgresConnectionString()
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(databaseUrl))
        return databaseUrl;

    var host = Environment.GetEnvironmentVariable("POSTGRES_HOST");
    if (!string.IsNullOrEmpty(host))
    {
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var db = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "rivianmate";
        var user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "rivianmate";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "rivianmate";
        return $"Host={host};Port={port};Database={db};Username={user};Password={password}";
    }

    return builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Database=rivianmate;Username=rivianmate;Password=rivianmate";
}

string? postgresConnectionString = null;

if (useSqlite)
{
    var sqliteConnection = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=rivianmate.db";
    // Use DbContextFactory for Blazor Server to avoid disposed context issues
    builder.Services.AddDbContextFactory<RivianMateDbContext>(options =>
        options.UseSqlite(sqliteConnection));
    // Register scoped DbContext with ownership validation services injected
    builder.Services.AddScoped(sp =>
    {
        var options = sp.GetRequiredService<DbContextOptions<RivianMateDbContext>>();
        var currentUserAccessor = sp.GetService<ICurrentUserAccessor>();
        var logger = sp.GetService<ILogger<RivianMateDbContext>>();
        return new RivianMateDbContext(options, currentUserAccessor, logger);
    });
}
else
{
    postgresConnectionString = GetPostgresConnectionString();
    // Use DbContextFactory for Blazor Server to avoid disposed context issues
    builder.Services.AddDbContextFactory<RivianMateDbContext>(options =>
        options.UseNpgsql(postgresConnectionString, npgsqlOptions =>
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null)));
    // Register scoped DbContext with ownership validation services injected
    builder.Services.AddScoped(sp =>
    {
        var options = sp.GetRequiredService<DbContextOptions<RivianMateDbContext>>();
        var currentUserAccessor = sp.GetService<ICurrentUserAccessor>();
        var logger = sp.GetService<ILogger<RivianMateDbContext>>();
        return new RivianMateDbContext(options, currentUserAccessor, logger);
    });
}

// === Hangfire (Distributed Job Scheduling) ===
// Uses PostgreSQL for job storage in production, in-memory for SQLite development
if (useSqlite)
{
    // In-memory storage for development (jobs won't persist across restarts)
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseInMemoryStorage());
}
else
{
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options =>
            options.UseNpgsqlConnection(postgresConnectionString)));
}

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount * 2;
    options.Queues = new[] { "default", "polling" };
});

// === Data Protection (for encrypting tokens) ===
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<RivianMateDbContext>()
    .SetApplicationName("RivianMate");

// === Identity ===
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    // Password requirements
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredUniqueChars = 4;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false; // Can enable later with email confirmation
})
.AddEntityFrameworkStores<RivianMateDbContext>()
.AddDefaultTokenProviders();

// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
});

// Add authorization services
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider>();

// === HTTP Client for Rivian API ===
// Note: We use named clients. Cookies may be required by Rivian's API during auth flow.
builder.Services.AddHttpClient(nameof(RivianApiClient), client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "RivianMate/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// === NHTSA VIN Decoder Service ===
builder.Services.AddHttpClient<NhtsaVinDecoderService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "RivianMate/1.0");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// === Application Services ===
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<LicenseService>();
builder.Services.AddScoped<FeatureService>();
builder.Services.AddScoped<TimeZoneService>();
builder.Services.AddSingleton<VehicleStateBuffer>(); // Singleton to maintain state across requests
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<BatteryHealthService>();
builder.Services.AddScoped<BatteryCareService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<RivianAccountService>();
builder.Services.AddScoped<DashboardConfigService>();
builder.Services.AddScoped<DriveTrackingService>();
builder.Services.AddScoped<ChargingTrackingService>();
builder.Services.AddScoped<DevDataSeeder>();

// === Polling Job Services ===
builder.Services.AddScoped<AccountPollingJob>();
builder.Services.AddScoped<PollingJobManager>();
builder.Services.AddHostedService<PollingJobSynchronizer>();

// === Blazor ===
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// === Database Migration ===
// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RivianMateDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var isSqlite = config["DatabaseProvider"]?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    if (isSqlite)
    {
        // SQLite - create schema from model (for development only)
        logger.LogInformation("Using SQLite database, creating schema...");
        await db.Database.EnsureCreatedAsync();

        // Ensure new columns exist (EnsureCreated doesn't update existing tables)
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Vehicles ADD COLUMN ImageData BLOB");
            logger.LogInformation("Added ImageData column to Vehicles table");
        }
        catch { /* Column already exists */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Vehicles ADD COLUMN ImageContentType TEXT");
            logger.LogInformation("Added ImageContentType column to Vehicles table");
        }
        catch { /* Column already exists */ }

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Vehicles ADD COLUMN ImageVersion INTEGER");
            logger.LogInformation("Added ImageVersion column to Vehicles table");
        }
        catch { /* Column already exists */ }

        logger.LogInformation("SQLite database ready");

        // Seed development data for SQLite
        var seeder = scope.ServiceProvider.GetRequiredService<DevDataSeeder>();
        await seeder.SeedAsync();
    }
    else
    {
        // PostgreSQL - retry logic for Docker startup timing
        const int maxRetries = 10;
        const int delaySeconds = 3;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                logger.LogInformation("Attempting database connection (attempt {Attempt}/{MaxRetries})...", attempt, maxRetries);

                if (await db.Database.CanConnectAsync())
                {
                    logger.LogInformation("Applying database migrations...");
                    await db.Database.MigrateAsync();
                    logger.LogInformation("Database migrations applied successfully");
                    break;
                }

                throw new Exception("Database connection check failed");
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    logger.LogError(ex, "Database initialization failed after {MaxRetries} attempts", maxRetries);
                    throw;
                }

                logger.LogWarning("Database not ready, waiting {Delay}s before retry... ({Message})", delaySeconds, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }
}

// === Middleware ===
app.UseExceptionHandling();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire Dashboard (admin only in production)
var adminEmails = builder.Configuration.GetSection("RivianMate:AdminEmails").Get<string[]>() ?? [];
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[]
    {
        new HangfireAuthorizationFilter(app.Environment.IsDevelopment(), adminEmails)
    },
    DashboardTitle = "RivianMate Jobs"
});

app.UseAntiforgery();

// === API Endpoints ===
// Vehicle image endpoint
app.MapGet("/api/vehicles/{vehicleId:int}/image", async (
    int vehicleId,
    RivianMateDbContext db,
    HttpContext httpContext) =>
{
    // Check authentication
    if (httpContext.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    var vehicle = await db.Vehicles
        .Where(v => v.Id == vehicleId)
        .Select(v => new { v.ImageData, v.ImageContentType, v.OwnerId })
        .FirstOrDefaultAsync();

    if (vehicle == null)
        return Results.NotFound();

    // Security check - verify user owns this vehicle
    var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId) || vehicle.OwnerId != userId)
        return Results.Forbid();

    if (vehicle.ImageData == null || vehicle.ImageData.Length == 0)
        return Results.NotFound();

    return Results.File(vehicle.ImageData, vehicle.ImageContentType ?? "image/png");
}).RequireAuthorization();

// Drive positions endpoint (for map plotting)
app.MapGet("/api/drives/{driveId:int}/positions", async (
    int driveId,
    RivianMateDbContext db,
    HttpContext httpContext) =>
{
    // Check authentication
    if (httpContext.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    // Get drive and verify ownership
    var drive = await db.Drives
        .Include(d => d.Vehicle)
        .FirstOrDefaultAsync(d => d.Id == driveId);

    if (drive == null)
        return Results.NotFound();

    // Security check - verify user owns this vehicle
    if (drive.Vehicle.OwnerId != userId)
        return Results.Forbid();

    // Get positions ordered by timestamp
    var positions = await db.Positions
        .Where(p => p.DriveId == driveId)
        .OrderBy(p => p.Timestamp)
        .Select(p => new
        {
            p.Timestamp,
            p.Latitude,
            p.Longitude,
            p.Altitude,
            p.Speed,
            p.Heading,
            p.BatteryLevel,
            p.Odometer
        })
        .ToListAsync();

    return Results.Ok(new
    {
        DriveId = driveId,
        StartTime = drive.StartTime,
        EndTime = drive.EndTime,
        DistanceMiles = drive.DistanceMiles,
        PositionCount = positions.Count,
        Positions = positions
    });
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
