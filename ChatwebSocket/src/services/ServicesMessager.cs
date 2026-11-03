using System.Xml;
using Microsoft.EntityFrameworkCore;

namespace ChatwebSocket;

public class ServicesMessager
{
    private readonly DbContex _contex;

    public ServicesMessager(DbContex contex)
    {
        _contex = contex;
    }

    public static Messager CreateModelMessage(UniqueId userId, string body)
    {
        return new Messager(new UniqueId(), userId, body);
    }

    public async Task<Messager> SaveMessageInDatabase(UniqueId userId, string body)
    {
        var message = CreateModelMessage(userId, body);
        _contex.Messages.Add(message);
        await _contex.SaveChangesAsync();
        return message;
    }

    public async Task<List<Messager>> GetAllMessages()
    {
        return await _contex.Messages.ToListAsync();
    }

    public async Task<List<Messager>> GetMessagesByUserId(UniqueId userId)
    {
        return await _contex.Messages.Where(m => m.UserId == userId).ToListAsync();
    }
}
