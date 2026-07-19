using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Core.Managers
{
    public partial class AudioManager : Node
    {
        private const string DefaultBgmPath = "res://assets/music/bgm/Bg1.mp3";

        public static AudioManager Instance { get; private set; }

        [ExportGroup("poolling")]
        [Export] public int InitialSfxPoolSize { get; set; } = 12;
        [Export] public int MaxSfxPoolSize { get; set; } = 32;

        [ExportGroup("Default Volume")]
        [Export] public float MasterVolumeDb { get; set; } = 0f;
        [Export] public float BgmVolumeDb { get; set; } = 0f;
        [Export] public float SfxVolumeDb { get; set; } = 0f;
        [Export] public float UiVolumeDb { get; set; } = 0f;
        [Export] public float VoiceVolumeDb { get; set; } = 0f;
        [Export] public string DefaultBgmResourcePath { get; set; } = DefaultBgmPath;
        [Export] public bool AutoPlayDefaultBgm { get; set; } = true;

        private AudioStreamPlayer _bgmPlayer;
        private readonly List<AudioStreamPlayer> _sfxPlayers = new();
        private AudioCueData _defaultBgmCue;

        public override void _Ready()
        {
            Instance = this;

            EnsureAudioBus("BGM");
            EnsureAudioBus("SFX");
            EnsureAudioBus("UI");
            EnsureAudioBus("Voice");

            CreateBgmPlayer();
            CreateSfxPool();

            SetMasterVolumeDb(MasterVolumeDb);
            SetBgmVolumeDb(BgmVolumeDb);
            SetSfxVolumeDb(SfxVolumeDb);
            SetUiVolumeDb(UiVolumeDb);
            SetVoiceVolumeDb(VoiceVolumeDb);

            CallDeferred(nameof(FinalizeStartupAudioState));
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }


        private void FinalizeStartupAudioState()
        {
            // Dùng toán tử ?. (null-conditional) để an toàn gọi
            SettingsManager.Instance?.ApplyAudioSettings();

            if (AutoPlayDefaultBgm)
            {
                PlayDefaultBgm();
            }
        }

        private void CreateBgmPlayer()
        {
            _bgmPlayer = new AudioStreamPlayer();
            _bgmPlayer.Name = "BgmPlayer";
            _bgmPlayer.Bus = "BGM";
            AddChild(_bgmPlayer);
        }

        private void CreateSfxPool()
        {
            for (int i = 0; i < InitialSfxPoolSize; i++)
            {
                _sfxPlayers.Add(CreateSfxPlayer(i));
            }
        }

        private AudioStreamPlayer CreateSfxPlayer(int index)
        {
            var player = new AudioStreamPlayer();
            player.Name = $"SfxPlayer_{index}";
            player.Bus = "SFX";
            AddChild(player);
            return player;
        }

        public void PlayBgm(AudioCueData cue)
        {
            if (cue == null || cue.Stream == null || _bgmPlayer == null)
            {
                return;
            }

            if (_bgmPlayer.Stream == cue.Stream && _bgmPlayer.Playing)
            {
                return;
            }

            _bgmPlayer.Stop();
            _bgmPlayer.Stream = cue.Stream;
            _bgmPlayer.VolumeDb = cue.VolumeDb;
            _bgmPlayer.PitchScale = cue.GetRandomPitch();
            _bgmPlayer.Bus = cue.GetBusName();

            if (_bgmPlayer.Stream is AudioStreamOggVorbis ogg)
            {
                ogg.Loop = cue.Loop;
            }
            else if (_bgmPlayer.Stream is AudioStreamMP3 mp3)
            {
                mp3.Loop = cue.Loop;
            }
            else if (_bgmPlayer.Stream is AudioStreamWav wav)
            {
                wav.LoopMode = cue.Loop
                    ? AudioStreamWav.LoopModeEnum.Forward
                    : AudioStreamWav.LoopModeEnum.Disabled;
            }

            _bgmPlayer.Play();
        }

        public void PlayDefaultBgm()
        {
            if (_defaultBgmCue == null)
            {
                if (string.IsNullOrWhiteSpace(DefaultBgmResourcePath))
                {
                    return;
                }

                AudioStream stream = GD.Load<AudioStream>(DefaultBgmResourcePath);
                if (stream == null)
                {
                    GD.PrintErr($"[AudioManager] Failed to load default BGM: {DefaultBgmResourcePath}");
                    return;
                }

                _defaultBgmCue = new AudioCueData
                {
                    Stream = stream,
                    BusType = AudioBusType.Bgm,
                    VolumeDb = 0f,
                    MinPitch = 1f,
                    MaxPitch = 1f,
                    Loop = true
                };
            }

            PlayBgm(_defaultBgmCue);
        }

        public void StopBgm()
        {
            _bgmPlayer?.Stop();
        }

        public void PlaySfx(AudioCueData cue)
        {
            if (cue == null || cue.Stream == null)
            {
                return;
            }

            AudioStreamPlayer player = GetAudioStreamPlayer();
            if (player == null)
            {
                return;
            }

            player.Stop();
            player.Stream = cue.Stream;
            player.VolumeDb = cue.VolumeDb;
            player.PitchScale = cue.GetRandomPitch();
            player.Bus = cue.GetBusName();
            player.Play();
        }

        public void PlayUi(AudioCueData cue)
        {
            PlaySfx(cue);
        }

        private AudioStreamPlayer GetAudioStreamPlayer()
        {
            foreach (var player in _sfxPlayers)
            {
                if (!player.Playing)
                {
                    return player;
                }
            }

            if (_sfxPlayers.Count >= MaxSfxPoolSize)
            {
                return null;
            }

            var newPlayer = CreateSfxPlayer(_sfxPlayers.Count);
            _sfxPlayers.Add(newPlayer);
            return newPlayer;
        }

        public void SetMasterVolumeDb(float volumeDb)
        {
            MasterVolumeDb = volumeDb;
            SetBusVolume("Master", volumeDb);
        }

        public void SetBgmVolumeDb(float volumeDb)
        {
            BgmVolumeDb = volumeDb;
            SetBusVolume("BGM", volumeDb);
        }

        public void SetSfxVolumeDb(float volumeDb)
        {
            SfxVolumeDb = volumeDb;
            SetBusVolume("SFX", volumeDb);
        }

        public void SetUiVolumeDb(float volumeDb)
        {
            UiVolumeDb = volumeDb;
            SetBusVolume("UI", volumeDb);
        }

        public void SetVoiceVolumeDb(float volumeDb)
        {
            VoiceVolumeDb = volumeDb;
            SetBusVolume("Voice", volumeDb);
        }

        public void SetMasterMuted(bool muted)
        {
            SetBusMuted("Master", muted);
        }

        public void SetBgmMuted(bool muted)
        {
            SetBusMuted("BGM", muted);
        }

        public void SetSfxMuted(bool muted)
        {
            SetBusMuted("SFX", muted);
        }

        public void SetUiMuted(bool muted)
        {
            SetBusMuted("UI", muted);
        }

        private void SetBusVolume(string busName, float volumeDb)
        {
            int index = AudioServer.GetBusIndex(busName);
            if (index == -1)
            {
                return;
            }

            AudioServer.SetBusVolumeDb(index, volumeDb);
        }

        private void SetBusMuted(string busName, bool muted)
        {
            int index = AudioServer.GetBusIndex(busName);
            if (index == -1)
            {
                return;
            }

            AudioServer.SetBusMute(index, muted);
        }

        private void EnsureAudioBus(string busName)
        {
            if (AudioServer.GetBusIndex(busName) != -1)
            {
                return;
            }

            AudioServer.AddBus();
            int newBusIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(newBusIndex, busName);

            int masterIndex = AudioServer.GetBusIndex("Master");
            if (masterIndex != -1)
            {
                AudioServer.SetBusSend(newBusIndex, "Master");
            }
        }
    }
}
