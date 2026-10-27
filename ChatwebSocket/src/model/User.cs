using System.Xml;

class User
{
    public UniqueId Uid { get; set; }
    public string Nickname { get; set; }
    public string Email { get; set; }

    public User(UniqueId uid, string nickname, string email)
    {
        this.Uid = uid;
        this.Nickname = nickname;
        this.Email = email;
    }
}