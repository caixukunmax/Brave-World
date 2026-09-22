namespace GameServer.Common;

/// <summary>
/// 方向计算工具 — 从移动向量推导 4 方向索引
/// </summary>
public static class DirectionUtil
{
    /// <summary>
    /// 从移动向量推导 4 方向索引 (0=右, 1=下, 2=左, 3=上)
    /// </summary>
    public static int FromMoveVector(int fromX, int fromY, int toX, int toY)
    {
        int dx = Math.Sign(toX - fromX);
        int dy = Math.Sign(toY - fromY);
        // 优先水平方向，再判断垂直方向
        if (dx > 0) return 0;  // 右
        if (dx < 0) return 2;  // 左
        if (dy > 0) return 1;  // 下
        if (dy < 0) return 3;  // 上
        return 1;               // 默认向下
    }
}