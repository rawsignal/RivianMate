using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RivianMate.Core.Entities;
using RivianMate.Core.Exceptions;
using RivianMate.Core.Interfaces;

namespace RivianMate.Infrastructure.Data;

public class RivianMateDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IDataProtectionKeyContext
{
    private readonly ICurrentUserAccessor? _currentUserAccessor;
    private readonly ILogger<RivianMateDbContext>? _logger;

    /// <summary>
    /// Constructor for DbContextFactory (used by EF tooling and background jobs).
    /// No ownership validation in this mode.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public RivianMateDbContext(DbContextOptions<RivianMateDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Constructor with ownership validation services.
    /// Used for scoped DbContext in request handling.
    /// </summary>
    public RivianMateDbContext(
        DbContextOptions<RivianMateDbContext> options,
        ICurrentUserAccessor? currentUserAccessor,
        ILogger<RivianMateDbContext>? logger)
        : base(options)
    {
        _currentUserAccessor = currentUserAccessor;
        _logger = logger;
    }
    
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleState> VehicleStates => Set<VehicleState>();
    public DbSet<ChargingSession> ChargingSessions => Set<ChargingSession>();
    public DbSet<Drive> Drives => Set<Drive>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<BatteryHealthSnapshot> BatteryHealthSnapshots => Set<BatteryHealthSnapshot>();
    public DbSet<ActivityFeedItem> ActivityFeed => Set<ActivityFeedItem>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<RivianAccount> RivianAccounts => Set<RivianAccount>();
    public DbSet<UserDashboardConfig> UserDashboardConfigs => Set<UserDashboardConfig>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
    public DbSet<UserLocation> UserLocations => Set<UserLocation>();
    public DbSet<GeocodingCache> GeocodingCache => Set<GeocodingCache>();

