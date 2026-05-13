using Godot;
using System.Collections.Generic;

namespace ClinetCSharp
{
    public partial class CharacterPanel
    {
        private readonly Dictionary<uint, SpinBox> _spinBoxes = new();

        private readonly struct AttrDefinition
        {
            public AttrDefinition(uint key, string label, string gmName, int minValue = 0, int maxValue = 99999)
            {
                Key = key;
                Label = label;
                GmName = gmName;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            public uint Key { get; }
            public string Label { get; }
            public string GmName { get; }
            public int MinValue { get; }
            public int MaxValue { get; }
        }

        private static readonly AttrDefinition[] AttrDefs =
        {
            new(1, "HP", "hp", 0, 99999),
            new(2, "MaxHP", "max_hp", 1, 99999),
            new(3, "MP", "mp", 0, 99999),
            new(4, "MaxMP", "max_mp", 1, 99999),
            new(5, "敏捷", "agility", 0, 99999),
            new(6, "物攻", "patk", 0, 99999),
            new(7, "魔攻", "matk", 0, 99999),
            new(8, "物防", "pdef", 0, 99999),
            new(9, "魔防", "mdef", 0, 99999),
            new(10, "移速", "move_speed", 120, 600),
            new(11, "MP恢复/秒", "mp_regen", 0, 99999),
        };
    }
}
