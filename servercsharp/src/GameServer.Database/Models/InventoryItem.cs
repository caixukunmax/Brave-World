using MongoDB.Bson.Serialization.Attributes;

namespace GameServer.Database.Models;

[BsonIgnoreExtraElements]
public class InventoryItem
{
    [BsonElement("role_id")]  public long RoleId  { get; set; }
    [BsonElement("item_id")]  public int  ItemId  { get; set; }
    [BsonElement("count")]    public int  Count   { get; set; }
}
