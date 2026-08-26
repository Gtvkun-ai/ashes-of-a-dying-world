using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Combat.Visuals
{
    /// <summary>
    /// Telegraph 2 giây của Frost Ward.
    ///
    /// V1.1: scale của cast circle được giải quyết ở WORLD SPACE và lấy field spec
    /// làm nguồn sự thật. Hyou root hiện scale x2, trong khi CombatField2D spawn ở
    /// world root; nếu dùng cùng local scale thì vòng cast sẽ to gấp đôi field.
    ///
    /// Giờ cast preview chỉ giữ "tỉ lệ charge" so với field cuối, vì vậy frame cuối
    /// của cast và frame đầu của field có cùng kích thước ngoài world.
    /// </summary>
    public partial class HyouFrostWardCastVisual : Node2D
    {
        private const string RuntimeBuild = "v1.1-frost-ward-world-scale-continuity";

        [ExportGroup("Binding")]
        [Export] public NodePath CharacterPath { get; set; } = new NodePath("..");
        [Export] public string CastActionId { get; set; } = "hyou_frost_ward";

        [ExportGroup("Visual")]
        [Export] public Texture2D CircleTexture { get; set; }
        [Export] public Vector2 GroundOffset { get; set; } = new Vector2(0f, 14f);
        [Export] public float CastDurationSeconds { get; set; } = 2f;

        [ExportSubgroup("World scale continuity")]
        [Export] public bool MatchSpawnedFieldWorldScale { get; set; } = true;
        [Export(PropertyHint.Range, "0.4,1.0,0.01")]
        public float StartRatioToField { get; set; } = 0.8333333f;
        [Export(PropertyHint.Range, "0.8,1.1,0.01")]
        public float EndRatioToField { get; set; } = 1f;

        // Backward-compatible fallback when an action has no SpawnField spec.
        [Export] public float StartScale { get; set; } = 1.35f;
        [Export] public float EndScale { get; set; } = 1.62f;

        [ExportSubgroup("Opacity")]
        [Export] public float StartAlpha { get; set; } = 0.18f;
        [Export] public float EndAlpha { get; set; } = 0.95f;
        [Export] public bool DebugLogging { get; set; } = false;

        private CombatCharacter _character;
        private Sprite2D _circle;
        private CombatActionData _playingAction;
        private float _elapsed;
        private bool _bound;
        private float _resolvedFieldWorldScale = 1f;

        public override void _Ready()
        {
            _circle = new Sprite2D
            {
                Name = "CastCircle",
                Texture = CircleTexture,
                Centered = true,
                Position = GroundOffset,
                ZIndex = -2,
                Visible = false
            };
            AddChild(_circle);
            Visible = true;
            SetProcess(true);
            TryBindActionRunner();

            if (DebugLogging)
            {
                GD.Print($"[HyouFrostWardCastVisual] READY build={RuntimeBuild} duration={CastDurationSeconds:0.00}s");
            }
        }

        public override void _ExitTree()
        {
            UnbindActionRunner();
        }

        public override void _Process(double delta)
        {
            if (!_bound)
            {
                TryBindActionRunner();
            }

            CombatActionData current = _character?.Actions?.CurrentAction;
            if (Matches(current))
            {
                if (_playingAction != current || !_circle.Visible)
                {
                    Begin(current);
                }

                _elapsed = Mathf.Min(
                    Mathf.Max(0.1f, CastDurationSeconds),
                    _elapsed + Mathf.Max(0f, (float)delta));
                UpdateVisual();
                return;
            }

            if (_circle?.Visible == true)
            {
                Stop("action_ended");
            }
        }

        private void Begin(CombatActionData action)
        {
            _playingAction = action;
            _elapsed = 0f;
            _resolvedFieldWorldScale = ResolveFieldWorldScale(action);

            if (_circle != null)
            {
                _circle.Visible = true;
            }

            UpdateVisual();

            if (DebugLogging)
            {
                GD.Print(
                    $"[HyouFrostWardCastVisual] CAST START action={action?.ActionId} " +
                    $"duration={CastDurationSeconds:0.00}s target_world_scale={_resolvedFieldWorldScale:0.###}");
            }
        }

        private void UpdateVisual()
        {
            if (_circle == null)
            {
                return;
            }

            float t = Mathf.Clamp(_elapsed / Mathf.Max(0.1f, CastDurationSeconds), 0f, 1f);
            float eased = t * t * (3f - 2f * t);

            // Nhịp charge rất nhẹ; pulse về đúng 0 ở t=1 nên frame cuối khớp field tuyệt đối.
            float pulseRatio = Mathf.Sin(t * Mathf.Pi * (2f + t * 3f))
                * (0.006f + 0.012f * t);

            if (MatchSpawnedFieldWorldScale && _resolvedFieldWorldScale > 0.001f)
            {
                float ratio = Mathf.Lerp(StartRatioToField, EndRatioToField, eased) + pulseRatio;
                float desiredWorldScale = _resolvedFieldWorldScale * Mathf.Max(0.05f, ratio);
                _circle.Scale = ResolveLocalScaleForWorldScale(desiredWorldScale);
            }
            else
            {
                float localScale = Mathf.Lerp(StartScale, EndScale, eased) + pulseRatio;
                _circle.Scale = Vector2.One * Mathf.Max(0.05f, localScale);
            }

            float alpha = Mathf.Lerp(StartAlpha, EndAlpha, eased);
            float brightness = Mathf.Lerp(0.78f, 1.10f, eased);
            _circle.Modulate = new Color(brightness, brightness, brightness, alpha);
        }

        private float ResolveFieldWorldScale(CombatActionData action)
        {
            if (!MatchSpawnedFieldWorldScale)
            {
                return 0f;
            }

            CombatFieldSpecData spec = action?.ResolveFieldSpec();
            if (spec != null && spec.VisualScale > 0.001f)
            {
                return spec.VisualScale;
            }

            // Preserve old appearance as a safe fallback:
            // EndScale was local under Hyou, so convert it to world scale.
            Vector2 hostScale = GlobalScale;
            float inherited = (Mathf.Abs(hostScale.X) + Mathf.Abs(hostScale.Y)) * 0.5f;
            return Mathf.Max(0.05f, EndScale * Mathf.Max(0.001f, inherited));
        }

        private Vector2 ResolveLocalScaleForWorldScale(float desiredWorldScale)
        {
            // The cast node inherits Hyou's x2 transform. Compensate per axis so the circle's
            // final WORLD scale equals CombatFieldSpec.VisualScale regardless of actor scale.
            Vector2 inherited = GlobalScale;
            float x = desiredWorldScale / Mathf.Max(0.001f, Mathf.Abs(inherited.X));
            float y = desiredWorldScale / Mathf.Max(0.001f, Mathf.Abs(inherited.Y));
            return new Vector2(x, y);
        }

        private void Stop(string reason)
        {
            _playingAction = null;
            _elapsed = 0f;
            if (_circle != null)
            {
                _circle.Visible = false;
            }

            if (DebugLogging)
            {
                GD.Print($"[HyouFrostWardCastVisual] CAST STOP reason={reason}");
            }
        }

        private bool TryBindActionRunner()
        {
            if (_bound || !IsInsideTree())
            {
                return _bound;
            }

            _character = GetNodeOrNull<CombatCharacter>(CharacterPath);
            if (_character?.Actions == null)
            {
                return false;
            }

            _character.Actions.ActionStarted += OnActionStarted;
            _character.Actions.ActionFinished += OnActionFinished;
            _bound = true;
            return true;
        }

        private void UnbindActionRunner()
        {
            if (!_bound || _character?.Actions == null)
            {
                return;
            }

            _character.Actions.ActionStarted -= OnActionStarted;
            _character.Actions.ActionFinished -= OnActionFinished;
            _bound = false;
        }

        private void OnActionStarted(CombatActionData action, Vector2 facing)
        {
            if (Matches(action))
            {
                Begin(action);
            }
        }

        private void OnActionFinished(CombatActionData action, bool completed)
        {
            if (Matches(action))
            {
                Stop(completed ? "completed" : "cancelled");
            }
        }

        private bool Matches(CombatActionData action)
        {
            return action != null
                && string.Equals(action.ActionId, CastActionId, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
