using System.Xml;

namespace ChatwebSocket;

public class User
{
    public UniqueId Uid { get; set; }
    public string Nickname { get; set; }
    public string Email { get; set; }

    public User(UniqueId uid, string nickname, string email)
    {
        Uid = uid;
        Nickname = nickname;
        Email = email;
    }
}