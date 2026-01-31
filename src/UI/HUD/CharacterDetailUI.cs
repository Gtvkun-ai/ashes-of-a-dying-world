using Godot;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Core.Managers;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class CharacterDetailUI : Control
    {
        [Export] public Texture2D BackgroundTexture; 
        
        // UI Elements
        private VBoxContainer _characterListContainer;
        private VideoStreamPlayer _avatarDisplay;
        private TextureRect _backgroundDisplay;
        
        // Tab system
        private Button _btnOverview;
        private Button _btnEquipment;
        private Button _btnSkills;
        
        // Content panels
        private Control _overviewPanel;
        private Control _equipmentPanel;
        private Control _skillsPanel;
        
        // Overview panel elements
        private Label _nameLabel;
        private Label _levelLabel;
        private Label _raceLabel;
        private VBoxContainer _statsTextContainer;

        // bar gốc (Hp, Mana, Stamina)
        private VBoxContainer _resourceBarsContainer;
        private ProgressBar _hpBar;
        private ProgressBar _mpBar;
        private ProgressBar _staminaBar;
        private Label _hpValueLabel;
        private Label _mpValueLabel;
        private Label _staminaValueLabel;
        
        // Chart
        private StatHexagonChart _overviewStatsChart; 
        
        // Cấu hình màu sắc
        private readonly Color _accentColor = new Color("#38bdf8"); 
        private readonly Color _subTextColor = new Color("#94a3b8"); 
        private readonly Color _tabActiveColor = new Color("#38bdf8");
        private readonly Color _tabInactiveColor = new Color("#64748b");
        private string _currentTab = "overview";
        private Color _themeBorderColor = new Color("#38bdf8");   
        private Color _btnNormalColor = new Color("#1e293b");     
        private Color _btnHoverColor = new Color("#334155");       

        private TextureRect _avatarDisplayRect;     // Cái này để hiện lên UI (có thể resize)
        private SubViewport _videoViewport;         // Cái này để chứa video gốc
        private VideoStreamPlayer _hiddenPlayer;    // Cái này là trình phát video thật (nằm ẩn)
        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            SetupBackground();

            var mainHBox = new HBoxContainer();
            mainHBox.SetAnchorsPreset(LayoutPreset.FullRect);
            mainHBox.AddThemeConstantOverride("separation", 0);
            AddChild(mainHBox);

            SetupCharacterListColumn(mainHBox);
            SetupMainContentColumn(mainHBox);
            SetupAvatarColumn(mainHBox);

            VisibilityChanged += OnVisibilityChanged;
            
            ApplyIceTheme();
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

        private void ApplyIceTheme()
        {
            var btnNormal = new StyleBoxFlat
            {
                BgColor = _btnNormalColor,
                CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
                ContentMarginLeft = 15, ContentMarginRight = 15,
                ContentMarginTop = 8, ContentMarginBottom = 8
            };

            var btnHover = (StyleBoxFlat)btnNormal.Duplicate();
            btnHover.BgColor = _btnHoverColor;
            btnHover.BorderColor = _themeBorderColor;
            btnHover.BorderWidthBottom = 2;

            var btnPressed = (StyleBoxFlat)btnNormal.Duplicate();
            btnPressed.BgColor = new Color("#0f172a");
            btnPressed.BorderColor = _themeBorderColor;
            btnPressed.BorderWidthTop = 2;

            // ĐÃ XÓA: _btnTalents khỏi danh sách này
            Button[] tabButtons = { _btnOverview, _btnEquipment, _btnSkills };

            foreach (var btn in tabButtons)
            {
                if (btn != null)
                {
                    btn.AddThemeStyleboxOverride("normal", btnNormal);
                    btn.AddThemeStyleboxOverride("hover", btnHover);
                    btn.AddThemeStyleboxOverride("pressed", btnPressed);
                    btn.AddThemeColorOverride("font_color", _tabInactiveColor);
                    btn.AddThemeColorOverride("font_hover_color", Colors.White);
                    btn.AddThemeColorOverride("font_pressed_color", _accentColor);
                    btn.AddThemeColorOverride("font_focus_color", _accentColor);
                }
            }
        }

        private void SetupCharacterListColumn(HBoxContainer parent)
        {
            var listPanel = new PanelContainer();
            listPanel.CustomMinimumSize = new Vector2(120, 0); 
            parent.AddChild(listPanel);

            var listVBox = new VBoxContainer();
            listPanel.AddChild(listVBox);

            var titleLabel = new Label();
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            listVBox.AddChild(titleLabel);

            _characterListContainer = new VBoxContainer();
            _characterListContainer.AddThemeConstantOverride("separation", 10);
            _characterListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _characterListContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            listVBox.AddChild(_characterListContainer);
        }

        private void SetupMainContentColumn(HBoxContainer parent)
        {
            var contentVBox = new VBoxContainer();
            contentVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(contentVBox);

            SetupContentHeader(contentVBox);
            SetupTabBar(contentVBox);
            SetupContentArea(contentVBox);
        }

        private void SetupContentHeader(VBoxContainer parent)
        {
            var headerMargin = new MarginContainer();
            headerMargin.AddThemeConstantOverride("margin_left", 20);
            headerMargin.AddThemeConstantOverride("margin_top", 20);
            parent.AddChild(headerMargin);

            var headerVBox = new VBoxContainer();
            headerMargin.AddChild(headerVBox);

            _nameLabel = new Label();
            _nameLabel.AddThemeFontSizeOverride("font_size", 36);
            _nameLabel.Uppercase = true;
            headerVBox.AddChild(_nameLabel);

            var subInfoHBox = new HBoxContainer();
            subInfoHBox.AddThemeConstantOverride("separation", 15);
            headerVBox.AddChild(subInfoHBox);

            _levelLabel = CreateStyledLabel(18, _accentColor);
            subInfoHBox.AddChild(_levelLabel);

            var separator = new Label();
            separator.Text = "|";
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

            // ĐÃ XÓA: Button Thiên Phú
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

            _overviewPanel = CreateOverviewPanel();
            contentContainer.AddChild(_overviewPanel);

            _equipmentPanel = CreateEquipmentPanel();
            contentContainer.AddChild(_equipmentPanel);

            _skillsPanel = CreateSkillsPanel();
            contentContainer.AddChild(_skillsPanel);

        }

        private Control CreateOverviewPanel()
        {
            var panel = new PanelContainer();
            //FullRect dùng để hiển thị panel đúng kích thước cha
            panel.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());

            var mainVBox = new VBoxContainer();
            mainVBox.AddThemeConstantOverride("separation", 15);
            panel.AddChild(mainVBox);

            
            // 1. Tạo Tiêu đề và Dòng kẻ ngang (Nằm trực tiếp trong mainVBox)
            var titleLabel = new Label();
            titleLabel.Text = "CHỈ SỐ NHÂN VẬT";
            titleLabel.AddThemeFontSizeOverride("font_size", 20);
            titleLabel.AddThemeColorOverride("font_color", _accentColor);
            mainVBox.AddChild(titleLabel);
            
            mainVBox.AddChild(new HSeparator());
            // -----------------------------------------------------------

            // 2. Tạo khu vực chứa 2 cột (Stats bên trái, Bar bên phải)
            var statsAreaHBox = new HBoxContainer();
            statsAreaHBox.AddThemeConstantOverride("separation", 20);
            mainVBox.AddChild(statsAreaHBox);

            // Cột Trái: Chỉ chứa các dòng STR, DEX... 
            _statsTextContainer = new VBoxContainer();
            _statsTextContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            statsAreaHBox.AddChild(_statsTextContainer);

            // Cột Phải: Chứa HP, MP, Stamina
            _resourceBarsContainer = new VBoxContainer();
            _resourceBarsContainer.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            _resourceBarsContainer.AddThemeConstantOverride("separation", 8);
            statsAreaHBox.AddChild(_resourceBarsContainer);

            // Tạo sẵn các thanh Bar
            _resourceBarsContainer.AddChild(CreateResourceBarRow("HP", new Color("#ef4444"), out _hpBar, out _hpValueLabel));
            _resourceBarsContainer.AddChild(CreateResourceBarRow("MP", new Color("#3b82f6"), out _mpBar, out _mpValueLabel));
            _resourceBarsContainer.AddChild(CreateResourceBarRow("STA", new Color("#22c55e"), out _staminaBar, out _staminaValueLabel));

            // --- Phần biểu đồ Hexagon  
            var hexagonContainer = new HBoxContainer();
            mainVBox.AddChild(hexagonContainer);

            _overviewStatsChart = new StatHexagonChart();
            _overviewStatsChart.MainColor = _accentColor;
            _overviewStatsChart.ChartRadiusOffset = 35f;
            _overviewStatsChart.FontSize = 10;
            _overviewStatsChart.CustomMinimumSize = new Vector2(220, 220);
            _overviewStatsChart.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

            hexagonContainer.AddChild(_overviewStatsChart);

            return panel;
        }

        private Control CreateEquipmentPanel()
        {
            var panel = new PanelContainer();
            // QUAN TRỌNG: Phải set FullRect để panel bung đầy màn hình, không bị đè lệch
            panel.SetAnchorsPreset(LayoutPreset.FullRect); 
            panel.Visible = false;
            
            // Thêm style nền để che đi các panel phía sau (nếu có)
            panel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());

            var label = new Label();
            label.Text = "TRANG BỊ (Đang cập nhật)";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            panel.AddChild(label);

            return panel;
        }

        private Control CreateSkillsPanel()
        {
            var panel = new PanelContainer();
            // QUAN TRỌNG: Set FullRect
            panel.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.Visible = false;

            // Thêm style nền
            panel.AddThemeStyleboxOverride("panel", GetCommonPanelStyle());

            var label = new Label();
            label.Text = "KỸ NĂNG (Đang cập nhật)";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            panel.AddChild(label);

            return panel;
        }

        // Tạo style chung cho các panel để đồng bộ và tránh lỗi trong suốt
        private StyleBoxFlat GetCommonPanelStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.05f, 0.08f, 0.15f, 1f); // Màu nền tối, không trong suốt
            style.BorderColor = new Color(_themeBorderColor.R, _themeBorderColor.G, _themeBorderColor.B, 0.5f);
            style.SetBorderWidthAll(2);
            style.CornerRadiusBottomLeft = 10;
            style.CornerRadiusBottomRight = 10;
            return style;
        }

        private void SetupAvatarColumn(HBoxContainer parent)
        {
            var avatarPanel = new PanelContainer();
            avatarPanel.CustomMinimumSize = new Vector2(300, 0);
            avatarPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            avatarPanel.ZIndex = 100;  // Đảm bảo panel avatar luôn ở trên cùng
            parent.AddChild(avatarPanel);

            var avatarMargin = new MarginContainer();
            avatarMargin.SetAnchorsPreset(LayoutPreset.FullRect);
            // Margin tùy chỉnh
            avatarMargin.AddThemeConstantOverride("margin_left", 10);
            avatarMargin.AddThemeConstantOverride("margin_right", 10);
            avatarMargin.AddThemeConstantOverride("margin_top", 10);
            avatarMargin.AddThemeConstantOverride("margin_bottom", 10);
            avatarPanel.AddChild(avatarMargin);

            // 1. TẠO VIEWPORT & PLAYER ẨN (Nơi render video gốc)
            // Lưu ý: Viewport cần kích thước cố định bằng đúng độ phân giải video của bạn
            _videoViewport = new SubViewport();
            _videoViewport.Size = new Vector2I(720, 1082);
            _videoViewport.TransparentBg = true; // Để nền trong suốt cho Shader hoạt động tốt
            _videoViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible; // Tối ưu hiệu năng
            AddChild(_videoViewport); // Add vào cây nhưng nó sẽ không hiện ra màn hình

            _hiddenPlayer = new VideoStreamPlayer();
            _hiddenPlayer.Loop = true;  // Set loop trước khi play
            _hiddenPlayer.Autoplay = false;  // Tắt autoplay, sẽ play thủ công
            _hiddenPlayer.VolumeDb = -80;
            _hiddenPlayer.BufferingMsec = 0;
            _hiddenPlayer.Finished += OnVideoFinished;  // Thêm event handler để đảm bảo loop
            _videoViewport.AddChild(_hiddenPlayer); // Nhét Player vào trong Viewport

            // TẠO TEXTURE RECT (Nơi hiển thị trên UI)
            _avatarDisplayRect = new TextureRect();
            _avatarDisplayRect.SetAnchorsPreset(LayoutPreset.FullRect);
            _avatarDisplayRect.ZIndex = 10;  // Z-index cao để luôn hiển thị trên
            
            // ĐÂY LÀ CHÌA KHÓA: TextureRect hỗ trợ Expand!
            _avatarDisplayRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize; 
            _avatarDisplayRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered; // Hoặc KeepAspectCentered
            
            // Lấy texture từ Viewport gán vào Rect
            _avatarDisplayRect.Texture = _videoViewport.GetTexture();

            // --- SETUP SHADER ---
            // Bây giờ bạn gắn Shader vào TextureRect chứ không phải VideoStreamPlayer
            var chromaShader = GD.Load<Shader>("res://assets/shader/chroma_key.gdshader");
            if (chromaShader != null)
            {
                var shaderMaterial = new ShaderMaterial();
                shaderMaterial.Shader = chromaShader;
                // Cấu hình tham số cho Shader 
                shaderMaterial.SetShaderParameter("chroma_key", new Vector3(0f, 1f, 0f));
                // Tham số điều chỉnh hiệu ứng
                shaderMaterial.SetShaderParameter("similarity", 0.4f);
                // Tham số điều chỉnh hiệu ứng
                shaderMaterial.SetShaderParameter("smoothness", 0.03f);
                shaderMaterial.SetShaderParameter("spill", 0.9f);
                
                _avatarDisplayRect.Material = shaderMaterial; // Gán vào Rect
            }

            avatarMargin.AddChild(_avatarDisplayRect);
        }
        private Label CreateStyledLabel(int size, Color color)
        {
            var lbl = new Label();
            lbl.AddThemeFontSizeOverride("font_size", size);
            lbl.AddThemeColorOverride("font_color", color);
            return lbl;
        }

        private Button CreateTabButton(string text)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(120, 35);
            return btn;
        }

        private void SwitchTab(string tabName)
        {
            HidePanel(_overviewPanel);
            HidePanel(_equipmentPanel);
            HidePanel(_skillsPanel);
            // ĐÃ XÓA: HidePanel(_talentsPanel);
            
            ResetTabButtonColors();

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
                // ĐÃ XÓA: Case talents
            }
            _currentTab = tabName; 
        }

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
            // ĐÃ XÓA: Talents button reset
        }

        private void LoadCharacterList()
        {
            var children = _characterListContainer.GetChildren();
            foreach (var child in children) child.QueueFree();

            for (int i = 0; i < PlayerManager.Instance.PartyMembers.Count; i++)
            {
                int index = i; 
                var character = PlayerManager.Instance.PartyMembers[i];
                
                if (character?.ConfigData?.Icon != null)
                {
                    var btn = new TextureButton();
                    btn.TextureNormal = character.ConfigData.Icon;
                    btn.CustomMinimumSize = new Vector2(70, 70);
                    btn.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
                    btn.IgnoreTextureSize = true;
                    btn.Pressed += () => OnCharacterSelected(index);
                    
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

            _nameLabel.Text = config.Name;
            _levelLabel.Text = $"LV. {currentStats.CurrentLevel:00}";
            _raceLabel.Text = config.CharacterRace?.RaceName?.ToUpper() ?? "UNKNOWN";
            if (config.Avatar is VideoStream videoStream)
                {
                    // Dừng video cũ nếu có
                    if (_hiddenPlayer.IsPlaying())
                    {
                        _hiddenPlayer.Stop();
                    }
                    
                    // Gán stream mới
                    _hiddenPlayer.Stream = videoStream;
                    _hiddenPlayer.Loop = true;  // Đảm bảo loop được set
                    
                    // Chờ 1 frame rồi mới play để đảm bảo stream đã load
                    CallDeferred(MethodName.PlayVideoDeferred);
                }
            UpdateOverviewPanel(currentStats);            
            LoadCharacterList();
            SwitchTab(_currentTab);
        }

        private void UpdateOverviewPanel(PlayerStats stats)
        {
            foreach (var child in _statsTextContainer.GetChildren()) child.QueueFree();

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

            if (_overviewStatsChart != null)
            {
                _overviewStatsChart.ClearStats();
                if (stats.FinalAttributes != null)
                {
                    foreach (var attr in stats.FinalAttributes)
                    {
                        _overviewStatsChart.SetStat(FormatStatName(attr.Key.ToString()), attr.Value);
                    }
                }
                _overviewStatsChart.UpdateAllStats();
            }
            
            UpdateResourceBars(stats);

        }

        private void UpdateResourceBars(PlayerStats stats)
        {
            SetBarValue(_hpBar, _hpValueLabel, (int)stats.CurrentHP, (int)stats.MaxHP);
            SetBarValue(_mpBar, _mpValueLabel, (int)stats.CurrentMP, (int)stats.MaxMP);
            SetBarValue(_staminaBar, _staminaValueLabel, (int)stats.CurrentStamina, (int)stats.MaxStamina);

        }
        private void SetBarValue(ProgressBar bar, Label valueLabel, int current, int max)
        {
            max = Mathf.Max(1, max);
            current = Mathf.Clamp(current, 0, max);

            if (bar != null)
            {
                bar.MinValue = 0;
                bar.MaxValue = max;
                bar.Value = current;
            }

            if (valueLabel != null)
            {
                valueLabel.Text = $"{current}/{max}";
            }
        }

        private HBoxContainer CreateResourceBarRow(string labelText, Color fillColor, out ProgressBar bar, out Label valueLabel)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var nameLabel = new Label();
            nameLabel.Text = labelText;
            nameLabel.CustomMinimumSize = new Vector2(40, 0);
            nameLabel.AddThemeFontSizeOverride("font_size", 14);
            nameLabel.AddThemeColorOverride("font_color", _subTextColor);
            row.AddChild(nameLabel);

            bar = new ProgressBar();
            bar.CustomMinimumSize = new Vector2(160, 18);
            bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var bg = new StyleBoxFlat();
            bg.BgColor = new Color(0.1f, 0.12f, 0.2f, 1f);
            bg.CornerRadiusTopLeft = 4;
            bg.CornerRadiusTopRight = 4;
            bg.CornerRadiusBottomLeft = 4;
            bg.CornerRadiusBottomRight = 4;

            var fill = new StyleBoxFlat();
            fill.BgColor = fillColor;
            fill.CornerRadiusTopLeft = 4;
            fill.CornerRadiusTopRight = 4;
            fill.CornerRadiusBottomLeft = 4;
            fill.CornerRadiusBottomRight = 4;

            bar.AddThemeStyleboxOverride("background", bg);
            bar.AddThemeStyleboxOverride("fill", fill);

            row.AddChild(bar);

            valueLabel = new Label();
            valueLabel.Text = "0/0";
            valueLabel.CustomMinimumSize = new Vector2(60, 0);
            valueLabel.AddThemeFontSizeOverride("font_size", 14);
            valueLabel.AddThemeColorOverride("font_color", Colors.White);
            row.AddChild(valueLabel);

            return row;
        }



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

        private void PlayVideoDeferred()
        {
            if (_hiddenPlayer != null && _hiddenPlayer.Stream != null)
            {
                _hiddenPlayer.Play();
            }
        }

        private void OnVideoFinished()
        {
            // Backup: Nếu Loop không hoạt động, tự động play lại
            if (_hiddenPlayer != null && _hiddenPlayer.Stream != null)
            {
                _hiddenPlayer.Play();
            }
        }
    }
}