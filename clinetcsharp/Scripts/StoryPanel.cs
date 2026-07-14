using Godot;
using System;
using System.Collections.Generic;

namespace ClinetCSharp
{
    /// <summary>
    /// 剧情播报窗 —— 按回车翻页，支持调试面板实时调整尺寸、字号、背景透明度与内容边距。
    /// 标题栏隐藏标题与关闭按钮，仅保留一条可拖拽的透明细条。
    /// </summary>
    public partial class StoryPanel : DraggablePanel
    {
        public static int StoryPanelWidth = 560;
        public static int StoryPanelHeight = 240;
        public static int StoryFontSize = 18;
        public static int StoryPanelAlpha = 85;
        public static int StoryContentPadding = 16;
        public static bool StoryTypewriterEnabled = false;
        public static int StoryTypewriterIntervalMs = 50;
        public static int StoryHistoryEntrySpacing = 8;

        private const string BeepPath = "res://assets/audio/typing_beep.wav";
        private const int BeepPlayerCount = 3;
        private const float HistoryScrollSmoothSpeed = 12f;

        private static readonly Color BasePanelBgColor = new(0.1f, 0.1f, 0.1f);
        private static readonly Color BasePanelBorderColor = new(0.3f, 0.3f, 0.3f);

        private Label _chapterTitle;
        private Label _speakerLabel;
        private Label _contentLabel;
        private Label _hintLabel;
        private PanelContainer _titleBar;
        private VBoxContainer _contentContainer;

        private int _currentChapterId;
        private int _currentIndex;
        private IReadOnlyList<StoryConfigUtil.StoryDialogueJsonRow> _currentDialogues;
        private int _pendingChapterId;

        private string _typingFullText = "";
        private int _typingVisibleCount;
        private double _typingElapsed;
        private bool _isTyping;
        private AudioStreamPlayer[] _beepPlayers = Array.Empty<AudioStreamPlayer>();
        private int _beepIndex;

        private readonly record struct HistoryEntry(string Speaker, string Content);
        private readonly List<HistoryEntry> _history = new();

        private VBoxContainer _rootVBox;
        private ScrollContainer _historyScroll;
        private VBoxContainer _historyList;
        private bool _historyMode;
        private float _historyScrollCurrent;
        private float _historyScrollTarget;

        public override void _Ready()
        {
            MinWidth = 200;
            MinHeight = 120;
            base._Ready();

            StoryConfigUtil.Load();

            _chapterTitle = GetNodeOrNull<Label>("VBoxContainer/Content/ChapterTitle");
            _speakerLabel = GetNodeOrNull<Label>("VBoxContainer/Content/SpeakerLabel");
            _contentLabel = GetNodeOrNull<Label>("VBoxContainer/Content/ContentLabel");
            _hintLabel = GetNodeOrNull<Label>("VBoxContainer/Content/HintLabel");
            _titleBar = GetNodeOrNull<PanelContainer>("VBoxContainer/TitleBar");
            _contentContainer = GetNodeOrNull<VBoxContainer>("VBoxContainer/Content");
            _rootVBox = GetNodeOrNull<VBoxContainer>("VBoxContainer");

            if (_chapterTitle != null)
                _chapterTitle.Visible = false;

            ProcessMode = ProcessModeEnum.Always;
            AddToGroup("story_panel");

            SetupBeepPlayers();
            SetupHistoryView();

            ApplyRuntimeConfig();
            Visible = false;
            SetProcess(false);

            if (_pendingChapterId != 0)
            {
                int chapterId = _pendingChapterId;
                _pendingChapterId = 0;
                ShowChapter(chapterId);
            }
        }

        public void ShowChapter(int chapterId)
        {
            if (!IsNodeReady())
            {
                _pendingChapterId = chapterId;
                return;
            }

            _currentChapterId = chapterId;
            _currentDialogues = StoryConfigUtil.GetDialogues(chapterId);
            _currentIndex = 0;
            _history.Clear();
            ExitHistoryMode();

            // 章节标题已隐藏，不显示
            if (_chapterTitle != null)
                _chapterTitle.Visible = false;

            Visible = true;
            SetProcess(true);
            ShowDialogue(_currentIndex);
            PanelManager.Instance?.RequestFocus(this);
        }

