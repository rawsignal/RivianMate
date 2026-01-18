using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RivianMate.Core.Enums;
using DriveType = RivianMate.Core.Enums.DriveType;

namespace RivianMate.Infrastructure.Nhtsa;

/// <summary>
/// Service to decode VINs using the NHTSA vPIC API
/// https://vpic.nhtsa.dot.gov/api/
/// </summary>
public class NhtsaVinDecoderService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NhtsaVinDecoderService> _logger;
    private const string BaseUrl = "https://vpic.nhtsa.dot.gov/api/vehicles/decodevin";

    public NhtsaVinDecoderService(HttpClient httpClient, ILogger<NhtsaVinDecoderService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Decode a VIN and return vehicle information
    /// </summary>
    public async Task<VinDecodedInfo?> DecodeVinAsync(string vin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vin) || vin.Length != 17)
        {
            _logger.LogWarning("Invalid VIN provided: {Vin}", vin);
            return null;
        }

        try
        {
            var url = $"{BaseUrl}/{vin}?format=json";
            _logger.LogDebug("Calling NHTSA API: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<NhtsaResponse>(url, cancellationToken);

            if (response?.Results == null)
            {
                _logger.LogWarning("NHTSA API returned null response for VIN: {Vin}", vin);
                return null;
            }

            var result = ParseResults(response.Results);
            _logger.LogInformation("Decoded VIN {Vin}: {Model} {Year} {DriveType} {Trim}",
                vin, result.Model, result.Year, result.DriveType, result.Trim);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decoding VIN {Vin} from NHTSA API", vin);
            return null;
        }
    }

    private VinDecodedInfo ParseResults(List<NhtsaResultItem> results)
    {
        var info = new VinDecodedInfo();
        var lookup = results.ToDictionary(r => r.Variable ?? "", r => r.Value ?? "", StringComparer.OrdinalIgnoreCase);

        // Make
        if (lookup.TryGetValue("Make", out var make))
        {
            info.Make = make;
        }

        // Model
        if (lookup.TryGetValue("Model", out var model))
        {
            info.ModelName = model;
            info.Model = model?.ToUpperInvariant() switch
            {
                "R1T" => VehicleModel.R1T,
                "R1S" => VehicleModel.R1S,
                "R2" => VehicleModel.R2,
                "R3" => VehicleModel.R3,
                _ => VehicleModel.Unknown
            };
        }

        // Model Year
        if (lookup.TryGetValue("Model Year", out var yearStr) && int.TryParse(yearStr, out var year))
        {
            info.Year = year;
        }

        // Trim
        if (lookup.TryGetValue("Trim", out var trim))
        {
            info.TrimName = trim;
            info.Trim = trim?.ToUpperInvariant() switch
            {
                "EXPLORE" => VehicleTrim.Explore,
                "ADVENTURE" => VehicleTrim.Adventure,
                "LAUNCH EDITION" or "LAUNCHEDITION" => VehicleTrim.LaunchEdition,
                "ASCEND" => VehicleTrim.Ascend,
                _ => VehicleTrim.Unknown
            };
        }

        // Drive Type - from "Other Engine Info" field which contains "Quad-Motor" etc.
        if (lookup.TryGetValue("Other Engine Info", out var engineInfo))
        {
            info.DriveTypeName = engineInfo;
            var normalizedEngineInfo = engineInfo?.ToUpperInvariant() ?? "";

            if (normalizedEngineInfo.Contains("QUAD"))
            {
                info.DriveType = DriveType.QuadMotor;
            }
            else if (normalizedEngineInfo.Contains("TRI"))
            {
                info.DriveType = DriveType.TriMotor;
            }
            else if (normalizedEngineInfo.Contains("DUAL"))
            {
                info.DriveType = DriveType.DualMotor;
            }
        }

        // Also check "Drive Type" field as backup
        if (info.DriveType == DriveType.Unknown && lookup.TryGetValue("Drive Type", out var driveType))
        {
            var normalizedDriveType = driveType?.ToUpperInvariant() ?? "";
            if (normalizedDriveType.Contains("AWD") || normalizedDriveType.Contains("4WD"))
            {
                // AWD could be dual or quad, not enough info
                info.DriveTypeName = driveType;
            }
        }

        return info;
    }
}

