using GameServer.Database.Models;
using MongoDB.Driver;

namespace GameServer.Database.Repositories;

public class RoleRepository
{
    private readonly IMongoCollection<Role> _col;
    private readonly CounterRepository _counters;

    public RoleRepository(MongoDbContext db, CounterRepository counters)
    {
        _col = db.Roles;
        _counters = counters;
    }

    public async Task<List<Role>> FindByAccountAndServer(long accountId, int serverId)
    {
        var filter = Builders<Role>.Filter.And(
            Builders<Role>.Filter.Eq(r => r.AccountId, accountId),
            Builders<Role>.Filter.Eq(r => r.ServerId, serverId));
        return await _col.Find(filter).ToListAsync();
    }

    public async Task<List<Role>> FindAllByAccount(long accountId)
    {
        return await _col.Find(Builders<Role>.Filter.Eq(r => r.AccountId, accountId)).ToListAsync();
    }

    public async Task<Role?> FindById(long roleId)
    {
        return await _col.Find(Builders<Role>.Filter.Eq(r => r.RoleId, roleId)).FirstOrDefaultAsync();
    }

    public async Task<Role> Create(Role roleData)
    {
        await _col.InsertOneAsync(roleData);
        return roleData;
    }

    public async Task Update(long roleId, Func<UpdateDefinitionBuilder<Role>, UpdateDefinition<Role>> buildUpdate)
    {
        var filter = Builders<Role>.Filter.Eq(r => r.RoleId, roleId);
        var update = buildUpdate(Builders<Role>.Update);
        await _col.UpdateOneAsync(filter, update);
    }

    public async Task UpdateUIPanelPos(long roleId, int posX, int posY, int width, int height)
    {
        var filter = Builders<Role>.Filter.Eq(r => r.RoleId, roleId);
        var update = Builders<Role>.Update
            .Set(r => r.UiPanelPosX, posX)
            .Set(r => r.UiPanelPosY, posY)
            .Set(r => r.UiPanelWidth, width)
            .Set(r => r.UiPanelHeight, height);
        await _col.UpdateOneAsync(filter, update);
    }

    public async Task<bool> CheckNameExists(int serverId, string roleName)
    {
        var filter = Builders<Role>.Filter.And(
            Builders<Role>.Filter.Eq(r => r.ServerId, serverId),
            Builders<Role>.Filter.Eq(r => r.RoleName, roleName));
        return await _col.Find(filter).FirstOrDefaultAsync() != null;
    }

    public async Task<long> CountByAccountAndServer(long accountId, int serverId)
    {
        var filter = Builders<Role>.Filter.And(
            Builders<Role>.Filter.Eq(r => r.AccountId, accountId),
            Builders<Role>.Filter.Eq(r => r.ServerId, serverId));
        return await _col.CountDocumentsAsync(filter);
    }

    public async Task<long> GetNextRoleId() => await _counters.GetNextId("role_id");

    public async Task EnsureIndex()
    {
        await _col.Indexes.CreateOneAsync(
            new CreateIndexModel<Role>(
                Builders<Role>.IndexKeys
                    .Ascending(r => r.ServerId)
                    .Ascending(r => r.RoleName),
                new CreateIndexOptions { Unique = true }));
    }
}
