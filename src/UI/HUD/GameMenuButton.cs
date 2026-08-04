using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.UI.HUD;
using AshesofaDyingWorld.UI.Skills;
using AshesofaDyingWorld.UI.Quests;
using PartyManagementPanel = AshesofaDyingWorld.UI.Party.PartyPanel;
using AshesofaDyingWorld.Quests.Runtime;

namespace AshesofaDyingWorld.UI.Menus
{
    /// <summary>
    /// Button menu tổng quan - Dùng CanvasLayer để luôn hiện trên cùng
    /// </summary>
    public partial class GameMenuButton : CanvasLayer
    {
        [ExportGroup("Main Button")]
        [Export] public Button MenuButton;
        
        [ExportGroup("Menu Grid Panel")]
        [Export] public Panel MenuGridPanel;
        [Export] public GridContainer MenuGrid;
        
        [ExportGroup("Feature Buttons")]
        [Export] public Button CharacterButton;
        [Export] public Button InventoryButton;
        [Export] public Button SkillsButton;
        [Export] public Button QuestsButton;
        [Export] public Button SettingsButton;
        [Export] public Button MapButton;
        [Export] public Button PartyButton;
        [Export] public Button AchievementsButton;
        
        [ExportGroup("Feature Panels")]
        [Export] public Control CharacterPanel;
        [Export] public Control InventoryPanel;
        [Export] public Control SkillsPanel;
        [Export] public Control QuestsPanel;
        [Export] public Control SettingsPanel;
        [Export] public Control MapPanel;
        [Export] public Control PartyPanel;
        [Export] public Control AchievementsPanel;

        [ExportGroup("Settings")]
        [Export] public Key ToggleKey = Key.Escape;
        
        private bool _isGridOpen = false;
        private Control _currentOpenPanel = null;
        private QuestTrackerHud _questTracker;

        public override void _Ready()
        {
            _questTracker = GetNodeOrNull<QuestTrackerHud>("Control/QuestTrackerHud");

            if (MenuButton != null)
            {
                MenuButton.Pressed += ToggleMenuGrid;
            }
            
            if (MenuGridPanel != null)
            {
                MenuGridPanel.Hide();
            }
            
            // CharacterDetailUI đã được gán sẵn trong tscn (CharacterPanel có script CharacterDetailUI)
            // Không cần tạo thêm ở đây
            
            HideAllPanels();
            RegisterPanelVisibilityHandlers();
            ConnectFeatureButtons();
        }

        private void ConnectFeatureButtons()
        {
            if (CharacterButton != null)
                CharacterButton.Pressed += () => OpenPanel(CharacterPanel, "Character");
            
            if (InventoryButton != null)
                InventoryButton.Pressed += OpenInventoryTab;
            
            if (SkillsButton != null)
                SkillsButton.Pressed += () => OpenPanel(SkillsPanel, "Skills");
            
            if (QuestsButton != null)
                QuestsButton.Pressed += () => OpenPanel(QuestsPanel, "Quests");
            
            if (SettingsButton != null)
                SettingsButton.Pressed += () => OpenPanel(SettingsPanel, "Settings");
            
            if (MapButton != null)
                MapButton.Pressed += () => OpenPanel(MapPanel, "Map");
            
            if (PartyButton != null)
                PartyButton.Pressed += () => OpenPanel(PartyPanel, "Party");
            
            if (AchievementsButton != null)
                AchievementsButton.Pressed += () => OpenPanel(AchievementsPanel, "Achievements");
            
        }

        private void OpenInventoryTab()
        {
            OpenPanel(InventoryPanel, "Inventory");
        }

        private void ToggleMenuGrid()
        {
            _isGridOpen = !_isGridOpen;
            
            if (MenuGridPanel != null)
            {
                MenuGridPanel.Visible = _isGridOpen;
            }
            _questTracker?.SetMenuSuppressed(_isGridOpen || _currentOpenPanel != null);
        }

