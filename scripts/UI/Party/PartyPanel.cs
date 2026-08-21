using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Entities.NPC;
using AshesofaDyingWorld.UI.Shared;

namespace AshesofaDyingWorld.UI.Party
{
    /// <summary>
    /// Panel Tổ đội độc lập trong menu chính.
    /// Panel chỉ hiển thị dữ liệu và gửi yêu cầu đổi đội hình cho PlayerManager,
    /// không tự sửa danh sách party để tránh UI và gameplay giữ hai trạng thái khác nhau.
    /// </summary>
    public partial class PartyPanel : Panel
    {
        private const int MaximumPartySize = PlayerManager.MaxPartySize;

        private sealed class MemberCardRefs
        {
            public PanelContainer Card;
            public Label HpLabel;
            public Label LeaderLabel;
        }

        private readonly Dictionary<PlayerStats, MemberCardRefs> _memberCards = new();
        private readonly List<PlayerStats> _observedStats = new();

        private HBoxContainer _formationRow;
        private Label _partyCountLabel;

        private TextureRect _detailPortrait;
        private Label _detailPortraitFallback;
        private Label _detailNameLabel;
        private Label _detailRoleLabel;
        private Label _detailLevelLabel;
        private Label _detailRaceLabel;
        private ProgressBar _detailHpBar;
        private ProgressBar _detailMpBar;
        private ProgressBar _detailStaminaBar;
        private Label _detailHpValue;
        private Label _detailMpValue;
        private Label _detailStaminaValue;
        private Label _detailAttackValue;
        private Label _detailArmorValue;
        private Label _detailSpeedValue;
        private Button _leaderButton;
        private Button _moveLeftButton;
        private Button _moveRightButton;
        private Label _actionHintLabel;
        private VBoxContainer _commandSection;
        private Label _commandHintLabel;
        private readonly Dictionary<CompanionCommandMode, Button> _commandButtons = new();

        private int _selectedIndex = -1;

        private Color MainText => InventoryPanelChrome.MainTextColor;
        private Color MutedText => InventoryPanelChrome.MutedTextColor;
        private Color Border => InventoryPanelChrome.BorderColor;
        private Color Accent => InventoryPanelChrome.AccentColor;
        private Color DeepSurface => InventoryPanelChrome.DeepSurfaceColor;

        public override void _Ready()
        {
            InventoryPanelChrome.ApplyPanelSize(this);
            AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            BuildInterface();
            BindPlayerManager();
            RefreshParty();
        }

        public override void _ExitTree()
        {
            UnbindPlayerManager();
            UnbindMemberStats();
        }

        /// <summary>Được gọi khi panel mở hoặc party thay đổi.</summary>
        public void RefreshParty()
        {
            PlayerManager manager = PlayerManager.Instance;
            int count = manager?.PartyMembers.Count ?? 0;
            _partyCountLabel.Text = $"{count}/{MaximumPartySize} THÀNH VIÊN";

            if (count == 0)
            {
                _selectedIndex = -1;
            }
            else if (_selectedIndex < 0 || _selectedIndex >= count)
            {
                _selectedIndex = Mathf.Clamp(manager.ActiveCharacterIndex, 0, count - 1);
            }

            RebuildFormationCards();
            BindMemberStats();
            UpdateAllMemberValues();
            UpdateDetailPanel();
        }

        private void BuildInterface()
        {
            VBoxContainer root = InventoryPanelChrome.BuildWindowShell(this);
            root.AddChild(BuildHeader());
            root.AddChild(BuildInfoBar());
            root.AddChild(BuildBody());
        }

        private Control BuildHeader()
        {
            PanelContainer panel = InventoryPanelChrome.CreateHeader(out HBoxContainer row);

            Label title = CreateLabel("TỔ ĐỘI", 18, MainText);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            title.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(title);

            _partyCountLabel = CreateLabel($"0/{MaximumPartySize} THÀNH VIÊN", 13, MutedText);
            _partyCountLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(_partyCountLabel);
            row.AddChild(InventoryPanelChrome.CreateCloseButton(Hide));
            return panel;
        }

        private Control BuildInfoBar()
        {
            PanelContainer panel = new();
            panel.CustomMinimumSize = new Vector2(0, 42);
            panel.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreateTabsBarStyle());

