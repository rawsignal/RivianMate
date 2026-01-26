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
        for (int i = 0; i < vehicles.Count; i++)
        {
            var vehicle = vehicles[i];
            // First vehicle gets real production data, others get synthetic
            var useRealDriveData = i == 0;
            await GenerateVehicleDataAsync(vehicle, useRealDriveData);
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
            WheelConfig = "22\" Bright Sport",
            BatteryCellType = "50g",
            OriginalCapacityKwh = 135.0,
            EpaRangeMiles = 314,
            SoftwareVersion = "2024.50.0",
            BuildDate = new DateTime(2023, 1, 26),
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
            WheelConfig = "21\" Road",
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
            WheelConfig = "20\" All-Terrain",
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
            WheelConfig = "22\" Bright Sport",
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
            WheelConfig = "20\" All-Terrain Dark",
            BatteryCellType = "50g",
            OriginalCapacityKwh = 135.0,
            EpaRangeMiles = 328,
            SoftwareVersion = "2024.50.0",
            CreatedAt = DateTime.UtcNow.AddMonths(-18),
            LastSeenAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    private async Task GenerateVehicleDataAsync(Vehicle vehicle, bool useRealDriveData = false)
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

        // Generate activity feed items (for the last 30 days)
        var activities = GenerateActivityFeedItems(vehicle, scenarios, random);
        _db.ActivityFeed.AddRange(activities);

        // Save everything except drives/positions first
        await _db.SaveChangesAsync();

        int driveCount;
        if (useRealDriveData)
        {
            // Use real production drive data with actual GPS traces
            await SeedRealDriveDataAsync(vehicle);
            driveCount = 4; // Known count from real data
        }
        else
        {
            // Generate synthetic drives with positions (for the last 30 days)
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
            driveCount = drives.Count;
        }

        _logger.LogInformation(
            "Generated data for {VehicleName}: {Snapshots} snapshots, {Sessions} sessions, {Drives} drives, {Activities} activities{RealData}",
            vehicle.Name, snapshots.Count, sessions.Count, driveCount, activities.Count,
            useRealDriveData ? " (real drive data)" : "");
    }

    private List<ActivityFeedItem> GenerateActivityFeedItems(
        Vehicle vehicle, VehicleScenario scenario, Random random)
    {
        var activities = new List<ActivityFeedItem>();
        var vehicleName = vehicle.Name ?? vehicle.Model.ToString();
        var startDate = DateTime.UtcNow.AddDays(-30);

        // Generate varied activities over the last 30 days
        for (int day = 0; day < 30; day++)
        {
            var dayDate = startDate.AddDays(day);

            // Morning: Wake up, unlock, drive away
            if (random.NextDouble() < 0.7) // 70% chance of activity each day
            {
                var morningTime = dayDate.Date.AddHours(7 + random.Next(0, 3)).AddMinutes(random.Next(0, 60));

                // Wake up
                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = morningTime,
                    Type = ActivityType.Power,
                    Message = $"{vehicleName} woke up"
                });

                // Unlock
                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = morningTime.AddSeconds(random.Next(5, 30)),
                    Type = ActivityType.Security,
                    Message = $"{vehicleName} was unlocked"
                });

                // Doors opened then closed
                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = morningTime.AddSeconds(random.Next(30, 60)),
                    Type = ActivityType.Closure,
                    Message = $"{vehicleName}'s doors were opened"
                });

                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = morningTime.AddSeconds(random.Next(60, 120)),
                    Type = ActivityType.Closure,
                    Message = $"{vehicleName}'s doors were closed"
                });

                // Shift to drive
                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = morningTime.AddMinutes(random.Next(1, 3)),
                    Type = ActivityType.Gear,
                    Message = $"{vehicleName} shifted to Drive"
                });
            }

            // Evening: Return home, park, lock, sleep
            if (random.NextDouble() < 0.7)
            {
                var eveningTime = dayDate.Date.AddHours(17 + random.Next(0, 4)).AddMinutes(random.Next(0, 60));

                // Shift to park
                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = eveningTime,
                    Type = ActivityType.Gear,
                    Message = $"{vehicleName} shifted to Park"
                });

                // Doors opened and closed
                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = eveningTime.AddSeconds(random.Next(10, 30)),
                    Type = ActivityType.Closure,
                    Message = $"{vehicleName}'s doors were opened"
                });

                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = eveningTime.AddSeconds(random.Next(30, 60)),
                    Type = ActivityType.Closure,
                    Message = $"{vehicleName}'s doors were closed"
                });

                // Lock
                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = eveningTime.AddMinutes(random.Next(1, 5)),
                    Type = ActivityType.Security,
                    Message = $"{vehicleName} was locked"
                });

                // Sleep (after some idle time)
                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = eveningTime.AddMinutes(random.Next(10, 30)),
                    Type = ActivityType.Power,
                    Message = $"{vehicleName} went to sleep"
                });
            }

            // Occasional charging events
            if (random.NextDouble() < 0.3) // 30% chance of charging each day
            {
                var chargeTime = dayDate.Date.AddHours(20 + random.Next(0, 4)).AddMinutes(random.Next(0, 60));

                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = chargeTime,
                    Type = ActivityType.Charging,
                    Message = $"{vehicleName}'s charger was connected"
                });

                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = chargeTime.AddMinutes(random.Next(1, 5)),
                    Type = ActivityType.Charging,
                    Message = $"{vehicleName} started charging"
                });

                // Charging complete (next morning usually)
                var completeTime = chargeTime.AddHours(random.Next(3, 8));
                if (completeTime < DateTime.UtcNow)
                {
                    activities.Add(new ActivityFeedItem
                    {
                        VehicleId = vehicle.Id,
                        Timestamp = completeTime,
                        Type = ActivityType.Charging,
                        Message = $"{vehicleName} finished charging"
                    });
                }
            }

            // Occasional preconditioning
            if (random.NextDouble() < 0.15) // 15% chance
            {
                var preconditionTime = dayDate.Date.AddHours(6 + random.Next(0, 2)).AddMinutes(random.Next(0, 60));

                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = preconditionTime,
                    Type = ActivityType.Climate,
                    Message = $"{vehicleName}'s climate preconditioning started"
                });

                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = preconditionTime.AddMinutes(random.Next(10, 20)),
                    Type = ActivityType.Climate,
                    Message = $"{vehicleName}'s climate preconditioning stopped"
                });
            }

            // Occasional frunk/tailgate/gear tunnel access
            if (random.NextDouble() < 0.1) // 10% chance
            {
                var accessTime = dayDate.Date.AddHours(random.Next(8, 20)).AddMinutes(random.Next(0, 60));
                var closure = vehicle.Model == VehicleModel.R1T
                    ? (random.NextDouble() < 0.5 ? "Tailgate" : "Left Gear Tunnel")
                    : "Liftgate";

                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = accessTime,
                    Type = ActivityType.Closure,
                    Message = $"{vehicleName}'s {closure} was opened"
                });

                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = accessTime.AddMinutes(random.Next(1, 10)),
                    Type = ActivityType.Closure,
                    Message = $"{vehicleName}'s {closure} was closed"
                });
            }

            // Occasional Frunk access
            if (random.NextDouble() < 0.05) // 5% chance
            {
                var frunkTime = dayDate.Date.AddHours(random.Next(8, 20)).AddMinutes(random.Next(0, 60));

                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = frunkTime,
                    Type = ActivityType.Closure,
                    Message = $"{vehicleName}'s Frunk was opened"
                });

                activities.Add(new ActivityFeedItem
                {
                    VehicleId = vehicle.Id,
                    Timestamp = frunkTime.AddMinutes(random.Next(1, 5)),
                    Type = ActivityType.Closure,
                    Message = $"{vehicleName}'s Frunk was closed"
                });
            }
        }

        // Filter out any future activities and sort by timestamp
        return activities
            .Where(a => a.Timestamp <= DateTime.UtcNow)
            .OrderBy(a => a.Timestamp)
            .ToList();
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

            // Driver names for variety
            var driverNames = new[] { "Primary Driver", "Guest", "Spouse", null };

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
                DriveMode = random.NextDouble() < 0.8 ? "everyday" : "sport",
                DriverName = driverNames[random.Next(driverNames.Length)],
                WheelConfig = vehicle.WheelConfig
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
        var driverNames = new[] { "Primary Driver", "Guest", "Spouse", null };

        // Generate 2-3 states per day
        for (int day = 0; day < 30; day++)
        {
            var statesPerDay = random.Next(2, 4);
            for (int s = 0; s < statesPerDay; s++)
            {
                var timestamp = startDate.AddDays(day).AddHours(random.Next(0, 24));
                if (timestamp > DateTime.UtcNow.AddMinutes(-10)) continue;

                var batteryLevel = 30 + random.Next(0, 60);
                var rangeEstimate = batteryLevel * 3;
                var projectedRangeAt100 = batteryLevel > 5 ? rangeEstimate / (batteryLevel / 100.0) : (double?)null;
                var isCharging = random.NextDouble() < 0.3;

                states.Add(new VehicleState
                {
                    VehicleId = vehicle.Id,
                    Timestamp = timestamp,

                    // Driver
                    ActiveDriverName = driverNames[random.Next(driverNames.Length)],

                    // Battery
                    BatteryLevel = batteryLevel,
                    BatteryLimit = scenario.AverageChargeLimit + random.Next(-5, 5),
                    BatteryCapacityKwh = (vehicle.OriginalCapacityKwh ?? 135) * (scenario.CurrentHealthPercent / 100.0),
                    RangeEstimate = rangeEstimate,
                    ProjectedRangeAt100 = projectedRangeAt100.HasValue ? Math.Round(projectedRangeAt100.Value, 0) : null,
                    TwelveVoltBatteryHealth = "ready",
                    BatteryCellType = vehicle.BatteryCellType,

                    // Location & Odometer
                    Odometer = scenario.CurrentOdometer - (30 - day) * 30,
                    Latitude = scenario.HomeLatitude + (random.NextDouble() - 0.5) * 0.01,
                    Longitude = scenario.HomeLongitude + (random.NextDouble() - 0.5) * 0.01,
                    Altitude = 10 + random.Next(-5, 50),

                    // Power & Drive
                    PowerState = PowerState.Standby,
                    GearStatus = GearStatus.Park,
                    DriveMode = "everyday",
                    ChargerState = isCharging ? ChargerState.Charging : ChargerState.Disconnected,
                    TimeToEndOfCharge = isCharging ? random.Next(30, 240) : null,

                    // Climate
                    CabinTemperature = 15 + random.Next(-5, 15),
                    ClimateTargetTemp = 21,

                    // Closures (vary occasionally)
                    AllDoorsClosed = random.NextDouble() > 0.05,
                    AllDoorsLocked = random.NextDouble() > 0.1,
                    AllWindowsClosed = true,
                    FrunkClosed = true,
                    FrunkLocked = true,
                    GearGuardStatus = random.NextDouble() > 0.2 ? "Enabled" : "Disabled"
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

        // Calculate projected range at 100%
        double? projectedRangeAt100 = batteryLevel > 5 ? rangeEstimate / (batteryLevel / 100.0) : null;

        // Driver names for variety
        var driverNames = new[] { "Primary Driver", "Guest", "Spouse", null };
        var cabinTemp = 18 + random.Next(-5, 15);

        // Tire pressures - typical range 38-44 PSI
        var basePressure = 41.0;

        return new VehicleState
        {
            VehicleId = vehicle.Id,
            Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(5, 60)),

            // Driver
            ActiveDriverName = driverNames[random.Next(driverNames.Length)],

            // Battery
            BatteryLevel = batteryLevel,
            BatteryLimit = scenario.AverageChargeLimit,
            BatteryCapacityKwh = latestSnapshot.ReportedCapacityKwh,
            RangeEstimate = Math.Round(rangeEstimate, 0),
            ProjectedRangeAt100 = projectedRangeAt100.HasValue ? Math.Round(projectedRangeAt100.Value, 0) : null,
            TwelveVoltBatteryHealth = "ready",
            BatteryCellType = vehicle.BatteryCellType,
            BatteryNeedsLfpCalibration = vehicle.BatteryCellType == "LFP" ? random.NextDouble() < 0.1 : null,

            // Odometer & Location
            Odometer = scenario.CurrentOdometer,
            Latitude = scenario.HomeLatitude + (random.NextDouble() - 0.5) * 0.001,
            Longitude = scenario.HomeLongitude + (random.NextDouble() - 0.5) * 0.001,
            Altitude = 10 + random.Next(-5, 50),

            // Power & Drive
            PowerState = PowerState.Standby,
            GearStatus = GearStatus.Park,
            DriveMode = "everyday",
            ChargerState = ChargerState.Disconnected,

            // Climate
            CabinTemperature = cabinTemp,
            ClimateTargetTemp = 21,
            IsPreconditioningActive = false,
            IsPetModeActive = false,
            IsDefrostActive = false,

            // Closures
            AllDoorsClosed = true,
            AllDoorsLocked = true,
            AllWindowsClosed = true,
            FrunkClosed = true,
            FrunkLocked = true,
            LiftgateClosed = vehicle.Model == VehicleModel.R1S,
            TailgateClosed = vehicle.Model == VehicleModel.R1T,
            TonneauClosed = vehicle.Model == VehicleModel.R1T,
            SideBinLeftClosed = vehicle.Model == VehicleModel.R1T,
            SideBinLeftLocked = vehicle.Model == VehicleModel.R1T,
            SideBinRightClosed = vehicle.Model == VehicleModel.R1T,
            SideBinRightLocked = vehicle.Model == VehicleModel.R1T,
            GearGuardStatus = "Enabled",

            // Tire Pressure
            TirePressureStatusFrontLeft = TirePressureStatus.Ok,
            TirePressureStatusFrontRight = TirePressureStatus.Ok,
            TirePressureStatusRearLeft = TirePressureStatus.Ok,
            TirePressureStatusRearRight = TirePressureStatus.Ok,
            TirePressureFrontLeft = Math.Round(basePressure + (random.NextDouble() - 0.5) * 4, 1),
            TirePressureFrontRight = Math.Round(basePressure + (random.NextDouble() - 0.5) * 4, 1),
            TirePressureRearLeft = Math.Round(basePressure + (random.NextDouble() - 0.5) * 4, 1),
            TirePressureRearRight = Math.Round(basePressure + (random.NextDouble() - 0.5) * 4, 1),

            // Cold weather (usually not limited)
            LimitedAccelCold = false,
            LimitedRegenCold = false,

            // Software
            OtaCurrentVersion = vehicle.SoftwareVersion,
            OtaStatus = "idle"
        };
    }

    /// <summary>
    /// Seeds real drive data from production for testing route maps with actual GPS traces.
    /// Called after synthetic data generation for the first vehicle.
    /// </summary>
    private async Task SeedRealDriveDataAsync(Vehicle vehicle)
    {
        _logger.LogInformation("Seeding real drive data for {VehicleName}...", vehicle.Name);

        // Real drives exported from production (Ontario, Canada area)
        var drives = new List<Drive>
        {
            new Drive
            {
                VehicleId = vehicle.Id,
                StartTime = DateTime.Parse("2026-01-20T22:51:29.322297Z").ToUniversalTime(),
                EndTime = DateTime.Parse("2026-01-20T22:52:39.679385Z").ToUniversalTime(),
                IsActive = false,
                StartOdometer = 16682.724141016464,
                EndOdometer = 16682.83785194464,
                DistanceMiles = 0.11371092817716999,
                StartBatteryLevel = 70.599998,
                EndBatteryLevel = 70.400002,
                EnergyUsedKwh = 0.26199475999999833,
                StartRangeEstimate = 203.188317,
                EndRangeEstimate = 202.566946,
                EfficiencyMilesPerKwh = 0.4340198566458761,
                EfficiencyWhPerMile = 2304.042049430739,
                StartLatitude = 43.9792519,
                StartLongitude = -79.224884,
                EndLatitude = 43.9778519,
                EndLongitude = -79.2244034,
                MaxSpeedMph = 8.27,
                AverageSpeedMph = 5.5935,
                StartElevation = 241.86,
                EndElevation = 240.718,
                ElevationGain = 0.013,
                DriveMode = "everyday"
            },
            new Drive
            {
                VehicleId = vehicle.Id,
                StartTime = DateTime.Parse("2026-01-20T23:05:30.560425Z").ToUniversalTime(),
                EndTime = DateTime.Parse("2026-01-20T23:09:01.968731Z").ToUniversalTime(),
                IsActive = false,
                StartOdometer = 16682.83785194464,
                EndOdometer = 16683.88672651714,
                DistanceMiles = 1.0488745724978799,
                StartBatteryLevel = 69.599998,
                EndBatteryLevel = 68.700005,
                EnergyUsedKwh = 1.1789908299999934,
                StartRangeEstimate = 200.08146200000002,
                EndRangeEstimate = 197.595978,
                EfficiencyMilesPerKwh = 0.8896376000633404,
                EfficiencyWhPerMile = 1124.0532099012025,
                StartLatitude = 43.9778519,
                StartLongitude = -79.2244034,
                EndLatitude = 43.972496,
                EndLongitude = -79.2422485,
                MaxSpeedMph = 13.317,
                AverageSpeedMph = 9.349583333333335,
                StartElevation = 240.719,
                EndElevation = 229.832,
                ElevationGain = 7.378999999999991,
                DriveMode = "everyday"
            },
            new Drive
            {
                VehicleId = vehicle.Id,
                StartTime = DateTime.Parse("2026-01-20T23:13:32.37995Z").ToUniversalTime(),
                EndTime = DateTime.Parse("2026-01-20T23:22:35.869992Z").ToUniversalTime(),
                IsActive = false,
                StartOdometer = 16683.88672651714,
                EndOdometer = 16686.65244969379,
                DistanceMiles = 2.7657231766497716,
                StartBatteryLevel = 68.300003,
                EndBatteryLevel = 66.700005,
                EnergyUsedKwh = 2.095997379999999,
                StartRangeEstimate = 196.974607,
                EndRangeEstimate = 192.003639,
                EfficiencyMilesPerKwh = 1.3195260657481227,
                EfficiencyWhPerMile = 757.8478561035753,
                StartLatitude = 43.9724922,
                StartLongitude = -79.2422791,
                EndLatitude = 43.9547386,
                EndLongitude = -79.2767334,
                MaxSpeedMph = 14.756,
                AverageSpeedMph = 9.475,
                StartElevation = 229.974,
                EndElevation = 216.269,
                ElevationGain = 14.221999999999952,
                DriveMode = "everyday"
            },
            new Drive
            {
                VehicleId = vehicle.Id,
                StartTime = DateTime.Parse("2026-01-20T23:50:15.997412Z").ToUniversalTime(),
                EndTime = DateTime.Parse("2026-01-21T00:03:15.471669Z").ToUniversalTime(),
                IsActive = false,
                StartOdometer = 16686.65244969379,
                EndOdometer = 16690.431629284976,
                DistanceMiles = 3.7791795911871304,
                StartBatteryLevel = 65.599998,
                EndBatteryLevel = 62.700001,
                EnergyUsedKwh = 3.7989960699999985,
                StartRangeEstimate = 188.896784,
                EndRangeEstimate = 180.19759,
                EfficiencyMilesPerKwh = 0.9947837590648342,
                EfficiencyWhPerMile = 1005.2435927784642,
                StartLatitude = 43.9547195,
                StartLongitude = -79.2768326,
                EndLatitude = 43.9792328,
                EndLongitude = -79.224884,
                MaxSpeedMph = 14.178,
                AverageSpeedMph = 8.615195876288661,
                StartElevation = 215.661,
                EndElevation = 242.232,
                ElevationGain = 44.07299999999995,
                DriveMode = "everyday"
            }
        };

        _db.Drives.AddRange(drives);
        await _db.SaveChangesAsync();

        // Now add positions for each drive
        var allPositions = new List<Position>();

        // Drive 1 positions (short backup out of driveway)
        allPositions.AddRange(new[]
        {
            new Position { DriveId = drives[0].Id, Timestamp = DateTime.Parse("2026-01-20T22:51:29.322297Z").ToUniversalTime(), Latitude = 43.9792519, Longitude = -79.224884, Altitude = 241.86, Speed = 0, Heading = 66.0642, BatteryLevel = 70.599998, Odometer = 16682.724141016464, Gear = GearStatus.Reverse },
            new Position { DriveId = drives[0].Id, Timestamp = DateTime.Parse("2026-01-20T22:52:20.463268Z").ToUniversalTime(), Latitude = 43.9792519, Longitude = -79.224884, Altitude = 241.86, Speed = 0, Heading = 66.0615, BatteryLevel = 70.599998, Odometer = 16682.743403523422, Gear = GearStatus.Reverse },
            new Position { DriveId = drives[0].Id, Timestamp = DateTime.Parse("2026-01-20T22:52:28.214391Z").ToUniversalTime(), Latitude = 43.9781265, Longitude = -79.2245941, Altitude = 240.57, Speed = 8.27, Heading = 150.8234, BatteryLevel = 70.400002, Odometer = 16682.782549908534, Gear = GearStatus.Drive },
            new Position { DriveId = drives[0].Id, Timestamp = DateTime.Parse("2026-01-20T22:52:34.057741Z").ToUniversalTime(), Latitude = 43.9778824, Longitude = -79.2244263, Altitude = 240.583, Speed = 2.917, Heading = 155.2334, BatteryLevel = 70.400002, Odometer = 16682.83785194464, Gear = GearStatus.Drive }
        });

        // Drive 2 positions
        allPositions.AddRange(new[]
        {
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:05:30.560425Z").ToUniversalTime(), Latitude = 43.9778519, Longitude = -79.2244034, Altitude = 240.719, Speed = 0, Heading = 155.3333, BatteryLevel = 69.599998, Odometer = 16682.83785194464, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:05:39.801479Z").ToUniversalTime(), Latitude = 43.9776497, Longitude = -79.2243271, Altitude = 239.975, Speed = 5.221, Heading = 175.0269, BatteryLevel = 69.599998, Odometer = 16682.84282291418, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:05:47.312678Z").ToUniversalTime(), Latitude = 43.9774666, Longitude = -79.2245255, Altitude = 240.101, Speed = 6.582, Heading = 242.6452, BatteryLevel = 69.599998, Odometer = 16682.868920504254, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:05:54.218305Z").ToUniversalTime(), Latitude = 43.9772186, Longitude = -79.2256241, Altitude = 240.99, Speed = 10.927, Heading = 253.0471, BatteryLevel = 69.599998, Odometer = 16682.92297979798, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:06:02.230639Z").ToUniversalTime(), Latitude = 43.9770737, Longitude = -79.2262802, Altitude = 242.561, Speed = 10.638, Heading = 252.3937, BatteryLevel = 69.599998, Odometer = 16682.989466515548, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:06:10.421085Z").ToUniversalTime(), Latitude = 43.9768295, Longitude = -79.2273407, Altitude = 244.591, Speed = 5.802, Heading = 249.5981, BatteryLevel = 69.599998, Odometer = 16682.989466515548, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:06:16.358869Z").ToUniversalTime(), Latitude = 43.9765816, Longitude = -79.2274551, Altitude = 244.465, Speed = 7.664, Heading = 167.634, BatteryLevel = 69.400002, Odometer = 16683.035447983773, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:06:22.312224Z").ToUniversalTime(), Latitude = 43.976223, Longitude = -79.2273102, Altitude = 246.596, Speed = 8.478, Heading = 164.6366, BatteryLevel = 69.400002, Odometer = 16683.083293565578, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:06:35.031382Z").ToUniversalTime(), Latitude = 43.975647, Longitude = -79.2278214, Altitude = 246.677, Speed = 12.621, Heading = 251.8711, BatteryLevel = 69.400002, Odometer = 16683.083293565578, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:06:43.30777Z").ToUniversalTime(), Latitude = 43.9752922, Longitude = -79.2294235, Altitude = 244.425, Speed = 13.317, Heading = 252.0849, BatteryLevel = 69.400002, Odometer = 16683.083293565578, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:06:49.199888Z").ToUniversalTime(), Latitude = 43.9751167, Longitude = -79.2301941, Altitude = 244.295, Speed = 13.304, Heading = 252.6461, BatteryLevel = 69.200005, Odometer = 16683.202596834486, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:06:55.077806Z").ToUniversalTime(), Latitude = 43.974968, Longitude = -79.2308502, Altitude = 244.094, Speed = 6.846, Heading = 252.9251, BatteryLevel = 69.200005, Odometer = 16683.282132347093, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:07:18.466437Z").ToUniversalTime(), Latitude = 43.9748955, Longitude = -79.231163, Altitude = 244.215, Speed = 7.069, Heading = 250.8289, BatteryLevel = 69.099998, Odometer = 16683.294559770937, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:07:24.446693Z").ToUniversalTime(), Latitude = 43.9747772, Longitude = -79.2316666, Altitude = 243.305, Speed = 8.763, Heading = 254.1606, BatteryLevel = 69.099998, Odometer = 16683.294559770937, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:07:30.796335Z").ToUniversalTime(), Latitude = 43.9746513, Longitude = -79.2322083, Altitude = 242.786, Speed = 10.447, Heading = 251.5713, BatteryLevel = 69, Odometer = 16683.33991986797, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:07:40.547121Z").ToUniversalTime(), Latitude = 43.97435, Longitude = -79.23349, Altitude = 242.052, Speed = 10.512, Heading = 252.7368, BatteryLevel = 69, Odometer = 16683.405163843156, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:07:52.842768Z").ToUniversalTime(), Latitude = 43.9740715, Longitude = -79.2347412, Altitude = 239.226, Speed = 10.234, Heading = 252.6435, BatteryLevel = 68.900002, Odometer = 16683.47102918953, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:07:59.831919Z").ToUniversalTime(), Latitude = 43.973793, Longitude = -79.2359695, Altitude = 235.916, Speed = 10.651, Heading = 253.0329, BatteryLevel = 68.900002, Odometer = 16683.535651793525, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:08:09.212712Z").ToUniversalTime(), Latitude = 43.9735146, Longitude = -79.2372131, Altitude = 234.192, Speed = 10.524, Heading = 253.4724, BatteryLevel = 68.900002, Odometer = 16683.60089576871, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:08:15.005138Z").ToUniversalTime(), Latitude = 43.9733887, Longitude = -79.2378311, Altitude = 233.461, Speed = 10.372, Heading = 253.3935, BatteryLevel = 68.900002, Odometer = 16683.666139743895, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:08:28.172199Z").ToUniversalTime(), Latitude = 43.9729576, Longitude = -79.2396851, Altitude = 232.773, Speed = 10.64, Heading = 254.5497, BatteryLevel = 68.800003, Odometer = 16683.730140976695, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:08:33.81481Z").ToUniversalTime(), Latitude = 43.9728203, Longitude = -79.240303, Altitude = 230.592, Speed = 9.963, Heading = 252.9998, BatteryLevel = 68.800003, Odometer = 16683.730140976695, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:08:39.254984Z").ToUniversalTime(), Latitude = 43.9726868, Longitude = -79.2409058, Altitude = 229.724, Speed = 10.395, Heading = 252.7398, BatteryLevel = 68.800003, Odometer = 16683.79538495188, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:08:44.760691Z").ToUniversalTime(), Latitude = 43.9725456, Longitude = -79.2415314, Altitude = 229.336, Speed = 10.513, Heading = 252.8996, BatteryLevel = 68.800003, Odometer = 16683.79538495188, Gear = GearStatus.Drive },
            new Position { DriveId = drives[1].Id, Timestamp = DateTime.Parse("2026-01-20T23:08:54.365268Z").ToUniversalTime(), Latitude = 43.9724998, Longitude = -79.2421951, Altitude = 229.766, Speed = 2.907, Heading = 310.1447, BatteryLevel = 68.700005, Odometer = 16683.86000755587, Gear = GearStatus.Drive }
        });

        // Drive 3 positions (longer drive) - sample of key points
        allPositions.AddRange(new[]
        {
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:13:32.37995Z").ToUniversalTime(), Latitude = 43.9724922, Longitude = -79.2422791, Altitude = 229.974, Speed = 0, Heading = 263.4757, BatteryLevel = 68.300003, Odometer = 16683.88672651714, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:14:33.16495Z").ToUniversalTime(), Latitude = 43.9720039, Longitude = -79.2439194, Altitude = 230.961, Speed = 11.497, Heading = 253.3549, BatteryLevel = 68.200005, Odometer = 16683.94824226517, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:15:19.48251Z").ToUniversalTime(), Latitude = 43.9708443, Longitude = -79.2492065, Altitude = 235.134, Speed = 9.647, Heading = 253.4281, BatteryLevel = 68.099998, Odometer = 16684.22910204406, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:16:33.035386Z").ToUniversalTime(), Latitude = 43.969265, Longitude = -79.2563324, Altitude = 235.399, Speed = 13.279, Heading = 253.2443, BatteryLevel = 67.800003, Odometer = 16684.557807404755, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:17:48.67314Z").ToUniversalTime(), Latitude = 43.9679222, Longitude = -79.2623367, Altitude = 232.398, Speed = 13.306, Heading = 253.0702, BatteryLevel = 67.599998, Odometer = 16684.87594945518, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:18:33.958749Z").ToUniversalTime(), Latitude = 43.9663544, Longitude = -79.2693939, Altitude = 224.543, Speed = 13.032, Heading = 248.9677, BatteryLevel = 67.599998, Odometer = 16685.28605444206, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:19:13.549114Z").ToUniversalTime(), Latitude = 43.9636116, Longitude = -79.269783, Altitude = 220.773, Speed = 10.558, Heading = 173.6376, BatteryLevel = 67.300003, Odometer = 16685.52776783584, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:19:52.653977Z").ToUniversalTime(), Latitude = 43.9609299, Longitude = -79.2666092, Altitude = 220.718, Speed = 10.772, Heading = 131.57, BatteryLevel = 67.200005, Odometer = 16685.793093334923, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:20:29.47623Z").ToUniversalTime(), Latitude = 43.9592018, Longitude = -79.268631, Altitude = 217.696, Speed = 13.298, Heading = 244.4622, BatteryLevel = 67.099998, Odometer = 16685.960242185636, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:21:09.659504Z").ToUniversalTime(), Latitude = 43.9577522, Longitude = -79.2749786, Altitude = 218.963, Speed = 12.21, Heading = 250.1537, BatteryLevel = 66.900002, Odometer = 16686.296404000637, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:21:59.147143Z").ToUniversalTime(), Latitude = 43.9555397, Longitude = -79.2763138, Altitude = 217.725, Speed = 5.289, Heading = 161.6979, BatteryLevel = 66.800003, Odometer = 16686.543709735146, Gear = GearStatus.Drive },
            new Position { DriveId = drives[2].Id, Timestamp = DateTime.Parse("2026-01-20T23:22:19.231599Z").ToUniversalTime(), Latitude = 43.9547768, Longitude = -79.2766876, Altitude = 216.182, Speed = 2.983, Heading = 183.5587, BatteryLevel = 66.700005, Odometer = 16686.623866618946, Gear = GearStatus.Drive }
        });

        // Drive 4 positions (return trip) - sample of key points
        allPositions.AddRange(new[]
        {
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-20T23:50:15.997412Z").ToUniversalTime(), Latitude = 43.9547195, Longitude = -79.2768326, Altitude = 215.661, Speed = 0, Heading = 252.805, BatteryLevel = 65.599998, Odometer = 16686.65244969379, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-20T23:51:46.029898Z").ToUniversalTime(), Latitude = 43.9572258, Longitude = -79.2759781, Altitude = 218.643, Speed = 4.627, Heading = 344.4612, BatteryLevel = 65.599998, Odometer = 16686.874279209416, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-20T23:52:22.319216Z").ToUniversalTime(), Latitude = 43.9582787, Longitude = -79.2716904, Altitude = 219.849, Speed = 13.513, Heading = 75.9884, BatteryLevel = 65.400002, Odometer = 16687.132148254193, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-20T23:53:05.422895Z").ToUniversalTime(), Latitude = 43.9599113, Longitude = -79.2655182, Altitude = 219.063, Speed = 10.699, Heading = 71.5832, BatteryLevel = 65.200005, Odometer = 16687.426678199314, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-20T23:54:00.501334Z").ToUniversalTime(), Latitude = 43.9615974, Longitude = -79.2588043, Altitude = 223.024, Speed = 11.507, Heading = 76.5976, BatteryLevel = 65.099998, Odometer = 16687.748548476895, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-20T23:55:39.210503Z").ToUniversalTime(), Latitude = 43.9625702, Longitude = -79.2541046, Altitude = 224.168, Speed = 11.493, Heading = 66.4878, BatteryLevel = 64.800003, Odometer = 16688.046806649167, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-20T23:56:35.395776Z").ToUniversalTime(), Latitude = 43.9644012, Longitude = -79.2467499, Altitude = 226.749, Speed = 4.062, Heading = 72.8355, BatteryLevel = 64.599998, Odometer = 16688.446969696968, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-20T23:57:31.052984Z").ToUniversalTime(), Latitude = 43.9654388, Longitude = -79.2422791, Altitude = 226.052, Speed = 4.767, Heading = 67.8667, BatteryLevel = 64.300003, Odometer = 16688.657614531137, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-20T23:58:46.739257Z").ToUniversalTime(), Latitude = 43.9666176, Longitude = -79.2363815, Altitude = 230.197, Speed = 3.447, Heading = 100.7956, BatteryLevel = 63.900002, Odometer = 16688.9807275511, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-20T23:59:27.505658Z").ToUniversalTime(), Latitude = 43.9676933, Longitude = -79.231369, Altitude = 233.392, Speed = 9.305, Heading = 72.0131, BatteryLevel = 63.700001, Odometer = 16689.27215064026, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-21T00:00:16.323879Z").ToUniversalTime(), Latitude = 43.9708023, Longitude = -79.231102, Altitude = 237.728, Speed = 10.699, Heading = 350.4032, BatteryLevel = 63.600002, Odometer = 16689.531883798616, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-21T00:00:51.212756Z").ToUniversalTime(), Latitude = 43.9741211, Longitude = -79.2318802, Altitude = 241.155, Speed = 9.673, Heading = 353.9201, BatteryLevel = 63.299999, Odometer = 16689.734450807286, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-21T00:01:25.798936Z").ToUniversalTime(), Latitude = 43.9754829, Longitude = -79.2284317, Altitude = 245.599, Speed = 13.652, Heading = 73.1394, BatteryLevel = 63.100002, Odometer = 16689.983620655374, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-21T00:02:15.775919Z").ToUniversalTime(), Latitude = 43.9772682, Longitude = -79.2252808, Altitude = 241.407, Speed = 8.658, Heading = 73.2841, BatteryLevel = 62.900002, Odometer = 16690.176245724964, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-21T00:02:50.431523Z").ToUniversalTime(), Latitude = 43.9789467, Longitude = -79.2252197, Altitude = 241.857, Speed = 5.729, Heading = 328.2819, BatteryLevel = 62.799999, Odometer = 16690.378191362444, Gear = GearStatus.Drive },
            new Position { DriveId = drives[3].Id, Timestamp = DateTime.Parse("2026-01-21T00:03:15.471669Z").ToUniversalTime(), Latitude = 43.9792328, Longitude = -79.224884, Altitude = 242.232, Speed = 0.002, Heading = 61.697, BatteryLevel = 62.700001, Odometer = 16690.431629284976, Gear = GearStatus.Park }
        });

        _db.Positions.AddRange(allPositions);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Seeded {DriveCount} real drives with {PositionCount} positions", drives.Count, allPositions.Count);
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
