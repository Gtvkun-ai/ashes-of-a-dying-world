using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Combat.Visuals;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Presentation-only feedback: camera impulse, procedural impact VFX, impact SFX
    /// và flash màn hình khi Player nhận damage.
    /// </summary>
    public partial class CombatFeedbackService : CanvasLayer
    {
        public static CombatFeedbackService Instance { get; private set; }

        private readonly RandomNumberGenerator _rng = new();
        private float _shakeRemaining;
        private float _shakeDuration;
        private float _shakeStrength;
        private Camera2D _shakeCamera;
        private Vector2 _cameraBaseOffset;
        private ColorRect _damageFlash;
        private float _damageFlashRemaining;
        private Label _damageDirectionMarker;
        private float _damageDirectionRemaining;
        private Vector2 _lastIncomingDirection;
        private AudioCueData _physicalImpactCue;
        private AudioCueData _iceImpactCue;
        private AudioCueData _swingCue;

        public override void _Ready()
        {
            if (Instance != null && Instance != this)
            {
                QueueFree();
                return;
            }

            Instance = this;
            Layer = 70;
            _rng.Randomize();
            BuildDamageFlash();
            LoadAudioCues();
        }

        public override void _ExitTree()
        {
            RestoreCameraOffset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static CombatFeedbackService GetOrCreate(SceneTree tree)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                return Instance;
            }

            if (tree?.Root == null)
            {
                return null;
            }

            var service = new CombatFeedbackService { Name = "CombatFeedbackService" };
            tree.Root.AddChild(service);
            return service;
        }

        public void PlayHit(
            CombatCharacter attacker,
            CombatCharacter target,
            HitRequest request,
            HitResult result)
        {
            if (target == null || request?.Profile == null || result == null || !result.Applied)
            {
                return;
            }

            HitProfileData profile = request.Profile;
            Vector2 direction = request.AttackDirection.LengthSquared() > 0.001f
                ? request.AttackDirection.Normalized()
                : (target.CombatCenter - (attacker?.CombatCenter ?? request.HitOrigin)).Normalized();
            bool ice = profile.DamageType == DamageType.Ice || profile.ChillStacks > 0 || profile.FreezeOnHit;
            bool strong = result.Shattered
                || (request.Action != null && (request.Action.Tags & CombatActionTag.Heavy) != CombatActionTag.None)
                || profile.LaunchHeight > 0f
                || result.GuardBroken;

            SpawnImpact(target.CombatCenter, direction, profile.ImpactVfxScale * (strong ? 1.25f : 1f), ice, result.WasBlocked, result.Shattered);
            AddCameraShake(profile.CameraShakeStrength * (strong ? 1.3f : 1f), strong ? 0.16f : 0.10f);
            PlayImpactAudio(ice, strong);

            if (target.Faction == CombatFaction.Player && result.HpDamage > 0f)
            {
                _damageFlashRemaining = Mathf.Max(_damageFlashRemaining, strong ? 0.18f : 0.11f);
                _damageDirectionRemaining = Mathf.Max(_damageDirectionRemaining, strong ? 0.42f : 0.30f);
                _lastIncomingDirection = (request.HitOrigin - target.CombatCenter).Normalized();
            }
        }

        public void PlaySwing(CombatCharacter actor, CombatActionData action, Vector2 direction)
        {
            if (actor == null || action == null || action.DeliveryMode != CombatDeliveryMode.MeleeHitbox)
            {
                return;
            }

            Node parent = GetTree()?.CurrentScene ?? GetTree()?.Root;
            if (parent == null)
            {
                return;
            }

            // Player sword animations already contain their slash arc in the sprite sheet.
            // Do not layer the procedural trail on top or the swing appears doubled.
            // Keep the generic trail available for non-player melee actors that may still need it.
            if (actor.Faction != CombatFaction.Player)
            {
                bool heavy = (action.Tags & CombatActionTag.Heavy) != CombatActionTag.None;
                var trail = new CombatSwingTrail2D { Name = heavy ? "HeavySwingTrail" : "SwingTrail" };
                trail.Initialize(direction, heavy ? 1.35f : 1f);
                parent.AddChild(trail);
                trail.GlobalPosition = actor.CombatCenter;
            }

            if (_swingCue != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(_swingCue);
            }
        }

        public override void _Process(double delta)
        {
            float dt = Mathf.Max(0f, (float)delta);
            UpdateCameraShake(dt);
            UpdateDamageFlash(dt);
            UpdateDamageDirection(dt);
        }

        private void SpawnImpact(Vector2 worldPosition, Vector2 direction, float strength, bool ice, bool blocked, bool shattered)
        {
            Node parent = GetTree()?.CurrentScene ?? GetTree()?.Root;
            if (parent == null)
            {
                return;
            }

            var burst = new CombatImpactBurst2D
            {
                Name = shattered ? "ShatterImpact" : ice ? "IceImpact" : "CombatImpact"
            };
            burst.Initialize(direction, strength, ice, blocked, shattered);
            parent.AddChild(burst);
            burst.GlobalPosition = worldPosition;
        }

        private void AddCameraShake(float strength, float duration)
        {
            if (strength <= 0.01f || duration <= 0.01f)
            {
                return;
            }

            Camera2D camera = GetViewport()?.GetCamera2D();
            if (camera == null)
            {
                return;
            }

            if (_shakeCamera != camera || _shakeRemaining <= 0f)
            {
                RestoreCameraOffset();
                _shakeCamera = camera;
                _cameraBaseOffset = camera.Offset;
            }

            _shakeStrength = Mathf.Max(_shakeStrength, strength);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
            _shakeRemaining = Mathf.Max(_shakeRemaining, duration);
        }

        private void UpdateCameraShake(float delta)
        {
            if (_shakeRemaining <= 0f || _shakeCamera == null || !GodotObject.IsInstanceValid(_shakeCamera))
            {
                RestoreCameraOffset();
                return;
            }

            _shakeRemaining -= delta;
            float ratio = Mathf.Clamp(_shakeRemaining / Mathf.Max(0.01f, _shakeDuration), 0f, 1f);
            float amplitude = _shakeStrength * ratio;
            _shakeCamera.Offset = _cameraBaseOffset + new Vector2(
                _rng.RandfRange(-amplitude, amplitude),
                _rng.RandfRange(-amplitude, amplitude));

            if (_shakeRemaining <= 0f)
            {
                RestoreCameraOffset();
            }
        }

        private void RestoreCameraOffset()
        {
            if (_shakeCamera != null && GodotObject.IsInstanceValid(_shakeCamera))
            {
                _shakeCamera.Offset = _cameraBaseOffset;
            }

            _shakeCamera = null;
            _shakeRemaining = 0f;
            _shakeDuration = 0f;
            _shakeStrength = 0f;
        }

        private void BuildDamageFlash()
        {
            _damageFlash = new ColorRect
            {
                Name = "PlayerDamageFlash",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Color = new Color(0.68f, 0.05f, 0.03f, 0f)
            };
            AddChild(_damageFlash);
            _damageFlash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            _damageDirectionMarker = new Label
            {
                Name = "IncomingDamageDirection",
                Text = "▲",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = new Color(1f, 0.34f, 0.20f, 0f)
            };
            _damageDirectionMarker.AddThemeFontSizeOverride("font_size", 20);
            _damageDirectionMarker.AddThemeColorOverride("font_color", new Color(1f, 0.34f, 0.20f));
            _damageDirectionMarker.AddThemeColorOverride("font_outline_color", Colors.Black);
            _damageDirectionMarker.AddThemeConstantOverride("outline_size", 4);
            AddChild(_damageDirectionMarker);
        }

        private void UpdateDamageFlash(float delta)
        {
            if (_damageFlash == null)
            {
                return;
            }

            if (_damageFlashRemaining <= 0f)
            {
                _damageFlash.Color = new Color(0.68f, 0.05f, 0.03f, 0f);
                return;
            }

            _damageFlashRemaining -= delta;
            float alpha = Mathf.Clamp(_damageFlashRemaining / 0.18f, 0f, 1f) * 0.12f;
            _damageFlash.Color = new Color(0.68f, 0.05f, 0.03f, alpha);
        }

        private void UpdateDamageDirection(float delta)
        {
            if (_damageDirectionMarker == null)
            {
                return;
            }

            if (_damageDirectionRemaining <= 0f || _lastIncomingDirection.LengthSquared() <= 0.001f)
            {
                _damageDirectionMarker.Modulate = new Color(1f, 0.34f, 0.20f, 0f);
                return;
            }

            _damageDirectionRemaining -= delta;
            Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
            float radius = Mathf.Min(viewportSize.X, viewportSize.Y) * 0.20f;
            Vector2 center = viewportSize * 0.5f;
            Vector2 markerSize = _damageDirectionMarker.GetCombinedMinimumSize();
            _damageDirectionMarker.Position = center + _lastIncomingDirection * radius - markerSize * 0.5f;
            _damageDirectionMarker.Rotation = _lastIncomingDirection.Angle() + Mathf.Pi * 0.5f;
            float alpha = Mathf.Clamp(_damageDirectionRemaining / 0.42f, 0f, 1f);
            _damageDirectionMarker.Modulate = new Color(1f, 0.34f, 0.20f, alpha);
        }

        private void LoadAudioCues()
        {
            AudioStream physical = GD.Load<AudioStream>("res://assets/audio/sfx/tools/hammer/hammer_slash_01.mp3");
            if (physical != null)
            {
                _physicalImpactCue = new AudioCueData
                {
                    Stream = physical,
                    BusType = AudioBusType.Sfx,
                    VolumeDb = -10f,
                    MinPitch = 0.88f,
                    MaxPitch = 1.08f
                };
            }

            AudioStream slash = GD.Load<AudioStream>("res://assets/audio/sfx/weapons/sword/wooden_slash_01.mp3");
            if (slash != null)
            {
                _swingCue = new AudioCueData
                {
                    Stream = slash,
                    BusType = AudioBusType.Sfx,
                    VolumeDb = -9f,
                    MinPitch = 0.94f,
                    MaxPitch = 1.08f
                };
                _iceImpactCue = new AudioCueData
                {
                    Stream = slash,
                    BusType = AudioBusType.Sfx,
                    VolumeDb = -12f,
                    MinPitch = 1.18f,
                    MaxPitch = 1.36f
                };
            }
        }

        private void PlayImpactAudio(bool ice, bool strong)
        {
            AudioCueData cue = ice ? _iceImpactCue : _physicalImpactCue;
            if (cue == null || AudioManager.Instance == null)
            {
                return;
            }

            float originalVolume = cue.VolumeDb;
            if (strong)
            {
                cue.VolumeDb += 2f;
            }
            AudioManager.Instance.PlaySfx(cue);
            cue.VolumeDb = originalVolume;
        }
    }
}
