using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.UI.Shared;
using AshesofaDyingWorld.Core.Skills;
using AshesofaDyingWorld.UI.HUD.Skills;

namespace AshesofaDyingWorld.UI.Skills
{
    /// <summary>
    /// Panel Kỹ năng riêng được mở từ menu chính.
    ///
    /// Panel này chịu trách nhiệm phát triển kỹ năng theo nhánh. Nó không thay thế
    /// tab Kỹ năng trong CharacterDetailUI; tab kia tiếp tục hiển thị những skill
    /// nhân vật đã sở hữu. Hai màn hình có hai công việc khác nhau, cuối cùng cũng
    /// được phép sống riêng như những người trưởng thành tương đối ổn định.
    /// </summary>
    public partial class SkillTreePanel : Panel
    {
        private HBoxContainer _characterSelector;
        private HBoxContainer _branchSelector;
        private Label _characterNameLabel;
        private Label _characterLevelLabel;
        private Label _skillPointsLabel;
        private Label _branchDescriptionLabel;
        private SkillTreeGraphView _graphView;

        private TextureRect _detailIcon;
        private Label _detailIconFallback;
        private Label _detailNameLabel;
        private Label _detailStateLabel;
        private Label _detailDescriptionLabel;
        private Label _detailRequirementsLabel;
        private Label _detailCostLabel;
        private Label _detailCombatLabel;
        private Label _actionMessageLabel;
        private Button _unlockButton;

        private readonly List<Button> _characterButtons = new();
        private readonly List<Button> _branchButtons = new();

        private int _selectedPartyIndex;
        private int _selectedBranchIndex;
        private SkillTreeNodeData _selectedNode;
        private CharacterSkillTreeData _currentTree;
        private SkillTreeBranchData _currentBranch;
        private PlayerSkillCollection _currentCollection;

        public override void _Ready()
        {
            InventoryPanelChrome.ApplyPanelSize(this);
            AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            BuildUi();

            VisibilityChanged += OnVisibilityChanged;
            BindPlayerManagerSignals();
            RefreshFromCurrentParty();
        }

        public override void _ExitTree()
        {
            UnbindPlayerManagerSignals();
        }

        /// <summary>
        /// Được GameMenuButton gọi mỗi lần mở panel để dữ liệu luôn phản ánh party hiện tại.
        /// </summary>
        public void RefreshFromCurrentParty()
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null || manager.PartyMembers.Count == 0)
            {
                ShowEmptyPanel("Chưa có nhân vật trong tổ đội.");
                return;
            }

            if (_selectedPartyIndex < 0 || _selectedPartyIndex >= manager.PartyMembers.Count)
            {
                _selectedPartyIndex = Mathf.Clamp(manager.ActiveCharacterIndex, 0, manager.PartyMembers.Count - 1);
            }

