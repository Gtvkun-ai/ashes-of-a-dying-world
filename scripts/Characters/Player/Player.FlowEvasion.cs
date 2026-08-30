using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.Core.Data;

/// <summary>
/// Reactive movement của Hikaru khi một timed skill có AutoEvadeChancePercent.
///
/// Deep Flow V2 dùng relative mastery thay vì 20% phẳng:
/// - AutoEvadeChancePercent là baseline cho kèo ngang trình;
/// - level + DEX + INT + tốc độ di chuyển của Hikaru đối chiếu với target/attack;
/// - attack nhanh, startup ngắn, lunge/projectile nhanh sẽ khó đọc hơn;
/// - khi Hikaru out trình đủ xa, attack hợp lệ của mob thấp cấp có thể bị né 100%;
/// - proc thành công thì hit đó bị từ chối, action hiện tại nhường ưu tiên cho né;
/// - Hikaru lách ngang bằng external motion có collision;
/// - khi attacker vào recovery, nếu người chơi không tự ra lệnh di chuyển mới,
///   Hikaru tự áp lại một bước ngắn về khoảng cách trước cú né.
///
/// Không có VFX/eye ở đây. Presentation được để riêng cho phase sau.
/// </summary>
public partial class Player
{
    private readonly RandomNumberGenerator _flowRng = new();
    private bool _flowRngReady;
    private float _flowEvadeCooldownRemaining;
    private float _flowInvulnerableRemaining;
    private float _flowReengageDelayRemaining;
    private float _flowReengageWindowRemaining;
    private CombatCharacter _flowReengageTarget;
    private float _flowPreEvadeTargetDistance;
    private Vector2 _lastRawMoveInput = Vector2.Zero;

    private void InitializeFlowEvasion()
    {
        if (!_flowRngReady)
        {
            _flowRng.Randomize();
            _flowRngReady = true;
        }
        ResetFlowEvasionRuntime();
    }

    private void ResetFlowEvasionRuntime()
    {
        _flowEvadeCooldownRemaining = 0f;
        _flowInvulnerableRemaining = 0f;
        ClearFlowReengage();
        _lastRawMoveInput = Vector2.Zero;
    }

    private void UpdateFlowEvasion(float delta, Vector2 rawMoveInput)
    {
        float dt = Mathf.Max(0f, delta);
        _lastRawMoveInput = rawMoveInput.LengthSquared() <= 0.001f
            ? Vector2.Zero
            : rawMoveInput.Normalized();

        _flowEvadeCooldownRemaining = Mathf.Max(0f, _flowEvadeCooldownRemaining - dt);
        _flowInvulnerableRemaining = Mathf.Max(0f, _flowInvulnerableRemaining - dt);

        if (_flowReengageTarget == null)
        {
            return;
        }

        SkillData activeSkill = Abilities?.ActiveTimedSkill;
        if (!IsFlowEvasionSkill(activeSkill)
            || !IsAlive
            || Statuses?.IsFrozen == true
            || !IsUsableFlowTarget(_flowReengageTarget))
        {
            ClearFlowReengage();
            return;
        }

        _flowReengageDelayRemaining = Mathf.Max(0f, _flowReengageDelayRemaining - dt);
        _flowReengageWindowRemaining = Mathf.Max(0f, _flowReengageWindowRemaining - dt);
        if (_flowReengageWindowRemaining <= 0f)
        {
            ClearFlowReengage();
            return;
        }

        // Manual movement luôn có quyền cao hơn auto-return. Nếu người chơi đang tự xử lý
        // vị trí thì Flow không giành tay lái.
        if (_lastRawMoveInput != Vector2.Zero)
        {
            ClearFlowReengage();
            return;
        }

        if (_flowReengageDelayRemaining > 0f)
        {
            return;
        }

        bool attackerRecovered = _flowReengageTarget.StateMachine == null
            || _flowReengageTarget.StateMachine.Current == CombatStateId.AttackRecovery
            || !_flowReengageTarget.IsPerformingAttack;
        if (!attackerRecovered)
        {
            return;
        }

        Vector2 toTarget = _flowReengageTarget.CombatCenter - CombatCenter;
        float currentDistance = toTarget.Length();
        float distanceLostToEvade = currentDistance - _flowPreEvadeTargetDistance;
        if (toTarget.LengthSquared() > 0.001f && distanceLostToEvade > 3f)
        {
            float impulse = Mathf.Max(0f, activeSkill.AutoReengageImpulse);
            if (impulse > 0f)
            {
                FaceToward(_flowReengageTarget.CombatCenter);
                ApplyExternalForce(toTarget.Normalized() * impulse);
            }
        }

        ClearFlowReengage();
    }

