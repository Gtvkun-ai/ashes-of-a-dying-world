using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Combat.Runtime
{
    public static class CombatResolver
    {
        private const float ArmorCurveConstant = 100f;

        public static HitResult Resolve(HitRequest request)
        {
            if (request?.Attacker == null || request.Target == null || request.Profile == null)
            {
                return HitResult.Rejected(HitRejectionReason.InvalidRequest);
            }

            CombatCharacter attacker = request.Attacker;
            CombatCharacter target = request.Target;
            if (!target.IsAlive)
            {
                return HitResult.Rejected(HitRejectionReason.TargetDead);
            }

            if (!FactionRules.CanDamage(attacker.Faction, target.Faction))
            {
                return HitResult.Rejected(HitRejectionReason.FriendlyFire);
            }

            HitProfileData profile = request.Profile;
            float attackPower = attacker.Stats?.AttackDamage ?? 1f;
            float rawDamage = Mathf.Max(0f, profile.BaseDamage + attackPower * profile.AttackPowerScale);
            float armor = Mathf.Max(0f, (target.Stats?.Armor ?? 0f) - profile.ArmorPenetration);
            float mitigatedDamage = profile.DamageType == DamageType.True
                ? rawDamage
                : rawDamage * (1f - armor / (armor + ArmorCurveConstant));

            bool wasBlocked = target.IsBlockingAttackFrom(request.HitOrigin);
            bool guardBroken = false;
            float guardDamage = 0f;
            float hpDamage = mitigatedDamage;
            float poiseDamage = profile.PoiseDamage;

            if (wasBlocked)
            {
                WeaponMovesetData guardMoveset = target.ActiveMoveset;
                float reduction = guardMoveset?.GuardDamageReduction ?? 0.3f;
                int defense = target.Stats?.GetAttributeValue(AttributeType.Defense) ?? 0;
                reduction = Mathf.Clamp(reduction + defense * 0.0025f, 0f, 0.9f);

                guardDamage = Mathf.Max(0f, profile.GuardDamage + rawDamage * 0.2f);
                float staminaPerDamage = guardMoveset?.GuardStaminaPerDamage ?? 0.5f;
                float staminaCost = guardDamage * Mathf.Max(0f, staminaPerDamage);

                bool hadGuard = target.Stats == null || target.Stats.ConsumeGuard(guardDamage);
                bool hadStamina = target.Stats == null || target.Stats.CurrentStamina + 0.001f >= staminaCost;
                if (target.Stats != null && staminaCost > 0f)
                {
                    // Khi guard không gánh nổi cost, stamina phải cạn thật thay vì báo fail rồi giữ nguyên thanh.
                    target.Stats.ChangeStamina(-staminaCost);
                }
                guardBroken = !hadGuard || !hadStamina || (target.Stats != null && target.Stats.CurrentGuard <= 0f);

                if (guardBroken)
                {
                    hpDamage = mitigatedDamage * 0.75f;
                    poiseDamage *= 1.5f;
                }
                else
                {
                    hpDamage = mitigatedDamage * (1f - reduction);
                    poiseDamage *= 0.25f;
                }
            }

            bool staggered = false;
            if (target.Stats != null)
            {
                target.Stats.ApplyDamage(hpDamage);
                if (poiseDamage > 0f && target.Stats.CurrentHP > 0f)
                {
                    target.Stats.ConsumePoise(poiseDamage);
                    staggered = target.Stats.CurrentPoise <= 0f;
                }
            }

            Vector2 direction = request.AttackDirection;
            if (direction == Vector2.Zero)
            {
                direction = (target.GlobalPosition - attacker.GlobalPosition).Normalized();
            }

            float resistance = target.Stats?.GetKnockbackResistance() ?? 0f;
            Vector2 knockback = direction.Normalized() * profile.KnockbackForce * (1f - resistance);
            bool killed = target.Stats != null && target.Stats.CurrentHP <= 0f;

            return new HitResult
            {
                Applied = true,
                RejectionReason = HitRejectionReason.None,
                RawDamage = rawDamage,
                HpDamage = Mathf.Max(0f, hpDamage),
                GuardDamage = guardDamage,
                PoiseDamage = poiseDamage,
                WasBlocked = wasBlocked,
                GuardBroken = guardBroken,
                Staggered = staggered,
                Killed = killed,
                HitstunSeconds = profile.HitstunSeconds,
                Knockback = knockback
            };
        }
    }
}
