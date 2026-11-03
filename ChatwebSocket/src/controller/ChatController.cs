using System.Xml;
using Microsoft.AspNetCore.Mvc;

namespace ChatwebSocket;

[ApiController]
public class ChatController : ControllerBase
{
    private readonly ChatWebSocketManager _wsManager;
    private readonly ServiceCreaterUser _userService;
    private readonly ServicesMessager _messageService;

    public ChatController(
        ChatWebSocketManager wsManager,
        ServiceCreaterUser userService,
        ServicesMessager messageService)
    {
        _wsManager = wsManager;
        _userService = userService;
        _messageService = messageService;
    }

    [HttpGet("/ws")]
    [HttpGet("/ws/chat")]
    public async Task ConnectWebSocket(
        [FromQuery] string? userId = null,
        [FromQuery] string? nickname = null,
        CancellationToken cancellationToken = default)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("Uma requisição WebSocket é esperada neste endpoint.", cancellationToken);
            return;
        }

        UniqueId resolvedUserId = new UniqueId();
        string resolvedNickname = string.IsNullOrWhiteSpace(nickname) ? "Anônimo" : nickname.Trim();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                resolvedUserId = ChatWebSocketManager.ParseUniqueId(userId);
                var existingUser = await _userService.GetUserById(resolvedUserId);
                if (existingUser is not null && string.IsNullOrWhiteSpace(nickname))
                {
                    resolvedNickname = existingUser.Nickname;
                }
            }
            catch
            {
                resolvedUserId = new UniqueId();
            }
        }

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await _wsManager.HandleConnectionAsync(webSocket, resolvedUserId, resolvedNickname, cancellationToken);
    }

    [HttpGet("/api/messages")]
    public async Task<IResult> GetMessages([FromQuery] string? userId = null)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                var parsedId = ChatWebSocketManager.ParseUniqueId(userId);
                var userMessages = await _messageService.GetMessagesByUserId(parsedId);
                return Results.Ok(userMessages);
            }
            catch
            {
                return Results.BadRequest(new { error = "Formato de userId inválido." });
            }
        }

        var messages = await _messageService.GetAllMessages();
        return Results.Ok(messages);
    }

    [HttpGet("/api/chat/online")]
    public IResult GetOnlineClients()
    {
        var clients = _wsManager.GetConnectedClients()
            .Select(c => new
            {
                c.ConnectionId,
                UserId = c.UserId.ToString(),
                c.Nickname
            });
        return Results.Ok(clients);
    }
}
