using Microsoft.EntityFrameworkCore;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Provides battery care analysis and tips based on cell chemistry and usage patterns.
/// </summary>
public class BatteryCareService
{
    private readonly RivianMateDbContext _db;
    private readonly ILogger<BatteryCareService> _logger;

    public BatteryCareService(RivianMateDbContext db, ILogger<BatteryCareService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get battery care analysis and tips for a vehicle.
    /// </summary>
    public async Task<BatteryCareAnalysis> GetBatteryCareAnalysisAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle == null)
        {
            return new BatteryCareAnalysis { Tips = GetGenericTips(null) };
        }

        var cellType = ParseCellType(vehicle.BatteryCellType);

        // Fallback: if cell type is unknown but we know it's a 2025+ Standard pack, assume LFP
        if (cellType == BatteryCellType.Unknown &&
            vehicle.BatteryPack == Core.Enums.BatteryPackType.Standard &&
            vehicle.Year >= 2025)
        {
            cellType = BatteryCellType.LFP;
        }

        // Get charge limit history from the last 30 days
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var chargeLimitHistory = await _db.VehicleStates
            .Where(s => s.VehicleId == vehicleId && s.Timestamp >= thirtyDaysAgo && s.BatteryLimit != null)
            .Select(s => new { s.Timestamp, s.BatteryLimit })
            .ToListAsync(cancellationToken);

        // Get completed drives from the last 30 days
        var recentDrives = await _db.Drives
            .Where(d => d.VehicleId == vehicleId && !d.IsActive && d.StartTime >= thirtyDaysAgo)
            .Select(d => new { d.StartTime, d.DistanceMiles })
            .ToListAsync(cancellationToken);

        // Analyze charge limit patterns
        var analysis = new BatteryCareAnalysis
        {
            CellType = cellType,
            CellTypeDisplayName = GetCellTypeDisplayName(cellType),
            NeedsLfpCalibration = await CheckLfpCalibrationNeeded(vehicleId, cancellationToken)
        };

        // Calculate driving statistics
        if (recentDrives.Count > 0)
        {
            analysis.TotalDrivesLast30Days = recentDrives.Count;
            analysis.TotalMilesLast30Days = recentDrives.Sum(d => d.DistanceMiles ?? 0);

            // Count unique days with drives
            var daysWithDrives = recentDrives
                .Select(d => d.StartTime.Date)
                .Distinct()
                .Count();
            analysis.DaysWithDrives = daysWithDrives;

            // Calculate average daily miles (based on days with driving activity)
            if (daysWithDrives > 0)
            {
                analysis.AverageDailyMiles = analysis.TotalMilesLast30Days / daysWithDrives;
            }
        }

        if (chargeLimitHistory.Count > 0)
        {
            var limits = chargeLimitHistory.Select(h => h.BatteryLimit!.Value).ToList();
            analysis.AverageChargeLimit = limits.Average();
            analysis.MaxChargeLimit = limits.Max();
            analysis.ChargesAbove90Percent = limits.Count(l => l > 90);
            analysis.ChargesAt100Percent = limits.Count(l => l >= 100);
            analysis.TotalChargeRecords = limits.Count;

            // Calculate percentage of time spent at high charge limits
            if (limits.Count > 0)
            {
                analysis.PercentTimeAbove90 = (double)analysis.ChargesAbove90Percent / limits.Count * 100;
                analysis.PercentTimeAt100 = (double)analysis.ChargesAt100Percent / limits.Count * 100;
            }
        }

        // Generate personalized tips based on analysis
        analysis.Tips = GenerateTips(analysis);

