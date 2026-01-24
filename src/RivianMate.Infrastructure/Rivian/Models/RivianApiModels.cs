using System.Text.Json.Serialization;

namespace RivianMate.Infrastructure.Rivian.Models;

// === Authentication Models ===

public class CreateCsrfTokenResponse
{
    [JsonPropertyName("data")]
    public CreateCsrfTokenData? Data { get; set; }
}

public class CreateCsrfTokenData
{
    [JsonPropertyName("createCsrfToken")]
    public CsrfTokenResult? CreateCsrfToken { get; set; }
}

public class CsrfTokenResult
{
    [JsonPropertyName("__typename")]
    public string? TypeName { get; set; }
    
    [JsonPropertyName("csrfToken")]
    public string? CsrfToken { get; set; }
    
    [JsonPropertyName("appSessionToken")]
    public string? AppSessionToken { get; set; }
}

public class LoginResponse
{
    [JsonPropertyName("data")]
    public LoginData? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<GraphQlError>? Errors { get; set; }
}

public class LoginData
{
    [JsonPropertyName("login")]
    public LoginResult? Login { get; set; }
    
    [JsonPropertyName("loginWithOTP")]
    public LoginResult? LoginWithOtp { get; set; }
}

public class LoginResult
{
    [JsonPropertyName("__typename")]
    public string? TypeName { get; set; }
    
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }
    
    [JsonPropertyName("userSessionToken")]
    public string? UserSessionToken { get; set; }
    
    // For MFA response
    [JsonPropertyName("otpToken")]
    public string? OtpToken { get; set; }
}

// === User Info Models ===

public class GetUserInfoResponse
{
    [JsonPropertyName("data")]
    public GetUserInfoData? Data { get; set; }
    
    [JsonPropertyName("errors")]
    public List<GraphQlError>? Errors { get; set; }
}

public class GetUserInfoData
{
    [JsonPropertyName("currentUser")]
    public CurrentUser? CurrentUser { get; set; }
}

public class CurrentUser
{
    [JsonPropertyName("__typename")]
    public string? TypeName { get; set; }
    
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }
    
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("vehicles")]
    public List<UserVehicle>? Vehicles { get; set; }
}

public class UserVehicle
{
    [JsonPropertyName("__typename")]
    public string? TypeName { get; set; }
    
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("owner")]
    public VehicleOwner? Owner { get; set; }
    
    [JsonPropertyName("roles")]
    public List<string>? Roles { get; set; }
    
    [JsonPropertyName("state")]
    public string? State { get; set; }
    
    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }
    
    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }
    
    [JsonPropertyName("vin")]
    public string? Vin { get; set; }
    
    [JsonPropertyName("vas")]
    public VehicleAccessory? Vas { get; set; }
    
    [JsonPropertyName("vehicle")]
    public VehicleDetails? Vehicle { get; set; }
}

public class VehicleOwner
{
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }
}

public class VehicleAccessory
{
    [JsonPropertyName("vasVehicleId")]
    public string? VasVehicleId { get; set; }
    
    [JsonPropertyName("vehiclePublicKey")]
    public string? VehiclePublicKey { get; set; }
}

public class VehicleDetails
{
    [JsonPropertyName("modelYear")]
    public int? ModelYear { get; set; }
    
    [JsonPropertyName("make")]
    public string? Make { get; set; }
    
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    
    [JsonPropertyName("expectedBuildDate")]
    public string? ExpectedBuildDate { get; set; }
    
    [JsonPropertyName("plannedBuildDate")]
    public string? PlannedBuildDate { get; set; }
    
    [JsonPropertyName("expectedGeneralAssemblyStartDate")]
    public string? ExpectedGeneralAssemblyStartDate { get; set; }
    
    [JsonPropertyName("actualGeneralAssemblyDate")]
    public string? ActualGeneralAssemblyDate { get; set; }
    
    [JsonPropertyName("mobileConfiguration")]
    public MobileConfiguration? MobileConfiguration { get; set; }
}

public class MobileConfiguration
{
    [JsonPropertyName("trimOption")]
    public ConfigOption? TrimOption { get; set; }
    
    [JsonPropertyName("exteriorColorOption")]
    public ConfigOption? ExteriorColorOption { get; set; }
    
    [JsonPropertyName("interiorColorOption")]
    public ConfigOption? InteriorColorOption { get; set; }
    