        private void ShowDialogue(int index)
        {
            if (_currentDialogues == null || _currentDialogues.Count == 0)
            {
                ClosePanel();
                return;
            }

            if (index < 0 || index >= _currentDialogues.Count)
            {
                ClosePanel();
                return;
            }

            var dialogue = _currentDialogues[index];

            if (_speakerLabel != null)
            {
                bool hasSpeaker = !string.IsNullOrWhiteSpace(dialogue.speaker);
                _speakerLabel.Visible = hasSpeaker;
                _speakerLabel.Text = hasSpeaker ? $"【{dialogue.speaker}】" : "";
            }

            _typingFullText = dialogue.content ?? "";
            if (_contentLabel != null)
            {
                if (StoryTypewriterEnabled)
                {
                    _contentLabel.Text = "";
                    _typingVisibleCount = 0;
                    _typingElapsed = 0.0;
                    _isTyping = !string.IsNullOrEmpty(_typingFullText);
                }
                else
                {
                    _contentLabel.Text = _typingFullText;
                    _isTyping = false;
                }
            }

            if (_hintLabel != null)
                _hintLabel.Text = index < _currentDialogues.Count - 1 ? "按回车继续" : "按回车结束";

            CallDeferred(MethodName.RefreshMaxLinesVisible);
        }

        private void SetupBeepPlayers()
        {
            var stream = GD.Load<AudioStream>(BeepPath);
            _beepPlayers = new AudioStreamPlayer[BeepPlayerCount];
            for (int i = 0; i < BeepPlayerCount; i++)
            {
                var player = new AudioStreamPlayer
                {
                    Stream = stream,
                    Bus = "Master",
                    ProcessMode = ProcessModeEnum.Always,
                };
                AddChild(player);
                _beepPlayers[i] = player;
            }
        }

        private void SetupHistoryView()
        {
            if (_rootVBox == null)
                return;

            _historyScroll = new ScrollContainer
            {
                Visible = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };

            _historyList = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            };
            _historyList.AddThemeConstantOverride("separation", StoryHistoryEntrySpacing);

            _historyScroll.AddChild(_historyList);
            _rootVBox.AddChild(_historyScroll);
            if (_contentContainer != null)
                _rootVBox.MoveChild(_historyScroll, _contentContainer.GetIndex() + 1);
        }

        private void StopTyping()
        {
            _isTyping = false;
            _typingElapsed = 0.0;
        }

        private void FinishCurrentTyping()
        {
            if (!_isTyping)
                return;

            if (_contentLabel != null)
                _contentLabel.Text = _typingFullText;
            _typingVisibleCount = _typingFullText.Length;
            StopTyping();
        }

        private void Advance()
        {
            RecordCurrentDialogue();
            _currentIndex++;
            if (_currentIndex >= _currentDialogues.Count)
            {
                ClosePanel();
                return;
            }
            ShowDialogue(_currentIndex);
        }

        private void RecordCurrentDialogue()
        {
            if (_currentDialogues == null || _currentDialogues.Count == 0)
                return;
            if (_currentIndex < 0 || _currentIndex >= _currentDialogues.Count)
                return;

            var dialogue = _currentDialogues[_currentIndex];
            string content = _typingFullText;
            if (string.IsNullOrEmpty(content))
                content = dialogue.content ?? "";
            if (string.IsNullOrEmpty(content) && string.IsNullOrWhiteSpace(dialogue.speaker))
                return;

            _history.Add(new HistoryEntry(dialogue.speaker ?? "", content));
        }

        private void EnterHistoryMode()
        {
            if (_historyMode)
                return;

            if (_isTyping)
                FinishCurrentTyping();

            RebuildHistoryList();

            if (_contentContainer != null)
                _contentContainer.Visible = false;
            if (_historyScroll != null)
                _historyScroll.Visible = true;

            CallDeferred(MethodName.HistoryScrollToPreviousAndActivate);
        }

        private void HistoryScrollToPreviousAndActivate()
        {
            if (_historyScroll == null || _historyList == null)
            {
                _historyMode = true;
                return;
            }

            var vScroll = _historyScroll.GetVScrollBar();
            if (vScroll == null)
            {
                _historyMode = true;
                return;
            }

            float target;
            if (_historyList.GetChildCount() >= 2)
            {
                var secondLast = _historyList.GetChild(_historyList.GetChildCount() - 2);
                if (secondLast is Control c)
                    target = c.Position.Y;
                else
                    target = (float)vScroll.MaxValue;
            }
            else
            {
                target = (float)vScroll.MaxValue;
            }

            target = Mathf.Clamp(target, 0, (float)vScroll.MaxValue);
            _historyScrollTarget = target;
            _historyScrollCurrent = target;
            _historyScroll.ScrollVertical = (int)target;
            _historyMode = true;
        }

