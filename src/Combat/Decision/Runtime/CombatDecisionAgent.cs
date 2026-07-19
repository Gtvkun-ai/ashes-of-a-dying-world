using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Profiles;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Xương sống Decision Core gắn lên actor.
    /// Bản foundation chỉ chạy shadow trace, cố ý chưa phát lệnh movement/action để rollout an toàn.
    /// </summary>
    public partial class CombatDecisionAgent : Node
    {
        [Signal] public delegate void DecisionEvaluatedEventHandler(string summary);

        [ExportGroup("Rollout Safety")]
        [Export] public bool Enabled { get; set; } = true;
        [Export] public bool UseDecisionCore { get; set; } = false;
        [Export] public bool ShadowMode { get; set; } = true;

        [ExportGroup("Bindings")]
        [Export] public NodePath CharacterPath { get; set; } = new NodePath("..");
        [Export] public NodePath LeaderPath { get; set; } = new NodePath("");
        [Export] public NodePath LineOfSightRayPath { get; set; } = new NodePath("../LoSRay");

        [ExportGroup("Perception")]
        [Export] public float DecisionIntervalSeconds { get; set; } = 0.15f;
        [Export] public float EnemySearchRadius { get; set; } = 220f;
        [Export] public float ThreatDangerRange { get; set; } = 78f;
        [Export] public float LeaderDangerRadius { get; set; } = 86f;
        [Export(PropertyHint.Range, "-1,1,0.01")] public float ThreatFacingDot { get; set; } = 0.3f;

        [ExportGroup("Profiles")]
        [Export] public CombatClassProfile ClassProfile { get; set; }
        [Export] public CombatDoctrineProfile DoctrineProfile { get; set; }
        [Export] public CombatPersonalityProfile PersonalityProfile { get; set; }

        [ExportGroup("Debug")]
        [Export] public bool DebugLogging { get; set; } = false;
        [Export] public float DebugLogIntervalSeconds { get; set; } = 1f;
        [Export] public int TraceHistoryCapacity { get; set; } = 24;

        public DecisionTrace LastTrace { get; private set; }
        public CombatBlackboard Blackboard => _blackboard;
        public bool IsInitialized => _initialized;

        private readonly CombatBlackboard _blackboard = new();
        private CombatCharacter _self;
        private CombatCharacter _leader;
        private ICombatPerception _perception;
        private ITacticalEvaluator _evaluator;
        private float _decisionRemaining;
        private float _debugLogRemaining;
        private float _elapsedSeconds;
        private bool _initialized;
        private bool _executionWarningPrinted;

        public override void _Ready()
        {
            CallDeferred(nameof(Initialize));
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!Enabled)
            {
                return;
            }

            if (!_initialized)
            {
                Initialize();
                if (!_initialized)
                {
                    return;
                }
            }

            float dt = Mathf.Max(0f, (float)delta);
            _elapsedSeconds += dt;
            _decisionRemaining -= dt;
            _debugLogRemaining -= dt;
            _blackboard.Tick(dt);

            // Chỉ chạy khi đang shadow hoặc đã bật cờ rollout. Hai cờ cùng false nghĩa là tắt hẳn.
            if (!ShadowMode && !UseDecisionCore)
            {
                return;
            }

            if (_decisionRemaining > 0f)
            {
                return;
            }

            _decisionRemaining = Mathf.Max(0.05f, DecisionIntervalSeconds);
            RefreshLeaderIfNeeded();

            CombatSnapshot snapshot = _perception.BuildSnapshot(
                _self,
                _leader,
                null,
                _blackboard,
                _elapsedSeconds);
            CombatEmotionState emotion = BuildEmotion(snapshot);
            LastTrace = _evaluator.Evaluate(
                snapshot,
                _blackboard,
                null,
                ClassProfile,
                DoctrineProfile,
                PersonalityProfile,
                emotion);

            _blackboard.PushTrace(LastTrace, TraceHistoryCapacity);
            EmitSignal(SignalName.DecisionEvaluated, LastTrace?.Summary ?? string.Empty);

            if (DebugLogging && _debugLogRemaining <= 0f && LastTrace != null)
            {
                _debugLogRemaining = Mathf.Max(0.1f, DebugLogIntervalSeconds);
                string classId = ClassProfile?.ClassId ?? "unassigned";
                GD.Print($"[DecisionShadow:{_self.CombatantId}] class={classId} {LastTrace.ToCompactString()}");
            }

            if (UseDecisionCore && !ShadowMode && !_executionWarningPrinted)
            {
                _executionWarningPrinted = true;
                GD.PushWarning(
                    $"[CombatDecisionAgent:{_self.CombatantId}] Foundation chưa có Scheduler/Movement executor; "
                    + "agent chỉ đánh giá và không phát lệnh mechanics để tránh giành quyền HyouAI giữa chừng.");
            }
        }

        public void ResetDecisionRuntime()
        {
            LastTrace = null;
            _blackboard.Reset();
            _decisionRemaining = 0f;
            _debugLogRemaining = 0f;
            _elapsedSeconds = 0f;
            _executionWarningPrinted = false;
        }

        public string GetLastTraceSummary()
        {
            return LastTrace?.ToCompactString(5) ?? "Decision trace chưa có dữ liệu.";
        }

        private void Initialize()
        {
            if (_initialized || !IsInsideTree())
            {
                return;
            }

            _self = ResolveCharacter();
            if (_self == null)
            {
                GD.PrintErr("[CombatDecisionAgent] Không tìm thấy CombatCharacter từ CharacterPath.");
                return;
            }

            RayCast2D lineOfSightRay = ResolveOptionalNode<RayCast2D>(LineOfSightRayPath);
            var threatPredictor = new ThreatPredictor(ThreatDangerRange, ThreatFacingDot);
            _perception = new CombatPerception(
                GetTree(),
                lineOfSightRay,
                threatPredictor,
                EnemySearchRadius,
                LeaderDangerRadius);
            _evaluator = new TacticalEvaluator();
            _leader = ResolveLeader();
            _decisionRemaining = 0f;
            _debugLogRemaining = 0f;
            _initialized = true;
        }

        private CombatCharacter ResolveCharacter()
        {
            CombatCharacter configured = ResolveOptionalNode<CombatCharacter>(CharacterPath);
            return configured ?? GetParentOrNull<CombatCharacter>();
        }

        private CombatCharacter ResolveLeader()
        {
            CombatCharacter configured = ResolveOptionalNode<CombatCharacter>(LeaderPath);
            if (configured != null)
            {
                return configured;
            }

            foreach (Node node in GetTree().GetNodesInGroup("Player"))
            {
                if (node is CombatCharacter player && player.IsAlive)
                {
                    return player;
                }
            }

            return null;
        }

        private void RefreshLeaderIfNeeded()
        {
            if (_leader == null
                || !GodotObject.IsInstanceValid(_leader)
                || _leader.IsQueuedForDeletion()
                || !_leader.IsAlive)
            {
                _leader = ResolveLeader();
            }
        }

        private T ResolveOptionalNode<T>(NodePath path) where T : Node
        {
            string text = path.ToString();
            return string.IsNullOrWhiteSpace(text)
                ? null
                : GetNodeOrNull<T>(path);
        }

        private CombatEmotionState BuildEmotion(in CombatSnapshot snapshot)
        {
            CombatPersonalityProfile personality = PersonalityProfile ?? new CombatPersonalityProfile();
            float stress = Mathf.Clamp(
                snapshot.ThreatSeverity * (0.5f + 0.5f * personality.StressSensitivity),
                0f,
                1f);
            float confidence = Mathf.Clamp(personality.Confidence * (1f - stress * 0.55f), 0f, 1f);
            float protectiveness = Mathf.Clamp(
                personality.Protectiveness * (snapshot.LeaderThreatened ? 1f : 0.25f),
                0f,
                1f);
            return new CombatEmotionState(stress, confidence, protectiveness);
        }
    }
}
