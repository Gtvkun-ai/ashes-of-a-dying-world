using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Runtime;

namespace AshesofaDyingWorld.Combat.AI
{
    /// <summary>
    /// AI slime tách khỏi actor: wander, aggro, leash, chase và attack bằng intent chung.
    /// Slime dùng cùng cardinal lane và hysteresis khoảng cách với companion, nếu không
    /// chính slime sẽ lao vào Hyou rồi cả hai dính thành một cục dù Hyou đã biết lùi.
    /// </summary>
    public partial class SlimeBrain : Node
    {
        private enum EnemyState
        {
            Wander,
            Chase,
            Attack,
            Return,
            Reposition
        }

        [ExportGroup("General")]
        [Export] public NodePath CharacterPath { get; set; } = new NodePath("..");
        [Export] public float AggroRadius { get; set; } = 105f;
        [Export] public float LeashRadius { get; set; } = 170f;
        [Export] public float TargetRefreshInterval { get; set; } = 0.2f;

        [ExportGroup("Combat Positioning")]
        [Export] public float AttackRange { get; set; } = 37f;
        [Export] public float PreferredAttackDistance { get; set; } = 31f;
        [Export] public float MinimumAttackDistance { get; set; } = 24f;
        [Export] public float TargetSeparationExitMargin { get; set; } = 7f;
        [Export] public float AttackLaneTolerance { get; set; } = 11f;
        [Export] public float AxisSwitchBias { get; set; } = 1.3f;
        [Export] public float AttackCooldown { get; set; } = 0.65f;

        [ExportGroup("Wander")]
        [Export] public float WanderRadius { get; set; } = 70f;
        [Export] public float WanderRetargetMin { get; set; } = 1.2f;
        [Export] public float WanderRetargetMax { get; set; } = 3.4f;
        [Export] public float StopDistance { get; set; } = 5f;

        private readonly RandomNumberGenerator _rng = new();
        private Slime1 _character;
        private CombatCharacter _target;
        private Vector2 _spawnPosition;
        private Vector2 _wanderTarget;
        private Vector2 _approachFacing = Vector2.Down;
        private EnemyState _state = EnemyState.Wander;
        private float _attackCooldownRemaining;
        private float _targetRefreshRemaining;
        private float _wanderRetargetRemaining;
        private bool _escapingTargetOverlap;

        public override void _Ready()
        {
            _rng.Randomize();
            CallDeferred(nameof(Initialize));
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_character == null || !_character.IsAlive)
            {
                ReleaseCommands();
                return;
            }

            float dt = (float)delta;
            _attackCooldownRemaining = Mathf.Max(0f, _attackCooldownRemaining - dt);
            _targetRefreshRemaining -= dt;
            _wanderRetargetRemaining -= dt;

            if (_targetRefreshRemaining <= 0f)
            {
                _targetRefreshRemaining = Mathf.Max(0.05f, TargetRefreshInterval);
                RefreshTarget();
            }

            if (_target != null && IsUsable(_target) && _target.IsAlive)
            {
                float fromSpawn = _target.GlobalPosition.DistanceTo(_spawnPosition);
                if (fromSpawn <= LeashRadius)
                {
                    RunCombat();
                    return;
                }

                _target = null;
                _escapingTargetOverlap = false;
            }

            RunReturnOrWander();
        }

        private void Initialize()
        {
            string path = CharacterPath.ToString();
            _character = !string.IsNullOrWhiteSpace(path)
                ? GetNodeOrNull<Slime1>(path)
                : GetParentOrNull<Slime1>();
            _character ??= GetParentOrNull<Slime1>();
            if (_character == null)
            {
                GD.PrintErr("[SlimeBrain] Không tìm thấy Slime1.");
                return;
            }

            _spawnPosition = _character.GlobalPosition;
            ChooseWanderTarget();
        }

        private void RunCombat()
        {
            if (_character.IsPerformingAttack)
            {
                _state = EnemyState.Attack;
                _character.StopMoveInput();
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

            if (_escapingTargetOverlap)
            {
                _state = EnemyState.Reposition;
                Vector2 away = CombatSteering.SafeAwayDirection(
                    _character.CombatCenter,
                    _target.CombatCenter,
                    -_approachFacing);
                Vector2 towardSlot = approach.DesiredPosition - _character.CombatCenter;
                Vector2 move = away * 1.6f;
                if (towardSlot.LengthSquared() > 0.001f)
                {
                    move += towardSlot.Normalized() * 0.45f;
                }

                _character.SetMoveInput(move.Normalized(), false, true);
                return;
            }

            if (approach.CanAttack)
            {
                _state = EnemyState.Attack;
                _character.StopMoveInput();
                if (_attackCooldownRemaining <= 0f && _character.RequestAttack())
                {
                    _attackCooldownRemaining = AttackCooldown;
                }
                return;
            }

            _state = EnemyState.Chase;
            Vector2 toSlot = approach.DesiredPosition - _character.CombatCenter;
            Vector2 moveDirection = toSlot.LengthSquared() > 1f
                ? toSlot.Normalized()
                : (approach.TooFar ? _approachFacing : -_approachFacing);
            _character.SetMoveInput(moveDirection, false, true);
        }

        private void RunReturnOrWander()
        {
            _escapingTargetOverlap = false;
            float distanceFromSpawn = _character.GlobalPosition.DistanceTo(_spawnPosition);
            if (distanceFromSpawn > WanderRadius * 1.15f)
            {
                _state = EnemyState.Return;
                Vector2 homeDirection = (_spawnPosition - _character.GlobalPosition).Normalized();
                _character.SetMoveInput(homeDirection, false);
                return;
            }

            _state = EnemyState.Wander;
            if (_wanderRetargetRemaining <= 0f
                || _character.GlobalPosition.DistanceTo(_wanderTarget) <= StopDistance)
            {
                ChooseWanderTarget();
            }

            Vector2 direction = _wanderTarget - _character.GlobalPosition;
            if (direction.Length() <= StopDistance)
            {
                _character.StopMoveInput();
            }
            else
            {
                _character.SetMoveInput(direction.Normalized(), false);
            }
        }

        private void RefreshTarget()
        {
            if (_target != null && IsUsable(_target) && _target.IsAlive
                && _character.CombatCenter.DistanceTo(_target.CombatCenter) <= AggroRadius * 1.2f)
            {
                return;
            }

            CombatCharacter nearest = null;
            float nearestDistanceSquared = AggroRadius * AggroRadius;
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
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            if (nearest != _target)
            {
                _target = nearest;
                _escapingTargetOverlap = false;
                _approachFacing = _target == null
                    ? _character.FacingDirection
                    : CombatSteering.ResolveStableCardinalFacing(
                        _target.CombatCenter - _character.CombatCenter,
                        _character.FacingDirection,
                        AxisSwitchBias);
            }
        }

        private void ChooseWanderTarget()
        {
            float angle = _rng.RandfRange(0f, Mathf.Tau);
            float radius = _rng.RandfRange(WanderRadius * 0.2f, WanderRadius);
            _wanderTarget = _spawnPosition + Vector2.Right.Rotated(angle) * radius;
            _wanderRetargetRemaining = _rng.RandfRange(WanderRetargetMin, WanderRetargetMax);
        }

        private void ReleaseCommands()
        {
            _character?.StopMoveInput();
            _character?.SetBlocking(false);
        }

        private static bool IsUsable(Node node)
        {
            return node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
        }
    }
}
