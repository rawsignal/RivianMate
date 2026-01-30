using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using RivianMate.Core.Entities;

namespace RivianMate.Api.Services;

/// <summary>
/// Centralized vehicle selection logic: resolves URL param > localStorage > first vehicle.
/// Eliminates duplicate code across all detail pages.
/// </summary>
public class VehicleSelectionService
{
    private readonly VehicleService _vehicleService;

    private const string SelectedVehicleKey = "rivianmate_selectedVehicleId";

    public VehicleSelectionService(VehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    public record VehicleSelectionResult(
        Vehicle? Vehicle,
        bool NoVehicles,
        bool NotFound);

    /// <summary>
    /// Extract the current user ID from the authentication state.
    /// Returns Guid.Empty if the user is not authenticated.
    /// </summary>
    public static async Task<Guid> GetUserIdAsync(AuthenticationStateProvider authStateProvider)
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Resolve the vehicle to display: URL param > localStorage > first vehicle.
    /// </summary>
    public async Task<VehicleSelectionResult> ResolveVehicleAsync(
        Guid userId,
        Guid? urlVehicleId,
        IJSRuntime js,
        CancellationToken ct = default)
    {
        var vehicles = await _vehicleService.GetVehiclesForUserAsync(userId, ct);

        if (vehicles.Count == 0)
            return new(null, NoVehicles: true, NotFound: false);

        if (urlVehicleId.HasValue)
        {
            var vehicle = await _vehicleService.GetVehicleByPublicIdAsync(urlVehicleId.Value, userId, ct);
            return vehicle == null
                ? new(null, NoVehicles: false, NotFound: true)
                : new(vehicle, NoVehicles: false, NotFound: false);
        }

        // Try localStorage, fallback to first vehicle
        var saved = await GetLastSelectedVehicleAsync(vehicles, js);
        saved ??= vehicles.FirstOrDefault();
        return new(saved, NoVehicles: false, NotFound: false);
    }

    /// <summary>
    /// Save the selected vehicle ID to localStorage.
    /// </summary>
    public static async Task SaveSelectedVehicleAsync(int vehicleId, IJSRuntime js)
    {
        try
        {
            await js.InvokeVoidAsync("rivianMate.storage.set", SelectedVehicleKey, vehicleId.ToString());
        }
        catch
        {
            // JS interop can fail during prerendering
        }
    }

    private static async Task<Vehicle?> GetLastSelectedVehicleAsync(List<Vehicle> vehicles, IJSRuntime js)
    {
        try
        {
            var savedIdStr = await js.InvokeAsync<string?>("rivianMate.storage.get", SelectedVehicleKey);
            if (int.TryParse(savedIdStr, out var savedId))
            {
                return vehicles.FirstOrDefault(v => v.Id == savedId);
            }
        }
        catch
        {
            // JS interop can fail during prerendering
        }
        return null;
    }
}
