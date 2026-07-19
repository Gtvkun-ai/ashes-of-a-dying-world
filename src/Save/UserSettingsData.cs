namespace AshesofaDyingWorld.Core.Save
{
    public sealed class UserSettingsData
    {
        public int Version { get; set; } = 1;
        public float MasterVolumeLinear { get; set; } = 1f;
        public float BgmVolumeLinear { get; set; } = 1f;
        public float SfxVolumeLinear { get; set; } = 1f;
        public float UiVolumeLinear { get; set; } = 1f;
        public float VoiceVolumeLinear { get; set; } = 1f;
        public bool Fullscreen { get; set; } = false;
    }
}
