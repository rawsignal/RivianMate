namespace RivianMate.Api.Services;

/// <summary>
/// Service for notifying UI components when vehicle state changes.
/// Used to push real-time updates from WebSocket to Blazor components.
/// </summary>
public class VehicleStateNotifier
{
    /// <summary>
    /// Event raised when any vehicle's state is updated.
    /// Parameters: vehicleId (internal ID)
    /// </summary>
    public event Func<int, Task>? OnVehicleStateChanged;

    /// <summary>
    /// Event raised when a new vehicle is added or vehicle list changes.
    /// </summary>
    public event Func<Task>? OnVehiclesChanged;

    /// <summary>
    /// Notify subscribers that a vehicle's state has been updated.
    /// </summary>
    public async Task NotifyStateChangedAsync(int vehicleId)
    {
        if (OnVehicleStateChanged != null)
        {
            await OnVehicleStateChanged.Invoke(vehicleId);
        }
    }

    /// <summary>
    /// Notify subscribers that the vehicle list has changed.
    /// </summary>
    public async Task NotifyVehiclesChangedAsync()
    {
        if (OnVehiclesChanged != null)
        {
            await OnVehiclesChanged.Invoke();
        }
    }
}
