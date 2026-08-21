using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Skills;

/// <summary>
/// Tactical follower cho Hikaru khi người chơi đang điều khiển một thành viên khác.
/// Mục tiêu là giữ nhịp melee có phòng thủ: đọc startup của địch, guard/retreat,
/// giữ stamina reserve rồi mới vào đánh thay vì lao thẳng vào hitbox đến chết.
/// </summary>
public partial class PartyFollowerBrain : Node
{
    [ExportGroup("Formation")]
    [Export] public float FollowDistance { get; set; } = 54f;
    [Export] public float FollowResumeDistance { get; set; } = 76f;
    [Export] public float CombatTetherFromLeader { get; set; } = 165f;

    [ExportGroup("Targeting")]
    [Export] public float EnemySearchRadius { get; set; } = 150f;
    [Export] public float PreferredCombatDistance { get; set; } = 38f;
    [Export] public float AttackRange { get; set; } = 42f;
    [Export] public float TooCloseDistance { get; set; } = 25f;
    [Export] public float AttackCooldownMin { get; set; } = 0.48f;
    [Export] public float AttackCooldownMax { get; set; } = 0.72f;

    [ExportGroup("Defense")]
    [Export] public float ThreatResponseDistance { get; set; } = 57f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float MinimumGuardRatio { get; set; } = 0.30f;
    [Export] public float EvadeDuration { get; set; } = 0.34f;
    [Export] public float EvadeCooldown { get; set; } = 1.10f;

    [ExportGroup("Stamina Discipline")]
    [Export(PropertyHint.Range, "0,1,0.05")] public float EnterRecoveryStaminaRatio { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float ExitRecoveryStaminaRatio { get; set; } = 0.58f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float AttackReserveStaminaRatio { get; set; } = 0.42f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float RunReserveStaminaRatio { get; set; } = 0.62f;
    [Export] public float RecoveryKeepAwayDistance { get; set; } = 67f;

    private global::Player _character;
    private readonly RandomNumberGenerator _rng = new();
    private float _attackCooldownRemaining;
    private float _evadeRemaining;
    private float _evadeCooldownRemaining;
    private Vector2 _evadeDirection;
    private bool _recoveringStamina;
    private float _strafeSign = 1f;
    private float _strafeRetargetRemaining;
    private float _recoverySkillRetryRemaining;

    public override void _Ready()
    {
        _character = GetParentOrNull<global::Player>();
        _rng.Randomize();
        _strafeSign = _rng.Randf() < 0.5f ? -1f : 1f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_character == null || !_character.IsAlive || _character.UsePlayerInput)
        {
            return;
        }

        float dt = Mathf.Max(0f, (float)delta);
        _attackCooldownRemaining = Mathf.Max(0f, _attackCooldownRemaining - dt);
        _evadeRemaining = Mathf.Max(0f, _evadeRemaining - dt);
        _evadeCooldownRemaining = Mathf.Max(0f, _evadeCooldownRemaining - dt);
        _strafeRetargetRemaining = Mathf.Max(0f, _strafeRetargetRemaining - dt);
        _recoverySkillRetryRemaining = Mathf.Max(0f, _recoverySkillRetryRemaining - dt);

        UpdateStaminaRecoveryState();

        CombatCharacter leader = PlayerManager.Instance?.GetActiveCombatCharacter();
        if (leader == null || leader == _character || !GodotObject.IsInstanceValid(leader))
        {
            ResetIntent();
            return;
        }

        CombatCharacter target = FindBestHostile(leader);
        if (target != null)
        {
            RunCombat(target, leader);
            return;
        }

        FollowLeader(leader);
    }

