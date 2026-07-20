using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Profiles;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Utility evaluator chiến thuật. Hard gate loại hành động bất khả thi trước,
    /// response curve xử lý khoảng cách liên tục, scheduler ở lớp sau mới giữ commitment.
    /// </summary>
    public sealed class TacticalEvaluator : ITacticalEvaluator
    {
        public DecisionTrace Evaluate(
            in CombatSnapshot snapshot,
            CombatBlackboard blackboard,
            CombatRoleAssignment? assignment,
            CombatClassProfile classProfile,
            CombatDoctrineProfile doctrine,
            CombatPersonalityProfile personality,
            CombatEmotionState emotion)
        {
            CombatClassProfile safeClass = classProfile ?? new CombatClassProfile();
            CombatDoctrineProfile safeDoctrine = doctrine ?? new CombatDoctrineProfile();
            CombatPersonalityProfile safePersonality = personality ?? new CombatPersonalityProfile();
            safeClass.GetValidatedRanges(
                out float panicRange,
                out float unsafeRange,
                out float preferredMin,
                out float preferredMax,
                out float reacquireRange,
                out float rangeEdge);

            var candidates = new List<CandidateTrace>(9);
            if (!snapshot.HasTarget)
            {
                CombatIntent idle = CombatIntent.None(new StringName("no_target"));
                candidates.Add(BuildCandidate(
                    idle,
                    true,
                    1f,
                    string.Empty,
                    TacticalActionTag.LowCommitment,
                    new Dictionary<string, float> { ["target"] = 0f }));
                return BuildTrace(snapshot, idle, candidates, "Không có mục tiêu hợp lệ.");
            }

            float distance = snapshot.TargetDistance;
            float inBand = ResponseCurve.SmoothBand(distance, preferredMin, preferredMax, rangeEdge);
            float approachNeed = ResponseCurve.SmoothRamp(distance, preferredMax, reacquireRange);
            float closeNeed = ResponseCurve.InverseSmoothRamp(distance, panicRange, preferredMin);
            float unsafePressure = ResponseCurve.InverseSmoothRamp(distance, unsafeRange, preferredMin);
            float safety = ResponseCurve.InverseLinear(snapshot.ThreatSeverity);
            float targetExposure = snapshot.TargetInRecovery ? 1f : 0.55f;
            bool insidePanic = distance <= panicRange;
            bool insideUnsafe = distance < unsafeRange;

            AddHoldRangeCandidate(
                candidates,
                snapshot,
                blackboard,
                preferredMin,
                preferredMax,
                inBand,
                safety,
                insidePanic);
            AddApproachCandidate(
                candidates,
                snapshot,
                blackboard,
                safeDoctrine,
                preferredMin,
                preferredMax,
                approachNeed,
                insideUnsafe);
            AddBackpedalCandidate(
                candidates,
                snapshot,
                blackboard,
                safeDoctrine,
                safePersonality,
                emotion,
                preferredMin,
                preferredMax,
                closeNeed,
                insidePanic);
            AddStrafeCandidate(
                candidates,
                snapshot,
                blackboard,
                safeDoctrine,
                preferredMin,
                preferredMax,
                unsafePressure,
                safety,
                insidePanic);
            AddGuardCandidate(
                candidates,
                snapshot,
                safePersonality,
                preferredMin,
                preferredMax);

            SkillData primarySkill = safeClass.GetPrimarySkill();
            AddCastCandidate(
                candidates,
                snapshot,
                safeClass,
                safeDoctrine,
                primarySkill,
                preferredMin,
                preferredMax,
                inBand,
                safety,
                targetExposure,
                insideUnsafe);
            AddRecoverCandidate(
                candidates,
                snapshot,
                safeClass,
                safeDoctrine,
                primarySkill,
                preferredMin,
                preferredMax,
                unsafeRange,
                reacquireRange,
                rangeEdge,
                safety);
            AddProtectCandidate(
                candidates,
                snapshot,
                safeDoctrine,
                safePersonality,
                emotion,
                preferredMin,
                preferredMax,
                insidePanic);

            candidates.Sort(CompareCandidates);
            CombatIntent chosen = candidates.Count > 0 && candidates[0].Feasible
                ? candidates[0].Intent
                : CombatIntent.None(new StringName("no_feasible_candidate"));

            string summary = $"Đề xuất {chosen}; range={distance:0.0}, LOS={snapshot.HasLineOfSight}, threat={snapshot.ThreatSeverity:0.00}.";
            return BuildTrace(snapshot, chosen, candidates, summary);
        }

        private static void AddHoldRangeCandidate(
            ICollection<CandidateTrace> candidates,
            in CombatSnapshot snapshot,
            CombatBlackboard blackboard,
            float preferredMin,
            float preferredMax,
            float inBand,
            float safety,
            bool insidePanic)
        {
            CombatIntent intent = MakeIntent(
                CombatIntentType.HoldRange,
                snapshot,
                string.Empty,
                snapshot.TargetPosition,
                preferredMin,
                preferredMax,
                0.22f,
                "range_in_band");
            bool feasible = snapshot.CanMove && !insidePanic;
            float stability = GetStabilityBonus(blackboard, CombatIntentType.HoldRange);
            float score = Mathf.Clamp(inBand * (0.72f + 0.28f * safety) + stability, 0f, 1f);
            candidates.Add(BuildCandidate(
                intent,
                feasible,
                score,
                feasible ? string.Empty : "panic_or_movement_unavailable",
                TacticalActionTag.LowCommitment | TacticalActionTag.Mobility,
                new Dictionary<string, float>
                {
                    ["range_band"] = inBand,
                    ["safety"] = safety,
                    ["stability"] = stability
                }));
        }

        private static void AddApproachCandidate(
            ICollection<CandidateTrace> candidates,
            in CombatSnapshot snapshot,
            CombatBlackboard blackboard,
            CombatDoctrineProfile doctrine,
            float preferredMin,
            float preferredMax,
            float approachNeed,
            bool insideUnsafe)
        {
            CombatIntent intent = MakeIntent(
                CombatIntentType.Approach,
                snapshot,
                string.Empty,
                snapshot.TargetPosition,
                preferredMin,
                preferredMax,
                0.24f,
                "target_too_far");
            bool feasible = snapshot.CanMove && !insideUnsafe;
            float drive = Mathf.Clamp(
                0.58f
                + 0.20f * doctrine.Aggression
                + 0.22f * doctrine.MobilityPreference,
                0f,
                1f);
            float stability = GetStabilityBonus(blackboard, CombatIntentType.Approach);
            float score = Mathf.Clamp(approachNeed * drive + stability, 0f, 1f);
            candidates.Add(BuildCandidate(
                intent,
                feasible,
                score,
                feasible ? string.Empty : "too_close_or_movement_unavailable",
                TacticalActionTag.Mobility | TacticalActionTag.LowCommitment,
                new Dictionary<string, float>
                {
                    ["approach_need"] = approachNeed,
                    ["drive"] = drive,
                    ["stability"] = stability
                }));
        }

        private static void AddBackpedalCandidate(
            ICollection<CandidateTrace> candidates,
            in CombatSnapshot snapshot,
            CombatBlackboard blackboard,
            CombatDoctrineProfile doctrine,
            CombatPersonalityProfile personality,
            CombatEmotionState emotion,
            float preferredMin,
            float preferredMax,
            float closeNeed,
            bool insidePanic)
        {
            CombatIntent intent = MakeIntent(
                CombatIntentType.Backpedal,
                snapshot,
                string.Empty,
                snapshot.TargetPosition,
                preferredMin,
                preferredMax,
                0.24f,
                insidePanic ? "panic_spacing" : "target_too_close");
            bool feasible = snapshot.CanMove;
            float retreatBias = Mathf.Clamp(
                0.35f * doctrine.RetreatReadiness
                + 0.30f * personality.SelfPreservation
                + 0.20f * personality.Discipline
                + 0.15f * emotion.Stress,
                0f,
                1f);
            float stability = GetStabilityBonus(blackboard, CombatIntentType.Backpedal);
            float score = closeNeed * (0.66f + 0.34f * retreatBias)
                + snapshot.ThreatSeverity * 0.12f
                + stability;
            if (insidePanic)
            {
                // Panic radius là hard tactical priority, không để RecoverResources thắng vì vài phần trăm.
                score = Mathf.Max(score, 0.84f + 0.14f * snapshot.ThreatSeverity);
            }
            score = Mathf.Clamp(score, 0f, 1f);

            candidates.Add(BuildCandidate(
                intent,
                feasible,
                score,
                feasible ? string.Empty : "movement_unavailable",
                TacticalActionTag.Escape | TacticalActionTag.Mobility | TacticalActionTag.LowCommitment,
                new Dictionary<string, float>
                {
                    ["close_need"] = closeNeed,
                    ["retreat_bias"] = retreatBias,
                    ["panic"] = insidePanic ? 1f : 0f,
                    ["threat"] = snapshot.ThreatSeverity,
                    ["stability"] = stability
                }));
        }

        private static void AddStrafeCandidate(
            ICollection<CandidateTrace> candidates,
            in CombatSnapshot snapshot,
            CombatBlackboard blackboard,
            CombatDoctrineProfile doctrine,
            float preferredMin,
            float preferredMax,
            float unsafePressure,
            float safety,
            bool insidePanic)
        {
            CombatIntentType type = blackboard.OrbitSide < 0
                ? CombatIntentType.StrafeLeft
                : CombatIntentType.StrafeRight;
            CombatIntent intent = MakeIntent(
                type,
                snapshot,
                string.Empty,
                snapshot.TargetPosition,
                preferredMin,
                preferredMax,
                0.30f,
                "unsafe_cast_band");
            bool feasible = snapshot.CanMove && unsafePressure > 0.001f && !insidePanic;
            float mobilityBias = Mathf.Clamp(
                0.55f + 0.25f * doctrine.MobilityPreference + 0.20f * doctrine.RangeDiscipline,
                0f,
                1f);
            float stability = GetStabilityBonus(blackboard, type);
            float score = Mathf.Clamp(
                unsafePressure * mobilityBias * (0.62f + 0.38f * safety) + stability,
                0f,
                1f);
            candidates.Add(BuildCandidate(
                intent,
                feasible,
                score,
                feasible ? string.Empty : (insidePanic
                    ? "panic_requires_direct_escape"
                    : "outside_strafe_band_or_movement_unavailable"),
                TacticalActionTag.Mobility | TacticalActionTag.LowCommitment,
                new Dictionary<string, float>
                {
                    ["unsafe_pressure"] = unsafePressure,
                    ["mobility_bias"] = mobilityBias,
                    ["safety"] = safety,
                    ["orbit_side"] = blackboard.OrbitSide,
                    ["panic"] = insidePanic ? 1f : 0f
                }));
        }

        private static void AddGuardCandidate(
            ICollection<CandidateTrace> candidates,
            in CombatSnapshot snapshot,
            CombatPersonalityProfile personality,
            float preferredMin,
            float preferredMax)
        {
            CombatIntent intent = MakeIntent(
                CombatIntentType.Guard,
                snapshot,
                string.Empty,
                snapshot.TargetPosition,
                preferredMin,
                preferredMax,
                0.18f,
                "incoming_threat");
            bool feasible = snapshot.CanBlock && snapshot.ThreatBlockable;
            float etaUrgency = snapshot.ThreatEtaSeconds <= 0.001f
                ? 0f
                : ResponseCurve.InverseSmoothRamp(snapshot.ThreatEtaSeconds, 0.08f, 0.65f);
            float score = Mathf.Clamp(
                snapshot.ThreatSeverity
                * (0.58f + 0.22f * personality.Discipline + 0.20f * personality.SelfPreservation)
                * (0.72f + 0.28f * etaUrgency),
                0f,
                1f);
            candidates.Add(BuildCandidate(
                intent,
                feasible,
                score,
                ResolveGuardFailure(snapshot),
                TacticalActionTag.Defensive | TacticalActionTag.LowCommitment,
                new Dictionary<string, float>
                {
                    ["threat"] = snapshot.ThreatSeverity,
                    ["eta_urgency"] = etaUrgency,
                    ["guard_resource"] = snapshot.GuardRatio,
                    ["blockable"] = snapshot.ThreatBlockable ? 1f : 0f
                }));
        }

        private static void AddCastCandidate(
            ICollection<CandidateTrace> candidates,
            in CombatSnapshot snapshot,
            CombatClassProfile classProfile,
            CombatDoctrineProfile doctrine,
            SkillData primarySkill,
            float preferredMin,
            float preferredMax,
            float inBand,
            float safety,
            float targetExposure,
            bool insideUnsafe)
        {
            bool hasSkill = primarySkill != null;
            float staminaCost = hasSkill
                ? Mathf.Max(0f, primarySkill.StaminaCost)
                    + Mathf.Max(0f, primarySkill.CombatAction?.StaminaCost ?? 0f)
                : 0f;
            bool enoughMana = !hasSkill || primarySkill.ManaCost <= 0 || snapshot.Mana.CanAfford(primarySkill.ManaCost);
            bool enoughStamina = !hasSkill || staminaCost <= 0f || snapshot.Stamina.CanAfford(staminaCost);
            bool castFeasible = hasSkill
                && snapshot.HasLineOfSight
                && enoughMana
                && enoughStamina
                && snapshot.CanStartAction
                && !insideUnsafe;
            string castFailure = ResolveCastFailure(
                hasSkill,
                snapshot.HasLineOfSight,
                enoughMana,
                enoughStamina,
                snapshot.CanStartAction,
                insideUnsafe);
            CombatIntent intent = MakeIntent(
                CombatIntentType.CastPrimary,
                snapshot,
                primarySkill?.SkillId ?? string.Empty,
                snapshot.TargetPosition,
                preferredMin,
                preferredMax,
                0.46f,
                "primary_skill_window");
            float manaReadiness = !classProfile.UsesMana || !snapshot.Mana.HasPool
                ? 1f
                : ResponseCurve.Logistic(snapshot.ManaRatio, 0.25f, 12f);
            float staminaReadiness = !classProfile.UsesStamina || !snapshot.Stamina.HasPool
                ? 1f
                : ResponseCurve.Logistic(snapshot.StaminaRatio, 0.18f, 10f);
            float score = ResponseCurve.WeightedGeometricMean(
                new[]
                {
                    Mathf.Max(0.001f, inBand),
                    snapshot.HasLineOfSight ? 1f : 0.001f,
                    manaReadiness,
                    staminaReadiness,
                    safety,
                    targetExposure
                },
                new[] { 1.35f, 1.1f, 0.85f, 0.5f, 1.1f, 0.8f });
            score *= Mathf.Lerp(0.68f, 1f, doctrine.RangeDiscipline);

            candidates.Add(BuildCandidate(
                intent,
                castFeasible,
                score,
                castFailure,
                TacticalActionTag.Damage
                    | TacticalActionTag.Control
                    | TacticalActionTag.Projectile
                    | TacticalActionTag.RequiresLos
                    | TacticalActionTag.RequiresRangeBand
                    | TacticalActionTag.LowCommitment,
                new Dictionary<string, float>
                {
                    ["range_band"] = inBand,
                    ["los"] = snapshot.HasLineOfSight ? 1f : 0f,
                    ["mana_ready"] = manaReadiness,
                    ["stamina_ready"] = staminaReadiness,
                    ["safety"] = safety,
                    ["target_exposure"] = targetExposure
                }));
        }

        private static void AddRecoverCandidate(
            ICollection<CandidateTrace> candidates,
            in CombatSnapshot snapshot,
            CombatClassProfile classProfile,
            CombatDoctrineProfile doctrine,
            SkillData primarySkill,
            float preferredMin,
            float preferredMax,
            float unsafeRange,
            float reacquireRange,
            float rangeEdge,
            float safety)
        {
            bool manaRelevant = classProfile.UsesMana
                && primarySkill != null
                && primarySkill.ManaCost > 0
                && snapshot.Mana.HasPool;
            bool staminaRelevant = classProfile.UsesStamina && snapshot.Stamina.HasPool;
            bool canRecoverMana = manaRelevant && classProfile.CanRecoverManaPassively;
            bool canRecoverStamina = staminaRelevant && classProfile.CanRecoverStaminaPassively;

            float manaNeed = canRecoverMana
                ? ResourceNeed(snapshot.ManaRatio, classProfile.LowManaRatio)
                : 0f;
            float staminaNeed = canRecoverStamina
                ? ResourceNeed(snapshot.StaminaRatio, classProfile.LowStaminaRatio) * 0.65f
                : 0f;
            float resourceNeed = Mathf.Max(manaNeed, staminaNeed);
            bool resourcesLow = resourceNeed > 0.001f;
            bool hasRecoveryMechanic = canRecoverMana || canRecoverStamina;
            bool safeToRecover = snapshot.TargetDistance >= unsafeRange
                && snapshot.ThreatSeverity <= 0.20f
                && snapshot.CanMove
                && !snapshot.LeaderThreatened;
            float positionReadiness = ResponseCurve.SmoothBand(
                snapshot.TargetDistance,
                unsafeRange,
                reacquireRange,
                rangeEdge);

            CombatIntent intent = MakeIntent(
                CombatIntentType.RecoverResources,
                snapshot,
                string.Empty,
                snapshot.TargetPosition,
                preferredMin,
                preferredMax,
                0.34f,
                "resource_low_safe_window");
            bool feasible = resourcesLow && hasRecoveryMechanic && safeToRecover;
            float score = Mathf.Clamp(
                resourceNeed
                * positionReadiness
                * safety
                * Mathf.Lerp(0.58f, 0.92f, doctrine.ResourceConservation),
                0f,
                1f);

            bool criticalMana = canRecoverMana && snapshot.ManaRatio <= classProfile.CriticalManaRatio;
            bool criticalStamina = canRecoverStamina && snapshot.StaminaRatio <= classProfile.CriticalStaminaRatio;
            if (feasible && (criticalMana || criticalStamina))
            {
                score = Mathf.Max(score, criticalMana ? 0.82f : 0.62f);
            }

            candidates.Add(BuildCandidate(
                intent,
                feasible,
                score,
                ResolveRecoverFailure(resourcesLow, hasRecoveryMechanic, safeToRecover, snapshot.LeaderThreatened),
                TacticalActionTag.Recover | TacticalActionTag.LowCommitment,
                new Dictionary<string, float>
                {
                    ["mana_need"] = manaNeed,
                    ["stamina_need"] = staminaNeed,
                    ["resource_need"] = resourceNeed,
                    ["position_ready"] = positionReadiness,
                    ["safety"] = safety,
                    ["has_recovery"] = hasRecoveryMechanic ? 1f : 0f
                }));
        }

        private static void AddProtectCandidate(
            ICollection<CandidateTrace> candidates,
            in CombatSnapshot snapshot,
            CombatDoctrineProfile doctrine,
            CombatPersonalityProfile personality,
            CombatEmotionState emotion,
            float preferredMin,
            float preferredMax,
            bool insidePanic)
        {
            CombatIntent intent = MakeIntent(
                CombatIntentType.ProtectLeader,
                snapshot,
                string.Empty,
                snapshot.HasLeader ? snapshot.LeaderPosition : snapshot.TargetPosition,
                preferredMin,
                preferredMax,
                0.34f,
                "leader_threatened");
            bool feasible = snapshot.HasLeader && snapshot.LeaderThreatened && snapshot.CanMove;
            float protectAffinity = Mathf.Clamp(
                0.45f * doctrine.LeaderProtection
                + 0.35f * personality.Protectiveness
                + 0.20f * emotion.Protectiveness,
                0f,
                1f);
            float score = snapshot.LeaderThreatened
                ? Mathf.Clamp(0.72f + 0.28f * protectAffinity, 0f, 1f)
                : 0f;
            if (insidePanic)
            {
                // Hyou bảo vệ leader nhưng không được tự sát ngu ngốc khi quái đang dí sát mặt.
                score *= 0.76f;
            }

            candidates.Add(BuildCandidate(
                intent,
                feasible,
                score,
                feasible ? string.Empty : "leader_not_threatened_or_movement_unavailable",
                TacticalActionTag.Protect | TacticalActionTag.Control | TacticalActionTag.Mobility,
                new Dictionary<string, float>
                {
                    ["leader_danger"] = snapshot.LeaderThreatened ? 1f : 0f,
                    ["protect_affinity"] = protectAffinity,
                    ["self_panic"] = insidePanic ? 1f : 0f
                }));
        }

        private static CombatIntent MakeIntent(
            CombatIntentType type,
            in CombatSnapshot snapshot,
            string actionId,
            Vector2 desiredAnchor,
            float preferredMin,
            float preferredMax,
            float commitment,
            string reason)
        {
            CombatInterruptMask interruptMask = CombatInterruptMask.Dead
                | CombatInterruptMask.Hitstun
                | CombatInterruptMask.GuardBreak
                | CombatInterruptMask.TargetInvalid;
            if (type != CombatIntentType.PanicEvade)
            {
                interruptMask |= CombatInterruptMask.EmergencyEvade;
            }

            return new CombatIntent(
                type,
                new StringName(actionId ?? string.Empty),
                snapshot.TargetId,
                desiredAnchor,
                preferredMin,
                preferredMax,
                commitment,
                interruptMask,
                new StringName(reason));
        }

        private static CandidateTrace BuildCandidate(
            CombatIntent intent,
            bool feasible,
            float score,
            string failureReason,
            TacticalActionTag tags,
            IReadOnlyDictionary<string, float> factors)
        {
            return new CandidateTrace(
                intent,
                feasible,
                feasible ? score : 0f,
                new StringName(failureReason ?? string.Empty),
                tags,
                factors);
        }

        private static DecisionTrace BuildTrace(
            in CombatSnapshot snapshot,
            CombatIntent chosen,
            IReadOnlyList<CandidateTrace> candidates,
            string summary)
        {
            return new DecisionTrace(snapshot, chosen, candidates, summary);
        }

        private static float GetStabilityBonus(CombatBlackboard blackboard, CombatIntentType type)
        {
            return blackboard.CurrentIntent.HasValue && blackboard.CurrentIntent.Value.Type == type
                ? 0.06f
                : 0f;
        }

        private static float ResourceNeed(float ratio, float lowThreshold)
        {
            float threshold = Mathf.Clamp(lowThreshold, 0.01f, 1f);
            return ratio >= threshold
                ? 0f
                : Mathf.Clamp((threshold - ratio) / threshold, 0f, 1f);
        }

        private static int CompareCandidates(CandidateTrace left, CandidateTrace right)
        {
            if (left.Feasible != right.Feasible)
            {
                return left.Feasible ? -1 : 1;
            }

            return right.FinalScore.CompareTo(left.FinalScore);
        }

        private static string ResolveGuardFailure(in CombatSnapshot snapshot)
        {
            if (!snapshot.CanBlock)
            {
                return "guard_unavailable";
            }
            if (!snapshot.ThreatBlockable)
            {
                return "threat_not_blockable";
            }
            return string.Empty;
        }

        private static string ResolveCastFailure(
            bool hasSkill,
            bool hasLineOfSight,
            bool enoughMana,
            bool enoughStamina,
            bool canStartAction,
            bool insideUnsafe)
        {
            if (!hasSkill)
            {
                return "class_has_no_granted_skill";
            }
            if (insideUnsafe)
            {
                return "inside_unsafe_cast_range";
            }
            if (!hasLineOfSight)
            {
                return "line_of_sight_blocked";
            }
            if (!enoughMana)
            {
                return "mana_unavailable";
            }
            if (!enoughStamina)
            {
                return "stamina_unavailable";
            }
            if (!canStartAction)
            {
                return "state_blocks_cast";
            }

            return string.Empty;
        }

        private static string ResolveRecoverFailure(
            bool resourcesLow,
            bool hasRecoveryMechanic,
            bool safeToRecover,
            bool leaderThreatened)
        {
            if (!hasRecoveryMechanic)
            {
                return "no_recovery_mechanic";
            }
            if (!resourcesLow)
            {
                return "resources_above_low_threshold";
            }
            if (leaderThreatened)
            {
                return "leader_needs_protection";
            }
            if (!safeToRecover)
            {
                return "unsafe_to_recover";
            }
            return string.Empty;
        }
    }
}
