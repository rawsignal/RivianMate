using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsEncrypted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RivianAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RivianEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RivianUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EncryptedCsrfToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedAppSessionToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedUserSessionToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedAccessToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedRefreshToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RivianAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RivianAccounts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDashboardConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDashboardConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDashboardConfigs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RivianVehicleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Model = table.Column<int>(type: "integer", nullable: false),
                    BatteryPack = table.Column<int>(type: "integer", nullable: false),
                    DriveType = table.Column<int>(type: "integer", nullable: false),
                    Trim = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    ExteriorColor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InteriorColor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WheelConfig = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EpaRangeMiles = table.Column<double>(type: "double precision", nullable: true),
                    OriginalCapacityKwh = table.Column<double>(type: "double precision", nullable: true),
                    BatteryCellType = table.Column<string>(type: "text", nullable: true),
                    SoftwareVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ImageData = table.Column<byte[]>(type: "bytea", nullable: true),
                    ImageContentType = table.Column<string>(type: "text", nullable: true),
                    ImageVersion = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    RivianAccountId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Vehicles_RivianAccounts_RivianAccountId",
                        column: x => x.RivianAccountId,
                        principalTable: "RivianAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BatteryHealthSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Odometer = table.Column<double>(type: "double precision", nullable: false),
                    ReportedCapacityKwh = table.Column<double>(type: "double precision", nullable: false),
                    StateOfCharge = table.Column<double>(type: "double precision", nullable: true),
                    Temperature = table.Column<double>(type: "double precision", nullable: true),
                    OriginalCapacityKwh = table.Column<double>(type: "double precision", nullable: false),
                    HealthPercent = table.Column<double>(type: "double precision", nullable: false),
                    CapacityLostKwh = table.Column<double>(type: "double precision", nullable: false),
                    DegradationPercent = table.Column<double>(type: "double precision", nullable: false),
                    DegradationRatePer10kMiles = table.Column<double>(type: "double precision", nullable: true),
                    ProjectedHealthAt100kMiles = table.Column<double>(type: "double precision", nullable: true),
                    ProjectedMilesTo70Percent = table.Column<double>(type: "double precision", nullable: true),
                    RemainingWarrantyCapacityKwh = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatteryHealthSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatteryHealthSnapshots_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChargingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartBatteryLevel = table.Column<double>(type: "double precision", nullable: false),
                    EndBatteryLevel = table.Column<double>(type: "double precision", nullable: true),
                    ChargeLimit = table.Column<double>(type: "double precision", nullable: true),
                    EnergyAddedKwh = table.Column<double>(type: "double precision", nullable: true),
                    PeakPowerKw = table.Column<double>(type: "double precision", nullable: true),
                    AveragePowerKw = table.Column<double>(type: "double precision", nullable: true),
                    StartRangeEstimate = table.Column<double>(type: "double precision", nullable: true),
                    EndRangeEstimate = table.Column<double>(type: "double precision", nullable: true),
                    RangeAdded = table.Column<double>(type: "double precision", nullable: true),
                    ChargeType = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    LocationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsHomeCharging = table.Column<bool>(type: "boolean", nullable: true),
                    Cost = table.Column<double>(type: "double precision", nullable: true),
                    DriveMode = table.Column<string>(type: "text", nullable: true),
                    OdometerAtStart = table.Column<double>(type: "double precision", nullable: true),
                    TemperatureAtStart = table.Column<double>(type: "double precision", nullable: true),
                    CalculatedCapacityKwh = table.Column<double>(type: "double precision", nullable: true),
                    CapacityConfidence = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargingSessions_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Drives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartOdometer = table.Column<double>(type: "double precision", nullable: false),
                    EndOdometer = table.Column<double>(type: "double precision", nullable: true),
                    DistanceMiles = table.Column<double>(type: "double precision", nullable: true),
                    StartBatteryLevel = table.Column<double>(type: "double precision", nullable: false),
                    EndBatteryLevel = table.Column<double>(type: "double precision", nullable: true),
                    EnergyUsedKwh = table.Column<double>(type: "double precision", nullable: true),
                    StartRangeEstimate = table.Column<double>(type: "double precision", nullable: true),
                    EndRangeEstimate = table.Column<double>(type: "double precision", nullable: true),
                    EfficiencyMilesPerKwh = table.Column<double>(type: "double precision", nullable: true),
                    EfficiencyWhPerMile = table.Column<double>(type: "double precision", nullable: true),
                    StartLatitude = table.Column<double>(type: "double precision", nullable: true),
                    StartLongitude = table.Column<double>(type: "double precision", nullable: true),
                    EndLatitude = table.Column<double>(type: "double precision", nullable: true),
                    EndLongitude = table.Column<double>(type: "double precision", nullable: true),
                    StartAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EndAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MaxSpeedMph = table.Column<double>(type: "double precision", nullable: true),
                    AverageSpeedMph = table.Column<double>(type: "double precision", nullable: true),
                    StartElevation = table.Column<double>(type: "double precision", nullable: true),
                    EndElevation = table.Column<double>(type: "double precision", nullable: true),
                    ElevationGain = table.Column<double>(type: "double precision", nullable: true),
                    AverageTemperature = table.Column<double>(type: "double precision", nullable: true),
                    DriveMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drives_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleStates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Altitude = table.Column<double>(type: "double precision", nullable: true),
                    Speed = table.Column<double>(type: "double precision", nullable: true),
                    Heading = table.Column<double>(type: "double precision", nullable: true),
                    ActiveDriverName = table.Column<string>(type: "text", nullable: true),
                    BatteryLevel = table.Column<double>(type: "double precision", nullable: true),
                    BatteryLimit = table.Column<double>(type: "double precision", nullable: true),
                    BatteryCapacityKwh = table.Column<double>(type: "double precision", nullable: true),
                    RangeEstimate = table.Column<double>(type: "double precision", nullable: true),
                    ProjectedRangeAt100 = table.Column<double>(type: "double precision", nullable: true),
                    TwelveVoltBatteryHealth = table.Column<string>(type: "text", nullable: true),
                    BatteryCellType = table.Column<string>(type: "text", nullable: true),
                    BatteryNeedsLfpCalibration = table.Column<bool>(type: "boolean", nullable: true),
                    Odometer = table.Column<double>(type: "double precision", nullable: true),
                    PowerState = table.Column<int>(type: "integer", nullable: false),
                    GearStatus = table.Column<int>(type: "integer", nullable: false),
                    DriveMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ChargerState = table.Column<int>(type: "integer", nullable: false),
                    TimeToEndOfCharge = table.Column<int>(type: "integer", nullable: true),
                    ChargePortOpen = table.Column<bool>(type: "boolean", nullable: true),
                    ChargerDerateStatus = table.Column<string>(type: "text", nullable: true),
                    LimitedAccelCold = table.Column<bool>(type: "boolean", nullable: true),
                    LimitedRegenCold = table.Column<bool>(type: "boolean", nullable: true),
                    CabinTemperature = table.Column<double>(type: "double precision", nullable: true),
                    ClimateTargetTemp = table.Column<double>(type: "double precision", nullable: true),
                    IsPreconditioningActive = table.Column<bool>(type: "boolean", nullable: true),
                    IsPetModeActive = table.Column<bool>(type: "boolean", nullable: true),
                    IsDefrostActive = table.Column<bool>(type: "boolean", nullable: true),
                    AllDoorsClosed = table.Column<bool>(type: "boolean", nullable: true),
                    AllDoorsLocked = table.Column<bool>(type: "boolean", nullable: true),
                    AllWindowsClosed = table.Column<bool>(type: "boolean", nullable: true),
                    FrunkClosed = table.Column<bool>(type: "boolean", nullable: true),
                    FrunkLocked = table.Column<bool>(type: "boolean", nullable: true),
                    LiftgateClosed = table.Column<bool>(type: "boolean", nullable: true),
                    TonneauClosed = table.Column<bool>(type: "boolean", nullable: true),
                    GearGuardEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    TirePressureStatusFrontLeft = table.Column<int>(type: "integer", nullable: false),
                    TirePressureStatusFrontRight = table.Column<int>(type: "integer", nullable: false),
                    TirePressureStatusRearLeft = table.Column<int>(type: "integer", nullable: false),
                    TirePressureStatusRearRight = table.Column<int>(type: "integer", nullable: false),
                    TirePressureFrontLeft = table.Column<double>(type: "double precision", nullable: true),
                    TirePressureFrontRight = table.Column<double>(type: "double precision", nullable: true),
                    TirePressureRearLeft = table.Column<double>(type: "double precision", nullable: true),
                    TirePressureRearRight = table.Column<double>(type: "double precision", nullable: true),
                    OtaCurrentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OtaAvailableVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OtaStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OtaInstallProgress = table.Column<int>(type: "integer", nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleStates_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DriveId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Altitude = table.Column<double>(type: "double precision", nullable: true),
                    Speed = table.Column<double>(type: "double precision", nullable: true),
                    Heading = table.Column<double>(type: "double precision", nullable: true),
                    BatteryLevel = table.Column<double>(type: "double precision", nullable: true),
                    Odometer = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Positions_Drives_DriveId",
                        column: x => x.DriveId,
                        principalTable: "Drives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BatteryHealthSnapshots_Timestamp",
                table: "BatteryHealthSnapshots",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_BatteryHealthSnapshots_VehicleId_Timestamp",
                table: "BatteryHealthSnapshots",
                columns: new[] { "VehicleId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ChargingSessions_IsActive",
                table: "ChargingSessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingSessions_StartTime",
                table: "ChargingSessions",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingSessions_VehicleId_StartTime",
                table: "ChargingSessions",
                columns: new[] { "VehicleId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Drives_IsActive",
                table: "Drives",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Drives_StartTime",
                table: "Drives",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Drives_VehicleId_StartTime",
                table: "Drives",
                columns: new[] { "VehicleId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Positions_DriveId_Timestamp",
                table: "Positions",
                columns: new[] { "DriveId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Positions_Timestamp",
                table: "Positions",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_RivianAccounts_UserId",
                table: "RivianAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RivianAccounts_UserId_RivianEmail",
                table: "RivianAccounts",
                columns: new[] { "UserId", "RivianEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Key",
                table: "Settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDashboardConfigs_UserId",
                table: "UserDashboardConfigs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDashboardConfigs_UserId_CardId",
                table: "UserDashboardConfigs",
                columns: new[] { "UserId", "CardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_OwnerId",
                table: "Vehicles",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RivianAccountId",
                table: "Vehicles",
                column: "RivianAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RivianVehicleId",
                table: "Vehicles",
                column: "RivianVehicleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Vin",
                table: "Vehicles",
                column: "Vin");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleStates_Timestamp",
                table: "VehicleStates",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleStates_VehicleId_Timestamp",
                table: "VehicleStates",
                columns: new[] { "VehicleId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BatteryHealthSnapshots");

            migrationBuilder.DropTable(
                name: "ChargingSessions");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "UserDashboardConfigs");

            migrationBuilder.DropTable(
                name: "VehicleStates");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Drives");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "RivianAccounts");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
