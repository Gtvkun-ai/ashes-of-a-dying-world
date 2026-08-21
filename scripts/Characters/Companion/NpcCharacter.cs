using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Runtime;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Core.Skills;

namespace AshesofaDyingWorld.Entities.NPC
{
    public partial class NpcCharacter : CombatCharacter
    {
        [Signal] public delegate void CommandModeChangedEventHandler(int mode);

        [Export] public bool IsRecruitable { get; set; } = true;
        [Export] public bool StartRecruited { get; set; } = false;
        [Export] public bool UsePlayerInput { get; set; } = false;
        [Export] public CompanionCommandMode DefaultCommandMode { get; set; } = CompanionCommandMode.Follow;
        [Export] public float WanderRadius { get; set; } = 66f;
        [Export] public float WanderRetargetMinSeconds { get; set; } = 1.8f;
        [Export] public float WanderRetargetMaxSeconds { get; set; } = 4.2f;

        [ExportGroup("Manual Aim Assist")]
        [Export] public float ManualAimAssistRadius { get; set; } = 250f;
        [Export(PropertyHint.Range, "5,60,1")] public float ManualAimAssistConeDegrees { get; set; } = 42f;
        [Export(PropertyHint.Range, "0,0.65,0.05")] public float ManualAimAssistStrength { get; set; } = 0.38f;

        public CompanionCommandMode CommandMode { get; private set; } = CompanionCommandMode.Follow;
        public bool IsRecruited => _isRecruited;

        private static readonly string[] SkillSlotActions = { "skill_1", "skill_2", "skill_3", "skill_4" };
        private readonly RandomNumberGenerator _wanderRng = new();
        private bool _isRecruited;
        private Vector2 _wanderAnchor;
        private Vector2 _wanderTarget;
        private float _wanderRetargetRemaining;

        protected override void OnCombatReady()
        {
            Faction = CombatFaction.Companion;
            AddToGroup("Companion");
            CommandMode = DefaultCommandMode;
            _wanderRng.Randomize();
            _wanderAnchor = GlobalPosition;
            _wanderTarget = GlobalPosition;

            if (StartRecruited)
            {
                Recruit();
            }

            RefreshAutonomyState();
        }

        protected override void UpdateControlSource(float delta)
        {
            if (!IsAlive)
            {
                return;
            }

            if (UsePlayerInput)
            {
                UpdateManualInput();
                return;
            }

            if (CommandMode == CompanionCommandMode.Stay)
            {
                StopMoveInput();
                SetBlocking(false);
                return;
            }

            if (CommandMode == CompanionCommandMode.Wander)
            {
                UpdateWander(delta);
            }
        }

        public void Recruit()
        {
            if (!IsRecruitable || _isRecruited || Stats == null)
            {
                return;
            }

            PlayerManager.GetOrCreate(GetTree())?.RegisterMember(Stats);
            _isRecruited = true;
        }

        public void SetPlayerControlled(bool controlled)
        {
            if (UsePlayerInput == controlled)
            {
                RefreshAutonomyState();
                return;
            }

            UsePlayerInput = controlled;
            StopMoveInput();
            SetBlocking(false);
            RefreshAutonomyState();
        }

        public void SetCommandMode(CompanionCommandMode mode)
        {
            if (CommandMode == mode)
            {
                return;
            }

            CommandMode = mode;
            StopMoveInput();
            SetBlocking(false);

            if (mode == CompanionCommandMode.Wander)
            {
                _wanderAnchor = GlobalPosition;
                ChooseWanderTarget();
            }

            RefreshAutonomyState();
            EmitSignal(SignalName.CommandModeChanged, (int)mode);
        }

        private void UpdateManualInput()
        {
            bool wantsBlock = Input.IsKeyPressed(Key.X)
                || (InputMap.HasAction("block") && Input.IsActionPressed("block"));
            SetBlocking(wantsBlock);

            Vector2 inputDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
            bool wantsRun = Input.IsKeyPressed(Key.Shift)
                || (InputMap.HasAction("run") && Input.IsActionPressed("run"));
            SetMoveInput(inputDirection, wantsRun);

            if (InputMap.HasAction("attack") && Input.IsActionJustPressed("attack"))
            {
                RequestAttack();
            }

            var collection = SkillCollectionResolver.Resolve(Stats);
            for (int slot = 0; slot < SkillSlotActions.Length; slot++)
            {
                string actionName = SkillSlotActions[slot];
                if (!InputMap.HasAction(actionName) || !Input.IsActionJustPressed(actionName))
                {
                    continue;
                }

                var skill = collection?.GetEquippedSkill(slot);
                if (skill != null)
                {
                    Vector2 assistedAim = ResolveManualAimAssist(FacingDirection);
                    Abilities?.TryActivate(skill, assistedAim);
                }
            }
        }


