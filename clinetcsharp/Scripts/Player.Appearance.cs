using Godot;
using Protocol;

namespace ClinetCSharp
{
    public partial class Player
    {
        /// <summary>
        /// 从配置文件加载样式参数，确保 VisualSizeScale 等值在场景显示前就绪
        /// </summary>
        private void LoadStyleConfig()
        {
            var config = new ConfigFile();
            if (config.Load("res://debug_panel_config.cfg") != Error.Ok)
                return;
            if (!config.HasSection("player"))
                return;

            var scale = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, "player", "visual_size_scale", 10000));
            if (scale != 1.0f)
                SetVisualSizeScale(scale);

            var borderScale = EntityProfileManager.FromFp(EntityProfileManager.ReadFp(config, "player", "border_width_scale", 270));
            if (borderScale != (3.0f / 111.0f))
                SetBorderWidthScale(borderScale);
        }

        public override void SetVisualSizeScale(float scale)
        {
            VisualSizeScale = scale;
            QueueRedraw();
        }

        public override void SetBorderWidthScale(float scale)
        {
            BorderWidthScale = scale;
            QueueRedraw();
        }

        public void SetTextAlignment(HorizontalAlignment newAlignment)
        {
            TextAlignment = newAlignment;
            UpdateAllLabelPositions();
        }

        public override void SetFontSize(int size)
        {
            FontSizeOverride = size;
            UpdateLabelFontSize();
        }

        public void SetLineSpacing(float spacing)
        {
            LineSpacing = spacing;
            UpdateLabelFontSize();
        }

        public override void SetCornerRadius(float radius)
        {
            CornerRadius = radius;
            QueueRedraw();
        }

        public void SetLineColor(int lineIndex, Color color)
        {
            if (lineIndex >= 0 && lineIndex < LineColors.Count)
            {
                LineColors[lineIndex] = color;
                if (_labels[lineIndex] != null)
                    _labels[lineIndex].AddThemeColorOverride("font_color", color);
            }
        }

        public void SetLetterSpacing(float spacing)
        {
            LetterSpacing = spacing;
            UpdateLabelFontSize();
        }

        public void SetFontBold(bool enabled)
        {
            FontBold = enabled;
            UpdateLabelFontSize();
        }

        public void SetFontItalic(bool enabled)
        {
            FontItalic = enabled;
            UpdateLabelFontSize();
        }

        public void SetFontShadow(bool enabled)
        {
            FontShadow = enabled;
            UpdateLabelFontSize();
        }

        public override void SetBgOpacity(float opacity)
        {
            BgOpacity = opacity;
            QueueRedraw();
        }

        public void SetShowDebugInfo(bool show)
        {
            ShowDebugInfo = show;
            EntityBase.GlobalDebugOverlayVisible = show;
            QueueRedraw();
        }

        public Vector2 GetLevelBadgeOffset() => LevelBadgeOffset;

        public void SetLevelBadgeOffset(Vector2 offset)
        {
            LevelBadgeOffset = offset;
            QueueRedraw();
        }

        public void SetLevelBadgeFontSize(float size)
        {
            LevelBadgeFontSize = size;
            QueueRedraw();
        }

        public void SetLevelBadgeTextColor(Color color)
        {
            LevelBadgeTextColor = color;
            QueueRedraw();
        }

        public void SetLevelBadgeText(string text)
        {
            LevelBadgeText = text;
            QueueRedraw();
        }

        public void SetLevelBadgeVisible(bool visible)
        {
            LevelBadgeVisible = visible;
            QueueRedraw();
        }

        /// <summary>
        /// 从服务器返回的角色数据更新显示
        /// </summary>
        public void ApplyRoleInfo(Game.FullRoleInfo roleInfo)
        {
            if (roleInfo == null)
                return;

            CharacterName = roleInfo.RoleName;
            Level = (int)roleInfo.Level;
            Job = roleInfo.Job;
            Title = roleInfo.Title;
            Status = roleInfo.Status;
            GridSizeX = roleInfo.SizeX > 0 ? roleInfo.SizeX : 1;
            GridSizeY = roleInfo.SizeY > 0 ? roleInfo.SizeY : 1;

            PlayerLabelTexts[0] = $"LV.{Level} {CharacterName}";
            PlayerLabelTexts[1] = Job;
            PlayerLabelTexts[2] = Title;
            PlayerLabelTexts[3] = Status;

            LevelBadgeText = "LV.{level}";

            GD.Print($"[Player] ApplyRoleInfo: name={CharacterName}, level={Level}, job={Job}, title={Title}, status={Status}");

            CombatAttrs.Clear();
            foreach (var attr in roleInfo.Attrs)
                CombatAttrs[attr.Key] = attr.Value;
            if (roleInfo.Attrs.Count > 0)
                GD.Print($"[Player] ApplyRoleInfo: attrs loaded, count={roleInfo.Attrs.Count}");

            if (CombatAttrs.TryGetValue(10, out var moveSpeedVal) && moveSpeedVal > 0)
            {
                // move_speed 属性值 120~600，值越大越快
                // 映射到实际移动时间：120→600ms（最慢），600→120ms（最快）
                int durationMs = 720 - moveSpeedVal;
                if (durationMs < 120) durationMs = 120;
                if (durationMs > 600) durationMs = 600;
                MoveDuration = durationMs / 1000f;
            }

            // 同步血条/蓝条显示
            if (CombatAttrs.TryGetValue(2, out var maxHp) && maxHp > 0 && CombatAttrs.TryGetValue(1, out var hp))
                HealthBarFillPercent = (float)hp / maxHp;
            if (CombatAttrs.TryGetValue(4, out var maxMp) && maxMp > 0 && CombatAttrs.TryGetValue(3, out var mp))
                MpBarFillPercent = (float)mp / maxMp;

            QueueServerGridCorrection(new Vector2I(roleInfo.GridX, roleInfo.GridY));
            GD.Print($"[Player] Received server position: ({roleInfo.GridX}, {roleInfo.GridY})");

            SetupLabels();
            QueueRedraw();
        }
    }
}
