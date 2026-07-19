using Godot;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using AshesofaDyingWorld.Core.Save;

namespace AshesofaDyingWorld.Core.Managers
{
    public partial class SettingsManager : Node
    {
        private const string SettingsPath = "user://settings.json";
        private const float MinLinearVolume = 0.0001f;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static SettingsManager Instance { get; private set; }

        public UserSettingsData CurrentSettings { get; private set; } = new();

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            LoadSettings();
            ApplyAll();
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void LoadSettings()
        {
            if (!FileAccess.FileExists(SettingsPath))
            {
                CurrentSettings = new UserSettingsData();
                SaveSettings();
                return;
            }

            using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"[SettingsManager] Cannot open settings for reading: {SettingsPath}");
                CurrentSettings = new UserSettingsData();
                return;
            }

            string json = file.GetAsText();
            if (string.IsNullOrWhiteSpace(json))
            {
                CurrentSettings = new UserSettingsData();
                SaveSettings();
                return;
            }

            try
            {
                CurrentSettings = JsonSerializer.Deserialize<UserSettingsData>(json, _jsonOptions) ?? new UserSettingsData();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SettingsManager] Failed to parse settings file: {ex.Message}");
                CurrentSettings = new UserSettingsData();
            }
        }

        public void SaveSettings()
        {
            NormalizeSettings(); // Chắc chắn rằng các giá trị âm lượng đã được chuẩn hóa trước khi lưu

            string json;
            try
            {
                json = JsonSerializer.Serialize(CurrentSettings, _jsonOptions);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SettingsManager] Failed to serialize settings: {ex.Message}");
                return;
            }

            using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"[SettingsManager] Cannot open settings for writing: {SettingsPath}");
                return;
            }

            file.StoreString(json);
            file.Flush();
        }

        // Áp dụng tất cả cài đặt hiện tại (âm thanh, hiển thị, v.v.)
        public void ApplyAll()
        {
            NormalizeSettings();
            ApplyAudioSettings();
            ApplyDisplaySettings();
        }

        public void ApplyAudioSettings()
        {
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager == null)
            {
                return;
            }

            audioManager.SetMasterVolumeDb(LinearToDb(CurrentSettings.MasterVolumeLinear));
            audioManager.SetBgmVolumeDb(LinearToDb(CurrentSettings.BgmVolumeLinear));
            audioManager.SetSfxVolumeDb(LinearToDb(CurrentSettings.SfxVolumeLinear));
            audioManager.SetUiVolumeDb(LinearToDb(CurrentSettings.UiVolumeLinear));
            audioManager.SetVoiceVolumeDb(LinearToDb(CurrentSettings.VoiceVolumeLinear));
        }

        // Áp dụng cài đặt hiển thị (fullscreen/windowed)
        public void ApplyDisplaySettings()
        {
            Window window = GetWindow();
            if (window == null)
            {
                return;
            }

            window.Mode = CurrentSettings.Fullscreen
                ? Window.ModeEnum.Fullscreen
                : Window.ModeEnum.Windowed;
        }

        public void ResetToDefaults()
        {
            CurrentSettings = new UserSettingsData();
            ApplyAll();
            SaveSettings();
        }

        public void SetMasterVolumeLinear(float value)
        {
            CurrentSettings.MasterVolumeLinear = ClampLinear(value);
            ApplyAudioSettings();
            SaveSettings();
        }

        public void SetBgmVolumeLinear(float value)
        {
            CurrentSettings.BgmVolumeLinear = ClampLinear(value);
            ApplyAudioSettings();
            SaveSettings();
        }

        public void SetSfxVolumeLinear(float value)
        {
            CurrentSettings.SfxVolumeLinear = ClampLinear(value);
            ApplyAudioSettings();
            SaveSettings();
        }

        public void SetUiVolumeLinear(float value)
        {
            CurrentSettings.UiVolumeLinear = ClampLinear(value);
            ApplyAudioSettings();
            SaveSettings();
        }

        public void SetVoiceVolumeLinear(float value)
        {
            CurrentSettings.VoiceVolumeLinear = ClampLinear(value);
            ApplyAudioSettings();
            SaveSettings();
        }

        public void SetFullscreen(bool fullscreen)
        {
            CurrentSettings.Fullscreen = fullscreen;
            ApplyDisplaySettings();
            SaveSettings();
        }

        private void NormalizeSettings()
        {
            CurrentSettings ??= new UserSettingsData();
            CurrentSettings.MasterVolumeLinear = ClampLinear(CurrentSettings.MasterVolumeLinear);
            CurrentSettings.BgmVolumeLinear = ClampLinear(CurrentSettings.BgmVolumeLinear);
            CurrentSettings.SfxVolumeLinear = ClampLinear(CurrentSettings.SfxVolumeLinear);
            CurrentSettings.UiVolumeLinear = ClampLinear(CurrentSettings.UiVolumeLinear);
            CurrentSettings.VoiceVolumeLinear = ClampLinear(CurrentSettings.VoiceVolumeLinear);
        }

        private static float ClampLinear(float value)
        {
            return Mathf.Clamp(value, 0f, 1f);
        }

        private static float LinearToDb(float linear)
        {
            if (linear <= MinLinearVolume)
            {
                return -80f;
            }

            return Mathf.LinearToDb(linear);
        }
    }
}
