using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Quests.Data;
using AshesofaDyingWorld.Quests.Runtime;
using AshesofaDyingWorld.UI.Shared;

namespace AshesofaDyingWorld.UI.Quests
{
    /// <summary>
    /// Nhật ký nhiệm vụ riêng mở từ menu chính.
    /// Bố cục hai cột: danh sách bên trái, chi tiết và mục tiêu bên phải.
    /// </summary>
    public partial class QuestJournalPanel : Panel
    {
        private const string FilterAll = "all";
        private const string FilterMain = "main";
        private const string FilterSide = "side";
        private const string FilterCharacter = "character";

        private QuestManager _questManager;
        private QuestStatus _statusFilter = QuestStatus.Active;
        private string _typeFilter = FilterAll;
        private string _selectedQuestId = "";

        private readonly Dictionary<QuestStatus, Button> _statusButtons = new();
        private readonly Dictionary<string, Button> _typeButtons = new();
        private readonly Dictionary<string, PanelContainer> _questCards = new();

        private VBoxContainer _questListContainer;
        private Label _activeCountLabel;
        private Label _detailTitleLabel;
        private Label _detailBadgeLabel;
        private Label _detailMetaLabel;
        private Label _detailSummaryLabel;
        private Label _detailDescriptionLabel;
        private VBoxContainer _objectiveContainer;
        private HFlowContainer _rewardContainer;
        private Label _actionHintLabel;
        private Button _primaryActionButton;
        private Button _mapButton;

        private Color MainText => InventoryPanelChrome.MainTextColor;
        private Color MutedText => InventoryPanelChrome.MutedTextColor;
        private Color Accent => InventoryPanelChrome.AccentColor;
        private Color Border => InventoryPanelChrome.BorderColor;
        private Color DeepSurface => InventoryPanelChrome.DeepSurfaceColor;

        /// <summary>
        /// Tracker HUD và các thành phần ngoài panel lắng nghe sự kiện này để tự làm mới.
        /// </summary>
        public event Action JournalChanged;

        public QuestManager Manager => _questManager;

        public override void _Ready()
        {
            InventoryPanelChrome.ApplyPanelSize(this);
            AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

            _questManager = new QuestManager { Name = "QuestManager" };
            AddChild(_questManager);
            _questManager.Changed += OnQuestManagerChanged;

            BuildInterface();
            _questManager.InitializeFromDirectory();

            VisibilityChanged += OnVisibilityChanged;
            RefreshJournal();
        }

        public override void _ExitTree()
        {
            if (_questManager != null)
            {
                _questManager.Changed -= OnQuestManagerChanged;
            }
        }

        /// <summary>
        /// Làm mới toàn bộ panel khi được mở hoặc sau khi tiến độ nhiệm vụ thay đổi.
        /// </summary>
        public void RefreshJournal()
        {
            RefreshStatusButtons();
            RefreshTypeButtons();
            RefreshQuestList();
            RefreshQuestDetail();
        }

        public List<QuestProgressRecord> CaptureProgress()
        {
            return _questManager?.CaptureProgress() ?? new List<QuestProgressRecord>();
        }

        public void RestoreProgress(IReadOnlyList<QuestProgressRecord> records, string trackedQuestId)
        {
            if (_questManager == null)
            {
                return;
            }

            _questManager.RestoreProgress(records, trackedQuestId);
            RefreshJournal();
        }

        private void BuildInterface()
        {
            VBoxContainer root = InventoryPanelChrome.BuildWindowShell(this);
            root.AddChild(BuildHeader());
            root.AddChild(BuildStatusBar());
            root.AddChild(BuildTypeFilterBar());
            root.AddChild(BuildBody());
        }

