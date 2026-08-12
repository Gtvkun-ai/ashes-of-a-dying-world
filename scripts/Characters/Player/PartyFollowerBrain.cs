using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.Core.Managers;

/// <summary>
/// AI nhẹ cho Hikaru khi quyền điều khiển được chuyển sang một thành viên khác.
/// Nó chỉ làm ba việc: theo đội trưởng, áp sát hostile gần nhất, đánh melee khi đủ gần.
/// Không cố thay thế Decision Core; đây là fallback để nhân vật cũ không hóa tượng khi switch.
/// </summary>
public partial class PartyFollowerBrain : Node
{
    [Export] public float FollowDistance { get; set; } = 52f;
    [Export] public float FollowResumeDistance { get; set; } = 72f;
    [Export] public float EnemySearchRadius { get; set; } = 135f;
    [Export] public float AttackRange { get; set; } = 38f;
    [Export] public float AttackCooldown { get; set; } = 0.42f;

    private global::Player _character;
    private float _attackCooldownRemaining;

    public override void _Ready()
    {
        _character = GetParentOrNull<global::Player>();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_character == null || !_character.IsAlive || _character.UsePlayerInput)
        {
            return;
        }

        float dt = Mathf.Max(0f, (float)delta);
        _attackCooldownRemaining = Mathf.Max(0f, _attackCooldownRemaining - dt);

        CombatCharacter leader = PlayerManager.Instance?.GetActiveCombatCharacter();
        if (leader == null || leader == _character || !GodotObject.IsInstanceValid(leader))
        {
            _character.StopMoveInput();
            _character.SetBlocking(false);
            return;
        }

        CombatCharacter target = FindNearestHostile();
        if (target != null)
        {
            RunCombat(target);
            return;
        }

        _character.SetBlocking(false);
        float distance = _character.GlobalPosition.DistanceTo(leader.GlobalPosition);
        if (distance <= FollowDistance)
        {
            _character.StopMoveInput();
            return;
        }

        Vector2 direction = (leader.GlobalPosition - _character.GlobalPosition).Normalized();
        _character.SetMoveInput(direction, distance > FollowResumeDistance * 1.8f);
    }

    private void RunCombat(CombatCharacter target)
    {
        Vector2 toTarget = target.CombatCenter - _character.CombatCenter;
        float distance = toTarget.Length();
        _character.FaceToward(target.CombatCenter);
        _character.SetBlocking(false);

        if (distance <= AttackRange)
        {
            _character.StopMoveInput();
            if (_attackCooldownRemaining <= 0f && _character.RequestAttack())
            {
                _attackCooldownRemaining = AttackCooldown;
            }
            return;
        }

        if (distance > 0.001f)
        {
            _character.SetMoveInput(toTarget.Normalized(), distance > EnemySearchRadius * 0.65f, true);
        }
    }

    private CombatCharacter FindNearestHostile()
    {
        CombatCharacter nearest = null;
        float bestDistanceSquared = EnemySearchRadius * EnemySearchRadius;
        foreach (Node node in GetTree().GetNodesInGroup("Combatant"))
        {
            if (node is not CombatCharacter candidate
                || candidate == _character
                || !candidate.IsAlive
                || !FactionRules.CanDamage(_character.Faction, candidate.Faction))
            {
                continue;
            }

            float distanceSquared = _character.CombatCenter.DistanceSquaredTo(candidate.CombatCenter);
            if (distanceSquared < bestDistanceSquared)
            {
                nearest = candidate;
                bestDistanceSquared = distanceSquared;
            }
        }
        return nearest;
    }
}
