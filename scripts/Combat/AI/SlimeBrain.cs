using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;
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
        private const string RuntimeBuild = "v11-stable-threat-reactions";
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
        // Sau khi vừa chốt target, slime giữ quyết định một nhịp để Hyou/Hikaru không cướp aggro qua lại mỗi hit.
        [Export] public float TargetCommitSeconds { get; set; } = 1.10f;
        // Challenger ở xa vẫn có thể kéo aggro nếu gây đủ damage trong một cửa sổ ngắn.
        [Export] public float ThreatBurstWindowSeconds { get; set; } = 1.60f;
        [Export] public float ThreatBurstDamageThreshold { get; set; } = 55f;
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

        [ExportGroup("Combo Pressure")]
        // Shove chỉ được nối bite sau hit-confirm thật. Chance giúp common slime nguy hiểm nhưng không máy móc 100%.
        [Export(PropertyHint.Range, "0,1,0.05")] public float ComboFollowupChance { get; set; } = 0.70f;
        [Export] public float ComboFollowupMaxDistance { get; set; } = 44f;
        [Export] public float ComboFollowupLaneTolerance { get; set; } = 20f;
        [Export] public float PostComboRepositionSeconds { get; set; } = 0.34f;

        [ExportGroup("Pounce Skill")]
        [Export] public CombatActionData PounceAction { get; set; }
        [Export(PropertyHint.Range, "0,1,0.05")] public float PounceChance { get; set; } = 0.55f;
        [Export] public float PounceMinDistance { get; set; } = 34f;
        [Export] public float PounceMaxDistance { get; set; } = 66f;
        [Export] public float PounceLaneTolerance { get; set; } = 18f;
        [Export] public float PounceCooldown { get; set; } = 3.4f;
        [Export] public float PounceDecisionInterval { get; set; } = 0.35f;
        [Export] public float PostPounceRecoverySeconds { get; set; } = 0.26f;

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
        private float _pounceCooldownRemaining;
        private float _pounceDecisionRemaining;
        private float _postComboRepositionRemaining;
        private float _postPounceRecoveryRemaining;
        private float _targetRefreshRemaining;
        private float _wanderRetargetRemaining;
        private float _provokedTargetRemaining;
        private float _targetCommitRemaining;
        private CombatCharacter _threatChallenger;
        private float _threatChallengerDamage;
        private float _threatChallengerWindowRemaining;
        private bool _escapingTargetOverlap;
        private bool _shoveHitConfirmed;
        private bool _comboBuffered;

        public override void _Ready()
        {
            _rng.Randomize();
            CallDeferred(nameof(Initialize));
        }

        public override void _ExitTree()
        {
            if (_character != null && GodotObject.IsInstanceValid(_character))
            {
                _character.HitDealt -= OnHitDealt;
                if (_character.Actions != null)
                {
                    _character.Actions.ActionFinished -= OnActionFinished;
                }
            }
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
            _pounceCooldownRemaining = Mathf.Max(0f, _pounceCooldownRemaining - dt);
            _pounceDecisionRemaining = Mathf.Max(0f, _pounceDecisionRemaining - dt);
            _postComboRepositionRemaining = Mathf.Max(0f, _postComboRepositionRemaining - dt);
            _postPounceRecoveryRemaining = Mathf.Max(0f, _postPounceRecoveryRemaining - dt);
            _targetRefreshRemaining -= dt;
            _wanderRetargetRemaining -= dt;
            _provokedTargetRemaining = Mathf.Max(0f, _provokedTargetRemaining - dt);
            _targetCommitRemaining = Mathf.Max(0f, _targetCommitRemaining - dt);
            _threatChallengerWindowRemaining = Mathf.Max(0f, _threatChallengerWindowRemaining - dt);
            if (_threatChallengerWindowRemaining <= 0f)
            {
                ResetThreatChallenger();
            }

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
            // HitDealt là nguồn hit-confirm thật; ActionFinished dùng để đóng nhịp combo/reposition.
            _character.HitDealt += OnHitDealt;
            if (_character.Actions != null)
            {
                _character.Actions.ActionFinished += OnActionFinished;
            }
            ChooseWanderTarget();
            if (DebugTargeting)
            {
                GD.Print(
                    $"[SlimeBrain] READY build={RuntimeBuild} slime={_character.CombatantId} instance={_character.GetInstanceId()} "
                    + $"combat_spawn_leash={UseCombatSpawnLeash} forget={TargetForgetRadius:0.0} "
                    + $"provoked_forget={ProvokedForgetRadius:0.0}");
            }
        }

        private void RunCombat()
        {
            if (_postPounceRecoveryRemaining > 0f)
            {
                // Pounce mạnh phải có cửa punish thật: sau khi đáp xong slime đứng khựng một nhịp,
                // không lập tức quay sang chase/ủi ở physics frame kế tiếp.
                _state = EnemyState.Reposition;
                _character.StopMoveInput();
                return;
            }

            if (_character.IsPerformingAttack)
            {
                _state = EnemyState.Attack;
                _character.StopMoveInput();
                // Chỉ có shove đã hit-confirm mới được thử buffer bite. Miss/block sạch sẽ dừng ở hit 1.
                TryBufferShoveFollowup();
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

            if (_postComboRepositionRemaining > 0f)
            {
                _state = EnemyState.Reposition;
                Vector2 awayAfterCombo = CombatSteering.SafeAwayDirection(
                    _character.CombatCenter,
                    _target.CombatCenter,
                    -_approachFacing);
                _character.SetMoveInput(awayAfterCombo, false, true);
                return;
            }

            float separationExit = Mathf.Max(
                MinimumAttackDistance + 1f,
                MinimumAttackDistance + TargetSeparationExitMargin);

            bool canPointBlankBite = approach.DirectDistance <= Mathf.Max(10f, MinimumAttackDistance + 4f)
                && approach.ForwardDistance > 0f
                && approach.LateralDistance <= AttackLaneTolerance + 8f;
            if (canPointBlankBite && _attackCooldownRemaining <= 0f)
            {
                _escapingTargetOverlap = false;
                _state = EnemyState.Attack;
                _character.StopMoveInput();
                if (_character.RequestAttack())
                {
                    BeginShoveDecision();
                    _attackCooldownRemaining = AttackCooldown;
                }
                return;
            }

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

            // Pounce chỉ được cân nhắc ở khoảng cách vừa: đủ xa để nhìn ra cú nhảy,
            // nhưng không spam random mỗi physics frame. Nếu roll fail, decision interval giữ AI ổn định.
            if (TryStartPounce(approach))
            {
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

        private void BeginShoveDecision()
        {
            _shoveHitConfirmed = false;
            _comboBuffered = false;
        }

        private void OnHitDealt(CombatCharacter target, CombatActionData action, HitResult result)
        {
            if (target != _target
                || action == null
                || action.ActionId != "slime_shove"
                || result == null
                || !result.Applied)
            {
                return;
            }

            // Block sạch không mở combo. Guard break vẫn được xem là hit-confirm hợp lệ.
            if (result.WasBlocked && !result.GuardBroken)
            {
                return;
            }

            _shoveHitConfirmed = true;
            // Buffer ngay trong hit callback để không phụ thuộc thứ tự _PhysicsProcess giữa
            // ActionRunner, hitbox và Brain ở frame active cuối cùng.
            TryBufferShoveFollowup();
        }

        private void OnActionFinished(CombatActionData action, bool completed)
        {
            if (action == null)
            {
                return;
            }

            if (action.ActionId == "slime_shove")
            {
                // Quyết định follow-up chỉ sống trong đúng action shove hiện tại.
                _shoveHitConfirmed = false;
                return;
            }

            if (action.ActionId == "slime_pounce")
            {
                _shoveHitConfirmed = false;
                _comboBuffered = false;
                if (completed)
                {
                    _postPounceRecoveryRemaining = Mathf.Max(0f, PostPounceRecoverySeconds);
                }
                return;
            }

            if (action.ActionId == "slime_bite")
            {
                _shoveHitConfirmed = false;
                _comboBuffered = false;
                if (completed)
                {
                    // Sau combo cho player một cửa phản công ngắn thay vì nối pounce ngay lập tức.
                    _postComboRepositionRemaining = Mathf.Max(0f, PostComboRepositionSeconds);
                }
            }
        }

        private void TryBufferShoveFollowup()
        {
            CombatActionData action = _character?.Actions?.CurrentAction;
            if (action == null
                || action.ActionId != "slime_shove"
                || !_shoveHitConfirmed
                || _comboBuffered
                || !IsValidHostile(_target))
            {
                return;
            }

            Vector2 toTarget = _target.CombatCenter - _character.CombatCenter;
            float directDistance = toTarget.Length();
            Vector2 facing = _character.FacingDirection;
            if (facing.LengthSquared() <= 0.001f)
            {
                facing = _approachFacing;
            }
            facing = facing.Normalized();
            Vector2 side = new Vector2(-facing.Y, facing.X);
            float forwardDistance = toTarget.Dot(facing);
            float lateralDistance = Mathf.Abs(toTarget.Dot(side));

            if (directDistance > Mathf.Max(1f, ComboFollowupMaxDistance)
                || forwardDistance <= 0f
                || lateralDistance > Mathf.Max(1f, ComboFollowupLaneTolerance))
            {
                return;
            }

            // Đánh dấu trước khi roll để không reroll 60 lần/giây trong cùng một active frame.
            _comboBuffered = true;
            if (_rng.Randf() > Mathf.Clamp(ComboFollowupChance, 0f, 1f))
            {
                return;
            }

            _character.RequestAttack();
        }

        private bool TryStartPounce(CombatSteering.CardinalApproach approach)
        {
            if (PounceAction == null
                || _pounceCooldownRemaining > 0f
                || _pounceDecisionRemaining > 0f
                || _attackCooldownRemaining > 0f
                || approach.DirectDistance < Mathf.Max(0f, PounceMinDistance)
                || approach.DirectDistance > Mathf.Max(PounceMinDistance, PounceMaxDistance)
                || approach.ForwardDistance <= 0f
                || approach.LateralDistance > Mathf.Max(1f, PounceLaneTolerance))
            {
                return false;
            }

            _pounceDecisionRemaining = Mathf.Max(0.1f, PounceDecisionInterval);
            if (_rng.Randf() > Mathf.Clamp(PounceChance, 0f, 1f))
            {
                return false;
            }

            _state = EnemyState.Attack;
            _escapingTargetOverlap = false;
            _character.StopMoveInput();
            _character.FaceDirection(_approachFacing);

            // Dùng ability-action entry point để pounce không chen vào light-combo index.
            // AimTarget được truyền vào để sau này telegraph/homing nhẹ có cùng nguồn target thật.
            bool started = _character.Actions?.TryStartAbilityAction(
                PounceAction,
                _approachFacing,
                _target,
                1f) == true;
            if (!started)
            {
                return false;
            }

            _shoveHitConfirmed = false;
            _comboBuffered = false;
            _pounceCooldownRemaining = Mathf.Max(0.25f, PounceCooldown);
            _attackCooldownRemaining = Mathf.Max(AttackCooldown, 0.4f);
            return true;
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
        /// Damage tạo threat nhưng không còn cướp target vô điều kiện.
        /// Slime chỉ đổi sang attacker mới khi: chưa có target, target cũ đã quá xa,
        /// challenger gần hơn rõ rệt, hoặc challenger gây đủ burst damage trong một cửa sổ ngắn.
        /// </summary>
        public void NotifyProvoked(CombatCharacter attacker, float hpDamage = 0f)
        {
            if (_character == null
                || !IsUsable(attacker)
                || !attacker.IsAlive
                || attacker == _character
                || !FactionRules.IsHostile(_character.Faction, attacker.Faction))
            {
                return;
            }

            if (!IsValidHostile(_target))
            {
                _provokedTargetRemaining = Mathf.Max(0.1f, ProvokedTargetMemorySeconds);
                SetTarget(attacker, hpDamage > 0f ? "damaged_acquire" : "provoked_acquire");
                return;
            }

            if (attacker == _target)
            {
                // Đánh đúng target hiện tại chỉ refresh memory; không tạo challenger giả.
                _provokedTargetRemaining = Mathf.Max(0.1f, ProvokedTargetMemorySeconds);
                ResetThreatChallenger();
                return;
            }

            // Field/status 0 HP damage không được cướp aggro khỏi target đang hợp lệ.
            // Nó vẫn có thể acquire ở nhánh phía trên nếu slime chưa có target nào.
            if (hpDamage <= 0f)
            {
                if (DebugTargeting)
                {
                    GD.Print(
                        $"[SlimeBrain] THREAT slime={_character.CombatantId} challenger={attacker.CombatantId} "
                        + "reason=zero_damage_ignored");
                }
                return;
            }

            float currentDistance = _character.CombatCenter.DistanceTo(_target.CombatCenter);
            float challengerDistance = _character.CombatCenter.DistanceTo(attacker.CombatCenter);
            float retentionRadius = Mathf.Max(AggroRadius * 1.2f, TargetForgetRadius);
            bool currentOutsideRetention = currentDistance > retentionRadius;

            if (currentOutsideRetention)
            {
                _provokedTargetRemaining = Mathf.Max(0.1f, ProvokedTargetMemorySeconds);
                SetTarget(attacker, "damaged_replacement");
                return;
            }

            if (_threatChallenger != attacker || _threatChallengerWindowRemaining <= 0f)
            {
                _threatChallenger = attacker;
                _threatChallengerDamage = 0f;
            }

            _threatChallengerDamage += Mathf.Max(0f, hpDamage);
            _threatChallengerWindowRemaining = Mathf.Max(0.1f, ThreatBurstWindowSeconds);

            if (DebugTargeting)
            {
                GD.Print(
                    $"[SlimeBrain] THREAT slime={_character.CombatantId} current={_target.CombatantId} "
                    + $"challenger={attacker.CombatantId} burst={_threatChallengerDamage:0.0}/{ThreatBurstDamageThreshold:0.0} "
                    + $"distance={challengerDistance:0.0}/{currentDistance:0.0} commit={_targetCommitRemaining:0.00}");
            }

            bool challengerClearlyCloser = challengerDistance + Mathf.Max(0f, TargetSwitchAdvantage) < currentDistance;
            bool burstThreat = _threatChallengerDamage >= Mathf.Max(0.1f, ThreatBurstDamageThreshold);
            bool commitExpired = _targetCommitRemaining <= 0f;

            if (commitExpired && (challengerClearlyCloser || burstThreat))
            {
                _provokedTargetRemaining = Mathf.Max(0.1f, ProvokedTargetMemorySeconds);
                SetTarget(attacker, challengerClearlyCloser ? "damaged_closer" : "damaged_burst");
            }
        }

        private void ResetThreatChallenger()
        {
            _threatChallenger = null;
            _threatChallengerDamage = 0f;
            _threatChallengerWindowRemaining = 0f;
        }

        private void RefreshTarget()
        {
            // Target đã được chốt bởi retaliation có quyền ưu tiên ngắn hạn. Refresh định kỳ
            // không được phá quyết định đó chỉ vì một hostile khác tình cờ đứng gần hơn vài pixel.
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
                && FactionRules.IsHostile(_character.Faction, candidate.Faction);
        }

        private void SetTarget(CombatCharacter target, string reason)
        {
            if (_target == target)
            {
                return;
            }

            _target = target;
            _escapingTargetOverlap = false;
            _targetCommitRemaining = _target == null
                ? 0f
                : Mathf.Max(0f, TargetCommitSeconds);
            ResetThreatChallenger();
            _approachFacing = _target == null
                ? _character.FacingDirection
                : CombatSteering.ResolveStableCardinalFacing(
                    _target.CombatCenter - _character.CombatCenter,
                    _character.FacingDirection,
                    AxisSwitchBias);

            if (DebugTargeting)
            {
                string targetId = _target?.CombatantId ?? "none";
                string targetInstance = _target == null ? "none" : _target.GetInstanceId().ToString();
                float distance = _target == null
                    ? 0f
                    : _character.CombatCenter.DistanceTo(_target.CombatCenter);
                GD.Print($"[SlimeBrain] TARGET slime={_character.CombatantId} instance={_character.GetInstanceId()} target={targetId} target_instance={targetInstance} reason={reason} distance={distance:0.0}");
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