    [JsonPropertyName("driveSystemOption")]
    public ConfigOption? DriveSystemOption { get; set; }
    
    [JsonPropertyName("batteryOption")]
    public ConfigOption? BatteryOption { get; set; }
    
    [JsonPropertyName("wheelOption")]
    public ConfigOption? WheelOption { get; set; }
}

public class ConfigOption
{
    [JsonPropertyName("optionId")]
    public string? OptionId { get; set; }
    
    [JsonPropertyName("optionName")]
    public string? OptionName { get; set; }
}

// === Vehicle State Models ===

public class GetVehicleStateResponse
{
    [JsonPropertyName("data")]
    public GetVehicleStateData? Data { get; set; }
    
    [JsonPropertyName("errors")]
    public List<GraphQlError>? Errors { get; set; }
}

public class GetVehicleStateData
{
    [JsonPropertyName("vehicleState")]
    public RivianVehicleState? VehicleState { get; set; }
}

public class RivianVehicleState
{
    [JsonPropertyName("__typename")]
    public string? TypeName { get; set; }

    // === Location & Movement ===
    [JsonPropertyName("gnssLocation")]
    public GnssLocation? GnssLocation { get; set; }

    [JsonPropertyName("gnssSpeed")]
    public TimestampedValue<double?>? GnssSpeed { get; set; }

    [JsonPropertyName("gnssAltitude")]
    public TimestampedValue<double?>? GnssAltitude { get; set; }

    [JsonPropertyName("gnssBearing")]
    public TimestampedValue<double?>? GnssBearing { get; set; }

    // === Driver ===
    [JsonPropertyName("activeDriverName")]
    public TimestampedValue<string?>? ActiveDriverName { get; set; }
    
    // === Battery ===
    [JsonPropertyName("batteryLevel")]
    public TimestampedValue<double?>? BatteryLevel { get; set; }

    [JsonPropertyName("batteryLimit")]
    public TimestampedValue<double?>? BatteryLimit { get; set; }

    /// <summary>
    /// Current usable battery capacity in kWh.
    /// This is the key field for battery health estimation!
    /// Reports actual current capacity (degrades over time from original).
    /// </summary>
    [JsonPropertyName("batteryCapacity")]
    public TimestampedValue<double?>? BatteryCapacity { get; set; }

    [JsonPropertyName("batteryCellType")]
    public TimestampedValue<string?>? BatteryCellType { get; set; }

    [JsonPropertyName("batteryNeedsLfpCalibration")]
    public TimestampedValue<object?>? BatteryNeedsLfpCalibration { get; set; }

    [JsonPropertyName("twelveVoltBatteryHealth")]
    public TimestampedValue<string?>? TwelveVoltBatteryHealth { get; set; }

    // === Range ===
    [JsonPropertyName("distanceToEmpty")]
    public TimestampedValue<double?>? DistanceToEmpty { get; set; }

    [JsonPropertyName("rangeThreshold")]
    public TimestampedValue<string?>? RangeThreshold { get; set; }

    // === Charging ===
    [JsonPropertyName("chargerState")]
    public TimestampedValue<string?>? ChargerState { get; set; }

    [JsonPropertyName("chargerStatus")]
    public TimestampedValue<string?>? ChargerStatus { get; set; }

    [JsonPropertyName("chargerDerateStatus")]
    public TimestampedValue<string?>? ChargerDerateStatus { get; set; }

    [JsonPropertyName("chargePortState")]
    public TimestampedValue<string?>? ChargePortState { get; set; }

    [JsonPropertyName("chargingDisabledAll")]
    public TimestampedValue<int?>? ChargingDisabledAll { get; set; }

    [JsonPropertyName("remoteChargingAvailable")]
    public TimestampedValue<int?>? RemoteChargingAvailable { get; set; }

    [JsonPropertyName("timeToEndOfCharge")]
    public TimestampedValue<int?>? TimeToEndOfCharge { get; set; }
    
    // === Power & Drive ===
    [JsonPropertyName("powerState")]
    public TimestampedValue<string?>? PowerState { get; set; }

    [JsonPropertyName("gearStatus")]
    public TimestampedValue<string?>? GearStatus { get; set; }

    [JsonPropertyName("driveMode")]
    public TimestampedValue<string?>? DriveMode { get; set; }

    [JsonPropertyName("serviceMode")]
    public TimestampedValue<string?>? ServiceMode { get; set; }

    [JsonPropertyName("carWashMode")]
    public TimestampedValue<string?>? CarWashMode { get; set; }

