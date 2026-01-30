using System.Text.Json;
using FluentAssertions;
using RivianMate.Api.Services;
using RivianMate.Core.Enums;
using Xunit;
using DriveType = RivianMate.Core.Enums.DriveType;

namespace RivianMate.Tests.Services;

public class VehicleServiceParsingTests
{
    // === ParseVehicleModel ===

    [Theory]
    [InlineData("R1T", VehicleModel.R1T)]
    [InlineData("r1t", VehicleModel.R1T)]
    [InlineData("R1S", VehicleModel.R1S)]
    [InlineData("r1s", VehicleModel.R1S)]
    [InlineData("R2", VehicleModel.R2)]
    [InlineData("R3", VehicleModel.R3)]
    [InlineData("unknown_model", VehicleModel.Unknown)]
    [InlineData("", VehicleModel.Unknown)]
    [InlineData(null, VehicleModel.Unknown)]
    public void ParseVehicleModel_ReturnsCorrectModel(string? input, VehicleModel expected)
    {
        VehicleService.ParseVehicleModel(input).Should().Be(expected);
    }

    // === ParseDriveType ===

    [Theory]
    [InlineData("Quad-Motor AWD", DriveType.QuadMotor)]
    [InlineData("quad motor", DriveType.QuadMotor)]
    [InlineData("Tri-Motor AWD", DriveType.TriMotor)]
    [InlineData("tri motor", DriveType.TriMotor)]
    [InlineData("Dual-Motor AWD", DriveType.DualMotor)]
    [InlineData("dual motor", DriveType.DualMotor)]
    [InlineData("something_else", DriveType.Unknown)]
    [InlineData("", DriveType.Unknown)]
    [InlineData(null, DriveType.Unknown)]
    public void ParseDriveType_ReturnsCorrectType(string? input, DriveType expected)
    {
        VehicleService.ParseDriveType(input).Should().Be(expected);
    }

    // === ParseBatteryPack ===

    [Theory]
    [InlineData("Max pack", BatteryPackType.Max)]
    [InlineData("Large Pack", BatteryPackType.Large)]
    [InlineData("Standard Pack", BatteryPackType.Standard)]
    [InlineData("something_else", BatteryPackType.Unknown)]
    [InlineData("", BatteryPackType.Unknown)]
    [InlineData(null, BatteryPackType.Unknown)]
    public void ParseBatteryPack_ReturnsCorrectType(string? input, BatteryPackType expected)
    {
        VehicleService.ParseBatteryPack(input).Should().Be(expected);
    }

    // === ParsePowerState ===

    [Theory]
    [InlineData("sleep", PowerState.Sleep)]
    [InlineData("Sleep", PowerState.Sleep)]
    [InlineData("standby", PowerState.Standby)]
    [InlineData("ready", PowerState.Ready)]
    [InlineData("go", PowerState.Go)]
    [InlineData("charging", PowerState.Charging)]
    [InlineData("unknown_state", PowerState.Unknown)]
    [InlineData("", PowerState.Unknown)]
    [InlineData(null, PowerState.Unknown)]
    public void ParsePowerState_ReturnsCorrectState(string? input, PowerState expected)
    {
        VehicleService.ParsePowerState(input).Should().Be(expected);
    }

    // === ParseGearStatus ===

    [Theory]
    [InlineData("park", GearStatus.Park)]
    [InlineData("Park", GearStatus.Park)]
    [InlineData("drive", GearStatus.Drive)]
    [InlineData("reverse", GearStatus.Reverse)]
    [InlineData("neutral", GearStatus.Neutral)]
    [InlineData("unknown_gear", GearStatus.Unknown)]
    [InlineData("", GearStatus.Unknown)]
    [InlineData(null, GearStatus.Unknown)]
    public void ParseGearStatus_ReturnsCorrectGear(string? input, GearStatus expected)
    {
        VehicleService.ParseGearStatus(input).Should().Be(expected);
    }

    // === ParseChargerState ===

