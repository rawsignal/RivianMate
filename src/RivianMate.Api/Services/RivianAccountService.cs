using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RivianMate.Api.Configuration;
using RivianMate.Api.Services.Jobs;
using RivianMate.Core.Entities;
using RivianMate.Core.Enums;
using RivianMate.Infrastructure.Data;
using RivianMate.Infrastructure.Nhtsa;
using RivianMate.Infrastructure.Rivian;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for managing linked Rivian accounts for users.
/// Handles authentication, token storage, and vehicle syncing.
///
/// SECURITY NOTE: We NEVER store Rivian passwords. Passwords are only held
/// in memory during the authentication flow and passed directly to Rivian's
/// servers. Only the encrypted API tokens returned by Rivian are persisted.
/// </summary>
public class RivianAccountService
{
    private readonly RivianMateDbContext _db;
    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NhtsaVinDecoderService _nhtsaService;
    private readonly PollingJobManager _jobManager;
    private readonly PollingConfiguration _pollingConfig;
    private readonly VehicleStateNotifier _stateNotifier;
    private readonly ILogger<RivianAccountService> _logger;
    private readonly ILogger<RivianApiClient> _rivianLogger;

    public RivianAccountService(
        RivianMateDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        NhtsaVinDecoderService nhtsaService,
        PollingJobManager jobManager,
        IOptions<PollingConfiguration> pollingConfig,
        VehicleStateNotifier stateNotifier,
        ILogger<RivianAccountService> logger,
        ILogger<RivianApiClient> rivianLogger)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector("RivianMate.RivianAccounts");
        _httpClientFactory = httpClientFactory;
        _nhtsaService = nhtsaService;
        _jobManager = jobManager;
        _pollingConfig = pollingConfig.Value;
        _stateNotifier = stateNotifier;
        _logger = logger;
        _rivianLogger = rivianLogger;
    }

    /// <summary>
    /// Get all Rivian accounts for a user.
    /// </summary>
    public async Task<List<RivianAccount>> GetAccountsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.RivianAccounts
            .Where(ra => ra.UserId == userId)
            .OrderBy(ra => ra.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get a specific Rivian account, verifying ownership.
    /// </summary>
    public async Task<RivianAccount?> GetAccountAsync(int accountId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.RivianAccounts
            .FirstOrDefaultAsync(ra => ra.Id == accountId && ra.UserId == userId, cancellationToken);
    }

    /// <summary>
    /// Check if a user has any linked Rivian accounts.
    /// </summary>
    public async Task<bool> HasAnyAccountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.RivianAccounts.AnyAsync(ra => ra.UserId == userId, cancellationToken);
    }

    /// <summary>
    /// Begin the login process for a new Rivian account.
    /// The password is passed directly to Rivian's servers and is NOT stored.
    /// Returns a session context containing only API tokens for completing the login.
    /// </summary>
    public async Task<RivianLoginSession> BeginLoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(RivianApiClient));
        var client = new RivianApiClient(httpClient, _rivianLogger);

        // Create CSRF token
        if (!await client.CreateCsrfTokenAsync(cancellationToken))
        {
            throw new Exception("Failed to initialize Rivian API session");
        }

        // Attempt login
        var otpToken = await client.LoginAsync(email, password, cancellationToken);

        var tokens = client.GetTokens();

        return new RivianLoginSession
        {
            Email = email,
            RequiresMfa = otpToken != null,
            OtpToken = otpToken,
            CsrfToken = tokens.CsrfToken,
            AppSessionToken = tokens.AppSessionToken,
            UserSessionToken = tokens.UserSessionToken,
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken
        };
    }

    /// <summary>
    /// Complete MFA login with OTP code.
    /// </summary>
    public async Task<RivianLoginSession> CompleteMfaLoginAsync(
        RivianLoginSession session,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        if (!session.RequiresMfa || string.IsNullOrEmpty(session.OtpToken))
        {
            throw new InvalidOperationException("MFA not required for this session");
        }

        var httpClient = _httpClientFactory.CreateClient(nameof(RivianApiClient));
        var client = new RivianApiClient(httpClient, _rivianLogger);

        // Restore session state
        client.SetTokens(
            session.CsrfToken ?? "",
            session.AppSessionToken ?? "",
            session.UserSessionToken ?? "",
            session.AccessToken ?? "",
            session.RefreshToken ?? "");

        // Complete OTP login
        await client.LoginWithOtpAsync(session.Email, otpCode, session.OtpToken, cancellationToken);

        var tokens = client.GetTokens();

        return new RivianLoginSession
        {
            Email = session.Email,
            RequiresMfa = false,
            CsrfToken = tokens.CsrfToken,
            AppSessionToken = tokens.AppSessionToken,
            UserSessionToken = tokens.UserSessionToken,
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken
        };
    }

    /// <summary>
    /// Create a new linked Rivian account from a completed login session.
    /// </summary>
    public async Task<RivianAccount> CreateAccountFromSessionAsync(
        Guid userId,
        RivianLoginSession session,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(session.AccessToken))
        {
            throw new InvalidOperationException("Login session is not authenticated");
        }

        // Check if this Rivian email is already linked to this user
        var existingAccount = await _db.RivianAccounts
            .FirstOrDefaultAsync(ra => ra.UserId == userId && ra.RivianEmail == session.Email, cancellationToken);

        if (existingAccount != null)
        {
            // Update existing account with new tokens
            existingAccount.EncryptedCsrfToken = Encrypt(session.CsrfToken);
            existingAccount.EncryptedAppSessionToken = Encrypt(session.AppSessionToken);
            existingAccount.EncryptedUserSessionToken = Encrypt(session.UserSessionToken);
            existingAccount.EncryptedAccessToken = Encrypt(session.AccessToken);
            existingAccount.EncryptedRefreshToken = Encrypt(session.RefreshToken);
            existingAccount.IsActive = true;
            existingAccount.LastSyncError = null;

            await _db.SaveChangesAsync(cancellationToken);

            // Register for state updates based on mode
            // In GraphQL mode, register Hangfire polling job
            // In WebSocket mode, the background service will pick up the account on next refresh
            if (_pollingConfig.Mode == PollingMode.GraphQL)
            {
                _jobManager.RegisterAccountJob(existingAccount.Id);
            }

            _logger.LogInformation("Updated existing Rivian account {AccountId} for user {UserId} (mode: {Mode})",
                existingAccount.Id, userId, _pollingConfig.Mode);
            return existingAccount;
        }

        // Create new account
        var account = new RivianAccount
        {
            UserId = userId,
            RivianEmail = session.Email,
            EncryptedCsrfToken = Encrypt(session.CsrfToken),
            EncryptedAppSessionToken = Encrypt(session.AppSessionToken),
            EncryptedUserSessionToken = Encrypt(session.UserSessionToken),
            EncryptedAccessToken = Encrypt(session.AccessToken),
            EncryptedRefreshToken = Encrypt(session.RefreshToken),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.RivianAccounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);

        // Register for state updates based on mode
        if (_pollingConfig.Mode == PollingMode.GraphQL)
        {
            _jobManager.RegisterAccountJob(account.Id);
        }

        _logger.LogInformation("Created new Rivian account {AccountId} for user {UserId} (mode: {Mode})",
            account.Id, userId, _pollingConfig.Mode);
        return account;
    }

    /// <summary>
    /// Create an authenticated API client for a Rivian account.
    /// </summary>
    public RivianApiClient CreateApiClient(RivianAccount account)
    {
        var httpClient = _httpClientFactory.CreateClient(nameof(RivianApiClient));
        var client = new RivianApiClient(httpClient, _rivianLogger);

        client.SetTokens(
            Decrypt(account.EncryptedCsrfToken) ?? "",
            Decrypt(account.EncryptedAppSessionToken) ?? "",
            Decrypt(account.EncryptedUserSessionToken) ?? "",
            Decrypt(account.EncryptedAccessToken) ?? "",
            Decrypt(account.EncryptedRefreshToken) ?? "");

        return client;
    }

    /// <summary>
    /// Get decrypted tokens for a Rivian account.
    /// Returns (userSessionToken, accessToken) for WebSocket authentication.
    /// </summary>
    public (string? UserSessionToken, string? AccessToken) GetDecryptedTokens(RivianAccount account)
    {
        return (
            Decrypt(account.EncryptedUserSessionToken),
            Decrypt(account.EncryptedAccessToken)
        );
    }

    /// <summary>
    /// Sync vehicles from a Rivian account.
    /// </summary>
    public async Task<List<Vehicle>> SyncVehiclesAsync(
        RivianAccount account,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateApiClient(account);

        var userInfo = await client.GetUserInfoAsync(cancellationToken);
        if (userInfo == null)
        {
            throw new Exception("Failed to get user info from Rivian API");
        }

        // Update Rivian user ID
        if (!string.IsNullOrEmpty(userInfo.Id) && account.RivianUserId != userInfo.Id)
        {
            account.RivianUserId = userInfo.Id;
        }

        var syncedVehicles = new List<Vehicle>();

        if (userInfo.Vehicles == null || !userInfo.Vehicles.Any())
        {
            _logger.LogWarning("No vehicles found in Rivian account {AccountId}", account.Id);
            account.LastSyncAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return syncedVehicles;
        }

        // Debug logging to understand API response structure
        foreach (var rv in userInfo.Vehicles)
        {
            _logger.LogInformation(
                "Rivian API vehicle data: Id={Id}, Name={Name}, VIN={Vin}, " +
                "VehicleDetails=[ModelYear={ModelYear}, Make={Make}, Model={Model}]",
                rv.Id, rv.Name, rv.Vin,
                rv.Vehicle?.ModelYear, rv.Vehicle?.Make, rv.Vehicle?.Model);
        }

        foreach (var rivianVehicle in userInfo.Vehicles)
        {
            if (string.IsNullOrEmpty(rivianVehicle.Id))
                continue;

            // Find existing vehicle by Rivian ID
            var vehicle = await _db.Vehicles
                .FirstOrDefaultAsync(v => v.RivianVehicleId == rivianVehicle.Id, cancellationToken);

            if (vehicle == null)
            {
                vehicle = new Vehicle
                {
                    RivianVehicleId = rivianVehicle.Id,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Vehicles.Add(vehicle);
            }

            // Link to this account and user
            vehicle.RivianAccountId = account.Id;
            vehicle.OwnerId = account.UserId;

            // Update vehicle details
            vehicle.Vin = rivianVehicle.Vin ?? vehicle.Vin;
            vehicle.Name = rivianVehicle.Name ?? vehicle.Name;

            // Parse vehicle configuration from API response
            if (rivianVehicle.Vehicle != null)
            {
                if (rivianVehicle.Vehicle.ModelYear.HasValue)
                    vehicle.Year = rivianVehicle.Vehicle.ModelYear.Value;

                var model = rivianVehicle.Vehicle.Model?.ToUpperInvariant();
                vehicle.Model = model switch
                {
                    "R1T" => RivianMate.Core.Enums.VehicleModel.R1T,
                    "R1S" => RivianMate.Core.Enums.VehicleModel.R1S,
                    _ => vehicle.Model
                };

                // Parse configuration options (if available)
                var config = rivianVehicle.Vehicle.MobileConfiguration;
                if (config != null)
                {
                    vehicle.ExteriorColor = config.ExteriorColorOption?.OptionName ?? vehicle.ExteriorColor;
                    vehicle.InteriorColor = config.InteriorColorOption?.OptionName ?? vehicle.InteriorColor;
                    vehicle.WheelConfig = config.WheelOption?.OptionName ?? vehicle.WheelConfig;

                    var batteryOption = config.BatteryOption?.OptionName?.ToUpperInvariant() ?? "";
                    vehicle.BatteryPack = batteryOption switch
                    {
                        var b when b.Contains("STANDARD") => RivianMate.Core.Enums.BatteryPackType.Standard,
                        var b when b.Contains("LARGE") => RivianMate.Core.Enums.BatteryPackType.Large,
                        var b when b.Contains("MAX") => RivianMate.Core.Enums.BatteryPackType.Max,
                        _ => vehicle.BatteryPack
                    };

                    var driveOption = config.DriveSystemOption?.OptionName?.ToUpperInvariant() ?? "";
                    vehicle.DriveType = driveOption switch
                    {
                        var d when d.Contains("DUAL") => RivianMate.Core.Enums.DriveType.DualMotor,
                        var d when d.Contains("QUAD") => RivianMate.Core.Enums.DriveType.QuadMotor,
                        _ => vehicle.DriveType
                    };
                }
            }

            vehicle.LastSeenAt = DateTime.UtcNow;
            vehicle.IsActive = true;

            // If we have a VIN but are missing configuration data, try NHTSA
            if (!string.IsNullOrEmpty(vehicle.Vin) &&
                (vehicle.DriveType == RivianMate.Core.Enums.DriveType.Unknown ||
                 vehicle.Trim == RivianMate.Core.Enums.VehicleTrim.Unknown ||
                 vehicle.Model == RivianMate.Core.Enums.VehicleModel.Unknown ||
                 !vehicle.Year.HasValue))
            {
                var nhtsaInfo = await _nhtsaService.DecodeVinAsync(vehicle.Vin, cancellationToken);
                if (nhtsaInfo != null)
                {
                    _logger.LogInformation(
                        "NHTSA decoded VIN {Vin}: Model={Model}, Year={Year}, DriveType={DriveType}, Trim={Trim}",
                        vehicle.Vin, nhtsaInfo.Model, nhtsaInfo.Year, nhtsaInfo.DriveType, nhtsaInfo.Trim);

                    // Only populate fields that are currently unknown
                    if (vehicle.Model == RivianMate.Core.Enums.VehicleModel.Unknown && nhtsaInfo.Model != RivianMate.Core.Enums.VehicleModel.Unknown)
                        vehicle.Model = nhtsaInfo.Model;

                    if (!vehicle.Year.HasValue && nhtsaInfo.Year.HasValue)
                        vehicle.Year = nhtsaInfo.Year;

                    if (vehicle.DriveType == RivianMate.Core.Enums.DriveType.Unknown && nhtsaInfo.DriveType != RivianMate.Core.Enums.DriveType.Unknown)
                        vehicle.DriveType = nhtsaInfo.DriveType;

                    if (vehicle.Trim == RivianMate.Core.Enums.VehicleTrim.Unknown && nhtsaInfo.Trim != RivianMate.Core.Enums.VehicleTrim.Unknown)
                        vehicle.Trim = nhtsaInfo.Trim;
                }
            }

            syncedVehicles.Add(vehicle);
        }

        account.LastSyncAt = DateTime.UtcNow;
        account.LastSyncError = null;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Synced {Count} vehicles from Rivian account {AccountId}", syncedVehicles.Count, account.Id);

        // Notify UI components that vehicles have changed
        await _stateNotifier.NotifyVehiclesChangedAsync();

        return syncedVehicles;
    }

    /// <summary>
    /// Update tokens for a Rivian account.
    /// </summary>
    public async Task UpdateTokensAsync(
        RivianAccount account,
        string? csrfToken,
        string? appSessionToken,
        string? userSessionToken,
        string? accessToken,
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(csrfToken))
            account.EncryptedCsrfToken = Encrypt(csrfToken);
        if (!string.IsNullOrEmpty(appSessionToken))
            account.EncryptedAppSessionToken = Encrypt(appSessionToken);
        if (!string.IsNullOrEmpty(userSessionToken))
            account.EncryptedUserSessionToken = Encrypt(userSessionToken);
        if (!string.IsNullOrEmpty(accessToken))
            account.EncryptedAccessToken = Encrypt(accessToken);
        if (!string.IsNullOrEmpty(refreshToken))
            account.EncryptedRefreshToken = Encrypt(refreshToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Delete a Rivian account and all associated data.
    /// This removes vehicles, vehicle states, charging sessions, drives, positions, and battery health snapshots.
    /// </summary>
    public async Task DeleteAccountAsync(RivianAccount account, CancellationToken cancellationToken = default)
    {
        // Remove state updates based on mode
        // In GraphQL mode, remove the Hangfire polling job
        // In WebSocket mode, the background service will notice the account is gone on next refresh
        if (_pollingConfig.Mode == PollingMode.GraphQL)
        {
            _jobManager.RemoveAccountJob(account.Id);
        }

        // Get all vehicles for this account
        var vehicleIds = await _db.Vehicles
            .Where(v => v.RivianAccountId == account.Id)
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        if (vehicleIds.Any())
        {
            _logger.LogInformation(
                "Deleting {Count} vehicles and all associated data for Rivian account {AccountId}",
                vehicleIds.Count, account.Id);

            // Delete all vehicles - cascade delete will remove:
            // - VehicleStates
            // - ChargingSessions
            // - Drives (which cascades to Positions)
            // - BatteryHealthSnapshots
            var vehicles = await _db.Vehicles
                .Where(v => vehicleIds.Contains(v.Id))
                .ToListAsync(cancellationToken);

            _db.Vehicles.RemoveRange(vehicles);
        }

        _db.RivianAccounts.Remove(account);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted Rivian account {AccountId} for user {UserId} (removed {VehicleCount} vehicles)",
            account.Id, account.UserId, vehicleIds.Count);
    }

    /// <summary>
    /// Get all active Rivian accounts for polling.
    /// </summary>
    public async Task<List<RivianAccount>> GetAllActiveAccountsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.RivianAccounts
            .Where(ra => ra.IsActive && ra.EncryptedAccessToken != null)
            .ToListAsync(cancellationToken);
    }

    private string? Encrypt(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        try
        {
            return _protector.Protect(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt value");
            return null;
        }
    }

    private string? Decrypt(string? encryptedValue)
    {
        if (string.IsNullOrEmpty(encryptedValue))
            return null;

        try
        {
            return _protector.Unprotect(encryptedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt value");
            return null;
        }
    }
}

/// <summary>
/// Session state for the Rivian login flow.
/// Contains only API tokens returned by Rivian - the password is NEVER stored here.
/// This object is held in memory only during the authentication flow.
/// </summary>
public class RivianLoginSession
{
    /// <summary>
    /// Rivian account email (needed for MFA completion)
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Whether MFA is required to complete login
    /// </summary>
    public bool RequiresMfa { get; set; }

    /// <summary>
    /// OTP token from Rivian for MFA verification (not the user's password)
    /// </summary>
    public string? OtpToken { get; set; }

    // API session tokens from Rivian - these are NOT passwords
    public string? CsrfToken { get; set; }
    public string? AppSessionToken { get; set; }
    public string? UserSessionToken { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
}