    [JsonPropertyName("trailerStatus")]
    public TimestampedValue<string?>? TrailerStatus { get; set; }

    [JsonPropertyName("rearHitchStatus")]
    public TimestampedValue<string?>? RearHitchStatus { get; set; }

    // === Cold Weather Limitations ===
    [JsonPropertyName("limitedAccelCold")]
    public TimestampedValue<int?>? LimitedAccelCold { get; set; }

    [JsonPropertyName("limitedRegenCold")]
    public TimestampedValue<int?>? LimitedRegenCold { get; set; }

    // === Odometer ===
    [JsonPropertyName("vehicleMileage")]
    public TimestampedValue<double?>? VehicleMileage { get; set; }
    
    // === Climate ===
    [JsonPropertyName("cabinClimateInteriorTemperature")]
    public TimestampedValue<double?>? CabinClimateInteriorTemperature { get; set; }

    [JsonPropertyName("cabinClimateDriverTemperature")]
    public TimestampedValue<double?>? CabinClimateDriverTemperature { get; set; }

    [JsonPropertyName("cabinPreconditioningStatus")]
    public TimestampedValue<string?>? CabinPreconditioningStatus { get; set; }

    [JsonPropertyName("cabinPreconditioningType")]
    public TimestampedValue<string?>? CabinPreconditioningType { get; set; }

    [JsonPropertyName("defrostDefogStatus")]
    public TimestampedValue<string?>? DefrostDefogStatus { get; set; }

    [JsonPropertyName("petModeStatus")]
    public TimestampedValue<string?>? PetModeStatus { get; set; }

    [JsonPropertyName("petModeTemperatureStatus")]
    public TimestampedValue<string?>? PetModeTemperatureStatus { get; set; }

    // === Seats ===
    [JsonPropertyName("seatFrontLeftHeat")]
    public TimestampedValue<string?>? SeatFrontLeftHeat { get; set; }

    [JsonPropertyName("seatFrontLeftVent")]
    public TimestampedValue<string?>? SeatFrontLeftVent { get; set; }

    [JsonPropertyName("seatFrontRightHeat")]
    public TimestampedValue<string?>? SeatFrontRightHeat { get; set; }

    [JsonPropertyName("seatFrontRightVent")]
    public TimestampedValue<string?>? SeatFrontRightVent { get; set; }

    [JsonPropertyName("seatRearLeftHeat")]
    public TimestampedValue<string?>? SeatRearLeftHeat { get; set; }

    [JsonPropertyName("seatRearRightHeat")]
    public TimestampedValue<string?>? SeatRearRightHeat { get; set; }

    [JsonPropertyName("seatThirdRowLeftHeat")]
    public TimestampedValue<string?>? SeatThirdRowLeftHeat { get; set; }

    [JsonPropertyName("seatThirdRowRightHeat")]
    public TimestampedValue<string?>? SeatThirdRowRightHeat { get; set; }

    [JsonPropertyName("steeringWheelHeat")]
    public TimestampedValue<string?>? SteeringWheelHeat { get; set; }
    
    // === Doors ===
    [JsonPropertyName("doorFrontLeftLocked")]
    public TimestampedValue<string?>? DoorFrontLeftLocked { get; set; }

    [JsonPropertyName("doorFrontLeftClosed")]
    public TimestampedValue<string?>? DoorFrontLeftClosed { get; set; }

    [JsonPropertyName("doorFrontRightLocked")]
    public TimestampedValue<string?>? DoorFrontRightLocked { get; set; }

    [JsonPropertyName("doorFrontRightClosed")]
    public TimestampedValue<string?>? DoorFrontRightClosed { get; set; }

    [JsonPropertyName("doorRearLeftLocked")]
    public TimestampedValue<string?>? DoorRearLeftLocked { get; set; }

    [JsonPropertyName("doorRearLeftClosed")]
    public TimestampedValue<string?>? DoorRearLeftClosed { get; set; }

    [JsonPropertyName("doorRearRightLocked")]
    public TimestampedValue<string?>? DoorRearRightLocked { get; set; }

    [JsonPropertyName("doorRearRightClosed")]
    public TimestampedValue<string?>? DoorRearRightClosed { get; set; }

    // === Windows ===
    [JsonPropertyName("windowFrontLeftClosed")]
    public TimestampedValue<string?>? WindowFrontLeftClosed { get; set; }

