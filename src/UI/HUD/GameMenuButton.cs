using Godot;
using AshesofaDyingWorld.UI.HUD;

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
        private CharacterDetailUI _characterDetailUI; // Để hiển thị thông tin nhân vật

        public override void _Ready()
        {
            GD.Print("[GameMenuButton] Ready called");
            
            if (MenuButton != null)
            {
                MenuButton.Pressed += ToggleMenuGrid;
                GD.Print("[GameMenuButton] MenuButton connected");
            }
            
            if (MenuGridPanel != null)
            {
                MenuGridPanel.Hide();
                GD.Print("[GameMenuButton] MenuGridPanel hidden");
            }
            
            // Tạo CharacterDetailUI và thêm vào CharacterPanel
            if (CharacterPanel != null)
            {
                _characterDetailUI = new CharacterDetailUI();
                _characterDetailUI.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                CharacterPanel.AddChild(_characterDetailUI);
                GD.Print("[GameMenuButton] CharacterDetailUI added to CharacterPanel");
            }
            
            HideAllPanels();
            ConnectFeatureButtons();
        }

        private void ConnectFeatureButtons()
        {
            if (CharacterButton != null)
                CharacterButton.Pressed += () => OpenPanel(CharacterPanel, "Character");
            
            if (InventoryButton != null)
                InventoryButton.Pressed += () => OpenPanel(InventoryPanel, "Inventory");
            
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
            
            GD.Print("[GameMenuButton] All buttons connected");
        }

        private void ToggleMenuGrid()
        {
            _isGridOpen = !_isGridOpen;
            
            if (MenuGridPanel != null)
            {
                MenuGridPanel.Visible = _isGridOpen;
                GD.Print($"[GameMenuButton] Grid {(_isGridOpen ? "opened" : "closed")}");
            }
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
            
            // Cập nhật thông tin khi mở CharacterPanel
            if (panel == CharacterPanel && _characterDetailUI != null)
            {
                _characterDetailUI.UpdateCharacterInfo();
            }
            
            if (MenuGridPanel != null)
                MenuGridPanel.Hide();
            
            _isGridOpen = false;
            GD.Print($"[GameMenuButton] Opened {panelName}");
        }

        private void CloseCurrentPanel()
        {
            if (_currentOpenPanel != null)
            {
                _currentOpenPanel.Hide();
                _currentOpenPanel = null;
            }
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