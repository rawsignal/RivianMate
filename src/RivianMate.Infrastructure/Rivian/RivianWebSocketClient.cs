using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RivianMate.Infrastructure.Rivian.Models;

// RivianVehicleState is defined in RivianApiModels.cs

namespace RivianMate.Infrastructure.Rivian;

/// <summary>
/// WebSocket client for real-time vehicle state subscriptions from Rivian.
/// Implements the GraphQL over WebSocket protocol used by Rivian's API.
/// </summary>
public class RivianWebSocketClient : IAsyncDisposable
{
    private const string WebSocketUrl = "wss://api.rivian.com/gql-consumer-subscriptions/graphql";
    private const string SubProtocol = "graphql-transport-ws";

    private readonly ILogger<RivianWebSocketClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    // Authentication tokens
    private string? _userSessionToken;
    private string? _accessToken;
    private string _clientId = Guid.NewGuid().ToString();

    // Subscription tracking
    private readonly ConcurrentDictionary<string, SubscriptionInfo> _subscriptions = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _connectionAcknowledged;

    /// <summary>
    /// Event raised when vehicle state data is received.
    /// Parameters: vehicleId, vehicle state data, raw JSON
    /// </summary>
    public event Func<string, RivianVehicleState, string, Task>? OnVehicleStateUpdate;

    /// <summary>
    /// Event raised when an error occurs.
    /// </summary>
    public event Func<Exception, Task>? OnError;

    /// <summary>
    /// Event raised when the connection is lost.
    /// </summary>
    public event Func<Task>? OnDisconnected;

    /// <summary>
    /// Event raised when the connection is established.
    /// </summary>
    public event Func<Task>? OnConnected;

    /// <summary>
    /// Whether the WebSocket is currently connected.
    /// </summary>
    public bool IsConnected => _webSocket?.State == WebSocketState.Open && _connectionAcknowledged;

    public RivianWebSocketClient(ILogger<RivianWebSocketClient> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Set authentication tokens for the connection.
    /// </summary>
    public void SetTokens(string userSessionToken, string accessToken)
    {
        _userSessionToken = userSessionToken;
        _accessToken = accessToken;
    }

    /// <summary>
    /// Connect to the Rivian WebSocket server.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                _logger.LogDebug("Already connected to WebSocket");
                return;
            }

            if (string.IsNullOrEmpty(_userSessionToken))
            {
                throw new InvalidOperationException("User session token not set. Call SetTokens first.");
            }

            _logger.LogInformation("Connecting to Rivian WebSocket...");

            // Clean up any existing connection
            await CleanupConnectionAsync();

            _webSocket = new ClientWebSocket();
            _webSocket.Options.AddSubProtocol(SubProtocol);

            await _webSocket.ConnectAsync(new Uri(WebSocketUrl), cancellationToken);

            _logger.LogDebug("WebSocket connected, sending connection_init...");

            // Send connection_init
            await SendConnectionInitAsync(cancellationToken);

            // Start receive loop
            _receiveCts = new CancellationTokenSource();
            _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

            // Wait for connection acknowledgment
            var timeout = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            while (!_connectionAcknowledged && !timeout.IsCompleted)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (!_connectionAcknowledged)
            {
                throw new TimeoutException("Timed out waiting for connection acknowledgment");
            }

            _logger.LogInformation("Connected to Rivian WebSocket successfully");

            if (OnConnected != null)
            {
                await OnConnected();
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Subscribe to vehicle state updates for a specific vehicle.
    /// </summary>
    public async Task SubscribeToVehicleAsync(
        string vehicleId,
        IEnumerable<string> properties,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
        }

        var subscriptionId = Guid.NewGuid().ToString();

        _logger.LogInformation("Subscribing to vehicle {VehicleId} updates (subscription {SubscriptionId})",
            vehicleId, subscriptionId);

        var query = BuildSubscriptionQuery(properties);
        // Must use exact casing: VehicleState for operation, vehicleID for variable
        var payload = new Dictionary<string, object?>
        {
            ["id"] = subscriptionId,
            ["type"] = "subscribe",
            ["payload"] = new Dictionary<string, object?>
            {
                ["operationName"] = "VehicleState",
                ["query"] = query,
                ["variables"] = new Dictionary<string, string> { ["vehicleID"] = vehicleId }
            }
        };

        await SendMessageAsync(payload, cancellationToken);

        _subscriptions[subscriptionId] = new SubscriptionInfo
        {
            VehicleId = vehicleId,
            Properties = properties.ToList()
        };

        _logger.LogDebug("Subscription {SubscriptionId} registered for vehicle {VehicleId}",
            subscriptionId, vehicleId);
    }

