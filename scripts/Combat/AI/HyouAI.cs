using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.AI;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.Combat.Visuals;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Entities.NPC
{
    /// <summary>
    /// Brain đồng đội theo state. AI chỉ phát intent cho CombatCharacter.
    ///
    /// Điểm khác biệt của bản này:
    /// - follow bằng orbit slot ngẫu nhiên quanh Player, không đóng đinh vào gót chân chéo;
    /// - combat tách hướng nhìn khỏi hướng di chuyển để backpedal/strafe đúng nghĩa;
    /// - khóa hướng action, hitbox không được gây damage phía sau;
    /// - quá gần mục tiêu phải lùi ra đủ xa theo hysteresis rồi mới được đánh lại.
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

        private const string CryomancerMovesetPath = "res://data/combat/movesets/hyou_cryomancer.tres";

        [ExportGroup("General")]
        [Export] public bool Enabled { get; set; } = true;
        [Export] public NodePath CharacterPath { get; set; } = new NodePath("..");
        [Export] public NodePath LeaderPath { get; set; } = new NodePath("");
        [Export] public NodePath CastVisualPath { get; set; } = new NodePath("../HyouCastVisual");
        [Export] public float EnemySearchRadius { get; set; } = 180f;
        [Export] public float EnemyRefreshInterval { get; set; } = 0.2f;

        [ExportGroup("Follow Orbit")]
        [Export] public float FollowRunDistance { get; set; } = 92f;
        [Export] public float FollowOrbitMinRadius { get; set; } = 46f;
        [Export] public float FollowOrbitMaxRadius { get; set; } = 72f;
        [Export] public float FollowOrbitRetargetMinSeconds { get; set; } = 3.5f;
        [Export] public float FollowOrbitRetargetMaxSeconds { get; set; } = 7.0f;
        [Export] public float FollowOrbitArriveRadius { get; set; } = 7f;
        [Export] public float FollowOrbitResumeRadius { get; set; } = 16f;
        [Export] public float LeaderSeparationEnterRadius { get; set; } = 30f;
        [Export] public float LeaderSeparationExitRadius { get; set; } = 40f;
        [Export] public float LeaderSeparationWeight { get; set; } = 2.8f;

        [ExportGroup("Combat Positioning")]
        [Export] public float AttackRange { get; set; } = 43f;
        [Export] public float PreferredAttackDistance { get; set; } = 37f;
        [Export] public float MinimumAttackDistance { get; set; } = 29f;
        [Export] public float RetreatEnterDistance { get; set; } = 31f;
        [Export] public float RetreatExitDistance { get; set; } = 38f;
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
        private HyouCastVisual _castVisual;
        private CompanionState _state = CompanionState.Follow;
        private float _attackCooldownRemaining;
        private float _refreshRemaining;
        private float _guardReactionRemaining;
        private bool _initialized;
        private bool _movingToFormation;
        private bool _escapingLeaderOverlap;
        private bool _escapingTargetOverlap;
        private bool _hasFollowOrbitSlot;
        private int _formationSideSign = 1;
        private float _followOrbitRetargetRemaining;
        private Vector2 _followOrbitOffset;
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
            _followOrbitRetargetRemaining -= dt;
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

            _formationSideSign = _rng.RandiRange(0, 1) == 0 ? -1 : 1;
            _castVisual = ResolveCastVisual();
            _leader = ResolveLeader();
            ChooseFollowOrbitSlot(false);
            RefreshAllyCollisionExceptions();
            InstallDefaultMageMoveset();
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
            _character.FaceDirection(_approachFacing);
            _character.SetBlocking(false);

            float retreatEnter = Mathf.Max(MinimumAttackDistance, RetreatEnterDistance);
            float retreatExit = Mathf.Max(retreatEnter + 2f, RetreatExitDistance);
            bool insideRetreatBand = approach.DirectDistance < retreatEnter
                || approach.ForwardDistance < retreatEnter;

            if (!_escapingTargetOverlap && insideRetreatBand)
            {
                _escapingTargetOverlap = true;
            }
            else if (_escapingTargetOverlap
                && approach.DirectDistance >= retreatExit
                && approach.ForwardDistance >= retreatExit * 0.85f)
            {
                _escapingTargetOverlap = false;
            }

            if (_escapingTargetOverlap)
            {
                _state = CompanionState.Reposition;

                // Backpedal thật: vận tốc đi ngược hướng mặt, còn sprite vẫn nhìn slime.
                // Thêm một ít actual-away để thoát ổn khi hai tâm gần như chồng nhau.
                Vector2 backward = -_approachFacing;
                Vector2 actualAway = CombatSteering.SafeAwayDirection(
                    _character.CombatCenter,
                    _target.CombatCenter,
                    backward);
                Vector2 move = backward * 1.65f + actualAway * 0.75f;

                // Nếu đang lệch làn, trộn correction ngang nhẹ; không chạy vòng ra sau target.
                Vector2 toSlot = approach.DesiredPosition - _character.CombatCenter;
                Vector2 lateral = toSlot - _approachFacing * toSlot.Dot(_approachFacing);
                if (lateral.LengthSquared() > 1f)
                {
                    move += lateral.Normalized() * 0.35f;
                }

                _character.SetMoveInput(move.Normalized(), false, true);
                return;
            }

            if (approach.CanAttack)
            {
                _state = CompanionState.Attack;
                _character.StopMoveInput();
                _character.FaceDirection(_approachFacing);
                if (_attackCooldownRemaining <= 0f && _character.RequestAttack())
                {
                    _attackCooldownRemaining = AttackCooldown;
                }
                return;
            }

            _state = approach.DirectDistance <= RepositionRange
                ? CompanionState.Reposition
                : CompanionState.Chase;

            Vector2 toDesiredSlot = approach.DesiredPosition - _character.CombatCenter;
            Vector2 moveDirection = toDesiredSlot.LengthSquared() > 1f
                ? toDesiredSlot.Normalized()
                : (approach.TooFar ? _approachFacing : -_approachFacing);

            bool wantsRun = approach.DirectDistance > AttackRange * 2f;
            // Cả lúc tiến, strafe và lùi đều giữ mắt vào mục tiêu.
            _character.SetMoveInput(moveDirection, wantsRun, true);
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
            Vector2 threatPosition = _guardThreat?.CombatCenter ?? _character.CombatCenter + _approachFacing;
            _character.FaceToward(threatPosition);
            Vector2 fallback = -(_guardThreat?.FacingDirection ?? _approachFacing);
            Vector2 away = CombatSteering.SafeAwayDirection(
                _character.CombatCenter,
                threatPosition,
                fallback);
            _character.SetMoveInput(away, false, true);
        }

        private void RunFollow()
        {
            _state = CompanionState.Follow;
            _character.SetBlocking(false);
            if (_leader == null || !IsNodeUsable(_leader))
            {
                _leader = ResolveLeader();
                _hasFollowOrbitSlot = false;
            }

            if (_leader == null)
            {
                _character.StopMoveInput();
                return;
            }

            if (!_hasFollowOrbitSlot)
            {
                ChooseFollowOrbitSlot(false);
            }

            float leaderDistance = _character.GlobalPosition.DistanceTo(_leader.GlobalPosition);
            float separationEnter = Mathf.Max(1f, LeaderSeparationEnterRadius);
            float separationExit = Mathf.Max(separationEnter + 2f, LeaderSeparationExitRadius);

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
                    -_leader.FacingDirection);
                _character.SetMoveInput(away, false);
                return;
            }

            Vector2 orbitTarget = _leader.GlobalPosition + _followOrbitOffset;
            Vector2 toOrbit = orbitTarget - _character.GlobalPosition;
            float orbitDistance = toOrbit.Length();

            bool leaderMostlyIdle = _leader.Velocity.LengthSquared() < 36f;
            if (_followOrbitRetargetRemaining <= 0f
                && leaderMostlyIdle
                && orbitDistance <= FollowOrbitResumeRadius)
            {
                // Chỉ đổi chỗ khi đã gần slot cũ và Player không chạy. Như vậy vị trí là
                // ngẫu nhiên quanh Player nhưng không hóa thành vệ tinh tăng động.
                ChooseFollowOrbitSlot(true);
                orbitTarget = _leader.GlobalPosition + _followOrbitOffset;
                toOrbit = orbitTarget - _character.GlobalPosition;
                orbitDistance = toOrbit.Length();
            }

            if (_movingToFormation)
            {
                if (orbitDistance <= FollowOrbitArriveRadius)
                {
                    _movingToFormation = false;
                }
            }
            else if (orbitDistance >= FollowOrbitResumeRadius)
            {
                _movingToFormation = true;
            }

            if (!_movingToFormation)
            {
                _character.StopMoveInput();
                return;
            }

            Vector2 desiredDirection = toOrbit.LengthSquared() > 0.001f
                ? toOrbit.Normalized()
                : Vector2.Zero;

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
                -_leader.FacingDirection);

            bool wantsRun = orbitDistance > FollowRunDistance;
            _character.SetMoveInput(desiredDirection, wantsRun);
        }

        private void ChooseFollowOrbitSlot(bool preferCurrentSide)
        {
            if (_leader == null || _character == null)
            {
                return;
            }

            float minRadius = Mathf.Max(LeaderSeparationExitRadius + 4f, FollowOrbitMinRadius);
            float maxRadius = Mathf.Max(minRadius + 4f, FollowOrbitMaxRadius);
            float angle;

            Vector2 currentRelative = _character.GlobalPosition - _leader.GlobalPosition;
            if (preferCurrentSide && currentRelative.LengthSquared() > 4f)
            {
                // Giữ cùng bán cầu rồi jitter khá rộng. Tránh cảnh slot mới nhảy qua đúng
                // phía đối diện khiến Hyou lại cắt ngang người chơi.
                angle = currentRelative.Angle() + _rng.RandfRange(-1.15f, 1.15f);
            }
            else
            {
                angle = _rng.RandfRange(0f, Mathf.Tau);
            }

            float radius = _rng.RandfRange(minRadius, maxRadius);
            _followOrbitOffset = Vector2.Right.Rotated(angle) * radius;
            _followOrbitRetargetRemaining = _rng.RandfRange(
                Mathf.Max(0.5f, FollowOrbitRetargetMinSeconds),
                Mathf.Max(FollowOrbitRetargetMinSeconds + 0.5f, FollowOrbitRetargetMaxSeconds));
            _formationSideSign = _rng.RandiRange(0, 1) == 0 ? -1 : 1;
            _hasFollowOrbitSlot = true;
            _movingToFormation = true;
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
                bool returnedToFollow = _target != null && next == null;
                _target = next;
                _escapingTargetOverlap = false;
                _approachFacing = _target == null
                    ? _character.FacingDirection
                    : CombatSteering.ResolveStableCardinalFacing(
                        _target.CombatCenter - _character.CombatCenter,
                        _character.FacingDirection,
                        AxisSwitchBias);

                if (returnedToFollow)
                {
                    ChooseFollowOrbitSlot(true);
                }
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

                // Đồng minh không khóa cứng thân nhau để Hyou không chặn Player ở cửa hẹp.
                // Orbit slot + vùng separation chịu trách nhiệm giữ khoảng cách mềm.
                _character.AddCollisionExceptionWith(ally);
                ally.AddCollisionExceptionWith(_character);
            }
        }

        private HyouCastVisual ResolveCastVisual()
        {
            string path = CastVisualPath.ToString();
            return string.IsNullOrWhiteSpace(path)
                ? null
                : GetNodeOrNull<HyouCastVisual>(CastVisualPath);
        }

        private void InstallDefaultMageMoveset()
        {
            if (_character.DefaultMoveset != null)
            {
                return;
            }

            WeaponMovesetData moveset = GD.Load<WeaponMovesetData>(CryomancerMovesetPath);
            if (moveset != null)
            {
                _character.DefaultMoveset = moveset;
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

            // Không được dừng HyouCastVisual tại đây.
            // ReleaseCommands có thể chạy mỗi physics frame khi legacy HyouAI bị tắt
            // để Decision Core nắm quyền. VFX cast thuộc quyền sở hữu của ActionRunner:
            // ActionStarted mở hiệu ứng, ActionFinished mới đóng hiệu ứng. Nếu AI cũ gọi
            // StopCast ở đây, vòng phép vừa bật sẽ bị dập ngay trong cùng một frame.
        }

        private static bool IsNodeUsable(Node node)
        {
            return node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
        }
    }
}