    [Theory]
    [InlineData("not_connected", ChargerState.Disconnected)]
    [InlineData("disconnected", ChargerState.Disconnected)]
    [InlineData("charging_ready", ChargerState.ReadyToCharge)]
    [InlineData("ready", ChargerState.ReadyToCharge)]
    [InlineData("charging_active", ChargerState.Charging)]
    [InlineData("charging", ChargerState.Charging)]
    [InlineData("complete", ChargerState.Complete)]
    [InlineData("connected", ChargerState.Connected)]
    [InlineData("fault", ChargerState.Fault)]
    [InlineData("error", ChargerState.Fault)]
    [InlineData("", ChargerState.Unknown)]
    [InlineData(null, ChargerState.Unknown)]
    public void ParseChargerState_ReturnsCorrectState(string? input, ChargerState expected)
    {
        VehicleService.ParseChargerState(input).Should().Be(expected);
    }

    // === ParseTirePressure ===

    [Theory]
    [InlineData("OK", TirePressureStatus.Ok)]
    [InlineData("LOW", TirePressureStatus.Low)]
    [InlineData("HIGH", TirePressureStatus.High)]
    [InlineData("CRITICAL", TirePressureStatus.Critical)]
    [InlineData("ok", TirePressureStatus.Ok)]  // Tests ToUpper
    [InlineData("", TirePressureStatus.Unknown)]
    [InlineData(null, TirePressureStatus.Unknown)]
    public void ParseTirePressure_ReturnsCorrectStatus(string? input, TirePressureStatus expected)
    {
        VehicleService.ParseTirePressure(input).Should().Be(expected);
    }

    // === ParseBoolFromString ===

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    [InlineData("0", false)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("maybe", null)]
    public void ParseBoolFromString_HandlesVariousInputs(string? input, bool? expected)
    {
        VehicleService.ParseBoolFromString(input).Should().Be(expected);
    }

    // === ParseBoolFromObject ===

    [Fact]
    public void ParseBoolFromObject_HandlesNull()
    {
        VehicleService.ParseBoolFromObject(null).Should().BeNull();
    }

    [Fact]
    public void ParseBoolFromObject_HandlesBool()
    {
        VehicleService.ParseBoolFromObject(true).Should().BeTrue();
        VehicleService.ParseBoolFromObject(false).Should().BeFalse();
    }

    [Fact]
    public void ParseBoolFromObject_HandlesInt()
    {
        VehicleService.ParseBoolFromObject(1).Should().BeTrue();
        VehicleService.ParseBoolFromObject(0).Should().BeFalse();
    }

    [Fact]
    public void ParseBoolFromObject_HandlesJsonElement_True()
    {
        var json = JsonDocument.Parse("true").RootElement;
        VehicleService.ParseBoolFromObject(json).Should().BeTrue();
    }

    [Fact]
    public void ParseBoolFromObject_HandlesJsonElement_False()
    {
        var json = JsonDocument.Parse("false").RootElement;
        VehicleService.ParseBoolFromObject(json).Should().BeFalse();
    }

    [Fact]
    public void ParseBoolFromObject_HandlesJsonElement_Number()
    {
        var json = JsonDocument.Parse("1").RootElement;
        VehicleService.ParseBoolFromObject(json).Should().BeTrue();

        var zero = JsonDocument.Parse("0").RootElement;
        VehicleService.ParseBoolFromObject(zero).Should().BeFalse();
    }

    [Fact]
    public void ParseBoolFromObject_HandlesJsonElement_String()
    {
        var json = JsonDocument.Parse("\"true\"").RootElement;
        VehicleService.ParseBoolFromObject(json).Should().BeTrue();

        var falseJson = JsonDocument.Parse("\"false\"").RootElement;
        VehicleService.ParseBoolFromObject(falseJson).Should().BeFalse();
    }

    // === KmToMiles ===

    [Fact]
    public void KmToMiles_ConvertsCorrectly()
    {
        var result = VehicleService.KmToMiles(100.0);
        result.Should().BeApproximately(62.1371, 0.001);
    }

