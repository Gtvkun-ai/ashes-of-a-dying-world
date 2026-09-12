using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Combat.Runtime
{
    public static class CombatResolver
    {
        public static HitResult Resolve(HitRequest request)
        {
            if (request?.Attacker == null || request.Target == null || request.Profile == null)
            {
                return HitResult.Rejected(HitRejectionReason.InvalidRequest);
            }

            CombatCharacter attacker = request.Attacker;
            CombatCharacter target = request.Target;
            if (attacker == target)
            {
                return HitResult.Rejected(HitRejectionReason.InvalidRequest);
            }

            if (!target.IsAlive)
            {
                return HitResult.Rejected(HitRejectionReason.TargetDead);
            }

            if (!FactionRules.CanDamage(attacker.Faction, target.Faction))
            {
                return HitResult.Rejected(HitRejectionReason.FriendlyFire);
            }

            HitProfileData profile = request.Profile;
            bool wasBlocked = target.IsBlockingAttackFrom(request.HitOrigin);
            bool shattered = profile.ShatterFrozen
                && target.Statuses?.IsFrozen == true
                && !wasBlocked;
            float attackPower = attacker.Stats?.GetAttackPower(profile.PowerScaling, profile.DamageType) ?? 1f;
            float damageMultiplier = Mathf.Max(0f, request.DamageMultiplier);
            float rawDamage = Mathf.Max(
                0f,
                (profile.BaseDamage + attackPower * profile.AttackPowerScale) * damageMultiplier);
            if (shattered)
            {
                // Shatter là bonus trạng thái riêng, không bị nhân tiếp bởi multiplier của skill.
                rawDamage += Mathf.Max(0f, profile.ShatterBonusDamage);
            }

            float damageResistance = Mathf.Max(
                0f,
                (target.Stats?.GetDamageResistance(profile.DamageType) ?? 0f) - profile.ArmorPenetration);
            float mitigationCurve = Mathf.Max(1f, target.Stats?.MitigationCurveConstant ?? 100f);
            float mitigatedDamage = profile.DamageType == DamageType.True
                ? rawDamage
                : rawDamage * mitigationCurve / (damageResistance + mitigationCurve);

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

            // Damage/Poise giải quyết trước; Impact dùng trạng thái Poise sau hit để biết target
            // còn trụ vững hay đã bị bào mòn đến ngưỡng mất thăng bằng.
            ImpactResolution impact = ImpactReactionResolver.Resolve(
                request,
                wasBlocked,
                guardBroken,
                staggered,
                shattered);

            bool killed = target.Stats != null && target.Stats.CurrentHP <= 0f;
            bool reactionStaggered = (int)impact.Reaction >= (int)ImpactReactionType.Stagger;
            float reactionHitstun = impact.Reaction switch
            {
                ImpactReactionType.Absorb => 0f,
                ImpactReactionType.Flinch => Mathf.Max(0f, profile.HitstunSeconds) * 0.65f,
                _ => Mathf.Max(0f, profile.HitstunSeconds)
            };

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
                Staggered = reactionStaggered,
                Killed = killed,
                Shattered = shattered,
                Reaction = impact.Reaction,
                EffectiveImpact = impact.ImpactPower,
                EffectiveStability = impact.Stability,
                ImpactRatio = impact.ImpactRatio,
                MassFactor = impact.MassFactor,
                MomentumFactor = impact.MomentumFactor,
                HitstunSeconds = reactionHitstun,
                ForcedStaggerSeconds = impact.ReactionLockSeconds,
                HitStopSeconds = Mathf.Max(0f, profile.HitStopSeconds),
                HitFlashSeconds = Mathf.Max(0f, profile.HitFlashSeconds),
                LaunchHeight = impact.LaunchHeight,
                LaunchDuration = impact.LaunchDuration,
                Knockback = impact.KnockbackVelocity
            };
        }
    }
}
