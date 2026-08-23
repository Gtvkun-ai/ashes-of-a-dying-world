using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.UI.Shared;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class SettingsPanel : Panel
    {
        private enum SettingsTab
        {
            Audio,
            Display,
            Controls,
            Gameplay
        }

        private readonly Dictionary<SettingsTab, Button> _tabButtons = new();
        private readonly Vector2I[] _commonResolutions =
        {
            new(1280, 720),
            new(1600, 900),
            new(1920, 1080),
            new(2560, 1440)
        };
        private readonly int[] _fpsOptions = { 60, 120, 144, 240 };

        private SettingsManager _settings;
        private VBoxContainer _content;
        private Label _sectionEyebrow;
        private Label _sectionTitle;
        private Label _sectionDescription;
        private SettingsTab _activeTab = SettingsTab.Audio;
        private bool _isRefreshingUi;
        private int _bindingSkillSlot = -1;
        private Button _bindingButton;
        private bool _settingsPauseOwned;
        private bool _treeWasPaused;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            _settings = SettingsManager.GetOrCreate(GetTree());
            BuildUi();
            ShowTab(SettingsTab.Audio);
        }

        public override void _Notification(int what)
        {
            if (what != NotificationVisibilityChanged)
            {
                return;
            }

            if (Visible)
            {
                PauseGameForSettings();
                if (_content != null)
                {
                    _settings = SettingsManager.GetOrCreate(GetTree());
                    ShowTab(_activeTab);
                }
            }
            else
            {
                CancelKeyBinding();
                RestoreGamePauseState();
            }
        }

        public override void _ExitTree()
        {
            RestoreGamePauseState();
        }

        public override void _UnhandledKeyInput(InputEvent @event)
        {
            if (!Visible || @event is not InputEventKey key || !key.Pressed || key.Echo)
            {
                return;
            }

            if (_bindingSkillSlot >= 0)
            {
                if (key.Keycode == Key.Escape)
                {
                    CancelKeyBinding();
                    GetViewport()?.SetInputAsHandled();
                    return;
                }

                Key pressedKey = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
                if (IsReservedCharacterSwitchKey(key))
                {
                    if (_bindingButton != null)
                    {
                        _bindingButton.Text = "1 / 2 / 3 dành cho đổi nhân vật";
                    }
                    GetViewport()?.SetInputAsHandled();
                    return;
                }

                if (pressedKey != Key.None)
                {
                    _settings?.SetSkillKey(_bindingSkillSlot, pressedKey);
                    _bindingSkillSlot = -1;
                    _bindingButton = null;
                    ShowTab(SettingsTab.Controls);
                    GetViewport()?.SetInputAsHandled();
                    return;
                }
            }

            if (key.Keycode == Key.Escape)
            {
                Hide();
                GetViewport()?.SetInputAsHandled();
            }
        }

        private void PauseGameForSettings()
        {
            SceneTree tree = GetTree();
            if (tree == null || _settingsPauseOwned)
            {
                return;
            }

            _treeWasPaused = tree.Paused;
            tree.Paused = true;
            _settingsPauseOwned = true;
        }

        private void RestoreGamePauseState()
        {
            SceneTree tree = GetTree();
            if (tree == null || !_settingsPauseOwned)
            {
                return;
            }

            tree.Paused = _treeWasPaused;
            _settingsPauseOwned = false;
        }

        private void BuildUi()
        {
            var backdrop = new ColorRect
            {
                Name = "Backdrop",
                Color = new Color(0f, 0f, 0f, 0.72f),
                MouseFilter = MouseFilterEnum.Stop
            };
            StretchFullRect(backdrop);
            AddChild(backdrop);

            var window = new Control
            {
                Name = "SettingsWindow",
                MouseFilter = MouseFilterEnum.Stop
            };
            InventoryPanelChrome.ApplyPanelSize(window);
            AddChild(window);

            VBoxContainer root = InventoryPanelChrome.BuildWindowShell(window);
            root.AddThemeConstantOverride("separation", 7);

            root.AddChild(BuildHeader());

            var body = new HBoxContainer
            {
                Name = "Body",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            body.AddThemeConstantOverride("separation", 10);
            root.AddChild(body);

            body.AddChild(BuildSidebar());
            body.AddChild(BuildContentArea());
            root.AddChild(BuildFooter());
        }

        private Control BuildHeader()
        {
            PanelContainer header = InventoryPanelChrome.CreateHeader(out HBoxContainer row);
            header.Name = "Header";

            var titleStack = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            titleStack.AddThemeConstantOverride("separation", 0);
            row.AddChild(titleStack);

            var title = InventoryPanelChrome.CreateLabel("CÀI ĐẶT", 22, InventoryPanelChrome.MainTextColor);
            titleStack.AddChild(title);

            var subtitle = InventoryPanelChrome.CreateLabel(
                "Âm thanh  •  Hiển thị  •  Điều khiển  •  Trải nghiệm",
                12,
                InventoryPanelChrome.MutedTextColor);
            titleStack.AddChild(subtitle);

            var saveHint = InventoryPanelChrome.CreateLabel("LƯU TỰ ĐỘNG", 11, InventoryPanelChrome.AccentColor);
            saveHint.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(saveHint);

            row.AddChild(InventoryPanelChrome.CreateCloseButton(Hide));
            return header;
        }

        private Control BuildSidebar()
        {
            var sidebarPanel = new PanelContainer
            {
                Name = "SidebarPanel",
                CustomMinimumSize = new Vector2(210f, 0f),
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            sidebarPanel.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreateSectionStyle());

            var margin = new MarginContainer();
            AddMargins(margin, 8, 10, 8, 10);
            sidebarPanel.AddChild(margin);

            var sidebar = new VBoxContainer
            {
                Name = "Sidebar",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            sidebar.AddThemeConstantOverride("separation", 5);
            margin.AddChild(sidebar);

            var label = InventoryPanelChrome.CreateLabel("DANH MỤC", 11, InventoryPanelChrome.AccentColor);
            sidebar.AddChild(label);
            sidebar.AddChild(InventoryPanelChrome.CreateDivider(true));

            AddTabButton(sidebar, SettingsTab.Audio, "ÂM THANH", "Nhạc và hiệu ứng");
            AddTabButton(sidebar, SettingsTab.Display, "HIỂN THỊ", "Cửa sổ và FPS");
            AddTabButton(sidebar, SettingsTab.Controls, "ĐIỀU KHIỂN", "Phím kỹ năng");
            AddTabButton(sidebar, SettingsTab.Gameplay, "TRẢI NGHIỆM", "Combat feedback");

            var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
            sidebar.AddChild(spacer);

            var note = InventoryPanelChrome.CreateLabel(
                "Mọi thay đổi được áp dụng ngay.\nKhông cần nút Apply.",
                11,
                InventoryPanelChrome.MutedTextColor);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            sidebar.AddChild(note);

            return sidebarPanel;
        }

        private void AddTabButton(VBoxContainer parent, SettingsTab tab, string title, string subtitle)
        {
            var button = new Button
            {
                Text = $"{title}\n{subtitle}",
                CustomMinimumSize = new Vector2(0f, 58f),
                Alignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.All,
                MouseDefaultCursorShape = CursorShape.PointingHand,
                TooltipText = subtitle
            };
            button.AddThemeFontSizeOverride("font_size", 13);
            button.Pressed += () => ShowTab(tab);
            parent.AddChild(button);
            _tabButtons[tab] = button;
        }

        private Control BuildContentArea()
        {
            var panel = new PanelContainer
            {
                Name = "ContentPanel",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            panel.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreateDetailSectionStyle());

            var margin = new MarginContainer();
            AddMargins(margin, 18, 14, 14, 12);
            panel.AddChild(margin);

            var right = new VBoxContainer
            {
                Name = "Right",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            right.AddThemeConstantOverride("separation", 4);
            margin.AddChild(right);

            _sectionEyebrow = InventoryPanelChrome.CreateLabel(string.Empty, 10, InventoryPanelChrome.AccentColor);
            right.AddChild(_sectionEyebrow);

            _sectionTitle = InventoryPanelChrome.CreateLabel(string.Empty, 22, InventoryPanelChrome.MainTextColor);
            right.AddChild(_sectionTitle);

            _sectionDescription = InventoryPanelChrome.CreateLabel(string.Empty, 12, InventoryPanelChrome.MutedTextColor);
            _sectionDescription.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _sectionDescription.CustomMinimumSize = new Vector2(0f, 34f);
            right.AddChild(_sectionDescription);

            right.AddChild(InventoryPanelChrome.CreateDivider(true));

            var scroll = new ScrollContainer
            {
                Name = "Scroll",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            right.AddChild(scroll);

            var scrollMargin = new MarginContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            AddMargins(scrollMargin, 0, 8, 5, 4);
            scroll.AddChild(scrollMargin);

            _content = new VBoxContainer
            {
                Name = "Content",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _content.AddThemeConstantOverride("separation", 7);
            scrollMargin.AddChild(_content);

            return panel;
        }

        private Control BuildFooter()
        {
            var footer = new PanelContainer
            {
                Name = "Footer",
                CustomMinimumSize = new Vector2(0f, 48f)
            };
            footer.AddThemeStyleboxOverride("panel", InventoryPanelChrome.CreateTabsBarStyle());

            var margin = new MarginContainer();
            AddMargins(margin, 10, 5, 7, 5);
            footer.AddChild(margin);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            margin.AddChild(row);

            var status = InventoryPanelChrome.CreateLabel(
                "●  Cài đặt được lưu tự động",
                11,
                InventoryPanelChrome.MutedTextColor);
            status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            status.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(status);

            var reset = new Button
            {
                Text = "Khôi phục mặc định",
                CustomMinimumSize = new Vector2(165f, 34f),
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            StyleSecondaryButton(reset);
            reset.Pressed += OnResetPressed;
            row.AddChild(reset);

            var close = new Button
            {
                Text = "Đóng",
                CustomMinimumSize = new Vector2(90f, 34f),
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            StylePrimaryButton(close);
            close.Pressed += Hide;
            row.AddChild(close);

            return footer;
        }

        private void ShowTab(SettingsTab tab)
        {
            if (tab != SettingsTab.Controls && _bindingSkillSlot >= 0)
            {
                CancelKeyBinding();
            }
            _activeTab = tab;
            _settings ??= SettingsManager.GetOrCreate(GetTree());
            UpdateTabStyles();
            ClearContent();
            _isRefreshingUi = true;

            switch (tab)
            {
                case SettingsTab.Audio:
                    BuildAudioTab();
                    break;
                case SettingsTab.Display:
                    BuildDisplayTab();
                    break;
                case SettingsTab.Controls:
                    BuildControlsTab();
                    break;
                case SettingsTab.Gameplay:
                    BuildGameplayTab();
                    break;
            }

            _isRefreshingUi = false;
        }

        private void BuildAudioTab()
        {
            SetSectionHeader(
                "AUDIO",
                "Âm thanh",
                "Chỉ giữ những bus game đang dùng thật: âm lượng tổng, nhạc nền và hiệu ứng chiến đấu.");

            if (_settings == null)
            {
                AddUnavailableCard();
                return;
            }

            AddSliderRow(
                "Âm lượng tổng",
                "Điều chỉnh toàn bộ âm thanh của game.",
                _settings.CurrentSettings.MasterVolumeLinear * 100f,
                value => _settings.SetMasterVolumeLinear(value / 100f));

            AddSliderRow(
                "Nhạc nền",
                "Âm lượng BGM, bao gồm bg_02 trong gameplay.",
                _settings.CurrentSettings.BgmVolumeLinear * 100f,
                value => _settings.SetBgmVolumeLinear(value / 100f));

            AddSliderRow(
                "Hiệu ứng chiến đấu",
                "Kiếm, hit, block, parry, băng và phản ứng của slime.",
                _settings.CurrentSettings.SfxVolumeLinear * 100f,
                value => _settings.SetSfxVolumeLinear(value / 100f));
        }

        private void BuildDisplayTab()
        {
            SetSectionHeader(
                "DISPLAY",
                "Hiển thị",
                "Các tùy chọn có tác dụng thật trên bản PC hiện tại. Không nhét checkbox giả cho đủ quân số.");

            if (_settings == null)
            {
                AddUnavailableCard();
                return;
            }

            var mode = new OptionButton { CustomMinimumSize = new Vector2(245f, 38f) };
            mode.AddItem("Cửa sổ");
            mode.AddItem("Toàn màn hình");
            mode.Selected = _settings.CurrentSettings.Fullscreen ? 1 : 0;
            StyleOptionButton(mode);
            mode.ItemSelected += index =>
            {
                if (_isRefreshingUi) return;
                _settings.SetFullscreen(index == 1);
                ShowTab(SettingsTab.Display);
            };
            AddControlRow(
                "Chế độ hiển thị",
                "Fullscreen dùng độ phân giải màn hình; Windowed dùng độ phân giải bên dưới.",
                mode);

            var resolutionOptions = BuildResolutionOptions();
            var resolution = new OptionButton
            {
                CustomMinimumSize = new Vector2(245f, 38f),
                Disabled = _settings.CurrentSettings.Fullscreen
            };
            int selectedResolution = 0;
            for (int i = 0; i < resolutionOptions.Count; i++)
            {
                Vector2I size = resolutionOptions[i];
                resolution.AddItem($"{size.X} × {size.Y}");
                if (size.X == _settings.CurrentSettings.ResolutionWidth
                    && size.Y == _settings.CurrentSettings.ResolutionHeight)
                {
                    selectedResolution = i;
                }
            }
            resolution.Selected = selectedResolution;
            StyleOptionButton(resolution);
            resolution.ItemSelected += index =>
            {
                if (_isRefreshingUi) return;
                int safeIndex = Mathf.Clamp((int)index, 0, resolutionOptions.Count - 1);
                Vector2I size = resolutionOptions[safeIndex];
                _settings.SetResolution(size.X, size.Y);
            };
            AddControlRow(
                "Độ phân giải cửa sổ",
                "Được khóa khi đang ở chế độ toàn màn hình.",
                resolution);

            var fps = new OptionButton { CustomMinimumSize = new Vector2(245f, 38f) };
            int selectedFps = 0;
            for (int i = 0; i < _fpsOptions.Length; i++)
            {
                int value = _fpsOptions[i];
                fps.AddItem($"{value} FPS");
                if (value == _settings.CurrentSettings.MaxFps)
                {
                    selectedFps = i;
                }
            }
            fps.Selected = selectedFps;
            StyleOptionButton(fps);
            fps.ItemSelected += index =>
            {
                if (_isRefreshingUi) return;
                int safeIndex = Mathf.Clamp((int)index, 0, _fpsOptions.Length - 1);
                _settings.SetMaxFps(_fpsOptions[safeIndex]);
            };
            AddControlRow(
                "Giới hạn FPS",
                "Giữ FPS có trần để OpenGL/NVIDIA không bị kẹt render khi Alt-Tab.",
                fps);
        }

        private void BuildControlsTab()
        {
            SetSectionHeader(
                "CONTROLS",
                "Điều khiển",
                "Đổi phím cho bốn ô kỹ năng. Phím 1 / 2 / 3 được giữ riêng cho chuyển nhân vật trong tổ đội.");

            if (_settings == null)
            {
                AddUnavailableCard();
                return;
            }

            AddSkillBindingRow(0, "Kỹ năng 1", "Ô kỹ năng chủ động số 1.");
            AddSkillBindingRow(1, "Kỹ năng 2", "Ô kỹ năng chủ động số 2.");
            AddSkillBindingRow(2, "Kỹ năng 3", "Ô kỹ năng chủ động số 3.");
            AddSkillBindingRow(3, "Kỹ năng 4", "Ô kỹ năng chủ động số 4.");

            var hint = InventoryPanelChrome.CreateLabel(
                "Chuyển nhân vật: 1 = thành viên 1, 2 = thành viên 2, 3 = thành viên 3. Các phím này không thể gán cho skill để tránh hai hành động nổ cùng lúc.",
                11,
                InventoryPanelChrome.MutedTextColor);
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            AddControlRow("Phím dành riêng", "Quyền điều khiển tổ đội", hint);
        }

        private void AddSkillBindingRow(int slotIndex, string title, string description)
        {
            Key key = _settings.GetSkillKey(slotIndex);
            var button = new Button
            {
                Text = FormatKey(key),
                CustomMinimumSize = new Vector2(185f, 38f),
                FocusMode = FocusModeEnum.All,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            StylePrimaryButton(button);
            button.Pressed += () => BeginKeyBinding(slotIndex, button);
            AddControlRow(title, description, button);
        }

        private void BeginKeyBinding(int slotIndex, Button button)
        {
            _bindingSkillSlot = slotIndex;
            _bindingButton = button;
            button.Text = "Nhấn phím mới...";
            button.GrabFocus();
        }

        private void CancelKeyBinding()
        {
            if (_bindingSkillSlot >= 0 && _bindingButton != null && _settings != null)
            {
                _bindingButton.Text = FormatKey(_settings.GetSkillKey(_bindingSkillSlot));
            }
            _bindingSkillSlot = -1;
            _bindingButton = null;
        }

        private static bool IsReservedCharacterSwitchKey(InputEventKey key)
        {
            return key.Unicode == (uint)'1' || key.Unicode == (uint)'2' || key.Unicode == (uint)'3';
        }

        private static string FormatKey(Key key)
        {
            string text = key.ToString();
            return string.IsNullOrWhiteSpace(text) ? "Chưa gán" : text.ToUpperInvariant();
        }

        private void BuildGameplayTab()
        {
            SetSectionHeader(
                "COMFORT",
                "Trải nghiệm",
                "Combat vẫn có lực, nhưng người chơi có quyền giảm các hiệu ứng dễ gây mỏi mắt hoặc mất tập trung.");

            if (_settings == null)
            {
                AddUnavailableCard();
                return;
            }

            AddSliderRow(
                "Rung màn hình",
                "Giảm hoặc tắt camera shake khi hit mạnh, parry và shatter.",
                _settings.CurrentSettings.ScreenShakeIntensity * 100f,
                value => _settings.SetScreenShakeIntensity(value / 100f));

            AddControlRow(
                "Hit-stop",
                "Khoảnh khắc khựng rất ngắn khi đòn đánh trúng để tăng cảm giác va chạm.",
                MakeToggle(_settings.CurrentSettings.HitStopEnabled, enabled => _settings.SetHitStopEnabled(enabled)));

            AddControlRow(
                "Số sát thương",
                "Bật hoặc tắt damage number trong combat.",
                MakeToggle(_settings.CurrentSettings.DamageNumbersEnabled, enabled => _settings.SetDamageNumbersEnabled(enabled)));
        }

        private List<Vector2I> BuildResolutionOptions()
        {
            var options = new List<Vector2I>();
            Vector2I current = new(
                _settings?.CurrentSettings.ResolutionWidth ?? 1280,
                _settings?.CurrentSettings.ResolutionHeight ?? 720);

            bool currentIsCommon = false;
            foreach (Vector2I size in _commonResolutions)
            {
                if (size == current)
                {
                    currentIsCommon = true;
                    break;
                }
            }

            if (!currentIsCommon)
            {
                options.Add(current);
            }

            options.AddRange(_commonResolutions);
            return options;
        }

        private void SetSectionHeader(string eyebrow, string title, string description)
        {
            _sectionEyebrow.Text = eyebrow;
            _sectionTitle.Text = title;
            _sectionDescription.Text = description;
        }

        private void AddSliderRow(string title, string description, float initialPercent, Action<float> onChanged)
        {
            var slider = new HSlider
            {
                MinValue = 0,
                MaxValue = 100,
                Step = 1,
                Value = Mathf.Clamp(initialPercent, 0f, 100f),
                CustomMinimumSize = new Vector2(220f, 24f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.All
            };
            slider.TooltipText = $"{title}: kéo hoặc dùng phím mũi tên";

            var value = InventoryPanelChrome.CreateLabel($"{slider.Value:0}%", 13, InventoryPanelChrome.MainTextColor);
            value.CustomMinimumSize = new Vector2(54f, 0f);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.VerticalAlignment = VerticalAlignment.Center;

            var control = new HBoxContainer { CustomMinimumSize = new Vector2(300f, 38f) };
            control.AddThemeConstantOverride("separation", 10);
            control.AddChild(slider);
            control.AddChild(value);

            slider.ValueChanged += changed =>
            {
                value.Text = $"{changed:0}%";
                if (!_isRefreshingUi)
                {
                    onChanged((float)changed);
                }
            };

            AddControlRow(title, description, control);
        }

        private CheckButton MakeToggle(bool initial, Action<bool> onChanged)
        {
            var toggle = new CheckButton
            {
                ButtonPressed = initial,
                Text = initial ? "Bật" : "Tắt",
                CustomMinimumSize = new Vector2(105f, 38f),
                FocusMode = FocusModeEnum.All,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            toggle.AddThemeColorOverride("font_color", InventoryPanelChrome.MainTextColor);
            toggle.AddThemeColorOverride("font_hover_color", Colors.White);
            toggle.Toggled += enabled =>
            {
                toggle.Text = enabled ? "Bật" : "Tắt";
                if (!_isRefreshingUi)
                {
                    onChanged(enabled);
                }
            };
            return toggle;
        }

        private void AddControlRow(string title, string description, Control control)
        {
            var card = new PanelContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0f, 76f)
            };
            card.AddThemeStyleboxOverride("panel", MakeRowStyle());
            _content.AddChild(card);

            var margin = new MarginContainer();
            AddMargins(margin, 14, 10, 14, 10);
            card.AddChild(margin);

            var row = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            row.AddThemeConstantOverride("separation", 18);
            margin.AddChild(row);

            var labels = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            labels.AddThemeConstantOverride("separation", 2);
            row.AddChild(labels);

            var titleLabel = InventoryPanelChrome.CreateLabel(title, 14, InventoryPanelChrome.MainTextColor);
            labels.AddChild(titleLabel);

            var descriptionLabel = InventoryPanelChrome.CreateLabel(description, 11, InventoryPanelChrome.MutedTextColor);
            descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            labels.AddChild(descriptionLabel);

            control.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(control);
        }

        private void AddUnavailableCard()
        {
            var label = InventoryPanelChrome.CreateLabel(
                "SettingsManager chưa sẵn sàng. Panel sẽ tự thử lại khi được mở lần sau.",
                12,
                InventoryPanelChrome.AccentColor);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            AddControlRow("Không thể tải cài đặt", "Runtime manager chưa được khởi tạo.", label);
        }

        private void OnResetPressed()
        {
            _settings ??= SettingsManager.GetOrCreate(GetTree());
            _settings?.ResetToDefaults();
            ShowTab(_activeTab);
        }

        private void UpdateTabStyles()
        {
            foreach (var pair in _tabButtons)
            {
                InventoryPanelChrome.ApplyTabStyle(pair.Value, pair.Key == _activeTab);
            }
        }

        private void ClearContent()
        {
            if (_content == null)
            {
                return;
            }

            foreach (Node child in _content.GetChildren())
            {
                child.QueueFree();
            }
        }

        private void StylePrimaryButton(Button button)
        {
            PixelButtonSkin.ApplyPrimary(button, PixelButtonSkin.RegularHeight);
        }

        private void StyleSecondaryButton(Button button)
        {
            PixelButtonSkin.ApplySecondary(button, PixelButtonSkin.RegularHeight);
        }

        private void StyleOptionButton(OptionButton option)
        {
            option.MouseDefaultCursorShape = CursorShape.PointingHand;
            PixelButtonSkin.ApplySecondary(option, PixelButtonSkin.TabHeight);
            option.AddThemeColorOverride("font_color", InventoryPanelChrome.MainTextColor);
        }

        private static StyleBoxFlat MakeRowStyle()
        {
            var style = InventoryPanelChrome.CreateSectionStyle();
            style.BgColor = InventoryPanelChrome.WithAlpha(InventoryPanelChrome.DeepSurfaceColor, 0.54f);
            style.BorderColor = InventoryPanelChrome.WithAlpha(InventoryPanelChrome.BorderColor, 0.72f);
            return style;
        }

        private static void AddMargins(MarginContainer margin, int left, int top, int right, int bottom)
        {
            margin.AddThemeConstantOverride("margin_left", left);
            margin.AddThemeConstantOverride("margin_top", top);
            margin.AddThemeConstantOverride("margin_right", right);
            margin.AddThemeConstantOverride("margin_bottom", bottom);
        }

        private static void StretchFullRect(Control control)
        {
            control.AnchorLeft = 0f;
            control.AnchorTop = 0f;
            control.AnchorRight = 1f;
            control.AnchorBottom = 1f;
            control.OffsetLeft = 0f;
            control.OffsetTop = 0f;
            control.OffsetRight = 0f;
            control.OffsetBottom = 0f;
        }
    }
}
