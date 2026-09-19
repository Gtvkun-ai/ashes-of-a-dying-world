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
        private const string RuntimeBuild = "v12.1-spring-readable-timing";
        private enum EnemyState
        {
            Wander,
            Chase,
            Attack,
            SpringCharge,
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
        [Export] public float TargetSwitchScoreMargin { get; set; } = 12f;
        [Export] public float CurrentTargetScoreBonus { get; set; } = 18f;
        [Export] public float TargetCommitScoreBonus { get; set; } = 22f;
        // Khi bị Slow, khoảng cách trở nên đắt hơn: slime ưu tiên hostile gần thay vì bò ngu tới target xa.
        [Export] public float SlowTargetProximityMultiplier { get; set; } = 1.35f;

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

        [ExportGroup("Spring Charge / Jump")]
        // Giữ tên resource PounceAction để scene cũ không gãy; từ V12 nó là action RELEASE của SpringJump.
        [Export] public CombatActionData PounceAction { get; set; }
        [Export] public CombatActionData SpringJumpAction { get; set; }
        [Export] public float SpringMinDistance { get; set; } = 34f;
        [Export] public float SpringMaxDistance { get; set; } = 118f;
        [Export] public float SpringMinJumpDistance { get; set; } = 28f;
        [Export] public float SpringMaxJumpDistance { get; set; } = 92f;
        [Export] public float SpringMinChargeSeconds { get; set; } = 0.16f;
        [Export] public float SpringMaxChargeSeconds { get; set; } = 0.78f;
        [Export(PropertyHint.Range, "0.5,0.95,0.05")] public float SpringTargetCommitFraction { get; set; } = 0.70f;
        // Retarget chỉ được chỉnh telegraph một lượng hữu hạn. Không cho target chạy qua lại kéo charge vô tận.
        [Export] public float SpringRetargetMinLeadSeconds { get; set; } = 0.06f;
        [Export] public float SpringRetargetMaxExtensionSeconds { get; set; } = 0.18f;
        [Export] public float SpringMinMotionMultiplier { get; set; } = 0.72f;
        [Export] public float SpringMaxMotionMultiplier { get; set; } = 2.10f;
        [Export] public float SpringMinImpactMultiplier { get; set; } = 0.45f;
        [Export] public float SpringMaxImpactMultiplier { get; set; } = 1.15f;
        [Export(PropertyHint.Range, "0.4,0.95,0.05")] public float SpringPounceChargeThreshold { get; set; } = 0.68f;
        [Export] public float SpringMobilityCooldown { get; set; } = 0.75f;
        [Export] public float SpringPounceCooldown { get; set; } = 1.45f;
        [Export] public float SpringDecisionInterval { get; set; } = 0.20f;
        [Export] public float PostSpringRecoverySeconds { get; set; } = 0.10f;
        [Export] public float PostPounceRecoverySeconds { get; set; } = 0.24f;

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
        private float _springCooldownRemaining;
        private float _springDecisionRemaining;
        private bool _springCharging;
        private float _springChargeElapsed;
        private float _springPlannedChargeSeconds;
        private float _springInitialPlannedChargeSeconds;
        private bool _springDirectionCommitted;
        private Vector2 _springCommittedDirection = Vector2.Down;
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
            _springCooldownRemaining = Mathf.Max(0f, _springCooldownRemaining - dt);
            _springDecisionRemaining = Mathf.Max(0f, _springDecisionRemaining - dt);
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
                    RunCombat(dt);
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

        private void RunCombat(float dt)
        {
            if (_springCharging)
            {
                UpdateSpringCharge(dt);
                return;
            }

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

            // SpringJump là combat mobility riêng, không phụ thuộc locomotion speed/Slow.
            // Charge ngắn để gap-close; charge sâu mới trở thành pounce có momentum lớn.
            if (TryStartSpringCharge(approach))
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

            if (action.ActionId == "slime_spring_jump")
            {
                _shoveHitConfirmed = false;
                _comboBuffered = false;
                if (completed)
                {
                    // Gap-close không phải attack finisher: chỉ cho một recovery rất ngắn rồi AI được shove/bite.
                    _postPounceRecoveryRemaining = Mathf.Max(0f, PostSpringRecoverySeconds);
                }
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

        private bool TryStartSpringCharge(CombatSteering.CardinalApproach approach)
        {
            if (PounceAction == null
                || SpringJumpAction == null
                || _springCharging
                || _springCooldownRemaining > 0f
                || _springDecisionRemaining > 0f
                || _attackCooldownRemaining > 0f
                || _character.Statuses?.IsFrozen == true
                || approach.DirectDistance < Mathf.Max(0f, SpringMinDistance)
                || approach.DirectDistance > Mathf.Max(SpringMinDistance, SpringMaxDistance))
            {
                return false;
            }

            _springDecisionRemaining = Mathf.Max(0.05f, SpringDecisionInterval);
            _springCharging = true;
            _springChargeElapsed = 0f;
            _springDirectionCommitted = false;
            _springCommittedDirection = _approachFacing;
            _escapingTargetOverlap = false;
            _state = EnemyState.SpringCharge;
            _character.StopMoveInput();
            PlanSpringChargeForCurrentTarget();
            ApplySpringChargeVisual();

            // Charge cue phát ngay lúc slime ép thân xuống. Action release không phát cue này lần hai.
            CombatFeedbackService.GetOrCreate(_character.GetTree())?
                .PlaySlimePresentationCue(_character, new StringName("slime_pounce_charge"));

            if (DebugTargeting)
            {
                GD.Print(
                    $"[SlimeBrain] SPRING begin target={_target?.CombatantId ?? "none"} "
                    + $"distance={approach.DirectDistance:0.0} planned={_springPlannedChargeSeconds:0.00}s "
                    + $"slowed={_character.Statuses?.IsSlowed == true}");
            }
            return true;
        }

        private void UpdateSpringCharge(float dt)
        {
            if (!_springCharging)
            {
                return;
            }

            if (_character.Statuses?.IsFrozen == true)
            {
                CancelSpringCharge("frozen");
                return;
            }

            if (!IsValidHostile(_target))
            {
                CancelSpringCharge("target_invalid");
                return;
            }

            _state = EnemyState.SpringCharge;
            _character.StopMoveInput();

            // Kế hoạch charge được khóa từ lúc bắt đầu. Target tự tiến/lùi không được âm thầm
            // viết lại telegraph; muốn phá khoảng cách thì player thật sự có counterplay.
            Vector2 toTarget = _target.CombatCenter - _character.CombatCenter;
            if (!_springDirectionCommitted && toTarget.LengthSquared() > 0.001f)
            {
                _approachFacing = CombatSteering.ResolveStableCardinalFacing(
                    toTarget,
                    _approachFacing,
                    AxisSwitchBias);
                _character.FaceDirection(_approachFacing);
            }
            else if (_springDirectionCommitted)
            {
                _character.FaceDirection(_springCommittedDirection);
            }

            // Quan trọng: charge tích bằng delta thật. Slow chỉ làm chậm locomotion thường,
            // không được nhân vào timer này; cơ thể slime vẫn có thể nén lò xo khi chân đang bị slow.
            float planned = Mathf.Max(0.05f, _springPlannedChargeSeconds);
            // Khi release tạm bị chặn bởi soft hitstun, slime chỉ GIỮ lực đã nén chứ không
            // bí mật tích thêm lực để từ gap-close biến thành full pounce.
            _springChargeElapsed = Mathf.Min(_springChargeElapsed + dt, planned);
            float progress = Mathf.Clamp(_springChargeElapsed / planned, 0f, 1f);
            if (!_springDirectionCommitted
                && progress >= Mathf.Clamp(SpringTargetCommitFraction, 0.5f, 0.95f))
            {
                Vector2 commitDirection = _target.CombatCenter - _character.CombatCenter;
                _springCommittedDirection = commitDirection.LengthSquared() > 0.001f
                    ? commitDirection.Normalized()
                    : _approachFacing;
                _springDirectionCommitted = true;
            }

            ApplySpringChargeVisual();

            if (_springChargeElapsed + 0.0001f >= _springPlannedChargeSeconds)
            {
                ReleaseSpringJump();
            }
        }

        private void PlanSpringChargeForCurrentTarget()
        {
            if (!_springCharging || !IsValidHostile(_target))
            {
                return;
            }

            float planned = ComputeSpringPlanSeconds(_target);
            _springInitialPlannedChargeSeconds = planned;
            _springPlannedChargeSeconds = planned;
        }

        private float ComputeSpringPlanSeconds(CombatCharacter target)
        {
            float directDistance = _character.CombatCenter.DistanceTo(target.CombatCenter);
            float desiredJumpDistance = Mathf.Max(0f, directDistance - Mathf.Max(0f, PreferredAttackDistance));
            float charge01 = ComputeRequiredCharge01(desiredJumpDistance);
            return Mathf.Lerp(
                Mathf.Max(0.05f, SpringMinChargeSeconds),
                Mathf.Max(SpringMinChargeSeconds, SpringMaxChargeSeconds),
                charge01);
        }

        private void AdjustSpringPlanAfterRetarget()
        {
            if (!_springCharging || !IsValidHostile(_target))
            {
                return;
            }

            float minCharge = Mathf.Max(0.05f, SpringMinChargeSeconds);
            float maxCharge = Mathf.Max(minCharge + 0.01f, SpringMaxChargeSeconds);
            float desired = Mathf.Clamp(ComputeSpringPlanSeconds(_target), minCharge, maxCharge);

            // Retarget gần hơn có thể búng sớm, nhưng vẫn phải cho player một nhịp đọc hướng mới.
            float earliestRelease = Mathf.Clamp(
                _springChargeElapsed + Mathf.Max(0.02f, SpringRetargetMinLeadSeconds),
                minCharge,
                maxCharge);

            // Tổng phần được kéo dài luôn tính từ kế hoạch BAN ĐẦU, không cộng dồn qua mỗi lần
            // đổi target. Nhờ vậy utility AI vẫn khôn nhưng telegraph không co giãn vô hạn.
            float extensionCeiling = Mathf.Clamp(
                _springInitialPlannedChargeSeconds + Mathf.Max(0f, SpringRetargetMaxExtensionSeconds),
                minCharge,
                maxCharge);
            float latestRelease = Mathf.Max(earliestRelease, extensionCeiling);

            _springPlannedChargeSeconds = Mathf.Clamp(desired, earliestRelease, latestRelease);
        }

        private float ComputeRequiredCharge01(float desiredJumpDistance)
        {
            float minJump = Mathf.Max(1f, SpringMinJumpDistance);
            float maxJump = Mathf.Max(minJump + 1f, SpringMaxJumpDistance);
            float distance01 = Mathf.Clamp((desiredJumpDistance - minJump) / (maxJump - minJump), 0f, 1f);

            // Jump distance dùng curve charge^2: charge ngắn là mobility nhỏ,
            // còn muốn bật rất xa phải chấp nhận anticipation lâu hơn và dễ bị đọc hơn.
            return Mathf.Sqrt(distance01);
        }

        private float ComputeSpringMotionMultiplier(float charge01)
        {
            charge01 = Mathf.Clamp(charge01, 0f, 1f);
            float springCurve = charge01 * charge01;
            return Mathf.Lerp(
                Mathf.Max(0f, SpringMinMotionMultiplier),
                Mathf.Max(SpringMinMotionMultiplier, SpringMaxMotionMultiplier),
                springCurve);
        }

        private float ComputeSpringImpactMultiplier(float charge01)
        {
            charge01 = Mathf.Clamp(charge01, 0f, 1f);
            float springCurve = charge01 * charge01;
            return Mathf.Lerp(
                Mathf.Max(0f, SpringMinImpactMultiplier),
                Mathf.Max(SpringMinImpactMultiplier, SpringMaxImpactMultiplier),
                springCurve);
        }

        private float GetSpringCharge01()
        {
            float minCharge = Mathf.Max(0.05f, SpringMinChargeSeconds);
            float maxCharge = Mathf.Max(minCharge + 0.01f, SpringMaxChargeSeconds);
            return Mathf.Clamp((_springChargeElapsed - minCharge) / (maxCharge - minCharge), 0f, 1f);
        }

        private bool CanRetargetDuringSpringCharge()
        {
            if (!_springCharging)
            {
                return true;
            }

            float planned = Mathf.Max(0.05f, _springPlannedChargeSeconds);
            float progress = Mathf.Clamp(_springChargeElapsed / planned, 0f, 1f);
            return progress < Mathf.Clamp(SpringTargetCommitFraction, 0.5f, 0.95f);
        }

        private void ApplySpringChargeVisual()
        {
            AnimatedSprite2D body = _character?.BodySprite;
            if (body?.SpriteFrames == null)
            {
                return;
            }

            string animation = $"pounce_{_character.FacingCardinal}";
            if (!body.SpriteFrames.HasAnimation(animation))
            {
                return;
            }

            // Frame 1 là pose nén sâu của sheet pounce. Giữ frame này thay vì chạy animation
            // giúp player đọc được slime đang "lên dây cót" bao lâu trước khi release.
            body.Animation = animation;
            body.Stop();
            int frameCount = body.SpriteFrames.GetFrameCount(animation);
            body.Frame = Mathf.Clamp(GetSpringCharge01() > 0.08f ? 1 : 0, 0, Mathf.Max(0, frameCount - 1));
        }

        private void ReleaseSpringJump()
        {
            if (!_springCharging || !IsValidHostile(_target))
            {
                CancelSpringCharge("release_invalid");
                return;
            }

            // Soft Hitstun có thể xảy ra do Flinch. Nó không xóa năng lượng đã nén; slime chỉ giữ pose
            // thêm một nhịp và release ngay khi state cho phép attack trở lại. Hard CC đã cancel ở hook riêng.
            if (_character.StateMachine?.CanStartAttack != true)
            {
                ApplySpringChargeVisual();
                return;
            }

            float charge01 = GetSpringCharge01();
            float motionMultiplier = ComputeSpringMotionMultiplier(charge01);
            float impactMultiplier = ComputeSpringImpactMultiplier(charge01);
            bool offensivePounce = charge01 >= Mathf.Clamp(SpringPounceChargeThreshold, 0.4f, 0.95f);
            CombatActionData releaseAction = offensivePounce ? PounceAction : SpringJumpAction;
            Vector2 jumpDirection = _springDirectionCommitted
                ? _springCommittedDirection
                : (_target.CombatCenter - _character.CombatCenter);
            if (jumpDirection.LengthSquared() <= 0.001f)
            {
                jumpDirection = _approachFacing;
            }
            jumpDirection = jumpDirection.Normalized();

            CombatCharacter releaseTarget = _target;
            float chargedSeconds = _springChargeElapsed;
            float plannedSeconds = _springPlannedChargeSeconds;
            _springCharging = false;
            _springDirectionCommitted = false;
            _springCommittedDirection = _approachFacing;
            _springChargeElapsed = 0f;
            _springPlannedChargeSeconds = 0f;
            _springInitialPlannedChargeSeconds = 0f;
            _state = EnemyState.Attack;
            _character.FaceDirection(jumpDirection);

            bool started = _character.Actions?.TryStartAbilityAction(
                releaseAction,
                jumpDirection,
                releaseTarget,
                1f,
                motionMultiplier,
                offensivePounce ? impactMultiplier : 1f) == true;
            if (!started)
            {
                _springDecisionRemaining = Mathf.Max(0.10f, SpringDecisionInterval);
                return;
            }

            _shoveHitConfirmed = false;
            _comboBuffered = false;
            _springCooldownRemaining = offensivePounce
                ? Mathf.Max(0.25f, SpringPounceCooldown)
                : Mathf.Max(0.15f, SpringMobilityCooldown);
            _attackCooldownRemaining = offensivePounce
                ? Mathf.Max(AttackCooldown, 0.4f)
                : Mathf.Max(0.10f, PostSpringRecoverySeconds);

            if (DebugTargeting)
            {
                GD.Print(
                    $"[SlimeBrain] SPRING release target={releaseTarget.CombatantId} "
                    + $"charge={charge01:0.00} time={chargedSeconds:0.00}s planned={plannedSeconds:0.00}s "
                    + $"mode={(offensivePounce ? "pounce" : "gap_close")} "
                    + $"motion={motionMultiplier:0.00} impact={impactMultiplier:0.00}");
            }
        }

        private void CancelSpringCharge(string reason)
        {
            if (!_springCharging)
            {
                return;
            }

            _springCharging = false;
            _springDirectionCommitted = false;
            _springCommittedDirection = _approachFacing;
            _springChargeElapsed = 0f;
            _springPlannedChargeSeconds = 0f;
            _springInitialPlannedChargeSeconds = 0f;
            _springDecisionRemaining = Mathf.Max(_springDecisionRemaining, 0.12f);

            if (DebugTargeting)
            {
                GD.Print($"[SlimeBrain] SPRING cancel reason={reason}");
            }
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
        /// Hard reaction mới được quyền phá SpringCharge. Flinch/Shove không gọi cancel,
        /// nên slime vẫn có thể giữ lực nén khi bị quấy nhẹ; Stagger+ mới thật sự làm mất thăng bằng.
        /// </summary>
        public void NotifyCombatReaction(ImpactReactionType reaction)
        {
            if ((int)reaction >= (int)ImpactReactionType.Stagger)
            {
                CancelSpringCharge($"hard_reaction_{reaction}");
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

            bool springAllowsSwitch = CanRetargetDuringSpringCharge();
            if (commitExpired && springAllowsSwitch && (challengerClearlyCloser || burstThreat))
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
            bool hasCurrent = IsValidHostile(_target);
            float scanRadius = hasCurrent
                ? Mathf.Max(TargetForgetRadius, Mathf.Max(SpringMaxDistance + 24f, AggroRadius))
                : Mathf.Max(AggroRadius, SpringMinDistance);
            float scanRadiusSquared = scanRadius * scanRadius;

            CombatCharacter best = null;
            float bestScore = float.NegativeInfinity;
            float bestDistance = float.PositiveInfinity;
            foreach (Node node in GetTree().GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter candidate || !IsValidHostile(candidate))
                {
                    continue;
                }

                float distanceSquared = _character.CombatCenter.DistanceSquaredTo(candidate.CombatCenter);
                if (distanceSquared > scanRadiusSquared)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSquared);
                float score = EvaluateTargetScore(candidate, distance);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                    bestDistance = distance;
                }
            }

            if (!hasCurrent)
            {
                // Không có combat target thì chỉ acquire trong AggroRadius thật; scan rộng hơn chỉ phục vụ switching.
                if (best != null && bestDistance <= Mathf.Max(1f, AggroRadius))
                {
                    SetTarget(best, "acquired_utility");
                }
                return;
            }

            float currentDistance = _character.CombatCenter.DistanceTo(_target.CombatCenter);
            float retentionRadius = Mathf.Max(AggroRadius * 1.2f, TargetForgetRadius);
            if (currentDistance > retentionRadius && best == null)
            {
                SetTarget(null, "lost_range");
                return;
            }

            if (best == null || best == _target || !CanRetargetDuringSpringCharge())
            {
                return;
            }

            float currentScore = EvaluateTargetScore(_target, currentDistance);
            float requiredMargin = Mathf.Max(0f, TargetSwitchScoreMargin);
            bool utilityWins = bestScore >= currentScore + requiredMargin;

            // Khi đang Slow, hostile ở sát mặt là cơ hội thực dụng. Cho phép override commit cũ
            // nếu hắn gần hơn rõ rệt, thay vì bắt slime tiếp tục bò tới một target xa.
            bool slowCloseOverride = _character.Statuses?.IsSlowed == true
                && bestDistance <= Mathf.Max(AttackRange + 12f, SpringMinDistance)
                && bestDistance + Mathf.Max(8f, TargetSwitchAdvantage * 0.65f) < currentDistance;
            bool commitExpired = _targetCommitRemaining <= 0f;

            bool springEarlyRetarget = _springCharging && CanRetargetDuringSpringCharge();
            if (((commitExpired || springEarlyRetarget) && utilityWins) || slowCloseOverride)
            {
                SetTarget(best, slowCloseOverride ? "slow_close_utility" : "utility_switch");
            }
        }

        private float EvaluateTargetScore(CombatCharacter candidate, float distance)
        {
            if (!IsValidHostile(candidate))
            {
                return float.NegativeInfinity;
            }

            float proximityRange = Mathf.Max(AggroRadius, SpringMaxDistance + 20f);
            float proximity01 = 1f - Mathf.Clamp(distance / Mathf.Max(1f, proximityRange), 0f, 1f);
            float proximityMultiplier = _character.Statuses?.IsSlowed == true
                ? Mathf.Max(1f, SlowTargetProximityMultiplier)
                : 1f;
            float score = proximity01 * 100f * proximityMultiplier;

            if (distance <= Mathf.Max(AttackRange, MinimumAttackDistance + 5f))
            {
                score += 26f;
            }
            else if (distance <= Mathf.Max(SpringMinDistance, SpringMaxDistance))
            {
                score += 12f;
            }

            if (candidate == _target)
            {
                score += Mathf.Max(0f, CurrentTargetScoreBonus);
                if (_targetCommitRemaining > 0f)
                {
                    float commit01 = Mathf.Clamp(
                        _targetCommitRemaining / Mathf.Max(0.05f, TargetCommitSeconds),
                        0f,
                        1f);
                    score += Mathf.Max(0f, TargetCommitScoreBonus) * commit01;
                }
                if (_provokedTargetRemaining > 0f)
                {
                    score += 8f;
                }
            }

            if (candidate == _threatChallenger && _threatChallengerWindowRemaining > 0f)
            {
                float threat01 = Mathf.Clamp(
                    _threatChallengerDamage / Mathf.Max(1f, ThreatBurstDamageThreshold),
                    0f,
                    1.5f);
                score += threat01 * 34f;
            }

            return score;
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

            CombatCharacter previousTarget = _target;
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

            if (_springCharging)
            {
                if (_target == null)
                {
                    CancelSpringCharge("target_lost");
                }
                else if (CanRetargetDuringSpringCharge())
                {
                    AdjustSpringPlanAfterRetarget();
                    if (DebugTargeting && previousTarget != null)
                    {
                        GD.Print(
                            $"[SlimeBrain] SPRING retarget from={previousTarget.CombatantId} "
                            + $"to={_target.CombatantId} planned={_springPlannedChargeSeconds:0.00}s");
                    }
                }
            }

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
