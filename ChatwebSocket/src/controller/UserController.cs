using Microsoft.AspNetCore.Mvc;

namespace ChatwebSocket;

public record UserRequestDto(string Nickname, string Email);

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly ServiceCreaterUser _userService;

    public UserController(ServiceCreaterUser userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IResult> GetAll()
    {
        var users = await _userService.GetAllUsers();
        return Results.Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IResult> GetById(string id)
    {
        try
        {
            var uniqueId = ChatWebSocketManager.ParseUniqueId(id);
            var user = await _userService.GetUserById(uniqueId);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        }
        catch
        {
            return Results.BadRequest(new { error = "ID inválido." });
        }
    }

    [HttpPost]
    public async Task<IResult> Create([FromBody] UserRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Nickname) || string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new { error = "Nickname e Email são obrigatórios." });
        }

        return await _userService.CreateUserInDatabase(request.Nickname, request.Email);
    }

    [HttpPut("{id}")]
    public async Task<IResult> Update(string id, [FromBody] UserRequestDto request)
    {
        try
        {
            var uniqueId = ChatWebSocketManager.ParseUniqueId(id);
            return await _userService.EditUserInDatabase(uniqueId, request.Nickname, request.Email);
        }
        catch
        {
            return Results.BadRequest(new { error = "ID inválido." });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IResult> Delete(string id)
    {
        try
        {
            var uniqueId = ChatWebSocketManager.ParseUniqueId(id);
            return await _userService.DeleteUser(uniqueId);
        }
        catch
        {
            return Results.BadRequest(new { error = "ID inválido." });
        }
    }
}
