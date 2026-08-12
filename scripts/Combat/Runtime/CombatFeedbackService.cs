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
    /// Presentation-only feedback: camera impulse, asset-backed combat VFX, impact SFX
    /// và flash màn hình khi Player nhận damage.
    /// </summary>
    public partial class CombatFeedbackService : CanvasLayer
    {
        public static CombatFeedbackService Instance { get; private set; }

        private const string PhysicalLightImpactPath = "res://assets/graphics/vfx/combat/hit/physical_hit_light.png";
        private const string IceImpactPath = "res://assets/graphics/vfx/combat/ice/ice_impact.png";
        private const string BlockSparkFramesPath = "res://assets/graphics/vfx/combat/defense/block_spark_frames.tres";
        private const string ParryFlashFramesPath = "res://assets/graphics/vfx/combat/defense/parry_flash_frames.tres";
        private const string ParryRingFramesPath = "res://assets/graphics/vfx/combat/defense/parry_ring_frames.tres";

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
        private AudioCueData _parryCue;

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
            bool heavy = request.Action != null
                && (request.Action.Tags & CombatActionTag.Heavy) != CombatActionTag.None;
            bool strong = result.Shattered
                || heavy
                || profile.LaunchHeight > 0f
                || result.GuardBroken;

            SpawnImpact(
                target.CombatCenter,
                direction,
                profile.ImpactVfxScale * (strong ? 1.25f : 1f),
                ice,
                result.WasBlocked,
                result.Shattered,
                heavy,
                result.GuardBroken);
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

        /// <summary>
        /// Feedback riêng cho perfect parry thật đã được gameplay layer xác nhận.
        /// Không dùng guard-break hay block thường giả làm parry.
        /// </summary>
        public void PlayParry(Vector2 worldPosition, Vector2 incomingDirection)
        {
            Node parent = GetTree()?.CurrentScene ?? GetTree()?.Root;
            if (parent == null)
            {
                return;
            }

            float rotation = incomingDirection.LengthSquared() > 0.001f
                ? incomingDirection.Angle()
                : 0f;
            SpawnFramesVfx(parent, worldPosition, ParryFlashFramesPath, "parry_flash", 0.72f, rotation, "ParryFlash");
            SpawnFramesVfx(parent, worldPosition, ParryRingFramesPath, "parry_ring", 0.78f, 0f, "ParryRing");
            AddCameraShake(1.2f, 0.12f);

            if (_parryCue != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySfx(_parryCue);
            }
        }

        public override void _Process(double delta)
        {
            float dt = Mathf.Max(0f, (float)delta);
            UpdateCameraShake(dt);
            UpdateDamageFlash(dt);
            UpdateDamageDirection(dt);
        }

        private void SpawnImpact(
            Vector2 worldPosition,
            Vector2 direction,
            float strength,
            bool ice,
            bool blocked,
            bool shattered,
            bool heavy,
            bool guardBroken)
        {
            Node parent = GetTree()?.CurrentScene ?? GetTree()?.Root;
            if (parent == null)
            {
                return;
            }

            // Guard feedback có asset riêng. Guard break vẫn thêm burst procedural để đọc được độ nặng.
            if (blocked)
            {
                Vector2 contact = worldPosition - direction * 5f;
                bool spawnedBlock = SpawnFramesVfx(
                    parent,
                    contact,
                    BlockSparkFramesPath,
                    "block",
                    Mathf.Clamp(0.58f * strength, 0.48f, 0.88f),
                    direction.Angle(),
                    "BlockSpark");

                if (!spawnedBlock || guardBroken)
                {
                    SpawnProceduralImpact(parent, contact, direction, strength, false, true, false);
                }
                return;
            }

            // Shatter chưa có art riêng trong asset hiện tại: dùng Ice Impact + burst procedural,
            // thay vì giả vờ physical-light là shatter.
            if (shattered)
            {
                SpawnSheetVfx(
                    parent,
                    worldPosition,
                    IceImpactPath,
                    48,
                    48,
                    6,
                    30f,
                    Mathf.Clamp(0.72f * strength, 0.72f, 1.25f),
                    0f,
                    "ShatterIceImpact");
                SpawnProceduralImpact(parent, worldPosition, direction, strength, true, false, true);
                return;
            }

            if (ice)
            {
                bool spawnedIce = SpawnSheetVfx(
                    parent,
                    worldPosition,
                    IceImpactPath,
                    48,
                    48,
                    6,
                    30f,
                    Mathf.Clamp(0.62f * strength, 0.56f, 1.08f),
                    0f,
                    "IceImpact");
                if (spawnedIce)
                {
                    return;
                }
            }

            // Hiện asset mới chỉ có physical hit light. Heavy giữ fallback procedural
            // cho tới khi có physical_hit_heavy thật, tránh phóng to light rồi gọi đó là heavy.
            if (!heavy)
            {
                bool spawnedPhysical = SpawnSheetVfx(
                    parent,
                    worldPosition,
                    PhysicalLightImpactPath,
                    48,
                    48,
                    5,
                    30f,
                    Mathf.Clamp(0.62f * strength, 0.52f, 0.92f),
                    direction.Angle(),
                    "PhysicalLightImpact");
                if (spawnedPhysical)
                {
                    return;
                }
            }

            SpawnProceduralImpact(parent, worldPosition, direction, strength, ice, false, shattered);
        }

        private static bool SpawnSheetVfx(
            Node parent,
            Vector2 worldPosition,
            string texturePath,
            int frameWidth,
            int frameHeight,
            int frameCount,
            float fps,
            float scale,
            float rotation,
            string nodeName)
        {
            var fx = new CombatSpriteVfx2D { Name = nodeName };
            if (!fx.InitializeFromHorizontalSheet(
                texturePath,
                frameWidth,
                frameHeight,
                frameCount,
                fps,
                scale,
                rotation))
            {
                fx.Free();
                return false;
            }

            parent.AddChild(fx);
            fx.GlobalPosition = worldPosition;
            return true;
        }

        private static bool SpawnFramesVfx(
            Node parent,
            Vector2 worldPosition,
            string framesPath,
            StringName animation,
            float scale,
            float rotation,
            string nodeName)
        {
            var fx = new CombatSpriteVfx2D { Name = nodeName };
            if (!fx.InitializeFromSpriteFrames(framesPath, animation, scale, rotation))
            {
                fx.Free();
                return false;
            }

            parent.AddChild(fx);
            fx.GlobalPosition = worldPosition;
            return true;
        }

        private static void SpawnProceduralImpact(
            Node parent,
            Vector2 worldPosition,
            Vector2 direction,
            float strength,
            bool ice,
            bool blocked,
            bool shattered)
        {
            var burst = new CombatImpactBurst2D
            {
                Name = shattered ? "ShatterImpactFallback" : ice ? "IceImpactFallback" : "CombatImpactFallback"
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
                _parryCue = new AudioCueData
                {
                    Stream = slash,
                    BusType = AudioBusType.Sfx,
                    VolumeDb = -5f,
                    MinPitch = 1.48f,
                    MaxPitch = 1.62f
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
