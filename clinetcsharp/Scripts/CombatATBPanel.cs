using Godot;
using System.Collections.Generic;
using Protocol;

namespace ClinetCSharp
{
    public partial class CombatATBPanel : Control
    {
        private Control _line;
        private Control _markersContainer;
        private readonly List<Marker> _markers = new();
        private NetworkManager _network;

        private const float LINE_WIDTH = 600f;
        private const float LERP_SPEED = 15f;

        private class UnitState
        {
            public ulong EntityId;
            public string Name;
            public float TargetAtb;
            public float CurrentAtb;
            public bool IsPlayer;
        }

        private readonly Dictionary<ulong, UnitState> _units = new();

        public override void _Ready()
        {
            _line = GetNode<Control>("Line");
            _markersContainer = GetNode<Control>("MarkersContainer");

            var tree = GetTree();
            if (tree != null)
            {
                foreach (var child in tree.Root.GetChildren())
                {
                    if (child is NetworkManager nm)
                    {
                        _network = nm;
                        break;
                    }
                }
                if (_network == null)
                    _network = tree.Root.GetNodeOrNull<NetworkManager>("NetworkManager");
                if (_network != null)
                    _network.CombatStateNotify += OnCombatStateNotify;
            }

            Modulate = Colors.White;
        }

        public override void _ExitTree()
        {
            if (_network != null)
                _network.CombatStateNotify -= OnCombatStateNotify;
        }

        public override void _Process(double delta)
        {
            bool anyVisible = false;
            foreach (var marker in _markers)
            {
                if (!marker.IsValid) continue;
                if (_units.TryGetValue(marker.EntityId, out var state))
                {
                    marker.Node.Visible = true;
                    anyVisible = true;

                    // 检测是否刚刚行动（从 >=95 掉到 <10）
                    if (state.CurrentAtb >= 95f && state.TargetAtb < 10f)
                    {
                        marker.TriggerFlash();
                    }

                    state.CurrentAtb = Mathf.Lerp(state.CurrentAtb, state.TargetAtb, (float)delta * LERP_SPEED);

                    // 如果差距很小，直接同步
                    if (Mathf.Abs(state.CurrentAtb - state.TargetAtb) < 0.5f)
                        state.CurrentAtb = state.TargetAtb;

                    float t = Mathf.Clamp(state.CurrentAtb / 100f, 0f, 1f);
                    float x = t * LINE_WIDTH;
                    marker.Node.Position = new Vector2(x - marker.Node.Size.X * 0.5f, marker.Node.Position.Y);
                }
                else
                {
                    marker.Node.Visible = false;
                }
            }

            Visible = anyVisible;
        }

        private void OnCombatStateNotify(Game.CombatStateNotify notify)
        {
            CallDeferred(nameof(ApplyState), notify);
        }

        private void ApplyState(Game.CombatStateNotify notify)
        {
            if (notify.Units.Count == 0)
            {
                _units.Clear();
                return;
            }

            var receivedIds = new HashSet<ulong>();
            foreach (var u in notify.Units)
            {
                ulong entityId = u.EntityId;
                receivedIds.Add(entityId);
                if (!_units.TryGetValue(entityId, out var state))
                {
                    state = new UnitState { EntityId = entityId };
                    _units[entityId] = state;
                }
                state.Name = u.EntityName;
                state.TargetAtb = u.Atb;
                state.IsPlayer = u.IsPlayer;
            }

            // 移除不再存在的单位
            var toRemove = new List<ulong>();
            foreach (var id in _units.Keys)
            {
                if (!receivedIds.Contains(id))
                    toRemove.Add(id);
            }
            foreach (var id in toRemove)
                _units.Remove(id);

            SyncMarkers();
        }

        private void SyncMarkers()
        {
            // 复用或创建标记
            int index = 0;
            foreach (var pair in _units)
            {
                var state = pair.Value;
                Marker marker;
                if (index < _markers.Count)
                {
                    marker = _markers[index];
                }
                else
                {
                    marker = CreateMarker();
                    _markers.Add(marker);
                }
                marker.EntityId = state.EntityId;
                marker.SetStyle(state.IsPlayer, state.Name);
                marker.Node.Visible = true;
                index++;
            }

            for (int i = index; i < _markers.Count; i++)
            {
                _markers[i].Node.Visible = false;
            }
        }

        private Marker CreateMarker()
        {
            var node = new Control();
            node.CustomMinimumSize = new Vector2(80, 30);
            node.Size = new Vector2(80, 30);

            var shape = new ColorRect();
            shape.Size = new Vector2(10, 10);
            shape.Position = new Vector2(35, 10);
            node.AddChild(shape);

            var label = new Label();
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Size = new Vector2(80, 14);
            label.Position = new Vector2(0, -2);
            label.AddThemeFontSizeOverride("font_size", 12);
            node.AddChild(label);

            _markersContainer.AddChild(node);
            return new Marker { Node = node, Shape = shape, Label = label };
        }

        private class Marker
        {
            public Control Node;
            public ColorRect Shape;
            public Label Label;
            public ulong EntityId;
            public bool IsValid => Node != null && GodotObject.IsInstanceValid(Node);

            public void SetStyle(bool isPlayer, string name)
            {
                Label.Text = name;
                if (isPlayer)
                {
                    Shape.Color = new Color("#00FF00");
                    Shape.Size = new Vector2(10, 10);
                    Label.Position = new Vector2(0, -14);
                }
                else
                {
                    Shape.Color = new Color("#FF4444");
                    Shape.Size = new Vector2(10, 10);
                    Label.Position = new Vector2(0, 12);
                }
                // 圆点 vs 方块：敌人用圆角
                var style = new StyleBoxFlat
                {
                    BgColor = Shape.Color,
                    CornerRadiusTopLeft = isPlayer ? 0 : 5,
                    CornerRadiusTopRight = isPlayer ? 0 : 5,
                    CornerRadiusBottomLeft = isPlayer ? 0 : 5,
                    CornerRadiusBottomRight = isPlayer ? 0 : 5,
                };
                Shape.AddThemeStyleboxOverride("panel", style);
                Shape.Position = new Vector2(35, 10);
            }

            public void TriggerFlash()
            {
                if (!IsValid) return;
                var tween = Node.CreateTween();
                if (tween == null) return;
                tween.TweenProperty(Node, "scale", new Vector2(1.3f, 1.3f), 0.05f);
                tween.TweenProperty(Node, "scale", Vector2.One, 0.15f);
            }
        }
    }
}