    /// <summary>
    /// Unsubscribe from vehicle state updates.
    /// </summary>
    public async Task UnsubscribeFromVehicleAsync(string vehicleId, CancellationToken cancellationToken = default)
    {
        var subscriptionsToRemove = _subscriptions
            .Where(kvp => kvp.Value.VehicleId == vehicleId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var subscriptionId in subscriptionsToRemove)
        {
            _logger.LogInformation("Unsubscribing from vehicle {VehicleId} (subscription {SubscriptionId})",
                vehicleId, subscriptionId);

            if (IsConnected)
            {
                var payload = new Dictionary<string, object?>
                {
                    ["id"] = subscriptionId,
                    ["type"] = "complete"
                };

                try
                {
                    await SendMessageAsync(payload, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error sending unsubscribe message for {SubscriptionId}", subscriptionId);
                }
            }

            _subscriptions.TryRemove(subscriptionId, out _);
        }
    }

    /// <summary>
    /// Disconnect from the WebSocket server.
    /// </summary>
    public async Task DisconnectAsync()
    {
        _logger.LogInformation("Disconnecting from Rivian WebSocket...");
        await CleanupConnectionAsync();
    }

    /// <summary>
    /// Resubscribe all active subscriptions (used after reconnection).
    /// </summary>
    public async Task ResubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        var subscriptionsCopy = _subscriptions.ToList();
        _subscriptions.Clear();

        foreach (var (_, info) in subscriptionsCopy)
        {
            await SubscribeToVehicleAsync(info.VehicleId, info.Properties, cancellationToken);
        }
    }

    private async Task SendConnectionInitAsync(CancellationToken cancellationToken)
    {
        // Payload format must match exactly what the Rivian Python client uses
        // Keys use hyphens and specific naming conventions
        var payload = new Dictionary<string, object?>
        {
            ["type"] = "connection_init",
            ["payload"] = new Dictionary<string, string?>
            {
                ["client-name"] = "com.rivian.ios.consumer-apollo-ios",
                ["client-version"] = "1.13.0-1494",
                ["dc-cid"] = $"m-ios-{_clientId}",
                ["u-sess"] = _userSessionToken
            }
        };

        await SendMessageAsync(payload, cancellationToken);
    }

