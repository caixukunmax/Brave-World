using GameServer.Database.Models;
using MongoDB.Driver;

namespace GameServer.Database.Repositories;

public class ChestRepository
{
    private readonly IMongoCollection<BsonDocument> _col;
    private readonly IMongoCollection<GmChest> _gmCol;
    private readonly IMongoCollection<BsonDocument> _seqCol;

    public ChestRepository(MongoDbContext db)
    {
        _col = db.Chests;
        _gmCol = db.GmChests;
        _seqCol = db.EntitySeqMeta;
    }

    // ---- 开启状态 ----

    public async Task<bool> IsOpened(long roleId, long chestId)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("role_id", roleId),
            Builders<BsonDocument>.Filter.Eq("chest_id", chestId));
        return await _col.Find(filter).FirstOrDefaultAsync() != null;
    }

    public async Task MarkOpened(long roleId, long chestId)
    {
        await _col.InsertOneAsync(new BsonDocument
        {
            { "role_id", roleId },
            { "chest_id", chestId },
        });
    }

    public async Task<HashSet<long>> GetOpened(long roleId)
    {
        var docs = await _col.Find(Builders<BsonDocument>.Filter.Eq("role_id", roleId)).ToListAsync();
        return docs.Select(d => d["chest_id"].ToInt64()).ToHashSet();
    }

    // ---- GM 动态宝箱 ----

    public async Task AddGmChest(long id, int mapId, int entityType, int chestTypeId, int x, int y, string rewards)
    {
        await _gmCol.InsertOneAsync(new GmChest
        {
            Id = id,
            MapId = mapId,
            EntityType = entityType,
            ChestTypeId = chestTypeId,
            X = x,
            Y = y,
            Rewards = rewards,
        });
    }

    public async Task<List<GmChest>> GetGmChestsByMapAndType(int mapId, int entityType)
    {
        var filter = Builders<GmChest>.Filter.And(
            Builders<GmChest>.Filter.Eq(c => c.MapId, mapId),
            Builders<GmChest>.Filter.Eq(c => c.EntityType, entityType));
        return await _gmCol.Find(filter).ToListAsync();
    }

    // ---- 实体序列号 ----

    public async Task<int> AllocateEntitySeq(int mapId, int entityType, HashSet<int> configChestSeqs)
    {
        var metaId = $"{mapId}_{entityType}";
        var meta = await _seqCol.Find(Builders<BsonDocument>.Filter.Eq("_id", metaId)).FirstOrDefaultAsync();

        int lastSeq = 99;
        if (meta == null)
        {
            await _seqCol.InsertOneAsync(new BsonDocument
            {
                { "_id", metaId },
                { "map_id", mapId },
                { "entity_type", entityType },
                { "last_seq", 99 },
                { "active_count", 0 },
            });
        }
        else
        {
            lastSeq = meta.Contains("last_seq") ? meta["last_seq"].ToInt32() : 99;
        }

        // 收集已占用 seq
        var gmDocs = await _gmCol.Find(Builders<GmChest>.Filter.And(
            Builders<GmChest>.Filter.Eq(c => c.MapId, mapId),
            Builders<GmChest>.Filter.Eq(c => c.EntityType, entityType))).ToListAsync();
        var occupied = new HashSet<int>(configChestSeqs);
        foreach (var doc in gmDocs)
            occupied.Add((int)(doc.Id % 10000));

        int startSeq = Math.Max(100, (lastSeq + 1) % 10000);
        if (startSeq < 100) startSeq = 100;
        int seq = startSeq;
        while (occupied.Contains(seq))
        {
            seq++;
            if (seq > 9999) seq = 100;
            if (seq == startSeq)
                throw new InvalidOperationException($"Entity seq overflow: map={mapId} type={entityType}");
        }

        await _seqCol.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", metaId),
            Builders<BsonDocument>.Update
                .Set("last_seq", seq)
                .Inc("active_count", 1),
            new UpdateOptions { IsUpsert = true });

        return seq;
    }

    public async Task CleanupOldGmChests()
    {
        try
        {
            // 清理旧格式 gm_chests (id >= 20000 且无 entity_type 字段)
            // 这里仍用 BsonDocument filter 因为需要 Exists 检查
            var filter = Builders<GmChest>.Filter.And(
                Builders<GmChest>.Filter.Gte(c => c.Id, 20000),
                Builders<GmChest>.Filter.Eq(c => c.EntityType, 0));
            await _gmCol.DeleteManyAsync(filter);
        }
        catch { /* ignore */ }
    }
}