    [JsonPropertyName("windowFrontLeftCalibrated")]
    public TimestampedValue<string?>? WindowFrontLeftCalibrated { get; set; }

    [JsonPropertyName("windowFrontRightClosed")]
    public TimestampedValue<string?>? WindowFrontRightClosed { get; set; }

    [JsonPropertyName("windowFrontRightCalibrated")]
    public TimestampedValue<string?>? WindowFrontRightCalibrated { get; set; }

    [JsonPropertyName("windowRearLeftClosed")]
    public TimestampedValue<string?>? WindowRearLeftClosed { get; set; }

    [JsonPropertyName("windowRearLeftCalibrated")]
    public TimestampedValue<string?>? WindowRearLeftCalibrated { get; set; }

    [JsonPropertyName("windowRearRightClosed")]
    public TimestampedValue<string?>? WindowRearRightClosed { get; set; }

    [JsonPropertyName("windowRearRightCalibrated")]
    public TimestampedValue<string?>? WindowRearRightCalibrated { get; set; }

    [JsonPropertyName("windowsNextAction")]
    public TimestampedValue<string?>? WindowsNextAction { get; set; }

    // === Frunk ===
    [JsonPropertyName("closureFrunkLocked")]
    public TimestampedValue<string?>? ClosureFrunkLocked { get; set; }

    [JsonPropertyName("closureFrunkClosed")]
    public TimestampedValue<string?>? ClosureFrunkClosed { get; set; }

    [JsonPropertyName("closureFrunkNextAction")]
    public TimestampedValue<string?>? ClosureFrunkNextAction { get; set; }

    // === Liftgate (R1S) ===
    [JsonPropertyName("closureLiftgateLocked")]
    public TimestampedValue<string?>? ClosureLiftgateLocked { get; set; }

    [JsonPropertyName("closureLiftgateClosed")]
    public TimestampedValue<string?>? ClosureLiftgateClosed { get; set; }

    [JsonPropertyName("closureLiftgateNextAction")]
    public TimestampedValue<string?>? ClosureLiftgateNextAction { get; set; }

    // === Tailgate (R1T) ===
    [JsonPropertyName("closureTailgateLocked")]
    public TimestampedValue<string?>? ClosureTailgateLocked { get; set; }

    [JsonPropertyName("closureTailgateClosed")]
    public TimestampedValue<string?>? ClosureTailgateClosed { get; set; }

    [JsonPropertyName("closureTailgateNextAction")]
    public TimestampedValue<string?>? ClosureTailgateNextAction { get; set; }

    // === Tonneau (R1T) ===
    [JsonPropertyName("closureTonneauLocked")]
    public TimestampedValue<string?>? ClosureTonneauLocked { get; set; }

    [JsonPropertyName("closureTonneauClosed")]
    public TimestampedValue<string?>? ClosureTonneauClosed { get; set; }

    [JsonPropertyName("closureTonneauNextAction")]
    public TimestampedValue<string?>? ClosureTonneauNextAction { get; set; }

    // === Side Bins (R1T) ===
    [JsonPropertyName("closureSideBinLeftLocked")]
    public TimestampedValue<string?>? ClosureSideBinLeftLocked { get; set; }

    [JsonPropertyName("closureSideBinLeftClosed")]
    public TimestampedValue<string?>? ClosureSideBinLeftClosed { get; set; }

    [JsonPropertyName("closureSideBinLeftNextAction")]
    public TimestampedValue<string?>? ClosureSideBinLeftNextAction { get; set; }

    [JsonPropertyName("closureSideBinRightLocked")]
    public TimestampedValue<string?>? ClosureSideBinRightLocked { get; set; }

    [JsonPropertyName("closureSideBinRightClosed")]
    public TimestampedValue<string?>? ClosureSideBinRightClosed { get; set; }

    [JsonPropertyName("closureSideBinRightNextAction")]
    public TimestampedValue<string?>? ClosureSideBinRightNextAction { get; set; }

    // === Gear Guard ===
    [JsonPropertyName("gearGuardLocked")]
    public TimestampedValue<string?>? GearGuardLocked { get; set; }
    
    // === Tires - Status ===
    [JsonPropertyName("tirePressureStatusFrontLeft")]
    public TimestampedValue<string?>? TirePressureStatusFrontLeft { get; set; }

    [JsonPropertyName("tirePressureStatusFrontRight")]
    public TimestampedValue<string?>? TirePressureStatusFrontRight { get; set; }

