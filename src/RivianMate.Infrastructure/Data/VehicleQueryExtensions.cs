using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Entities;

namespace RivianMate.Infrastructure.Data;

/// <summary>
/// Extension methods for efficient vehicle queries.
/// </summary>
public static class VehicleQueryExtensions
{
    /// <summary>
    /// Query vehicles without loading ImageData blob.
    /// Use this for all queries where the image isn't needed to reduce network transfer.
    /// </summary>
    public static IQueryable<Vehicle> WithoutImageData(this IQueryable<Vehicle> query)
    {
        return query.Select(v => new Vehicle
        {
            Id = v.Id,
            PublicId = v.PublicId,
            RivianVehicleId = v.RivianVehicleId,
            Vin = v.Vin,
            Name = v.Name,
            Model = v.Model,
            BatteryPack = v.BatteryPack,
            DriveType = v.DriveType,
            Trim = v.Trim,
            Year = v.Year,
            ExteriorColor = v.ExteriorColor,
            InteriorColor = v.InteriorColor,
            WheelConfig = v.WheelConfig,
            EpaRangeMiles = v.EpaRangeMiles,
            OriginalCapacityKwh = v.OriginalCapacityKwh,
            BatteryCellType = v.BatteryCellType,
            SoftwareVersion = v.SoftwareVersion,
            // ImageData intentionally excluded - this is the big blob
            ImageContentType = v.ImageContentType,
            ImageUrl = v.ImageUrl,
            ImageVersion = v.ImageVersion,
            CreatedAt = v.CreatedAt,
            LastSeenAt = v.LastSeenAt,
            IsActive = v.IsActive,
            OwnerId = v.OwnerId,
            RivianAccountId = v.RivianAccountId
            // Navigation properties not loaded - use Include() if needed
        });
    }

    /// <summary>
    /// Get a vehicle by ID without loading ImageData blob.
    /// </summary>
    public static async Task<Vehicle?> FindWithoutImageAsync(
        this DbSet<Vehicle> vehicles,
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        return await vehicles
            .WithoutImageData()
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);
    }
}
