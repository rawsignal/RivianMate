namespace RivianMate.Infrastructure.Data;

/// <summary>
/// Compile-time table name constants based on edition.
/// Pro uses production names, SelfHosted uses obfuscated names.
/// </summary>
public static class TableNames
{
#if EDITION_PRO
    // Identity tables
    public const string Users = "AspNetUsers";
    public const string Roles = "AspNetRoles";
    public const string UserRoles = "AspNetUserRoles";
    public const string UserClaims = "AspNetUserClaims";
    public const string UserLogins = "AspNetUserLogins";
    public const string UserTokens = "AspNetUserTokens";
    public const string RoleClaims = "AspNetRoleClaims";

    // Core tables
    public const string RivianAccounts = "RivianAccounts";
    public const string Vehicles = "Vehicles";
    public const string VehicleStates = "VehicleStates";
    public const string ChargingSessions = "ChargingSessions";
    public const string Drives = "Drives";
    public const string Positions = "Positions";
    public const string BatteryHealthSnapshots = "BatteryHealthSnapshots";
    public const string ActivityFeed = "ActivityFeed";
    public const string Settings = "Settings";
    public const string UserDashboardConfigs = "UserDashboardConfigs";
    public const string UserPreferences = "UserPreferences";
    public const string UserLocations = "UserLocations";
    public const string GeocodingCache = "GeocodingCache";
    public const string EmailLogs = "EmailLogs";
    public const string BroadcastEmails = "BroadcastEmails";
    public const string UserRecoveryCodes = "UserRecoveryCodes";
    public const string SecurityEvents = "SecurityEvents";
    public const string DataProtectionKeys = "DataProtectionKeys";
#else
    // Identity tables (obfuscated)
    public const string Users = "Users";
    public const string Roles = "Roles";
    public const string UserRoles = "UserRoles";
    public const string UserClaims = "UserClaims";
    public const string UserLogins = "UserLogins";
    public const string UserTokens = "UserTokens";
    public const string RoleClaims = "RoleClaims";

    // Core tables (obfuscated)
    public const string RivianAccounts = "LinkedAccounts";
    public const string Vehicles = "Cars";
    public const string VehicleStates = "CarStates";
    public const string ChargingSessions = "ChargeRecords";
    public const string Drives = "Trips";
    public const string Positions = "TripPoints";
    public const string BatteryHealthSnapshots = "BatteryRecords";
    public const string ActivityFeed = "Events";
    public const string Settings = "Config";
    public const string UserDashboardConfigs = "DashboardPrefs";
    public const string UserPreferences = "Prefs";
    public const string UserLocations = "Locations";
    public const string GeocodingCache = "AddressCache";
    public const string EmailLogs = "MailHistory";
    public const string BroadcastEmails = "Broadcasts";
    public const string UserRecoveryCodes = "RecoveryCodes";
    public const string SecurityEvents = "AuditLog";
    public const string DataProtectionKeys = "KeyStore";
#endif
}
