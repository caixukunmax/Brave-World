using TMPro;
using UnityClientSharp.Map.Core;
using UnityClientSharp.Map.Rendering;
using UnityEngine;

namespace UnityClientSharp.Entity
{
    /// <summary>
    /// 地图装饰实体（房舍等静态摆件）。
    /// 移植自 clinetcsharp/Scripts/MapDecoration.cs 的显示部分：
    /// 由 entityType="decoration" 的 EntityProfile 驱动外观（程序生成圆角色块）/ 名称标签 / 障碍属性。
    /// 裁剪（后续阶段）：传送门菜单/酒馆按钮交互、编辑模式拖拽、HitTest。
    /// </summary>
    public class MapDecoration : MonoBehaviour
    {
        /// <summary>建筑配置 ID（即 map.json 的 decoration 字段 / ProfileId）</summary>
        public int ProfileId { get; private set; }
        /// <summary>建筑实例唯一 UID</summary>
        public int BuildingUid { get; private set; } = -1;
        /// <summary>footprint 左上角锚点格子</summary>
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        /// <summary>占地宽度（格子数）</summary>
        public int SizeX { get; private set; } = 1;
        /// <summary>占地高度（格子数）</summary>
        public int SizeY { get; private set; } = 1;
        /// <summary>是否阻塞移动（由 obstacle 组件控制）</summary>
        public bool BlockMovement { get; private set; }

        private int _gridSize = 111;

        public void Setup(int profileId, int gridX, int gridY, int gridSize, int buildingUid = -1, int sizeX = 1, int sizeY = 1)
        {
            ProfileId = profileId;
            BuildingUid = buildingUid;
            GridX = gridX;
            GridY = gridY;
            _gridSize = gridSize;
            SizeX = Mathf.Max(1, sizeX);
            SizeY = Mathf.Max(1, sizeY);

            name = $"MapDecoration_{gridX}_{gridY}_{profileId}_{buildingUid}";

            // 从 EntityProfile 应用配置；profile 不存在时回退到同建筑类型 base id（对齐 Godot 防御逻辑）
            var profile = EntityProfileManager.GetProfile(profileId);
            if (profile == null)
            {
                int fallbackId = BuildingType.GetConfigBaseId(BuildingType.GetTypeFromConfigId(profileId));
                if (fallbackId != profileId && EntityProfileManager.GetProfile(fallbackId) != null)
                {
                    Debug.LogWarning($"[MapDecoration] Profile {profileId} 不存在，fallback 到 {fallbackId} (grid={gridX},{gridY})");
                    ProfileId = fallbackId;
                    profile = EntityProfileManager.GetProfile(fallbackId);
                }
            }

            var app = profile?.GetData<AppearanceData>("appearance");
            if (app != null)
            {
                SizeX = Mathf.Max(1, app.SizeX);
                SizeY = Mathf.Max(1, app.SizeY);
            }
            BlockMovement = profile?.GetData<ObstacleData>("obstacle")?.BlockMovement ?? false;

            // 定位：footprint 中心（Y 翻转统一走 GridMath，AGENTS.md 第 1 条）
            transform.localPosition = GridMath.FootprintCenterWorld(GridX, GridY, SizeX, SizeY, _gridSize);

            BuildBody(app);
            BuildLabel(profile, app);
        }

        private void BuildBody(AppearanceData app)
        {
            // 尺寸公式统一走 EntityAppearanceLayout（与编辑器预览/游戏内实体一致）
            EntityAppearanceLayout.ComputeBody(_gridSize, SizeX, SizeY,
                app?.VisualSizeScale ?? 1.0f, app?.BorderWidthScale ?? EntityAppearanceLayout.DefaultBorderWidthScale,
                out int outerW, out int outerH, out int border);

            var go = new GameObject("Body");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EntityBodySprite.Get(outerW, outerH, border, app?.CornerRadius ?? 8f,
                app?.BorderColor ?? Color.white, app?.BgColor ?? Color.white, app?.BgOpacity ?? 0.9f);
            sr.sortingOrder = 1; // 地图 Quad（Opaque 队列）之上
        }

        /// <summary>
        /// 按当前 ProfileId 重新应用配置外观（ProfileRefreshUtil.RefreshAll / 调试面板手动同步用）。
        /// 重建 Body/Label；注意占地(SizeX/Y)/阻挡(BlockMovement)变更对地图阻挡格的登记需重新进图才完全生效。
        /// </summary>
        public void RefreshFromProfile()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                string n = transform.GetChild(i).name;
                if (n == "Body" || n == "Label" || n == "LabelShadow")
                    Destroy(transform.GetChild(i).gameObject);
            }
            Setup(ProfileId, GridX, GridY, _gridSize, BuildingUid, SizeX, SizeY);
        }

        private void BuildLabel(EntityProfile profile, AppearanceData app)
        {
            // 「停用」语义与调试面板预览一致：labels 组件停用时不生成任何标签
            if (profile == null || profile.IsComponentDisabled("labels")) return;
            var labels = profile.GetData<LabelGroupData>("labels");
            string text = labels != null && labels.Count > 0 && labels.Visible[0] ? labels.ContentPreview[0] : "";
            if (string.IsNullOrEmpty(text)) return;

            // 字号/颜色/样式统一走 EntityAppearanceLayout（与编辑器预览/游戏内实体一致）
            int fs = EntityAppearanceLayout.RowFontSize(_gridSize, labels, 0,
                app?.VisualSizeScale ?? 1.0f, app?.BorderWidthScale ?? EntityAppearanceLayout.DefaultBorderWidthScale);

            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(labels.CenterX[0] ? 0 : labels.XOffset[0],
                EntityAppearanceLayout.LineWorldOffsetY(0, fs, labels.YOffset[0], labels.Count), 0);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            FontUtil.SetWorldFontSize(tmp, fs);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = EntityAppearanceLayout.RowFontStyle(labels);
            tmp.color = EntityAppearanceLayout.RowTextColor(labels, 0);
            FontUtil.ApplyCjkFont(tmp);
            const int order = 3; // Body=1，阴影=2，主标签=3
            tmp.GetComponent<MeshRenderer>().sortingOrder = order;
            if (labels.Shadow) EntityLabelShadow.Create(tmp, "LabelShadow", order - 1);
        }
    }
}
