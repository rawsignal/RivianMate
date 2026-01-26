using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RivianMate.Api.Components;
using RivianMate.Api.Configuration;
using RivianMate.Api.Middleware;
using RivianMate.Api.Services;
using RivianMate.Api.Services.Jobs;
using RivianMate.Core.Enums;
using RivianMate.Core.Entities;
using RivianMate.Core.Interfaces;
using RivianMate.Infrastructure.Data;
using RivianMate.Infrastructure.Nhtsa;
using RivianMate.Infrastructure.Rivian;
using RivianMate.Api.Services.Email;
using Tables = RivianMate.Infrastructure.Data.TableNames;

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

// === NHTSA Services ===
builder.Services.AddHttpClient<NhtsaVinDecoderService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "RivianMate/1.0");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<NhtsaRecallService>(client =>
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
builder.Services.AddSingleton<VehicleStateNotifier>(); // Singleton to notify UI of state changes
builder.Services.AddScoped<ActivityFeedService>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<BatteryHealthService>();
builder.Services.AddScoped<BatteryCareService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<RivianAccountService>();
builder.Services.AddScoped<DashboardConfigService>();
builder.Services.AddScoped<DriveTrackingService>();
builder.Services.AddScoped<ChargingTrackingService>();
builder.Services.AddScoped<UserPreferencesService>();
builder.Services.AddScoped<UserLocationService>();
builder.Services.AddScoped<UnitConversionService>();
builder.Services.AddScoped<TwoFactorService>();
builder.Services.AddHttpClient<GeocodingService>();
builder.Services.AddScoped<GeocodeAddressJob>();
builder.Services.AddScoped<ReferralService>();
builder.Services.AddScoped<DevDataSeeder>();

// === Email Services ===
builder.Services.AddEmailServices(builder.Configuration);

// === Polling/WebSocket Configuration ===
builder.Services.Configure<PollingConfiguration>(
    builder.Configuration.GetSection("RivianMate:Polling"));

// === Data Retention Configuration ===
builder.Services.Configure<DataRetentionConfiguration>(
    builder.Configuration.GetSection("RivianMate:DataRetention"));

// === Two-Factor Authentication Configuration ===
builder.Services.Configure<TwoFactorConfiguration>(
    builder.Configuration.GetSection("RivianMate:TwoFactor"));

// === Background Job Services ===
builder.Services.AddScoped<DataRetentionJob>();
builder.Services.AddScoped<DataExportService>();
builder.Services.AddScoped<DataExportJob>();
builder.Services.AddScoped<ExportCleanupJob>();

// === WebSocket Subscription Service (conditionally enabled) ===
var pollingMode = builder.Configuration.GetValue<string>("RivianMate:Polling:Mode") ?? "GraphQL";
if (pollingMode.Equals("WebSocket", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<WebSocketSubscriptionService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<WebSocketSubscriptionService>());
}

// === Subscription Manager (unified interface for both modes) ===
builder.Services.AddScoped<SubscriptionManager>();

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

        // EnsureCreated doesn't update existing tables, so we may need to add new columns
        // Check column existence first to avoid noisy error logs
        async Task<bool> ColumnExistsAsync(string table, string column)
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info({table})";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        async Task AddColumnIfNotExistsAsync(string table, string column, string type)
        {
            if (!await ColumnExistsAsync(table, column))
            {
                // Table/column/type are hardcoded constants, not user input
                #pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync($"ALTER TABLE {table} ADD COLUMN {column} {type}");
                #pragma warning restore EF1002
                logger.LogInformation("Added {Column} column to {Table} table", column, table);
            }
        }

        await AddColumnIfNotExistsAsync(Tables.Vehicles, "ImageData", "BLOB");
        await AddColumnIfNotExistsAsync(Tables.Vehicles, "ImageContentType", "TEXT");
        await AddColumnIfNotExistsAsync(Tables.Vehicles, "ImageVersion", "INTEGER");
        await AddColumnIfNotExistsAsync(Tables.Vehicles, "BuildDate", "TEXT");
        await AddColumnIfNotExistsAsync(Tables.Positions, "Gear", "INTEGER DEFAULT 0");

        // Battery health snapshot smoothing columns
        await AddColumnIfNotExistsAsync(Tables.BatteryHealthSnapshots, "SmoothedCapacityKwh", "REAL");
        await AddColumnIfNotExistsAsync(Tables.BatteryHealthSnapshots, "SmoothedHealthPercent", "REAL");
        await AddColumnIfNotExistsAsync(Tables.BatteryHealthSnapshots, "ReadingConfidence", "REAL");

        // Charging session location FK
        await AddColumnIfNotExistsAsync(Tables.ChargingSessions, "UserLocationId", "INTEGER");

        // Create UserPreferences table if it doesn't exist (for existing SQLite databases)
        async Task<bool> TableExistsAsync(string table)
        {
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{table}'";
                var result = await cmd.ExecuteScalarAsync();
                return result != null;
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        if (!await TableExistsAsync(Tables.UserPreferences))
        {
            #pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE {Tables.UserPreferences} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT NOT NULL,
                    DistanceUnit INTEGER NOT NULL DEFAULT 0,
                    TemperatureUnit INTEGER NOT NULL DEFAULT 0,
                    TirePressureUnit INTEGER NOT NULL DEFAULT 0,
                    HomeElectricityRate REAL,
                    CurrencyCode TEXT NOT NULL DEFAULT 'USD',
                    HomeLatitude REAL,
                    HomeLongitude REAL,
                    TimeZoneId TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    FOREIGN KEY (UserId) REFERENCES {Tables.Users}(Id) ON DELETE CASCADE
                )");
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE UNIQUE INDEX IX_{Tables.UserPreferences}_UserId ON {Tables.UserPreferences}(UserId)");
            #pragma warning restore EF1002
            logger.LogInformation("Created {Table} table", Tables.UserPreferences);
        }
        else
        {
            // Add TimeZoneId column if it doesn't exist (for existing UserPreferences tables)
            await AddColumnIfNotExistsAsync(Tables.UserPreferences, "TimeZoneId", "TEXT");
        }

        // Create UserLocations table if it doesn't exist
        if (!await TableExistsAsync(Tables.UserLocations))
        {
            #pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE {Tables.UserLocations} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT NOT NULL,
                    Name TEXT NOT NULL DEFAULT 'Home',
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    IsDefault INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    FOREIGN KEY (UserId) REFERENCES {Tables.Users}(Id) ON DELETE CASCADE
                )");
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE INDEX IX_{Tables.UserLocations}_UserId ON {Tables.UserLocations}(UserId)");
            #pragma warning restore EF1002
            logger.LogInformation("Created {Table} table", Tables.UserLocations);

            // Migrate existing home locations from UserPreferences
            #pragma warning disable EF1002
            var migrated = await db.Database.ExecuteSqlRawAsync($@"
                INSERT INTO {Tables.UserLocations} (UserId, Name, Latitude, Longitude, IsDefault, CreatedAt, UpdatedAt)
                SELECT UserId, 'Home', HomeLatitude, HomeLongitude, 1, datetime('now'), datetime('now')
                FROM {Tables.UserPreferences}
                WHERE HomeLatitude IS NOT NULL AND HomeLongitude IS NOT NULL");
            #pragma warning restore EF1002
            if (migrated > 0)
            {
                logger.LogInformation("Migrated {Count} home locations from {From} to {To}", migrated, Tables.UserPreferences, Tables.UserLocations);
            }
        }

        // Create GeocodingCache table if it doesn't exist
        if (!await TableExistsAsync(Tables.GeocodingCache))
        {
            #pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE {Tables.GeocodingCache} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    Address TEXT NOT NULL,
                    ShortAddress TEXT,
                    City TEXT,
                    State TEXT,
                    Country TEXT,
                    CreatedAt TEXT NOT NULL
                )");
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE UNIQUE INDEX IX_{Tables.GeocodingCache}_Lat_Lon ON {Tables.GeocodingCache}(Latitude, Longitude)");
            #pragma warning restore EF1002
            logger.LogInformation("Created {Table} table", Tables.GeocodingCache);
        }

        // Create DataExports table if it doesn't exist
        if (!await TableExistsAsync(Tables.DataExports))
        {
            #pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE {Tables.DataExports} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT NOT NULL,
                    VehicleId INTEGER NOT NULL,
                    ExportType TEXT NOT NULL,
                    Status INTEGER NOT NULL DEFAULT 0,
                    DownloadToken TEXT NOT NULL,
                    FileData BLOB,
                    FileName TEXT,
                    FileSizeBytes INTEGER,
                    RecordCount INTEGER,
                    ErrorMessage TEXT,
                    CreatedAt TEXT NOT NULL,
                    CompletedAt TEXT,
                    ExpiresAt TEXT NOT NULL,
                    DownloadedAt TEXT,
                    FOREIGN KEY (UserId) REFERENCES {Tables.Users}(Id) ON DELETE CASCADE,
                    FOREIGN KEY (VehicleId) REFERENCES {Tables.Vehicles}(Id) ON DELETE CASCADE
                )");
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE INDEX IX_{Tables.DataExports}_UserId ON {Tables.DataExports}(UserId)");
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE UNIQUE INDEX IX_{Tables.DataExports}_DownloadToken ON {Tables.DataExports}(DownloadToken)");
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE INDEX IX_{Tables.DataExports}_ExpiresAt ON {Tables.DataExports}(ExpiresAt)");
            #pragma warning restore EF1002
            logger.LogInformation("Created {Table} table", Tables.DataExports);
        }

        // Add referral columns to Users table
        await AddColumnIfNotExistsAsync(Tables.Users, "ReferralCode", "TEXT");
        await AddColumnIfNotExistsAsync(Tables.Users, "ReferredByUserId", "TEXT");

        // Create PromoCampaigns table if it doesn't exist
        if (!await TableExistsAsync(Tables.PromoCampaigns))
        {
            #pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE {Tables.PromoCampaigns} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    CampaignType TEXT NOT NULL,
                    CreditsPerReward INTEGER NOT NULL DEFAULT 1,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    StartsAt TEXT,
                    EndsAt TEXT,
                    MaxRedemptionsPerUser INTEGER,
                    CreatedAt TEXT NOT NULL
                )");
            #pragma warning restore EF1002
            logger.LogInformation("Created {Table} table", Tables.PromoCampaigns);
        }

        // Create Referrals table if it doesn't exist
        if (!await TableExistsAsync(Tables.Referrals))
        {
            #pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE {Tables.Referrals} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CampaignId INTEGER NOT NULL,
                    ReferrerId TEXT NOT NULL,
                    ReferredUserId TEXT NOT NULL,
                    ReferralCode TEXT NOT NULL,
                    Status INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    QualifiedAt TEXT,
                    RewardedAt TEXT,
                    FOREIGN KEY (CampaignId) REFERENCES {Tables.PromoCampaigns}(Id),
                    FOREIGN KEY (ReferrerId) REFERENCES {Tables.Users}(Id),
                    FOREIGN KEY (ReferredUserId) REFERENCES {Tables.Users}(Id) ON DELETE CASCADE
                )");
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE INDEX IX_{Tables.Referrals}_ReferrerId ON {Tables.Referrals}(ReferrerId)");
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE INDEX IX_{Tables.Referrals}_ReferredUserId ON {Tables.Referrals}(ReferredUserId)");
            #pragma warning restore EF1002
            logger.LogInformation("Created {Table} table", Tables.Referrals);
        }

        // Create PromoCredits table if it doesn't exist
        if (!await TableExistsAsync(Tables.PromoCredits))
        {
            #pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($@"
                CREATE TABLE {Tables.PromoCredits} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT NOT NULL,
                    CampaignId INTEGER NOT NULL,
                    ReferralId INTEGER,
                    Credits INTEGER NOT NULL DEFAULT 1,
                    Reason TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    ExpiresAt TEXT,
                    ConsumedAt TEXT,
                    FOREIGN KEY (UserId) REFERENCES {Tables.Users}(Id) ON DELETE CASCADE,
                    FOREIGN KEY (CampaignId) REFERENCES {Tables.PromoCampaigns}(Id),
                    FOREIGN KEY (ReferralId) REFERENCES {Tables.Referrals}(Id) ON DELETE SET NULL
                )");
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE INDEX IX_{Tables.PromoCredits}_UserId ON {Tables.PromoCredits}(UserId)");
            #pragma warning restore EF1002
            logger.LogInformation("Created {Table} table", Tables.PromoCredits);
        }

        logger.LogInformation("SQLite database ready");

        // Seed default referral campaign
        #pragma warning disable EF1002
        var campaignExists = await db.PromoCampaigns.AnyAsync(c => c.CampaignType == "Referral");
        #pragma warning restore EF1002
        if (!campaignExists)
        {
            db.PromoCampaigns.Add(new PromoCampaign
            {
                Name = "Refer a Friend",
                Description = "Refer a friend and both get 1 month of credit",
                CampaignType = "Referral",
                CreditsPerReward = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded default referral campaign");
        }

        // Seed development data for SQLite
        var seeder = scope.ServiceProvider.GetRequiredService<DevDataSeeder>();
        await seeder.SeedAsync();
    }
    else
    {
        // PostgreSQL - retry logic for Docker startup timing
        const int maxRetries = 10;
        const int delaySeconds = 3;

        // Helper to add column if not exists (for post-migration schema updates)
        async Task AddPostgresColumnIfNotExistsAsync(
            RivianMateDbContext dbContext, string table, string column, string type, string? constraint = null)
        {
            var conn = dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = $@"
                    SELECT COUNT(*) FROM information_schema.columns
                    WHERE table_name = '{table}' AND column_name = '{column}'";
                var exists = (long)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;

                if (!exists)
                {
                    using var addCmd = conn.CreateCommand();
                    var sql = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}";
                    if (!string.IsNullOrEmpty(constraint))
                        sql += $" {constraint}";
                    addCmd.CommandText = sql;
                    await addCmd.ExecuteNonQueryAsync();
                    logger.LogInformation("Added {Column} column to {Table} table (PostgreSQL)", column, table);

                    // Add index for FK
                    if (constraint?.Contains("REFERENCES") == true)
                    {
                        using var indexCmd = conn.CreateCommand();
                        indexCmd.CommandText = $"CREATE INDEX IF NOT EXISTS \"IX_{table}_{column}\" ON \"{table}\" (\"{column}\")";
                        await indexCmd.ExecuteNonQueryAsync();
                    }
                }
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                logger.LogInformation("Attempting database connection (attempt {Attempt}/{MaxRetries})...", attempt, maxRetries);

                if (await db.Database.CanConnectAsync())
                {
                    logger.LogInformation("Applying database migrations...");
                    await db.Database.MigrateAsync();

                    // Add UserLocationId FK to ChargingSessions if not exists (post-migration schema update)
                    await AddPostgresColumnIfNotExistsAsync(
                        db, Tables.ChargingSessions, "UserLocationId", "INTEGER",
                        $"REFERENCES \"{Tables.UserLocations}\"(\"Id\") ON DELETE SET NULL");

                    // Add BuildDate to Vehicles if not exists
                    await AddPostgresColumnIfNotExistsAsync(
                        db, Tables.Vehicles, "BuildDate", "TIMESTAMP WITHOUT TIME ZONE");

                    // Add referral columns to Users if not exists
                    await AddPostgresColumnIfNotExistsAsync(
                        db, Tables.Users, "ReferralCode", "VARCHAR(20)");
                    await AddPostgresColumnIfNotExistsAsync(
                        db, Tables.Users, "ReferredByUserId", "UUID");

                    logger.LogInformation("Database migrations applied successfully");

                    // Seed default referral campaign
                    var pgCampaignExists = await db.PromoCampaigns.AnyAsync(c => c.CampaignType == "Referral");
                    if (!pgCampaignExists)
                    {
                        db.PromoCampaigns.Add(new PromoCampaign
                        {
                            Name = "Refer a Friend",
                            Description = "Refer a friend and both get 1 month of credit",
                            CampaignType = "Referral",
                            CreditsPerReward = 1,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                        await db.SaveChangesAsync();
                        logger.LogInformation("Seeded default referral campaign");
                    }

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

// Schedule data retention cleanup job
var retentionConfig = builder.Configuration
    .GetSection("RivianMate:DataRetention")
    .Get<DataRetentionConfiguration>() ?? new DataRetentionConfiguration();

if (retentionConfig.Enabled && retentionConfig.Tables.Count > 0)
{
    RecurringJob.AddOrUpdate<DataRetentionJob>(
        "data-retention-cleanup",
        job => job.ExecuteAsync(CancellationToken.None),
        retentionConfig.Schedule);
}

// Geocode addresses for drives - runs daily at 3 AM, processes up to 100 drives per run
// Can also be manually triggered from Hangfire dashboard
RecurringJob.AddOrUpdate<GeocodeAddressJob>(
    "geocode-drive-addresses",
    job => job.BackfillAddressesAsync(100, CancellationToken.None),
    "0 3 * * *"); // Daily at 3:00 AM

// Email verification enforcement - runs daily at 2 AM to deactivate unverified accounts past deadline
RecurringJob.AddOrUpdate<EmailVerificationEnforcementJob>(
    "email-verification-enforcement",
    job => job.ExecuteAsync(),
    "0 2 * * *"); // Daily at 2:00 AM

// Email verification reminder - runs hourly to send 24-hour reminders
RecurringJob.AddOrUpdate<EmailVerificationReminderJob>(
    "email-verification-reminder",
    job => job.ExecuteAsync(),
    "0 * * * *"); // Every hour at minute 0

// Export cleanup - runs daily at 4 AM to delete expired exports and free storage
RecurringJob.AddOrUpdate<ExportCleanupJob>(
    "export-cleanup",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 4 * * *"); // Daily at 4:00 AM

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

// Data export download endpoint
app.MapGet("/api/exports/{downloadToken:guid}", async (
    Guid downloadToken,
    DataExportService exportService,
    HttpContext httpContext) =>
{
    var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var export = await exportService.GetExportForDownloadAsync(downloadToken, userId);
    if (export?.FileData == null)
        return Results.NotFound();

    var contentType = export.FileName?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true
        ? "application/zip"
        : "text/csv";

    return Results.File(export.FileData, contentType, export.FileName);
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
