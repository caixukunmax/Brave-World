using MongoDB.Bson.Serialization.Attributes;

namespace GameServer.Database.Models;

[BsonIgnoreExtraElements]
public class Account
{
    [BsonElement("account_id")]      public long   AccountId     { get; set; }
    [BsonElement("username")]        public string Username      { get; set; } = "";
    [BsonElement("password")]        public string Password      { get; set; } = "";
    [BsonElement("status")]          public int    Status        { get; set; }
    [BsonElement("last_server_id")]  public long   LastServerId  { get; set; }
    [BsonElement("last_role_name")]  public string LastRoleName  { get; set; } = "";
    [BsonElement("created_at")]      public int    CreatedAt     { get; set; }
}
