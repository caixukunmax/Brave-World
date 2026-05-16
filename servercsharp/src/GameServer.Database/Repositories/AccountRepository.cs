using GameServer.Database.Models;
using MongoDB.Driver;

namespace GameServer.Database.Repositories;

public class AccountRepository
{
    private readonly IMongoCollection<Account> _col;
    private readonly CounterRepository _counters;

    public AccountRepository(MongoDbContext db, CounterRepository counters)
    {
        _col = db.Accounts;
        _counters = counters;
    }

    public async Task<Account?> FindByUsername(string username)
    {
        return await _col.Find(Builders<Account>.Filter.Eq(a => a.Username, username)).FirstOrDefaultAsync();
    }

    public async Task<Account?> FindById(long accountId)
    {
        return await _col.Find(Builders<Account>.Filter.Eq(a => a.AccountId, accountId)).FirstOrDefaultAsync();
    }

    public async Task<Account> Create(string username, string hashedPassword)
    {
        var accountId = await _counters.GetNextId("account_id");
        var account = new Account
        {
            AccountId = accountId,
            Username = username,
            Password = hashedPassword,
            CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        await _col.InsertOneAsync(account);
        return account;
    }

    public async Task UpdateLastServer(long accountId, int serverId, string roleName)
    {
        var filter = Builders<Account>.Filter.Eq(a => a.AccountId, accountId);
        var update = Builders<Account>.Update
            .Set(a => a.LastServerId, serverId)
            .Set(a => a.LastRoleName, roleName);
        await _col.UpdateOneAsync(filter, update);
    }

    public async Task UpdatePassword(long accountId, string newHashedPassword)
    {
        var filter = Builders<Account>.Filter.Eq(a => a.AccountId, accountId);
        var update = Builders<Account>.Update.Set(a => a.Password, newHashedPassword);
        await _col.UpdateOneAsync(filter, update);
    }

    public async Task EnsureIndex()
    {
        await _col.Indexes.CreateOneAsync(
            new CreateIndexModel<Account>(
                Builders<Account>.IndexKeys.Ascending(a => a.Username),
                new CreateIndexOptions { Unique = true }));
    }
}