        private Vector2 ResolveManualAimAssist(Vector2 requestedDirection)
        {
            Vector2 baseDirection = requestedDirection.LengthSquared() > 0.001f
                ? requestedDirection.Normalized()
                : Vector2.Down;

            CombatCharacter target = FindManualAimAssistTarget(baseDirection);
            if (target == null)
            {
                return baseDirection;
            }

            Vector2 towardTarget = target.CombatCenter - CombatCenter;
            if (towardTarget.LengthSquared() <= 0.001f)
            {
                return baseDirection;
            }

            // Chỉ bẻ nhẹ quỹ đạo lúc cast. Không truyền aimTarget sang projectile để tránh
            // biến assist thành auto-lock/predictive homing hoàn chỉnh.
            float strength = Mathf.Clamp(ManualAimAssistStrength, 0f, 0.65f);
            return baseDirection.Lerp(towardTarget.Normalized(), strength).Normalized();
        }

        private CombatCharacter FindManualAimAssistTarget(Vector2 baseDirection)
        {
            float radius = Mathf.Max(24f, ManualAimAssistRadius);
            float minimumDot = Mathf.Cos(Mathf.DegToRad(Mathf.Clamp(ManualAimAssistConeDegrees, 5f, 60f)));
            CombatCharacter best = null;
            float bestScore = float.PositiveInfinity;

            foreach (Node node in GetTree().GetNodesInGroup("Combatant"))
            {
                CombatCharacter candidate = node as CombatCharacter;
                if (candidate == null
                    || candidate == this
                    || !candidate.IsAlive
                    || !FactionRules.IsHostile(Faction, candidate.Faction))
                {
                    continue;
                }

                Vector2 toCandidate = candidate.CombatCenter - CombatCenter;
                float distance = toCandidate.Length();
                if (distance <= 0.001f || distance > radius)
                {
                    continue;
                }

                float dot = baseDirection.Dot(toCandidate / distance);
                if (dot < minimumDot)
                {
                    continue;
                }

                // Góc quan trọng hơn khoảng cách: aim assist chỉ bắt mục tiêu người chơi đang
                // thật sự hướng tới, không giật viên đạn sang con slime gần nhưng lệch hẳn bên.
                float angularPenalty = 1f - dot;
                float distancePenalty = distance / radius;
                float score = angularPenalty * 3.2f + distancePenalty * 0.35f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private void UpdateWander(float delta)
        {
            SetBlocking(false);
            _wanderRetargetRemaining -= Mathf.Max(0f, delta);
            Vector2 toTarget = _wanderTarget - GlobalPosition;
            if (_wanderRetargetRemaining <= 0f || toTarget.LengthSquared() <= 36f)
            {
                ChooseWanderTarget();
                toTarget = _wanderTarget - GlobalPosition;
            }

            if (toTarget.LengthSquared() <= 4f)
            {
                StopMoveInput();
                return;
            }

            SetMoveInput(toTarget.Normalized(), false);
        }

        private void ChooseWanderTarget()
        {
            float radius = Mathf.Max(12f, WanderRadius);
            float angle = _wanderRng.RandfRange(0f, Mathf.Tau);
            float distance = _wanderRng.RandfRange(radius * 0.30f, radius);
            _wanderTarget = _wanderAnchor + Vector2.FromAngle(angle) * distance;
            _wanderRetargetRemaining = _wanderRng.RandfRange(
                Mathf.Max(0.5f, WanderRetargetMinSeconds),
                Mathf.Max(WanderRetargetMinSeconds + 0.1f, WanderRetargetMaxSeconds));
        }

        private void RefreshAutonomyState()
        {
            CombatDecisionAgent decisionAgent = GetNodeOrNull<CombatDecisionAgent>("CombatDecisionAgent");
            if (decisionAgent != null)
            {
                decisionAgent.Enabled = !UsePlayerInput
                    && (CommandMode == CompanionCommandMode.Follow || CommandMode == CompanionCommandMode.Protect);
            }

            HyouAI legacyAi = GetNodeOrNull<HyouAI>("HyouAI");
            if (legacyAi != null && UsePlayerInput)
            {
                legacyAi.Enabled = false;
            }
        }

        protected override void OnDefeated(CombatCharacter attacker)
        {
            if (_isRecruited && Stats != null)
            {
                PlayerManager.Instance?.UnregisterMember(Stats);
                _isRecruited = false;
            }
        }
    }
}