    public override HitResult ReceiveHit(HitRequest request)
    {
        if (TryResolveDeepFlowEvasion(request))
        {
            return HitResult.Rejected(HitRejectionReason.Evaded);
        }

        return base.ReceiveHit(request);
    }

    private bool TryResolveDeepFlowEvasion(HitRequest request)
    {
        if (!IsEligibleFlowHit(request))
        {
            return false;
        }

        // Sau một proc, cửa sổ ngắn này bảo vệ chính chuyển động né khỏi multi-hit/tick
        // của cùng khoảnh khắc. Nó không reroll và không kéo dài duration skill.
        if (_flowInvulnerableRemaining > 0f)
        {
            return true;
        }

        SkillData activeSkill = Abilities?.ActiveTimedSkill;
        if (!IsFlowEvasionSkill(activeSkill) || _flowEvadeCooldownRemaining > 0f)
        {
            return false;
        }

        EnsureFlowRng();
        float chance = ComputeDeepFlowEvadeChance(activeSkill, request);
        if (_flowRng.Randf() >= chance)
        {
            return false;
        }

        BeginDeepFlowEvasion(request, activeSkill);
        return true;
    }

    /// <summary>
    /// Xác suất né theo tương quan thực lực.
    ///
    /// 20% trong resource là "kèo ngang trình", không phải hard cap.
    /// Vì vậy Lv99 + Deep Flow có thể đọc sạch attack của Slime Lv1, trong khi
    /// gặp boss ngang cấp thì vẫn quay về vùng 20-30% tùy tốc độ/DEX/INT.
    /// </summary>
    private float ComputeDeepFlowEvadeChance(SkillData skill, HitRequest request)
    {
        if (skill == null)
        {
            return 0f;
        }

        float baseline = Mathf.Clamp(skill.AutoEvadeChancePercent, 0f, 100f);
        if (!skill.AutoEvadeUseRelativeMastery)
        {
            return baseline / 100f;
        }

        CombatCharacter attacker = request?.Attacker;
        var selfStats = Stats;
        var enemyStats = attacker?.Stats;

        int selfLevel = Mathf.Max(1, selfStats?.CurrentLevel ?? 1);
        int enemyLevel = Mathf.Max(1, enemyStats?.CurrentLevel ?? 1);

        int selfDexterity = Mathf.Max(0, selfStats?.GetAttributeValue(AttributeType.Dexterity) ?? 0);
        int enemyDexterity = Mathf.Max(0, enemyStats?.GetAttributeValue(AttributeType.Dexterity) ?? 0);
        int selfIntelligence = Mathf.Max(0, selfStats?.GetAttributeValue(AttributeType.Intelligence) ?? 0);
        int enemyIntelligence = Mathf.Max(0, enemyStats?.GetAttributeValue(AttributeType.Intelligence) ?? 0);

        float selfMoveSpeed = ResolveFlowMovementSpeed(this);
        float enemyMoveSpeed = ResolveFlowMovementSpeed(attacker);
        float enemyAttackSpeed = Mathf.Max(0.25f, enemyStats?.AttackSpeed ?? 1f);

        CombatActionData action = request?.Action;
        float startupSeconds = Mathf.Max(0.04f, action?.StartupSeconds ?? 0.18f);
        float travelSpeed = ResolveAttackTravelSpeed(action);

        int levelDelta = selfLevel - enemyLevel;
        int dexterityDelta = selfDexterity - enemyDexterity;
        int intelligenceDelta = selfIntelligence - enemyIntelligence;

        float chancePercent = baseline;
        chancePercent += levelDelta * Mathf.Max(0f, skill.AutoEvadeLevelDeltaWeight);
        chancePercent += dexterityDelta * Mathf.Max(0f, skill.AutoEvadeDexterityDeltaWeight);
        chancePercent += intelligenceDelta * Mathf.Max(0f, skill.AutoEvadeIntelligenceDeltaWeight);
        chancePercent += (selfMoveSpeed - enemyMoveSpeed)
            * Mathf.Max(0f, skill.AutoEvadeMoveSpeedDeltaWeight);

        // Attack >1.0 attack speed gây áp lực; attack chậm hơn 1.0 thì dễ đọc hơn.
        chancePercent -= (enemyAttackSpeed - 1f)
            * Mathf.Max(0f, skill.AutoEvadeAttackSpeedPressureWeight);

        // Mốc 0.18s là một attack "neutral". Telegraph dài hơn cho thêm read-time,
        // startup ngắn hơn trừ chance. Clamp để một resource lạ không phá công thức.
        float startupRead = Mathf.Clamp(
            (startupSeconds - 0.18f) * Mathf.Max(0f, skill.AutoEvadeStartupReadWeight),
            -6f,
            7f);
        chancePercent += startupRead;

        // Lunge/projectile nhanh hơn tốc độ combat movement của Hikaru thì khó né hơn.
        if (travelSpeed > 0f)
        {
            chancePercent -= Mathf.Max(0f, travelSpeed - selfMoveSpeed)
                * Mathf.Max(0f, skill.AutoEvadeTravelSpeedPressureWeight);
        }

        // Overmatch là rule riêng, không phải clamp 95%.
        // Cần cả level gap lẫn mastery ratio để boss/resource level thấp nhưng stat cực cao
        // không vô tình bị auto-dodge 100%.
        if (IsDeepFlowOvermatch(
            skill,
            levelDelta,
            selfLevel,
            selfDexterity,
            selfIntelligence,
            selfMoveSpeed,
            enemyLevel,
            enemyDexterity,
            enemyIntelligence,
            enemyMoveSpeed,
            enemyAttackSpeed,
            travelSpeed))
        {
            return 1f;
        }

        float minimum = Mathf.Clamp(skill.AutoEvadeMinChancePercent, 0f, 100f);
        float maximum = Mathf.Clamp(
            Mathf.Max(minimum, skill.AutoEvadeMaxChancePercent),
            minimum,
            100f);

        return Mathf.Clamp(chancePercent, minimum, maximum) / 100f;
    }

