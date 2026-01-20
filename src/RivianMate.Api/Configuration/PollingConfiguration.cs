using RivianMate.Core.Enums;

namespace RivianMate.Api.Configuration;

/// <summary>
/// Configuration for vehicle state polling/subscription behavior.
/// </summary>
public class PollingConfiguration
{
    public const string SectionName = "Polling";

    /// <summary>
    /// The mode to use for fetching vehicle state updates.
    /// GraphQL = traditional polling, WebSocket = real-time subscriptions.
    /// </summary>
    public PollingMode Mode { get; set; } = PollingMode.GraphQL;

    /// <summary>
    /// Polling interval in seconds when vehicle is awake (GraphQL mode only).
    /// </summary>
    public int IntervalAwakeSeconds { get; set; } = 15;

    /// <summary>
    /// Polling interval in seconds when vehicle is asleep (GraphQL mode only).
    /// </summary>
    public int IntervalAsleepSeconds { get; set; } = 60;

    /// <summary>
    /// Initial delay before attempting to reconnect WebSocket (WebSocket mode only).
    /// </summary>
    public int WebSocketReconnectDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Maximum delay between WebSocket reconnection attempts (WebSocket mode only).
    /// Uses exponential backoff up to this value.
    /// </summary>
    public int WebSocketMaxReconnectDelaySeconds { get; set; } = 300;

    /// <summary>
    /// Number of consecutive errors before giving up on a WebSocket subscription.
    /// </summary>
    public int WebSocketMaxConsecutiveErrors { get; set; } = 5;
}