        private Control BuildHeader()
        {
            PanelContainer header = InventoryPanelChrome.CreateHeader(out HBoxContainer row);

            Texture2D questIcon = InventoryPanelChrome.TryLoadTexture(
                "res://assets/graphics/ui/inventory/category_quest.png");
            if (questIcon != null)
            {
                var icon = new TextureRect
                {
                    Texture = questIcon,
                    CustomMinimumSize = new Vector2(34, 34),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest
                };
                row.AddChild(icon);
            }

            Label title = CreateLabel("NHIỆM VỤ", 18, MainText);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            title.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(title);

            _activeCountLabel = CreateLabel("0 đang thực hiện", 13, MutedText);
            _activeCountLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(_activeCountLabel);

            row.AddChild(InventoryPanelChrome.CreateCloseButton(Hide));
            return header;
        }

        private Control BuildStatusBar()
        {
            PanelContainer panel = InventoryPanelChrome.CreateTabBar(out HBoxContainer row, 8);

            row.AddChild(CreateStatusButton(QuestStatus.Available, "CÓ THỂ NHẬN"));
            row.AddChild(CreateStatusButton(QuestStatus.Active, "ĐANG LÀM"));
            row.AddChild(CreateStatusButton(QuestStatus.Completed, "HOÀN THÀNH"));
            row.AddChild(CreateStatusButton(QuestStatus.Failed, "THẤT BẠI"));
            return panel;
        }