        return analysis;
    }

    private BatteryCellType ParseCellType(string? cellTypeString)
    {
        if (string.IsNullOrEmpty(cellTypeString))
            return BatteryCellType.Unknown;

        var normalized = cellTypeString.ToUpperInvariant();

        return normalized switch
        {
            // Direct chemistry types
            "NMC" => BatteryCellType.NMC,
            "LFP" => BatteryCellType.LFP,

            // Samsung NMC cell models (from Rivian API)
            "50G" => BatteryCellType.NMC,  // Large pack - Samsung 50G
            "53G" => BatteryCellType.NMC,  // Max pack - Samsung 53G

            // LFP cell models (research needed for exact model name)
            // Standard pack 2025+ uses LFP cells

            _ => BatteryCellType.Unknown
        };
    }

    private string GetCellTypeDisplayName(BatteryCellType cellType) => cellType switch
    {
        BatteryCellType.NMC => "Nickel Manganese Cobalt (NMC)",
        BatteryCellType.LFP => "Lithium Iron Phosphate (LFP)",
        _ => "Unknown"
    };

    private async Task<bool> CheckLfpCalibrationNeeded(int vehicleId, CancellationToken cancellationToken)
    {
        // Check the most recent state for LFP calibration flag
        var latestState = await _db.VehicleStates
            .Where(s => s.VehicleId == vehicleId)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        return latestState?.BatteryNeedsLfpCalibration == true;
    }

    private List<BatteryCareTip> GenerateTips(BatteryCareAnalysis analysis)
    {
        var tips = new List<BatteryCareTip>();

        // Cell-type specific tips
        switch (analysis.CellType)
        {
            case BatteryCellType.NMC:
                tips.AddRange(GetNmcTips(analysis));
                break;
            case BatteryCellType.LFP:
                tips.AddRange(GetLfpTips(analysis));
                break;
            default:
                tips.AddRange(GetGenericTips(analysis));
                break;
        }

        // Add universal tips
        tips.AddRange(GetUniversalTips());

        // Sort: personalized tips first, then by priority (Warning > High > Positive > Medium > Low)
        return tips
            .OrderByDescending(t => t.IsPersonalized)
            .ThenBy(t => t.Priority switch
            {
                TipPriority.Warning => 0,
                TipPriority.High => 1,
                TipPriority.Positive => 2,
                TipPriority.Medium => 3,
                TipPriority.Low => 4,
                _ => 5
            })
            .ToList();
    }

    private List<BatteryCareTip> GetNmcTips(BatteryCareAnalysis analysis)
    {
        var tips = new List<BatteryCareTip>();

        // Primary NMC guidance - emphasize charging only what you need
        tips.Add(new BatteryCareTip
        {
            Category = TipCategory.ChargingHabits,
            Priority = TipPriority.High,
            Icon = "battery",
            Title = "NMC Battery: Charge Only What You Need",
            Description = "Your vehicle has NMC (Nickel Manganese Cobalt) cells. For optimal longevity, charge only as much as you need for your daily driving. Lower average state of charge means longer battery life. 80-90% is fine when needed, but 50-70% is even better for daily use if it covers your needs.",
            IsPersonalized = false
        });

        // Context about typical driving
        tips.Add(new BatteryCareTip
        {
            Category = TipCategory.ChargingHabits,
            Priority = TipPriority.Medium,
            Icon = "map-pin",
            Title = "Most Commutes Need Less Than You Think",
            Description = "The average American commute is under 30 miles round-trip—less than 10% of your Rivian's range. If your daily driving is predictable, consider setting a lower charge limit (50-60%) and only increasing it when you have longer trips planned. Your battery will thank you.",
            IsPersonalized = false
        });

        // Personalized tip: Low mileage but high charge limit
        if (analysis.AverageDailyMiles.HasValue &&
            analysis.AverageDailyMiles.Value <= 30 &&
            analysis.AverageChargeLimit >= 70 &&
            analysis.DaysWithDrives >= 5) // Need enough data to be meaningful
        {
            tips.Add(new BatteryCareTip
            {
                Category = TipCategory.ChargingHabits,
                Priority = TipPriority.High,
                Icon = "trending-down",
                Title = "Your Driving Supports a Lower Limit",
                Description = $"Based on your driving this month, you averaged just {analysis.AverageDailyMiles.Value:0} miles per day across {analysis.DaysWithDrives} days of driving. With an average charge limit of {analysis.AverageChargeLimit:0}%, you're carrying more charge than you typically need. Consider lowering your daily charge limit to 50-60%—your battery will last longer, and you'll still have plenty of range for your routine.",
                IsPersonalized = true
            });
        }

        // Personalized feedback based on charge patterns
        if (analysis.PercentTimeAt100 > 20)
        {
            tips.Add(new BatteryCareTip
            {
                Category = TipCategory.ChargingHabits,
                Priority = TipPriority.Warning,
                Icon = "alert-triangle",
                Title = "Frequent 100% Charging Detected",
                Description = $"You've charged to 100% about {analysis.PercentTimeAt100:0}% of the time over the last 30 days. For NMC batteries, prolonged time at high state of charge accelerates degradation—especially in warm climates. Unless you need the full range daily, consider lowering your limit to match your actual driving needs.",
                IsPersonalized = true
            });
        }
        else if (analysis.AverageChargeLimit > 85 && analysis.TotalChargeRecords > 10)
        {
            tips.Add(new BatteryCareTip
            {
                Category = TipCategory.ChargingHabits,
                Priority = TipPriority.Medium,
                Icon = "info",
                Title = "Could You Charge Less?",
                Description = $"Your average charge limit is {analysis.AverageChargeLimit:0}%. While this is acceptable, research shows batteries last longer at lower average SOC. If your daily driving doesn't require this much range, try lowering your limit to 60-70% and see if it still meets your needs.",
                IsPersonalized = true
            });
        }
        else if (analysis.AverageChargeLimit <= 70 && analysis.TotalChargeRecords > 10)
        {
            tips.Add(new BatteryCareTip
            {
                Category = TipCategory.ChargingHabits,
                Priority = TipPriority.Positive,
                Icon = "check-circle",
                Title = "Excellent Charging Habits!",
                Description = $"Your average charge limit of {analysis.AverageChargeLimit:0}% is excellent for NMC battery longevity. By charging only what you need, you're minimizing time at high SOC and maximizing your battery's lifespan.",
                IsPersonalized = true
            });
        }
        else if (analysis.AverageChargeLimit <= 85 && analysis.TotalChargeRecords > 10)
        {
            tips.Add(new BatteryCareTip
            {
                Category = TipCategory.ChargingHabits,
                Priority = TipPriority.Positive,
                Icon = "check-circle",
                Title = "Good Charging Habits",
                Description = $"Your average charge limit of {analysis.AverageChargeLimit:0}% is good for NMC battery health. If your daily driving allows, you could extend battery life even further by dropping to 60-70% for routine use.",
                IsPersonalized = true
            });
        }

        return tips;
    }

    private List<BatteryCareTip> GetLfpTips(BatteryCareAnalysis analysis)
    {
        var tips = new List<BatteryCareTip>();

        // Primary LFP guidance - more nuanced based on research
        tips.Add(new BatteryCareTip
        {
            Category = TipCategory.ChargingHabits,
            Priority = TipPriority.High,
            Icon = "battery",
            Title = "LFP Battery: Charge to 100% Weekly",
            Description = "Your vehicle has LFP (Lithium Iron Phosphate) cells. LFP batteries have a flat voltage curve, which makes it difficult for the battery management system (BMS) to accurately estimate state of charge. Charging to 100% weekly allows the BMS to recalibrate for accurate range estimates.",
            IsPersonalized = false
        });

        // Add nuance about LFP degradation
        tips.Add(new BatteryCareTip
        {
            Category = TipCategory.ChargingHabits,
            Priority = TipPriority.Medium,
            Icon = "info",
            Title = "LFP Longevity Note",
            Description = "While LFP batteries are more tolerant of high charge levels than NMC, recent research shows they still experience slightly faster degradation at 100% SOC—just less severe than NMC. For maximum longevity, some experts suggest 20-80% for daily use with weekly 100% charges for calibration. However, the practical difference is small, so prioritize calibration.",
            IsPersonalized = false
        });

        // LFP calibration recommendation
        if (analysis.NeedsLfpCalibration)
        {
            tips.Add(new BatteryCareTip
            {
                Category = TipCategory.Calibration,
                Priority = TipPriority.Warning,
                Icon = "refresh-cw",
                Title = "Calibration Charge Needed",
                Description = "Your vehicle is indicating that an LFP calibration charge is needed. Charge to 100% and let the vehicle sit plugged in for at least 1-2 hours after reaching full. This allows the BMS to balance cells and recalibrate the state of charge reading.",
                IsPersonalized = true
            });
        }
        else if (analysis.PercentTimeAt100 < 10 && analysis.TotalChargeRecords > 10)
        {
            tips.Add(new BatteryCareTip
            {
                Category = TipCategory.Calibration,
                Priority = TipPriority.Medium,
                Icon = "refresh-cw",
                Title = "Consider a Calibration Charge",
                Description = "You haven't charged to 100% recently. For LFP batteries, charging to 100% at least once per week helps the BMS maintain accurate range estimates. Without periodic full charges, the displayed range may drift from actual capacity.",
                IsPersonalized = true
            });
        }
        else if (analysis.PercentTimeAt100 >= 10 && analysis.TotalChargeRecords > 10)
        {
            tips.Add(new BatteryCareTip
            {
                Category = TipCategory.Calibration,
                Priority = TipPriority.Positive,
                Icon = "check-circle",
                Title = "Good Calibration Habits",
                Description = "You're charging to 100% regularly, which helps keep your LFP battery's BMS calibrated for accurate range estimates. This is the recommended practice for LFP batteries.",
                IsPersonalized = true
            });
        }

        return tips;
    }

    private List<BatteryCareTip> GetGenericTips(BatteryCareAnalysis? analysis)
    {
        var tips = new List<BatteryCareTip>
        {
            new()
            {
                Category = TipCategory.ChargingHabits,
                Priority = TipPriority.Medium,
                Icon = "battery",
                Title = "Know Your Battery Type",
                Description = "Rivian vehicles use different battery chemistries: Standard pack uses LFP (charge to 100% weekly for calibration), while Large and Max packs use NMC (charge only what you need—lower is better). Once we detect your battery type, we'll provide specific recommendations.",
                IsPersonalized = false
            },
            new()
            {
                Category = TipCategory.ChargingHabits,
                Priority = TipPriority.Low,
                Icon = "info",
                Title = "LFP vs NMC Quick Guide",
                Description = "LFP (Standard pack): Needs weekly 100% charge for BMS calibration due to flat voltage curve; more tolerant of high SOC. NMC (Large/Max pack): Charge only what you need for daily driving—50-70% is ideal if it covers your commute; save higher limits for longer trips.",
                IsPersonalized = false
            }
        };

        return tips;
    }

    private List<BatteryCareTip> GetUniversalTips()
    {
        return new List<BatteryCareTip>
        {
            new()
            {
                Category = TipCategory.Temperature,
                Priority = TipPriority.Medium,
                Icon = "thermometer",
                Title = "Temperature + High SOC = Faster Degradation",
                Description = "Research shows battery degradation accelerates significantly when high state of charge is combined with high temperatures. A 2023 study found calendar aging doubles when SOC is above 90% and temperature exceeds 45°C (113°F). In hot climates, consider slightly lower charge limits.",
                IsPersonalized = false
            },
            new()
            {
                Category = TipCategory.Temperature,
                Priority = TipPriority.Low,
                Icon = "zap",
                Title = "Precondition Before DC Fast Charging",
                Description = "For the fastest DC charging speeds, precondition your battery by navigating to a fast charger in the nav system. This warms or cools the battery to its optimal temperature range, which also reduces stress on the cells during fast charging.",
                IsPersonalized = false
            },
            new()
            {
                Category = TipCategory.Temperature,
                Priority = TipPriority.Low,
                Icon = "sun",
                Title = "Park in the Shade When Possible",
                Description = "High temperatures accelerate battery degradation, especially when combined with a high state of charge. When possible, park in the shade or a garage to keep your battery cooler, particularly in summer months or hot climates.",
                IsPersonalized = false
            },
            new()
            {
                Category = TipCategory.DrivingHabits,
                Priority = TipPriority.Low,
                Icon = "gauge",
                Title = "DC Fast Charging Impact is Minimal",
                Description = "Contrary to popular belief, recent analysis of ~13,000 EVs found no statistically significant difference in degradation between vehicles that fast-charged frequently and those that rarely did. Don't avoid fast charging when you need it.",
                IsPersonalized = false
            },
            new()
            {
                Category = TipCategory.Storage,
                Priority = TipPriority.Low,
                Icon = "calendar",
                Title = "Long-Term Storage: 50% SOC",
                Description = "If storing your vehicle for weeks or longer, keep the battery around 50% state of charge in a cool location. Research shows batteries age slowest around 50% SOC. Avoid leaving it at 100% or below 20% for extended periods.",
                IsPersonalized = false
            },
            new()
            {
                Category = TipCategory.ChargingHabits,
                Priority = TipPriority.Low,
                Icon = "battery-charging",
                Title = "Real-World Degradation is Low",
                Description = "Modern EV batteries are robust. Geotab's 2024 analysis of 10,000+ EVs shows an average degradation rate of just 1.8% per year, down from 2.3% in 2019. Most EVs retain 85-90% capacity after 100,000 miles under normal use.",
                IsPersonalized = false
            }
        };
    }
}

