using System.Xml;




class Messager
{

    public UniqueId Uid { get; set; }
    public UniqueId UserId { get; set; }
    public string Body { get; set; }

    public Messager(UniqueId uid, UniqueId userId, string body)
    {
        this.Uid = uid;
        this.UserId = userId;
        this.Body = body;
    }

}