    private void RunCombat(CombatCharacter target, CombatCharacter leader)
    {
        Vector2 toTarget = target.CombatCenter - _character.CombatCenter;
        float distance = toTarget.Length();
        if (distance <= 0.001f)
        {
            distance = 0.001f;
            toTarget = _character.FacingDirection;
        }

        Vector2 towardTarget = toTarget / distance;
        _character.FaceToward(target.CombatCenter);

        // Không bỏ đội trưởng để đuổi một con quái qua nửa bản đồ.
        float leaderDistance = _character.GlobalPosition.DistanceTo(leader.GlobalPosition);
        if (leaderDistance > CombatTetherFromLeader)
        {
            _character.SetBlocking(false);
            Vector2 backToLeader = (leader.GlobalPosition - _character.GlobalPosition).Normalized();
            _character.SetMoveInput(backToLeader, CanSpendStaminaOnRun(), true);
            return;
        }

        if (_evadeRemaining > 0f)
        {
            _character.SetBlocking(false);
            _character.SetMoveInput(_evadeDirection, CanSpendStaminaOnRun(), true);
            return;
        }

        bool imminentThreat = IsImminentMeleeThreat(target, distance);
        if (imminentThreat)
        {
            if (ShouldEvade(target, distance))
            {
                BeginEvade(towardTarget);
                return;
            }

            if (CanGuard())
            {
                _character.StopMoveInput();
                _character.SetBlocking(true);
                return;
            }
        }

        _character.SetBlocking(false);

        if (_recoveringStamina)
        {
            RecoverStaminaAround(target, towardTarget, distance);
            return;
        }

        float staminaRatio = GetStaminaRatio();
        if (distance < TooCloseDistance && target.StateMachine?.Current != CombatStateId.AttackRecovery)
        {
            // Backpedal thay vì đứng dính vào collider rồi trade hit vô nghĩa.
            _character.SetMoveInput(-towardTarget, false, true, 0.92f);
            return;
        }

        if (distance <= AttackRange
            && staminaRatio >= AttackReserveStaminaRatio
            && _attackCooldownRemaining <= 0f
            && (!target.IsPerformingAttack || target.StateMachine?.Current == CombatStateId.AttackRecovery))
        {
            _character.StopMoveInput();
            if (_character.RequestAttack())
            {
                _attackCooldownRemaining = _rng.RandfRange(
                    Mathf.Max(0.25f, AttackCooldownMin),
                    Mathf.Max(AttackCooldownMin + 0.05f, AttackCooldownMax));
            }
            return;
        }

        if (distance > AttackRange + 8f)
        {
            bool run = distance > 92f && CanSpendStaminaOnRun();
            _character.SetMoveInput(towardTarget, run, true, distance < 64f ? 0.72f : 1f);
            return;
        }

        // Neutral melee: orbit nhẹ thay vì đứng yên hoặc lao thẳng. Điều này cũng tự tạo
        // khoảng trống để stamina hồi vì không dùng run.
        StrafeAroundTarget(towardTarget, distance);
    }

    private bool IsImminentMeleeThreat(CombatCharacter target, float distance)
    {
        if (target?.Actions?.CurrentAction == null || target.StateMachine == null)
        {
            return false;
        }

        CombatStateId state = target.StateMachine.Current;
        if (state != CombatStateId.AttackStartup && state != CombatStateId.AttackActive)
        {
            return false;
        }

        Vector2 fromTarget = _character.CombatCenter - target.CombatCenter;
        if (fromTarget.LengthSquared() <= 0.001f)
        {
            return true;
        }

        float facingDot = target.FacingDirection.Dot(fromTarget.Normalized());
        if (facingDot < 0.30f)
        {
            return false;
        }

        float actionReach = target.Actions.CurrentAction.HitProfile?.Reach ?? 18f;
        Vector2 hitbox = target.Actions.CurrentAction.HitProfile?.HitboxSize ?? new Vector2(20f, 20f);
        float practicalReach = actionReach + Mathf.Max(hitbox.X, hitbox.Y) * 0.75f + 18f;
        return distance <= Mathf.Max(ThreatResponseDistance, practicalReach);
    }

    private bool ShouldEvade(CombatCharacter target, float distance)
    {
        if (_evadeCooldownRemaining > 0f || GetStaminaRatio() < 0.22f)
        {
            return false;
        }

        bool activeNow = target.StateMachine?.Current == CombatStateId.AttackActive;
        bool guardWeak = !CanGuard();
        var currentAction = target.Actions?.CurrentAction;
        bool uninterruptible = currentAction != null
            && (currentAction.Tags & CombatActionTag.Uninterruptible) != 0;
        return (activeNow && distance <= TooCloseDistance + 8f) || guardWeak || uninterruptible;
    }

    private void BeginEvade(Vector2 towardTarget)
    {
        Vector2 perpendicular = new Vector2(-towardTarget.Y, towardTarget.X) * _strafeSign;
        // Pha thêm vector lùi để né ra khỏi hitbox chứ không chỉ chạy ngang mặt kiếm.
        _evadeDirection = (perpendicular * 0.72f - towardTarget * 0.68f).Normalized();
        _evadeRemaining = Mathf.Max(0.18f, EvadeDuration);
        _evadeCooldownRemaining = Mathf.Max(0.55f, EvadeCooldown);
        _strafeSign *= -1f;
        _character.SetBlocking(false);
        _character.SetMoveInput(_evadeDirection, CanSpendStaminaOnRun(), true);
    }

    private void RecoverStaminaAround(CombatCharacter target, Vector2 towardTarget, float distance)
    {
        // Nếu người chơi đã equip một skill hồi stamina như Hồi sức, AI biết dùng nó.
        // Không có skill/đang cooldown thì fallback về disengage + passive regen.
        if (_recoverySkillRetryRemaining <= 0f)
        {
            TryUseStaminaRecoverySkill();
            _recoverySkillRetryRemaining = 0.75f;
        }

        // Stamina chỉ regen ở Locomotion, vì vậy không giữ guard vô thức trong recovery.
        _character.SetBlocking(false);

        if (distance < RecoveryKeepAwayDistance - 8f)
        {
            Vector2 perpendicular = new Vector2(-towardTarget.Y, towardTarget.X) * _strafeSign;
            Vector2 retreat = (-towardTarget * 0.85f + perpendicular * 0.35f).Normalized();
            _character.SetMoveInput(retreat, false, true, 0.82f);
            return;
        }

        if (distance > RecoveryKeepAwayDistance + 22f)
        {
            _character.SetMoveInput(towardTarget, false, true, 0.55f);
            return;
        }

        StrafeAroundTarget(towardTarget, distance, 0.52f);
    }


