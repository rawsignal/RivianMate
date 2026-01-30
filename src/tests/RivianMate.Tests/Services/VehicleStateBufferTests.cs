using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RivianMate.Api.Services;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using Xunit;

namespace RivianMate.Tests.Services;

public class VehicleStateBufferTests
{
    private readonly VehicleStateBuffer _buffer;

    public VehicleStateBufferTests()
    {
        var logger = new Mock<ILogger<VehicleStateBuffer>>();
        _buffer = new VehicleStateBuffer(logger.Object);
    }

    // === HasSignificantChange (internal static) ===

    [Fact]
    public void HasSignificantChange_ReturnsFalse_WhenBothNull()
    {
        VehicleStateBuffer.HasSignificantChange(null, null, 1.0).Should().BeFalse();
    }

    [Fact]
    public void HasSignificantChange_ReturnsTrue_WhenOldIsNull()
    {
        VehicleStateBuffer.HasSignificantChange(null, 50.0, 1.0).Should().BeTrue();
    }

    [Fact]
    public void HasSignificantChange_ReturnsTrue_WhenNewIsNull()
    {
        VehicleStateBuffer.HasSignificantChange(50.0, null, 1.0).Should().BeTrue();
    }

    [Fact]
    public void HasSignificantChange_ReturnsFalse_WhenBelowThreshold()
    {
        VehicleStateBuffer.HasSignificantChange(50.0, 50.3, 0.5).Should().BeFalse();
    }

    [Fact]
    public void HasSignificantChange_ReturnsTrue_WhenAtThreshold()
    {
        VehicleStateBuffer.HasSignificantChange(50.0, 50.5, 0.5).Should().BeTrue();
    }

    [Fact]
    public void HasSignificantChange_ReturnsTrue_WhenAboveThreshold()
    {
        VehicleStateBuffer.HasSignificantChange(50.0, 52.0, 0.5).Should().BeTrue();
    }

    // === CalculateDistanceMeters (internal static) ===

