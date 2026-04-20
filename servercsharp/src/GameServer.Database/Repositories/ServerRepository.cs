using GameServer.Database.Models;
using MongoDB.Driver;

namespace GameServer.Database.Repositories;

public class ServerRepository
{
    private readonly IMongoCollection<ServerInfo> _col;

    public ServerRepository(MongoDbContext db)
    {
        _col = db.Servers;
    }

    public async Task<List<ServerInfo>> GetAll()
    {
        return await _col.Find(Builders<ServerInfo>.Filter.Empty).ToListAsync();
    }

    public async Task<ServerInfo?> FindById(int serverId)
    {
        return await _col.Find(Builders<ServerInfo>.Filter.Eq(s => s.ServerId, serverId)).FirstOrDefaultAsync();
    }

    public async Task UpdateOnlineCount(int serverId, int count)
    {
        var filter = Builders<ServerInfo>.Filter.Eq(s => s.ServerId, serverId);
        await _col.UpdateOneAsync(filter, Builders<ServerInfo>.Update.Set(s => s.OnlineCount, count));
    }

    public async Task Seed(List<ServerInfo> servers)
    {
        var existing = await _col.CountDocumentsAsync(Builders<ServerInfo>.Filter.Empty);
        if (existing > 0) return;
        if (servers.Count > 0)
            await _col.InsertManyAsync(servers);
    }
}
