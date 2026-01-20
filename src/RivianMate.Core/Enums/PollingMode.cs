namespace RivianMate.Core.Enums;

/// <summary>
/// Defines the mode used for fetching vehicle state updates from Rivian.
/// </summary>
public enum PollingMode
{
    /// <summary>
    /// Traditional polling using GraphQL queries at regular intervals.
    /// More reliable but may have 2-hour token expiration issues.
    /// </summary>
    GraphQL,

    /// <summary>
    /// Real-time updates via WebSocket subscriptions.
    /// Maintains persistent connection that keeps session alive.
    /// </summary>
    WebSocket
}
