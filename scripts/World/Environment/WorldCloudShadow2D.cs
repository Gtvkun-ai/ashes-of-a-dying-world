using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Drives the world-locked broad cloud light/shadow overlay.
    ///
    /// The important detail is that movement is integrated into flow_offset. We do not
    /// multiply TIME by a direction that changes at runtime, because that would teleport
    /// the noise field whenever wind changes. Every 35-75 seconds this controller picks a
    /// new nearby weather-cell direction, speed and density, then eases toward it.
    /// </summary>
    public partial class WorldCloudShadow2D : Polygon2D
    {
        [ExportGroup("Random Weather Cells")]
        [Export]
        public bool RandomizeOnReady { get; set; } = true;

        /// <summary>0 = random every scene load. Non-zero = deterministic for capture/tests.</summary>
        [Export]
        public int Seed { get; set; } = 0;

        [Export(PropertyHint.Range, "15,180,1")]
        public float MinRetargetSeconds { get; set; } = 28f;

        [Export(PropertyHint.Range, "15,240,1")]
        public float MaxRetargetSeconds { get; set; } = 52f;

        [ExportGroup("Cloud Motion")]
        [Export]
        public Vector2 BaseWindDirection { get; set; } = new Vector2(1f, 0.12f);

        [Export(PropertyHint.Range, "0,45,0.5")]
        public float DirectionJitterDegrees { get; set; } = 18f;

        /// <summary>Physical drift speed in world pixels / second before environment wind.</summary>
        [Export(PropertyHint.Range, "0,40,0.25")]
        public float MinSpeed { get; set; } = 5.5f;

        [Export(PropertyHint.Range, "0,50,0.25")]
        public float MaxSpeed { get; set; } = 11.0f;

        [Export(PropertyHint.Range, "0.02,2,0.01")]
        public float TransitionResponsiveness { get; set; } = 0.11f;

        /// <summary>Must match world_cloud_shadow.gdshader world_scale.</summary>
        [Export(PropertyHint.Range, "0.00025,0.004,0.00005")]
        public float WorldScale { get; set; } = 0.00078f;

        [ExportGroup("Shape Drift")]
        [Export(PropertyHint.Range, "0,0.14,0.005")]
        public float DensityJitter { get; set; } = 0.062f;

        [Export(PropertyHint.Range, "0,0.5,0.01")]
        public float SunOpenJitter { get; set; } = 0.16f;

        private readonly RandomNumberGenerator _rng = new();
        private ShaderMaterial _material;
        private WorldEnvironmentService _environment;

        private Vector2 _flowOffset;
        private Vector2 _currentDirection = Vector2.Right;
        private Vector2 _targetDirection = Vector2.Right;
        private float _currentSpeed = 8f;
        private float _targetSpeed = 8f;
        private float _currentDensityBias;
        private float _targetDensityBias;
        private float _currentSunOpenBoost = 1f;
        private float _targetSunOpenBoost = 1f;
        private float _retargetRemaining;
        private float _worldScale = 0.00110f;

        public override void _Ready()
        {
            _material = Material as ShaderMaterial;
            if (_material == null)
            {
                GD.PushWarning("[WorldCloudShadow2D] Missing ShaderMaterial; cloud pass disabled.");
                SetProcess(false);
                return;
            }

            _environment = WorldEnvironmentService.GetOrCreate(GetTree());
            ConfigureRandom();

            _worldScale = Mathf.Max(WorldScale, 0.00001f);
            _material.SetShaderParameter("world_scale", _worldScale);

            Vector2 baseDirection = SafeDirection(BaseWindDirection);
            _currentDirection = baseDirection;
            _targetDirection = baseDirection;
            _currentSpeed = Mathf.Max(MinSpeed, 0f);
            _targetSpeed = _currentSpeed;

            // Random phase gives every map load a different broad arrangement without
            // creating any temporal pop after the scene is visible.
            Vector2 seedOffset = RandomizeOnReady
                ? new Vector2(_rng.RandfRange(-96f, 96f), _rng.RandfRange(-96f, 96f))
                : new Vector2(17f, 41f);
            _material.SetShaderParameter("seed_offset", seedOffset);

            ChooseNextCell(immediate: true);
            ApplyMaterialState();

            GD.Print(
                $"[WorldCloudShadow2D] READY V7.2 | seed={(Seed == 0 ? "random" : Seed.ToString())} | " +
                $"speed={_currentSpeed:0.0}px/s | broad_world_clouds=ON");
        }

        public override void _Process(double delta)
        {
            if (_material == null)
            {
                return;
            }

            float dt = Mathf.Max((float)delta, 0f);
            _retargetRemaining -= dt;
            if (_retargetRemaining <= 0f)
            {
                ChooseNextCell(immediate: false);
            }

            // Slow easing keeps cell changes invisible instead of snapping the cloud mass.
            float blend = Mathf.Clamp(Mathf.Max(TransitionResponsiveness, 0.01f) * dt, 0f, 1f);
            _currentDirection = SafeDirection(_currentDirection.Lerp(_targetDirection, blend));
            _currentSpeed = Mathf.Lerp(_currentSpeed, _targetSpeed, blend);
            _currentDensityBias = Mathf.Lerp(_currentDensityBias, _targetDensityBias, blend);
            _currentSunOpenBoost = Mathf.Lerp(_currentSunOpenBoost, _targetSunOpenBoost, blend);

            float windStrength = _environment?.CurrentState?.WindStrength ?? 0.6f;
            float weatherSpeedMultiplier = Mathf.Lerp(0.76f, 1.42f, Mathf.Clamp(windStrength, 0f, 1.6f) / 1.6f);

            // Convert physical world-pixel motion to the shader's scaled noise domain.
            _flowOffset += _currentDirection * _currentSpeed * weatherSpeedMultiplier * dt * _worldScale;

            ApplyMaterialState();
        }

        private void ConfigureRandom()
        {
            if (Seed == 0)
            {
                _rng.Randomize();
            }
            else
            {
                _rng.Seed = (ulong)(uint)Seed;
            }
        }

        private void ChooseNextCell(bool immediate)
        {
            float minWait = Mathf.Max(1f, Mathf.Min(MinRetargetSeconds, MaxRetargetSeconds));
            float maxWait = Mathf.Max(minWait, Mathf.Max(MinRetargetSeconds, MaxRetargetSeconds));
            _retargetRemaining = _rng.RandfRange(minWait, maxWait);

            Vector2 baseDirection = SafeDirection(BaseWindDirection);
            float jitterRadians = _rng.RandfRange(-DirectionJitterDegrees, DirectionJitterDegrees) * Mathf.Pi / 180f;
            _targetDirection = baseDirection.Rotated(jitterRadians).Normalized();

            float minSpeed = Mathf.Max(0f, Mathf.Min(MinSpeed, MaxSpeed));
            float maxSpeed = Mathf.Max(minSpeed, Mathf.Max(MinSpeed, MaxSpeed));
            _targetSpeed = _rng.RandfRange(minSpeed, maxSpeed);
            _targetDensityBias = _rng.RandfRange(-DensityJitter, DensityJitter);
            _targetSunOpenBoost = 1f + _rng.RandfRange(-SunOpenJitter, SunOpenJitter);

            if (!immediate)
            {
                return;
            }

            _currentDirection = _targetDirection;
            _currentSpeed = _targetSpeed;
            _currentDensityBias = _targetDensityBias;
            _currentSunOpenBoost = _targetSunOpenBoost;
        }

        private void ApplyMaterialState()
        {
            _material.SetShaderParameter("flow_offset", _flowOffset);
            _material.SetShaderParameter("density_bias", _currentDensityBias);
            _material.SetShaderParameter("sun_open_boost", _currentSunOpenBoost);
        }

        private static Vector2 SafeDirection(Vector2 value)
        {
            return value.LengthSquared() > 0.0001f ? value.Normalized() : Vector2.Right;
        }
    }
}