        private Control BuildTypeFilterBar()
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(0, 42);
            panel.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreateDetailSectionStyle());

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 10);
            margin.AddThemeConstantOverride("margin_top", 5);
            margin.AddThemeConstantOverride("margin_right", 10);
            margin.AddThemeConstantOverride("margin_bottom", 5);
            panel.AddChild(margin);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            margin.AddChild(row);

            row.AddChild(CreateTypeButton(FilterAll, "Tất cả"));
            row.AddChild(CreateTypeButton(FilterMain, "Chính tuyến"));
            row.AddChild(CreateTypeButton(FilterSide, "Nhiệm vụ phụ"));
            row.AddChild(CreateTypeButton(FilterCharacter, "Nhân vật"));
            return panel;
        }

        private Control BuildBody()
        {
            var panel = new PanelContainer();
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.SizeFlagsVertical = SizeFlags.ExpandFill;
            panel.AddThemeStyleboxOverride("panel", CreateBodyStyle());

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_top", 12);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_bottom", 12);
            panel.AddChild(margin);

            var body = new HBoxContainer();
            body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            body.SizeFlagsVertical = SizeFlags.ExpandFill;
            body.AddThemeConstantOverride("separation", 0);
            margin.AddChild(body);

            body.AddChild(BuildQuestListColumn());
            body.AddChild(CreateVerticalDivider());
            body.AddChild(BuildQuestDetailColumn());
            return panel;
        }

        private Control BuildQuestListColumn()
        {
            var frame = CreateColumn(390, out VBoxContainer column);
            column.AddChild(CreateSectionTitle("DANH SÁCH", HorizontalAlignment.Left));
            column.AddChild(InventoryPanelChrome.CreateDivider());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddChild(scroll);

            _questListContainer = new VBoxContainer();
            _questListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _questListContainer.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_questListContainer);
            return frame;
        }

        private Control BuildQuestDetailColumn()
        {
            var frame = CreateColumn(610, out VBoxContainer column);
            frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            column.AddChild(CreateSectionTitle("CHI TIẾT NHIỆM VỤ", HorizontalAlignment.Left));
            column.AddChild(InventoryPanelChrome.CreateDivider());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddChild(scroll);

            var content = new VBoxContainer();
            content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            content.AddThemeConstantOverride("separation", 9);
            scroll.AddChild(content);

            _detailTitleLabel = CreateLabel("Chưa chọn nhiệm vụ", 19, MainText);
            _detailTitleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(_detailTitleLabel);

            var badgeRow = new HBoxContainer();
            badgeRow.AddThemeConstantOverride("separation", 8);
            content.AddChild(badgeRow);

            _detailBadgeLabel = CreateLabel("", 12, MainText);
            _detailBadgeLabel.AddThemeStyleboxOverride("normal", CreateBadgeStyle());
            badgeRow.AddChild(_detailBadgeLabel);

            _detailMetaLabel = CreateLabel("", 12, MutedText);
            _detailMetaLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _detailMetaLabel.VerticalAlignment = VerticalAlignment.Center;
            badgeRow.AddChild(_detailMetaLabel);

            _detailSummaryLabel = CreateLabel("", 13, MainText);
            _detailSummaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(_detailSummaryLabel);

            _detailDescriptionLabel = CreateLabel(
                "Chọn một nhiệm vụ ở danh sách bên trái để xem nội dung.",
                12,
                MutedText);
            _detailDescriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(_detailDescriptionLabel);

            content.AddChild(CreateSectionTitle("MỤC TIÊU", HorizontalAlignment.Left));
            content.AddChild(InventoryPanelChrome.CreateDivider(true));

            _objectiveContainer = new VBoxContainer();
            _objectiveContainer.AddThemeConstantOverride("separation", 6);
            content.AddChild(_objectiveContainer);

            content.AddChild(CreateSectionTitle("PHẦN THƯỞNG", HorizontalAlignment.Left));
            content.AddChild(InventoryPanelChrome.CreateDivider(true));

            _rewardContainer = new HFlowContainer();
            _rewardContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _rewardContainer.AddThemeConstantOverride("h_separation", 8);
            _rewardContainer.AddThemeConstantOverride("v_separation", 8);
            content.AddChild(_rewardContainer);

            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            content.AddChild(actionRow);

            _primaryActionButton = CreateActionButton("THEO DÕI");
            _primaryActionButton.Pressed += OnPrimaryActionPressed;
            actionRow.AddChild(_primaryActionButton);

            _mapButton = CreateActionButton("XEM BẢN ĐỒ");
            PixelButtonSkin.ApplySecondary(_mapButton, PixelButtonSkin.RegularHeight);
            _mapButton.Pressed += OnMapButtonPressed;
            actionRow.AddChild(_mapButton);

            _actionHintLabel = CreateLabel("", 11, MutedText);
            _actionHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(_actionHintLabel);
            return frame;
        }

        private Button CreateStatusButton(QuestStatus status, string text)
        {
            var button = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(132, 34),
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            button.Pressed += () =>
            {
                _statusFilter = status;
                RefreshJournal();
            };
            _statusButtons[status] = button;
            return button;
        }

        private Button CreateTypeButton(string filterId, string text)
        {
            var button = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(0, 30),
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            button.AddThemeFontSizeOverride("font_size", 12);
            button.Pressed += () =>
            {
                _typeFilter = filterId;
                RefreshJournal();
            };
            _typeButtons[filterId] = button;
            return button;
        }

        private void RefreshStatusButtons()
        {
            int availableCount = CountQuestsForStatus(QuestStatus.Available);
            int activeCount = CountQuestsForStatus(QuestStatus.Active);
            int completedCount = CountQuestsForStatus(QuestStatus.Completed);
            int failedCount = CountQuestsForStatus(QuestStatus.Failed);

            _activeCountLabel.Text = $"{activeCount} đang thực hiện";
            ApplyStatusButtonStyle(QuestStatus.Available, $"CÓ THỂ NHẬN {availableCount}");
            ApplyStatusButtonStyle(QuestStatus.Active, $"ĐANG LÀM {activeCount}");
            ApplyStatusButtonStyle(QuestStatus.Completed, $"HOÀN THÀNH {completedCount}");
            ApplyStatusButtonStyle(QuestStatus.Failed, $"THẤT BẠI {failedCount}");
        }

        private void ApplyStatusButtonStyle(QuestStatus status, string text)
        {
            if (!_statusButtons.TryGetValue(status, out Button button)) return;
            bool selected = _statusFilter == status;
            button.Text = text;
            InventoryPanelChrome.ApplyTabStyle(button, selected);
        }

        private void RefreshTypeButtons()
        {
            foreach (var pair in _typeButtons)
            {
                bool selected = pair.Key == _typeFilter;
                PixelButtonSkin.ApplyTab(pair.Value, selected, PixelButtonSkin.CompactHeight);
                pair.Value.AddThemeColorOverride("font_color", selected ? MainText : MutedText);
            }
        }

        private void RefreshQuestList()
        {
            if (_questListContainer == null || _questManager == null) return;

            foreach (Node child in _questListContainer.GetChildren())
            {
                child.QueueFree();
            }
            _questCards.Clear();

            List<QuestData> quests = GetFilteredQuests();
            if (quests.Count == 0)
            {
                _selectedQuestId = "";
                _questListContainer.AddChild(CreateEmptyState(
                    "Không có nhiệm vụ phù hợp với bộ lọc hiện tại."));
                return;
            }

            bool selectionStillVisible = false;
            foreach (QuestData quest in quests)
            {
                if (quest.QuestId == _selectedQuestId)
                {
                    selectionStillVisible = true;
                    break;
                }
            }
            if (!selectionStillVisible)
            {
                _selectedQuestId = quests[0].QuestId;
            }

            foreach (QuestData quest in quests)
            {
                _questListContainer.AddChild(CreateQuestCard(quest));
            }
            RefreshQuestCardSelection();
        }

        private Control CreateQuestCard(QuestData quest)
        {
            QuestRuntimeState state = _questManager.GetState(quest.QuestId);
            bool selected = quest.QuestId == _selectedQuestId;

            var card = new PanelContainer();
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            card.CustomMinimumSize = new Vector2(0, 76);
            card.AddThemeStyleboxOverride("panel", CreateQuestCardStyle(selected, state));

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_top", 9);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_bottom", 9);
            card.AddChild(margin);

            var content = new VBoxContainer();
            content.AddThemeConstantOverride("separation", 2);
            margin.AddChild(content);

            var titleRow = new HBoxContainer();
            titleRow.AddThemeConstantOverride("separation", 6);
            content.AddChild(titleRow);

            Label title = CreateLabel(quest.Title.ToUpperInvariant(), 14, MainText);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            titleRow.AddChild(title);

            if (state != null && state.IsNew)
            {
                titleRow.AddChild(CreateLabel("!", 15, Accent));
            }
            if (_questManager.TrackedQuestId == quest.QuestId)
            {
                titleRow.AddChild(CreateLabel("◆", 13, Accent));
            }

            string location = string.IsNullOrWhiteSpace(quest.LocationName)
                ? GetQuestTypeLabel(quest.QuestType)
                : $"{GetQuestTypeLabel(quest.QuestType)} · {quest.LocationName}";
            content.AddChild(CreateLabel(location, 12, MutedText));
            content.AddChild(CreateLabel(BuildQuestCardProgress(quest, state), 12, MainText));

            var button = new Button();
            button.SetAnchorsPreset(LayoutPreset.FullRect);
            button.FocusMode = FocusModeEnum.None;
            button.MouseDefaultCursorShape = CursorShape.PointingHand;
            button.AddThemeStyleboxOverride("normal", InventoryPanelChrome.CreateTransparentButtonStyle());
            button.AddThemeStyleboxOverride("hover", InventoryPanelChrome.CreateTransparentButtonStyle());
            button.AddThemeStyleboxOverride("pressed", InventoryPanelChrome.CreateTransparentButtonStyle());
            button.Pressed += () =>
            {
                _selectedQuestId = quest.QuestId;
                if (state != null) state.IsNew = false;
                RefreshQuestCardSelection();
                RefreshQuestDetail();
                JournalChanged?.Invoke();
            };
            card.AddChild(button);

            _questCards[quest.QuestId] = card;
            return card;
        }

        private void RefreshQuestCardSelection()
        {
            foreach (var pair in _questCards)
            {
                QuestRuntimeState state = _questManager.GetState(pair.Key);
                pair.Value.AddThemeStyleboxOverride(
                    "panel",
                    CreateQuestCardStyle(pair.Key == _selectedQuestId, state));
            }
        }

        private void RefreshQuestDetail()
        {
            ClearContainer(_objectiveContainer);
            ClearContainer(_rewardContainer);

            QuestData quest = _questManager?.GetDefinition(_selectedQuestId);
            QuestRuntimeState state = _questManager?.GetState(_selectedQuestId);
            if (quest == null || state == null)
            {
                _detailTitleLabel.Text = "Chưa chọn nhiệm vụ";
                _detailBadgeLabel.Text = "";
                _detailMetaLabel.Text = "";
                _detailSummaryLabel.Text = "";
                _detailDescriptionLabel.Text = "Chọn một nhiệm vụ ở danh sách bên trái để xem nội dung.";
                _primaryActionButton.Disabled = true;
                _mapButton.Visible = false;
                _actionHintLabel.Text = "";
                return;
            }

            _detailTitleLabel.Text = quest.Title.ToUpperInvariant();
            _detailBadgeLabel.Text = $"  {GetQuestTypeLabel(quest.QuestType)}  ";

            var metaParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(quest.Chapter)) metaParts.Add(quest.Chapter);
            if (!string.IsNullOrWhiteSpace(quest.QuestGiver)) metaParts.Add($"Người giao: {quest.QuestGiver}");
            if (!string.IsNullOrWhiteSpace(quest.LocationName)) metaParts.Add($"Địa điểm: {quest.LocationName}");
            _detailMetaLabel.Text = string.Join(" · ", metaParts);

            _detailSummaryLabel.Text = quest.Summary ?? "";
            _detailDescriptionLabel.Text = string.IsNullOrWhiteSpace(quest.Description)
                ? "Nhiệm vụ này chưa có mô tả."
                : quest.Description;

            BuildObjectiveRows(quest, state);
            BuildRewardCards(quest);
            RefreshDetailActions(quest, state);
        }

        private void BuildObjectiveRows(QuestData quest, QuestRuntimeState state)
        {
            if (quest.Objectives == null || quest.Objectives.Count == 0)
            {
                _objectiveContainer.AddChild(CreateLabel("Chưa có mục tiêu được khai báo.", 12, MutedText));
                return;
            }

            QuestObjectiveData current = _questManager.GetCurrentObjective(quest.QuestId);
            foreach (QuestObjectiveData objective in quest.Objectives)
            {
                if (objective == null || !_questManager.IsObjectiveVisible(quest, state, objective))
                {
                    continue;
                }

                bool complete = _questManager.IsObjectiveComplete(objective, state);
                bool isCurrent = objective == current;
                int progress = state.ObjectiveProgress.TryGetValue(objective.ObjectiveId, out int value) ? value : 0;

                var row = new HBoxContainer();
                row.CustomMinimumSize = new Vector2(0, 28);
                row.AddThemeConstantOverride("separation", 8);
                _objectiveContainer.AddChild(row);

                string marker = complete ? "✓" : isCurrent ? "◇" : "○";
                Color markerColor = complete ? MutedText : isCurrent ? Accent : MutedText;
                row.AddChild(CreateLabel(marker, 14, markerColor));

                Label description = CreateLabel(objective.Description, 12, complete ? MutedText : MainText);
                description.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                row.AddChild(description);

                if (objective.RequiredAmount > 1)
                {
                    string unit = string.IsNullOrWhiteSpace(objective.UnitLabel)
                        ? $"{progress}/{objective.RequiredAmount}"
                        : $"{progress}/{objective.RequiredAmount} {objective.UnitLabel}";
                    row.AddChild(CreateLabel(unit, 12, isCurrent ? Accent : MutedText));
                }
            }
        }

        private void BuildRewardCards(QuestData quest)
        {
            if (quest.Rewards == null || quest.Rewards.Count == 0)
            {
                _rewardContainer.AddChild(CreateLabel("Không có phần thưởng được khai báo.", 12, MutedText));
                return;
            }

            foreach (QuestRewardData reward in quest.Rewards)
            {
                if (reward == null) continue;
                _rewardContainer.AddChild(CreateRewardCard(reward));
            }
        }

        private Control CreateRewardCard(QuestRewardData reward)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(150, 44);
            panel.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreatePreviewStyle());

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            panel.AddChild(margin);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 7);
            margin.AddChild(row);

            Texture2D iconTexture = reward.Icon ?? InventoryPanelChrome.TryLoadTexture(
                "res://assets/graphics/ui/inventory/category_quest.png");
            if (iconTexture != null)
            {
                row.AddChild(new TextureRect
                {
                    Texture = iconTexture,
                    CustomMinimumSize = new Vector2(28, 28),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest
                });
            }

            string displayName = string.IsNullOrWhiteSpace(reward.DisplayName)
                ? GetRewardTypeLabel(reward.RewardType)
                : reward.DisplayName;
            string text = reward.Amount > 0 ? $"{displayName} {reward.Amount}" : displayName;
            Label label = CreateLabel(text, 12, MainText);
            label.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(label);
            return panel;
        }

        private void RefreshDetailActions(QuestData quest, QuestRuntimeState state)
        {
            _mapButton.Visible = !string.IsNullOrWhiteSpace(quest.MapHint);
            _mapButton.Disabled = false;

            switch (state.Status)
            {
                case QuestStatus.Available:
                    _primaryActionButton.Text = "NHẬN NHIỆM VỤ";
                    _primaryActionButton.Disabled = false;
                    _actionHintLabel.Text = $"Yêu cầu cấp {Mathf.Max(1, quest.RequiredCharacterLevel)}.";
                    break;
                case QuestStatus.Active:
                    bool tracked = _questManager.TrackedQuestId == quest.QuestId;
                    _primaryActionButton.Text = tracked ? "BỎ THEO DÕI" : "THEO DÕI";
                    _primaryActionButton.Disabled = false;
                    _actionHintLabel.Text = tracked
                        ? "Nhiệm vụ này đang xuất hiện trên tracker ngoài HUD."
                        : "Theo dõi để hiển thị mục tiêu hiện tại ngoài HUD.";
                    break;
                case QuestStatus.Completed:
                    _primaryActionButton.Text = "ĐÃ HOÀN THÀNH";
                    _primaryActionButton.Disabled = true;
                    _actionHintLabel.Text = "Tất cả mục tiêu đã hoàn thành.";
                    break;
                default:
                    _primaryActionButton.Text = "ĐÃ THẤT BẠI";
                    _primaryActionButton.Disabled = true;
                    _actionHintLabel.Text = "Nhiệm vụ này không còn có thể tiếp tục.";
                    break;
            }
        }

        private void OnPrimaryActionPressed()
        {
            QuestData quest = _questManager?.GetDefinition(_selectedQuestId);
            QuestRuntimeState state = _questManager?.GetState(_selectedQuestId);
            if (quest == null || state == null) return;

            bool changed = false;
            if (state.Status == QuestStatus.Available)
            {
                int level = GetActiveCharacterLevel();
                changed = _questManager.AcceptQuest(quest.QuestId, level);
                if (!changed)
                {
                    _actionHintLabel.Text = $"Cần đạt cấp {Mathf.Max(1, quest.RequiredCharacterLevel)} để nhận nhiệm vụ.";
                }
            }
            else if (state.Status == QuestStatus.Active)
            {
                changed = _questManager.ToggleTrackedQuest(quest.QuestId);
            }

            if (changed)
            {
                RefreshJournal();
            }
        }

        private void OnMapButtonPressed()
        {
            QuestData quest = _questManager?.GetDefinition(_selectedQuestId);
            if (quest == null || string.IsNullOrWhiteSpace(quest.MapHint)) return;

            // MapPanel hiện chưa có hệ thống marker thật. Giữ dữ liệu MapHint ở QuestData
            // để sau này nối trực tiếp, còn hiện tại vẫn cho người chơi biết nơi cần đến.
            _actionHintLabel.Text = $"Gợi ý bản đồ: {quest.MapHint}";
            GD.Print($"[QuestJournal] Map hint for {quest.QuestId}: {quest.MapHint}");
        }

        private void OnQuestManagerChanged()
        {
            RefreshJournal();
            JournalChanged?.Invoke();
        }

        private void OnVisibilityChanged()
        {
            if (Visible)
            {
                RefreshJournal();
            }
            JournalChanged?.Invoke();
        }

        private List<QuestData> GetFilteredQuests()
        {
            var result = new List<QuestData>();
            if (_questManager == null) return result;

            foreach (var pair in _questManager.Definitions)
            {
                QuestData quest = pair.Value;
                QuestRuntimeState state = _questManager.GetState(pair.Key);
                if (quest == null || state == null) continue;

                bool statusMatches = state.Status == _statusFilter;
                if (!statusMatches || !MatchesTypeFilter(quest)) continue;
                result.Add(quest);
            }

            result.Sort((left, right) => string.Compare(left.Title, right.Title, StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        private bool MatchesTypeFilter(QuestData quest)
        {
            return _typeFilter switch
            {
                FilterMain => quest.QuestType == QuestType.Main,
                FilterSide => quest.QuestType == QuestType.Side,
                FilterCharacter => quest.QuestType == QuestType.Character,
                _ => true
            };
        }

        private int CountQuestsForStatus(QuestStatus status)
        {
            if (_questManager == null) return 0;
            int count = 0;
            foreach (var pair in _questManager.States)
            {
                if (pair.Value.Status == status)
                {
                    count++;
                }
            }
            return count;
        }

        private string BuildQuestCardProgress(QuestData quest, QuestRuntimeState state)
        {
            if (state == null) return "";
            return state.Status switch
            {
                QuestStatus.Available => $"Có thể nhận · Yêu cầu cấp {Mathf.Max(1, quest.RequiredCharacterLevel)}",
                QuestStatus.Completed => "Đã hoàn thành",
                QuestStatus.Failed => "Thất bại",
                _ => $"{_questManager.GetCompletedObjectiveCount(quest, state)}/{quest.Objectives.Count} mục tiêu"
            };
        }

        private string GetQuestTypeLabel(QuestType type)
        {
            return type switch
            {
                QuestType.Main => "Chính tuyến",
                QuestType.Character => "Nhân vật",
                _ => "Nhiệm vụ phụ"
            };
        }

        private string GetRewardTypeLabel(QuestRewardType type)
        {
            return type switch
            {
                QuestRewardType.Gold => "Vàng",
                QuestRewardType.Item => "Vật phẩm",
                QuestRewardType.SkillPoint => "Điểm KN",
                _ => "XP"
            };
        }

        private int GetActiveCharacterLevel()
        {
            if (PlayerManager.Instance == null || PlayerManager.Instance.PartyMembers.Count == 0)
            {
                return 1;
            }

            int index = Mathf.Clamp(
                PlayerManager.Instance.ActiveCharacterIndex,
                0,
                PlayerManager.Instance.PartyMembers.Count - 1);
            return PlayerManager.Instance.PartyMembers[index]?.CurrentLevel ?? 1;
        }

        private Label CreateLabel(string text, int size, Color color)
        {
            return InventoryPanelChrome.CreateLabel(text, size, color);
        }

        private MarginContainer CreateColumn(float width, out VBoxContainer column)
        {
            var frame = new MarginContainer();
            frame.CustomMinimumSize = new Vector2(width, 0);
            frame.SizeFlagsVertical = SizeFlags.ExpandFill;
            frame.AddThemeConstantOverride("margin_left", 16);
            frame.AddThemeConstantOverride("margin_top", 4);
            frame.AddThemeConstantOverride("margin_right", 16);
            frame.AddThemeConstantOverride("margin_bottom", 4);

            column = new VBoxContainer();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddThemeConstantOverride("separation", 8);
            frame.AddChild(column);
            return frame;
        }

        private Label CreateSectionTitle(string text, HorizontalAlignment alignment)
        {
            Label label = CreateLabel(text, 14, MainText);
            label.CustomMinimumSize = new Vector2(0, 28);
            label.HorizontalAlignment = alignment;
            label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        private ColorRect CreateVerticalDivider()
        {
            return new ColorRect
            {
                Color = new Color(Border.R, Border.G, Border.B, 0.72f),
                CustomMinimumSize = new Vector2(1, 0),
                MouseFilter = MouseFilterEnum.Ignore
            };
        }

        private Control CreateEmptyState(string message)
        {
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreateDetailSectionStyle());
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 16);
            margin.AddThemeConstantOverride("margin_top", 24);
            margin.AddThemeConstantOverride("margin_right", 16);
            margin.AddThemeConstantOverride("margin_bottom", 24);
            panel.AddChild(margin);
            Label label = CreateLabel(message, 13, MutedText);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            margin.AddChild(label);
            return panel;
        }

        private Button CreateActionButton(string text)
        {
            var button = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(0, 36),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            PixelButtonSkin.ApplyPrimary(button, PixelButtonSkin.RegularHeight);
            return button;
        }

        private StyleBoxFlat CreateBodyStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(DeepSurface.R, DeepSurface.G, DeepSurface.B, 0.58f);
            style.BorderColor = new Color(Border.R, Border.G, Border.B, 0.84f);
            style.SetBorderWidthAll(1);
            return style;
        }

        private StyleBoxFlat CreateBadgeStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(Accent.R, Accent.G, Accent.B, 0.13f);
            style.BorderColor = new Color(Accent.R, Accent.G, Accent.B, 0.60f);
            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(99);
            style.ContentMarginLeft = 7;
            style.ContentMarginRight = 7;
            style.ContentMarginTop = 3;
            style.ContentMarginBottom = 3;
            return style;
        }

        private StyleBoxFlat CreateFilterStyle(bool selected)
        {
            return InventoryPanelChrome.CreateButtonStyle(
                selected
                    ? new Color(Accent.R, Accent.G, Accent.B, 0.16f)
                    : InventoryPanelChrome.ButtonNormalColor,
                selected ? Accent : Border,
                selected ? 2 : 1);
        }

        private StyleBoxFlat CreateQuestCardStyle(bool selected, QuestRuntimeState state)
        {
            var style = new StyleBoxFlat();
            style.BgColor = selected
                ? InventoryPanelChrome.RaisedSurfaceColor.Lightened(0.02f)
                : InventoryPanelChrome.SlotSurfaceColor;
            style.BorderColor = selected ? Accent : Border.Darkened(0.05f);
            style.SetBorderWidthAll(selected ? 2 : 1);
            style.SetCornerRadiusAll(3);

            if (selected)
            {
                style.BorderWidthLeft = 4;
                style.ShadowColor = new Color(Accent.R, Accent.G, Accent.B, 0.20f);
                style.ShadowSize = 4;
            }
            else if (state?.Status == QuestStatus.Failed)
            {
                style.BorderColor = InventoryPanelChrome.DangerColor;
            }
            return style;
        }

        private static void ClearContainer(Node container)
        {
            if (container == null) return;
            foreach (Node child in container.GetChildren())
            {
                child.QueueFree();
            }
        }
    }
}
