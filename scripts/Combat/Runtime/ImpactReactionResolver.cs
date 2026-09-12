using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Resolver riêng cho cảm giác va chạm vật lý.
    ///
    /// Mục tiêu của hệ này không phải mô phỏng Newton tuyệt đối, mà tạo quy luật nhất quán:
    /// - attacker nặng / lao nhanh -> Impact lớn hơn;
    /// - target nặng / còn nhiều Poise / DEF tốt -> khó mất thăng bằng hơn;
    /// - cùng một hit có thể chỉ làm boss khựng nhưng lại hất slime nhỏ bay;
    /// - Launch/Stagger không còn xảy ra chỉ vì profile bật một bool cứng.
    /// </summary>
    public static class ImpactReactionResolver
    {
        private const float MinMassFactor = 0.40f;
        private const float MaxMassFactor = 1.60f;
        private const float MinMomentumFactor = 0.75f;
        private const float MaxMomentumFactor = 1.40f;

        public static ImpactResolution Resolve(
            HitRequest request,
            bool wasBlocked,
            bool guardBroken,
            bool poiseBroken,
            bool shattered)
        {
            CombatCharacter attacker = request.Attacker;
            CombatCharacter target = request.Target;
            HitProfileData profile = request.Profile;

            Vector2 direction = ResolveDirection(request, attacker, target);
            float attackerMass = Mathf.Max(0.1f, attacker.Stats?.BodyMass ?? 1f);
            float targetMass = Mathf.Max(0.1f, target.Stats?.BodyMass ?? 1f);

            // Ratio đối xứng quanh 1.0: hai body bằng nhau -> factor 1.0.
            // Dùng 2Ma/(Ma+Mt) thay vì Ma/Mt để chênh mass không bùng số quá mạnh.
            float rawMassFactor = Mathf.Clamp(
                (2f * attackerMass) / Mathf.Max(0.1f, attackerMass + targetMass),
                MinMassFactor,
                MaxMassFactor);
            float massFactor = Mathf.Lerp(
                1f,
                rawMassFactor,
                Mathf.Clamp(profile.BodyMassInfluence, 0f, 1f));

            Vector2 relativeVelocity = attacker.ImpactVelocity - target.ImpactVelocity;
            float closingSpeed = Mathf.Max(0f, relativeVelocity.Dot(direction));
            float referenceSpeed = Mathf.Max(1f, profile.ReferenceImpactSpeed);
            float rawMomentumFactor = Mathf.Clamp(
                0.80f + 0.40f * (closingSpeed / referenceSpeed),
                MinMomentumFactor,
                MaxMomentumFactor);
            float momentumFactor = Mathf.Lerp(
                1f,
                rawMomentumFactor,
                Mathf.Clamp(profile.MomentumInfluence, 0f, 1f));

            float shatterFactor = shattered
                ? Mathf.Max(1f, profile.ShatterKnockbackMultiplier)
                : 1f;
            float baseImpact = ResolveBaseImpact(profile);
            float effectiveImpact = baseImpact * massFactor * momentumFactor * shatterFactor;
            float stability = ResolveTargetStability(target, wasBlocked, guardBroken);
            float impactRatio = effectiveImpact / Mathf.Max(1f, stability);

            ImpactReactionType reaction = ResolveReaction(impactRatio);

            // Poise vỡ vẫn là stagger thật. ForceStagger được giữ như legacy/scripted override,
            // nhưng không dùng cho profile thường mới.
            if (poiseBroken || profile.ForceStagger || shattered)
            {
                reaction = MaxReaction(reaction, ImpactReactionType.Stagger);
            }

            reaction = ApplyCapabilityCaps(reaction, profile);

            // Block thành công hấp thụ displacement. Guard break đi tiếp qua reaction bình thường.
            if (wasBlocked && !guardBroken)
            {
                reaction = ImpactReactionType.Absorb;
            }

            float controlResistance = target.Stats?.GetImpactControlResistance() ?? 0f;
            float knockbackScale = GetKnockbackScale(reaction);
            float knockbackSpeed = Mathf.Max(0f, profile.KnockbackForce)
                * massFactor
                * momentumFactor
                * shatterFactor
                * (1f - controlResistance)
                * knockbackScale;

            Vector2 knockback = direction * knockbackSpeed;
            float reactionLockSeconds = ResolveReactionLockSeconds(profile, reaction);
            float launchHeight = ResolveLaunchHeight(profile, reaction, impactRatio);

            return new ImpactResolution
            {
                Reaction = reaction,
                ImpactPower = effectiveImpact,
                Stability = stability,
                ImpactRatio = impactRatio,
                MassFactor = massFactor,
                MomentumFactor = momentumFactor,
                ControlResistance = controlResistance,
                ClosingSpeed = closingSpeed,
                ReactionLockSeconds = reactionLockSeconds,
                LaunchHeight = launchHeight,
                LaunchDuration = Mathf.Max(0.05f, profile.LaunchDuration),
                KnockbackVelocity = knockback
            };
        }

        /// <summary>
        /// 0 giữ tương thích resource cũ: tự suy Impact từ hai field control đã tồn tại.
        /// Resource mới nên đặt ImpactPower rõ ràng để designer tune độc lập với damage.
        /// </summary>
        private static float ResolveBaseImpact(HitProfileData profile)
        {
            if (profile.ImpactPower > 0f)
            {
                return profile.ImpactPower;
            }

            return Mathf.Max(
                20f,
                Mathf.Max(0f, profile.PoiseDamage) * 2.2f
                + Mathf.Max(0f, profile.KnockbackForce) * 0.32f);
        }

        private static float ResolveTargetStability(
            CombatCharacter target,
            bool wasBlocked,
            bool guardBroken)
        {
            int defense = Mathf.Max(0, target.Stats?.GetAttributeValue(AttributeType.Defense) ?? 0);
            float maxPoise = Mathf.Max(0f, target.Stats?.MaxPoise ?? 0f);
            float currentPoise = Mathf.Clamp(target.Stats?.CurrentPoise ?? maxPoise, 0f, maxPoise);
            float poiseRatio = maxPoise > 0.001f ? currentPoise / maxPoise : 1f;

            // MaxPoise đã nhận đóng góp từ Vitality + Defense trong stat system.
            // DEF chỉ góp thêm một phần nhỏ ở đây, tránh double-dip quá mạnh.
            float baseStability = 40f + maxPoise * 0.55f + defense * 0.75f;
            float poiseState = Mathf.Lerp(0.68f, 1f, poiseRatio);

            CombatStateId state = target.StateMachine?.Current ?? CombatStateId.Locomotion;
            float stateFactor = state switch
            {
                CombatStateId.Blocking when !guardBroken => 1.25f,
                CombatStateId.Hitstun => 0.90f,
                CombatStateId.Stagger => 0.82f,
                CombatStateId.GuardBreak => 0.78f,
                _ => 1f
            };

            if (wasBlocked && !guardBroken)
            {
                stateFactor *= 1.15f;
            }

            return Mathf.Max(1f, baseStability * poiseState * stateFactor);
        }

        private static ImpactReactionType ResolveReaction(float ratio)
        {
            if (ratio < 0.45f)
            {
                return ImpactReactionType.Absorb;
            }
            if (ratio < 0.75f)
            {
                return ImpactReactionType.Flinch;
            }
            if (ratio < 1.00f)
            {
                return ImpactReactionType.Shove;
            }
            if (ratio < 1.30f)
            {
                return ImpactReactionType.Knockback;
            }
            if (ratio < 1.65f)
            {
                return ImpactReactionType.Stagger;
            }
            if (ratio < 2.05f)
            {
                return ImpactReactionType.Knockdown;
            }

            return ImpactReactionType.Launch;
        }

        private static ImpactReactionType ApplyCapabilityCaps(
            ImpactReactionType reaction,
            HitProfileData profile)
        {
            // Capability là cổng gameplay thật: có LaunchHeight nhưng CanLaunch=false thì hit vẫn
            // không được nhấc target. Nhờ vậy designer có thể giữ asset/tuning height mà khóa effect.
            bool canLaunch = profile.CanLaunch;
            bool canKnockdown = profile.CanKnockdown || canLaunch;

            if (reaction == ImpactReactionType.Launch && !canLaunch)
            {
                reaction = canKnockdown
                    ? ImpactReactionType.Knockdown
                    : ImpactReactionType.Stagger;
            }

            if (reaction == ImpactReactionType.Knockdown && !canKnockdown)
            {
                reaction = ImpactReactionType.Stagger;
            }

            return reaction;
        }

        private static float GetKnockbackScale(ImpactReactionType reaction)
        {
            return reaction switch
            {
                ImpactReactionType.Absorb => 0f,
                ImpactReactionType.Flinch => 0.30f,
                ImpactReactionType.Shove => 1.00f,
                ImpactReactionType.Knockback => 1.00f,
                ImpactReactionType.Stagger => 1.05f,
                ImpactReactionType.Knockdown => 1.12f,
                ImpactReactionType.Launch => 1.15f,
                _ => 1f
            };
        }

        private static float ResolveReactionLockSeconds(
            HitProfileData profile,
            ImpactReactionType reaction)
        {
            if ((int)reaction < (int)ImpactReactionType.Stagger)
            {
                return 0f;
            }

            float authored = Mathf.Max(0f, profile.ForcedStaggerSeconds);
            float fallback = reaction switch
            {
                ImpactReactionType.Knockdown => 0.58f,
                ImpactReactionType.Launch => 0.50f,
                _ => 0.42f
            };

            // Authored value chỉ nâng/giảm quanh mốc hợp lý; không ép mọi reaction thành cùng một duration.
            if (authored <= 0f)
            {
                return fallback;
            }

            if (reaction == ImpactReactionType.Stagger)
            {
                return Mathf.Max(0.08f, authored);
            }

            return Mathf.Max(fallback, authored);
        }

        private static float ResolveLaunchHeight(
            HitProfileData profile,
            ImpactReactionType reaction,
            float impactRatio)
        {
            if (reaction != ImpactReactionType.Launch || profile.LaunchHeight <= 0f)
            {
                return 0f;
            }

            // Ratio càng vượt ngưỡng launch thì lift càng rõ, nhưng clamp để asset không bay lố.
            float ratioScale = Mathf.Clamp(0.80f + (impactRatio - 2.05f) * 0.22f, 0.80f, 1.25f);
            return Mathf.Max(0f, profile.LaunchHeight) * ratioScale;
        }

        private static Vector2 ResolveDirection(
            HitRequest request,
            CombatCharacter attacker,
            CombatCharacter target)
        {
            Vector2 direction = request.AttackDirection;
            if (direction.LengthSquared() <= 0.001f)
            {
                direction = target.CombatCenter - attacker.CombatCenter;
            }

            if (direction.LengthSquared() <= 0.001f)
            {
                direction = attacker.FacingDirection;
            }

            return direction.LengthSquared() <= 0.001f
                ? Vector2.Right
                : direction.Normalized();
        }

        private static ImpactReactionType MaxReaction(
            ImpactReactionType a,
            ImpactReactionType b)
        {
            return (ImpactReactionType)Mathf.Max((int)a, (int)b);
        }
    }
}
