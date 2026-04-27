using GameServer.Common.Config;

namespace GameServer.Services.Monster.AI;

/// <summary>
/// 轻量寻路模块 — 移植自 ai/pathfind.lua
/// </summary>
public static class Pathfind
{
    public static int Manhattan(int ax, int ay, int bx, int by)
        => Math.Abs(ax - bx) + Math.Abs(ay - by);

    /// <summary>
    /// BFS 寻找从 (startX, startY) 到 (goalX, goalY) 的下一步方向
    /// 返回 (nextX, nextY)，若不可达返回 null
    /// </summary>
    /// <param name="isBlocked">动态障碍回调，返回 true 表示该格被实体占据应跳过（不含自身）</param>
    public static (int x, int y)? BfsNextStep(int startX, int startY, int goalX, int goalY,
        string mapName, MapDataProvider mapData, Func<int, int, bool>? isBlocked = null)
    {
        if (startX == goalX && startY == goalY)
            return (startX, startY);

        var visited = new HashSet<(int, int)> { (startX, startY) };
        var queue = new Queue<(int x, int y)>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();

        queue.Enqueue((startX, startY));

        int[][] dirs = [[0, -1], [0, 1], [-1, 0], [1, 0]];

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();

            foreach (var d in dirs)
            {
                int nx = cur.x + d[0], ny = cur.y + d[1];
                var key = (nx, ny);

                if (visited.Contains(key)) continue;
                if (!mapData.IsWalkable(mapName, nx, ny)) continue;
                if (isBlocked != null && isBlocked(nx, ny)) continue;

                visited.Add(key);
                cameFrom[key] = cur;
                queue.Enqueue((nx, ny));

                if (nx == goalX && ny == goalY)
                {
                    // 回溯找到第一步
                    var step = (nx, ny);
                    while (true)
                    {
                        if (!cameFrom.TryGetValue(step, out var prev))
                            return step;
                        if (prev == (startX, startY))
                            return step;
                        step = prev;
                    }
                }
            }
        }

        return null;
    }
}
