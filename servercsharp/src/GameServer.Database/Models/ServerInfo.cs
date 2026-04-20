using MongoDB.Bson.Serialization.Attributes;

namespace GameServer.Database.Models;

[BsonIgnoreExtraElements]
public class ServerInfo
{
    [BsonElement("server_id")]       public int    ServerId      { get; set; }
    [BsonElement("server_name")]     public string ServerName    { get; set; } = "";
    [BsonElement("host")]            public string Host          { get; set; } = "";
    [BsonElement("port")]            public long   Port          { get; set; }
    [BsonElement("status")]          public int    Status        { get; set; }
    [BsonElement("online_count")]    public long   OnlineCount   { get; set; }
    [BsonElement("is_new")]          public bool   IsNew         { get; set; }
    [BsonElement("is_recommend")]    public bool   IsRecommend   { get; set; }
    [BsonElement("created_at")]      public int    CreatedAt     { get; set; }
}
