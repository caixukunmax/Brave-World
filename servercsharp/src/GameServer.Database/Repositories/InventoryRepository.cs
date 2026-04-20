using GameServer.Database.Models;
using MongoDB.Driver;

namespace GameServer.Database.Repositories;

public class InventoryRepository
{
    private readonly IMongoCollection<InventoryItem> _col;

    public InventoryRepository(MongoDbContext db)
    {
        _col = db.Inventories;
    }

    public async Task<List<InventoryItem>> GetByRole(long roleId)
    {
        return await _col.Find(Builders<InventoryItem>.Filter.Eq(i => i.RoleId, roleId)).ToListAsync();
    }

    public async Task AddItem(long roleId, int itemId, int count)
    {
        var filter = Builders<InventoryItem>.Filter.And(
            Builders<InventoryItem>.Filter.Eq(i => i.RoleId, roleId),
            Builders<InventoryItem>.Filter.Eq(i => i.ItemId, itemId));
        var existing = await _col.Find(filter).FirstOrDefaultAsync();

        if (existing != null)
        {
            var newCount = existing.Count + count;
            await _col.UpdateOneAsync(filter, Builders<InventoryItem>.Update.Set(i => i.Count, newCount));
        }
        else
        {
            await _col.InsertOneAsync(new InventoryItem
            {
                RoleId = roleId,
                ItemId = itemId,
                Count = count,
            });
        }
    }

    public async Task<bool> RemoveItem(long roleId, int itemId, int count)
    {
        var filter = Builders<InventoryItem>.Filter.And(
            Builders<InventoryItem>.Filter.Eq(i => i.RoleId, roleId),
            Builders<InventoryItem>.Filter.Eq(i => i.ItemId, itemId));
        var existing = await _col.Find(filter).FirstOrDefaultAsync();
        if (existing == null) return false;

        var newCount = existing.Count - count;
        if (newCount <= 0)
            await _col.DeleteOneAsync(filter);
        else
            await _col.UpdateOneAsync(filter, Builders<InventoryItem>.Update.Set(i => i.Count, newCount));
        return true;
    }
}
