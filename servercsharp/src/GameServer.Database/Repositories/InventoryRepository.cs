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

    /// <summary>
    /// 添加物品。返回 true 表示新插入了一条文档（此前该 item_id 不存在），false 表示更新了已有文档。
    /// </summary>
    public async Task<bool> AddItem(long roleId, int itemId, int count)
    {
        var filter = Builders<InventoryItem>.Filter.And(
            Builders<InventoryItem>.Filter.Eq(i => i.RoleId, roleId),
            Builders<InventoryItem>.Filter.Eq(i => i.ItemId, itemId));
        var existing = await _col.Find(filter).FirstOrDefaultAsync();

        if (existing != null)
        {
            var newCount = existing.Count + count;
            await _col.UpdateOneAsync(filter, Builders<InventoryItem>.Update.Set(i => i.Count, newCount));
            return false;
        }
        else
        {
            await _col.InsertOneAsync(new InventoryItem
            {
                RoleId = roleId,
                ItemId = itemId,
                Count = count,
            });
            return true;
        }
    }

    /// <summary>
    /// 移除物品。
    /// Success：物品存在且数量足够扣除请求数量。
    /// Deleted：扣除后数量归零，文档已被删除。
    /// </summary>
    public async Task<(bool Success, bool Deleted)> RemoveItem(long roleId, int itemId, int count)
    {
        var filter = Builders<InventoryItem>.Filter.And(
            Builders<InventoryItem>.Filter.Eq(i => i.RoleId, roleId),
            Builders<InventoryItem>.Filter.Eq(i => i.ItemId, itemId));
        var existing = await _col.Find(filter).FirstOrDefaultAsync();
        if (existing == null) return (false, false);

        var newCount = existing.Count - count;
        if (newCount <= 0)
        {
            await _col.DeleteOneAsync(filter);
            return (true, true);
        }
        else
        {
            await _col.UpdateOneAsync(filter, Builders<InventoryItem>.Update.Set(i => i.Count, newCount));
            return (true, false);
        }
    }
}
