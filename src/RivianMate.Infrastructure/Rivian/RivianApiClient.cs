using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RivianMate.Core.Exceptions;
using RivianMate.Infrastructure.Rivian.Models;

namespace RivianMate.Infrastructure.Rivian;

/// <summary>
/// Client for interacting with the Rivian API.
/// Based on the unofficial API documentation at https://rivian-api.kaedenb.org/
/// </summary>
public class RivianApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RivianApiClient> _logger;
    
    private const string BaseUrl = "https://rivian.com/api/gql/gateway/graphql";
    private const string ClientName = "com.rivian.android.consumer";
    
    // Authentication state
    private string? _csrfToken;
    private string? _appSessionToken;
    private string? _userSessionToken;
    private string? _accessToken;
    private string? _refreshToken;
    
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);
    
    public RivianApiClient(HttpClient httpClient, ILogger<RivianApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Set default headers
        _httpClient.DefaultRequestHeaders.Add("apollographql-client-name", ClientName);
    }
    
    /// <summary>
    /// Set authentication tokens from stored values (e.g., from database)
    /// </summary>
    public void SetTokens(string csrfToken, string appSessionToken, string userSessionToken, 
        string accessToken, string refreshToken)
    {
        _csrfToken = csrfToken;
        _appSessionToken = appSessionToken;
        _userSessionToken = userSessionToken;
        _accessToken = accessToken;
        _refreshToken = refreshToken;
    }
    
    /// <summary>
    /// Get current tokens for storage
    /// </summary>
    public (string? CsrfToken, string? AppSessionToken, string? UserSessionToken, 
        string? AccessToken, string? RefreshToken) GetTokens()
    {
        return (_csrfToken, _appSessionToken, _userSessionToken, _accessToken, _refreshToken);
    }
    
    /// <summary>
    /// Step 1: Create CSRF token (required before login)
    /// </summary>
    public async Task<bool> CreateCsrfTokenAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating CSRF token...");
        
        var request = new GraphQlRequest
        {
            OperationName = "CreateCSRFToken",
            Variables = new { },
            Query = "mutation CreateCSRFToken { createCsrfToken { __typename csrfToken appSessionToken } }"
        };
        
        try
        {
            var response = await _httpClient.PostAsJsonAsync(BaseUrl, request, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<CreateCsrfTokenResponse>(cancellationToken);
            
            if (result?.Data?.CreateCsrfToken != null)
            {
                _csrfToken = result.Data.CreateCsrfToken.CsrfToken;
                _appSessionToken = result.Data.CreateCsrfToken.AppSessionToken;
                
                _logger.LogDebug("CSRF token created successfully");
                return true;
            }
            
            _logger.LogWarning("Failed to create CSRF token: empty response");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating CSRF token");
            return false;
        }
    }
    
    /// <summary>
    /// Step 2: Login with email and password
    /// Returns OTP token if MFA is required, null if login successful, throws on error
    /// </summary>
    public async Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_csrfToken) || string.IsNullOrEmpty(_appSessionToken))
        {
            throw new InvalidOperationException("Must create CSRF token before logging in");
        }
        
        _logger.LogDebug("Attempting login for {Email}...", email);
        
        var request = new GraphQlRequest
        {
            OperationName = "Login",
            Variables = new { email, password },
            Query = @"mutation Login($email: String!, $password: String!) {
                login(email: $email, password: $password) {
                    __typename
                    ... on MobileLoginResponse {
                        accessToken
                        refreshToken
                        userSessionToken
                    }
                    ... on MobileMFALoginResponse {
                        otpToken
                    }
                }
            }"
        };
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
        httpRequest.Headers.Add("a-sess", _appSessionToken);
        httpRequest.Headers.Add("csrf-token", _csrfToken);
        httpRequest.Content = JsonContent.Create(request);
        
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        
        if (result?.Errors?.Any() == true)
        {
            var errorMessage = string.Join(", ", result.Errors.Select(e => e.Message));
            _logger.LogError("Login failed: {Errors}", errorMessage);
            throw new ExternalServiceException("Rivian", "Invalid email or password", errorMessage);
        }

        var loginResult = result?.Data?.Login;

        if (loginResult?.TypeName == "MobileMFALoginResponse")
        {
            _logger.LogDebug("MFA required");
            return loginResult.OtpToken;
        }

        if (loginResult?.AccessToken != null)
        {
            _accessToken = loginResult.AccessToken;
            _refreshToken = loginResult.RefreshToken;
            _userSessionToken = loginResult.UserSessionToken;

            _logger.LogInformation("Login successful");
            return null;
        }

        throw new ExternalServiceException("Rivian", "Login failed", "Unexpected login response from Rivian API");
    }
    
    /// <summary>
    /// Step 2b: Complete login with OTP code (if MFA was required)
    /// </summary>
    public async Task LoginWithOtpAsync(string email, string otpCode, string otpToken, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_csrfToken) || string.IsNullOrEmpty(_appSessionToken))
        {
            throw new InvalidOperationException("Must create CSRF token before logging in");
        }
        
        _logger.LogDebug("Completing MFA login...");
        
        var request = new GraphQlRequest
        {
            OperationName = "LoginWithOTP",
            Variables = new { email, otpCode, otpToken },
            Query = @"mutation LoginWithOTP($email: String!, $otpCode: String!, $otpToken: String!) {
                loginWithOTP(email: $email, otpCode: $otpCode, otpToken: $otpToken) {
                    __typename
                    accessToken
                    refreshToken
                    userSessionToken
                }
            }"
        };
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
        httpRequest.Headers.Add("a-sess", _appSessionToken);
        httpRequest.Headers.Add("csrf-token", _csrfToken);
        httpRequest.Content = JsonContent.Create(request);
        
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        
        if (result?.Errors?.Any() == true)
        {
            var errorMessage = string.Join(", ", result.Errors.Select(e => e.Message));
            _logger.LogError("OTP login failed: {Errors}", errorMessage);
            throw new ExternalServiceException("Rivian", "Invalid verification code", errorMessage);
        }

        var loginResult = result?.Data?.LoginWithOtp;

        if (loginResult?.AccessToken != null)
        {
            _accessToken = loginResult.AccessToken;
            _refreshToken = loginResult.RefreshToken;
            _userSessionToken = loginResult.UserSessionToken;

            _logger.LogInformation("MFA login successful");
            return;
        }

        throw new ExternalServiceException("Rivian", "Verification failed", "Unexpected OTP login response from Rivian API");
    }
    
    /// <summary>
    /// Get user info including list of vehicles
    /// </summary>
    public async Task<CurrentUser?> GetUserInfoAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        _logger.LogDebug("Getting user info...");

        // Simplified query - mobileConfiguration fields no longer available in Rivian API
        var request = new GraphQlRequest
        {
            OperationName = "getUserInfo",
            Variables = new { },
            Query = @"query getUserInfo {
                currentUser {
                    __typename
                    id
                    firstName
                    lastName
                    email
                    vehicles {
                        __typename
                        id
                        name
                        vin
                        state
                        createdAt
                        updatedAt
                        vehicle {
                            modelYear
                            make
                            model
                        }
                    }
                }
            }"
        };

        // Use raw request to capture response for debugging
        using var httpRequest = CreateAuthenticatedRequest(request);
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug("GetUserInfo raw response: {Response}", responseBody);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("API request failed with {StatusCode}. Response: {Response}",
                response.StatusCode, responseBody);
            throw new ExternalServiceException("Rivian", responseBody, (int)response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<GetUserInfoResponse>(responseBody);

        if (result?.Errors?.Any() == true)
        {
            var errorMessage = string.Join(", ", result.Errors.Select(e => e.Message));
            var errorCode = result.Errors.FirstOrDefault()?.Extensions?.Code;

            if (errorCode == "UNAUTHENTICATED" || errorMessage.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Authentication failed when getting user info: {Errors}", errorMessage);
                throw ExternalServiceException.AuthenticationFailed("Rivian", errorMessage);
            }

            _logger.LogError("GetUserInfo failed: {Errors}", errorMessage);
            throw new ExternalServiceException("Rivian", "Failed to retrieve vehicle information", errorMessage);
        }

        return result?.Data?.CurrentUser;
    }
    
    /// <summary>
    /// Get current vehicle state
    /// </summary>
    public async Task<(RivianVehicleState? State, string? RawJson)> GetVehicleStateAsync(
        string vehicleId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        
        _logger.LogDebug("Getting vehicle state for {VehicleId}...", vehicleId);
        
        // Full query based on rivian-python-client VEHICLE_STATE_PROPERTIES
        var request = new GraphQlRequest
        {
            OperationName = "GetVehicleState",
            Variables = new { vehicleID = vehicleId },
            Query = @"query GetVehicleState($vehicleID: String!) {
                vehicleState(id: $vehicleID) {
                    __typename
                    gnssLocation { latitude longitude timeStamp }
                    gnssSpeed { timeStamp value }
                    gnssAltitude { timeStamp value }
                    gnssBearing { timeStamp value }
                    alarmSoundStatus { timeStamp value }
                    batteryCapacity { timeStamp value }
                    batteryCellType { timeStamp value }
                    batteryHvThermalEvent { timeStamp value }
                    batteryHvThermalEventPropagation { timeStamp value }
                    batteryLevel { timeStamp value }
                    batteryLimit { timeStamp value }
                    brakeFluidLow { timeStamp value }
                    cabinClimateDriverTemperature { timeStamp value }
                    cabinClimateInteriorTemperature { timeStamp value }
                    cabinPreconditioningStatus { timeStamp value }
                    cabinPreconditioningType { timeStamp value }
                    carWashMode { timeStamp value }
                    chargerDerateStatus { timeStamp value }
                    chargerState { timeStamp value }
                    chargerStatus { timeStamp value }
                    chargePortState { timeStamp value }
                    closureFrunkClosed { timeStamp value }
                    closureFrunkLocked { timeStamp value }
                    closureLiftgateClosed { timeStamp value }
                    closureLiftgateLocked { timeStamp value }
                    closureLiftgateNextAction { timeStamp value }
                    closureSideBinLeftClosed { timeStamp value }
                    closureSideBinLeftLocked { timeStamp value }
                    closureSideBinRightClosed { timeStamp value }
                    closureSideBinRightLocked { timeStamp value }
                    closureTailgateClosed { timeStamp value }
                    closureTailgateLocked { timeStamp value }
                    closureTonneauClosed { timeStamp value }
                    closureTonneauLocked { timeStamp value }
                    defrostDefogStatus { timeStamp value }
                    distanceToEmpty { timeStamp value }
                    doorFrontLeftClosed { timeStamp value }
                    doorFrontLeftLocked { timeStamp value }
                    doorFrontRightClosed { timeStamp value }
                    doorFrontRightLocked { timeStamp value }
                    doorRearLeftClosed { timeStamp value }
                    doorRearLeftLocked { timeStamp value }
                    doorRearRightClosed { timeStamp value }
                    doorRearRightLocked { timeStamp value }
                    driveMode { timeStamp value }
                    gearGuardLocked { timeStamp value }
                    gearGuardVideoMode { timeStamp value }
                    gearGuardVideoStatus { timeStamp value }
                    gearGuardVideoTermsAccepted { timeStamp value }
                    gearStatus { timeStamp value }
                    limitedAccelCold { timeStamp value }
                    limitedRegenCold { timeStamp value }
                    otaAvailableVersion { timeStamp value }
                    otaAvailableVersionGitHash { timeStamp value }
                    otaAvailableVersionNumber { timeStamp value }
                    otaAvailableVersionWeek { timeStamp value }
                    otaAvailableVersionYear { timeStamp value }
                    otaCurrentStatus { timeStamp value }
                    otaCurrentVersion { timeStamp value }
                    otaCurrentVersionGitHash { timeStamp value }
                    otaCurrentVersionNumber { timeStamp value }
                    otaCurrentVersionWeek { timeStamp value }
                    otaCurrentVersionYear { timeStamp value }
                    otaDownloadProgress { timeStamp value }
                    otaInstallDuration { timeStamp value }
                    otaInstallProgress { timeStamp value }
                    otaInstallReady { timeStamp value }
                    otaInstallTime { timeStamp value }
                    otaInstallType { timeStamp value }
                    otaStatus { timeStamp value }
                    petModeStatus { timeStamp value }
                    petModeTemperatureStatus { timeStamp value }
                    powerState { timeStamp value }
                    rangeThreshold { timeStamp value }
                    remoteChargingAvailable { timeStamp value }
                    seatFrontLeftHeat { timeStamp value }
                    seatFrontLeftVent { timeStamp value }
                    seatFrontRightHeat { timeStamp value }
                    seatFrontRightVent { timeStamp value }
                    seatRearLeftHeat { timeStamp value }
                    seatRearRightHeat { timeStamp value }
                    seatThirdRowLeftHeat { timeStamp value }
                    seatThirdRowRightHeat { timeStamp value }
                    serviceMode { timeStamp value }
                    steeringWheelHeat { timeStamp value }
                    timeToEndOfCharge { timeStamp value }
                    tirePressureStatusFrontLeft { timeStamp value }
                    tirePressureStatusFrontRight { timeStamp value }
                    tirePressureStatusRearLeft { timeStamp value }
                    tirePressureStatusRearRight { timeStamp value }
                    tirePressureFrontLeft { timeStamp value }
                    tirePressureFrontRight { timeStamp value }
                    tirePressureRearLeft { timeStamp value }
                    tirePressureRearRight { timeStamp value }
                    trailerStatus { timeStamp value }
                    twelveVoltBatteryHealth { timeStamp value }
                    vehicleMileage { timeStamp value }
                    windowFrontLeftCalibrated { timeStamp value }
                    windowFrontLeftClosed { timeStamp value }
                    windowFrontRightCalibrated { timeStamp value }
                    windowFrontRightClosed { timeStamp value }
                    windowRearLeftCalibrated { timeStamp value }
                    windowRearLeftClosed { timeStamp value }
                    windowRearRightCalibrated { timeStamp value }
                    windowRearRightClosed { timeStamp value }
                    windowsNextAction { timeStamp value }
                    wiperFluidState { timeStamp value }
                }
            }"
        };
        
        // Get raw JSON for storage/debugging
        using var httpRequest = CreateAuthenticatedRequest(request);
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("GetVehicleState raw response (first 2000 chars): {Response}",
            rawJson.Length > 2000 ? rawJson.Substring(0, 2000) + "..." : rawJson);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GetVehicleState failed with {StatusCode}: {Response}", response.StatusCode, rawJson);
            throw new ExternalServiceException("Rivian", rawJson, (int)response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<GetVehicleStateResponse>(rawJson);
        
        if (result?.Errors?.Any() == true)
        {
            var errorMessage = string.Join(", ", result.Errors.Select(e => e.Message));
            var errorCode = result.Errors.FirstOrDefault()?.Extensions?.Code;

            if (errorCode == "RATE_LIMIT")
            {
                _logger.LogWarning("Rate limited by Rivian API. Response: {Response}",
                    rawJson.Length > 1000 ? rawJson.Substring(0, 1000) + "..." : rawJson);
                throw new RateLimitedException("Rivian API rate limit exceeded. Please wait before retrying.");
            }

            // Log full response at error level so we can debug issues
            _logger.LogError("GetVehicleState failed with error: {Errors}. Raw response: {Response}",
                errorMessage,
                rawJson.Length > 2000 ? rawJson.Substring(0, 2000) + "..." : rawJson);
            throw new ExternalServiceException("Rivian", "Failed to retrieve vehicle state", $"Rivian API error: {errorMessage}");
        }

        return (result?.Data?.VehicleState, rawJson);
    }

    /// <summary>
    /// Get vehicle image URLs from Rivian API using getVehicleMobileImages query.
    /// Returns the image URL and the vehicle version that worked.
    /// </summary>
    /// <param name="vehicleId">The Rivian vehicle ID</param>
    /// <param name="knownVersion">If provided, try this version first before cycling through 1-5</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (imageUrl, workingVersion) or (null, null) if not found</returns>
    public async Task<(string? Url, int? Version)> GetVehicleImageUrlAsync(
        string vehicleId, int? knownVersion = null, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        _logger.LogDebug("Getting vehicle images for {VehicleId}...", vehicleId);

        // If we know which version works, try that first
        if (knownVersion.HasValue)
        {
            var imageUrl = await TryGetVehicleImageAsync(knownVersion.Value.ToString(), cancellationToken);
            if (!string.IsNullOrEmpty(imageUrl))
            {
                _logger.LogDebug("Found vehicle image with known version {Version}", knownVersion.Value);
                return (imageUrl, knownVersion.Value);
            }
        }

        // Try vehicle versions 1-5 until we find images
        for (int version = 1; version <= 5; version++)
        {
            // Skip the known version since we already tried it
            if (version == knownVersion)
                continue;

            var imageUrl = await TryGetVehicleImageAsync(version.ToString(), cancellationToken);
            if (!string.IsNullOrEmpty(imageUrl))
            {
                _logger.LogDebug("Found vehicle image with version {Version}", version);
                return (imageUrl, version);
            }
        }

        _logger.LogDebug("No vehicle images found for any version");
        return (null, null);
    }

    private async Task<string?> TryGetVehicleImageAsync(
        string vehicleVersion, CancellationToken cancellationToken)
    {
        var request = new GraphQlRequest
        {
            OperationName = "GetVehicleImages",
            Variables = new
            {
                extension = "png",
                resolution = "@3x",
                vehicleVersion = vehicleVersion
            },
            Query = @"query GetVehicleImages($extension: String!, $resolution: String!, $vehicleVersion: String!) {
                getVehicleMobileImages(
                    resolution: $resolution
                    extension: $extension
                    version: $vehicleVersion
                ) {
                    __typename
                    vehicleId
                    url
                    resolution
                    size
                    design
                    placement
                }
            }"
        };

        using var httpRequest = CreateAuthenticatedRequest(request);
        // Add the additional header that may be required
        httpRequest.Headers.TryAddWithoutValidation("dc-cid", "m-android");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug("GetVehicleMobileImages (version {Version}) response: {Response}",
            vehicleVersion, rawJson.Length > 500 ? rawJson.Substring(0, 500) + "..." : rawJson);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GetVehicleMobileImages failed with {StatusCode}", response.StatusCode);
            return null;
        }

        var result = JsonSerializer.Deserialize<GetVehicleMobileImagesResponse>(rawJson);

        if (result?.Errors?.Any() == true)
        {
            _logger.LogDebug("GetVehicleMobileImages returned errors: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Message)));
            return null;
        }

        // Find the best image: prefer "large" size with "dark" design for our dark UI
        var images = result?.Data?.GetVehicleMobileImages;
        if (images == null || !images.Any())
            return null;

        // First try: large + dark + threequarter placement
        var image = images.FirstOrDefault(i =>
            i.Size == "large" && i.Design == "dark");

        // Fallback: large + dark
        image ??= images.FirstOrDefault(i => i.Size == "large" && i.Design == "dark");

        // Fallback: any large image
        image ??= images.FirstOrDefault(i => i.Size == "large");

        // Fallback: any image
        image ??= images.FirstOrDefault();

        return image?.Url;
    }

    /// <summary>
    /// Download image from URL and return as byte array
    /// </summary>
    public async Task<(byte[]? Data, string? ContentType)> DownloadImageAsync(
        string imageUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Downloading image from {Url}...", imageUrl);

            var response = await _httpClient.GetAsync(imageUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download image: {StatusCode}", response.StatusCode);
                return (null, null);
            }

            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";

            _logger.LogDebug("Downloaded image: {Size} bytes, {ContentType}", data.Length, contentType);
            return (data, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image from {Url}", imageUrl);
            return (null, null);
        }
    }

    private async Task<T?> SendAuthenticatedRequestAsync<T>(GraphQlRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = CreateAuthenticatedRequest(request);
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        // Capture response body for error logging
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("API request failed with {StatusCode}. Response: {Response}",
                response.StatusCode, responseBody);
            throw new ExternalServiceException("Rivian", responseBody, (int)response.StatusCode);
        }

        return JsonSerializer.Deserialize<T>(responseBody);
    }
    
    private HttpRequestMessage CreateAuthenticatedRequest(GraphQlRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
        httpRequest.Headers.Add("a-sess", _appSessionToken);
        httpRequest.Headers.Add("u-sess", _userSessionToken);
        httpRequest.Headers.Add("csrf-token", _csrfToken);

        // Add Authorization header with access token
        if (!string.IsNullOrEmpty(_accessToken))
        {
            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        }

        httpRequest.Content = JsonContent.Create(request);
        return httpRequest;
    }
    
    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("Not authenticated. Call LoginAsync first.");
        }
    }
    
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
