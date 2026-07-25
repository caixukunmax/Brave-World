using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityClientSharp.Map.Rendering;

namespace UnityClientSharp.UI
{
    /// <summary>
    /// GM 面板构建 — 移植自 Godot GMPanel.Build.cs。
    /// 布局（自上而下）：命令栏（输入框+执行）→ 参数提示行 → 补全下拉 → 响应日志区（Unity 增强）
    /// → 分组滚动区 → 内联命令编辑器 → 内联分组编辑器 → 工具栏（+ 添加分组）。
    /// 分组命令按钮的流式布局：Godot HFlowContainer 在 uGUI 无等价物，用"估算宽度手动装行"
    /// （VLG of HLG）实现；宽度估算 CJK 13px / 其它 7px + 24 内边距（fontSize 12）。
    /// 参数暗字提示：Godot 是在输入框最后一个逗号后画幽灵文本，uGUI 输入框内叠加文本定位不可靠，
    /// 适配为命令栏下方的灰色提示行（信息等价）。
    /// </summary>
    public partial class GMPanelHud
    {
        private const int PanelWidth = 420;  // 对齐 Godot gm_panel.tscn 初始尺寸（offset 920-500）
        private const int PanelHeight = 480; // 对齐 Godot gm_panel.tscn 初始尺寸（offset 540-60）
        private const int FlowWidth = PanelWidth - 40; // 内容边距 + 滚动条预留

        private void Build()
        {
            UiEventSystemUtil.EnsureExists();

            var canvasGo = new GameObject("GMPanelCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = GamePanelManager.BaseSortingOrder;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            _panelRoot = canvasGo;

            var panelGo = CreateUI(canvasGo.transform, "Panel");
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 0.95f);
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            // 对齐 Godot MinHeight = 300
            DraggablePanel.MakeDraggable(panelRt, "GM面板", new Vector2(400, 300), () => SetVisible(false));

            var contentGo = CreateUI(panelGo.transform, "Content");
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(8, 8);
            contentRt.offsetMax = new Vector2(-8, -30);
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            // childControlHeight 必须为 true：uGUI 源码 GetChildSizes 在 controlSize=false 时
            // 直接取 child.sizeDelta（运行时新建节点恒为 0），LayoutElement 的 preferred/flexible
            // 全部失效，整块内容塌缩成 0 高（本次布局事故根因）
            vlg.childControlHeight = true;

            BuildCommandBar(contentGo.transform);
            BuildSuggestionMenu(contentGo.transform);
            BuildResponseLog(contentGo.transform);
            BuildGroupScroll(contentGo.transform);
            BuildInlineCommandEditor(contentGo.transform);
            BuildInlineGroupEditor(contentGo.transform);
            BuildToolbar(contentGo.transform);

            if (!LoadGmConfig())
                AddDefaultGroups();
            else
                RebuildGroupUI();

            // 订阅先于使用；Godot 端同样只订阅 GmResponse
            var nm = Net.NetworkManager.Instance;
            if (nm != null)
                nm.GmResponse += OnGmResponse;

            SetVisible(false); // 对齐 Godot DraggablePanel 默认隐藏

            GamePanelManager.Instance?.RegisterPanel(this);
            GamePanelManager.Instance?.RegisterHotkey(KeyCode.F2, this);
        }

        // ============ 命令栏（对齐 BuildCommandBar） ============

