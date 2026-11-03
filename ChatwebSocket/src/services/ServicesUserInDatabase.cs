using System.Xml;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatwebSocket;

public class ServiceCreaterUser
{
    private readonly DbContex _contex;

    public ServiceCreaterUser(DbContex contex)
    {
        _contex = contex;
    }

    public async Task<List<User>> GetAllUsers()
    {
        return await _contex.Users.ToListAsync();
    }

    public async Task<User?> GetUserById(UniqueId id)
    {
        return await _contex.Users.FirstOrDefaultAsync(x => x.Uid == id);
    }

    public async Task<IResult> CreateUserInDatabase(string nickname, string email)
    {
        var user = ServicesModelUser.CreateUser(nickname, email);
        _contex.Users.Add(user);
        await _contex.SaveChangesAsync();
        return Results.Created(user.ToString(), user);
    }

    public async Task<IResult> EditUserInDatabase(UniqueId id, string nickname, string email)
    {
        var rowsAffected = await _contex.Users
            .Where(x => x.Uid == id)
            .ExecuteUpdateAsync(x =>
            x.SetProperty(y => y.Nickname, nickname).SetProperty(y => y.Email, email));


        if (rowsAffected == 0)
        {
            return Results.NotFound();
        }

        return Results.Ok();
    }

    public async Task<IResult> DeleteUser(UniqueId id)
    {
        var rowsAffected = await _contex.Users.Where(x => x.Uid == id).ExecuteDeleteAsync();
        if (rowsAffected == 0)
        {
            return Results.NotFound();
        }

        return Results.NoContent();
    }
}