            MarginContainer margin = new();
            margin.AddThemeConstantOverride("margin_left", 14);
            margin.AddThemeConstantOverride("margin_right", 14);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            panel.AddChild(margin);

            HBoxContainer row = new();
            row.AddThemeConstantOverride("separation", 10);
            margin.AddChild(row);

            Label heading = CreateLabel("ĐỘI HÌNH HIỆN TẠI", 14, MainText);
            heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            heading.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(heading);

            Label hint = CreateLabel("Đội trưởng là nhân vật đang điều khiển", 12, MutedText);
            hint.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(hint);
            return panel;
        }

        private Control BuildBody()
        {
            PanelContainer surface = new();
            surface.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            surface.SizeFlagsVertical = SizeFlags.ExpandFill;
            surface.AddThemeStyleboxOverride("panel", CreateBodyStyle());

            MarginContainer outer = new();
            outer.AddThemeConstantOverride("margin_left", 12);
            outer.AddThemeConstantOverride("margin_top", 12);
            outer.AddThemeConstantOverride("margin_right", 12);
            outer.AddThemeConstantOverride("margin_bottom", 12);
            surface.AddChild(outer);

            HBoxContainer body = new();
            body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            body.SizeFlagsVertical = SizeFlags.ExpandFill;
            body.AddThemeConstantOverride("separation", 0);
            outer.AddChild(body);

            body.AddChild(BuildFormationSection());
            body.AddChild(CreateVerticalDivider());
            body.AddChild(BuildDetailSection());
            return surface;
        }

        private Control BuildFormationSection()
        {
            MarginContainer frame = new();
            frame.CustomMinimumSize = new Vector2(680, 0);
            frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            frame.SizeFlagsVertical = SizeFlags.ExpandFill;
            frame.AddThemeConstantOverride("margin_left", 14);
            frame.AddThemeConstantOverride("margin_top", 4);
            frame.AddThemeConstantOverride("margin_right", 14);
            frame.AddThemeConstantOverride("margin_bottom", 4);

            VBoxContainer column = new();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddThemeConstantOverride("separation", 10);
            frame.AddChild(column);

            column.AddChild(CreateSectionTitle("THÀNH VIÊN", HorizontalAlignment.Left));
            column.AddChild(InventoryPanelChrome.CreateDivider());

            CenterContainer center = new();
            center.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            center.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddChild(center);

            _formationRow = new HBoxContainer();
            _formationRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _formationRow.SizeFlagsVertical = SizeFlags.ExpandFill;
            _formationRow.Alignment = BoxContainer.AlignmentMode.Center;
            _formationRow.AddThemeConstantOverride("separation", 12);
            center.AddChild(_formationRow);

            Label note = CreateLabel(
                "Chọn thành viên để xem chỉ số. Dùng bảng bên phải để đổi đội trưởng hoặc thứ tự đội hình.",
                12,
                MutedText);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            note.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(note);
            return frame;
        }

        private Control BuildDetailSection()
        {
            MarginContainer frame = new();
            frame.CustomMinimumSize = new Vector2(340, 0);
            frame.SizeFlagsVertical = SizeFlags.ExpandFill;
            frame.AddThemeConstantOverride("margin_left", 16);
            frame.AddThemeConstantOverride("margin_top", 4);
            frame.AddThemeConstantOverride("margin_right", 16);
            frame.AddThemeConstantOverride("margin_bottom", 4);

            VBoxContainer column = new();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddThemeConstantOverride("separation", 8);
            frame.AddChild(column);

            column.AddChild(CreateSectionTitle("THÔNG TIN THÀNH VIÊN", HorizontalAlignment.Left));
            column.AddChild(InventoryPanelChrome.CreateDivider());

            CenterContainer portraitCenter = new();
            portraitCenter.CustomMinimumSize = new Vector2(0, 112);
            column.AddChild(portraitCenter);

            PanelContainer portraitFrame = new();
            portraitFrame.CustomMinimumSize = new Vector2(96, 96);
            portraitFrame.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreatePreviewStyle());
            portraitCenter.AddChild(portraitFrame);

            CenterContainer portraitContent = new();
            portraitFrame.AddChild(portraitContent);

            _detailPortrait = new TextureRect
            {
                CustomMinimumSize = new Vector2(78, 78),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest
            };
            portraitContent.AddChild(_detailPortrait);

