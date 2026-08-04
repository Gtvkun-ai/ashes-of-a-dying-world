using Godot;

namespace AshesofaDyingWorld.Core.Data
{
    public enum AudioBusType
    {
        Master,
        Bgm,
        Sfx,
        Ui,
        Voice
    }

    [GlobalClass]
    public partial class AudioCueData : Resource
    {
        [ExportGroup("Audio")]
        [Export] public AudioStream Stream { get; set; }

        [Export] public AudioBusType BusType { get; set; } = AudioBusType.Sfx;

        [ExportGroup("Volume")]
        [Export] public float VolumeDb { get; set; } = 0f;

        [ExportGroup("Pitch Random")]
        [Export] public float MinPitch { get; set; } = 1f;
        [Export] public float MaxPitch { get; set; } = 1f;

        [ExportGroup("Playback")]
        [Export] public bool Loop { get; set; } = false;

        public float GetRandomPitch()
        {
            float min = Mathf.Min(MinPitch, MaxPitch);
            float max = Mathf.Max(MinPitch, MaxPitch);

            return (float)GD.RandRange(min, max);
        }

        public string GetBusName()
        {
            return BusType switch
            {
                AudioBusType.Bgm => "BGM",
                AudioBusType.Sfx => "SFX",
                AudioBusType.Ui => "UI",
                AudioBusType.Voice => "Voice",
                _ => "Master"
            };
        }
    }
}