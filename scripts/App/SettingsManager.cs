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
        private const int CurrentSettingsVersion = 3;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static SettingsManager Instance { get; private set; }
        public UserSettingsData CurrentSettings { get; private set; } = new();

        public static SettingsManager GetOrCreate(SceneTree tree)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                return Instance;
            }

            if (tree?.Root == null)
            {
                return null;
            }

            var existing = tree.Root.GetNodeOrNull<SettingsManager>("SettingsManager");
            if (existing != null && GodotObject.IsInstanceValid(existing))
            {
                Instance = existing;
                return existing;
            }

            var manager = new SettingsManager { Name = "SettingsManager" };
            tree.Root.AddChild(manager);
            GD.Print("[SettingsManager] Created runtime fallback at /root/SettingsManager");
            return manager;
        }

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            LoadSettings();
            ApplyAll();
            GD.Print($"[SettingsManager] READY fullscreen={CurrentSettings.Fullscreen} resolution={CurrentSettings.ResolutionWidth}x{CurrentSettings.ResolutionHeight} fps_cap={CurrentSettings.MaxFps}");
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
                SeedDisplayDefaultsFromCurrentWindow();
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
                SeedDisplayDefaultsFromCurrentWindow();
                SaveSettings();
                return;
            }

            try
            {
                CurrentSettings = JsonSerializer.Deserialize<UserSettingsData>(json, _jsonOptions) ?? new UserSettingsData();
                MigrateSettingsIfNeeded();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SettingsManager] Failed to parse settings file: {ex.Message}");
                CurrentSettings = new UserSettingsData();
                SeedDisplayDefaultsFromCurrentWindow();
            }
        }

        public void SaveSettings()
        {
            NormalizeSettings();

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

        public void ApplyAll()
        {
            NormalizeSettings();
            ApplyAudioSettings();
            ApplyDisplaySettings();
            ApplyRuntimeSettings();
            ApplyControlSettings();
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

            if (!CurrentSettings.Fullscreen)
            {
                window.Size = new Vector2I(CurrentSettings.ResolutionWidth, CurrentSettings.ResolutionHeight);
            }
        }

        public void ApplyRuntimeSettings()
        {
            Engine.MaxFps = CurrentSettings.MaxFps;
        }


        public void ApplyControlSettings()
        {
            ApplySkillBinding("skill_1", GetSkillKey(0));
            ApplySkillBinding("skill_2", GetSkillKey(1));
            ApplySkillBinding("skill_3", GetSkillKey(2));
            ApplySkillBinding("skill_4", GetSkillKey(3));
        }

        public Key GetSkillKey(int slotIndex)
        {
            long raw = slotIndex switch
            {
                0 => CurrentSettings.Skill1Key,
                1 => CurrentSettings.Skill2Key,
                2 => CurrentSettings.Skill3Key,
                3 => CurrentSettings.Skill4Key,
                _ => (long)Key.None
            };
            return (Key)raw;
        }

        public void SetSkillKey(int slotIndex, Key key)
        {
            if (slotIndex < 0 || slotIndex > 3 || key == Key.None)
            {
                return;
            }

            Key previousKey = GetSkillKey(slotIndex);
            for (int other = 0; other < 4; other++)
            {
                if (other != slotIndex && GetSkillKey(other) == key)
                {
                    SetSkillKeyRaw(other, previousKey);
                    break;
                }
            }

            SetSkillKeyRaw(slotIndex, key);
            ApplyControlSettings();
            SaveSettings();
        }

        private void SetSkillKeyRaw(int slotIndex, Key key)
        {
            long raw = (long)key;
            switch (slotIndex)
            {
                case 0: CurrentSettings.Skill1Key = raw; break;
                case 1: CurrentSettings.Skill2Key = raw; break;
                case 2: CurrentSettings.Skill3Key = raw; break;
                case 3: CurrentSettings.Skill4Key = raw; break;
            }
        }

        private static void ApplySkillBinding(string actionName, Key key)
        {
            if (key == Key.None)
            {
                return;
            }

            if (!InputMap.HasAction(actionName))
            {
                InputMap.AddAction(actionName);
            }

            var keyboardEvents = new System.Collections.Generic.List<InputEvent>();
            foreach (InputEvent inputEvent in InputMap.ActionGetEvents(actionName))
            {
                if (inputEvent is InputEventKey)
                {
                    keyboardEvents.Add(inputEvent);
                }
            }
            foreach (InputEvent inputEvent in keyboardEvents)
            {
                InputMap.ActionEraseEvent(actionName, inputEvent);
            }

            InputMap.ActionAddEvent(actionName, new InputEventKey
            {
                PhysicalKeycode = key
            });
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

        public void SetResolution(int width, int height)
        {
            CurrentSettings.ResolutionWidth = Mathf.Clamp(width, 960, 7680);
            CurrentSettings.ResolutionHeight = Mathf.Clamp(height, 540, 4320);
            ApplyDisplaySettings();
            SaveSettings();
        }

        public void SetMaxFps(int maxFps)
        {
            CurrentSettings.MaxFps = NormalizeFps(maxFps);
            ApplyRuntimeSettings();
            SaveSettings();
        }

        public void SetScreenShakeIntensity(float value)
        {
            CurrentSettings.ScreenShakeIntensity = Mathf.Clamp(value, 0f, 1f);
            SaveSettings();
        }

        public void SetHitStopEnabled(bool enabled)
        {
            CurrentSettings.HitStopEnabled = enabled;
            SaveSettings();
        }

        public void SetDamageNumbersEnabled(bool enabled)
        {
            CurrentSettings.DamageNumbersEnabled = enabled;
            SaveSettings();
        }

        private void MigrateSettingsIfNeeded()
        {
            CurrentSettings ??= new UserSettingsData();
            if (CurrentSettings.Version < CurrentSettingsVersion)
            {
                // Version 1 only had audio + fullscreen. Preserve the current project window size
                // instead of silently forcing a new resolution during migration.
                Window window = GetWindow();
                if (window != null && !CurrentSettings.Fullscreen && window.Size.X > 0 && window.Size.Y > 0)
                {
                    CurrentSettings.ResolutionWidth = window.Size.X;
                    CurrentSettings.ResolutionHeight = window.Size.Y;
                }

                CurrentSettings.Version = CurrentSettingsVersion;
                SaveSettings();
            }
        }

        private void SeedDisplayDefaultsFromCurrentWindow()
        {
            Window window = GetWindow();
            if (window == null)
            {
                return;
            }

            CurrentSettings.Fullscreen = window.Mode == Window.ModeEnum.Fullscreen
                || window.Mode == Window.ModeEnum.ExclusiveFullscreen;

            if (!CurrentSettings.Fullscreen && window.Size.X > 0 && window.Size.Y > 0)
            {
                CurrentSettings.ResolutionWidth = window.Size.X;
                CurrentSettings.ResolutionHeight = window.Size.Y;
            }
        }

        private void NormalizeSettings()
        {
            CurrentSettings ??= new UserSettingsData();
            CurrentSettings.Version = CurrentSettingsVersion;
            CurrentSettings.MasterVolumeLinear = ClampLinear(CurrentSettings.MasterVolumeLinear);
            CurrentSettings.BgmVolumeLinear = ClampLinear(CurrentSettings.BgmVolumeLinear);
            CurrentSettings.SfxVolumeLinear = ClampLinear(CurrentSettings.SfxVolumeLinear);
            CurrentSettings.UiVolumeLinear = ClampLinear(CurrentSettings.UiVolumeLinear);
            CurrentSettings.VoiceVolumeLinear = ClampLinear(CurrentSettings.VoiceVolumeLinear);
            CurrentSettings.ResolutionWidth = Mathf.Clamp(CurrentSettings.ResolutionWidth, 960, 7680);
            CurrentSettings.ResolutionHeight = Mathf.Clamp(CurrentSettings.ResolutionHeight, 540, 4320);
            CurrentSettings.MaxFps = NormalizeFps(CurrentSettings.MaxFps);
            CurrentSettings.ScreenShakeIntensity = Mathf.Clamp(CurrentSettings.ScreenShakeIntensity, 0f, 1f);
            NormalizeControlBindings();
        }


        private void NormalizeControlBindings()
        {
            if ((Key)CurrentSettings.Skill1Key == Key.None) CurrentSettings.Skill1Key = (long)Key.Q;
            if ((Key)CurrentSettings.Skill2Key == Key.None) CurrentSettings.Skill2Key = (long)Key.E;
            if ((Key)CurrentSettings.Skill3Key == Key.None) CurrentSettings.Skill3Key = (long)Key.R;
            if ((Key)CurrentSettings.Skill4Key == Key.None) CurrentSettings.Skill4Key = (long)Key.F;
        }

        private static int NormalizeFps(int value)
        {
            if (value <= 0)
            {
                return 0;
            }
            return Mathf.Clamp(value, 30, 360);
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
