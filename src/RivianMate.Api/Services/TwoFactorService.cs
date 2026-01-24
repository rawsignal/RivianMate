using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QRCoder;
using RivianMate.Api.Services.Email;
using RivianMate.Core.Entities;
using RivianMate.Infrastructure.Data;

namespace RivianMate.Api.Services;

public class TwoFactorService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<RivianMateDbContext> _dbContextFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEmailTrigger _emailTrigger;
    private readonly ILogger<TwoFactorService> _logger;

    private const int RecoveryCodeCount = 10;
    private const string Issuer = "RivianMate";

    public TwoFactorService(
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<RivianMateDbContext> dbContextFactory,
        IHttpContextAccessor httpContextAccessor,
        IEmailTrigger emailTrigger,
        ILogger<TwoFactorService> logger)
    {
        _scopeFactory = scopeFactory;
        _dbContextFactory = dbContextFactory;
        _httpContextAccessor = httpContextAccessor;
        _emailTrigger = emailTrigger;
        _logger = logger;
    }

    /// <summary>
    /// Gets a fresh UserManager from an isolated scope to avoid DbContext tracking issues
    /// </summary>
    private async Task<T> WithFreshUserManagerAsync<T>(Func<UserManager<ApplicationUser>, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await action(userManager);
    }

    /// <summary>
    /// Generates a QR code data URL for authenticator app setup
    /// </summary>
    public async Task<(string SharedKey, string QrCodeDataUrl)?> GenerateQrCodeAsync(Guid userId)
    {
        return await WithFreshUserManagerAsync(async userManager =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found when generating QR code", userId);
                return ((string, string)?)null;
            }

            // Reset authenticator key to generate a new one
            await userManager.ResetAuthenticatorKeyAsync(user);
            var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);

            if (string.IsNullOrEmpty(unformattedKey))
            {
                throw new InvalidOperationException("Failed to generate authenticator key");
            }

            // Format the key for display (groups of 4 characters)
            var sharedKey = FormatKey(unformattedKey);

            // Generate the authenticator URI
            var email = await userManager.GetEmailAsync(user) ?? user.UserName ?? "user";
            var authenticatorUri = GenerateQrCodeUri(email, unformattedKey);

            // Generate QR code as data URL
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(authenticatorUri, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(20);
            var qrCodeDataUrl = $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";

            return (sharedKey, qrCodeDataUrl);
        });
    }

    /// <summary>
    /// Verifies a TOTP code and enables 2FA if valid
    /// </summary>
    public async Task<bool> VerifyAndEnableTwoFactorAsync(Guid userId, string verificationCode)
    {
        var result = await WithFreshUserManagerAsync(async userManager =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found when verifying 2FA code", userId);
                return (Success: false, Email: (string?)null, DisplayName: (string?)null);
            }

            // Strip any spaces or dashes from the code
            var code = verificationCode.Replace(" ", "").Replace("-", "");

            var isValid = await userManager.VerifyTwoFactorTokenAsync(
                user,
                userManager.Options.Tokens.AuthenticatorTokenProvider,
                code);

            if (!isValid)
            {
                _logger.LogWarning("Invalid 2FA verification code for user {UserId}", userId);
                return (Success: false, Email: (string?)null, DisplayName: (string?)null);
            }

            // Enable 2FA
            var enableResult = await userManager.SetTwoFactorEnabledAsync(user, true);
            if (!enableResult.Succeeded)
            {
                _logger.LogError("Failed to enable 2FA for user {UserId}: {Errors}",
                    user.Id, string.Join(", ", enableResult.Errors.Select(e => e.Description)));
                return (Success: false, Email: (string?)null, DisplayName: (string?)null);
            }

            _logger.LogInformation(
                "Two-factor authentication enabled for user {UserId}. TwoFactorEnabled={TwoFactorEnabled}",
                user.Id, user.TwoFactorEnabled);

            return (Success: true, Email: user.Email, DisplayName: user.DisplayName);
        });

        if (result.Success)
        {
            await LogSecurityEventAsync(userId, "TwoFactorEnabled");

            // Send confirmation email
            if (!string.IsNullOrEmpty(result.Email))
            {
                _emailTrigger.FireTwoFactorEnabled(result.Email, userId, result.DisplayName);
            }
        }

        return result.Success;
    }

    /// <summary>
    /// Generates new recovery codes for the user
    /// </summary>
    public async Task<IList<string>> GenerateRecoveryCodesAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Remove existing recovery codes
        var existingCodes = await dbContext.UserRecoveryCodes
            .Where(c => c.UserId == userId)
            .ToListAsync();
        dbContext.UserRecoveryCodes.RemoveRange(existingCodes);

        // Generate new recovery codes
        var recoveryCodes = new List<string>();
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var code = GenerateRecoveryCode();
            recoveryCodes.Add(code);

            dbContext.UserRecoveryCodes.Add(new UserRecoveryCode
            {
                UserId = userId,
                CodeHash = HashCode(code),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
        _logger.LogInformation("Generated {Count} recovery codes for user {UserId}", RecoveryCodeCount, userId);

        return recoveryCodes;
    }

    /// <summary>
    /// Validates a recovery code and marks it as used
    /// </summary>
    public async Task<bool> ValidateRecoveryCodeAsync(Guid userId, string recoveryCode)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Normalize the code
        var normalizedCode = recoveryCode.Replace(" ", "").Replace("-", "").ToUpperInvariant();
        var codeHash = HashCode(normalizedCode);

        var storedCode = await dbContext.UserRecoveryCodes
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CodeHash == codeHash && !c.IsUsed);

        if (storedCode == null)
        {
            await LogSecurityEventAsync(userId, "RecoveryCodeInvalid");
            return false;
        }

        // Mark the code as used
        storedCode.IsUsed = true;
        storedCode.UsedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        await LogSecurityEventAsync(userId, "RecoveryCodeUsed");
        _logger.LogInformation("Recovery code used for user {UserId}", userId);

        return true;
    }

    /// <summary>
    /// Gets the count of remaining (unused) recovery codes
    /// </summary>
    public async Task<int> GetRemainingRecoveryCodeCountAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.UserRecoveryCodes
            .CountAsync(c => c.UserId == userId && !c.IsUsed);
    }

    /// <summary>
    /// Disables two-factor authentication after verifying password
    /// </summary>
    public async Task<(bool Success, string? Error)> DisableTwoFactorAsync(Guid userId, string password)
    {
        var result = await WithFreshUserManagerAsync(async userManager =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return (false, "User not found.");
            }

            // Verify password
            var passwordValid = await userManager.CheckPasswordAsync(user, password);
            if (!passwordValid)
            {
                return (false, "Invalid password.");
            }

            // Disable 2FA
            var disableResult = await userManager.SetTwoFactorEnabledAsync(user, false);
            if (!disableResult.Succeeded)
            {
                return (false, string.Join(" ", disableResult.Errors.Select(e => e.Description)));
            }

            // Reset authenticator key
            await userManager.ResetAuthenticatorKeyAsync(user);

            return (true, (string?)null);
        });

        if (!result.Item1)
        {
            return result;
        }

        // Remove all recovery codes (using separate context)
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var recoveryCodes = await dbContext.UserRecoveryCodes
            .Where(c => c.UserId == userId)
            .ToListAsync();
        dbContext.UserRecoveryCodes.RemoveRange(recoveryCodes);
        await dbContext.SaveChangesAsync();

        await LogSecurityEventAsync(userId, "TwoFactorDisabled");
        _logger.LogInformation("Two-factor authentication disabled for user {UserId}", userId);

        return (true, null);
    }

    /// <summary>
    /// Logs a security event
    /// </summary>
    public async Task LogSecurityEventAsync(Guid userId, string eventType, string? details = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var httpContext = _httpContextAccessor.HttpContext;

        var securityEvent = new SecurityEvent
        {
            UserId = userId,
            EventType = eventType,
            IpAddress = GetClientIpAddress(httpContext),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        dbContext.SecurityEvents.Add(securityEvent);
        await dbContext.SaveChangesAsync();
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;

        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private static string GenerateQrCodeUri(string email, string unformattedKey)
    {
        return $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(email)}?secret={unformattedKey}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";
    }

    private static string GenerateRecoveryCode()
    {
        // Generate XXXXX-XXXXX format
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Exclude confusing chars: I, O, 0, 1
        var bytes = RandomNumberGenerator.GetBytes(10);
        var code = new char[11];

        for (var i = 0; i < 5; i++)
        {
            code[i] = chars[bytes[i] % chars.Length];
        }
        code[5] = '-';
        for (var i = 0; i < 5; i++)
        {
            code[i + 6] = chars[bytes[i + 5] % chars.Length];
        }

        return new string(code);
    }

    private static string HashCode(string code)
    {
        // Normalize: remove dashes/spaces and uppercase
        var normalized = code.Replace("-", "").Replace(" ", "").ToUpperInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private static string? GetClientIpAddress(HttpContext? context)
    {
        if (context == null) return null;

        // Check for forwarded header (common when behind a proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the list (original client)
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
