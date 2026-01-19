using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RivianMate.Core;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Seeds development/test data for local debugging.
/// Creates a test user with multiple vehicles showcasing different scenarios.
/// </summary>
public class DevDataSeeder
{
    private readonly RivianMateDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DevDataSeeder> _logger;

    private const string TestEmail = "test@rivianmate.local";
    private const string TestPassword = "TestPassword123!";

    public DevDataSeeder(
        RivianMateDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<DevDataSeeder> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // Check if already seeded
        var existingUser = await _userManager.FindByEmailAsync(TestEmail);
        if (existingUser != null)
        {
            _logger.LogInformation("Dev data already seeded for {Email}", TestEmail);
            return;
        }

        _logger.LogInformation("Seeding development test data...");

        // Create test user
        var user = new ApplicationUser
        {
            UserName = TestEmail,
            Email = TestEmail,
            DisplayName = "Test User",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-18),
            LastLoginAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, TestPassword);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to create test user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        // Create fake Rivian account
        var rivianAccount = new RivianAccount
        {
            UserId = user.Id,
            RivianEmail = "test.rivian@example.com",
            RivianUserId = "test-rivian-user-id",
            DisplayName = "Test Rivian Account",
            CreatedAt = DateTime.UtcNow.AddMonths(-18),
            LastSyncAt = DateTime.UtcNow,
            IsActive = true
        };
        _db.RivianAccounts.Add(rivianAccount);
        await _db.SaveChangesAsync();

        // Create test vehicles
        var vehicles = new List<Vehicle>
        {
            CreateHighMileageR1T(user.Id, rivianAccount.Id),
            CreateDegradedR1S(user.Id, rivianAccount.Id),
            CreateNewLfpR1T(user.Id, rivianAccount.Id),
            CreateMaxPackR1S(user.Id, rivianAccount.Id),
            CreatePoorChargingHabitsR1T(user.Id, rivianAccount.Id)
        };

        _db.Vehicles.AddRange(vehicles);
        await _db.SaveChangesAsync();

        // Generate historical data for each vehicle
        foreach (var vehicle in vehicles)
        {
            await GenerateVehicleDataAsync(vehicle);
        }

