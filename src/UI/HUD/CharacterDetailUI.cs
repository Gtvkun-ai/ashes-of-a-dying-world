using Godot;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Core.Managers;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class CharacterDetailUI : Control
    {
        [Export] public Texture2D BackgroundTexture; // Ảnh nền custom
        
        // UI Elements
        private VBoxContainer _characterListContainer; // Container cho danh sách avatar nhân vật
        private TextureRect _avatarDisplay; // Avatar lớn bên phải
        private TextureRect _backgroundDisplay;
        
        // Tab system
        private Button _btnOverview;
        private Button _btnEquipment;
        private Button _btnSkills;
        private Button _btnTalents;
        
        // Content panels
        private Control _overviewPanel;
        private Control _equipmentPanel;
        private Control _skillsPanel;
        private Control _talentsPanel;
        
        // Overview panel elements
        private Label _nameLabel;
        private Label _levelLabel;
        private Label _raceLabel;
        private VBoxContainer _statsTextContainer;
        
        // Talents panel elements
        private StatHexagonChart _statsChart;
        
        // Cấu hình màu sắc chủ đạo (Gold/Elite Style)
        private readonly Color _accentColor = new Color("#38bdf8"); // Xanh băng sáng
        private readonly Color _subTextColor = new Color("#94a3b8"); // Xám xanh (Slate 400)
        
        // Màu trạng thái Tab
        private readonly Color _tabActiveColor = new Color("#38bdf8"); // Sáng
        private readonly Color _tabInactiveColor = new Color("#64748b"); // Tối hơn
        private string _currentTab = "overview"; // Mặc định là overview

        private Color _themeBgColor = new Color("#0f172a", 0.90f); // Nền xanh đen thẫm
        private Color _themeBorderColor = new Color("#38bdf8");    // Viền xanh
        private Color _btnNormalColor = new Color("#1e293b");      // Nền nút thường
        private Color _btnHoverColor = new Color("#334155");       // Nền nút hover
        public override void _Ready()
        {
            // 1. Setup Root
            SetAnchorsPreset(LayoutPreset.FullRect);
            
            // 2. BACKGROUND LAYER (Nền tối hoặc ảnh custom)
            SetupBackground();

            // 3. Main Layout: HBox với 3 cột
            var mainHBox = new HBoxContainer();
            mainHBox.SetAnchorsPreset(LayoutPreset.FullRect);
            mainHBox.AddThemeConstantOverride("separation", 0);
            AddChild(mainHBox);

            // CỘT 1: Danh sách nhân vật bên trái (15% width)
            SetupCharacterListColumn(mainHBox);

            // CỘT 2: Nội dung chính với tab system (55% width)
            SetupMainContentColumn(mainHBox);

            // CỘT 3: Avatar lớn bên phải (30% width)
            SetupAvatarColumn(mainHBox);

            VisibilityChanged += OnVisibilityChanged;
        }

        private void SetupBackground()
        {
            if (BackgroundTexture != null)
            {
                _backgroundDisplay = new TextureRect();
                _backgroundDisplay.Texture = BackgroundTexture;
                _backgroundDisplay.SetAnchorsPreset(LayoutPreset.FullRect);
                _backgroundDisplay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                _backgroundDisplay.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
                AddChild(_backgroundDisplay);
            }
        }

        // --- HÀM TỰ ĐỘNG PHỐI MÀU (ĐÃ SỬA CHO CHARACTER UI) ---
        private void ApplyIceTheme()
        {
            // 1. Tạo Style cho các Nút Tab (Tổng quan, Kỹ năng...)
            var btnNormal = new StyleBoxFlat
            {
                BgColor = _btnNormalColor,
                CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
                CornerRadiusBottomRight = 0, CornerRadiusBottomLeft = 0, // Tab thường bo góc trên
                ContentMarginLeft = 15, ContentMarginRight = 15,
                ContentMarginTop = 8, ContentMarginBottom = 8
            };

            var btnHover = (StyleBoxFlat)btnNormal.Duplicate();
            btnHover.BgColor = _btnHoverColor;
            btnHover.BorderColor = _themeBorderColor; // Hover thì hiện viền màu băng
            btnHover.BorderWidthBottom = 2; // Viền dưới sáng lên

            var btnPressed = (StyleBoxFlat)btnNormal.Duplicate();
            btnPressed.BgColor = new Color("#0f172a"); // Khi chọn thì trùng màu nền panel
            btnPressed.BorderColor = _themeBorderColor;
            btnPressed.BorderWidthTop = 2; // Highlight cạnh trên

            // Danh sách các nút Tab cần tô màu
            Button[] tabButtons = { _btnOverview, _btnEquipment, _btnSkills, _btnTalents };

            foreach (var btn in tabButtons)
            {
                if (btn != null)
                {
                    btn.AddThemeStyleboxOverride("normal", btnNormal);
                    btn.AddThemeStyleboxOverride("hover", btnHover);
                    btn.AddThemeStyleboxOverride("pressed", btnPressed);
                    // Chỉnh màu chữ
                    btn.AddThemeColorOverride("font_color", _tabInactiveColor);
                    btn.AddThemeColorOverride("font_hover_color", Colors.White);
                    btn.AddThemeColorOverride("font_pressed_color", _accentColor);
                    btn.AddThemeColorOverride("font_focus_color", _accentColor);
                }
            }
        }

        // CỘT 1: Danh sách nhân vật có thể scroll (Dọc - VBox)
        private void SetupCharacterListColumn(HBoxContainer parent)
        {
            var listPanel = new PanelContainer();
            listPanel.CustomMinimumSize = new Vector2(120, 0); // Chiều rộng cố định cho cột dọc
            parent.AddChild(listPanel);

            var listVBox = new VBoxContainer();
            listPanel.AddChild(listVBox);

            // Title cho danh sách
            var titleLabel = new Label();
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleLabel.AddThemeFontSizeOverride("font_size", 14);
            titleLabel.AddThemeColorOverride("font_color", _accentColor);
            listVBox.AddChild(titleLabel);

            // Không dùng scroll container nữa, hiển thị trực tiếp theo chiều dọc
            _characterListContainer = new VBoxContainer();
            _characterListContainer.AddThemeConstantOverride("separation", 10);
            _characterListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _characterListContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _characterListContainer.Alignment = BoxContainer.AlignmentMode.Begin; // Bắt đầu từ trên xuống
            listVBox.AddChild(_characterListContainer);
        }

        // CỘT 2: Nội dung chính với tab
        private void SetupMainContentColumn(HBoxContainer parent)
        {
            var contentVBox = new VBoxContainer();
            contentVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            contentVBox.AddThemeConstantOverride("separation", 0);
            parent.AddChild(contentVBox);

            // Header với tên nhân vật
            SetupContentHeader(contentVBox);

            // Tab bar
            SetupTabBar(contentVBox);

            // Content area cho các tab
            SetupContentArea(contentVBox);
        }

        private void SetupContentHeader(VBoxContainer parent)
        {
            var headerMargin = new MarginContainer();
            headerMargin.AddThemeConstantOverride("margin_left", 20);
            headerMargin.AddThemeConstantOverride("margin_top", 20);
            parent.AddChild(headerMargin);

            var headerVBox = new VBoxContainer();
            headerVBox.AddThemeConstantOverride("separation", 5);
            headerMargin.AddChild(headerVBox);

            _nameLabel = new Label();
            _nameLabel.AddThemeFontSizeOverride("font_size", 36);
            _nameLabel.AddThemeColorOverride("font_color", Colors.White);
            _nameLabel.Uppercase = true;
            headerVBox.AddChild(_nameLabel);

            var subInfoHBox = new HBoxContainer();
            subInfoHBox.AddThemeConstantOverride("separation", 15);
            headerVBox.AddChild(subInfoHBox);

            _levelLabel = CreateStyledLabel(18, _accentColor);
            subInfoHBox.AddChild(_levelLabel);

            var separator = new Label();
            separator.Text = "|";
            separator.AddThemeColorOverride("font_color", _subTextColor);
            subInfoHBox.AddChild(separator);

            _raceLabel = CreateStyledLabel(18, _subTextColor);
            subInfoHBox.AddChild(_raceLabel);
        }

        private void SetupTabBar(VBoxContainer parent)
        {
            var tabMargin = new MarginContainer();
            tabMargin.AddThemeConstantOverride("margin_left", 20);
            tabMargin.AddThemeConstantOverride("margin_top", 10);
            parent.AddChild(tabMargin);

            var tabHBox = new HBoxContainer();
            tabHBox.AddThemeConstantOverride("separation", 5);
            tabMargin.AddChild(tabHBox);

            _btnOverview = CreateTabButton("TỔNG QUAN");
            _btnOverview.Pressed += () => SwitchTab("overview");
            tabHBox.AddChild(_btnOverview);

            _btnEquipment = CreateTabButton("TRANG BỊ");
            _btnEquipment.Pressed += () => SwitchTab("equipment");
            tabHBox.AddChild(_btnEquipment);

            _btnSkills = CreateTabButton("KỸ NĂNG");
            _btnSkills.Pressed += () => SwitchTab("skills");
            tabHBox.AddChild(_btnSkills);

            _btnTalents = CreateTabButton("THIÊN PHÚ");
            _btnTalents.Pressed += () => SwitchTab("talents");
            tabHBox.AddChild(_btnTalents);
        }

        private void SetupContentArea(VBoxContainer parent)
        {
            var contentMargin = new MarginContainer();
            contentMargin.SizeFlagsVertical = SizeFlags.ExpandFill;
            contentMargin.AddThemeConstantOverride("margin_left", 20);
            contentMargin.AddThemeConstantOverride("margin_right", 20);
            contentMargin.AddThemeConstantOverride("margin_top", 10);
            contentMargin.AddThemeConstantOverride("margin_bottom", 20);
            parent.AddChild(contentMargin);

            var contentContainer = new Control();
            contentContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            contentMargin.AddChild(contentContainer);

            // TAB 1: Tổng quan - Text chỉ số
            _overviewPanel = CreateOverviewPanel();
            contentContainer.AddChild(_overviewPanel);

            // TAB 2: Trang bị
            _equipmentPanel = CreateEquipmentPanel();
            contentContainer.AddChild(_equipmentPanel);

            // TAB 3: Kỹ năng
            _skillsPanel = CreateSkillsPanel();
            contentContainer.AddChild(_skillsPanel);

            // TAB 4: Thiên phú - Hexagon chart
            _talentsPanel = CreateTalentsPanel();
            contentContainer.AddChild(_talentsPanel);
        }

private Control CreateOverviewPanel()
        {
            var panel = new PanelContainer();
            panel.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.Visible = true;
            
            // Ice Theme: Nền tối gần solid, viền xanh băng (alpha 0.95 để che phủ hoàn toàn)
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.05f, 0.08f, 0.15f, 1f);
            panelStyle.BorderColor = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.5f);
            panelStyle.SetBorderWidthAll(2);
            panelStyle.CornerRadiusBottomLeft = 10;
            panelStyle.CornerRadiusBottomRight = 10;
            
            panel.AddThemeStyleboxOverride("panel", panelStyle);

            var scrollContainer = new ScrollContainer();
            // Thêm margin để nội dung không sát lề
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 15);
            margin.AddThemeConstantOverride("margin_right", 15);
            margin.AddThemeConstantOverride("margin_top", 15);
            margin.AddThemeConstantOverride("margin_bottom", 15);
            
            panel.AddChild(scrollContainer);
            scrollContainer.AddChild(margin); // Scroll chứa Margin

            _statsTextContainer = new VBoxContainer();
            _statsTextContainer.AddThemeConstantOverride("separation", 5); // Giảm khoảng cách chút
            _statsTextContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill; // Giãn ngang
            margin.AddChild(_statsTextContainer); // Margin chứa VBox

            return panel;
        }
        private Control CreateEquipmentPanel()
        {
            var panel = new PanelContainer();
            panel.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.Visible = false;
            
            // Ice Theme: Nền tối gần solid, viền xanh băng
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.05f, 0.08f, 0.15f, 1f);
            panelStyle.BorderColor = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.5f);
            panelStyle.SetBorderWidthAll(2);
            panelStyle.CornerRadiusBottomLeft = 10;
            panelStyle.CornerRadiusBottomRight = 10;
            panel.AddThemeStyleboxOverride("panel", panelStyle);

            var label = new Label();
            label.Text = "TRANG BỊ (Đang phát triển)";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            panel.AddChild(label);

            return panel;
        }

        private Control CreateSkillsPanel()
        {
            var panel = new PanelContainer();
            panel.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.Visible = false;
            
            // Ice Theme: Nền tối gần solid, viền xanh băng
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.05f, 0.08f, 0.15f, 1f);
            panelStyle.BorderColor = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.5f);
            panelStyle.SetBorderWidthAll(2);
            panelStyle.CornerRadiusBottomLeft = 10;
            panelStyle.CornerRadiusBottomRight = 10;
            panel.AddThemeStyleboxOverride("panel", panelStyle);
            
            var label = new Label();
            label.Text = "KỸ NĂNG (Đang phát triển)";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            panel.AddChild(label);

            return panel;
        }

        private Control CreateTalentsPanel()
        {
            var panel = new PanelContainer();
            panel.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.Visible = false;
            
            // Ice Theme: Nền tối gần solid, viền xanh băng
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.05f, 0.08f, 0.15f, 1f);
            panelStyle.BorderColor = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.5f);
            panelStyle.SetBorderWidthAll(2);
            panelStyle.CornerRadiusBottomLeft = 10;
            panelStyle.CornerRadiusBottomRight = 10;
            panel.AddThemeStyleboxOverride("panel", panelStyle);

            var centerContainer = new CenterContainer();
            centerContainer.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.AddChild(centerContainer);

            _statsChart = new StatHexagonChart();
            _statsChart.MainColor = _accentColor;
            _statsChart.ChartRadiusOffset = 100f;
            centerContainer.AddChild(_statsChart);

            return panel;
        }

        // CỘT 3: Avatar lớn bên phải
        private void SetupAvatarColumn(HBoxContainer parent)
        {
            var avatarPanel = new PanelContainer();
            avatarPanel.CustomMinimumSize = new Vector2(350, 0);
            // Cho phép panel này giãn hết chiều cao của cha
            avatarPanel.SizeFlagsVertical = SizeFlags.ExpandFill; 
            parent.AddChild(avatarPanel);

            var avatarMargin = new MarginContainer();
            // Giảm margin xuống 0 nếu muốn sát viền, hoặc giữ nguyên tùy ý
            avatarMargin.AddThemeConstantOverride("margin_left", 0); 
            avatarMargin.AddThemeConstantOverride("margin_right", 0);
            avatarMargin.AddThemeConstantOverride("margin_top", 0);
            avatarMargin.AddThemeConstantOverride("margin_bottom", 0);
            avatarPanel.AddChild(avatarMargin);

            
            _avatarDisplay = new TextureRect();
            _avatarDisplay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize; // Cho phép resize tự do
            
            _avatarDisplay.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;            
            // Quan trọng: Lệnh này bảo Godot kéo giãn control này lấp đầy chiều dọc và ngang của cha
            _avatarDisplay.SizeFlagsVertical = SizeFlags.ExpandFill;
            _avatarDisplay.SizeFlagsHorizontal = SizeFlags.ExpandFill;            
            // Add trực tiếp vào Margin (hoặc Panel)
            _avatarDisplay.SetAnchorsPreset(LayoutPreset.FullRect);

            avatarMargin.AddChild(_avatarDisplay);
        }

        // Helper tạo Label nhanh
        private Label CreateStyledLabel(int size, Color color)
        {
            var lbl = new Label();
            lbl.AddThemeFontSizeOverride("font_size", size);
            lbl.AddThemeColorOverride("font_color", color);
            return lbl;
        }

        // Helper tạo Tab Button
        private Button CreateTabButton(string text)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(120, 35);
            btn.AddThemeFontSizeOverride("font_size", 14);
            return btn;
        }

        // Chuyển tab
        private void SwitchTab(string tabName)
        {
            // 1. Ẩn TOÀN BỘ các panel - Disable processing và mouse filter
            HidePanel(_overviewPanel);
            HidePanel(_equipmentPanel);
            HidePanel(_skillsPanel);
            HidePanel(_talentsPanel);

            // Reset màu button
            ResetTabButtonColors();

            // 2. Chỉ bật panel cần thiết
            switch (tabName)
            {
                case "overview":
                    ShowPanel(_overviewPanel);
                    _btnOverview.AddThemeColorOverride("font_color", _tabActiveColor);
                    break;
                case "equipment":
                    ShowPanel(_equipmentPanel);
                    _btnEquipment.AddThemeColorOverride("font_color", _tabActiveColor);
                    break;
                case "skills":
                    ShowPanel(_skillsPanel);
                    _btnSkills.AddThemeColorOverride("font_color", _tabActiveColor);
                    break;
                case "talents":
                    ShowPanel(_talentsPanel);
                    _btnTalents.AddThemeColorOverride("font_color", _tabActiveColor);
                    break;
            }
            
            // Lưu lại tab hiện tại để dùng cho hàm Update (Xem Bước 2)
            _currentTab = tabName; 
        }

        // Helper methods để ẩn/hiện panels một cách triệt để
        private void HidePanel(Control panel)
        {
            if (panel != null)
            {
                panel.Visible = false;
                panel.ProcessMode = ProcessModeEnum.Disabled;
                panel.MouseFilter = MouseFilterEnum.Ignore;
            }
        }

        private void ShowPanel(Control panel)
        {
            if (panel != null)
            {
                panel.Visible = true;
                panel.ProcessMode = ProcessModeEnum.Inherit;
                panel.MouseFilter = MouseFilterEnum.Pass;
            }
        }
        private void ResetTabButtonColors()
        {
            _btnOverview.AddThemeColorOverride("font_color", _tabInactiveColor);
            _btnEquipment.AddThemeColorOverride("font_color", _tabInactiveColor);
            _btnSkills.AddThemeColorOverride("font_color", _tabInactiveColor);
            _btnTalents.AddThemeColorOverride("font_color", _tabInactiveColor);
        }

        private void LoadCharacterList()
        {
            // Xóa các avatar cũ
            var children = _characterListContainer.GetChildren();
            foreach (var child in children)
            {
                child.QueueFree();
            }

            // Thêm avatar của tất cả nhân vật trong party
            for (int i = 0; i < PlayerManager.Instance.PartyMembers.Count; i++)
            {
                int index = i; // Capture for closure
                var character = PlayerManager.Instance.PartyMembers[i];
                
                if (character?.ConfigData?.Icon != null)
                {
                    var btn = new TextureButton();
                    btn.TextureNormal = character.ConfigData.Icon;
                    btn.CustomMinimumSize = new Vector2(70, 70); // Size nhỏ hơn
                    btn.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
                    btn.IgnoreTextureSize = true;
                    btn.Pressed += () => OnCharacterSelected(index);
                    
                    // Highlight nếu là nhân vật đang chọn
                    if (index == PlayerManager.Instance.ActiveCharacterIndex)
                    {
                        var style = new StyleBoxFlat();
                        style.BgColor = new Color(1, 1, 0, 0.3f);
                        style.SetBorderWidthAll(3);
                        style.BorderColor = _accentColor;
                        style.SetCornerRadiusAll(5);
                        btn.AddThemeStyleboxOverride("normal", style);
                    }
                    
                    _characterListContainer.AddChild(btn);
                }
            }
        }

        private void OnCharacterSelected(int index)
        {
            PlayerManager.Instance.SetActiveCharacter(index);
            UpdateCharacterInfo();
        }

        private void OnVisibilityChanged()
        {
            if (Visible) UpdateCharacterInfo();
        }

        public void UpdateCharacterInfo()
        {
            var activeIndex = PlayerManager.Instance.ActiveCharacterIndex;
            if (activeIndex >= PlayerManager.Instance.PartyMembers.Count) return;

            PlayerStats currentStats = PlayerManager.Instance.PartyMembers[activeIndex];
            if (currentStats == null || currentStats.ConfigData == null) return;

            var config = currentStats.ConfigData;

            // 1. Update header
            _nameLabel.Text = config.Name;
            _levelLabel.Text = $"LV. {currentStats.CurrentLevel:00}";
            _raceLabel.Text = config.CharacterRace?.RaceName?.ToUpper() ?? "UNKNOWN";

            // 2. Update Avatar
            if (config.Avatar != null)
            {
                _avatarDisplay.Texture = config.Avatar;
            }

            // 3. Update Overview panel - Text chỉ số
            UpdateOverviewPanel(currentStats);

            // 4. Update Talents panel - Hexagon chart
            UpdateTalentsPanel(currentStats);

            // 5. Load character list
            LoadCharacterList();

            // 6. Show default tab
            SwitchTab(_currentTab);
        }

        private void UpdateOverviewPanel(PlayerStats stats)
        {
            // Xóa stats cũ
            foreach (var child in _statsTextContainer.GetChildren())
            {
                child.QueueFree();
            }

            // Title
            var titleLabel = new Label();
            titleLabel.Text = "CHỈ SỐ NHÂN VẬT";
            titleLabel.AddThemeFontSizeOverride("font_size", 20);
            titleLabel.AddThemeColorOverride("font_color", _accentColor);
            _statsTextContainer.AddChild(titleLabel);

            var separator = new HSeparator();
            _statsTextContainer.AddChild(separator);

            // Hiển thị các chỉ số dạng text
            if (stats.FinalAttributes != null)
            {
                foreach (var attr in stats.FinalAttributes)
                {
                    var hbox = new HBoxContainer();
                    hbox.AddThemeConstantOverride("separation", 10);
                    
                    var nameLabel = new Label();
                    nameLabel.Text = FormatStatName(attr.Key.ToString());
                    nameLabel.CustomMinimumSize = new Vector2(100, 0);
                    nameLabel.AddThemeFontSizeOverride("font_size", 16);
                    nameLabel.AddThemeColorOverride("font_color", _subTextColor);
                    hbox.AddChild(nameLabel);

                    var valueLabel = new Label();
                    valueLabel.Text = attr.Value.ToString();
                    valueLabel.AddThemeFontSizeOverride("font_size", 16);
                    valueLabel.AddThemeColorOverride("font_color", Colors.White);
                    hbox.AddChild(valueLabel);

                    _statsTextContainer.AddChild(hbox);
                }
            }
        }

        private void UpdateTalentsPanel(PlayerStats stats)
        {
            _statsChart.ClearStats();
            if (stats.FinalAttributes != null)
            {
                foreach (var attr in stats.FinalAttributes)
                {
                    _statsChart.SetStat(FormatStatName(attr.Key.ToString()), attr.Value);
                }
            }
            _statsChart.UpdateAllStats();
        }

        // Hàm làm gọn tên chỉ số
        private string FormatStatName(string original)
        {
            return original switch
            {
                "Strength" => "STR",
                "Agility" => "AGI",
                "Intelligence" => "INT",
                "Dexterity" => "DEX",
                "Vitality" => "VIT",
                "Luck" => "LUK",
                _ => original.Substring(0, Mathf.Min(3, original.Length)).ToUpper()
            };
        }
    }
}