    [JsonPropertyName("tirePressureStatusRearLeft")]
    public TimestampedValue<string?>? TirePressureStatusRearLeft { get; set; }

    [JsonPropertyName("tirePressureStatusRearRight")]
    public TimestampedValue<string?>? TirePressureStatusRearRight { get; set; }

    // === Tires - Actual Pressure (PSI) ===
    [JsonPropertyName("tirePressureFrontLeft")]
    public TimestampedValue<double?>? TirePressureFrontLeft { get; set; }

    [JsonPropertyName("tirePressureFrontRight")]
    public TimestampedValue<double?>? TirePressureFrontRight { get; set; }

    [JsonPropertyName("tirePressureRearLeft")]
    public TimestampedValue<double?>? TirePressureRearLeft { get; set; }

    [JsonPropertyName("tirePressureRearRight")]
    public TimestampedValue<double?>? TirePressureRearRight { get; set; }

    // === Tires - Validity ===
    [JsonPropertyName("tirePressureStatusValidFrontLeft")]
    public TimestampedValue<string?>? TirePressureStatusValidFrontLeft { get; set; }

    [JsonPropertyName("tirePressureStatusValidFrontRight")]
    public TimestampedValue<string?>? TirePressureStatusValidFrontRight { get; set; }

    [JsonPropertyName("tirePressureStatusValidRearLeft")]
    public TimestampedValue<string?>? TirePressureStatusValidRearLeft { get; set; }

    [JsonPropertyName("tirePressureStatusValidRearRight")]
    public TimestampedValue<string?>? TirePressureStatusValidRearRight { get; set; }
    
    // === OTA - Current Version ===
    [JsonPropertyName("otaCurrentVersion")]
    public TimestampedValue<string?>? OtaCurrentVersion { get; set; }

    [JsonPropertyName("otaCurrentVersionNumber")]
    public TimestampedValue<int?>? OtaCurrentVersionNumber { get; set; }

    [JsonPropertyName("otaCurrentVersionWeek")]
    public TimestampedValue<int?>? OtaCurrentVersionWeek { get; set; }

    [JsonPropertyName("otaCurrentVersionYear")]
    public TimestampedValue<int?>? OtaCurrentVersionYear { get; set; }

    [JsonPropertyName("otaCurrentVersionGitHash")]
    public TimestampedValue<string?>? OtaCurrentVersionGitHash { get; set; }

    // === OTA - Available Version ===
    [JsonPropertyName("otaAvailableVersion")]
    public TimestampedValue<string?>? OtaAvailableVersion { get; set; }

    [JsonPropertyName("otaAvailableVersionNumber")]
    public TimestampedValue<int?>? OtaAvailableVersionNumber { get; set; }

    [JsonPropertyName("otaAvailableVersionWeek")]
    public TimestampedValue<int?>? OtaAvailableVersionWeek { get; set; }

    [JsonPropertyName("otaAvailableVersionYear")]
    public TimestampedValue<int?>? OtaAvailableVersionYear { get; set; }

    [JsonPropertyName("otaAvailableVersionGitHash")]
    public TimestampedValue<string?>? OtaAvailableVersionGitHash { get; set; }

    // === OTA - Status ===
    [JsonPropertyName("otaStatus")]
    public TimestampedValue<string?>? OtaStatus { get; set; }

    [JsonPropertyName("otaCurrentStatus")]
    public TimestampedValue<string?>? OtaCurrentStatus { get; set; }

    [JsonPropertyName("otaDownloadProgress")]
    public TimestampedValue<int?>? OtaDownloadProgress { get; set; }

    [JsonPropertyName("otaInstallProgress")]
    public TimestampedValue<int?>? OtaInstallProgress { get; set; }

    [JsonPropertyName("otaInstallDuration")]
    public TimestampedValue<int?>? OtaInstallDuration { get; set; }

    [JsonPropertyName("otaInstallTime")]
    public TimestampedValue<int?>? OtaInstallTime { get; set; }

    [JsonPropertyName("otaInstallReady")]
    public TimestampedValue<string?>? OtaInstallReady { get; set; }

    [JsonPropertyName("otaInstallType")]
    public TimestampedValue<string?>? OtaInstallType { get; set; }
    
    // === Alarm ===
    [JsonPropertyName("alarmSoundStatus")]
    public TimestampedValue<string?>? AlarmSoundStatus { get; set; }

