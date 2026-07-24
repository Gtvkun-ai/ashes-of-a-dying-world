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
        private const string RuntimeBuild = "v6-soft-pursuit";
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

        [ExportGroup("Threat / Targeting")]
        [Export] public float ProvokedTargetMemorySeconds { get; set; } = 4.5f;
        [Export] public float TargetSwitchAdvantage { get; set; } = 18f;
        [Export] public float RetaliationLeashMultiplier { get; set; } = 1.15f;
        [Export] public bool UseCombatSpawnLeash { get; set; } = false;
        [Export] public float TargetForgetRadius { get; set; } = 360f;
        [Export] public float ProvokedForgetRadius { get; set; } = 520f;
        [Export] public bool DebugTargeting { get; set; } = true;

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
        private float _provokedTargetRemaining;
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
            _provokedTargetRemaining = Mathf.Max(0f, _provokedTargetRemaining - dt);

            if (_targetRefreshRemaining <= 0f)
            {
                _targetRefreshRemaining = Mathf.Max(0.05f, TargetRefreshInterval);
                RefreshTarget();
            }

            if (_target != null && IsUsable(_target) && _target.IsAlive)
            {
                bool isProvoked = _provokedTargetRemaining > 0f;
                float targetDistance = _character.CombatCenter.DistanceTo(_target.CombatCenter);
                float forgetRadius = isProvoked
                    ? Mathf.Max(TargetForgetRadius, ProvokedForgetRadius)
                    : Mathf.Max(AggroRadius, TargetForgetRadius);

                bool blockedByOptionalSpawnLeash = UseCombatSpawnLeash
                    && !isProvoked
                    && _target.GlobalPosition.DistanceTo(_spawnPosition) > LeashRadius;

                if (!blockedByOptionalSpawnLeash && targetDistance <= forgetRadius)
                {
                    RunCombat();
                    return;
                }

                SetTarget(
                    null,
                    blockedByOptionalSpawnLeash ? "combat_leash_exceeded" : "target_too_far");
                _provokedTargetRemaining = 0f;
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
            if (DebugTargeting)
            {
                GD.Print(
                    $"[SlimeBrain] READY build={RuntimeBuild} slime={_character.CombatantId} "
                    + $"combat_spawn_leash={UseCombatSpawnLeash} forget={TargetForgetRadius:0.0} "
                    + $"provoked_forget={ProvokedForgetRadius:0.0}");
            }
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

        /// <summary>
        /// Khi slime bị đánh, kẻ gây sát thương phải trở thành mục tiêu ngay cả khi đứng ngoài
        /// AggroRadius thường. Đây là phản ứng trả đũa, không phải mở rộng tầm nhìn toàn cục.
        /// Nhờ vậy Hyou có thể đứng ở cự ly pháp sư nhưng không được bắn miễn phí.
        /// </summary>
        public void NotifyProvoked(CombatCharacter attacker, float hpDamage = 0f)
        {
            if (_character == null
                || !IsUsable(attacker)
                || !attacker.IsAlive
                || attacker == _character
                || !FactionRules.CanDamage(_character.Faction, attacker.Faction))
            {
                return;
            }

            // Không dùng spawn leash để từ chối kẻ vừa gây damage. Nếu projectile đã
            // chạm slime thì attacker là mối đe dọa thật, bất kể slime sinh ra ở đâu.
            _provokedTargetRemaining = Mathf.Max(0.1f, ProvokedTargetMemorySeconds);
            SetTarget(attacker, hpDamage > 0f ? "damaged" : "provoked");
        }

        private void RefreshTarget()
        {
            // Mục tiêu vừa gây sát thương có quyền ưu tiên ngắn hạn. Không để refresh định kỳ
            // lập tức kéo slime trở lại Player trong khi Hyou vừa bắn trúng nó.
            if (_provokedTargetRemaining > 0f
                && IsValidHostile(_target)
                && _character.CombatCenter.DistanceTo(_target.CombatCenter)
                    <= Mathf.Max(TargetForgetRadius, ProvokedForgetRadius))
            {
                return;
            }

            CombatCharacter nearest = null;
            float nearestDistanceSquared = AggroRadius * AggroRadius;
            foreach (Node node in GetTree().GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter candidate || !IsValidHostile(candidate))
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

            if (!IsValidHostile(_target))
            {
                SetTarget(nearest, nearest == null ? "no_hostile" : "acquired");
                return;
            }

            float currentDistance = _character.CombatCenter.DistanceTo(_target.CombatCenter);
            float retentionRadius = Mathf.Max(AggroRadius * 1.2f, TargetForgetRadius);
            if (nearest == null)
            {
                if (currentDistance > retentionRadius)
                {
                    SetTarget(null, "lost_range");
                }
                return;
            }

            if (nearest == _target)
            {
                return;
            }

            float nearestDistance = Mathf.Sqrt(nearestDistanceSquared);
            bool currentOutsideRetention = currentDistance > retentionRadius;
            bool challengerClearlyCloser = nearestDistance + Mathf.Max(0f, TargetSwitchAdvantage) < currentDistance;
            if (currentOutsideRetention || challengerClearlyCloser)
            {
                SetTarget(nearest, currentOutsideRetention ? "replacement" : "closer_hostile");
            }
        }

        private bool IsValidHostile(CombatCharacter candidate)
        {
            return candidate != null
                && IsUsable(candidate)
                && candidate != _character
                && candidate.IsAlive
                && FactionRules.CanDamage(_character.Faction, candidate.Faction);
        }

        private void SetTarget(CombatCharacter target, string reason)
        {
            if (_target == target)
            {
                return;
            }

            _target = target;
            _escapingTargetOverlap = false;
            _approachFacing = _target == null
                ? _character.FacingDirection
                : CombatSteering.ResolveStableCardinalFacing(
                    _target.CombatCenter - _character.CombatCenter,
                    _character.FacingDirection,
                    AxisSwitchBias);

            if (DebugTargeting)
            {
                string targetId = _target?.CombatantId ?? "none";
                float distance = _target == null
                    ? 0f
                    : _character.CombatCenter.DistanceTo(_target.CombatCenter);
                GD.Print($"[SlimeBrain] TARGET slime={_character.CombatantId} target={targetId} reason={reason} distance={distance:0.0}");
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
