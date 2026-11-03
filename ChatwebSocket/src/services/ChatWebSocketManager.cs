using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace ChatwebSocket;

public record ConnectedClient(string ConnectionId, UniqueId UserId, string Nickname, WebSocket Socket);

public record ChatEventPayload(
    string Type,
    string MessageId,
    string UserId,
    string Nickname,
    string Body,
    DateTimeOffset Timestamp);

public record IncomingChatMessage(string? Body, string? UserId, string? Nickname);

public class ChatWebSocketManager
{
    private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public ChatWebSocketManager(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyCollection<ConnectedClient> GetConnectedClients() => _clients.Values.ToList();

    public async Task HandleConnectionAsync(WebSocket webSocket, UniqueId userId, string nickname, CancellationToken cancellationToken = default)
    {
        var connectionId = Guid.NewGuid().ToString("N");
        var client = new ConnectedClient(connectionId, userId, nickname, webSocket);
        _clients.TryAdd(connectionId, client);

        await BroadcastEventAsync(new ChatEventPayload(
            Type: "system",
            MessageId: new UniqueId().ToString(),
            UserId: userId.ToString(),
            Nickname: nickname,
            Body: $"{nickname} entrou no chat.",
            Timestamp: DateTimeOffset.UtcNow), cancellationToken);

        var buffer = new byte[4096];

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var rawText = Encoding.UTF8.GetString(ms.ToArray()).Trim();
                    if (string.IsNullOrEmpty(rawText))
                    {
                        continue;
                    }

                    var (msgUserId, msgNickname, body) = ParseIncomingMessage(rawText, client);
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        continue;
                    }

                    Messager savedMessage;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var messageService = scope.ServiceProvider.GetRequiredService<ServicesMessager>();
                        savedMessage = await messageService.SaveMessageInDatabase(msgUserId, body);
                    }

                    var broadcastPayload = new ChatEventPayload(
                        Type: "message",
                        MessageId: savedMessage.Uid.ToString(),
                        UserId: savedMessage.UserId.ToString(),
                        Nickname: msgNickname,
                        Body: savedMessage.Body,
                        Timestamp: DateTimeOffset.UtcNow);

                    await BroadcastEventAsync(broadcastPayload, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (WebSocketException)
        {
            // Client disconnected abruptly
        }
        finally
        {
            _clients.TryRemove(connectionId, out _);

            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Conexão encerrada",
                        CancellationToken.None);
                }
                catch
                {
                    // Ignore errors when closing an already faulted socket
                }
            }

            await BroadcastEventAsync(new ChatEventPayload(
                Type: "system",
                MessageId: new UniqueId().ToString(),
                UserId: userId.ToString(),
                Nickname: nickname,
                Body: $"{nickname} saiu do chat.",
                Timestamp: DateTimeOffset.UtcNow), CancellationToken.None);
        }
    }

    public async Task BroadcastEventAsync(ChatEventPayload payload, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        var sendTasks = new List<Task>();
        foreach (var (id, client) in _clients)
        {
            if (client.Socket.State == WebSocketState.Open)
            {
                sendTasks.Add(SendSafeAsync(id, client.Socket, segment, cancellationToken));
            }
            else
            {
                _clients.TryRemove(id, out _);
            }
        }

        await Task.WhenAll(sendTasks);
    }

    private async Task SendSafeAsync(string connectionId, WebSocket socket, ArraySegment<byte> data, CancellationToken cancellationToken)
    {
        try
        {
            await socket.SendAsync(data, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        catch
        {
            _clients.TryRemove(connectionId, out _);
        }
    }

    private static (UniqueId UserId, string Nickname, string Body) ParseIncomingMessage(string rawText, ConnectedClient client)
    {
        if (rawText.StartsWith('{') && rawText.EndsWith('}'))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<IncomingChatMessage>(rawText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Body))
                {
                    var uid = client.UserId;
                    if (!string.IsNullOrWhiteSpace(parsed.UserId))
                    {
                        try
                        {
                            uid = ParseUniqueId(parsed.UserId);
                        }
                        catch
                        {
                            uid = client.UserId;
                        }
                    }

                    var nick = !string.IsNullOrWhiteSpace(parsed.Nickname) ? parsed.Nickname : client.Nickname;
                    return (uid, nick, parsed.Body);
                }
            }
            catch
            {
                // Fallback to treating rawText as plain message body
            }
        }

        return (client.UserId, client.Nickname, rawText);
    }

    public static UniqueId ParseUniqueId(string rawId)
    {
        if (rawId.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase))
        {
            return new UniqueId(rawId);
        }

        if (Guid.TryParse(rawId, out var guid))
        {
            return new UniqueId(guid);
        }

        return new UniqueId(rawId);
    }
}
