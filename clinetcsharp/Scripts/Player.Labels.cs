using Godot;

namespace ClinetCSharp
{
    public partial class Player
    {
        /// <summary>
        /// 公共方法：刷新标签显示（供 DebugPanel 调用）
        /// </summary>
        public async void RefreshLabels()
        {
            await SetupLabelsInternal();
        }

        private async void SetupLabels()
        {
            await SetupLabelsInternal();
        }

        private async System.Threading.Tasks.Task SetupLabelsInternal()
        {
            ClearLabelNodes();
            CreateLabelNodes();

            await ToSignal(GetTree(), "process_frame");
            UpdateAllLabelPositions();
            UpdateLabelFontSize();

            var pm = EntityProfileManager.Instance;
            if (pm != null && ProfileId > 0)
                pm.ApplyProfile(this, ProfileId);
        }

        public Vector2 GetLabelOffset(int index)
        {
            if (index < 0 || index >= LabelCount)
                return Vector2.Zero;
            float x = (LabelCenterX[index] || LabelAutoCenterX) ? 0.0f : LabelXOffsets[index];
            return new Vector2(x, LabelYOffsets[index]);
        }

        public void SetLabelOffset(int index, Vector2 offset)
        {
            if (index < 0 || index >= LabelCount)
                return;

            _labelOffsets[index] = offset;
            LabelYOffsets[index] = offset.Y;
            LabelXOffsets[index] = (LabelCenterX[index] || LabelAutoCenterX) ? 0.0f : offset.X;
            UpdateAllLabelPositions();
        }

        public void ResetLabelOffset(int index)
        {
            if (index < 0 || index >= LabelCount)
                return;

            _labelOffsets[index] = DefaultOffsets[index];
            LabelXOffsets[index] = 0.0f;
            LabelYOffsets[index] = 0.0f;
            UpdateAllLabelPositions();
        }

        public void ResetAllLabelOffsets()
        {
            for (int i = 0; i < LabelCount; i++)
            {
                _labelOffsets[i] = DefaultOffsets[i];
                LabelXOffsets[i] = 0.0f;
                LabelYOffsets[i] = 0.0f;
            }
            UpdateAllLabelPositions();
        }

        public void SetLabelAutoCenterX(bool enabled)
        {
            LabelAutoCenterX = enabled;
            if (enabled)
            {
                for (int i = 0; i < LabelCount; i++)
                {
                    _labelOffsets[i] = new Vector2(0, _labelOffsets[i].Y);
                    LabelXOffsets[i] = 0.0f;
                }
            }

            UpdateAllLabelPositions();
        }

        public override bool GetLabelVisible(int index)
        {
            if (index < 0 || index >= LabelCount)
                return false;
            return _labelVisible[index];
        }

        public string GetLabelName(int index)
        {
            if (index < 0 || index >= LabelCount)
                return "";
            return LabelNames[index];
        }

        public void SetLabelName(int index, string name)
        {
            if (index < 0 || index >= LabelCount)
                return;
            LabelNames[index] = name;
        }

        public string GetLabelText(int index)
        {
            if (index < 0 || index >= LabelCount)
                return "";
            return PlayerLabelTexts[index];
        }

        public override void SetLabelText(int index, string text)
        {
            if (index < 0 || index >= LabelCount)
                return;

            PlayerLabelTexts[index] = text;
            if (_labels[index] != null)
            {
                _labels[index].Text = text;
                UpdateAllLabelPositions();
            }
        }

        public override void RefreshLabelVisibility()
        {
            UpdateAllLabelPositions();
        }

        public override void SetLabelVisible(int index, bool visible)
        {
            if (index < 0 || index >= LabelCount)
                return;

            _labelVisible[index] = visible;
            UpdateAllLabelPositions();
        }

        public int GetLabelFontSize(int index)
        {
            if (index < 0 || index >= LabelCount)
                return 0;
            return _labelFontSizes[index];
        }

        public override void SetLabelFontSize(int index, int size)
        {
            if (index < 0 || index >= LabelCount)
                return;

            _labelFontSizes[index] = size;
            UpdateLabelFontSize();
        }

        public override void SetGridSize(int newSize)
        {
            base.SetGridSize(newSize);
            UpdateAllLabelPositions();
        }
    }
}
