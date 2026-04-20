using MongoDB.Bson.Serialization.Attributes;

namespace GameServer.Database.Models;

[BsonIgnoreExtraElements]
public class GmChest
{
    [BsonElement("id")]             public long   Id           { get; set; }
    [BsonElement("map_id")]         public int    MapId        { get; set; }
    [BsonElement("entity_type")]    public int    EntityType   { get; set; }
    [BsonElement("chest_type_id")]  public int    ChestTypeId  { get; set; }
    [BsonElement("x")]              public int    X            { get; set; }
    [BsonElement("y")]              public int    Y            { get; set; }
    [BsonElement("rewards")]        public string Rewards      { get; set; } = "";
}
