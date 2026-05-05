using Godot;
using AshesofaDyingWorld.Core.Managers;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class SettingsPanel : Panel
    {
        private HSlider _masterSlider;
        private Label _masterValue;
        private HSlider _bgmSlider;
        private Label _bgmValue;
        private HSlider _sfxSlider;
        private Label _sfxValue;
        private HSlider _uiSlider;
        private Label _uiValue;
        private HSlider _voiceSlider;
        private Label _voiceValue;
        private CheckButton _fullscreenToggle;
        private bool _isRefreshingUi;

        public override void _Ready()
        {
            EnsureUi();
            RefreshFromSettings();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationVisibilityChanged && Visible)
            {
                RefreshFromSettings();
            }
        }

        private void EnsureUi()
        {
            if (GetNodeOrNull<Control>("Backdrop") != null)
            {
                return;
            }

            var backdrop = new ColorRect
            {
                Name = "Backdrop", //Backdrop là một ColorRect phủ toàn bộ panel với màu đen bán trong suốt để làm mờ nền khi mở settings
                Color = new Color(0f, 0f, 0f, 0.55f),
                MouseFilter = MouseFilterEnum.Stop
            };
            StretchFullRect(backdrop); // Hàm tiện ích để đặt anchors và offsets sao cho control phủ đầy parent
            AddChild(backdrop);

            var center = new CenterContainer
            {
                Name = "CenterContainer",
                MouseFilter = MouseFilterEnum.Stop
            };
            StretchFullRect(center);
            AddChild(center);

            var card = new PanelContainer // Card là một PanelContainer ở giữa
            {
                Name = "Card",
                CustomMinimumSize = new Vector2(460f, 0f),
                MouseFilter = MouseFilterEnum.Stop // Đảm bảo rằng card nhận sự kiện chuột để người dùng có thể tương tác với nó, đồng thời ngăn sự kiện này truyền xuống backdrop bên dưới
            };
            center.AddChild(card);

            var margin = new MarginContainer
            {
                Name = "Margin"
            };
            margin.AddThemeConstantOverride("margin_left", 24);
            margin.AddThemeConstantOverride("margin_top", 20);
            margin.AddThemeConstantOverride("margin_right", 24);
            margin.AddThemeConstantOverride("margin_bottom", 20);
            card.AddChild(margin);

            var layout = new VBoxContainer
            {
                Name = "Layout"
            };
            layout.AddThemeConstantOverride("separation", 14);
            margin.AddChild(layout); // Layout là một VBoxContainer bên trong card để chứa tất cả các phần tử UI của settings, với khoảng cách giữa chúng là 14 pixels

            layout.AddChild(new Label
            {
                Text = "Settings"
            });

            layout.AddChild(new HSeparator());
            layout.AddChild(new Label
            {
                Text = "Audio"
            });

            
            CreateVolumeRow(layout, "Master", out _masterSlider, out _masterValue, value => OnVolumeChanged(value, _masterValue, manager => manager.SetMasterVolumeLinear));
            CreateVolumeRow(layout, "BGM", out _bgmSlider, out _bgmValue, value => OnVolumeChanged(value, _bgmValue, manager => manager.SetBgmVolumeLinear));
            CreateVolumeRow(layout, "SFX", out _sfxSlider, out _sfxValue, value => OnVolumeChanged(value, _sfxValue, manager => manager.SetSfxVolumeLinear));
            CreateVolumeRow(layout, "UI", out _uiSlider, out _uiValue, value => OnVolumeChanged(value, _uiValue, manager => manager.SetUiVolumeLinear));
            CreateVolumeRow(layout, "Voice", out _voiceSlider, out _voiceValue, value => OnVolumeChanged(value, _voiceValue, manager => manager.SetVoiceVolumeLinear));

            layout.AddChild(new HSeparator());

            var fullscreenRow = new HBoxContainer();
            fullscreenRow.AddThemeConstantOverride("separation", 12);
            layout.AddChild(fullscreenRow);

            var fullscreenLabel = new Label
            {
                Text = "Fullscreen",
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            fullscreenRow.AddChild(fullscreenLabel);

            _fullscreenToggle = new CheckButton
            {
                Text = "On"
            };
            _fullscreenToggle.Toggled += OnFullscreenToggled; // Đăng ký sự kiện khi toggle được bật/tắt
            fullscreenRow.AddChild(_fullscreenToggle);

            var actionRow = new HBoxContainer(); // Row chứa các nút hành động ở cuối panel (Reset, Close)
            actionRow.AddThemeConstantOverride("separation", 12);
            layout.AddChild(actionRow);

            var resetButton = new Button
            {
                Text = "Reset"
            };
            resetButton.Pressed += OnResetPressed;
            actionRow.AddChild(resetButton);

            var closeButton = new Button
            {
                Text = "Close"
            };
            closeButton.Pressed += () => Hide();
            actionRow.AddChild(closeButton);
        }

        private void CreateVolumeRow(
            VBoxContainer parent,
            string labelText,
            out HSlider slider,
            out Label valueLabel,
            Godot.Range.ValueChangedEventHandler onValueChanged)
        {
            var row = new HBoxContainer(); // Mỗi row chứa một slider và label hiển thị giá trị phần trăm của nó
            row.AddThemeConstantOverride("separation", 12);
            parent.AddChild(row);

            var label = new Label
            {
                Text = labelText,
                CustomMinimumSize = new Vector2(90f, 0f)
            };
            row.AddChild(label);

            slider = new HSlider
            {
                MinValue = 0,
                MaxValue = 100,
                Step = 1,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            slider.ValueChanged += onValueChanged;
            row.AddChild(slider);

            valueLabel = new Label
            {
                Text = "100%",
                CustomMinimumSize = new Vector2(52f, 0f),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            row.AddChild(valueLabel);
        }

        private void RefreshFromSettings()
        {
            SettingsManager manager = SettingsManager.Instance;
            if (manager == null)
            {
                return;
            }

            _isRefreshingUi = true;

            SetSlider(_masterSlider, _masterValue, manager.CurrentSettings.MasterVolumeLinear);
            SetSlider(_bgmSlider, _bgmValue, manager.CurrentSettings.BgmVolumeLinear);
            SetSlider(_sfxSlider, _sfxValue, manager.CurrentSettings.SfxVolumeLinear);
            SetSlider(_uiSlider, _uiValue, manager.CurrentSettings.UiVolumeLinear);
            SetSlider(_voiceSlider, _voiceValue, manager.CurrentSettings.VoiceVolumeLinear);

            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.ButtonPressed = manager.CurrentSettings.Fullscreen;
            }

            _isRefreshingUi = false;
        }

        private void OnVolumeChanged(double value, Label valueLabel, System.Func<SettingsManager, System.Action<float>> setterFactory)
        {
            valueLabel.Text = $"{value:0}%";

            if (_isRefreshingUi)
            {
                return;
            }

            SettingsManager manager = SettingsManager.Instance; // Lấy instance của SettingsManager để cập nhật cài đặt âm lượng khi slider thay đổi
            if (manager == null)
            {
                return;
            }

            float linear = (float)value / 100f; // Chuyển giá trị phần trăm (0-100) thành giá trị tuyến tính (0.0-1.0) trước khi gửi đến SettingsManager
            setterFactory(manager).Invoke(linear); // Sử dụng setterFactory để gọi phương thức thiết lập âm lượng tương ứng trên SettingsManager, truyền giá trị tuyến tính đã được chuẩn hóa
        }

        private void OnFullscreenToggled(bool toggledOn)
        {
            if (_isRefreshingUi)
            {
                return;
            }

            SettingsManager.Instance?.SetFullscreen(toggledOn);
        }

        private void OnResetPressed()
        {
            SettingsManager.Instance?.ResetToDefaults();
            RefreshFromSettings();
        }

        private static void SetSlider(HSlider slider, Label valueLabel, float linear)
        {
            if (slider == null || valueLabel == null)
            {
                return;
            }

            float percent = Mathf.Round(linear * 100f);
            slider.Value = percent;
            valueLabel.Text = $"{percent:0}%";
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