    [Fact]
    public void KmToMiles_Null_ReturnsNull()
    {
        VehicleService.KmToMiles(null).Should().BeNull();
    }

    // === BarToPsi ===

    [Fact]
    public void BarToPsi_ConvertsCorrectly()
    {
        // 1 bar ≈ 14.5038 PSI
        var result = VehicleService.BarToPsi(2.0);
        result.Should().BeApproximately(29.0076, 0.01);
    }

    [Fact]
    public void BarToPsi_Null_ReturnsNull()
    {
        VehicleService.BarToPsi(null).Should().BeNull();
    }

    // === FormatGearGuardStatus ===

    [Fact]
    public void FormatGearGuardStatus_FormatsSnakeCase()
    {
        VehicleService.FormatGearGuardStatus("away_from_home").Should().Be("Away From Home");
    }

    [Fact]
    public void FormatGearGuardStatus_FormatsSimpleValue()
    {
        VehicleService.FormatGearGuardStatus("enabled").Should().Be("Enabled");
    }

    [Fact]
    public void FormatGearGuardStatus_Null_ReturnsNull()
    {
        VehicleService.FormatGearGuardStatus(null).Should().BeNull();
    }

    [Fact]
    public void FormatGearGuardStatus_Empty_ReturnsNull()
    {
        VehicleService.FormatGearGuardStatus("").Should().BeNull();
    }

    // === FormatOtaVersion ===

    [Fact]
    public void FormatOtaVersion_FormatsCorrectly()
    {
        VehicleService.FormatOtaVersion(2025, 48, 3).Should().Be("2025.48.3");
    }

    [Fact]
    public void FormatOtaVersion_NullYear_ReturnsNull()
    {
        VehicleService.FormatOtaVersion(null, 48, 3).Should().BeNull();
    }

    [Fact]
    public void FormatOtaVersion_ZeroYear_ReturnsNull()
    {
        VehicleService.FormatOtaVersion(0, 48, 3).Should().BeNull();
    }

    [Fact]
    public void FormatOtaVersion_NullWeekAndNumber_DefaultsToZero()
    {
        VehicleService.FormatOtaVersion(2025, null, null).Should().Be("2025.0.0");
    }

    // === ParseClosedState ===

    [Theory]
    [InlineData("closed", true)]
    [InlineData("Closed", true)]
    [InlineData("open", false)]
    [InlineData("Open", false)]
    [InlineData("undefined", null)]
    [InlineData("unknown", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseClosedState_ReturnsCorrectState(string? input, bool? expected)
    {
        VehicleService.ParseClosedState(input).Should().Be(expected);
    }

    // === ParseLockedState ===

    [Theory]
    [InlineData("locked", true)]
    [InlineData("Locked", true)]
    [InlineData("unlocked", false)]
    [InlineData("Unlocked", false)]
    [InlineData("undefined", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseLockedState_ReturnsCorrectState(string? input, bool? expected)
    {
        VehicleService.ParseLockedState(input).Should().Be(expected);
    }

    // === ParseChargePortState ===

    [Theory]
    [InlineData("open", true)]
    [InlineData("Open", true)]
    [InlineData("closed", false)]
    [InlineData("Closed", false)]
    [InlineData("locked", false)]    // Door is closed (and locked)
    [InlineData("Locked", false)]
    [InlineData("unlocked", false)]  // Door is closed (but unlocked)
    [InlineData("Unlocked", false)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseChargePortState_ReturnsCorrectState(string? input, bool? expected)
    {
        VehicleService.ParseChargePortState(input).Should().Be(expected);
    }

    // === ParseServiceMode ===

    [Theory]
    [InlineData("on", true)]
    [InlineData("On", true)]
    [InlineData("off", false)]
    [InlineData("Off", false)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseServiceMode_ReturnsCorrectState(string? input, bool? expected)
    {
        VehicleService.ParseServiceMode(input).Should().Be(expected);
    }
}
