using Godot;
using AshesofaDyingWorld.Quests.Data;
using AshesofaDyingWorld.Quests.Runtime;
using AshesofaDyingWorld.UI.Shared;

namespace AshesofaDyingWorld.UI.Quests
{
    /// <summary>
    /// Tracker tối giản ngoài HUD. Chỉ hiện nhiệm vụ đang theo dõi và mục tiêu hiện tại,
    /// không sao chép nguyên nhật ký nhiệm vụ ra màn hình chơi.
    /// </summary>
    public partial class QuestTrackerHud : PanelContainer
    {
        [Export] public NodePath QuestJournalPath { get; set; }

        private QuestJournalPanel _journal;
        private bool _menuSuppressed;
        private Label _titleLabel;
        private Label _objectiveLabel;
        private Label _progressLabel;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            BuildInterface();

            _journal = GetNodeOrNull<QuestJournalPanel>(QuestJournalPath);
            if (_journal != null)
            {
                _journal.JournalChanged += RefreshTracker;
                _journal.VisibilityChanged += RefreshTracker;
            }
            RefreshTracker();
        }

        public override void _ExitTree()
        {
            if (_journal != null)
            {
                _journal.JournalChanged -= RefreshTracker;
                _journal.VisibilityChanged -= RefreshTracker;
            }
        }

        private void BuildInterface()
        {
            AddThemeStyleboxOverride("panel", CreateTrackerStyle());

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_top", 9);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_bottom", 9);
            AddChild(margin);

            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 3);
            margin.AddChild(column);

            _titleLabel = InventoryPanelChrome.CreateLabel("", 13, InventoryPanelChrome.MainTextColor);
            _titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(_titleLabel);

            _objectiveLabel = InventoryPanelChrome.CreateLabel("", 12, InventoryPanelChrome.MutedTextColor);
            _objectiveLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(_objectiveLabel);

            _progressLabel = InventoryPanelChrome.CreateLabel("", 11, InventoryPanelChrome.AccentColor);
            column.AddChild(_progressLabel);
        }

        /// <summary>
        /// Ẩn tracker khi menu lớn hoặc một feature panel đang mở để HUD không đè lên giao diện.
        /// </summary>
        public void SetMenuSuppressed(bool suppressed)
        {
            _menuSuppressed = suppressed;
            RefreshTracker();
        }

        private void RefreshTracker()
        {
            if (_menuSuppressed || _journal == null || _journal.Visible || _journal.Manager == null)
            {
                Hide();
                return;
            }

            QuestManager manager = _journal.Manager;
            QuestData quest = manager.GetDefinition(manager.TrackedQuestId);
            QuestRuntimeState state = manager.GetState(manager.TrackedQuestId);
            QuestObjectiveData objective = manager.GetCurrentObjective(manager.TrackedQuestId);
            if (quest == null || state == null || state.Status != QuestStatus.Active || objective == null)
            {
                Hide();
                return;
            }

            _titleLabel.Text = quest.Title.ToUpperInvariant();
            _objectiveLabel.Text = $"◇ {objective.Description}";

            int progress = state.ObjectiveProgress.TryGetValue(objective.ObjectiveId, out int value) ? value : 0;
            _progressLabel.Text = objective.RequiredAmount > 1
                ? $"{progress}/{objective.RequiredAmount} {objective.UnitLabel}".Trim()
                : "";
            Show();
        }

        private StyleBoxFlat CreateTrackerStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(
                InventoryPanelChrome.DeepSurfaceColor.R,
                InventoryPanelChrome.DeepSurfaceColor.G,
                InventoryPanelChrome.DeepSurfaceColor.B,
                0.88f);
            style.BorderColor = new Color(
                InventoryPanelChrome.BorderColor.R,
                InventoryPanelChrome.BorderColor.G,
                InventoryPanelChrome.BorderColor.B,
                0.86f);
            style.SetBorderWidthAll(1);
            style.BorderWidthLeft = 3;
            style.SetCornerRadiusAll(3);
            style.ShadowColor = new Color(0f, 0f, 0f, 0.35f);
            style.ShadowSize = 5;
            return style;
        }
    }
}
