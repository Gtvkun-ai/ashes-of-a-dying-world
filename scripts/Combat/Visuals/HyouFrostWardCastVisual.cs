using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Combat.Visuals
{
    /// <summary>
    /// Telegraph 2 giây của Frost Ward. Trong lúc cast chỉ hiện pháp trận dưới chân;
    /// crystal chỉ xuất hiện khi SpawnField hoàn tất cast.
    /// </summary>
    public partial class HyouFrostWardCastVisual : Node2D
    {
        private const string RuntimeBuild = "v1-frost-ward-cast-2s";

        [ExportGroup("Binding")]
        [Export] public NodePath CharacterPath { get; set; } = new NodePath("..");
        [Export] public string CastActionId { get; set; } = "hyou_frost_ward";

        [ExportGroup("Visual")]
        [Export] public Texture2D CircleTexture { get; set; }
        [Export] public Vector2 GroundOffset { get; set; } = new Vector2(0f, 14f);
        [Export] public float CastDurationSeconds { get; set; } = 2f;
        [Export] public float StartScale { get; set; } = 0.84f;
        [Export] public float EndScale { get; set; } = 1f;
        [Export] public float StartAlpha { get; set; } = 0.18f;
        [Export] public float EndAlpha { get; set; } = 0.95f;
        [Export] public bool DebugLogging { get; set; } = false;

        private CombatCharacter _character;
        private Sprite2D _circle;
        private CombatActionData _playingAction;
        private float _elapsed;
        private bool _bound;

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
            if (_circle != null)
            {
                _circle.Visible = true;
            }
            UpdateVisual();

            if (DebugLogging)
            {
                GD.Print($"[HyouFrostWardCastVisual] CAST START action={action?.ActionId} duration={CastDurationSeconds:0.00}s");
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
            float pulse = Mathf.Sin(t * Mathf.Pi * (2f + t * 3f)) * (0.015f + 0.025f * t);
            float scale = Mathf.Lerp(StartScale, EndScale, eased) + pulse;
            float alpha = Mathf.Lerp(StartAlpha, EndAlpha, eased);
            float brightness = Mathf.Lerp(0.78f, 1.12f, eased);

            _circle.Scale = Vector2.One * Mathf.Max(0.05f, scale);
            _circle.Modulate = new Color(brightness, brightness, brightness, alpha);
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