            BuildCharacterSelector();
            RefreshSelectedCharacter();
        }

        private void BuildUi()
        {
            VBoxContainer root = InventoryPanelChrome.BuildWindowShell(this);
            root.AddChild(BuildHeader());
            root.AddChild(BuildCharacterBar());
            root.AddChild(BuildBranchBar());
            root.AddChild(BuildBody());
        }

        private Control BuildHeader()
        {
            PanelContainer header = InventoryPanelChrome.CreateHeader(out HBoxContainer row);

            Label title = InventoryPanelChrome.CreateLabel("PHÁT TRIỂN KỸ NĂNG", 18, InventoryPanelChrome.MainTextColor);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            title.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(title);

            _characterNameLabel = InventoryPanelChrome.CreateLabel("NHÂN VẬT", 15, InventoryPanelChrome.MainTextColor);
            _characterNameLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(_characterNameLabel);

            row.AddChild(InventoryPanelChrome.CreateLabel("·", 13, InventoryPanelChrome.MutedTextColor));

            _characterLevelLabel = InventoryPanelChrome.CreateLabel("Cấp 01", 13, InventoryPanelChrome.MainTextColor);
            _characterLevelLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(_characterLevelLabel);

            row.AddChild(InventoryPanelChrome.CreateLabel("·", 13, InventoryPanelChrome.MutedTextColor));

            _skillPointsLabel = InventoryPanelChrome.CreateLabel("Điểm KN: 0", 13, InventoryPanelChrome.AccentColor);
            _skillPointsLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(_skillPointsLabel);

            row.AddChild(InventoryPanelChrome.CreateCloseButton(Hide));
            return header;
        }

        private Control BuildCharacterBar()
        {
            PanelContainer panel = InventoryPanelChrome.CreateTabBar(out HBoxContainer row, 10);
            Label caption = InventoryPanelChrome.CreateLabel("NHÂN VẬT", 12, InventoryPanelChrome.MutedTextColor);
            caption.CustomMinimumSize = new Vector2(82, 0);
            caption.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(caption);

            _characterSelector = new HBoxContainer();
            _characterSelector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _characterSelector.AddThemeConstantOverride("separation", 6);
            row.AddChild(_characterSelector);
            return panel;
        }

        private Control BuildBranchBar()
        {
            PanelContainer panel = new();
            panel.CustomMinimumSize = new Vector2(0, 48);
            panel.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreateDetailSectionStyle());

            MarginContainer margin = new();
            margin.AddThemeConstantOverride("margin_left", 10);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_right", 10);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            panel.AddChild(margin);

            HBoxContainer row = new();
            row.AddThemeConstantOverride("separation", 8);
            margin.AddChild(row);

            Label caption = InventoryPanelChrome.CreateLabel("NHÁNH", 12, InventoryPanelChrome.MutedTextColor);
            caption.CustomMinimumSize = new Vector2(82, 0);
            caption.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(caption);

            _branchSelector = new HBoxContainer();
            _branchSelector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _branchSelector.AddThemeConstantOverride("separation", 6);
            row.AddChild(_branchSelector);

            _branchDescriptionLabel = InventoryPanelChrome.CreateLabel("", 11, InventoryPanelChrome.MutedTextColor);
            _branchDescriptionLabel.CustomMinimumSize = new Vector2(300, 0);
            _branchDescriptionLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _branchDescriptionLabel.VerticalAlignment = VerticalAlignment.Center;
            _branchDescriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(_branchDescriptionLabel);
            return panel;
        }

        private Control BuildBody()
        {
            PanelContainer bodyPanel = new();
            bodyPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            bodyPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            bodyPanel.AddThemeStyleboxOverride("panel", CreateBodyStyle());

            MarginContainer outerMargin = new();
            outerMargin.AddThemeConstantOverride("margin_left", 10);
            outerMargin.AddThemeConstantOverride("margin_top", 10);
            outerMargin.AddThemeConstantOverride("margin_right", 10);
            outerMargin.AddThemeConstantOverride("margin_bottom", 10);
            bodyPanel.AddChild(outerMargin);

            HBoxContainer body = new();
            body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            body.SizeFlagsVertical = SizeFlags.ExpandFill;
            body.AddThemeConstantOverride("separation", 10);
            outerMargin.AddChild(body);

            body.AddChild(BuildGraphSection());
            body.AddChild(BuildDetailSection());
            return bodyPanel;
        }

        private Control BuildGraphSection()
        {
            PanelContainer frame = new();
            frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            frame.SizeFlagsVertical = SizeFlags.ExpandFill;
            frame.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreatePreviewStyle());

            VBoxContainer column = new();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddThemeConstantOverride("separation", 4);
            frame.AddChild(column);

            Label title = InventoryPanelChrome.CreateLabel("CÂY KỸ NĂNG", 13, InventoryPanelChrome.MainTextColor);
            title.CustomMinimumSize = new Vector2(0, 30);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.VerticalAlignment = VerticalAlignment.Center;
            column.AddChild(title);
            column.AddChild(InventoryPanelChrome.CreateDivider());

            ScrollContainer scroll = new();
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
            scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
            column.AddChild(scroll);

            _graphView = new SkillTreeGraphView();
            _graphView.NodeSelected += OnNodeSelected;
            scroll.AddChild(_graphView);
            return frame;
        }

        private Control BuildDetailSection()
        {
            PanelContainer frame = new();
            frame.CustomMinimumSize = new Vector2(330, 0);
            frame.SizeFlagsVertical = SizeFlags.ExpandFill;
            frame.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreateDetailSectionStyle());

            MarginContainer margin = new();
            margin.AddThemeConstantOverride("margin_left", 14);
            margin.AddThemeConstantOverride("margin_top", 12);
            margin.AddThemeConstantOverride("margin_right", 14);
            margin.AddThemeConstantOverride("margin_bottom", 12);
            frame.AddChild(margin);

            VBoxContainer column = new();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddThemeConstantOverride("separation", 8);
            margin.AddChild(column);

            Label title = InventoryPanelChrome.CreateLabel("CHI TIẾT", 14, InventoryPanelChrome.MainTextColor);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(title);
            column.AddChild(InventoryPanelChrome.CreateDivider());

            CenterContainer iconHolder = new();
            iconHolder.CustomMinimumSize = new Vector2(0, 86);
            column.AddChild(iconHolder);

            PanelContainer iconFrame = new();
            iconFrame.CustomMinimumSize = new Vector2(72, 72);
            iconFrame.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreatePreviewStyle());
            iconHolder.AddChild(iconFrame);

            CenterContainer iconCenter = new();
            iconFrame.AddChild(iconCenter);

            _detailIcon = new TextureRect
            {
                CustomMinimumSize = new Vector2(54, 54),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest
            };
            iconCenter.AddChild(_detailIcon);

            _detailIconFallback = InventoryPanelChrome.CreateLabel("?", 22, InventoryPanelChrome.MutedTextColor);
            _detailIconFallback.HorizontalAlignment = HorizontalAlignment.Center;
            iconCenter.AddChild(_detailIconFallback);

            _detailNameLabel = InventoryPanelChrome.CreateLabel("Chọn một kỹ năng", 17, InventoryPanelChrome.MainTextColor);
            _detailNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _detailNameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(_detailNameLabel);

            _detailStateLabel = InventoryPanelChrome.CreateLabel("", 12, InventoryPanelChrome.AccentColor);
            _detailStateLabel.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(_detailStateLabel);

            _detailDescriptionLabel = CreateWrappedDetailLabel();
            _detailDescriptionLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddChild(_detailDescriptionLabel);

            column.AddChild(InventoryPanelChrome.CreateDivider(true));
            _detailRequirementsLabel = CreateWrappedDetailLabel();
            column.AddChild(_detailRequirementsLabel);

            _detailCostLabel = CreateWrappedDetailLabel();
            column.AddChild(_detailCostLabel);

            _detailCombatLabel = CreateWrappedDetailLabel();
            column.AddChild(_detailCombatLabel);

            _unlockButton = CreatePrimaryButton("MỞ KHÓA");
            _unlockButton.Pressed += OnUnlockPressed;
            column.AddChild(_unlockButton);

            _actionMessageLabel = InventoryPanelChrome.CreateLabel("", 11, InventoryPanelChrome.MutedTextColor);
            _actionMessageLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _actionMessageLabel.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(_actionMessageLabel);
            return frame;
        }

        private Label CreateWrappedDetailLabel()
        {
            Label label = InventoryPanelChrome.CreateLabel("", 12, InventoryPanelChrome.MutedTextColor);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            return label;
        }

        private void RefreshSelectedCharacter()
        {
            PlayerStats stats = GetSelectedStats();
            CharacterConfig config = stats?.ConfigData;
            if (stats == null || config == null)
            {
                ShowEmptyPanel("Nhân vật chưa có CharacterConfig.");
                return;
            }

            _characterNameLabel.Text = string.IsNullOrWhiteSpace(config.Name) ? "NHÂN VẬT" : config.Name.ToUpper();
            _characterLevelLabel.Text = $"Cấp {stats.CurrentLevel:00}";
            _currentTree = config.SkillTree;
            _selectedNode = null;
            _actionMessageLabel.Text = "";

            if (_currentTree == null || _currentTree.Branches == null || _currentTree.Branches.Count == 0)
            {
                BuildBranchSelector();
                ShowEmptyPanel("Nhân vật này chưa được gán cây kỹ năng.");
                return;
            }

            _currentCollection = SkillCollectionResolver.Resolve(stats);
            _currentCollection?.RecalculateUnspentSkillPoints(stats.CurrentLevel, _currentTree);
            _selectedBranchIndex = Mathf.Clamp(_selectedBranchIndex, 0, _currentTree.Branches.Count - 1);
            BuildBranchSelector();
            SelectBranch(_selectedBranchIndex);
            RefreshHeaderPoints();
        }

        private void SelectBranch(int branchIndex)
        {
            PlayerStats stats = GetSelectedStats();
            CharacterConfig config = stats?.ConfigData;
            if (config == null || _currentTree?.Branches == null || _currentTree.Branches.Count == 0)
            {
                return;
            }

            _selectedBranchIndex = Mathf.Clamp(branchIndex, 0, _currentTree.Branches.Count - 1);
            _currentBranch = _currentTree.Branches[_selectedBranchIndex];
            _selectedNode = ResolveInitialNode(_currentBranch, _currentCollection);
            _branchDescriptionLabel.Text = _currentBranch?.Description ?? "";
            RefreshBranchButtonStyles();

            _graphView.ShowBranch(
                _currentBranch,
                _currentTree,
                _currentCollection,
                stats.CurrentLevel,
                GetCharacterAccent(config),
                config.BackgroundImage,
                _selectedNode);

            UpdateDetailPanel();
        }

        private SkillTreeNodeData ResolveInitialNode(SkillTreeBranchData branch, PlayerSkillCollection collection)
        {
            if (branch?.Nodes == null || branch.Nodes.Count == 0)
            {
                return null;
            }

            foreach (SkillTreeNodeData node in branch.Nodes)
            {
                if (SkillTreeProgression.IsUnlocked(collection, node))
                {
                    return node;
                }
            }

            return branch.Nodes[0];
        }

        private void OnNodeSelected(SkillTreeNodeData node)
        {
            _selectedNode = node;
            _actionMessageLabel.Text = "";
            UpdateDetailPanel();
        }

        private void UpdateDetailPanel()
        {
            PlayerStats stats = GetSelectedStats();
            CharacterConfig config = stats?.ConfigData;
            if (_selectedNode?.Skill == null || stats == null || config == null)
            {
                ClearDetailPanel("Chọn một node trên cây để xem chi tiết.");
                return;
            }

            SkillData skill = _selectedNode.Skill;
            bool unlocked = SkillTreeProgression.IsUnlocked(_currentCollection, _selectedNode);
            bool canUnlock = SkillTreeProgression.CanUnlock(
                _currentCollection,
                _currentTree,
                _selectedNode,
                stats.CurrentLevel,
                out string reason);

            Texture2D resolvedIcon = SkillIconResolver.Resolve(skill);
            _detailIcon.Texture = resolvedIcon;
            _detailIcon.Visible = resolvedIcon != null;
            _detailIconFallback.Visible = resolvedIcon == null;
            _detailNameLabel.Text = string.IsNullOrWhiteSpace(skill.SkillName)
                ? "KỸ NĂNG"
                : skill.SkillName.ToUpper();
            _detailStateLabel.Text = unlocked
                ? "ĐÃ MỞ KHÓA"
                : canUnlock ? "CÓ THỂ MỞ" : "ĐANG KHÓA";
            _detailDescriptionLabel.Text = string.IsNullOrWhiteSpace(skill.Description)
                ? "Kỹ năng này chưa có mô tả."
                : skill.Description;
            _detailRequirementsLabel.Text = BuildRequirementsText(_selectedNode);
            _detailCostLabel.Text = $"Chi phí: {Mathf.Max(0, _selectedNode.SkillPointCost)} điểm KN  ·  Yêu cầu cấp {Mathf.Max(1, _selectedNode.RequiredCharacterLevel)}";
            _detailCombatLabel.Text = BuildCombatSummary(skill);

            _unlockButton.Text = unlocked ? "ĐÃ MỞ KHÓA" : "MỞ KHÓA";
            _unlockButton.Disabled = unlocked || !canUnlock;
            _unlockButton.TooltipText = unlocked ? "Kỹ năng đã thuộc về nhân vật." : reason;

            if (!unlocked && !canUnlock && string.IsNullOrWhiteSpace(_actionMessageLabel.Text))
            {
                _actionMessageLabel.Text = reason;
            }
        }

        private string BuildRequirementsText(SkillTreeNodeData node)
        {
            if (node?.RequiredNodeIds == null || node.RequiredNodeIds.Count == 0)
            {
                return "Tiên quyết: Không có";
            }

            var names = new List<string>();
            foreach (string nodeId in node.RequiredNodeIds)
            {
                SkillTreeNodeData requirement = SkillTreeProgression.FindNode(_currentTree, nodeId);
                names.Add(requirement?.Skill?.SkillName ?? nodeId);
            }

            return $"Tiên quyết: {string.Join(", ", names)}";
        }

        private string BuildCombatSummary(SkillData skill)
        {
            if (skill == null)
            {
                return "";
            }

            var parts = new List<string>();
            if (skill.ManaCost > 0)
            {
                parts.Add($"{skill.ManaCost} MP");
            }
            if (skill.StaminaCost > 0)
            {
                parts.Add($"{skill.StaminaCost} STA");
            }
            if (skill.Cooldown > 0f)
            {
                parts.Add($"Hồi chiêu {FormatSeconds(skill.Cooldown)}");
            }
            if (skill.Duration > 0f)
            {
                parts.Add($"Hiệu lực {FormatSeconds(skill.Duration)}");
            }

            return parts.Count == 0 ? "Thông số: Không tiêu hao" : $"Thông số: {string.Join(" · ", parts)}";
        }

        private void OnUnlockPressed()
        {
            PlayerStats stats = GetSelectedStats();
            CharacterConfig config = stats?.ConfigData;
            if (_selectedNode == null || stats == null || config == null)
            {
                return;
            }

            bool unlocked = SkillTreeProgression.TryUnlock(
                _currentCollection,
                _currentTree,
                _selectedNode,
                stats.CurrentLevel,
                out string message);

            _actionMessageLabel.Text = message;
            if (unlocked)
            {
                RefreshHeaderPoints();
                _graphView.ShowBranch(
                    _currentBranch,
                    _currentTree,
                    _currentCollection,
                    stats.CurrentLevel,
                    GetCharacterAccent(config),
                    config.BackgroundImage,
                    _selectedNode);
            }

            UpdateDetailPanel();
        }

        private void BuildCharacterSelector()
        {
            ClearContainer(_characterSelector);
            _characterButtons.Clear();

            PlayerManager manager = PlayerManager.Instance;
            if (manager == null)
            {
                return;
            }

            for (int i = 0; i < manager.PartyMembers.Count; i++)
            {
                int capturedIndex = i;
                PlayerStats stats = manager.PartyMembers[i];
                CharacterConfig config = stats?.ConfigData;
                Button button = CreateSelectorButton(
                    config?.Name ?? $"Nhân vật {i + 1}",
                    config?.Icon,
                    i == _selectedPartyIndex,
                    GetCharacterAccent(config));
                button.Pressed += () => SelectCharacter(capturedIndex);
                _characterSelector.AddChild(button);
                _characterButtons.Add(button);
            }
        }

        private void SelectCharacter(int partyIndex)
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null || partyIndex < 0 || partyIndex >= manager.PartyMembers.Count)
            {
                return;
            }

            _selectedPartyIndex = partyIndex;
            _selectedBranchIndex = 0;
            BuildCharacterSelector();
            RefreshSelectedCharacter();
        }

        private void BuildBranchSelector()
        {
            ClearContainer(_branchSelector);
            _branchButtons.Clear();

            if (_currentTree?.Branches == null)
            {
                return;
            }

            CharacterConfig config = GetSelectedStats()?.ConfigData;
            Color accent = GetCharacterAccent(config);
            for (int i = 0; i < _currentTree.Branches.Count; i++)
            {
                int capturedIndex = i;
                SkillTreeBranchData branch = _currentTree.Branches[i];
                Button button = CreateSelectorButton(
                    branch?.BranchName ?? $"Nhánh {i + 1}",
                    branch?.Icon,
                    i == _selectedBranchIndex,
                    accent);
                button.Pressed += () => SelectBranch(capturedIndex);
                _branchSelector.AddChild(button);
                _branchButtons.Add(button);
            }
        }

        private void RefreshBranchButtonStyles()
        {
            CharacterConfig config = GetSelectedStats()?.ConfigData;
            Color accent = GetCharacterAccent(config);
            for (int i = 0; i < _branchButtons.Count; i++)
            {
                ApplySelectorStyle(_branchButtons[i], i == _selectedBranchIndex, accent);
            }
        }

        private Button CreateSelectorButton(string text, Texture2D icon, bool selected, Color accent)
        {
            Button button = new()
            {
                Text = text,
                Icon = icon,
                ExpandIcon = true,
                IconAlignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand,
                CustomMinimumSize = new Vector2(126, 32)
            };
            button.AddThemeConstantOverride("icon_max_width", 24);
            button.AddThemeFontSizeOverride("font_size", 12);
            ApplySelectorStyle(button, selected, accent);
            return button;
        }

        private void ApplySelectorStyle(Button button, bool selected, Color accent)
        {
            if (button == null)
            {
                return;
            }

            PixelButtonSkin.ApplyTab(button, selected, PixelButtonSkin.CompactHeight, 126f);
            button.AddThemeColorOverride("font_color", selected ? InventoryPanelChrome.MainTextColor : InventoryPanelChrome.MutedTextColor);
        }

        private Button CreatePrimaryButton(string text)
        {
            Button button = new()
            {
                Text = text,
                CustomMinimumSize = new Vector2(0, 38),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            PixelButtonSkin.ApplyPrimary(button, PixelButtonSkin.RegularHeight);
            return button;
        }

        private void RefreshHeaderPoints()
        {
            PlayerStats stats = GetSelectedStats();
            CharacterConfig config = stats?.ConfigData;
            _currentCollection?.RecalculateUnspentSkillPoints(stats?.CurrentLevel ?? 1, _currentTree);
            int points = _currentCollection?.UnspentSkillPoints ?? 0;
            _skillPointsLabel.Text = $"Điểm KN: {points}";
            _skillPointsLabel.AddThemeColorOverride("font_color", GetCharacterAccent(config));
        }

        private void ShowEmptyPanel(string message)
        {
            _currentTree = null;
            _currentBranch = null;
            _currentCollection = null;
            _selectedNode = null;
            _branchDescriptionLabel.Text = "";
            _skillPointsLabel.Text = "Điểm KN: 0";
            ClearContainer(_branchSelector);
            _graphView.ShowBranch(null, null, null, 1, InventoryPanelChrome.AccentColor, null);
            ClearDetailPanel(message);
        }

        private void ClearDetailPanel(string message)
        {
            _detailIcon.Texture = null;
            _detailIcon.Visible = false;
            _detailIconFallback.Visible = true;
            _detailNameLabel.Text = "CHƯA CHỌN KỸ NĂNG";
            _detailStateLabel.Text = "";
            _detailDescriptionLabel.Text = message;
            _detailRequirementsLabel.Text = "";
            _detailCostLabel.Text = "";
            _detailCombatLabel.Text = "";
            _unlockButton.Text = "MỞ KHÓA";
            _unlockButton.Disabled = true;
            _actionMessageLabel.Text = "";
        }

        private PlayerStats GetSelectedStats()
        {
            PlayerManager manager = PlayerManager.Instance;
            return manager != null
                && _selectedPartyIndex >= 0
                && _selectedPartyIndex < manager.PartyMembers.Count
                    ? manager.PartyMembers[_selectedPartyIndex]
                    : null;
        }

        private static Color GetCharacterAccent(CharacterConfig config)
        {
            Color color = config?.ThemeColor ?? InventoryPanelChrome.AccentColor;
            float brightest = Mathf.Max(color.R, Mathf.Max(color.G, color.B));
            return color.A > 0.1f && brightest > 0.22f
                ? color
                : InventoryPanelChrome.AccentColor;
        }

        private static string FormatSeconds(float value)
        {
            float rounded = Mathf.Round(value * 10f) / 10f;
            return Mathf.IsEqualApprox(rounded, Mathf.Round(rounded))
                ? $"{Mathf.RoundToInt(rounded)} giây"
                : $"{rounded:0.0} giây";
        }

        private static void ClearContainer(Node container)
        {
            if (container == null)
            {
                return;
            }

            foreach (Node child in container.GetChildren())
            {
                container.RemoveChild(child);
                child.QueueFree();
            }
        }

        private StyleBoxFlat CreateBodyStyle()
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color(
                    InventoryPanelChrome.DeepSurfaceColor.R,
                    InventoryPanelChrome.DeepSurfaceColor.G,
                    InventoryPanelChrome.DeepSurfaceColor.B,
                    0.72f),
                BorderColor = InventoryPanelChrome.BorderColor
            };
            style.SetBorderWidthAll(1);
            return style;
        }

        private void BindPlayerManagerSignals()
        {
            if (PlayerManager.Instance == null)
            {
                return;
            }

            PlayerManager.Instance.PartyUpdated += OnPartyUpdated;
            PlayerManager.Instance.ActiveCharacterChanged += OnActiveCharacterChanged;
        }

        private void UnbindPlayerManagerSignals()
        {
            if (PlayerManager.Instance == null)
            {
                return;
            }

            PlayerManager.Instance.PartyUpdated -= OnPartyUpdated;
            PlayerManager.Instance.ActiveCharacterChanged -= OnActiveCharacterChanged;
        }

        private void OnVisibilityChanged()
        {
            if (Visible)
            {
                RefreshFromCurrentParty();
            }
        }

        private void OnPartyUpdated()
        {
            if (Visible)
            {
                RefreshFromCurrentParty();
            }
        }

        private void OnActiveCharacterChanged(int index)
        {
            if (!Visible)
            {
                return;
            }

            _selectedPartyIndex = index;
            _selectedBranchIndex = 0;
            RefreshFromCurrentParty();
        }
    }
}
