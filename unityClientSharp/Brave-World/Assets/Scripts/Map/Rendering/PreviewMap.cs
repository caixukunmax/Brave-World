using UnityEngine;
using UnityEngine.UI;

namespace UnityClientSharp.Map.Rendering
{
    /// <summary>
    /// 整图迷你预览。第一版：用 MapThumbnailBuilder 生成静态地形缩略图显示在 RawImage，
    /// 与 Godot PreviewMap 的视觉一致。后续可升级为独立相机 RenderTexture 实时预览（含实体）。
    /// </summary>
    public class PreviewMap : MonoBehaviour
    {
        public RawImage TargetImage;
        public GridManager SourceGrid;
        public int MaxTextureLongSide = 256;

        private void Start()
        {
            if (SourceGrid == null) SourceGrid = FindObjectOfType<GridManager>();
            Refresh();
        }

        public void Refresh()
        {
            if (SourceGrid != null && TargetImage != null)
                TargetImage.texture = MapThumbnailBuilder.Build(SourceGrid, MaxTextureLongSide);
        }
    }
}