    [Fact]
    public void CalculateDistanceMeters_ReturnsZero_ForSamePoint()
    {
        var result = VehicleStateBuffer.CalculateDistanceMeters(40.7128, -74.0060, 40.7128, -74.0060);
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateDistanceMeters_ReturnsCorrectDistance_ForKnownPoints()
    {
        // NYC (40.7128, -74.0060) to LA (34.0522, -118.2437) ≈ 3,944 km
        var result = VehicleStateBuffer.CalculateDistanceMeters(40.7128, -74.0060, 34.0522, -118.2437);
        var distanceKm = result / 1000;
        distanceKm.Should().BeApproximately(3944, 50); // Within 50 km
    }

    [Fact]
    public void CalculateDistanceMeters_ReturnsCorrectDistance_ForShortDistance()
    {
        // Two points about 1 km apart in Manhattan
        var result = VehicleStateBuffer.CalculateDistanceMeters(40.7484, -73.9857, 40.7580, -73.9855);
        result.Should().BeApproximately(1068, 50); // ~1 km, within 50m tolerance
    }

    // === ShouldSaveState (instance method) ===

    [Fact]
    public void ShouldSaveState_ReturnsTrue_WhenFirstStateForVehicle()
    {
        var state = CreateState(vehicleId: 1, battery: 80);
        _buffer.ShouldSaveState(state).Should().BeTrue();
    }

    [Fact]
    public void ShouldSaveState_ReturnsFalse_WhenNothingChanged()
    {
        var state1 = CreateState(vehicleId: 1, battery: 80);
        _buffer.ShouldSaveState(state1).Should().BeTrue(); // First state always saves
        _buffer.UpdateBuffer(state1);

        var state2 = CreateState(vehicleId: 1, battery: 80);
        _buffer.ShouldSaveState(state2).Should().BeFalse();
    }

    [Fact]
    public void ShouldSaveState_ReturnsTrue_WhenBatteryChangesSignificantly()
    {
        var state1 = CreateState(vehicleId: 1, battery: 80);
        _buffer.ShouldSaveState(state1).Should().BeTrue();
        _buffer.UpdateBuffer(state1);

        var state2 = CreateState(vehicleId: 1, battery: 79); // 1% change > 0.5% threshold
        _buffer.ShouldSaveState(state2).Should().BeTrue();
    }

    [Fact]
    public void ShouldSaveState_ReturnsTrue_WhenPowerStateChanges()
    {
        var state1 = CreateState(vehicleId: 1, battery: 80);
        state1.PowerState = PowerState.Sleep;
        _buffer.ShouldSaveState(state1).Should().BeTrue();
        _buffer.UpdateBuffer(state1);

        var state2 = CreateState(vehicleId: 1, battery: 80);
        state2.PowerState = PowerState.Ready;
        _buffer.ShouldSaveState(state2).Should().BeTrue();
    }

    [Fact]
    public void ShouldSaveState_ReturnsTrue_WhenGearChanges()
    {
        var state1 = CreateState(vehicleId: 1, battery: 80);
        state1.GearStatus = GearStatus.Park;
        _buffer.ShouldSaveState(state1).Should().BeTrue();
        _buffer.UpdateBuffer(state1);

        var state2 = CreateState(vehicleId: 1, battery: 80);
        state2.GearStatus = GearStatus.Drive;
        _buffer.ShouldSaveState(state2).Should().BeTrue();
    }

    [Fact]
    public void ShouldSaveState_ReturnsTrue_WhenChargerStateChanges()
    {
        var state1 = CreateState(vehicleId: 1, battery: 80);
        state1.ChargerState = ChargerState.Disconnected;
        _buffer.ShouldSaveState(state1).Should().BeTrue();
        _buffer.UpdateBuffer(state1);

        var state2 = CreateState(vehicleId: 1, battery: 80);
        state2.ChargerState = ChargerState.Charging;
        _buffer.ShouldSaveState(state2).Should().BeTrue();
    }

    [Fact]
    public void ShouldSaveState_ReturnsTrue_AfterHeartbeatInterval()
    {
        var state1 = CreateState(vehicleId: 1, battery: 80);
        _buffer.ShouldSaveState(state1).Should().BeTrue();
        _buffer.UpdateBuffer(state1); // SavedAt = ~now

        // Same state but with timestamp far enough in the future (> 1 hour heartbeat).
        // Buffer stores SavedAt as DateTime.UtcNow at UpdateBuffer time,
        // so we set the new state's timestamp 2 hours in the future.
        var state2 = CreateState(vehicleId: 1, battery: 80);
        state2.Timestamp = DateTime.UtcNow.AddHours(2);
        _buffer.ShouldSaveState(state2).Should().BeTrue();
    }

    // === GetCurrentState / UpdateCurrentState ===

    [Fact]
    public void GetCurrentState_ReturnsNull_WhenNotTracked()
    {
        _buffer.GetCurrentState(999).Should().BeNull();
    }

    [Fact]
    public void UpdateCurrentState_StoresState()
    {
        var state = CreateState(vehicleId: 1, battery: 80);
        _buffer.UpdateCurrentState(state);

        var retrieved = _buffer.GetCurrentState(1);
        retrieved.Should().NotBeNull();
        retrieved!.BatteryLevel.Should().Be(80);
    }

    // === ClearVehicle ===

    [Fact]
    public void ClearVehicle_RemovesBothBuffers()
    {
        var state = CreateState(vehicleId: 1, battery: 80);
        _buffer.UpdateBuffer(state);
        _buffer.UpdateCurrentState(state);

        _buffer.ClearVehicle(1);

        _buffer.GetCurrentState(1).Should().BeNull();
        // After clearing, next ShouldSaveState should return true (no buffered state)
        _buffer.ShouldSaveState(state).Should().BeTrue();
    }

    private static VehicleState CreateState(int vehicleId, double battery)
    {
        return new VehicleState
        {
            VehicleId = vehicleId,
            BatteryLevel = battery,
            Timestamp = DateTime.UtcNow,
            PowerState = PowerState.Unknown,
            GearStatus = GearStatus.Unknown,
            ChargerState = ChargerState.Unknown
        };
    }
}