        private void ExitHistoryMode()
        {
            if (!_historyMode)
                return;

            if (_contentContainer != null)
                _contentContainer.Visible = true;
            if (_historyScroll != null)
                _historyScroll.Visible = false;

            if (_historyList != null)
            {
                foreach (var child in _historyList.GetChildren())
                {
                    child.QueueFree();
                }
            }

            _historyMode = false;
            _historyScrollCurrent = 0f;
            _historyScrollTarget = 0f;
        }

        private void RebuildHistoryList()
        {
            if (_historyList == null)
                return;

            foreach (var child in _historyList.GetChildren())
            {
                _historyList.RemoveChild(child);
                child.QueueFree();
            }

            var entries = new List<Control>();

            foreach (var entry in _history)
            {
                entries.Add(CreateHistoryEntry(entry.Speaker, entry.Content, false));
            }

            if (_currentDialogues != null && _currentIndex >= 0 && _currentIndex < _currentDialogues.Count)
            {
                var dialogue = _currentDialogues[_currentIndex];
                string content = _isTyping && _contentLabel != null ? _contentLabel.Text : _typingFullText;
                entries.Add(CreateHistoryEntry(dialogue.speaker ?? "", content, true));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                _historyList.AddChild(entries[i]);
                if (i < entries.Count - 1)
                    _historyList.AddChild(CreateHistorySeparator());
            }
        }

        private Control CreateHistorySeparator()
        {
            return new ColorRect
            {
                Color = new Color(0.5f, 0.5f, 0.5f, 0.4f),
                CustomMinimumSize = new Vector2(0, 1),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            };
        }

        private Control CreateHistoryEntry(string speaker, string content, bool isCurrent)
        {
            var entryBox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            };

