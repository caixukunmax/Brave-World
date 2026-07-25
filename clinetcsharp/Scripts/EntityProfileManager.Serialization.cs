using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// EntityProfileManager partial — 定点序列化辅助
    /// 转发到 ProfileConfigIO（不依赖 Node 的纯静态逻辑）。
    /// 保留 EntityProfileManager.FromFp/ToFp/ToFpD/ReadFp 以维持运行时与 UI 代码既有调用。
    /// </summary>
    public partial class EntityProfileManager
    {
        public static int ToFp(float v) => ProfileConfigIO.ToFp(v);
        public static int ToFpD(double v) => ProfileConfigIO.ToFpD(v);
        public static float FromFp(int v) => ProfileConfigIO.FromFp(v);
        public static int ReadFp(ConfigFile config, string section, string key, int defaultFp)
            => ProfileConfigIO.ReadFp(config, section, key, defaultFp);
    }
}