    // For ASP.NET Data Protection key storage
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // === ApplicationUser ===
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.DisplayName).HasMaxLength(100);
        });

        // === RivianAccount ===
        modelBuilder.Entity<RivianAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.RivianEmail }).IsUnique();

            entity.Property(e => e.RivianEmail).HasMaxLength(256);
            entity.Property(e => e.RivianUserId).HasMaxLength(100);
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.EncryptedCsrfToken).HasMaxLength(4000);
            entity.Property(e => e.EncryptedAppSessionToken).HasMaxLength(4000);
            entity.Property(e => e.EncryptedUserSessionToken).HasMaxLength(4000);
            entity.Property(e => e.EncryptedAccessToken).HasMaxLength(4000);
            entity.Property(e => e.EncryptedRefreshToken).HasMaxLength(4000);
            entity.Property(e => e.LastSyncError).HasMaxLength(1000);

            entity.HasOne(e => e.User)
                .WithMany(u => u.RivianAccounts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // === Vehicle ===
        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PublicId).IsUnique();
            entity.HasIndex(e => e.RivianVehicleId).IsUnique();
            entity.HasIndex(e => e.Vin);
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.RivianAccountId);

            entity.Property(e => e.RivianVehicleId).HasMaxLength(100);
            entity.Property(e => e.Vin).HasMaxLength(17);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.ExteriorColor).HasMaxLength(50);
            entity.Property(e => e.InteriorColor).HasMaxLength(50);
            entity.Property(e => e.WheelConfig).HasMaxLength(50);
            entity.Property(e => e.SoftwareVersion).HasMaxLength(50);

            entity.HasOne(e => e.Owner)
                .WithMany(u => u.Vehicles)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.RivianAccount)
                .WithMany(ra => ra.Vehicles)
                .HasForeignKey(e => e.RivianAccountId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // === VehicleState ===
        // Historical vehicle states for tracking changes over time
        modelBuilder.Entity<VehicleState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.VehicleId, e.Timestamp });

            entity.HasOne(e => e.Vehicle)
                .WithMany(v => v.States)
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.DriveMode).HasMaxLength(50);
            entity.Property(e => e.OtaCurrentVersion).HasMaxLength(50);
            entity.Property(e => e.OtaAvailableVersion).HasMaxLength(50);
            entity.Property(e => e.OtaStatus).HasMaxLength(50);
        });

        // === ActivityFeedItem ===
        modelBuilder.Entity<ActivityFeedItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.VehicleId, e.Timestamp });
            entity.HasIndex(e => new { e.VehicleId, e.Type, e.Timestamp });

            entity.HasOne(e => e.Vehicle)
                .WithMany(v => v.ActivityFeed)
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Message).HasMaxLength(500);
        });
        
        // === ChargingSession ===
        modelBuilder.Entity<ChargingSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => new { e.VehicleId, e.StartTime });
            entity.HasIndex(e => e.IsActive);
            
            entity.HasOne(e => e.Vehicle)
                .WithMany(v => v.ChargingSessions)
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.LocationName).HasMaxLength(200);
        });
        
        // === Drive ===
        modelBuilder.Entity<Drive>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => new { e.VehicleId, e.StartTime });
            entity.HasIndex(e => e.IsActive);
            
            entity.HasOne(e => e.Vehicle)
                .WithMany(v => v.Drives)
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.StartAddress).HasMaxLength(300);
            entity.Property(e => e.EndAddress).HasMaxLength(300);
            entity.Property(e => e.DriveMode).HasMaxLength(50);
        });
        
        // === Position ===
        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.DriveId, e.Timestamp });
            
            entity.HasOne(e => e.Drive)
                .WithMany(d => d.Positions)
                .HasForeignKey(e => e.DriveId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // === BatteryHealthSnapshot ===
        modelBuilder.Entity<BatteryHealthSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.VehicleId, e.Timestamp });
            
            entity.HasOne(e => e.Vehicle)
                .WithMany(v => v.BatteryHealthSnapshots)
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // === Setting ===
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();

            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(4000);
        });

        // === UserDashboardConfig ===
        modelBuilder.Entity<UserDashboardConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.CardId }).IsUnique();

            entity.Property(e => e.CardId).HasMaxLength(50);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // === UserPreferences ===
        modelBuilder.Entity<UserPreferences>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique(); // One preferences row per user

            entity.Property(e => e.CurrencyCode).HasMaxLength(3);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // === UserLocation ===
        modelBuilder.Entity<UserLocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId); // Multiple locations per user

            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // === GeocodingCache ===
        modelBuilder.Entity<GeocodingCache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Latitude, e.Longitude }).IsUnique();

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.ShortAddress).HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.Country).HasMaxLength(100);
        });
    }

    /// <summary>
    /// Override SaveChangesAsync to validate ownership of entities before saving.
    /// This provides defense-in-depth by ensuring that even if the application layer
    /// fails to validate ownership, the database layer will catch it.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await ValidateOwnershipAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Override SaveChanges to validate ownership of entities before saving.
    /// </summary>
    public override int SaveChanges()
    {
        ValidateOwnershipAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges();
    }

    private async Task ValidateOwnershipAsync(CancellationToken cancellationToken)
    {
        // Skip validation if no user accessor is available (e.g., during migrations or background jobs)
        var currentUserId = _currentUserAccessor?.UserId;
        if (currentUserId == null)
        {
            return;
        }

        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);

        foreach (var entry in entries)
        {
            var entity = entry.Entity;

            // Check IUserOwnedEntity (RivianAccount, UserDashboardConfig)
            if (entity is IUserOwnedEntity userOwned)
            {
                if (userOwned.UserId != currentUserId.Value)
                {
                    _logger?.LogWarning(
                        "Ownership violation: User {CurrentUserId} attempted to {Action} {EntityType} owned by {OwnerId}",
                        currentUserId, entry.State, entity.GetType().Name, userOwned.UserId);
                    throw new OwnershipViolationException(entity.GetType().Name, currentUserId, userOwned.UserId);
                }
            }
            // Check IOwnerOwnedEntity (Vehicle)
            else if (entity is IOwnerOwnedEntity ownerOwned)
            {
                // For vehicles, OwnerId can be null for legacy data
                // Only validate if OwnerId is set and doesn't match
                if (ownerOwned.OwnerId.HasValue && ownerOwned.OwnerId.Value != currentUserId.Value)
                {
                    _logger?.LogWarning(
                        "Ownership violation: User {CurrentUserId} attempted to {Action} {EntityType} owned by {OwnerId}",
                        currentUserId, entry.State, entity.GetType().Name, ownerOwned.OwnerId);
                    throw new OwnershipViolationException(entity.GetType().Name, currentUserId, ownerOwned.OwnerId);
                }
            }
            // Check IVehicleOwnedEntity (VehicleState, ChargingSession, Drive, BatteryHealthSnapshot)
            else if (entity is IVehicleOwnedEntity vehicleOwned)
            {
                // Need to look up the vehicle to check ownership
                var vehicle = await GetVehicleOwnerAsync(vehicleOwned.VehicleId, cancellationToken);
                if (vehicle?.OwnerId != null && vehicle.OwnerId != currentUserId.Value)
                {
                    _logger?.LogWarning(
                        "Ownership violation: User {CurrentUserId} attempted to {Action} {EntityType} for vehicle owned by {OwnerId}",
                        currentUserId, entry.State, entity.GetType().Name, vehicle.OwnerId);
                    throw new OwnershipViolationException(entity.GetType().Name, currentUserId, vehicle.OwnerId);
                }
            }
            // Check IDriveOwnedEntity (Position)
            else if (entity is IDriveOwnedEntity driveOwned)
            {
                // Need to look up the drive -> vehicle to check ownership
                var ownerId = await GetDriveOwnerAsync(driveOwned.DriveId, cancellationToken);
                if (ownerId != null && ownerId != currentUserId.Value)
                {
                    _logger?.LogWarning(
                        "Ownership violation: User {CurrentUserId} attempted to {Action} {EntityType} for drive owned by {OwnerId}",
                        currentUserId, entry.State, entity.GetType().Name, ownerId);
                    throw new OwnershipViolationException(entity.GetType().Name, currentUserId, ownerId);
                }
            }
        }
    }

    private async Task<Vehicle?> GetVehicleOwnerAsync(int vehicleId, CancellationToken cancellationToken)
    {
        // Check if vehicle is already tracked
        var trackedVehicle = ChangeTracker.Entries<Vehicle>()
            .FirstOrDefault(e => e.Entity.Id == vehicleId)?.Entity;

        if (trackedVehicle != null)
            return trackedVehicle;

        // Query from database
        return await Vehicles
            .AsNoTracking()
            .Where(v => v.Id == vehicleId)
            .Select(v => new Vehicle { Id = v.Id, OwnerId = v.OwnerId, RivianVehicleId = v.RivianVehicleId })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> GetDriveOwnerAsync(int driveId, CancellationToken cancellationToken)
    {
        // Check if drive is already tracked
        var trackedDrive = ChangeTracker.Entries<Drive>()
            .FirstOrDefault(e => e.Entity.Id == driveId)?.Entity;

        if (trackedDrive?.Vehicle != null)
            return trackedDrive.Vehicle.OwnerId;

        // Query from database through drive -> vehicle
        return await Drives
            .AsNoTracking()
            .Where(d => d.Id == driveId)
            .Select(d => d.Vehicle.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