/// <summary>
/// Decoded vehicle information from NHTSA
/// </summary>
public class VinDecodedInfo
{
    public string? Make { get; set; }
    public string? ModelName { get; set; }
    public VehicleModel Model { get; set; } = VehicleModel.Unknown;
    public int? Year { get; set; }
    public string? TrimName { get; set; }
    public VehicleTrim Trim { get; set; } = VehicleTrim.Unknown;
    public string? DriveTypeName { get; set; }
    public DriveType DriveType { get; set; } = DriveType.Unknown;
    public BatteryPackType BatteryPack { get; set; } = BatteryPackType.Unknown;
}

/// <summary>
/// Utility class for decoding Rivian-specific VIN information.
/// Position 6 in Rivian VINs encodes battery pack and motor configuration.
/// Source: https://www.rivianwave.com/rivian-vin-decoder/
/// </summary>
public static class RivianVinDecoder
{
    /// <summary>
    /// Decode battery pack type and drive type from Rivian VIN position 6.
    /// </summary>
    public static (BatteryPackType BatteryPack, DriveType DriveType) DecodeFromVin(string? vin)
    {
        if (string.IsNullOrEmpty(vin) || vin.Length < 10)
            return (BatteryPackType.Unknown, DriveType.Unknown);

        // Position 6 (index 5) encodes battery/motor config
        var code = char.ToUpperInvariant(vin[5]);

        // Position 10 (index 9) encodes model year
        var modelYear = DecodeModelYear(vin[9]);

        return code switch
        {
            // A = Quad-Motor
            // Pre-2025 (Gen 1): Always Large Pack
            // 2025+ (Gen 2): Could be Large or Max, use capacity inference
            'A' when modelYear < 2025 => (BatteryPackType.Large, DriveType.QuadMotor),
            'A' => (BatteryPackType.Unknown, DriveType.QuadMotor),

            // B = Dual-Motor with various battery options
            'B' => (BatteryPackType.Unknown, DriveType.DualMotor),

            // C = Dual-Motor with Max pack
            'C' => (BatteryPackType.Max, DriveType.DualMotor),

            // F = Dual-Motor with Large pack
            'F' => (BatteryPackType.Large, DriveType.DualMotor),

            // G = Dual-Motor with Standard (LFP) pack
            'G' => (BatteryPackType.Standard, DriveType.DualMotor),

            _ => (BatteryPackType.Unknown, DriveType.Unknown)
        };
    }

    /// <summary>
    /// Get model year from VIN position 10.
    /// </summary>
    public static int GetModelYearFromVin(string? vin)
    {
        if (string.IsNullOrEmpty(vin) || vin.Length < 10)
            return 0;
        return DecodeModelYear(vin[9]);
    }

    /// <summary>
    /// Decode model year from VIN position 10 character.
    /// </summary>
    private static int DecodeModelYear(char yearCode)
    {
        return char.ToUpperInvariant(yearCode) switch
        {
            'N' => 2022,
            'P' => 2023,
            'R' => 2024,
            'S' => 2025,
            'T' => 2026,
            'V' => 2027,
            'W' => 2028,
            'X' => 2029,
            'Y' => 2030,
            _ => 0
        };
    }

    /// <summary>
    /// Infer battery pack type from reported usable capacity.
    /// Useful when VIN decoding fails or isn't available.
    /// </summary>
    public static BatteryPackType InferFromCapacity(double? capacityKwh)
    {
        if (capacityKwh == null || capacityKwh <= 0)
            return BatteryPackType.Unknown;

        // Capacity ranges (with some tolerance for degradation):
        // Standard Pack: ~105 kWh usable (100-115 range)
        // Large Pack: ~128-135 kWh usable (120-140 range)
        // Max Pack: ~141-149 kWh usable (140+ range)

        return capacityKwh.Value switch
        {
            >= 140 => BatteryPackType.Max,
            >= 120 => BatteryPackType.Large,
            >= 95 => BatteryPackType.Standard,
            _ => BatteryPackType.Unknown
        };
    }
}

// NHTSA API response models
internal class NhtsaResponse
{
    public int Count { get; set; }
    public string? Message { get; set; }
    public List<NhtsaResultItem>? Results { get; set; }
}

internal class NhtsaResultItem
{
    public string? Value { get; set; }
    public string? ValueId { get; set; }
    public string? Variable { get; set; }
    public int? VariableId { get; set; }
}