/// <summary>
/// Battery care analysis results.
/// </summary>
public class BatteryCareAnalysis
{
    public BatteryCellType CellType { get; set; } = BatteryCellType.Unknown;
    public string CellTypeDisplayName { get; set; } = "Unknown";
    public bool NeedsLfpCalibration { get; set; }

    // Charge limit statistics (last 30 days)
    public double AverageChargeLimit { get; set; }
    public double MaxChargeLimit { get; set; }
    public int ChargesAbove90Percent { get; set; }
    public int ChargesAt100Percent { get; set; }
    public int TotalChargeRecords { get; set; }
    public double PercentTimeAbove90 { get; set; }
    public double PercentTimeAt100 { get; set; }

    // Driving statistics (last 30 days)
    public double? AverageDailyMiles { get; set; }
    public double TotalMilesLast30Days { get; set; }
    public int TotalDrivesLast30Days { get; set; }
    public int DaysWithDrives { get; set; }

    public List<BatteryCareTip> Tips { get; set; } = new();
}

/// <summary>
/// A battery care tip or recommendation.
/// </summary>
public class BatteryCareTip
{
    public TipCategory Category { get; set; }
    public TipPriority Priority { get; set; }
    public required string Icon { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public bool IsPersonalized { get; set; }
}

public enum BatteryCellType
{
    Unknown,
    NMC,  // Nickel Manganese Cobalt
    LFP   // Lithium Iron Phosphate
}

public enum TipCategory
{
    ChargingHabits,
    Temperature,
    DrivingHabits,
    Calibration,
    Storage
}

public enum TipPriority
{
    Low,
    Medium,
    High,
    Warning,
    Positive
}
