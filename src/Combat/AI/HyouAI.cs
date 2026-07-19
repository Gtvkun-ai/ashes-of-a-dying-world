using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.AI;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Entities.NPC
{
    /// <summary>
    /// Brain đồng đội theo state. AI chỉ phát intent cho CombatCharacter.
    ///
    /// Điểm khác biệt của bản này:
    /// - follow formation có vùng cấm cứng quanh Player;
    /// - tiếp cận combat theo làn ngang/dọc, không dùng khoảng cách tròn đơn thuần;
    /// - quá gần mục tiêu phải lùi ra đủ xa theo hysteresis rồi mới được đánh lại;
    /// - cooldown chỉ bắt đầu khi CombatActionRunner thật sự nhận đòn đánh.
    /// </summary>
    public partial class HyouAI : Node
    {
        private enum CompanionState
        {
            Follow,
            Chase,
            Attack,
            Guard,
            Reposition
        }

        private const string StarterWeaponPath = "res://assets/resources/data/weapons/sword/WoodSword.tres";

        [ExportGroup("General")]
        [Export] public bool Enabled { get; set; } = true;
        [Export] public NodePath CharacterPath { get; set; } = new NodePath("..");
        [Export] public NodePath LeaderPath { get; set; } = new NodePath("");
        [Export] public float EnemySearchRadius { get; set; } = 180f;
        [Export] public float EnemyRefreshInterval { get; set; } = 0.2f;

        [ExportGroup("Follow Formation")]
        [Export] public float FollowDistance { get; set; } = 58f;
        [Export] public float FormationBackOffset { get; set; } = 46f;
        [Export] public float FormationSideOffset { get; set; } = 24f;
        [Export] public float FormationArriveRadius { get; set; } = 6f;
        [Export] public float FormationResumeRadius { get; set; } = 14f;
        [Export] public float LeaderSeparationEnterRadius { get; set; } = 32f;
        [Export] public float LeaderSeparationExitRadius { get; set; } = 42f;
        [Export] public float LeaderSeparationWeight { get; set; } = 2.4f;

        [ExportGroup("Combat Positioning")]
        [Export] public float AttackRange { get; set; } = 41f;
        [Export] public float PreferredAttackDistance { get; set; } = 35f;
        [Export] public float MinimumAttackDistance { get; set; } = 27f;
        [Export] public float TargetSeparationExitMargin { get; set; } = 8f;
        [Export] public float AttackLaneTolerance { get; set; } = 10f;
        [Export] public float AxisSwitchBias { get; set; } = 1.35f;
        [Export] public float RepositionRange { get; set; } = 32f;
        [Export] public float AttackCooldown { get; set; } = 0.35f;

        [ExportGroup("Guard")]
        [Export] public float BlockRange { get; set; } = 48f;
        [Export] public float MinStaminaToBlock { get; set; } = 10f;
        [Export] public float ThreatFacingDot { get; set; } = 0.35f;
        [Export] public float ReactionDelayMin { get; set; } = 0.12f;
        [Export] public float ReactionDelayMax { get; set; } = 0.28f;

        private readonly RandomNumberGenerator _rng = new();
        private readonly HashSet<ulong> _allyCollisionExceptions = new();
        private NpcCharacter _character;
        private global::Player _leader;
        private CombatCharacter _target;
        private CombatCharacter _guardThreat;
        private CompanionState _state = CompanionState.Follow;
        private float _attackCooldownRemaining;
        private float _refreshRemaining;
        private float _guardReactionRemaining;
        private bool _initialized;
        private bool _movingToFormation;
        private bool _escapingLeaderOverlap;
        private bool _escapingTargetOverlap;
        private int _formationSideSign = 1;
        private Vector2 _approachFacing = Vector2.Down;

        public override void _Ready()
        {
            _rng.Randomize();
            CallDeferred(nameof(Initialize));
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!Enabled || !_initialized || _character == null || !_character.IsAlive)
            {
                ReleaseCommands();
                return;
            }

            float dt = (float)delta;
            _attackCooldownRemaining = Mathf.Max(0f, _attackCooldownRemaining - dt);
            _refreshRemaining -= dt;

            if (_refreshRemaining <= 0f)
            {
                _refreshRemaining = Mathf.Max(0.05f, EnemyRefreshInterval);
                RefreshAllyCollisionExceptions();
                RefreshTarget();
                RefreshGuardThreat();
            }

            if (_guardThreat != null && IsNodeUsable(_guardThreat))
            {
                if (_guardReactionRemaining > 0f)
                {
                    _guardReactionRemaining -= dt;
                    RunRepositionAwayFromThreat();
                    return;
                }

                RunGuard();
                return;
            }

            if (_target != null && IsNodeUsable(_target) && _target.IsAlive)
            {
                RunCombat();
                return;
            }

            _escapingTargetOverlap = false;
            RunFollow();
        }

        private void Initialize()
        {
            _character = ResolveCharacter();
            if (_character == null)
            {
                GD.PrintErr("[HyouAI] Không tìm thấy NpcCharacter.");
                return;
            }

            _formationSideSign = (_character.GetInstanceId() & 1UL) == 0UL ? 1 : -1;
            _leader = ResolveLeader();
            RefreshAllyCollisionExceptions();
            AutoEquipStarterWeapon();
            _initialized = true;
        }

        private void RunCombat()
        {
            if (_character.IsPerformingAttack)
            {
                _state = CompanionState.Attack;
                _character.StopMoveInput();
                _character.SetBlocking(false);
                return;
            }

            CombatSteering.CardinalApproach approach = CombatSteering.EvaluateCardinalApproach(
                _character.CombatCenter,
                _target.CombatCenter,
                _approachFacing,
                PreferredAttackDistance,
                MinimumAttackDistance,
                AttackRange,
                AttackLaneTolerance,
                AxisSwitchBias);

            _approachFacing = approach.Facing;
            _character.FaceToward(_character.CombatCenter + _approachFacing);
            _character.SetBlocking(false);

            float separationExit = Mathf.Max(
                MinimumAttackDistance + 1f,
                MinimumAttackDistance + TargetSeparationExitMargin);

            if (!_escapingTargetOverlap && approach.TooClose)
            {
                _escapingTargetOverlap = true;
            }
            else if (_escapingTargetOverlap && approach.DirectDistance >= separationExit)
            {
                _escapingTargetOverlap = false;
            }

            // Hysteresis bắt buộc: khi đã lọt sát slime, Hyou phải thoát hẳn ra ngoài
            // separationExit. Không được vừa nhích qua ngưỡng một pixel đã lao vào đánh lại.
            if (_escapingTargetOverlap)
            {
                _state = CompanionState.Reposition;
                Vector2 away = CombatSteering.SafeAwayDirection(
                    _character.CombatCenter,
                    _target.CombatCenter,
                    -_approachFacing);
                Vector2 towardSlot = approach.DesiredPosition - _character.CombatCenter;
                Vector2 move = away * 1.75f;
                if (towardSlot.LengthSquared() > 0.001f)
                {
                    move += towardSlot.Normalized() * 0.55f;
                }

                _character.SetMoveInput(move.Normalized(), false);
                return;
            }

            if (approach.CanAttack)
            {
                _state = CompanionState.Attack;
                _character.StopMoveInput();
                if (_attackCooldownRemaining <= 0f && _character.RequestAttack())
                {
                    _attackCooldownRemaining = AttackCooldown;
                }
                return;
            }

            _state = approach.DirectDistance <= RepositionRange
                ? CompanionState.Reposition
                : CompanionState.Chase;

            Vector2 toSlot = approach.DesiredPosition - _character.CombatCenter;
            Vector2 moveDirection = toSlot.LengthSquared() > 1f
                ? toSlot.Normalized()
                : (approach.TooFar ? _approachFacing : -_approachFacing);

            bool wantsRun = approach.DirectDistance > AttackRange * 2f;
            _character.SetMoveInput(moveDirection, wantsRun);
        }

        private void RunGuard()
        {
            float threatDistance = _character.CombatCenter.DistanceTo(_guardThreat.CombatCenter);
            if (threatDistance < MinimumAttackDistance)
            {
                RunRepositionAwayFromThreat();
                return;
            }

            _state = CompanionState.Guard;
            _character.StopMoveInput();
            _character.FaceToward(_guardThreat.CombatCenter);
            bool enoughStamina = _character.Stats == null
                || _character.Stats.CurrentStamina >= MinStaminaToBlock;
            _character.SetBlocking(enoughStamina);

            if (!IsThreatening(_guardThreat))
            {
                _guardThreat = null;
                _character.SetBlocking(false);
            }
        }

        private void RunRepositionAwayFromThreat()
        {
            _state = CompanionState.Reposition;
            _character.SetBlocking(false);
            Vector2 fallback = -(_guardThreat?.FacingDirection ?? _approachFacing);
            Vector2 away = CombatSteering.SafeAwayDirection(
                _character.CombatCenter,
                _guardThreat.CombatCenter,
                fallback);
            _character.SetMoveInput(away, false);
        }

        private void RunFollow()
        {
            _state = CompanionState.Follow;
            _character.SetBlocking(false);
            if (_leader == null || !IsNodeUsable(_leader))
            {
                _leader = ResolveLeader();
            }

            if (_leader == null)
            {
                _character.StopMoveInput();
                return;
            }

            Vector2 leaderFacing = _leader.FacingDirection;
            if (leaderFacing.LengthSquared() <= 0.001f)
            {
                leaderFacing = Vector2.Down;
            }

            float leaderDistance = _character.GlobalPosition.DistanceTo(_leader.GlobalPosition);
            float separationEnter = Mathf.Max(1f, LeaderSeparationEnterRadius);
            float separationExit = Mathf.Max(separationEnter + 1f, LeaderSeparationExitRadius);

            if (!_escapingLeaderOverlap && leaderDistance < separationEnter)
            {
                _escapingLeaderOverlap = true;
                _movingToFormation = true;
            }
            else if (_escapingLeaderOverlap && leaderDistance >= separationExit)
            {
                _escapingLeaderOverlap = false;
            }

            if (_escapingLeaderOverlap)
            {
                Vector2 away = CombatSteering.SafeAwayDirection(
                    _character.GlobalPosition,
                    _leader.GlobalPosition,
                    -leaderFacing);
                _character.SetMoveInput(away, false);
                return;
            }

            Vector2 side = new Vector2(-leaderFacing.Y, leaderFacing.X) * _formationSideSign;
            Vector2 formationTarget = _leader.GlobalPosition
                - leaderFacing * Mathf.Max(separationExit, FormationBackOffset)
                + side * FormationSideOffset;

            Vector2 toFormation = formationTarget - _character.GlobalPosition;
            float formationDistance = toFormation.Length();

            if (_movingToFormation)
            {
                if (formationDistance <= FormationArriveRadius)
                {
                    _movingToFormation = false;
                }
            }
            else if (formationDistance >= FormationResumeRadius)
            {
                _movingToFormation = true;
            }

            if (!_movingToFormation)
            {
                _character.StopMoveInput();
                return;
            }

            Vector2 desiredDirection = toFormation.LengthSquared() > 0.001f
                ? toFormation.Normalized()
                : Vector2.Zero;

            // Nếu leader vừa quay hướng làm formation slot nhảy sang phía bên kia,
            // bẻ đường sang tiếp tuyến để Hyou đi vòng thay vì cắt qua tâm Player.
            desiredDirection = CombatSteering.SteerAroundCircle(
                desiredDirection,
                _character.GlobalPosition,
                _leader.GlobalPosition,
                separationEnter,
                _formationSideSign);
            desiredDirection = CombatSteering.BlendSeparation(
                desiredDirection,
                _character.GlobalPosition,
                _leader.GlobalPosition,
                separationExit,
                LeaderSeparationWeight,
                -leaderFacing);

            bool wantsRun = formationDistance > FollowDistance;
            _character.SetMoveInput(desiredDirection, wantsRun);
        }

        private void RefreshTarget()
        {
            if (_target != null && IsNodeUsable(_target) && _target.IsAlive
                && _character.CombatCenter.DistanceTo(_target.CombatCenter) <= EnemySearchRadius * 1.25f)
            {
                return;
            }

            CombatCharacter next = FindNearestHostile(EnemySearchRadius);
            if (next != _target)
            {
                _target = next;
                _escapingTargetOverlap = false;
                _approachFacing = _target == null
                    ? _character.FacingDirection
                    : CombatSteering.ResolveStableCardinalFacing(
                        _target.CombatCenter - _character.CombatCenter,
                        _character.FacingDirection,
                        AxisSwitchBias);
            }
        }

        private void RefreshGuardThreat()
        {
            CombatCharacter threat = FindNearestHostile(BlockRange, requireThreatening: true);
            if (threat == _guardThreat)
            {
                return;
            }

            _guardThreat = threat;
            if (_guardThreat != null)
            {
                _guardReactionRemaining = _rng.RandfRange(ReactionDelayMin, ReactionDelayMax);
            }
        }

        private CombatCharacter FindNearestHostile(float radius, bool requireThreatening = false)
        {
            CombatCharacter best = null;
            float bestDistanceSquared = radius * radius;
            foreach (Node node in GetTree().GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter candidate || candidate == _character || !candidate.IsAlive)
                {
                    continue;
                }

                if (!FactionRules.CanDamage(_character.Faction, candidate.Faction))
                {
                    continue;
                }

                float distanceSquared = _character.CombatCenter.DistanceSquaredTo(candidate.CombatCenter);
                if (distanceSquared > bestDistanceSquared || (requireThreatening && !IsThreatening(candidate)))
                {
                    continue;
                }

                best = candidate;
                bestDistanceSquared = distanceSquared;
            }

            return best;
        }

        private bool IsThreatening(CombatCharacter candidate)
        {
            if (candidate == null || !candidate.IsAlive || candidate.StateMachine == null)
            {
                return false;
            }

            CombatStateId state = candidate.StateMachine.Current;
            if (state != CombatStateId.AttackStartup && state != CombatStateId.AttackActive)
            {
                return false;
            }

            Vector2 toCompanion = (_character.CombatCenter - candidate.CombatCenter).Normalized();
            return candidate.FacingDirection.Dot(toCompanion) >= ThreatFacingDot;
        }

        private NpcCharacter ResolveCharacter()
        {
            string path = CharacterPath.ToString();
            if (!string.IsNullOrWhiteSpace(path))
            {
                NpcCharacter fromPath = GetNodeOrNull<NpcCharacter>(path);
                if (fromPath != null)
                {
                    return fromPath;
                }
            }

            return GetParentOrNull<NpcCharacter>();
        }

        private global::Player ResolveLeader()
        {
            string path = LeaderPath.ToString();
            if (!string.IsNullOrWhiteSpace(path))
            {
                global::Player configured = GetNodeOrNull<global::Player>(path);
                if (configured != null)
                {
                    return configured;
                }
            }

            foreach (Node node in GetTree().GetNodesInGroup("Player"))
            {
                if (node is global::Player player)
                {
                    return player;
                }
            }

            return null;
        }

        private void RefreshAllyCollisionExceptions()
        {
            foreach (Node node in GetTree().GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter ally
                    || ally == _character
                    || !FactionRules.AreAllies(_character.Faction, ally.Faction))
                {
                    continue;
                }

                ulong allyId = ally.GetInstanceId();
                if (!_allyCollisionExceptions.Add(allyId))
                {
                    continue;
                }

                // Đồng minh không khóa cứng thân nhau. Formation + separation chịu trách nhiệm
                // giữ khoảng cách, tránh cảnh Player bị Hyou chặn cửa trong không gian hẹp.
                _character.AddCollisionExceptionWith(ally);
                ally.AddCollisionExceptionWith(_character);
            }
        }

        private void AutoEquipStarterWeapon()
        {
            EquipmentItemData weapon = GD.Load<EquipmentItemData>(StarterWeaponPath);
            if (weapon != null && _character.Equipment?.GetEquippedItem(EquipmentSlot.MainHand) == null)
            {
                _character.Equipment.EquipItem(weapon);
            }
        }

        private void ReleaseCommands()
        {
            if (_character == null)
            {
                return;
            }

            _character.StopMoveInput();
            _character.SetBlocking(false);
        }

        private static bool IsNodeUsable(Node node)
        {
            return node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
        }
    }
}