            _detailPortraitFallback = CreateLabel("?", 28, MutedText);
            _detailPortraitFallback.HorizontalAlignment = HorizontalAlignment.Center;
            portraitContent.AddChild(_detailPortraitFallback);

            _detailNameLabel = CreateLabel("Chưa có thành viên", 18, MainText);
            _detailNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(_detailNameLabel);

            _detailRoleLabel = CreateLabel("", 13, Accent);
            _detailRoleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(_detailRoleLabel);

            _detailLevelLabel = CreateLabel("", 12, MutedText);
            _detailLevelLabel.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(_detailLevelLabel);

            _detailRaceLabel = CreateLabel("", 12, MutedText);
            _detailRaceLabel.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(_detailRaceLabel);

            column.AddChild(CreateSpacer(4));
            column.AddChild(CreateResourceRow("HP", new Color("#c95752"), out _detailHpBar, out _detailHpValue));
            column.AddChild(CreateResourceRow("MP", new Color("#4e8eb8"), out _detailMpBar, out _detailMpValue));
            column.AddChild(CreateResourceRow("STA", new Color("#6e965b"), out _detailStaminaBar, out _detailStaminaValue));

            column.AddChild(CreateSpacer(4));
            column.AddChild(InventoryPanelChrome.CreateDivider(true));
            column.AddChild(CreateStatRow("Công", out _detailAttackValue));
            column.AddChild(CreateStatRow("Giáp", out _detailArmorValue));
            column.AddChild(CreateStatRow("Tốc độ đánh", out _detailSpeedValue));

            column.AddChild(CreateSpacer(2));
            Label commandHudHint = CreateLabel(
                "Mệnh lệnh đồng đội: chuột phải vào Character HUD của Hyou khi đang chơi.",
                10,
                MutedText);
            commandHudHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            commandHudHint.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(commandHudHint);

            Control actionSpacer = new();
            actionSpacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddChild(actionSpacer);

            _leaderButton = CreateActionButton("ĐẶT LÀM ĐỘI TRƯỞNG", SetSelectedAsLeader);
            column.AddChild(_leaderButton);

            HBoxContainer moveRow = new();
            moveRow.AddThemeConstantOverride("separation", 8);
            column.AddChild(moveRow);
            _moveLeftButton = CreateActionButton("← DỜI TRÁI", () => MoveSelected(-1));
            moveRow.AddChild(_moveLeftButton);
            _moveRightButton = CreateActionButton("DỜI PHẢI →", () => MoveSelected(1));
            moveRow.AddChild(_moveRightButton);