    // === Battery Thermal ===
    [JsonPropertyName("batteryHvThermalEvent")]
    public TimestampedValue<string?>? BatteryHvThermalEvent { get; set; }

    [JsonPropertyName("batteryHvThermalEventPropagation")]
    public TimestampedValue<string?>? BatteryHvThermalEventPropagation { get; set; }

    // === Fluids ===
    [JsonPropertyName("wiperFluidState")]
    public TimestampedValue<string?>? WiperFluidState { get; set; }

    [JsonPropertyName("brakeFluidLow")]
    public TimestampedValue<string?>? BrakeFluidLow { get; set; }

    // === Gear Guard Video ===
    [JsonPropertyName("gearGuardVideoStatus")]
    public TimestampedValue<string?>? GearGuardVideoStatus { get; set; }

    [JsonPropertyName("gearGuardVideoMode")]
    public TimestampedValue<string?>? GearGuardVideoMode { get; set; }

    [JsonPropertyName("gearGuardVideoTermsAccepted")]
    public TimestampedValue<string?>? GearGuardVideoTermsAccepted { get; set; }

    // === Bluetooth Module Status ===
    [JsonPropertyName("btmFfHardwareFailureStatus")]
    public TimestampedValue<string?>? BtmFfHardwareFailureStatus { get; set; }

    [JsonPropertyName("btmRfHardwareFailureStatus")]
    public TimestampedValue<string?>? BtmRfHardwareFailureStatus { get; set; }

    [JsonPropertyName("btmIcHardwareFailureStatus")]
    public TimestampedValue<string?>? BtmIcHardwareFailureStatus { get; set; }

    [JsonPropertyName("btmRfdHardwareFailureStatus")]
    public TimestampedValue<string?>? BtmRfdHardwareFailureStatus { get; set; }

    [JsonPropertyName("btmLfdHardwareFailureStatus")]
    public TimestampedValue<string?>? BtmLfdHardwareFailureStatus { get; set; }

    [JsonPropertyName("btmOcHardwareFailureStatus")]
    public TimestampedValue<string?>? BtmOcHardwareFailureStatus { get; set; }
}

public class GnssLocation
{
    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }
    
    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }
    
    [JsonPropertyName("timeStamp")]
    public string? TimeStamp { get; set; }
    
    [JsonPropertyName("isAuthorized")]
    public bool? IsAuthorized { get; set; }
}

public class TimestampedValue<T>
{
    [JsonPropertyName("timeStamp")]
    public string? TimeStamp { get; set; }
    
    [JsonPropertyName("value")]
    public T? Value { get; set; }
}

// === Vehicle Images ===

public class GetVehicleMobileImagesResponse
{
    [JsonPropertyName("data")]
    public GetVehicleMobileImagesData? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<GraphQlError>? Errors { get; set; }
}

public class GetVehicleMobileImagesData
{
    [JsonPropertyName("getVehicleMobileImages")]
    public List<VehicleMobileImage>? GetVehicleMobileImages { get; set; }
}

public class VehicleMobileImage
{
    [JsonPropertyName("__typename")]
    public string? TypeName { get; set; }

    [JsonPropertyName("vehicleId")]
    public string? VehicleId { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("resolution")]
    public string? Resolution { get; set; }

    /// <summary>
    /// Image size: "large", "small", etc.
    /// </summary>
    [JsonPropertyName("size")]
    public string? Size { get; set; }

    /// <summary>
    /// Design variant: "dark", "light"
    /// </summary>
    [JsonPropertyName("design")]
    public string? Design { get; set; }

    /// <summary>
    /// Placement/angle: "threequarter", "side", etc.
    /// </summary>
    [JsonPropertyName("placement")]
    public string? Placement { get; set; }
}

// === Common ===

public class GraphQlError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Path to the field that caused the error.
    /// Uses JsonElement because the path can contain both strings (field names)
    /// and integers (array indices).
    /// </summary>
    [JsonPropertyName("path")]
    public System.Text.Json.JsonElement? Path { get; set; }

    [JsonPropertyName("extensions")]
    public ErrorExtensions? Extensions { get; set; }
}

public class ErrorExtensions
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class GraphQlRequest
{
    [JsonPropertyName("operationName")]
    public string? OperationName { get; set; }
    
    [JsonPropertyName("variables")]
    public object? Variables { get; set; }
    
    [JsonPropertyName("query")]
    public string? Query { get; set; }
}