            if (!string.IsNullOrWhiteSpace(speaker))
            {
                var speakerLabel = new Label
                {
                    Text = $"【{speaker}】",
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    AutowrapMode = TextServer.AutowrapMode.Off,
                };
                speakerLabel.AddThemeFontSizeOverride("font_size", Mathf.Max(10, StoryFontSize - 2));
                speakerLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f));
                entryBox.AddChild(speakerLabel);
            }

            var contentLabel = new Label
            {
                Text = content ?? "",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            contentLabel.AddThemeFontSizeOverride("font_size", StoryFontSize);
            if (isCurrent)
                contentLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
            else
                contentLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));

            entryBox.AddChild(contentLabel);
            return entryBox;
        }

        private void HistoryScrollToBottom()
        {
            if (_historyScroll == null)
                return;
            var vScroll = _historyScroll.GetVScrollBar();
            if (vScroll != null)
                _historyScroll.ScrollVertical = (int)vScroll.MaxValue;
        }

        private void ClosePanel()
        {
            StopTyping();
            ExitHistoryMode();
            Visible = false;
            SetProcess(false);
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            if (!_isTyping)
                return;

            double interval = StoryTypewriterIntervalMs / 1000.0;
            if (interval <= 0.0)
            {
                FinishCurrentTyping();
                return;
            }

            _typingElapsed += delta;
            while (_typingElapsed >= interval && _typingVisibleCount < _typingFullText.Length)
            {
                _typingElapsed -= interval;
                _typingVisibleCount++;
                if (_contentLabel != null)
                    _contentLabel.Text = _typingFullText.Substring(0, _typingVisibleCount);

                var player = _beepPlayers[_beepIndex];
                if (player != null)
                    player.Play();
                _beepIndex = (_beepIndex + 1) % BeepPlayerCount;
            }

            if (_typingVisibleCount >= _typingFullText.Length)
                StopTyping();

            UpdateHistoryScroll(delta);
        }

        private void UpdateHistoryScroll(double delta)
        {
            if (!_historyMode || _historyScroll == null)
                return;

            var vScroll = _historyScroll.GetVScrollBar();
            if (vScroll == null)
                return;

            float maxScroll = Mathf.Max(0, (float)(vScroll.MaxValue - vScroll.Page));
            _historyScrollTarget = Mathf.Clamp(_historyScrollTarget, 0, maxScroll);

            if (Mathf.Abs(_historyScrollCurrent - _historyScrollTarget) < 0.5f)
            {
                _historyScrollCurrent = _historyScrollTarget;
            }
            else
            {
                _historyScrollCurrent = Mathf.Lerp(_historyScrollCurrent, _historyScrollTarget, (float)delta * HistoryScrollSmoothSpeed);
            }

            _historyScroll.ScrollVertical = (int)_historyScrollCurrent;

            if (_historyScrollTarget >= maxScroll && Mathf.Abs(_historyScrollCurrent - _historyScrollTarget) < 1f)
                ExitHistoryMode();
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (!Visible)
                return;

            if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
                return;

            if (keyEvent.Keycode != Key.Enter && keyEvent.Keycode != Key.KpEnter)
                return;

            // 文本输入框聚焦时不触发剧情翻页
            if (UiUtils.IsGuiTextInputFocused(GetViewport()))
                return;

            GetViewport()?.SetInputAsHandled();

            if (_historyMode)
            {
                ExitHistoryMode();
                return;
            }

            if (_isTyping)
            {
                FinishCurrentTyping();
                return;
            }

            Advance();
        }

        public override void _GuiInput(InputEvent @event)
        {
            base._GuiInput(@event);

            if (!Visible)
                return;

            if (@event is not InputEventMouseButton mb || !mb.Pressed)
                return;

            if (mb.ButtonIndex != MouseButton.WheelUp && mb.ButtonIndex != MouseButton.WheelDown)
                return;

            AcceptEvent();

            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                if (!_historyMode)
                {
                    bool hasCurrent = _currentDialogues != null && _currentIndex >= 0 && _currentIndex < _currentDialogues.Count;
                    if (_history.Count == 0 && !hasCurrent)
                        return;
                    EnterHistoryMode();
                    return;
                }

                if (_historyScroll == null)
                    return;
                float scrollAmount = Mathf.Max(1, StoryFontSize + StoryHistoryEntrySpacing + 8);
                _historyScrollTarget = Mathf.Max(0, _historyScrollTarget - scrollAmount);
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                if (!_historyMode)
                    return;
                if (_historyScroll == null)
                    return;

                var vScroll = _historyScroll.GetVScrollBar();
                float maxScroll = vScroll != null ? (float)(vScroll.MaxValue - vScroll.Page) : _historyScroll.ScrollVertical;
                float scrollAmount = Mathf.Max(1, StoryFontSize + StoryHistoryEntrySpacing + 8);
                float nextTarget = _historyScrollTarget + scrollAmount;

                if (nextTarget >= maxScroll)
                {
                    _historyScrollTarget = maxScroll;
                    return;
                }

                _historyScrollTarget = nextTarget;
            }
        }

        public void ApplyRuntimeConfig()
        {
            Size = new Vector2(StoryPanelWidth, StoryPanelHeight);
            if (_chapterTitle != null)
                _chapterTitle.AddThemeFontSizeOverride("font_size", StoryFontSize + 2);
            if (_speakerLabel != null)
                _speakerLabel.AddThemeFontSizeOverride("font_size", StoryFontSize);
            if (_contentLabel != null)
                _contentLabel.AddThemeFontSizeOverride("font_size", StoryFontSize);
            if (_hintLabel != null)
                _hintLabel.AddThemeFontSizeOverride("font_size", Mathf.Max(10, StoryFontSize - 4));

            ApplyPanelStyle();
            ApplyContentPadding();
            ApplyHistorySpacing();
            CallDeferred(MethodName.RefreshMaxLinesVisible);
        }

        private void ApplyHistorySpacing()
        {
            if (_historyList != null)
                _historyList.AddThemeConstantOverride("separation", Mathf.Max(0, StoryHistoryEntrySpacing));
        }

        private void ApplyPanelStyle()
        {
            float alpha = Mathf.Clamp(StoryPanelAlpha / 100f, 0f, 1f);

            var panelStyle = new StyleBoxFlat
            {
                BgColor = new Color(BasePanelBgColor, alpha),
                BorderColor = new Color(BasePanelBorderColor, alpha),
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomRight = 10,
                CornerRadiusBottomLeft = 10,
            };
            AddThemeStyleboxOverride("panel", panelStyle);

            if (_titleBar != null)
            {
                var titleStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
                _titleBar.AddThemeStyleboxOverride("panel", titleStyle);
            }
        }

        private void ApplyContentPadding()
        {
            if (_contentContainer == null)
                return;

            int padding = Mathf.Max(0, StoryContentPadding);
            _contentContainer.AddThemeConstantOverride("margin_left", padding);
            _contentContainer.AddThemeConstantOverride("margin_top", padding);
            _contentContainer.AddThemeConstantOverride("margin_right", padding);
            _contentContainer.AddThemeConstantOverride("margin_bottom", padding);
        }

        private void RefreshMaxLinesVisible()
        {
            if (_contentLabel == null)
                return;

            float lineHeight = _contentLabel.GetLineHeight();
            if (lineHeight <= 0)
                lineHeight = StoryFontSize + 4;

            int maxLines = Mathf.Max(1, (int)(_contentLabel.Size.Y / lineHeight));
            _contentLabel.MaxLinesVisible = maxLines;
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationResized)
                RefreshMaxLinesVisible();
        }
    }
}