            _actionHintLabel = CreateLabel("", 11, MutedText);
            _actionHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _actionHintLabel.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(_actionHintLabel);
            return frame;
        }

        private void RebuildFormationCards()
        {
            foreach (Node child in _formationRow.GetChildren())
            {
                child.QueueFree();
            }
            _memberCards.Clear();

            PlayerManager manager = PlayerManager.Instance;
            for (int slot = 0; slot < MaximumPartySize; slot++)
            {
                if (manager != null && slot < manager.PartyMembers.Count)
                {
                    PlayerStats member = manager.PartyMembers[slot];
                    _formationRow.AddChild(CreateMemberCard(member, slot, slot == manager.ActiveCharacterIndex));
                }
                else
                {
                    _formationRow.AddChild(CreateEmptySlot(slot));
                }
            }
        }

        private Control CreateMemberCard(PlayerStats member, int index, bool isLeader)
        {
            Color characterAccent = ResolveCharacterAccent(member);
            PanelContainer card = new();
            card.CustomMinimumSize = new Vector2(202, 300);
            card.SizeFlagsVertical = SizeFlags.ExpandFill;
            card.AddThemeStyleboxOverride("panel", CreateMemberCardStyle(index == _selectedIndex, isLeader, characterAccent));

            MarginContainer margin = new();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_top", 12);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_bottom", 12);
            card.AddChild(margin);

            VBoxContainer column = new();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddThemeConstantOverride("separation", 8);
            margin.AddChild(column);

            HBoxContainer top = new();
            column.AddChild(top);
            Label slotLabel = CreateLabel($"VỊ TRÍ {index + 1}", 11, MutedText);
            slotLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            top.AddChild(slotLabel);
            Label leaderLabel = CreateLabel(isLeader ? "ĐỘI TRƯỞNG" : "", 11, isLeader ? Accent : MutedText);
            leaderLabel.HorizontalAlignment = HorizontalAlignment.Right;
            top.AddChild(leaderLabel);

            CenterContainer portraitCenter = new();
            portraitCenter.CustomMinimumSize = new Vector2(0, 112);
            column.AddChild(portraitCenter);
            PanelContainer portraitFrame = new();
            portraitFrame.CustomMinimumSize = new Vector2(98, 98);
            portraitFrame.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreatePreviewStyle());
            portraitCenter.AddChild(portraitFrame);
            CenterContainer portraitContent = new();
            portraitFrame.AddChild(portraitContent);

            Texture2D icon = member?.ConfigData?.Icon;
            if (icon != null)
            {
                TextureRect portrait = new()
                {
                    Texture = icon,
                    CustomMinimumSize = new Vector2(80, 80),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest
                };
                portraitContent.AddChild(portrait);
            }
            else
            {
                string name = member?.ConfigData?.Name;
                Label fallback = CreateLabel(string.IsNullOrWhiteSpace(name) ? "?" : name.Substring(0, 1).ToUpperInvariant(), 28, MutedText);
                fallback.HorizontalAlignment = HorizontalAlignment.Center;
                portraitContent.AddChild(fallback);
            }

            Label nameLabel = CreateLabel(member?.ConfigData?.Name ?? "Thành viên", 16, MainText);
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(nameLabel);
            Label roleLabel = CreateLabel(GetRoleLabel(member), 12, characterAccent);
            roleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(roleLabel);
            Label levelLabel = CreateLabel($"Cấp {(member?.CurrentLevel ?? 1):00}", 12, MutedText);
            levelLabel.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(levelLabel);

            Control spacer = new();
            spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddChild(spacer);
            Label hpLabel = CreateLabel("HP 0/0", 12, MainText);
            hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(hpLabel);

            Button clickTarget = new();
            clickTarget.SetAnchorsPreset(LayoutPreset.FullRect);
            clickTarget.FocusMode = FocusModeEnum.None;
            clickTarget.MouseDefaultCursorShape = CursorShape.PointingHand;
            clickTarget.AddThemeStyleboxOverride("normal", InventoryPanelChrome.CreateTransparentButtonStyle());
            clickTarget.AddThemeStyleboxOverride("hover", InventoryPanelChrome.CreateTransparentButtonStyle());
            clickTarget.AddThemeStyleboxOverride("pressed", InventoryPanelChrome.CreateTransparentButtonStyle());
            clickTarget.Pressed += () => SelectMember(index);
            card.AddChild(clickTarget);

            _memberCards[member] = new MemberCardRefs
            {
                Card = card,
                HpLabel = hpLabel,
                LeaderLabel = leaderLabel
            };
            return card;
        }

        private Control CreateEmptySlot(int index)
        {
            PanelContainer card = new();
            card.CustomMinimumSize = new Vector2(202, 300);
            card.SizeFlagsVertical = SizeFlags.ExpandFill;
            card.AddThemeStyleboxOverride("panel", CreateEmptySlotStyle());

            CenterContainer center = new();
            card.AddChild(center);
            VBoxContainer content = new();
            content.AddThemeConstantOverride("separation", 10);
            center.AddChild(content);

            Label plus = CreateLabel("+", 36, MutedText);
            plus.HorizontalAlignment = HorizontalAlignment.Center;
            content.AddChild(plus);
            Label slot = CreateLabel($"VỊ TRÍ {index + 1}", 12, MutedText);
            slot.HorizontalAlignment = HorizontalAlignment.Center;
            content.AddChild(slot);
            Label empty = CreateLabel("Ô TRỐNG", 14, MutedText);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            content.AddChild(empty);
            return card;
        }

        private void SelectMember(int index)
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null || index < 0 || index >= manager.PartyMembers.Count)
            {
                return;
            }
            _selectedIndex = index;
            RefreshCardStyles();
            UpdateDetailPanel();
        }

        private void SetSelectedAsLeader()
        {
            if (PlayerManager.Instance?.SetPartyLeader(_selectedIndex) == true)
            {
                RefreshParty();
            }
        }

        private void MoveSelected(int direction)
        {
            PlayerManager manager = PlayerManager.Instance;
            int target = _selectedIndex + direction;
            if (manager?.MoveMember(_selectedIndex, target) == true)
            {
                _selectedIndex = target;
                RefreshParty();
            }
        }

        private void RefreshCardStyles()
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null)
            {
                return;
            }

            foreach (KeyValuePair<PlayerStats, MemberCardRefs> pair in _memberCards)
            {
                int index = manager.PartyMembers.IndexOf(pair.Key);
                bool isLeader = index == manager.ActiveCharacterIndex;
                pair.Value.Card.AddThemeStyleboxOverride(
                    "panel",
                    CreateMemberCardStyle(index == _selectedIndex, isLeader, ResolveCharacterAccent(pair.Key)));
                pair.Value.LeaderLabel.Text = isLeader ? "ĐỘI TRƯỞNG" : "";
                pair.Value.LeaderLabel.AddThemeColorOverride("font_color", isLeader ? Accent : MutedText);
            }
        }

        private void UpdateAllMemberValues()
        {
            foreach (KeyValuePair<PlayerStats, MemberCardRefs> pair in _memberCards)
            {
                PlayerStats stats = pair.Key;
                pair.Value.HpLabel.Text = $"HP {Mathf.RoundToInt(stats.CurrentHP)}/{Mathf.RoundToInt(stats.MaxHP)}";
            }
        }

        private void UpdateDetailPanel()
        {
            PlayerManager manager = PlayerManager.Instance;
            PlayerStats member = manager != null
                && _selectedIndex >= 0
                && _selectedIndex < manager.PartyMembers.Count
                    ? manager.PartyMembers[_selectedIndex]
                    : null;

            if (member == null)
            {
                _detailPortrait.Texture = null;
                _detailPortrait.Visible = false;
                _detailPortraitFallback.Text = "?";
                _detailPortraitFallback.Visible = true;
                _detailNameLabel.Text = "Chưa có thành viên";
                _detailRoleLabel.Text = "";
                _detailLevelLabel.Text = "";
                _detailRaceLabel.Text = "";
                SetBar(_detailHpBar, _detailHpValue, 0, 1);
                SetBar(_detailMpBar, _detailMpValue, 0, 1);
                SetBar(_detailStaminaBar, _detailStaminaValue, 0, 1);
                _detailAttackValue.Text = "-";
                _detailArmorValue.Text = "-";
                _detailSpeedValue.Text = "-";
                _leaderButton.Disabled = true;
                _moveLeftButton.Disabled = true;
                _moveRightButton.Disabled = true;
                if (_commandSection != null) _commandSection.Visible = false;
                _actionHintLabel.Text = "Đội chưa có thành viên để quản lý.";
                return;
            }

            CharacterConfig config = member.ConfigData;
            Color characterAccent = ResolveCharacterAccent(member);
            _detailPortrait.Texture = config?.Icon;
            _detailPortrait.Visible = config?.Icon != null;
            _detailPortraitFallback.Text = string.IsNullOrWhiteSpace(config?.Name)
                ? "?"
                : config.Name.Substring(0, 1).ToUpperInvariant();
            _detailPortraitFallback.Visible = config?.Icon == null;

            _detailNameLabel.Text = config?.Name ?? "Thành viên";
            _detailRoleLabel.Text = GetRoleLabel(member);
            _detailRoleLabel.AddThemeColorOverride("font_color", characterAccent);
            _detailLevelLabel.Text = $"Cấp {member.CurrentLevel:00}";
            _detailRaceLabel.Text = config?.CharacterRace?.RaceName ?? "Chưa rõ chủng tộc";
            SetBar(_detailHpBar, _detailHpValue, member.CurrentHP, member.MaxHP);
            SetBar(_detailMpBar, _detailMpValue, member.CurrentMP, member.MaxMP);
            SetBar(_detailStaminaBar, _detailStaminaValue, member.CurrentStamina, member.MaxStamina);
            _detailAttackValue.Text = Mathf.RoundToInt(member.PrimaryPower).ToString();
            _detailArmorValue.Text = Mathf.RoundToInt(member.Armor).ToString();
            _detailSpeedValue.Text = $"{member.AttackSpeed:0.00}x";

            bool isLeader = _selectedIndex == manager.ActiveCharacterIndex;
            _leaderButton.Text = isLeader ? "ĐANG LÀ ĐỘI TRƯỞNG" : "ĐẶT LÀM ĐỘI TRƯỞNG";
            _leaderButton.Disabled = isLeader;
            _moveLeftButton.Disabled = _selectedIndex <= 0;
            _moveRightButton.Disabled = _selectedIndex >= manager.PartyMembers.Count - 1;
            _actionHintLabel.Text = isLeader
                ? "Nhân vật này đang dẫn đội và nhận điều khiển trực tiếp."
                : "Đổi đội trưởng sẽ chuyển nhân vật đang điều khiển.";

        }

        private VBoxContainer BuildCompanionCommandSection()
        {
            var section = new VBoxContainer();
            section.AddThemeConstantOverride("separation", 5);
            section.Visible = false;

            section.AddChild(InventoryPanelChrome.CreateDivider(true));
            Label title = CreateLabel("MỆNH LỆNH ĐỒNG ĐỘI", 11, Accent);
            section.AddChild(title);

            var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);
            section.AddChild(grid);

            AddCommandButton(grid, CompanionCommandMode.Follow, "THEO SAU");
            AddCommandButton(grid, CompanionCommandMode.Stay, "ĐỨNG YÊN");
            AddCommandButton(grid, CompanionCommandMode.Protect, "BẢO VỆ");
            AddCommandButton(grid, CompanionCommandMode.Wander, "ĐI DẠO");

            _commandHintLabel = CreateLabel("", 10, MutedText);
            _commandHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _commandHintLabel.HorizontalAlignment = HorizontalAlignment.Center;
            section.AddChild(_commandHintLabel);
            return section;
        }

        private void AddCommandButton(GridContainer grid, CompanionCommandMode mode, string label)
        {
            Button button = CreateActionButton(label, () => SetSelectedCompanionCommand(mode));
            button.CustomMinimumSize = new Vector2(0, 32);
            button.AddThemeFontSizeOverride("font_size", 11);
            grid.AddChild(button);
            _commandButtons[mode] = button;
        }

        private void SetSelectedCompanionCommand(CompanionCommandMode mode)
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null || _selectedIndex < 0 || _selectedIndex >= manager.PartyMembers.Count)
            {
                return;
            }

            if (manager.GetCombatCharacter(manager.PartyMembers[_selectedIndex]) is not NpcCharacter companion)
            {
                return;
            }

            companion.SetCommandMode(mode);
            RefreshCompanionCommandSection(manager.PartyMembers[_selectedIndex], _selectedIndex == manager.ActiveCharacterIndex);
        }

        private void RefreshCompanionCommandSection(PlayerStats member, bool isActiveCharacter)
        {
            if (_commandSection == null)
            {
                return;
            }

            NpcCharacter companion = PlayerManager.Instance?.GetCombatCharacter(member) as NpcCharacter;
            _commandSection.Visible = companion != null && companion.IsRecruited;
            if (!_commandSection.Visible)
            {
                return;
            }

            foreach (KeyValuePair<CompanionCommandMode, Button> pair in _commandButtons)
            {
                bool selected = pair.Key == companion.CommandMode;
                pair.Value.AddThemeStyleboxOverride(
                    "normal",
                    InventoryPanelChrome.CreateButtonStyle(
                        selected ? DeepSurface : InventoryPanelChrome.ButtonNormalColor,
                        selected ? Accent : Border,
                        selected ? 2 : 1));
                pair.Value.AddThemeColorOverride("font_color", selected ? Accent : MainText);
            }

            _commandHintLabel.Text = isActiveCharacter
                ? "Đang điều khiển trực tiếp. Mệnh lệnh này sẽ có hiệu lực khi chuyển sang nhân vật khác."
                : companion.CommandMode switch
                {
                    CompanionCommandMode.Follow => "Theo đội trưởng và tự chiến đấu theo Decision Core.",
                    CompanionCommandMode.Stay => "Giữ nguyên vị trí, không tự đi theo hoặc truy đuổi.",
                    CompanionCommandMode.Protect => "Bám gần đội trưởng hơn và ưu tiên mối đe dọa quanh người được bảo vệ.",
                    CompanionCommandMode.Wander => "Đi dạo quanh vị trí được ra lệnh, không tự chạy theo đội trưởng.",
                    _ => string.Empty
                };
        }

        /// <summary>
        /// Vai trò hiện chỉ là nhãn trình bày, được suy ra từ chỉ số hiện tại.
        /// Khi project có class/role chính thức, thay hàm này bằng dữ liệu từ CharacterConfig.
        /// </summary>
        private string GetRoleLabel(PlayerStats stats)
        {
            if (stats == null)
            {
                return "Chưa xác định";
            }

            int strength = stats.GetAttributeValue(AttributeType.Strength);
            int dexterity = stats.GetAttributeValue(AttributeType.Dexterity);
            int intelligence = stats.GetAttributeValue(AttributeType.Intelligence);
            int vitality = stats.GetAttributeValue(AttributeType.Vitality);
            int defense = stats.GetAttributeValue(AttributeType.Defense);
            int spirit = stats.GetAttributeValue(AttributeType.Spirit);

            if (intelligence >= strength && intelligence >= dexterity && stats.MaxMP > 0f) return "Pháp thuật";
            if (defense + vitality >= strength + dexterity + 4) return "Hộ vệ";
            if (spirit > strength && spirit > dexterity) return "Hỗ trợ";
            if (dexterity > strength) return "Cơ động";
            return "Tiền tuyến";
        }

        private Color ResolveCharacterAccent(PlayerStats stats)
        {
            Color color = stats?.ConfigData?.ThemeColor ?? Accent;
            bool readable = color.A > 0.1f
                && Mathf.Max(color.R, Mathf.Max(color.G, color.B)) > 0.22f;
            return readable ? color : Accent;
        }

        private void BindPlayerManager()
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null) return;
            manager.PartyUpdated += OnPartyUpdated;
            manager.ActiveCharacterChanged += OnActiveCharacterChanged;
        }

        private void UnbindPlayerManager()
        {
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null) return;
            manager.PartyUpdated -= OnPartyUpdated;
            manager.ActiveCharacterChanged -= OnActiveCharacterChanged;
        }

        private void BindMemberStats()
        {
            UnbindMemberStats();
            PlayerManager manager = PlayerManager.Instance;
            if (manager == null) return;

            foreach (PlayerStats stats in manager.PartyMembers)
            {
                if (stats == null) continue;
                stats.StatsChanged += OnMemberStatsChanged;
                _observedStats.Add(stats);
            }
        }

        private void UnbindMemberStats()
        {
            foreach (PlayerStats stats in _observedStats)
            {
                if (GodotObject.IsInstanceValid(stats))
                {
                    stats.StatsChanged -= OnMemberStatsChanged;
                }
            }
            _observedStats.Clear();
        }

        private void OnPartyUpdated() => RefreshParty();

        private void OnActiveCharacterChanged(int index)
        {
            RefreshCardStyles();
            UpdateDetailPanel();
        }

        private void OnMemberStatsChanged()
        {
            UpdateAllMemberValues();
            UpdateDetailPanel();
        }

        private Label CreateLabel(string text, int fontSize, Color color)
            => InventoryPanelChrome.CreateLabel(text, fontSize, color);

        private Label CreateSectionTitle(string text, HorizontalAlignment alignment)
        {
            Label label = CreateLabel(text, 14, MainText);
            label.CustomMinimumSize = new Vector2(0, 28);
            label.HorizontalAlignment = alignment;
            label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        private Control CreateSpacer(float height)
            => new Control { CustomMinimumSize = new Vector2(0, height) };

        private ColorRect CreateVerticalDivider()
        {
            return new ColorRect
            {
                Color = new Color(Border.R, Border.G, Border.B, 0.72f),
                CustomMinimumSize = new Vector2(1, 0),
                MouseFilter = MouseFilterEnum.Ignore
            };
        }

        private HBoxContainer CreateResourceRow(
            string caption,
            Color fillColor,
            out ProgressBar bar,
            out Label valueLabel)
        {
            HBoxContainer row = new();
            row.CustomMinimumSize = new Vector2(0, 28);
            row.AddThemeConstantOverride("separation", 8);

            Label name = CreateLabel(caption, 12, MainText);
            name.CustomMinimumSize = new Vector2(34, 0);
            name.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(name);

            bar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(120, 13),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                ShowPercentage = false
            };

            StyleBoxFlat background = new();
            background.BgColor = DeepSurface;
            background.BorderColor = Border.Darkened(0.1f);
            background.SetBorderWidthAll(1);
            background.SetCornerRadiusAll(1);
            StyleBoxFlat fill = new();
            fill.BgColor = fillColor;
            fill.SetCornerRadiusAll(1);
            bar.AddThemeStyleboxOverride("background", background);
            bar.AddThemeStyleboxOverride("fill", fill);
            row.AddChild(bar);

            valueLabel = CreateLabel("0/0", 11, MainText);
            valueLabel.CustomMinimumSize = new Vector2(72, 0);
            valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
            valueLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(valueLabel);
            return row;
        }

        private HBoxContainer CreateStatRow(string caption, out Label valueLabel)
        {
            HBoxContainer row = new();
            row.CustomMinimumSize = new Vector2(0, 28);
            Label name = CreateLabel(caption, 12, MutedText);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(name);
            valueLabel = CreateLabel("-", 12, MainText);
            valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
            valueLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(valueLabel);
            return row;
        }

        private Button CreateActionButton(string text, System.Action action)
        {
            Button button = new();
            button.Text = text;
            button.CustomMinimumSize = new Vector2(0, 36);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.FocusMode = FocusModeEnum.None;
            button.MouseDefaultCursorShape = CursorShape.PointingHand;
            button.AddThemeStyleboxOverride(
                "normal",
                InventoryPanelChrome.CreateButtonStyle(InventoryPanelChrome.ButtonNormalColor, Border, 1));
            button.AddThemeStyleboxOverride(
                "hover",
                InventoryPanelChrome.CreateButtonStyle(InventoryPanelChrome.ButtonHoverColor, Accent, 1));
            button.AddThemeStyleboxOverride(
                "pressed",
                InventoryPanelChrome.CreateButtonStyle(DeepSurface, Accent, 1));
            button.AddThemeColorOverride("font_color", MainText);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            if (action != null) button.Pressed += action;
            return button;
        }

        private void SetBar(ProgressBar bar, Label label, float current, float maximum)
        {
            float safeMaximum = Mathf.Max(1f, maximum);
            float safeCurrent = Mathf.Clamp(current, 0f, safeMaximum);
            bar.MinValue = 0;
            bar.MaxValue = safeMaximum;
            bar.Value = safeCurrent;
            label.Text = $"{Mathf.RoundToInt(safeCurrent)}/{Mathf.RoundToInt(safeMaximum)}";
        }

        private StyleBoxFlat CreateBodyStyle()
        {
            StyleBoxFlat style = new();
            style.BgColor = new Color(DeepSurface.R, DeepSurface.G, DeepSurface.B, 0.58f);
            style.BorderColor = new Color(Border.R, Border.G, Border.B, 0.84f);
            style.SetBorderWidthAll(1);
            return style;
        }

        private StyleBoxFlat CreateMemberCardStyle(bool selected, bool leader, Color characterAccent)
        {
            Color borderColor = selected ? characterAccent : leader ? Accent : Border.Darkened(0.05f);
            StyleBoxFlat style = new();
            style.BgColor = selected
                ? new Color(characterAccent.R, characterAccent.G, characterAccent.B, 0.13f)
                : InventoryPanelChrome.SlotSurfaceColor;
            style.BorderColor = borderColor;
            style.SetBorderWidthAll(selected ? 2 : 1);
            style.SetCornerRadiusAll(3);
            if (selected || leader)
            {
                style.ShadowColor = new Color(borderColor.R, borderColor.G, borderColor.B, 0.20f);
                style.ShadowSize = 4;
            }
            return style;
        }

        private StyleBoxFlat CreateEmptySlotStyle()
        {
            StyleBoxFlat style = InventoryPanelChrome.CreateSlotStyle(false);
            style.BgColor = new Color(
                InventoryPanelChrome.SlotSurfaceColor.R,
                InventoryPanelChrome.SlotSurfaceColor.G,
                InventoryPanelChrome.SlotSurfaceColor.B,
                0.45f);
            style.BorderColor = new Color(Border.R, Border.G, Border.B, 0.45f);
            return style;
        }
    }
}
