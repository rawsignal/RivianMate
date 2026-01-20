using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RivianMate.Core.Enums;

namespace RivianMate.Infrastructure.Nhtsa;

/// <summary>
/// Service to fetch vehicle recalls from the NHTSA Recalls API
/// https://api.nhtsa.gov/recalls/recallsByVehicle
/// </summary>
public class NhtsaRecallService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NhtsaRecallService> _logger;
    private const string BaseUrl = "https://api.nhtsa.gov/recalls/recallsByVehicle";

    public NhtsaRecallService(HttpClient httpClient, ILogger<NhtsaRecallService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Get recalls for a vehicle by make, model, and year.
    /// </summary>
    public async Task<RecallResult> GetRecallsAsync(
        string make,
        string model,
        int year,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}?make={Uri.EscapeDataString(make)}&model={Uri.EscapeDataString(model)}&modelYear={year}";
            _logger.LogDebug("Calling NHTSA Recalls API: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<NhtsaRecallResponse>(url, cancellationToken);

            if (response?.Results == null)
            {
                _logger.LogWarning("NHTSA Recalls API returned null response for {Make} {Model} {Year}", make, model, year);
                return new RecallResult { Success = true, Recalls = [] };
            }

            var recalls = response.Results.Select(r => new RecallInfo
            {
                CampaignNumber = r.NHTSACampaignNumber ?? "",
                ReportReceivedDate = ParseDate(r.ReportReceivedDate),
                Component = r.Component ?? "",
                Summary = r.Summary ?? "",
                Consequence = r.Consequence ?? "",
                Remedy = r.Remedy ?? "",
                Notes = r.Notes ?? "",
                Manufacturer = r.Manufacturer ?? ""
            }).ToList();

            _logger.LogInformation("Found {Count} recalls for {Make} {Model} {Year}", recalls.Count, make, model, year);

            return new RecallResult
            {
                Success = true,
                Recalls = recalls
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching recalls for {Make} {Model} {Year}", make, model, year);
            return new RecallResult { Success = false, Error = "Unable to connect to NHTSA. Please try again later." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recalls for {Make} {Model} {Year}", make, model, year);
            return new RecallResult { Success = false, Error = "An error occurred while fetching recall data." };
        }
    }

    /// <summary>
    /// Get recalls for a Rivian vehicle by model and year.
    /// </summary>
    public Task<RecallResult> GetRivianRecallsAsync(
        VehicleModel model,
        int year,
        CancellationToken cancellationToken = default)
    {
        var modelName = model switch
        {
            VehicleModel.R1T => "R1T",
            VehicleModel.R1S => "R1S",
            VehicleModel.R2 => "R2",
            VehicleModel.R3 => "R3",
            _ => throw new ArgumentException($"Unknown vehicle model: {model}", nameof(model))
        };

        return GetRecallsAsync("Rivian", modelName, year, cancellationToken);
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        // NHTSA dates are typically in format "dd/MM/yyyy"
        if (DateTime.TryParse(dateStr, out var date))
            return date;

        return null;
    }
}

/// <summary>
/// Result of a recall lookup
/// </summary>
public class RecallResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<RecallInfo> Recalls { get; set; } = [];
}

/// <summary>
/// Information about a single recall
/// </summary>
public class RecallInfo
{
    public string CampaignNumber { get; set; } = "";
    public DateTime? ReportReceivedDate { get; set; }
    public string Component { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Consequence { get; set; } = "";
    public string Remedy { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Manufacturer { get; set; } = "";
}

// NHTSA Recalls API response models
internal class NhtsaRecallResponse
{
    public int Count { get; set; }
    public string? Message { get; set; }
    public List<NhtsaRecallItem>? Results { get; set; }
}

internal class NhtsaRecallItem
{
    public string? Manufacturer { get; set; }
    public string? NHTSACampaignNumber { get; set; }
    public string? ReportReceivedDate { get; set; }
    public string? Component { get; set; }
    public string? Summary { get; set; }
    public string? Consequence { get; set; }
    public string? Remedy { get; set; }
    public string? Notes { get; set; }
    public string? ModelYear { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
}
