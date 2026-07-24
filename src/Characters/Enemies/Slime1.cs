using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.AI;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.UI.HUD;

/// <summary>
/// Slime giờ chỉ là actor dữ liệu/presentation. Quyết định hành vi nằm trong SlimeBrain.
/// </summary>
public partial class Slime1 : CombatCharacter
{
    public float CurrentHP => Stats?.CurrentHP ?? 0f;
    public float MaxHP => Stats?.MaxHP ?? 1f;
    public int Level => Stats?.CurrentLevel ?? 1;

    private SlimeBrain _brain;

    protected override void OnCombatReady()
    {
        Faction = CombatFaction.Enemy;
        AddToGroup("Enemy");
        RemoveFromWorldOnDeath = true;
        _brain = GetNodeOrNull<SlimeBrain>("SlimeBrain");
        CallDeferred(nameof(RegisterHealthBar));
    }

    protected override void OnHitReceived(HitRequest request, HitResult result)
    {
        // Gửi attacker thật cho brain thay vì đoán theo khoảng cách. Projectile của Hyou có thể
        // bay từ ngoài AggroRadius, nhưng slime vẫn phải biết chính xác ai vừa đánh mình.
        if (result?.Applied == true && request?.Attacker != null)
        {
            _brain?.NotifyProvoked(request.Attacker, result.HpDamage);
        }
    }

    protected override void OnDefeated(CombatCharacter attacker)
    {
        EnemyHealthBarService.Instance?.UnregisterEnemy(this);
        if (attacker is Slime1 slimeAttacker)
        {
            slimeAttacker.GainLevel(1);
        }
    }

    public void GainLevel(int amount = 1)
    {
        if (amount <= 0 || Stats == null)
        {
            return;
        }

        Stats.SetCurrentLevel(Stats.CurrentLevel + amount);
        if (Stats.UseManualProfile)
        {
            Stats.ManualMaxHP += 10f * amount;
            Stats.ManualAttackPower += 2f * amount;
            Stats.RecalculateStats();
        }
        Stats.FillAllResources();
    }

    private void RegisterHealthBar()
    {
        EnemyHealthBarService.Instance?.RegisterEnemy(
            this,
            () => Stats?.CurrentHP ?? 0f,
            () => Stats?.MaxHP ?? 1f,
            () => Stats?.CurrentLevel ?? 1);
    }
}