    private static bool IsDeepFlowOvermatch(
        SkillData skill,
        int levelDelta,
        int selfLevel,
        int selfDexterity,
        int selfIntelligence,
        float selfMoveSpeed,
        int enemyLevel,
        int enemyDexterity,
        int enemyIntelligence,
        float enemyMoveSpeed,
        float enemyAttackSpeed,
        float attackTravelSpeed)
    {
        if (skill == null || levelDelta < Mathf.Max(1, skill.AutoEvadeOvermatchLevelGap))
        {
            return false;
        }

        // "Mastery" cố tình ưu tiên level + DEX + INT.
        // Move speed và attack tempo chỉ là phần phụ để không biến STR/damage thành dodge stat.
        float selfMastery =
            selfLevel * 1.25f
            + selfDexterity * 0.55f
            + selfIntelligence * 0.45f
            + selfMoveSpeed * 0.08f;

        float threatMastery =
            enemyLevel * 1.25f
            + enemyDexterity * 0.55f
            + enemyIntelligence * 0.35f
            + enemyMoveSpeed * 0.08f
            + enemyAttackSpeed * 8f
            + Mathf.Max(0f, attackTravelSpeed) * 0.05f;

        float requiredRatio = Mathf.Max(1f, skill.AutoEvadeOvermatchMasteryRatio);
        return selfMastery >= Mathf.Max(1f, threatMastery) * requiredRatio;
    }

    private static float ResolveFlowMovementSpeed(CombatCharacter actor)
    {
        if (actor == null)
        {
            return 0f;
        }

        float abilityMultiplier = actor.Abilities?.MoveSpeedMultiplier ?? 1f;
        float statusMultiplier = actor.Statuses?.MoveSpeedMultiplier ?? 1f;
        return Mathf.Max(0f, actor.Speed * abilityMultiplier * statusMultiplier);
    }

    private static float ResolveAttackTravelSpeed(CombatActionData action)
    {
        if (action == null)
        {
            return 0f;
        }

        if (action.DeliveryMode == CombatDeliveryMode.Projectile)
        {
            ProjectileSpecData projectile = action.ResolveProjectileSpec();
            return Mathf.Max(0f, projectile?.Speed ?? 0f);
        }

        return Mathf.Max(0f, action.LungeSpeed);
    }

