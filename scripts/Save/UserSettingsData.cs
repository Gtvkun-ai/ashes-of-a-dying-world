using Godot;

namespace AshesofaDyingWorld.Core.Save
{
    public sealed class UserSettingsData
    {
        public int Version { get; set; } = 3;

        // Audio
        public float MasterVolumeLinear { get; set; } = 1f;
        public float BgmVolumeLinear { get; set; } = 1f;
        public float SfxVolumeLinear { get; set; } = 1f;
        public float UiVolumeLinear { get; set; } = 1f;
        public float VoiceVolumeLinear { get; set; } = 1f;

        // Display
        public bool Fullscreen { get; set; } = false;
        public int ResolutionWidth { get; set; } = 1280;
        public int ResolutionHeight { get; set; } = 720;
        public int MaxFps { get; set; } = 144;

        // Gameplay / comfort
        public float ScreenShakeIntensity { get; set; } = 1f;
        public bool HitStopEnabled { get; set; } = true;
        public bool DamageNumbersEnabled { get; set; } = true;

        // Controls. Store enum values as long so settings.json stays engine-version tolerant.
        public long Skill1Key { get; set; } = (long)Key.Q;
        public long Skill2Key { get; set; } = (long)Key.E;
        public long Skill3Key { get; set; } = (long)Key.R;
        public long Skill4Key { get; set; } = (long)Key.F;
    }
}