        private void BuildCommandBar(Transform parent)
        {
            var row = CreateUI(parent, "CommandBar");
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 32;
            rowLe.minHeight = 32;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.childForceExpandWidth = false;
            // 禁止 childForceExpandHeight：HLG 会因此向父 VLG 报告 flexibleHeight=1，
            // 命令栏会分走弹性余量被拉高（第二轮事故根因）。子元素改用 LayoutElement 定高。
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            _cmdEdit = CreateInput(row.transform, "CmdInput", "例如 additem,1001,20");
            var inputLe = _cmdEdit.gameObject.AddComponent<LayoutElement>();
            inputLe.flexibleWidth = 1;
            inputLe.preferredHeight = 32;
            inputLe.minHeight = 32;
            _cmdEdit.onValueChanged.AddListener(OnCmdTextChanged);
            _cmdEdit.onSubmit.AddListener(_ => OnExecPressed());
            // 上下键留给命令历史，不做 UI 导航（对齐 Godot LineEdit 无导航行为）
            _cmdEdit.navigation = new Navigation { mode = Navigation.Mode.None };

            var execBtn = CreateButton(row.transform, "Exec", "执行", 60, 32);
            execBtn.onClick.AddListener(OnExecPressed);

            // 参数暗字提示（Godot 幽灵文本 → 命令栏下方灰色提示行）
            var hintGo = CreateUI(parent, "CmdHint");
            hintGo.AddComponent<LayoutElement>().preferredHeight = 16;
            _cmdHint = hintGo.AddComponent<TextMeshProUGUI>();
            _cmdHint.fontSize = 11;
            _cmdHint.color = new Color(1f, 1f, 1f, 0.45f);
            _cmdHint.alignment = TextAlignmentOptions.Left;
            _cmdHint.raycastTarget = false;
            FontUtil.ApplyCjkFont(_cmdHint);
            hintGo.SetActive(false);
        }

        // ============ 补全下拉（对齐 BuildSuggestionMenu/BuildSuggestionButton） ============

        private void BuildSuggestionMenu(Transform parent)
        {
            var go = CreateUI(parent, "SuggestPanel");
            _suggestPanel = (RectTransform)go.transform;
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            // 见 Build() 注释：必须 true，否则候选行高度塌缩为 0，且外层 VLG 读不到下拉区高度
            vlg.childControlHeight = true;
            go.SetActive(false);
        }

        private void BuildSuggestionButton(GmCommandSuggester.Suggestion suggestion, int index)
        {
            var go = CreateUI(_suggestPanel, "Suggestion");
            go.AddComponent<LayoutElement>().preferredHeight = 26;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.25f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int capturedIndex = index;
            btn.onClick.AddListener(() => OnSuggestionSelected(capturedIndex));
            var tmp = CreateText(go.transform, "Text", suggestion.DisplayText, 16, Color.white);
            tmp.alignment = TextAlignmentOptions.Left;
            var tmpRt = (RectTransform)tmp.transform;
            Stretch(tmpRt);
            tmpRt.offsetMin = new Vector2(6, 0);
        }

        // ============ 响应日志区（Unity 增强） ============

        private void BuildResponseLog(Transform parent)
        {
            var scrollGo = CreateUI(parent, "ResponseLog");
            var le = scrollGo.AddComponent<LayoutElement>();
            le.preferredHeight = 90;
            le.minHeight = 90; // 固定高，面板被压小时也不被挤没
            var bg = scrollGo.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.25f);
            scrollGo.AddComponent<RectMask2D>();
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            _logScroll = scroll;

            var contentGo = CreateUI(scrollGo.transform, "Content");
            _logContent = (RectTransform)contentGo.transform;
            _logContent.anchorMin = new Vector2(0, 1);
            _logContent.anchorMax = new Vector2(1, 1);
            _logContent.pivot = new Vector2(0.5f, 1);
            _logContent.anchoredPosition = Vector2.zero;
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true; // 见 Build() 注释：false 会让日志行高度塌缩为 0
            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _logContent;
        }

        // ============ 分组滚动区（对齐 BuildGroupScroll + RebuildGroupUI） ============

