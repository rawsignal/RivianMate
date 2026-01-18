using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RivianMate.Core.Entities;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

/// <summary>
/// Service for managing application settings stored in the database.
/// Handles encryption of sensitive values like API tokens.
/// </summary>
public class SettingsService
{
    private readonly RivianMateDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(
        RivianMateDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SettingsService> logger)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector("RivianMate.Settings");
        _logger = logger;
    }

    /// <summary>
    /// Get a setting value by key
    /// </summary>
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _db.Settings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting == null)
            return null;

        if (setting.IsEncrypted && !string.IsNullOrEmpty(setting.Value))
        {
            try
            {
                return _protector.Unprotect(setting.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt setting {Key}", key);
                return null;
            }
        }

        return setting.Value;
    }

    /// <summary>
    /// Get a setting value with a default if not found
    /// </summary>
    public async Task<string> GetAsync(string key, string defaultValue, CancellationToken cancellationToken = default)
    {
        return await GetAsync(key, cancellationToken) ?? defaultValue;
    }

    /// <summary>
    /// Get a setting value as integer
    /// </summary>
    public async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Get a setting value as boolean
    /// </summary>
    public async Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Get a setting value as double
    /// </summary>
    public async Task<double> GetDoubleAsync(string key, double defaultValue, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken);
        return double.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Set a setting value
    /// </summary>
    public async Task SetAsync(string key, string? value, bool encrypt = false, CancellationToken cancellationToken = default)
    {
        var setting = await _db.Settings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        var valueToStore = value;
        if (encrypt && !string.IsNullOrEmpty(value))
        {
            valueToStore = _protector.Protect(value);
        }

        if (setting == null)
        {
            setting = new Setting
            {
                Key = key,
                Value = valueToStore,
                IsEncrypted = encrypt,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Settings.Add(setting);
        }
        else
        {
            setting.Value = valueToStore;
            setting.IsEncrypted = encrypt;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Delete a setting
    /// </summary>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _db.Settings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting != null)
        {
            _db.Settings.Remove(setting);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Check if Rivian authentication is configured
    /// </summary>
    public async Task<bool> IsRivianAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAsync(SettingKeys.RivianAccessToken, cancellationToken);
        return !string.IsNullOrEmpty(accessToken);
    }

    /// <summary>
    /// Store Rivian authentication tokens (encrypted)
    /// </summary>
    public async Task SaveRivianTokensAsync(
        string csrfToken,
        string appSessionToken,
        string userSessionToken,
        string accessToken,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        await SetAsync(SettingKeys.RivianCsrfToken, csrfToken, encrypt: true, cancellationToken);
        await SetAsync(SettingKeys.RivianAppSessionToken, appSessionToken, encrypt: true, cancellationToken);
        await SetAsync(SettingKeys.RivianUserSessionToken, userSessionToken, encrypt: true, cancellationToken);
        await SetAsync(SettingKeys.RivianAccessToken, accessToken, encrypt: true, cancellationToken);
        await SetAsync(SettingKeys.RivianRefreshToken, refreshToken, encrypt: true, cancellationToken);
        
        _logger.LogInformation("Rivian authentication tokens saved");
    }

    /// <summary>
    /// Load Rivian authentication tokens
    /// </summary>
    public async Task<(string? CsrfToken, string? AppSessionToken, string? UserSessionToken, 
        string? AccessToken, string? RefreshToken)> LoadRivianTokensAsync(CancellationToken cancellationToken = default)
    {
        return (
            await GetAsync(SettingKeys.RivianCsrfToken, cancellationToken),
            await GetAsync(SettingKeys.RivianAppSessionToken, cancellationToken),
            await GetAsync(SettingKeys.RivianUserSessionToken, cancellationToken),
            await GetAsync(SettingKeys.RivianAccessToken, cancellationToken),
            await GetAsync(SettingKeys.RivianRefreshToken, cancellationToken)
        );
    }

    /// <summary>
    /// Clear all Rivian authentication tokens
    /// </summary>
    public async Task ClearRivianTokensAsync(CancellationToken cancellationToken = default)
    {
        await DeleteAsync(SettingKeys.RivianCsrfToken, cancellationToken);
        await DeleteAsync(SettingKeys.RivianAppSessionToken, cancellationToken);
        await DeleteAsync(SettingKeys.RivianUserSessionToken, cancellationToken);
        await DeleteAsync(SettingKeys.RivianAccessToken, cancellationToken);
        await DeleteAsync(SettingKeys.RivianRefreshToken, cancellationToken);
        await DeleteAsync(SettingKeys.RivianUserId, cancellationToken);
        
        _logger.LogInformation("Rivian authentication tokens cleared");
    }
}
