using GameServer.Database.Models;
using MongoDB.Driver;

namespace GameServer.Database;

public class MongoDbContext
{
    public IMongoCollection<BsonDocument> Players { get; }
    public IMongoCollection<Account> Accounts { get; }
    public IMongoCollection<Role> Roles { get; }
    public IMongoCollection<ServerInfo> Servers { get; }
    public IMongoCollection<BsonDocument> Counters { get; }
    public IMongoCollection<InventoryItem> Inventories { get; }
    public IMongoCollection<BsonDocument> Chests { get; }
    public IMongoCollection<GmChest> GmChests { get; }
    public IMongoCollection<BsonDocument> EntitySeqMeta { get; }

    public MongoDbContext(string host, int port, string database = "tslua2")
    {
        var client = new MongoClient($"mongodb://{host}:{port}");
        var db = client.GetDatabase(database);

        Players = db.GetCollection<BsonDocument>("players");
        Accounts = db.GetCollection<Account>("accounts");
        Roles = db.GetCollection<Role>("roles");
        Servers = db.GetCollection<ServerInfo>("servers");
        Counters = db.GetCollection<BsonDocument>("counters");
        Inventories = db.GetCollection<InventoryItem>("inventories");
        Chests = db.GetCollection<BsonDocument>("chests");
        GmChests = db.GetCollection<GmChest>("gm_chests");
        EntitySeqMeta = db.GetCollection<BsonDocument>("entity_seq_meta");
    }
}
