using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Execution;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Movement;
using AshesofaDyingWorld.Combat.Decision.Party;
using AshesofaDyingWorld.Combat.Decision.Profiles;
using AshesofaDyingWorld.Combat.Decision.Scheduling;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Decision agent hoàn chỉnh: Perception -> Director -> Evaluator -> Scheduler
    /// -> Spacing -> Movement -> Intent Executor. ShadowMode vẫn giữ để so trace mà không điều khiển.
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
        [Export] public NodePath NavigationAgentPath { get; set; } = new NodePath("../NavAgent");

        [ExportGroup("Perception")]
        [Export] public float DecisionIntervalSeconds { get; set; } = 0.15f;
        [Export] public float EnemySearchRadius { get; set; } = 240f;
        [Export] public float ThreatDangerRange { get; set; } = 78f;
        [Export] public float LeaderDangerRadius { get; set; } = 86f;
        [Export(PropertyHint.Range, "-1,1,0.01")] public float ThreatFacingDot { get; set; } = 0.3f;

        [ExportGroup("Scheduler")]
        [Export(PropertyHint.Range, "0,1,0.01")] public float SwitchScoreMargin { get; set; } = 0.14f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float EmergencyScoreMargin { get; set; } = 0.08f;
        [Export] public float MinimumSwitchCooldownSeconds { get; set; } = 0.12f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float EmergencyThreatThreshold { get; set; } = 0.38f;

        [ExportGroup("Movement")]
        [Export(PropertyHint.Layers2DPhysics)] public uint ObstacleCollisionMask { get; set; } = 1;
        [Export] public float MovementProbeDistance { get; set; } = 34f;
        [Export] public float MovementArrivalDistance { get; set; } = 6f;
        [Export] public float NavigationThreshold { get; set; } = 96f;

        [ExportGroup("Profiles")]
        [Export] public CombatClassProfile ClassProfile { get; set; }
        [Export] public CombatDoctrineProfile DoctrineProfile { get; set; }
        [Export] public CombatPersonalityProfile PersonalityProfile { get; set; }

        [ExportGroup("Debug")]
        [Export] public bool DebugLogging { get; set; } = false;
        [Export] public bool DebugFactorLogging { get; set; } = false;
        [Export] public float DebugLogIntervalSeconds { get; set; } = 1f;
        [Export] public int TraceHistoryCapacity { get; set; } = 24;

        public DecisionTrace LastTrace { get; private set; }
        public SchedulerDecision LastScheduledDecision { get; private set; }
        public MovementCommand LastMovementCommand { get; private set; }
        public CombatRoleAssignment? LastRoleAssignment { get; private set; }
        public CombatBlackboard Blackboard => _blackboard;
        public bool IsInitialized => _initialized;

        private readonly CombatBlackboard _blackboard = new();
        private CombatCharacter _self;
        private CombatCharacter _leader;
        private ICombatPerception _perception;
        private ITacticalEvaluator _evaluator;
        private ICombatActionScheduler _scheduler;
        private PartyTacticalDirector _director;
        private CombatSpacingController _spacing;
        private CombatMovementSolver _movement;
        private CombatIntentExecutor _executor;
        private float _decisionRemaining;
        private float _debugLogRemaining;
        private float _elapsedSeconds;
        private CombatSnapshot _lastSnapshot;
        private MovementCommand _plannedMovementCommand;
        private bool _hasSnapshot;
        private bool _initialized;

        public override void _Ready()
        {
            CallDeferred(nameof(Initialize));
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!Enabled)
            {
                ReleaseLiveCommands();
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
            _scheduler.Tick(dt);

            // Hai cờ cùng false nghĩa là tắt hẳn, không âm thầm chạy một nửa pipeline.
            if (!ShadowMode && !UseDecisionCore)
            {
                ReleaseLiveCommands();
                return;
            }

            bool liveControl = UseDecisionCore && !ShadowMode;
            bool evaluatedThisFrame = false;

            // Tactical cognition chạy ở nhịp thấp. Utility không cần tranh nhau từng physics frame.
            if (_decisionRemaining <= 0f)
            {
                _decisionRemaining = Mathf.Max(0.05f, DecisionIntervalSeconds);
                RefreshLeaderIfNeeded();
                LastRoleAssignment = _director.GetAssignment(_self, _leader, _blackboard);

                CombatSnapshot snapshot = _perception.BuildSnapshot(
                    _self,
                    _leader,
                    LastRoleAssignment,
                    _blackboard,
                    _elapsedSeconds);
                _lastSnapshot = snapshot;
                _hasSnapshot = true;

                CombatEmotionState emotion = BuildEmotion(snapshot);
                LastTrace = _evaluator.Evaluate(
                    snapshot,
                    _blackboard,
                    LastRoleAssignment,
                    ClassProfile,
                    DoctrineProfile,
                    PersonalityProfile,
                    emotion);
                LastScheduledDecision = _scheduler.Resolve(LastTrace, snapshot);

                // Chỉ scheduler được ghi CurrentIntent. Evaluator không còn tự giành quyền continuity.
                CombatIntent committed = LastScheduledDecision.CommittedIntent;
                _blackboard.CurrentIntent = committed;
                _blackboard.IntentLockRemaining = LastScheduledDecision.CommitmentRemaining;
                _blackboard.CurrentAnchor = committed.DesiredAnchor;
                _blackboard.PushTrace(LastTrace, TraceHistoryCapacity);

                CombatPose pose = _spacing.BuildPose(
                    snapshot,
                    committed,
                    LastRoleAssignment,
                    _blackboard);
                _plannedMovementCommand = _movement.Solve(
                    snapshot,
                    committed,
                    pose,
                    _blackboard);

                // Guard/cast là hành động rời rạc, chỉ thử khi scheduler vừa ra quyết định.
                if (liveControl)
                {
                    _executor.Execute(
                        committed,
                        snapshot,
                        _plannedMovementCommand,
                        _blackboard);
                }

                evaluatedThisFrame = true;
            }

            // Motor chạy mỗi physics frame. Đây là chỗ sửa hiện tượng đi 8 Hz rồi khựng,
            // trong khi utility vẫn giữ DecisionIntervalSeconds để không tốn query vô ích.
            if (liveControl && _hasSnapshot)
            {
                CombatIntent motorIntent = _blackboard.CurrentIntent
                    ?? CombatIntent.None(new StringName("motor_no_intent"));
                LastMovementCommand = _executor.TickMotor(
                    _leader,
                    _lastSnapshot.HasTarget,
                    motorIntent,
                    _plannedMovementCommand,
                    dt);
            }
            else
            {
                LastMovementCommand = _plannedMovementCommand;
            }

            if (!evaluatedThisFrame)
            {
                return;
            }

            string signalSummary = LastScheduledDecision.ToCompactString()
                + $" moveSlot={LastMovementCommand.DirectionSlot} moveScore={LastMovementCommand.Score:0.00} "
                + (LastTrace?.Summary ?? string.Empty);
            EmitSignal(SignalName.DecisionEvaluated, signalSummary);

            if (DebugLogging && _debugLogRemaining <= 0f && LastTrace != null)
            {
                _debugLogRemaining = Mathf.Max(0.1f, DebugLogIntervalSeconds);
                string classId = ClassProfile?.ClassId ?? "unassigned";
                string mode = ShadowMode ? "shadow" : "live";
                string motorMode = LastMovementCommand.DirectionSlot == -2
                    ? "follow"
                    : (LastMovementCommand.HasMovement ? "combat" : "stop");
                GD.Print(
                    $"[Decision:{mode}:{_self.CombatantId}] class={classId} state={_lastSnapshot.SelfState} "
                    + $"mp={_lastSnapshot.Mana} stamina={_lastSnapshot.Stamina} guard={_lastSnapshot.Guard} "
                    + LastScheduledDecision.ToCompactString() + " "
                    + $"motor={motorMode} move={LastMovementCommand.Direction} "
                    + $"slot={LastMovementCommand.DirectionSlot} anchor={LastMovementCommand.FacePosition} "
                    + LastTrace.ToCompactString());

                if (DebugFactorLogging)
                {
                    GD.Print($"[DecisionFactors:{_self.CombatantId}]\n{LastTrace.ToDetailedString()}");
                }
            }
        }

        public override void _ExitTree()
        {
            ReleaseLiveCommands();
        }

        public void ResetDecisionRuntime()
        {
            LastTrace = null;
            LastScheduledDecision = default;
            LastMovementCommand = default;
            LastRoleAssignment = null;
            _blackboard.Reset();
            _scheduler?.Reset();
            _lastSnapshot = default;
            _plannedMovementCommand = default;
            _hasSnapshot = false;
            _decisionRemaining = 0f;
            _debugLogRemaining = 0f;
            _elapsedSeconds = 0f;
            ReleaseLiveCommands();
        }

        public string GetLastTraceSummary()
        {
            return LastTrace == null
                ? "Decision trace chưa có dữ liệu."
                : LastScheduledDecision.ToCompactString()
                    + $" move={LastMovementCommand.Direction} "
                    + LastTrace.ToCompactString(5, 4);
        }

        public string GetLastDetailedTrace()
        {
            return LastTrace == null
                ? "Decision trace chưa có dữ liệu."
                : LastScheduledDecision.ToCompactString()
                    + $"\nMovement: direction={LastMovementCommand.Direction}, slot={LastMovementCommand.DirectionSlot}, score={LastMovementCommand.Score:0.00}"
                    + "\n" + LastTrace.ToDetailedString();
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
            NavigationAgent2D navigationAgent = ResolveOptionalNode<NavigationAgent2D>(NavigationAgentPath);
            var threatPredictor = new ThreatPredictor(ThreatDangerRange, ThreatFacingDot);
            _perception = new CombatPerception(
                GetTree(),
                lineOfSightRay,
                threatPredictor,
                EnemySearchRadius,
                LeaderDangerRadius);
            _evaluator = new TacticalEvaluator();
            _scheduler = new CombatActionScheduler(
                SwitchScoreMargin,
                EmergencyScoreMargin,
                MinimumSwitchCooldownSeconds,
                EmergencyThreatThreshold);
            _director = new PartyTacticalDirector(
                GetTree(),
                EnemySearchRadius,
                LeaderDangerRadius);
            _spacing = new CombatSpacingController();
            _movement = new CombatMovementSolver(
                _self,
                navigationAgent,
                ObstacleCollisionMask,
                MovementProbeDistance,
                MovementArrivalDistance,
                NavigationThreshold);
            _executor = new CombatIntentExecutor(_self, ClassProfile);
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

        private void ReleaseLiveCommands()
        {
            _executor?.ReleaseCommands();
        }
    }
}