    private bool TryUseStaminaRecoverySkill()
    {
        var collection = SkillCollectionResolver.Resolve(_character?.Stats);
        if (collection == null || _character?.Abilities == null)
        {
            return false;
        }

        for (int slot = 0; slot < 4; slot++)
        {
            SkillData skill = collection.GetEquippedSkill(slot);
            if (skill == null
                || skill.ExecutionType != SkillExecutionType.RestoreResources
                || skill.RestoreStaminaAmount <= 0f)
            {
                continue;
            }

            if (_character.Abilities.TryActivate(skill))
            {
                return true;
            }
        }

        return false;
    }

    private void StrafeAroundTarget(Vector2 towardTarget, float distance, float speedScale = 0.60f)
    {
        if (_strafeRetargetRemaining <= 0f)
        {
            if (_rng.Randf() < 0.42f)
            {
                _strafeSign *= -1f;
            }
            _strafeRetargetRemaining = _rng.RandfRange(0.55f, 1.20f);
        }

        Vector2 perpendicular = new Vector2(-towardTarget.Y, towardTarget.X) * _strafeSign;
        float radialCorrection = Mathf.Clamp((distance - PreferredCombatDistance) / 24f, -0.55f, 0.55f);
        Vector2 move = (perpendicular + towardTarget * radialCorrection).Normalized();
        _character.SetMoveInput(move, false, true, speedScale);
    }

    private void FollowLeader(CombatCharacter leader)
    {
        _character.SetBlocking(false);
        float distance = _character.GlobalPosition.DistanceTo(leader.GlobalPosition);
        if (distance <= FollowDistance)
        {
            _character.StopMoveInput();
            return;
        }

        Vector2 direction = (leader.GlobalPosition - _character.GlobalPosition).Normalized();
        bool run = distance > FollowResumeDistance * 1.8f && CanSpendStaminaOnRun();
        _character.SetMoveInput(direction, run, false, distance < FollowResumeDistance ? 0.60f : 1f);
    }

    private void UpdateStaminaRecoveryState()
    {
        float ratio = GetStaminaRatio();
        if (_recoveringStamina)
        {
            if (ratio >= Mathf.Max(EnterRecoveryStaminaRatio + 0.05f, ExitRecoveryStaminaRatio))
            {
                _recoveringStamina = false;
            }
        }
        else if (ratio <= Mathf.Clamp(EnterRecoveryStaminaRatio, 0.05f, 0.90f))
        {
            _recoveringStamina = true;
        }
    }

    private bool CanGuard()
    {
        if (_character.Stats == null || _character.Stats.MaxGuard <= 0.001f)
        {
            return false;
        }
        return _character.Stats.CurrentGuard / _character.Stats.MaxGuard >= MinimumGuardRatio;
    }

    private bool CanSpendStaminaOnRun()
    {
        return !_recoveringStamina && GetStaminaRatio() >= RunReserveStaminaRatio;
    }

    private float GetStaminaRatio()
    {
        if (_character?.Stats == null || _character.Stats.MaxStamina <= 0.001f)
        {
            return 1f;
        }
        return Mathf.Clamp(_character.Stats.CurrentStamina / _character.Stats.MaxStamina, 0f, 1f);
    }

    private CombatCharacter FindBestHostile(CombatCharacter leader)
    {
        CombatCharacter best = null;
        float bestScore = float.PositiveInfinity;
        float maxDistanceSquared = EnemySearchRadius * EnemySearchRadius;

        foreach (Node node in GetTree().GetNodesInGroup("Combatant"))
        {
            if (node is not CombatCharacter candidate
                || candidate == _character
                || !candidate.IsAlive
                || !FactionRules.IsHostile(_character.Faction, candidate.Faction))
            {
                continue;
            }

            float selfDistanceSquared = _character.CombatCenter.DistanceSquaredTo(candidate.CombatCenter);
            if (selfDistanceSquared > maxDistanceSquared)
            {
                continue;
            }

            float selfDistance = Mathf.Sqrt(selfDistanceSquared);
            float leaderDistance = leader.CombatCenter.DistanceTo(candidate.CombatCenter);
            float score = selfDistance + leaderDistance * 0.28f;
            if (candidate.IsPerformingAttack)
            {
                score -= 12f;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private void ResetIntent()
    {
        _character.StopMoveInput();
        _character.SetBlocking(false);
        _evadeRemaining = 0f;
    }
}
