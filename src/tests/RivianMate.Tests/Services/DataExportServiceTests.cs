using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RivianMate.Api.Services;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Tests.TestHelpers;
using Xunit;

namespace RivianMate.Tests.Services;

public class DataExportServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly int _vehicleId;
    private readonly string _dbName;

    public DataExportServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();

        // Seed the database with a vehicle owned by the test user
        using var db = DbContextHelper.CreateInMemory(_dbName);
        var vehicle = new Vehicle
        {
            RivianVehicleId = "test-123",
            OwnerId = _userId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Vehicles.Add(vehicle);
        db.SaveChanges();
        _vehicleId = vehicle.Id;
    }

    private DataExportService CreateService()
    {
        var factory = DbContextHelper.CreateFactory(_dbName);
        var logger = new Mock<ILogger<DataExportService>>();
        return new DataExportService(factory, logger.Object);
    }

    [Theory]
    [InlineData("InvalidType")]
    [InlineData("drives")]  // Wrong case
    [InlineData("")]
    public async Task RequestExportAsync_ReturnsError_ForInvalidExportType(string exportType)
    {
        var service = CreateService();

        var (export, error) = await service.RequestExportAsync(_userId, _vehicleId, exportType);

        export.Should().BeNull();
        error.Should().Be("Invalid export type.");
    }

    [Fact]
    public async Task RequestExportAsync_ReturnsError_WhenVehicleNotOwned()
    {
        var service = CreateService();
        var otherUserId = Guid.NewGuid();

        var (export, error) = await service.RequestExportAsync(otherUserId, _vehicleId, "Drives");

        export.Should().BeNull();
        error.Should().Be("Vehicle not found.");
    }

    [Fact]
    public async Task RequestExportAsync_ReturnsError_WhenMaxPendingExportsReached()
    {
        // Seed 3 pending exports
        using (var db = DbContextHelper.CreateInMemory(_dbName))
        {
            for (int i = 0; i < 3; i++)
            {
                db.DataExports.Add(new DataExport
                {
                    UserId = _userId,
                    VehicleId = _vehicleId,
                    ExportType = "Drives",
                    Status = ExportStatus.Pending,
                    CreatedAt = DateTime.UtcNow.AddHours(-1) // Old enough to not trigger cooldown for the request itself
                });
            }
            await db.SaveChangesAsync();
        }

        var service = CreateService();

        var (export, error) = await service.RequestExportAsync(_userId, _vehicleId, "Charging");

        export.Should().BeNull();
        error.Should().Contain("exports in progress");
    }

    [Fact]
    public async Task RequestExportAsync_ReturnsError_WhenCooldownNotElapsed()
    {
        // Seed a recent export (less than 5 minutes ago)
        using (var db = DbContextHelper.CreateInMemory(_dbName))
        {
            db.DataExports.Add(new DataExport
            {
                UserId = _userId,
                VehicleId = _vehicleId,
                ExportType = "Drives",
                Status = ExportStatus.Completed,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1) // Only 1 minute ago
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService();

        var (export, error) = await service.RequestExportAsync(_userId, _vehicleId, "Charging");

        export.Should().BeNull();
        error.Should().Contain("Please wait");
    }

    [Fact]
    public async Task RequestExportAsync_CreatesExport_WhenAllChecksPass()
    {
        var service = CreateService();

        var (export, error) = await service.RequestExportAsync(_userId, _vehicleId, "Drives");

        error.Should().BeNull();
        export.Should().NotBeNull();
        export!.UserId.Should().Be(_userId);
        export.VehicleId.Should().Be(_vehicleId);
        export.ExportType.Should().Be("Drives");
        export.Status.Should().Be(ExportStatus.Pending);
        export.DownloadToken.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("Drives")]
    [InlineData("Charging")]
    [InlineData("BatteryHealth")]
    [InlineData("All")]
    public async Task RequestExportAsync_AcceptsValidExportTypes(string exportType)
    {
        // Need a fresh DB for each test since cooldown may interfere
        var freshDbName = Guid.NewGuid().ToString();
        using (var freshDb = DbContextHelper.CreateInMemory(freshDbName))
        {
            freshDb.Vehicles.Add(new Vehicle
            {
                RivianVehicleId = "test-fresh",
                OwnerId = _userId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await freshDb.SaveChangesAsync();
        }

        var factory = DbContextHelper.CreateFactory(freshDbName);
        int vehicleId;
        using (var db = factory.CreateDbContext())
        {
            vehicleId = db.Vehicles.First().Id;
        }
        var logger = new Mock<ILogger<DataExportService>>();
        var service = new DataExportService(factory, logger.Object);

        var (export, error) = await service.RequestExportAsync(_userId, vehicleId, exportType);

        error.Should().BeNull();
        export.Should().NotBeNull();
        export!.ExportType.Should().Be(exportType);
    }

    // === GetExportForDownloadAsync ===

    [Fact]
    public async Task GetExportForDownloadAsync_ReturnsNull_WhenTokenInvalid()
    {
        var service = CreateService();

        var result = await service.GetExportForDownloadAsync(Guid.NewGuid(), _userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetExportForDownloadAsync_ReturnsNull_WhenNotCompleted()
    {
        var downloadToken = Guid.NewGuid();
        using (var db = DbContextHelper.CreateInMemory(_dbName))
        {
            db.DataExports.Add(new DataExport
            {
                UserId = _userId,
                VehicleId = _vehicleId,
                ExportType = "Drives",
                Status = ExportStatus.Pending,
                DownloadToken = downloadToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetExportForDownloadAsync(downloadToken, _userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetExportForDownloadAsync_ReturnsNull_WhenExpired()
    {
        var downloadToken = Guid.NewGuid();
        using (var db = DbContextHelper.CreateInMemory(_dbName))
        {
            db.DataExports.Add(new DataExport
            {
                UserId = _userId,
                VehicleId = _vehicleId,
                ExportType = "Drives",
                Status = ExportStatus.Completed,
                DownloadToken = downloadToken,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                ExpiresAt = DateTime.UtcNow.AddDays(-1) // Already expired
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetExportForDownloadAsync(downloadToken, _userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetExportForDownloadAsync_ReturnsExport_WhenValid()
    {
        var downloadToken = Guid.NewGuid();
        using (var db = DbContextHelper.CreateInMemory(_dbName))
        {
            db.DataExports.Add(new DataExport
            {
                UserId = _userId,
                VehicleId = _vehicleId,
                ExportType = "Drives",
                Status = ExportStatus.Completed,
                DownloadToken = downloadToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                FileData = new byte[] { 1, 2, 3 },
                FileName = "export.csv"
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetExportForDownloadAsync(downloadToken, _userId);

        result.Should().NotBeNull();
        result!.ExportType.Should().Be("Drives");
        result.FileName.Should().Be("export.csv");
        result.DownloadedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetExportForDownloadAsync_ReturnsNull_WhenWrongUser()
    {
        var downloadToken = Guid.NewGuid();
        using (var db = DbContextHelper.CreateInMemory(_dbName))
        {
            db.DataExports.Add(new DataExport
            {
                UserId = _userId,
                VehicleId = _vehicleId,
                ExportType = "Drives",
                Status = ExportStatus.Completed,
                DownloadToken = downloadToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetExportForDownloadAsync(downloadToken, Guid.NewGuid()); // Different user

        result.Should().BeNull();
    }
}