        private void BuildGroupScroll(Transform parent)
        {
            var scrollGo = CreateUI(parent, "GroupScroll");
            var le = scrollGo.AddComponent<LayoutElement>();
            le.flexibleHeight = 1;
            le.minHeight = 60; // 弹性吃满剩余空间，但保留最小可见高度
            var bg = scrollGo.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.15f);
            scrollGo.AddComponent<RectMask2D>();
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var contentGo = CreateUI(scrollGo.transform, "GroupContainer");
            _groupContainer = (RectTransform)contentGo.transform;
            _groupContainer.anchorMin = new Vector2(0, 1);
            _groupContainer.anchorMax = new Vector2(1, 1);
            _groupContainer.pivot = new Vector2(0.5f, 1);
            _groupContainer.anchoredPosition = Vector2.zero;
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            // 见 Build() 注释：false 会让组头/按钮行高度塌缩为 0，
            // 且 ContentSizeFitter 读到的内容高恒为 0（分组区空白的根因）
            vlg.childControlHeight = true;
            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _groupContainer;
        }

        private void RebuildGroupUI()
        {
            foreach (Transform child in _groupContainer)
                Destroy(child.gameObject);

            foreach (var group in _groups)
            {
                BuildGroupHeader(_groupContainer, group);
                BuildGroupFlow(_groupContainer, group);
            }
        }

        /// <summary>分组标题行（对齐 BuildGroupHeader）：折叠按钮 + 组名 + 添加命令 + 删除分组。</summary>
        private void BuildGroupHeader(Transform parent, GmGroup group)
        {
            var header = CreateUI(parent, "Header_" + group.Name);
            header.AddComponent<LayoutElement>().preferredHeight = 24;
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var collapseBtn = CreateButton(header.transform, "Collapse", group.Collapsed ? ">" : "v", 24, 24);
            collapseBtn.onClick.AddListener(() =>
            {
                group.Collapsed = !group.Collapsed;
                RebuildGroupUI();
                SaveGmConfig();
            });

            var nameTmp = CreateText(header.transform, "Name", group.Name, 13, new Color(1f, 0.9f, 0.6f));
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var addCmdBtn = CreateButton(header.transform, "AddCmd", "+", 28, 24);
            addCmdBtn.onClick.AddListener(() => ShowAddCommandRow(group));

            var delGroupBtn = CreateButton(header.transform, "DelGroup", "x", 28, 24);
            delGroupBtn.onClick.AddListener(() =>
            {
                _groups.Remove(group);
                CancelAddCommand();
                RebuildGroupUI();
                SaveGmConfig();
            });
        }

        /// <summary>命令按钮流（对齐 BuildGroupFlow 的 HFlowContainer）：按估算宽度手动装行。</summary>
        private void BuildGroupFlow(Transform parent, GmGroup group)
        {
            if (group.Collapsed)
                return;

            HorizontalLayoutGroup currentRow = null;
            float currentRowWidth = 0f;

            foreach (var command in group.Commands)
            {
                float btnWidth = EstimateButtonWidth(command.Label);
                if (currentRow == null || currentRowWidth + 4 + btnWidth > FlowWidth)
                {
                    var rowGo = CreateUI(parent, "FlowRow");
                    rowGo.AddComponent<LayoutElement>().preferredHeight = 26;
                    currentRow = rowGo.AddComponent<HorizontalLayoutGroup>();
                    currentRow.spacing = 4;
                    currentRow.childForceExpandWidth = false;
                    currentRow.childForceExpandHeight = true;
                    currentRow.childControlWidth = true;
                    currentRow.childControlHeight = true;
                    currentRowWidth = 0;
                }

                BuildCommandButton(currentRow.transform, group, command, btnWidth);
                currentRowWidth += (currentRowWidth > 0 ? 4 : 0) + btnWidth;
            }
        }

        /// <summary>按钮宽度估算：CJK 13px / 其它 7px + 24 内边距（fontSize 12 下与 TMP 实测接近）。</summary>
        private static float EstimateButtonWidth(string text)
        {
            float w = 24;
            foreach (char c in text)
                w += c > 0x7F ? 16 : 8;
            return w;
        }

        /// <summary>单个命令按钮（对齐 BuildCommandButton）：左键填入输入框，右键弹出编辑/删除菜单。</summary>
        private void BuildCommandButton(Transform parent, GmGroup group, GmCommand command, float width)
        {
            var go = CreateUI(parent, "Cmd_" + command.Label);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.28f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                _suppressSuggestionUpdate = true;
                _cmdEdit.text = command.Cmd;
                _cmdEdit.ActivateInputField();
                _cmdEdit.MoveTextEnd(false);
            });

            var rightClick = go.AddComponent<RightClickHandler>();
            rightClick.OnRightClick = () => ShowCommandContextMenu(group, command);

            var tmp = CreateText(go.transform, "Text", command.Label, 16, Color.white);
            Stretch((RectTransform)tmp.transform);
        }

        /// <summary>右键菜单（对齐 BuildContextMenu + GuiInput 右键处理）：编辑 / 删除。</summary>
        private void ShowCommandContextMenu(GmGroup group, GmCommand command)
        {
            CloseContextMenu();
            _menuCmd = command;
            _menuGroup = group;

            var backdrop = CreateUI(_panelRoot.transform, "ContextBackdrop");
            var backdropRt = (RectTransform)backdrop.transform;
            backdropRt.anchorMin = Vector2.zero;
            backdropRt.anchorMax = Vector2.one;
            backdropRt.offsetMin = Vector2.zero;
            backdropRt.offsetMax = Vector2.zero;
            var backdropImg = backdrop.AddComponent<Image>();
            backdropImg.color = new Color(0, 0, 0, 0);
            backdropImg.raycastTarget = true;
            var backdropBtn = backdrop.AddComponent<Button>();
            backdropBtn.onClick.AddListener(CloseContextMenu);
            _contextMenu = backdrop;

            var menuGo = CreateUI(backdrop.transform, "Menu");
            var menuRt = (RectTransform)menuGo.transform;
            menuRt.pivot = new Vector2(0, 1);
            var menuBg = menuGo.AddComponent<Image>();
            menuBg.color = new Color(0.12f, 0.12f, 0.18f, 0.98f);
            var menuOutline = menuGo.AddComponent<Outline>();
            menuOutline.effectColor = new Color(0.4f, 0.4f, 0.5f);
            menuOutline.effectDistance = new Vector2(1, -1);
            var vlg = menuGo.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            AddMenuItem(menuGo.transform, "编辑", () => ShowEditCommandRow(_menuGroup, _menuCmd));
            AddMenuItem(menuGo.transform, "删除", () =>
            {
                if (_menuGroup != null && _menuCmd != null)
                {
                    _menuGroup.Commands.Remove(_menuCmd);
                    RebuildGroupUI();
                    SaveGmConfig();
                }
                _menuCmd = null;
                _menuGroup = null;
            });

            menuRt.sizeDelta = new Vector2(100, 2 * 26 + 4);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_panelRoot.transform, Input.mousePosition, null, out var local);
            var canvasRect = ((RectTransform)_panelRoot.transform).rect;
            local.x = Mathf.Clamp(local.x, canvasRect.xMin, canvasRect.xMax - menuRt.sizeDelta.x);
            local.y = Mathf.Clamp(local.y, canvasRect.yMin + menuRt.sizeDelta.y, canvasRect.yMax);
            menuRt.anchoredPosition = local;
        }

        private void AddMenuItem(Transform parent, string label, System.Action onClick)
        {
            var go = CreateUI(parent, "Item_" + label);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.26f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                onClick();
                CloseContextMenu();
            });
            var tmp = CreateText(go.transform, "Text", label, 16, Color.white);
            Stretch((RectTransform)tmp.transform);
        }

        private void CloseContextMenu()
        {
            if (_contextMenu != null)
            {
                Destroy(_contextMenu);
                _contextMenu = null;
            }
        }

        // ============ 内联编辑器（对齐 BuildInlineCommandEditor/BuildInlineGroupEditor） ============

        private void BuildInlineCommandEditor(Transform parent)
        {
            _addCmdRow = CreateUI(parent, "AddCmdRow");
            _addCmdRow.AddComponent<LayoutElement>().preferredHeight = 28;
            var hlg = _addCmdRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.childForceExpandWidth = false;
            // 见 BuildCommandBar 注释：forceExpand 会让 HLG 向父 VLG 报告 flexibleHeight=1 抢余量
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            _addCmdLabelEdit = CreateInput(_addCmdRow.transform, "LabelInput", "标签");
            var labelLe = _addCmdLabelEdit.gameObject.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1;
            labelLe.preferredHeight = 28;

            _addCmdEdit = CreateInput(_addCmdRow.transform, "CmdInput", "命令");
            var cmdLe = _addCmdEdit.gameObject.AddComponent<LayoutElement>();
            cmdLe.flexibleWidth = 1;
            cmdLe.preferredHeight = 28;
            _addCmdEdit.onSubmit.AddListener(_ => ConfirmAddCommand());

            var confirmBtn = CreateButton(_addCmdRow.transform, "Confirm", "确定", 44, 28);
            confirmBtn.onClick.AddListener(ConfirmAddCommand);
            var cancelBtn = CreateButton(_addCmdRow.transform, "Cancel", "取消", 44, 28);
            cancelBtn.onClick.AddListener(CancelAddCommand);

            _addCmdRow.SetActive(false);
        }

        private void BuildInlineGroupEditor(Transform parent)
        {
            _addGroupRow = CreateUI(parent, "AddGroupRow");
            _addGroupRow.AddComponent<LayoutElement>().preferredHeight = 28;
            var hlg = _addGroupRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.childForceExpandWidth = false;
            // 见 BuildCommandBar 注释：forceExpand 会让 HLG 向父 VLG 报告 flexibleHeight=1 抢余量
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            _addGroupEdit = CreateInput(_addGroupRow.transform, "NameInput", "分组名称");
            var nameLe = _addGroupEdit.gameObject.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1;
            nameLe.preferredHeight = 28;
            _addGroupEdit.onSubmit.AddListener(_ => ConfirmAddGroup());

            var confirmBtn = CreateButton(_addGroupRow.transform, "Confirm", "确定", 44, 28);
            confirmBtn.onClick.AddListener(ConfirmAddGroup);
            var cancelBtn = CreateButton(_addGroupRow.transform, "Cancel", "取消", 44, 28);
            cancelBtn.onClick.AddListener(CancelAddGroup);

            _addGroupRow.SetActive(false);
        }

        private void OnAddGroupPressed()
        {
            CancelAddCommand();
            _addGroupRow.SetActive(true);
            _addGroupEdit.text = "";
            _addGroupEdit.ActivateInputField();
        }

        private void ConfirmAddGroup()
        {
            string name = _addGroupEdit.text.Trim();
            if (name != "")
            {
                AddGroupData(name);
                RebuildGroupUI();
                SaveGmConfig();
            }

            _addGroupRow.SetActive(false);
        }

        private void CancelAddGroup()
        {
            _addGroupRow.SetActive(false);
        }

        private void ShowAddCommandRow(GmGroup group)
        {
            CancelAddGroup();
            _addCmdTarget = group;
            _editCmdTarget = null;
            _addCmdRow.SetActive(true);
            _addCmdLabelEdit.text = "";
            _addCmdEdit.text = "";
            ((TextMeshProUGUI)_addCmdLabelEdit.placeholder).text = $"标签 ({group.Name})";
            _addCmdLabelEdit.ActivateInputField();
        }

        private void ShowEditCommandRow(GmGroup group, GmCommand command)
        {
            CancelAddGroup();
            _addCmdTarget = group;
            _editCmdTarget = command;
            _addCmdRow.SetActive(true);
            _addCmdLabelEdit.text = command.Label;
            _addCmdEdit.text = command.Cmd;
            _addCmdLabelEdit.ActivateInputField();
        }

        private void ConfirmAddCommand()
        {
            if (_addCmdTarget == null)
                return;

            string label = _addCmdLabelEdit.text.Trim();
            string cmd = _addCmdEdit.text.Trim();
            if (label == "" || cmd == "")
                return;

            if (_editCmdTarget != null)
            {
                _editCmdTarget.Label = label;
                _editCmdTarget.Cmd = cmd;
            }
            else
            {
                AddCommandData(_addCmdTarget, label, cmd);
            }

            RebuildGroupUI();
            SaveGmConfig();
            CancelAddCommand();
        }

        private void CancelAddCommand()
        {
            if (_addCmdRow != null) _addCmdRow.SetActive(false);
            _addCmdTarget = null;
            _editCmdTarget = null;
        }

        // ============ 工具栏（对齐 BuildToolbar） ============

        private void BuildToolbar(Transform parent)
        {
            var toolbar = CreateUI(parent, "Toolbar");
            var toolbarLe = toolbar.AddComponent<LayoutElement>();
            toolbarLe.preferredHeight = 32;
            toolbarLe.minHeight = 32;
            var hlg = toolbar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4;
            hlg.childForceExpandWidth = false;
            // 不拉伸按钮高度：按钮用自己的 preferredHeight（否则被撑满整行甚至更高）
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var addGroupBtn = CreateButton(toolbar.transform, "AddGroup", "+ 添加分组", 90, 26);
            addGroupBtn.onClick.AddListener(OnAddGroupPressed);
        }

        // ============ 基础控件 ============

        /// <summary>输入框（结构对齐 LoginPanel.CreateInput）。</summary>
        private static TMP_InputField CreateInput(Transform parent, string name, string placeholder)
        {
            var go = CreateUI(parent, name);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.22f, 1f);

            var area = CreateUI(go.transform, "TextArea");
            var areaRt = (RectTransform)area.transform;
            areaRt.anchorMin = Vector2.zero;
            areaRt.anchorMax = Vector2.one;
            areaRt.offsetMin = new Vector2(8, 3);
            areaRt.offsetMax = new Vector2(-8, -3);
            area.AddComponent<RectMask2D>();

            var textGo = CreateUI(area.transform, "Text");
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            // 单行输入框：关闭换行，防止 TMP 按窄宽度换行把 preferredHeight 撑大、
            // 经 TMP_InputField(ILayoutElement) 泄漏给父布局组拉高整行
            tmp.enableWordWrapping = false;
            FontUtil.ApplyCjkFont(tmp);
            Stretch((RectTransform)textGo.transform);

            var phGo = CreateUI(area.transform, "Placeholder");
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            ph.text = placeholder;
            ph.fontSize = 16;
            ph.color = new Color(1f, 1f, 1f, 0.35f);
            ph.alignment = TextAlignmentOptions.Left;
            ph.raycastTarget = false;
            ph.enableWordWrapping = false;
            FontUtil.ApplyCjkFont(ph);
            Stretch((RectTransform)phGo.transform);

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = img;
            input.textViewport = areaRt;
            input.textComponent = tmp;
            input.placeholder = ph;
            input.lineType = TMP_InputField.LineType.SingleLine; // 显式单行（默认值，写明防回归）
            return input;
        }

        private static Button CreateButton(Transform parent, string name, string label, int preferredWidth, int preferredHeight)
        {
            var go = CreateUI(parent, name);
            var le = go.AddComponent<LayoutElement>();
            if (preferredWidth > 0) le.preferredWidth = preferredWidth;
            if (preferredHeight > 0) le.preferredHeight = preferredHeight;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.3f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var tmp = CreateText(go.transform, "Text", label, 16, Color.white);
            Stretch((RectTransform)tmp.transform);
            return btn;
        }

        /// <summary>右键检测（uGUI 无 Button 右键事件，挂组件补 IPointerClickHandler）。</summary>
        private class RightClickHandler : MonoBehaviour, IPointerClickHandler
        {
            public System.Action OnRightClick;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                    OnRightClick?.Invoke();
            }
        }
    }
}