        _logger.LogInformation(
            "Dev data seeded: User={Email}, Password={Password}, Vehicles={Count}",
            TestEmail, TestPassword, vehicles.Count);
    }

    private Vehicle CreateHighMileageR1T(Guid userId, int accountId)
    {
        // High mileage vehicle approaching warranty limits
        return new Vehicle
        {
            RivianVehicleId = "dev-r1t-high-mileage",
            OwnerId = userId,
            RivianAccountId = accountId,
            Vin = "7FCTGAAL0PN000001",
            Name = "High Mileage Hauler",
            Model = VehicleModel.R1T,
            Year = 2022,
            BatteryPack = BatteryPackType.Large,
            DriveType = Core.Enums.DriveType.DualMotor,
            Trim = VehicleTrim.Adventure,
            ExteriorColor = "Rivian Blue",
            InteriorColor = "Ocean Coast",
            BatteryCellType = "50g",
            OriginalCapacityKwh = 135.0,
            EpaRangeMiles = 314,
            SoftwareVersion = "2024.50.0",
            CreatedAt = DateTime.UtcNow.AddMonths(-24),
            LastSeenAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    private Vehicle CreateDegradedR1S(Guid userId, int accountId)
    {
        // Severely degraded battery - under 70%
        return new Vehicle
        {
            RivianVehicleId = "dev-r1s-degraded",
            OwnerId = userId,
            RivianAccountId = accountId,
            Vin = "7FCTGAAL1PN000002",
            Name = "Degraded SUV",
            Model = VehicleModel.R1S,
            Year = 2022,
            BatteryPack = BatteryPackType.Large,
            DriveType = Core.Enums.DriveType.QuadMotor,
            Trim = VehicleTrim.LaunchEdition,
            ExteriorColor = "Launch Green",
            InteriorColor = "Black Mountain",
            BatteryCellType = "50g",
            OriginalCapacityKwh = 135.0,
            EpaRangeMiles = 316,
            SoftwareVersion = "2024.50.0",
            CreatedAt = DateTime.UtcNow.AddMonths(-30),
            LastSeenAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    private Vehicle CreateNewLfpR1T(Guid userId, int accountId)
    {
        // New LFP vehicle with minimal degradation
        return new Vehicle
        {
            RivianVehicleId = "dev-r1t-lfp-new",
            OwnerId = userId,
            RivianAccountId = accountId,
            Vin = "7FCTGAAL2PN000003",
            Name = "Fresh LFP Truck",
            Model = VehicleModel.R1T,
            Year = 2024,
            BatteryPack = BatteryPackType.Standard,
            DriveType = Core.Enums.DriveType.DualMotor,
            Trim = VehicleTrim.Ascend,
            ExteriorColor = "Glacier White",
            InteriorColor = "Forest Edge",
            BatteryCellType = "LFP",
            OriginalCapacityKwh = 105.0,
            EpaRangeMiles = 260,
            SoftwareVersion = "2024.50.0",
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            LastSeenAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    private Vehicle CreateMaxPackR1S(Guid userId, int accountId)
    {
        // Max pack with slight degradation
        return new Vehicle
        {
            RivianVehicleId = "dev-r1s-max-pack",
            OwnerId = userId,
            RivianAccountId = accountId,
            Vin = "7FCTGAAL3PN000004",
            Name = "Max Range SUV",
            Model = VehicleModel.R1S,
            Year = 2023,
            BatteryPack = BatteryPackType.Max,
            DriveType = Core.Enums.DriveType.DualMotor,
            Trim = VehicleTrim.Adventure,
            ExteriorColor = "Limestone",
            InteriorColor = "Ocean Coast",
            BatteryCellType = "53g",
            OriginalCapacityKwh = 180.0,
            EpaRangeMiles = 400,
            SoftwareVersion = "2024.50.0",
            CreatedAt = DateTime.UtcNow.AddMonths(-15),
            LastSeenAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    private Vehicle CreatePoorChargingHabitsR1T(Guid userId, int accountId)
    {
        // Vehicle with poor charging habits (high DC fast charging, 100% limits)
        return new Vehicle
        {
            RivianVehicleId = "dev-r1t-poor-habits",
            OwnerId = userId,
            RivianAccountId = accountId,
            Vin = "7FCTGAAL4PN000005",
            Name = "Road Tripper",
            Model = VehicleModel.R1T,
            Year = 2023,
            BatteryPack = BatteryPackType.Large,
            DriveType = Core.Enums.DriveType.DualMotor,
            Trim = VehicleTrim.Adventure,
            ExteriorColor = "El Cap Granite",
            InteriorColor = "Black Mountain",
            BatteryCellType = "50g",
            OriginalCapacityKwh = 135.0,
            EpaRangeMiles = 328,
            SoftwareVersion = "2024.50.0",
            CreatedAt = DateTime.UtcNow.AddMonths(-18),
            LastSeenAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    private async Task GenerateVehicleDataAsync(Vehicle vehicle)
    {
        var random = new Random(vehicle.Id);
        var scenarios = GetVehicleScenario(vehicle.RivianVehicleId);

        // Generate battery health snapshots over time
        var snapshots = GenerateBatteryHealthSnapshots(vehicle, scenarios, random);
        _db.BatteryHealthSnapshots.AddRange(snapshots);

        // Generate charging sessions
        var sessions = GenerateChargingSessions(vehicle, scenarios, random);
        _db.ChargingSessions.AddRange(sessions);

        // Generate current state
        var currentState = GenerateCurrentState(vehicle, scenarios, snapshots.Last(), random);
        _db.VehicleStates.Add(currentState);

        // Generate additional vehicle states for the last 30 days (for charge limit analysis)
        var recentStates = GenerateRecentVehicleStates(vehicle, scenarios, random);
        _db.VehicleStates.AddRange(recentStates);

        // Save everything except drives/positions first
        await _db.SaveChangesAsync();

        // Generate drives with positions (for the last 30 days)
        // We need to save drives first to get IDs, then add positions
        var (drives, positionsByDriveIndex) = GenerateDrivesWithPositions(vehicle, scenarios, random);

        _db.Drives.AddRange(drives);
        await _db.SaveChangesAsync(); // Drives now have IDs

        // Now assign the correct DriveIds to positions and add them
        var allPositions = new List<Position>();
        for (int i = 0; i < drives.Count; i++)
        {
            if (positionsByDriveIndex.TryGetValue(i, out var drivePositions))
            {
                foreach (var pos in drivePositions)
                {
                    pos.DriveId = drives[i].Id;
                }
                allPositions.AddRange(drivePositions);
            }
        }

        _db.Positions.AddRange(allPositions);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Generated data for {VehicleName}: {Snapshots} snapshots, {Sessions} sessions, {Drives} drives with {Positions} positions",
            vehicle.Name, snapshots.Count, sessions.Count, drives.Count, allPositions.Count);
    }

    private VehicleScenario GetVehicleScenario(string vehicleId) => vehicleId switch
    {
        "dev-r1t-high-mileage" => new VehicleScenario
        {
            CurrentOdometer = 151000,
            MonthsOfData = 24,
            StartingHealthPercent = 100,
            CurrentHealthPercent = 85,
            DegradationPattern = DegradationPattern.Linear,
            AverageChargeLimit = 80,
            DcFastChargePercent = 15,
            ChargingSessionsPerMonth = 20,
            DrivesPerMonth = 30,
            AverageDriveMiles = 40,
            DriveVariation = 0.4,
            HomeLatitude = 37.7749,
            HomeLongitude = -122.4194
        },
        "dev-r1s-degraded" => new VehicleScenario
        {
            CurrentOdometer = 82000,
            MonthsOfData = 30,
            StartingHealthPercent = 100,
            CurrentHealthPercent = 68, // Below 70% - warranty territory
            DegradationPattern = DegradationPattern.Accelerating,
            AverageChargeLimit = 100, // Always charging to 100%
            DcFastChargePercent = 40, // Heavy DC fast charging
            ChargingSessionsPerMonth = 25,
            DrivesPerMonth = 20,
            AverageDriveMiles = 35,
            DriveVariation = 0.5,
            HomeLatitude = 37.8044,
            HomeLongitude = -122.2712 // Oakland
        },
        "dev-r1t-lfp-new" => new VehicleScenario
        {
            CurrentOdometer = 15000,
            MonthsOfData = 6,
            StartingHealthPercent = 100,
            CurrentHealthPercent = 99, // LFP degrades very slowly
            DegradationPattern = DegradationPattern.Minimal,
            AverageChargeLimit = 100, // LFP is fine at 100%
            DcFastChargePercent = 20,
            ChargingSessionsPerMonth = 15,
            DrivesPerMonth = 25,
            AverageDriveMiles = 20,
            DriveVariation = 0.3,
            HomeLatitude = 37.5485,
            HomeLongitude = -122.0590 // Fremont (near Rivian service)
        },
        "dev-r1s-max-pack" => new VehicleScenario
        {
            // SHORT COMMUTER with HIGH CHARGE LIMIT - triggers "...And That's True For You, Too!" tip
            CurrentOdometer = 45000,
            MonthsOfData = 15,
            StartingHealthPercent = 100,
            CurrentHealthPercent = 92,
            DegradationPattern = DegradationPattern.Linear,
            AverageChargeLimit = 85, // High charge limit
            DcFastChargePercent = 10, // Rarely DCFC - mostly home charging
            ChargingSessionsPerMonth = 18,
            DrivesPerMonth = 22, // About 5 days/week of driving
            AverageDriveMiles = 18, // Short commute! Under 30 miles triggers tip
            DriveVariation = 0.3, // Consistent commuter pattern
            HomeLatitude = 37.4419,
            HomeLongitude = -122.1430 // Palo Alto
        },
        "dev-r1t-poor-habits" => new VehicleScenario
        {
            CurrentOdometer = 55000,
            MonthsOfData = 18,
            StartingHealthPercent = 100,
            CurrentHealthPercent = 82,
            DegradationPattern = DegradationPattern.Accelerating,
            AverageChargeLimit = 100, // Always 100%
            DcFastChargePercent = 60, // Lots of DCFC
            ChargingSessionsPerMonth = 30,
            DrivesPerMonth = 28,
            AverageDriveMiles = 55, // Road tripper - longer drives
            DriveVariation = 0.7, // Highly variable
            HomeLatitude = 37.3382,
            HomeLongitude = -121.8863 // San Jose
        },
        _ => new VehicleScenario()
    };

    private List<BatteryHealthSnapshot> GenerateBatteryHealthSnapshots(
        Vehicle vehicle, VehicleScenario scenario, Random random)
    {
        var snapshots = new List<BatteryHealthSnapshot>();
        var originalCapacity = vehicle.OriginalCapacityKwh ?? 135.0;
        var startDate = DateTime.UtcNow.AddMonths(-scenario.MonthsOfData);
        var milesPerMonth = scenario.CurrentOdometer / (double)scenario.MonthsOfData;

        // Generate roughly weekly snapshots
        var totalWeeks = scenario.MonthsOfData * 4;
        var healthDrop = scenario.StartingHealthPercent - scenario.CurrentHealthPercent;

        for (int week = 0; week <= totalWeeks; week++)
        {
            var progress = (double)week / totalWeeks;
            var timestamp = startDate.AddDays(week * 7 + random.Next(-2, 3));

            if (timestamp > DateTime.UtcNow) break;

            // Calculate health based on degradation pattern
            double healthPercent = scenario.DegradationPattern switch
            {
                DegradationPattern.Linear =>
                    scenario.StartingHealthPercent - (healthDrop * progress),
                DegradationPattern.Accelerating =>
                    scenario.StartingHealthPercent - (healthDrop * Math.Pow(progress, 1.5)),
                DegradationPattern.Minimal =>
                    scenario.StartingHealthPercent - (healthDrop * Math.Pow(progress, 0.5)),
                _ => scenario.StartingHealthPercent - (healthDrop * progress)
            };

            // Add some noise
            healthPercent += (random.NextDouble() - 0.5) * 1.5;
            healthPercent = Math.Clamp(healthPercent, scenario.CurrentHealthPercent - 2, 100);

            var capacityKwh = originalCapacity * (healthPercent / 100.0);
            var odometer = milesPerMonth * (week / 4.0);

            // Vary the battery level at time of measurement
            var batteryLevel = 50 + random.Next(-30, 40);
            batteryLevel = Math.Clamp(batteryLevel, 20, 95);

            snapshots.Add(new BatteryHealthSnapshot
            {
                VehicleId = vehicle.Id,
                Timestamp = timestamp,
                Odometer = Math.Round(odometer, 0),
                ReportedCapacityKwh = Math.Round(capacityKwh, 2),
                StateOfCharge = batteryLevel,
                OriginalCapacityKwh = originalCapacity,
                HealthPercent = Math.Round(healthPercent, 2),
                CapacityLostKwh = Math.Round(originalCapacity - capacityKwh, 2),
                DegradationPercent = Math.Round(100 - healthPercent, 2)
            });
        }

        // Calculate trend projections for the snapshots (especially important for the latest one)
        CalculateTrendProjections(snapshots, originalCapacity);

        return snapshots;
    }

    /// <summary>
    /// Calculate trend-based projections for battery health snapshots.
    /// Uses linear regression to determine degradation rate and projections.
    /// </summary>
    private void CalculateTrendProjections(List<BatteryHealthSnapshot> snapshots, double originalCapacity)
    {
        if (snapshots.Count < 3)
            return;

        // Build data points for linear regression
        var dataPoints = snapshots
            .Where(s => s.Odometer > 0)
            .Select(s => (Odometer: s.Odometer, Health: s.HealthPercent))
            .ToList();

        if (dataPoints.Count < 2)
            return;

        // Simple linear regression: Health = intercept + slope * Odometer
        var n = dataPoints.Count;
        var sumX = dataPoints.Sum(p => p.Odometer);
        var sumY = dataPoints.Sum(p => p.Health);
        var sumXY = dataPoints.Sum(p => p.Odometer * p.Health);
        var sumX2 = dataPoints.Sum(p => p.Odometer * p.Odometer);

        var denominator = n * sumX2 - sumX * sumX;
        if (Math.Abs(denominator) < 0.0001)
            return;

        var slope = (n * sumXY - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / n;

        // slope is degradation per mile (negative), convert to positive % per 10k miles
        var degradationPer10kMiles = -slope * 10000;

        // Only apply if degradation rate is reasonable (between 0 and 10% per 10k miles)
        if (degradationPer10kMiles <= 0 || degradationPer10kMiles > 10)
            return;

        // Apply projections to snapshots that have enough historical data (last half of snapshots)
        var startIndex = snapshots.Count / 2;
        for (int i = startIndex; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];

            snapshot.DegradationRatePer10kMiles = Math.Round(degradationPer10kMiles, 2);

            // Project health at 100k miles
            var healthAt100k = intercept + (slope * 100000);
            snapshot.ProjectedHealthAt100kMiles = Math.Round(Math.Max(0, Math.Min(100, healthAt100k)), 1);

            // Project miles until 70% (warranty threshold)
            // Only show projection if currently above 70% and degrading
            if (slope < 0 && snapshot.HealthPercent > 70)
            {
                var milesTo70 = (70 - intercept) / slope;
                if (milesTo70 > snapshot.Odometer)
                {
                    snapshot.ProjectedMilesTo70Percent = Math.Round(milesTo70, 0);
                }
            }

            // Calculate remaining warranty capacity
            var warrantyThresholdCapacity = originalCapacity * 0.70;
            snapshot.RemainingWarrantyCapacityKwh = Math.Round(snapshot.ReportedCapacityKwh - warrantyThresholdCapacity, 2);
        }
    }

    private List<ChargingSession> GenerateChargingSessions(
        Vehicle vehicle, VehicleScenario scenario, Random random)
    {
        var sessions = new List<ChargingSession>();
        var startDate = DateTime.UtcNow.AddMonths(-scenario.MonthsOfData);
        var totalSessions = scenario.ChargingSessionsPerMonth * scenario.MonthsOfData;

        var homeLocations = new[] { "Home", "Home Garage", null };
        var dcfcLocations = new[] { "Rivian Adventure Network", "Electrify America", "ChargePoint DC", "Tesla Supercharger" };
        var level2Locations = new[] { "Work", "Shopping Center", "Hotel", "Destination Charger" };

        for (int i = 0; i < totalSessions; i++)
        {
            var isDcfc = random.NextDouble() * 100 < scenario.DcFastChargePercent;
            var isHome = !isDcfc && random.NextDouble() > 0.3;

            var sessionDate = startDate.AddHours(random.Next(0, scenario.MonthsOfData * 30 * 24));
            if (sessionDate > DateTime.UtcNow) continue;

            var startSoc = random.Next(10, 60);
            var targetSoc = isDcfc
                ? random.Next(70, 90)
                : Math.Min(100, scenario.AverageChargeLimit + random.Next(-10, 10));

            var energyAdded = (vehicle.OriginalCapacityKwh ?? 135) * ((targetSoc - startSoc) / 100.0);
            energyAdded = Math.Max(5, energyAdded + (random.NextDouble() - 0.5) * 10);

            var chargePower = isDcfc ? random.Next(100, 200) : random.Next(7, 12);
            var durationMinutes = (int)(energyAdded / chargePower * 60);
            durationMinutes = Math.Max(15, durationMinutes + random.Next(-10, 20));

            string? locationName;
            double latitude;
            double longitude;

            if (isHome)
            {
                locationName = homeLocations[random.Next(homeLocations.Length)];
                // Home location with small variation
                latitude = scenario.HomeLatitude + (random.NextDouble() - 0.5) * 0.0002;
                longitude = scenario.HomeLongitude + (random.NextDouble() - 0.5) * 0.0002;
            }
            else if (isDcfc)
            {
                locationName = dcfcLocations[random.Next(dcfcLocations.Length)];
                // DCFC locations - scattered around the area (within ~10-30 miles)
                latitude = scenario.HomeLatitude + (random.NextDouble() - 0.5) * 0.3;
                longitude = scenario.HomeLongitude + (random.NextDouble() - 0.5) * 0.3;
            }
            else
            {
                locationName = level2Locations[random.Next(level2Locations.Length)];
                // Level 2 destination chargers - within ~5-15 miles
                latitude = scenario.HomeLatitude + (random.NextDouble() - 0.5) * 0.15;
                longitude = scenario.HomeLongitude + (random.NextDouble() - 0.5) * 0.15;
            }

            // DCFC sessions get cost from network (API provides it)
            // Home/destination charging: no API cost, will be estimated using user's rate
            double? cost = null;
            if (isDcfc)
            {
                // DCFC pricing varies by network ($0.31-0.48/kWh typical)
                var dcfcRate = 0.31 + random.NextDouble() * 0.17;
                cost = Math.Round(energyAdded * dcfcRate, 2);
            }

            sessions.Add(new ChargingSession
            {
                VehicleId = vehicle.Id,
                StartTime = sessionDate,
                EndTime = sessionDate.AddMinutes(durationMinutes),
                StartBatteryLevel = startSoc,
                EndBatteryLevel = targetSoc,
                ChargeLimit = scenario.AverageChargeLimit + random.Next(-5, 10),
                EnergyAddedKwh = Math.Round(energyAdded, 2),
                PeakPowerKw = chargePower + random.Next(-5, 10),
                AveragePowerKw = Math.Round(energyAdded / (durationMinutes / 60.0), 1),
                ChargeType = isDcfc ? ChargeType.DC_Fast : ChargeType.AC_Level2,
                LocationName = locationName,
                Latitude = latitude,
                Longitude = longitude,
                IsHomeCharging = isHome,
                Cost = cost,
                IsActive = false
            });
        }

        return sessions.OrderBy(s => s.StartTime).ToList();
    }

    private (List<Drive>, Dictionary<int, List<Position>>) GenerateDrivesWithPositions(
        Vehicle vehicle, VehicleScenario scenario, Random random)
    {
        var drives = new List<Drive>();
        var positionsByDriveIndex = new Dictionary<int, List<Position>>();

        // Generate drives for the last 30 days (this is what the battery care analysis looks at)
        var startDate = DateTime.UtcNow.AddDays(-30);
        var drivesInPeriod = scenario.DrivesPerMonth; // About 1 month of drives

        // Common destinations relative to home (offset in degrees, roughly)
        var destinations = new[]
        {
            (Name: "Work", LatOffset: 0.02, LonOffset: 0.03),
            (Name: "Grocery Store", LatOffset: -0.01, LonOffset: 0.015),
            (Name: "Gym", LatOffset: 0.005, LonOffset: -0.02),
            (Name: "School", LatOffset: -0.015, LonOffset: 0.01),
            (Name: "Downtown", LatOffset: 0.03, LonOffset: -0.04),
            (Name: "Mall", LatOffset: -0.025, LonOffset: 0.035),
            (Name: "Restaurant", LatOffset: 0.01, LonOffset: -0.01),
            (Name: "Park", LatOffset: -0.008, LonOffset: -0.012),
        };

        var odometerAtStart = scenario.CurrentOdometer - (scenario.AverageDriveMiles * drivesInPeriod);

        for (int i = 0; i < drivesInPeriod; i++)
        {
            // Spread drives across the 30 days
            var driveDate = startDate.AddDays(random.Next(0, 30));
            // Add time of day variation (morning commute, evening, etc.)
            var hourOfDay = random.NextDouble() < 0.6
                ? random.Next(7, 10)  // Morning commute
                : random.Next(16, 20); // Evening
            driveDate = driveDate.Date.AddHours(hourOfDay).AddMinutes(random.Next(0, 60));

            if (driveDate > DateTime.UtcNow) continue;

            // Determine drive distance with variation
            var baseMiles = scenario.AverageDriveMiles;
            var variation = baseMiles * scenario.DriveVariation;
            var driveMiles = baseMiles + (random.NextDouble() - 0.5) * 2 * variation;
            driveMiles = Math.Max(2, driveMiles); // Minimum 2 miles

            // Pick a destination
            var dest = destinations[random.Next(destinations.Length)];

            // Calculate energy and efficiency
            var efficiency = 2.5 + (random.NextDouble() - 0.5) * 1.0; // 2.0 - 3.0 mi/kWh
            var energyUsed = driveMiles / efficiency;

            // Battery levels
            var startBattery = 50 + random.Next(-20, 40);
            var capacity = vehicle.OriginalCapacityKwh ?? 135;
            var batteryDrop = (energyUsed / capacity) * 100;
            var endBattery = Math.Max(10, startBattery - batteryDrop);

            // Duration based on average speed (25-45 mph for mixed driving)
            var avgSpeed = 25 + random.Next(0, 20);
            var durationMinutes = (int)((driveMiles / avgSpeed) * 60);
            durationMinutes = Math.Max(5, durationMinutes + random.Next(-5, 10));

            var endTime = driveDate.AddMinutes(durationMinutes);

            // Create the drive
            var drive = new Drive
            {
                VehicleId = vehicle.Id,
                StartTime = driveDate,
                EndTime = endTime,
                IsActive = false,
                StartOdometer = odometerAtStart + (i * scenario.AverageDriveMiles),
                EndOdometer = odometerAtStart + (i * scenario.AverageDriveMiles) + driveMiles,
                DistanceMiles = Math.Round(driveMiles, 1),
                StartBatteryLevel = startBattery,
                EndBatteryLevel = Math.Round(endBattery, 0),
                EnergyUsedKwh = Math.Round(energyUsed, 2),
                StartRangeEstimate = startBattery * 3,
                EndRangeEstimate = endBattery * 3,
                EfficiencyMilesPerKwh = Math.Round(efficiency, 2),
                EfficiencyWhPerMile = Math.Round(1000 / efficiency, 0),
                StartLatitude = scenario.HomeLatitude,
                StartLongitude = scenario.HomeLongitude,
                EndLatitude = scenario.HomeLatitude + dest.LatOffset,
                EndLongitude = scenario.HomeLongitude + dest.LonOffset,
                AverageSpeedMph = avgSpeed,
                MaxSpeedMph = avgSpeed + random.Next(15, 35),
                StartElevation = 10 + random.Next(-5, 20),
                EndElevation = 10 + random.Next(-5, 20),
                ElevationGain = random.Next(5, 50),
                DriveMode = random.NextDouble() < 0.8 ? "everyday" : "sport"
            };

            // Store the index before adding (for position mapping)
            var driveIndex = drives.Count;
            drives.Add(drive);

            // Generate positions along the route (store by index for later ID assignment)
            var positions = GeneratePositionsForDrive(drive, scenario, dest, random);
            positionsByDriveIndex[driveIndex] = positions;
        }

        // Sort drives by time (but keep track of original indices for position mapping)
        // Since we're using indices, we need to rebuild the mapping after sorting
        var sortedDrives = drives.OrderBy(d => d.StartTime).ToList();

        // Rebuild position mapping based on sorted order
        var newPositionsByIndex = new Dictionary<int, List<Position>>();
        for (int newIndex = 0; newIndex < sortedDrives.Count; newIndex++)
        {
            var drive = sortedDrives[newIndex];
            var oldIndex = drives.IndexOf(drive);
            if (positionsByDriveIndex.TryGetValue(oldIndex, out var positions))
            {
                newPositionsByIndex[newIndex] = positions;
            }
        }

        return (sortedDrives, newPositionsByIndex);
    }

    private List<Position> GeneratePositionsForDrive(
        Drive drive, VehicleScenario scenario, (string Name, double LatOffset, double LonOffset) destination, Random random)
    {
        var positions = new List<Position>();

        if (drive.DistanceMiles == null || drive.EndTime == null)
            return positions;

        // Calculate number of positions based on drive duration (roughly every 30-60 seconds)
        var durationMinutes = (drive.EndTime.Value - drive.StartTime).TotalMinutes;
        var numPositions = Math.Max(5, (int)(durationMinutes * 1.5)); // ~1.5 positions per minute
        numPositions = Math.Min(numPositions, 100); // Cap at 100 positions

        var startLat = scenario.HomeLatitude;
        var startLon = scenario.HomeLongitude;
        var endLat = scenario.HomeLatitude + destination.LatOffset;
        var endLon = scenario.HomeLongitude + destination.LonOffset;

        // Generate positions along a slightly curved route
        for (int i = 0; i < numPositions; i++)
        {
            var progress = (double)i / (numPositions - 1);
            var timestamp = drive.StartTime.AddSeconds(progress * durationMinutes * 60);

            // Add some curve to the route (not a straight line)
            var curveOffset = Math.Sin(progress * Math.PI) * 0.002 * (random.NextDouble() - 0.5);

            var lat = startLat + (endLat - startLat) * progress + curveOffset;
            var lon = startLon + (endLon - startLon) * progress + curveOffset * 1.2;

            // Add small random jitter for realism
            lat += (random.NextDouble() - 0.5) * 0.0002;
            lon += (random.NextDouble() - 0.5) * 0.0002;

            // Speed varies during drive (slower at start/end, faster in middle)
            var speedFactor = Math.Sin(progress * Math.PI); // 0 at ends, 1 in middle
            var speed = (drive.AverageSpeedMph ?? 30) * (0.5 + speedFactor * 0.8);
            speed += (random.NextDouble() - 0.5) * 10;
            speed = Math.Max(0, speed);

            // Battery decreases linearly
            var batteryLevel = drive.StartBatteryLevel -
                (drive.StartBatteryLevel - (drive.EndBatteryLevel ?? drive.StartBatteryLevel)) * progress;

            // Heading based on direction of travel
            var heading = Math.Atan2(endLon - startLon, endLat - startLat) * 180 / Math.PI;
            heading = (heading + 360 + random.Next(-10, 10)) % 360;

            positions.Add(new Position
            {
                DriveId = drive.Id, // Will be set after drive is saved
                Timestamp = timestamp,
                Latitude = lat,
                Longitude = lon,
                Altitude = (drive.StartElevation ?? 10) + random.Next(-5, 10),
                Speed = Math.Round(speed, 1),
                Heading = Math.Round(heading, 0),
                BatteryLevel = Math.Round(batteryLevel, 0),
                Odometer = drive.StartOdometer + (drive.DistanceMiles.Value * progress)
            });
        }

        return positions;
    }

    private List<VehicleState> GenerateRecentVehicleStates(
        Vehicle vehicle, VehicleScenario scenario, Random random)
    {
        var states = new List<VehicleState>();

        // Generate states for the last 30 days to establish charge limit patterns
        var startDate = DateTime.UtcNow.AddDays(-30);

        // Generate 2-3 states per day
        for (int day = 0; day < 30; day++)
        {
            var statesPerDay = random.Next(2, 4);
            for (int s = 0; s < statesPerDay; s++)
            {
                var timestamp = startDate.AddDays(day).AddHours(random.Next(0, 24));
                if (timestamp > DateTime.UtcNow.AddMinutes(-10)) continue;

                var batteryLevel = 30 + random.Next(0, 60);

                states.Add(new VehicleState
                {
                    VehicleId = vehicle.Id,
                    Timestamp = timestamp,
                    BatteryLevel = batteryLevel,
                    BatteryLimit = scenario.AverageChargeLimit + random.Next(-5, 5),
                    BatteryCapacityKwh = (vehicle.OriginalCapacityKwh ?? 135) * (scenario.CurrentHealthPercent / 100.0),
                    RangeEstimate = batteryLevel * 3,
                    Odometer = scenario.CurrentOdometer - (30 - day) * 30,
                    PowerState = PowerState.Standby,
                    GearStatus = GearStatus.Park,
                    ChargerState = random.NextDouble() < 0.3 ? ChargerState.Charging : ChargerState.Disconnected,
                    Latitude = scenario.HomeLatitude + (random.NextDouble() - 0.5) * 0.01,
                    Longitude = scenario.HomeLongitude + (random.NextDouble() - 0.5) * 0.01,
                    BatteryCellType = vehicle.BatteryCellType
                });
            }
        }

        return states.OrderBy(s => s.Timestamp).ToList();
    }

    private VehicleState GenerateCurrentState(
        Vehicle vehicle, VehicleScenario scenario, BatteryHealthSnapshot latestSnapshot, Random random)
    {
        var batteryLevel = 50 + random.Next(-20, 40);
        var rangeEstimate = (vehicle.EpaRangeMiles ?? 300) * (batteryLevel / 100.0) *
            (scenario.CurrentHealthPercent / 100.0) * (0.85 + random.NextDouble() * 0.2);

        return new VehicleState
        {
            VehicleId = vehicle.Id,
            Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(5, 60)),
            BatteryLevel = batteryLevel,
            BatteryLimit = scenario.AverageChargeLimit,
            BatteryCapacityKwh = latestSnapshot.ReportedCapacityKwh,
            RangeEstimate = Math.Round(rangeEstimate, 0),
            Odometer = scenario.CurrentOdometer,
            PowerState = PowerState.Standby,
            GearStatus = GearStatus.Park,
            ChargerState = ChargerState.Disconnected,
            CabinTemperature = 18 + random.Next(-5, 15),
            AllDoorsClosed = true,
            AllDoorsLocked = true,
            AllWindowsClosed = true,
            FrunkClosed = true,
            FrunkLocked = true,
            LiftgateClosed = true,
            GearGuardStatus = "Enabled",
            OtaCurrentVersion = vehicle.SoftwareVersion,
            Latitude = 37.7749 + (random.NextDouble() - 0.5) * 0.1,
            Longitude = -122.4194 + (random.NextDouble() - 0.5) * 0.1,
            BatteryCellType = vehicle.BatteryCellType
        };
    }

    private enum DegradationPattern
    {
        Linear,
        Accelerating,
        Minimal
    }

    private class VehicleScenario
    {
        public double CurrentOdometer { get; set; } = 30000;
        public int MonthsOfData { get; set; } = 12;
        public double StartingHealthPercent { get; set; } = 100;
        public double CurrentHealthPercent { get; set; } = 95;
        public DegradationPattern DegradationPattern { get; set; } = DegradationPattern.Linear;
        public int AverageChargeLimit { get; set; } = 80;
        public int DcFastChargePercent { get; set; } = 20;
        public int ChargingSessionsPerMonth { get; set; } = 15;

        // Driving patterns
        public int DrivesPerMonth { get; set; } = 25;
        public double AverageDriveMiles { get; set; } = 25;
        public double DriveVariation { get; set; } = 0.5; // 0-1, how much variation in drive distance

        // Route generation settings
        public double HomeLatitude { get; set; } = 37.7749;
        public double HomeLongitude { get; set; } = -122.4194;
    }
}
