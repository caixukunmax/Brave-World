using GameServer.Tables;
using Microsoft.Extensions.Logging;

namespace GameServer.Services.Map.Drop;

/// <summary>
/// 掉落物运行时实体
/// </summary>
public class DropItemEntity
{
    public long DropId;          // 唯一 ID（自增）
    public int ItemId;           // 物品 ID
    public int Count;            // 数量
    public string MapName = "";  // 所在地图
    public int X, Y;             // 格子坐标
    public long SpawnTime;       // 生成时间（Unix 秒）
    public long OwnerId;         // 归属玩家（0=所有人可拾取）
    public float OwnerLockTime;  // 归属锁定剩余时间（秒）
}