        private void OpenPanel(Control panel, string panelName)
        {
            if (panel == null)
            {
                GD.PrintErr($"[GameMenuButton] Panel {panelName} is NULL!");
                return;
            }
            
            CloseCurrentPanel();
            panel.Show();
            _currentOpenPanel = panel;
            _questTracker?.SetMenuSuppressed(true);
            
            // Cập nhật thông tin khi mở CharacterPanel
            // CharacterPanel có script CharacterDetailUI nên cast trực tiếp
            if (panel == CharacterPanel && CharacterPanel is CharacterDetailUI characterUI)
            {
                characterUI.UpdateCharacterInfo();
            }

            if (panel == SkillsPanel && SkillsPanel is SkillTreePanel skillTreePanel)
            {
                skillTreePanel.RefreshFromCurrentParty();
            }

            if (panel == QuestsPanel && QuestsPanel is QuestJournalPanel questJournalPanel)
            {
                questJournalPanel.RefreshJournal();
            }

            if (panel == PartyPanel && PartyPanel is PartyManagementPanel partyPanel)
            {
                partyPanel.RefreshParty();
            }
            
            if (MenuGridPanel != null)
                MenuGridPanel.Hide();
            
            _isGridOpen = false;
        }

        private void CloseCurrentPanel()
        {
            if (_currentOpenPanel != null)
            {
                _currentOpenPanel.Hide();
                _currentOpenPanel = null;
            }
            _questTracker?.SetMenuSuppressed(_isGridOpen);
        }

        private void HideAllPanels()
        {
            CharacterPanel?.Hide();
            InventoryPanel?.Hide();
            SkillsPanel?.Hide();
            QuestsPanel?.Hide();
            SettingsPanel?.Hide();
            MapPanel?.Hide();
            PartyPanel?.Hide();
            AchievementsPanel?.Hide();
        }

        private void RegisterPanelVisibilityHandlers()
        {
            RegisterPanelVisibility(CharacterPanel);
            RegisterPanelVisibility(InventoryPanel);
            RegisterPanelVisibility(SkillsPanel);
            RegisterPanelVisibility(QuestsPanel);
            RegisterPanelVisibility(SettingsPanel);
            RegisterPanelVisibility(MapPanel);
            RegisterPanelVisibility(PartyPanel);
            RegisterPanelVisibility(AchievementsPanel);
        }

        private void RegisterPanelVisibility(Control panel)
        {
            if (panel == null)
            {
                return;
            }

            panel.VisibilityChanged += () =>
            {
                if (!panel.Visible && _currentOpenPanel == panel)
                {
                    _currentOpenPanel = null;
                    _questTracker?.SetMenuSuppressed(_isGridOpen);
                }
            };
        }

        /// <summary>
        /// SaveManager dùng các hàm này thay vì tự lần theo NodePath của panel nhiệm vụ.
        /// GameMenuButton là điểm nối UI đã tồn tại sẵn trong scene nên việc tích hợp ổn định hơn.
        /// </summary>
        public List<QuestProgressRecord> CaptureQuestProgress()
        {
            return QuestsPanel is QuestJournalPanel journal
                ? journal.CaptureProgress()
                : new List<QuestProgressRecord>();
        }

        public string CaptureTrackedQuestId()
        {
            return QuestsPanel is QuestJournalPanel journal
                ? journal.Manager?.TrackedQuestId ?? string.Empty
                : string.Empty;
        }

        public void RestoreQuestProgress(IReadOnlyList<QuestProgressRecord> records, string trackedQuestId)
        {
            if (QuestsPanel is QuestJournalPanel journal)
            {
                journal.RestoreProgress(records, trackedQuestId);
            }
        }

        public void ResetUiStateAfterLoad()
        {
            CloseCurrentPanel();
            HideAllPanels();

            _isGridOpen = false;

            if (MenuGridPanel != null)
            {
                MenuGridPanel.Hide();
            }
            _questTracker?.SetMenuSuppressed(false);

            ProcessMode = ProcessModeEnum.Inherit;
            SetProcess(true);
            SetProcessInput(true);
            SetProcessUnhandledInput(true);

            GetViewport()?.GuiReleaseFocus();
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
            {
                if (keyEvent.Keycode == ToggleKey)
                {
                    if (_currentOpenPanel != null)
                    {
                        CloseCurrentPanel();
                    }
                    else if (_isGridOpen)
                    {
                        ToggleMenuGrid();
                    }
                    
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }
}
