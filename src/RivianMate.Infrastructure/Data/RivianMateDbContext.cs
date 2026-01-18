using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Entities;

namespace RivianMate.Infrastructure.Data;

public class RivianMateDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IDataProtectionKeyContext
{
    public RivianMateDbContext(DbContextOptions<RivianMateDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleState> VehicleStates => Set<VehicleState>();
    public DbSet<ChargingSession> ChargingSessions => Set<ChargingSession>();
    public DbSet<Drive> Drives => Set<Drive>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<BatteryHealthSnapshot> BatteryHealthSnapshots => Set<BatteryHealthSnapshot>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<RivianAccount> RivianAccounts => Set<RivianAccount>();
    public DbSet<UserDashboardConfig> UserDashboardConfigs => Set<UserDashboardConfig>();

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
        modelBuilder.Entity<VehicleState>(entity =>
        {
            entity.HasKey(e => e.Id);
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
    }
}
