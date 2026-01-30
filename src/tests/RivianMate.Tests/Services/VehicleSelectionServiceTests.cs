using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using RivianMate.Api.Services;
using RivianMate.Core.Entities;
using RivianMate.Tests.TestHelpers;
using Xunit;

namespace RivianMate.Tests.Services;

public class VehicleSelectionServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IJSRuntime> _mockJs = new();

    private (VehicleSelectionService Service, string DbName) CreateServiceWithDb(List<Vehicle>? vehicles = null)
    {
        var dbName = Guid.NewGuid().ToString();

        if (vehicles != null && vehicles.Count > 0)
        {
            using var seedDb = DbContextHelper.CreateInMemory(dbName);
            foreach (var v in vehicles)
            {
                seedDb.Vehicles.Add(v);
            }
            seedDb.SaveChanges();
        }

        var db = DbContextHelper.CreateInMemory(dbName);
        var stateBuffer = new VehicleStateBuffer(
            new Mock<ILogger<VehicleStateBuffer>>().Object);
        var activityFeed = new ActivityFeedService(
            db,
            new Mock<ILogger<ActivityFeedService>>().Object);
        var vehicleService = new VehicleService(
            db,
            stateBuffer,
            activityFeed,
            new Mock<ILogger<VehicleService>>().Object);
        var service = new VehicleSelectionService(vehicleService);

        return (service, dbName);
    }

    [Fact]
    public async Task ResolveVehicleAsync_ReturnsNoVehicles_WhenUserHasNone()
    {
        var (service, _) = CreateServiceWithDb();

        var result = await service.ResolveVehicleAsync(_userId, null, _mockJs.Object);

        result.NoVehicles.Should().BeTrue();
        result.Vehicle.Should().BeNull();
        result.NotFound.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveVehicleAsync_ReturnsVehicle_WhenUrlParamProvided()
    {
        var publicId = Guid.NewGuid();
        var vehicles = new List<Vehicle>
        {
            new Vehicle
            {
                RivianVehicleId = "test-1",
                OwnerId = _userId,
                IsActive = true,
                PublicId = publicId,
                Name = "My R1T",
                CreatedAt = DateTime.UtcNow
            }
        };

        var (service, _) = CreateServiceWithDb(vehicles);

        var result = await service.ResolveVehicleAsync(_userId, publicId, _mockJs.Object);

        result.Vehicle.Should().NotBeNull();
        result.Vehicle!.Name.Should().Be("My R1T");
        result.NoVehicles.Should().BeFalse();
        result.NotFound.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveVehicleAsync_ReturnsNotFound_WhenUrlParamInvalid()
    {
        var vehicles = new List<Vehicle>
        {
            new Vehicle
            {
                RivianVehicleId = "test-1",
                OwnerId = _userId,
                IsActive = true,
                PublicId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            }
        };

        var (service, _) = CreateServiceWithDb(vehicles);

        var result = await service.ResolveVehicleAsync(_userId, Guid.NewGuid(), _mockJs.Object);

        result.NotFound.Should().BeTrue();
        result.Vehicle.Should().BeNull();
        result.NoVehicles.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveVehicleAsync_FallsBackToFirstVehicle_WhenNoUrlParam()
    {
        var vehicles = new List<Vehicle>
        {
            new Vehicle
            {
                RivianVehicleId = "test-1",
                OwnerId = _userId,
                IsActive = true,
                Name = "Alpha",
                CreatedAt = DateTime.UtcNow
            },
            new Vehicle
            {
                RivianVehicleId = "test-2",
                OwnerId = _userId,
                IsActive = true,
                Name = "Beta",
                CreatedAt = DateTime.UtcNow
            }
        };

        // Mock JS to throw (simulating prerendering where JS is not available)
        _mockJs.Setup(js => js.InvokeAsync<string?>(
            It.IsAny<string>(),
            It.IsAny<object[]>()))
            .ThrowsAsync(new InvalidOperationException("Prerendering"));

        var (service, _) = CreateServiceWithDb(vehicles);

        var result = await service.ResolveVehicleAsync(_userId, null, _mockJs.Object);

        result.Vehicle.Should().NotBeNull();
        result.NoVehicles.Should().BeFalse();
        result.NotFound.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveVehicleAsync_FallsBackToLocalStorage()
    {
        var vehicles = new List<Vehicle>
        {
            new Vehicle
            {
                RivianVehicleId = "test-1",
                OwnerId = _userId,
                IsActive = true,
                Name = "First Vehicle",
                CreatedAt = DateTime.UtcNow
            },
            new Vehicle
            {
                RivianVehicleId = "test-2",
                OwnerId = _userId,
                IsActive = true,
                Name = "Second Vehicle",
                CreatedAt = DateTime.UtcNow
            }
        };

        var (service, dbName) = CreateServiceWithDb(vehicles);

        // Look up the second vehicle's Id
        int secondVehicleId;
        using (var db = DbContextHelper.CreateInMemory(dbName))
        {
            secondVehicleId = db.Vehicles.OrderBy(v => v.Name).Skip(1).First().Id;
        }

        // Mock JS to return the second vehicle's ID from localStorage
        _mockJs.Setup(js => js.InvokeAsync<string?>(
            It.IsAny<string>(),
            It.IsAny<object[]>()))
            .ReturnsAsync(secondVehicleId.ToString());

        var result = await service.ResolveVehicleAsync(_userId, null, _mockJs.Object);

        result.Vehicle.Should().NotBeNull();
        result.Vehicle!.Name.Should().Be("Second Vehicle");
    }
}
