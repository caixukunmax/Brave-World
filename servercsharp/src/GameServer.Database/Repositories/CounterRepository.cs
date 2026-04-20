using MongoDB.Driver;

namespace GameServer.Database.Repositories;

public class CounterRepository
{
    private readonly IMongoCollection<BsonDocument> _col;

    public CounterRepository(MongoDbContext db)
    {
        _col = db.Counters;
    }

    public async Task<long> GetNextId(string counterName)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", counterName);
        await _col.FindOneAndUpdateAsync(filter,
            Builders<BsonDocument>.Update.Inc("seq", 1),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true });

        var doc = await _col.Find(filter).FirstOrDefaultAsync();
        if (doc == null) return 1000;
        var seq = doc["seq"].ToInt64();
        if (seq < 1000)
        {
            await _col.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set("seq", 1000));
            return 1000;
        }
        return seq;
    }
}
