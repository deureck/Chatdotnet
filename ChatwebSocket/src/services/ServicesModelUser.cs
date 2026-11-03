using System.Xml;

namespace ChatwebSocket;

public static class ServicesModelUser
{
    public static User CreateUser(string nickname, string email)
    {
        return new User(new UniqueId(), nickname, email);
    }



    public static User UpdateUser(User user, string nickname, string email)
    {
        user.Nickname = nickname;
        user.Email = email;
        return user;
    }


}
