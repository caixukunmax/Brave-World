using GameServer.Tables;

namespace GameServer.Common.Buffs;

/// <summary>
/// Buff 容器 — 管理实体上的所有 Buff/Debuff
/// 构造时注入 LubanTableLoader 用于查询 Buff 配置（Tags、AttrModifiers 等）
/// </summary>
public class BuffContainer
{
    private readonly List<BuffInstance> _buffs = new();
    private readonly LubanTableLoader? _tables;

    public BuffContainer(LubanTableLoader? tables = null)
    {
        _tables = tables;
    }

    public IReadOnlyList<BuffInstance> Buffs => _buffs;

    // ---- 添加/移除/查询 ----

    /// <summary>
    /// 添加 Buff，处理叠加规则
    /// 返回实际添加或更新的 BuffInstance
    /// </summary>
    public BuffInstance? AddBuff(BuffInstance buff)
    {
        var cfg = _tables?.GetBuff(buff.BuffId);

        // 有配置表但找不到配置 → 拒绝添加
        if (_tables != null && cfg == null) return null;

        var existing = _buffs.FirstOrDefault(b => b.BuffId == buff.BuffId);
        if (existing != null)
        {
            if (cfg == null)
            {
                // 无配置表时简单刷新
                existing.ApplyTime = buff.ApplyTime;
                existing.ExpireTime = buff.ExpireTime;
                existing.CasterId = buff.CasterId;
                return existing;
            }

            switch (cfg.StackRule)
            {
                case "Refresh":
                    existing.ExpireTime = buff.ExpireTime;
                    existing.Stacks = 1;
                    existing.ApplyTime = buff.ApplyTime;
                    existing.LastTickTime = buff.ApplyTime;
                    existing.TickCount = 0;
                    existing.ShieldRemaining = buff.ShieldRemaining;
                    existing.SnapshotAtk = buff.SnapshotAtk;
                    existing.SnapshotMatk = buff.SnapshotMatk;
                    existing.CasterId = buff.CasterId;
                    return existing;

                case "Add":
                    if (existing.Stacks < cfg.MaxStacks)
                        existing.Stacks++;
                    existing.ExpireTime = buff.ExpireTime;
                    return existing;

                case "Ignore":
                default:
                    return existing;
            }
        }

        _buffs.Add(buff);
        return buff;
    }

    /// <summary>移除 Buff</summary>
    public void RemoveBuff(int buffId)
    {
        _buffs.RemoveAll(b => b.BuffId == buffId);
    }

    public BuffInstance? GetBuff(int buffId) => _buffs.FirstOrDefault(b => b.BuffId == buffId);
    public bool HasBuff(int buffId) => _buffs.Any(b => b.BuffId == buffId);

    /// <summary>检查是否有带指定 tag 的 Buff</summary>
    public bool HasTag(string tag)
    {
        foreach (var buff in _buffs)
        {
            var cfg = _tables?.GetBuff(buff.BuffId);
            if (cfg != null && cfg.Tags.Contains(tag))
                return true;
        }
        return false;
    }

    // ---- 属性修正 ----

    /// <summary>获取某属性的修正值总和</summary>
    public int GetAttrModifier(string attrName)
    {
        double total = 0;
        foreach (var buff in _buffs)
        {
            var cfg = _tables?.GetBuff(buff.BuffId);
            if (cfg == null) continue;
            foreach (var mod in cfg.AttrModifiers)
            {
                if (mod.Attr != attrName) continue;
                if (mod.IsPct)
                    total += mod.Value;
                else
                    total += mod.Value * buff.Stacks;
            }
        }
        return (int)total;
    }

    // ---- 护盾 ----

    /// <summary>获取所有护盾 Buff 的剩余吸收量总和</summary>
    public int GetShieldAmount()
    {
        int total = 0;
        foreach (var buff in _buffs)
        {
            var cfg = _tables?.GetBuff(buff.BuffId);
            if (cfg != null && cfg.Tags.Contains("shield"))
                total += buff.ShieldRemaining;
        }
        return total;
    }

    /// <summary>吸收盾扣减，返回实际吸收量</summary>
    public int AbsorbShield(int damage)
    {
        int remaining = damage;
        int absorbed = 0;

        for (int i = _buffs.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var buff = _buffs[i];
            var cfg = _tables?.GetBuff(buff.BuffId);
            if (cfg == null || !cfg.Tags.Contains("shield")) continue;

            if (remaining <= buff.ShieldRemaining)
            {
                buff.ShieldRemaining -= remaining;
                absorbed += remaining;
                remaining = 0;
            }
            else
            {
                absorbed += buff.ShieldRemaining;
                remaining -= buff.ShieldRemaining;
                buff.ShieldRemaining = 0;
                _buffs.RemoveAt(i);
            }
        }

        return absorbed;
    }

    // ---- 清除 ----

    /// <summary>清除所有 Buff</summary>
    public void ClearAll()
    {
        _buffs.Clear();
    }

    /// <summary>清除脱战时应移除的 Buff（配置表 clear_on_disengage = true）</summary>
    public void ClearOnDisengage()
    {
        if (_tables == null)
        {
            // 无配置表时全部清除（安全默认）
            _buffs.Clear();
            return;
        }
        _buffs.RemoveAll(b =>
        {
            var cfg = _tables.GetBuff(b.BuffId);
            return cfg == null || cfg.ClearOnDisengage;
        });
    }

    /// <summary>移除所有 Debuff（buff_type == "Debuff"），返回移除数量</summary>
    public int RemoveDebuffs()
    {
        if (_tables == null) return 0;
        int count = _buffs.Count;
        _buffs.RemoveAll(b =>
        {
            var cfg = _tables.GetBuff(b.BuffId);
            return cfg != null && cfg.BuffType == "Debuff";
        });
        return count - _buffs.Count;
    }
}