    private void BeginDeepFlowEvasion(HitRequest request, SkillData activeSkill)
    {
        CombatCharacter attacker = request.Attacker;
        Vector2 incoming = request.AttackDirection;
        if (incoming.LengthSquared() <= 0.001f && attacker != null)
        {
            incoming = CombatCenter - attacker.CombatCenter;
        }
        if (incoming.LengthSquared() <= 0.001f)
        {
            incoming = -FacingDirection;
        }
        incoming = incoming.Normalized();

        Vector2 left = new Vector2(-incoming.Y, incoming.X);
        Vector2 right = -left;
        Vector2 side;
        if (_lastRawMoveInput != Vector2.Zero)
        {
            // Nếu player đã nghiêng input sang một phía, Flow ưu tiên đúng ý định đó.
            side = _lastRawMoveInput.Dot(left) >= _lastRawMoveInput.Dot(right) ? left : right;
        }
        else
        {
            side = _flowRng.Randf() < 0.5f ? left : right;
        }

        // Lách ngang là chính, kèm một chút "thoát khỏi" attacker để không trượt dọc
        // ngay mép hitbox rồi nhìn như né bằng toán học.
        Vector2 dodgeDirection = (side * 0.94f + incoming * 0.24f).Normalized();
        float dodgeImpulse = Mathf.Max(0f, activeSkill.AutoEvadeImpulse);

        if (Actions?.IsRunning == true)
        {
            // Deep Flow ưu tiên sinh tồn hơn việc cố hoàn tất nhát chém hiện tại.
            Actions.Cancel();
        }

        if (attacker != null && IsUsableFlowTarget(attacker))
        {
            _flowReengageTarget = attacker;
            _flowPreEvadeTargetDistance = CombatCenter.DistanceTo(attacker.CombatCenter);
            _flowReengageDelayRemaining = Mathf.Max(0f, activeSkill.AutoReengageDelaySeconds);
            _flowReengageWindowRemaining = Mathf.Max(
                _flowReengageDelayRemaining,
                activeSkill.AutoReengageWindowSeconds);
            FaceToward(attacker.CombatCenter);
        }
        else
        {
            ClearFlowReengage();
        }

        _flowInvulnerableRemaining = Mathf.Max(0.04f, activeSkill.AutoEvadeInvulnerabilitySeconds);
        _flowEvadeCooldownRemaining = Mathf.Max(
            _flowInvulnerableRemaining,
            activeSkill.AutoEvadeInternalCooldownSeconds);

        if (dodgeImpulse > 0f)
        {
            ApplyExternalForce(dodgeDirection * dodgeImpulse);
        }

    }

    private bool IsEligibleFlowHit(HitRequest request)
    {
        if (request?.Target != this
            || request.Attacker == null
            || !request.Attacker.IsAlive
            || !IsAlive
            || Statuses?.IsFrozen == true
            || IsBlocking
            || IsPerfectParryWindowActive
            || !FactionRules.IsHostile(request.Attacker.Faction, Faction)
            || request.Profile == null
            || request.Profile.DamageType == DamageType.True
            || request.Action == null)
        {
            return false;
        }

        return request.Action.DeliveryMode == CombatDeliveryMode.MeleeHitbox
            || request.Action.DeliveryMode == CombatDeliveryMode.Projectile;
    }

    private static bool IsFlowEvasionSkill(SkillData skill)
    {
        return skill != null && skill.AutoEvadeChancePercent > 0f;
    }

    private static bool IsUsableFlowTarget(CombatCharacter target)
    {
        return target != null
            && GodotObject.IsInstanceValid(target)
            && target.IsInsideTree()
            && target.IsAlive;
    }

    private void ClearFlowReengage()
    {
        _flowReengageTarget = null;
        _flowPreEvadeTargetDistance = 0f;
        _flowReengageDelayRemaining = 0f;
        _flowReengageWindowRemaining = 0f;
    }

    private void EnsureFlowRng()
    {
        if (_flowRngReady)
        {
            return;
        }

        _flowRng.Randomize();
        _flowRngReady = true;
    }
}