    private async Task SendMessageAsync(object message, CancellationToken cancellationToken)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }

        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        // Log at Information level for connection_init to help debug issues
        var truncatedJson = json.Length > 500 ? json[..500] + "..." : json;
        if (json.Contains("connection_init"))
        {
            _logger.LogInformation("Sending WebSocket connection_init: {Message}", truncatedJson);
        }
        else
        {
            _logger.LogDebug("Sending WebSocket message: {Message}", truncatedJson);
        }

        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                messageBuilder.Clear();

                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("WebSocket received close message: {Status} {Description}",
                            result.CloseStatus, result.CloseStatusDescription);
                        return;
                    }

                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                var message = messageBuilder.ToString();

                if (!string.IsNullOrEmpty(message))
                {
                    await ProcessMessageAsync(message);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Receive loop cancelled");
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "WebSocket error in receive loop");
            if (OnError != null)
            {
                await OnError(ex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in receive loop");
            if (OnError != null)
            {
                await OnError(ex);
            }
        }
        finally
        {
            _connectionAcknowledged = false;
            if (OnDisconnected != null)
            {
                await OnDisconnected();
            }
        }
    }

    private async Task ProcessMessageAsync(string message)
    {
        // Log received messages at Info level for debugging connection issues
        var truncatedMessage = message.Length > 500 ? message[..500] + "..." : message;
        _logger.LogInformation("Received WebSocket message: {Message}", truncatedMessage);

        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "connection_ack":
                    _connectionAcknowledged = true;
                    _logger.LogInformation("Connection acknowledged by server");
                    break;

                case "next":
                    await HandleNextMessageAsync(root);
                    break;

                case "error":
                    HandleErrorMessage(root);
                    break;

                case "complete":
                    HandleCompleteMessage(root);
                    break;

                case "ping":
                    await SendPongAsync();
                    break;

                case "ka": // keep-alive
                    _logger.LogDebug("Received keep-alive");
                    break;

                default:
                    _logger.LogDebug("Received unknown message type: {Type}", type);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse WebSocket message: {Message}", message);
        }
    }

    private async Task HandleNextMessageAsync(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var idElement))
        {
            _logger.LogWarning("Received 'next' message without subscription ID");
            return;
        }

        var subscriptionId = idElement.GetString();
        if (string.IsNullOrEmpty(subscriptionId) || !_subscriptions.TryGetValue(subscriptionId, out var subscriptionInfo))
        {
            _logger.LogWarning("Received data for unknown subscription: {SubscriptionId}", subscriptionId);
            return;
        }

        if (!root.TryGetProperty("payload", out var payloadElement) ||
            !payloadElement.TryGetProperty("data", out var dataElement) ||
            !dataElement.TryGetProperty("vehicleState", out var vehicleStateElement))
        {
            _logger.LogWarning("Received 'next' message with unexpected structure");
            return;
        }

        try
        {
            var rawJson = vehicleStateElement.GetRawText();
            var vehicleState = JsonSerializer.Deserialize<RivianVehicleState>(
                rawJson,
                _jsonOptions);

            if (vehicleState != null && OnVehicleStateUpdate != null)
            {
                _logger.LogDebug("Received vehicle state update for {VehicleId}", subscriptionInfo.VehicleId);
                await OnVehicleStateUpdate(subscriptionInfo.VehicleId, vehicleState, rawJson);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize vehicle state");
        }
    }

    private void HandleErrorMessage(JsonElement root)
    {
        if (root.TryGetProperty("payload", out var payloadElement))
        {
            _logger.LogError("WebSocket error: {Payload}", payloadElement.GetRawText());
        }
        else
        {
            _logger.LogError("WebSocket error with no payload");
        }
    }

    private void HandleCompleteMessage(JsonElement root)
    {
        if (root.TryGetProperty("id", out var idElement))
        {
            var subscriptionId = idElement.GetString();
            if (!string.IsNullOrEmpty(subscriptionId))
            {
                _subscriptions.TryRemove(subscriptionId, out _);
                _logger.LogInformation("Subscription {SubscriptionId} completed", subscriptionId);
            }
        }
    }

    private async Task SendPongAsync()
    {
        if (IsConnected)
        {
            await SendMessageAsync(new { type = "pong" }, CancellationToken.None);
        }
    }

    private static string BuildSubscriptionQuery(IEnumerable<string> properties)
    {
        var fields = string.Join("\n                    ", properties.Select(p => $"{p} {{ timeStamp value }}"));

        // Must use VehicleState (capital V and S) and $vehicleID (capital ID)
        return $@"subscription VehicleState($vehicleID: String!) {{
                vehicleState(id: $vehicleID) {{
                    __typename
                    gnssLocation {{ latitude longitude timeStamp }}
                    {fields}
                }}
            }}";
    }

    private async Task CleanupConnectionAsync()
    {
        _connectionAcknowledged = false;

        if (_receiveCts != null)
        {
            await _receiveCts.CancelAsync();
            _receiveCts.Dispose();
            _receiveCts = null;
        }

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Receive task did not complete in time");
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            _receiveTask = null;
        }

        if (_webSocket != null)
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client disconnecting",
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error closing WebSocket gracefully");
                }
            }

            _webSocket.Dispose();
            _webSocket = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private class SubscriptionInfo
    {
        public required string VehicleId { get; init; }
        public required List<string> Properties { get; init; }
    }

    private class WebSocketMessage
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("payload")]
        public object? Payload { get; set; }
    }
}
