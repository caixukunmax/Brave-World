using Godot;
using System;

namespace ClinetCSharp
{
    /// <summary>
    /// 条形控件（血条/MP条/施法条）的实体操作适配器接口。
    /// 将 Player 的具体方法抽象为统一接口，使 BarControlGroup 不依赖具体实体类型。
    /// </summary>
    public interface IBarEntityAdapter
    {
        int GetGridSize();
        bool GetVisible();
        void SetVisible(bool visible);
        Color GetColor();
        void SetColor(Color color);
        float GetLengthScale();
        void SetLengthScale(float scale);
        float GetHeightScale();
        void SetHeightScale(float scale);
        float GetFillPercent();
        void SetFillPercent(float percent);
        Vector2 GetOffset();
        void SetOffset(Vector2 offset);
    }

    public class HealthBarAdapter : IBarEntityAdapter
    {
        private readonly Player _player;
        public HealthBarAdapter(Player player) { _player = player; }
        public int GetGridSize() => _player.GridSize;
        public bool GetVisible() => _player.HealthBarVisible;
        public void SetVisible(bool v) => _player.SetHealthBarVisible(v);
        public Color GetColor() => _player.HealthBarColor;
        public void SetColor(Color c) => _player.SetHealthBarColor(c);
        public float GetLengthScale() => _player.HealthBarLengthScale;
        public void SetLengthScale(float s) => _player.SetHealthBarLengthScale(s);
        public float GetHeightScale() => _player.HealthBarHeightScale;
        public void SetHeightScale(float s) => _player.SetHealthBarHeightScale(s);
        public float GetFillPercent() => _player.HealthBarFillPercent;
        public void SetFillPercent(float p) => _player.SetHealthBarFillPercent(p);
        public Vector2 GetOffset() => _player.GetHealthBarOffset();
        public void SetOffset(Vector2 o) => _player.SetHealthBarOffset(o);
    }

    public class MpBarAdapter : IBarEntityAdapter
    {
        private readonly Player _player;
        public MpBarAdapter(Player player) { _player = player; }
        public int GetGridSize() => _player.GridSize;
        public bool GetVisible() => _player.MpBarVisible;
        public void SetVisible(bool v) => _player.SetMpBarVisible(v);
        public Color GetColor() => _player.MpBarColor;
        public void SetColor(Color c) => _player.SetMpBarColor(c);
        public float GetLengthScale() => _player.MpBarLengthScale;
        public void SetLengthScale(float s) => _player.SetMpBarLengthScale(s);
        public float GetHeightScale() => _player.MpBarHeightScale;
        public void SetHeightScale(float s) => _player.SetMpBarHeightScale(s);
        public float GetFillPercent() => _player.MpBarFillPercent;
        public void SetFillPercent(float p) => _player.SetMpBarFillPercent(p);
        public Vector2 GetOffset() => _player.GetMpBarOffset();
        public void SetOffset(Vector2 o) => _player.SetMpBarOffset(o);
    }

    public class CastBarAdapter : IBarEntityAdapter
    {
        private readonly Player _player;
        public CastBarAdapter(Player player) { _player = player; }
        public int GetGridSize() => _player.GridSize;
        public bool GetVisible() => _player.CastBarVisible;
        public void SetVisible(bool v) => _player.SetCastBarVisible(v);
        public Color GetColor() => _player.CastBarColor;
        public void SetColor(Color c) => _player.SetCastBarColor(c);
        public float GetLengthScale() => _player.CastBarLengthScale;
        public void SetLengthScale(float s) => _player.SetCastBarLengthScale(s);
        public float GetHeightScale() => _player.CastBarHeightScale;
        public void SetHeightScale(float s) => _player.SetCastBarHeightScale(s);
        public float GetFillPercent() => _player.CastBarFillPercent;
        public void SetFillPercent(float p) => _player.SetCastBarFillPercent(p);
        public Vector2 GetOffset() => _player.GetCastBarOffset();
        public void SetOffset(Vector2 o) => _player.SetCastBarOffset(o);
    }

    public struct BarControlGroupDefaults
    {
        public bool Visible;
        public double Length;
        public double LengthScale;
        public double Height;
        public double HeightScale;
        public double Fill;
        public double OffsetX;
        public double OffsetY;
        public bool CenterX;
        public Color Color;
        public int GridSize;

        public static BarControlGroupDefaults HealthBar(int gridSize) => new BarControlGroupDefaults
        {
            Visible = true,
            Length = 80,
            LengthScale = 80.0 / 111.0,
            Height = 6,
            HeightScale = 6.0 / 111.0,
            Fill = 100,
            OffsetX = 0,
            OffsetY = -70,
            CenterX = true,
            Color = new Color(0, 0.8f, 0, 1),
            GridSize = gridSize
        };

        public static BarControlGroupDefaults MpBar(int gridSize) => new BarControlGroupDefaults
        {
            Visible = true,
            Length = 80,
            LengthScale = 80.0 / 111.0,
            Height = 4,
            HeightScale = 4.0 / 111.0,
            Fill = 100,
            OffsetX = 0,
            OffsetY = -62,
            CenterX = true,
            Color = new Color(0.2f, 0.4f, 1.0f, 1),
            GridSize = gridSize
        };

        public static BarControlGroupDefaults CastBar(int gridSize) => new BarControlGroupDefaults
        {
            Visible = true,
            Length = 60,
            LengthScale = 60.0 / 111.0,
            Height = 4,
            HeightScale = 4.0 / 111.0,
            Fill = 60,
            OffsetX = 0,
            OffsetY = -80,
            CenterX = true,
            Color = new Color(0.3f, 0.5f, 1, 1),
            GridSize = gridSize
        };
    }
}
