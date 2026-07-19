using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Profiles;
using AshesofaDyingWorld.Combat.Model;
using AshesofaDyingWorld.Core.Data;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Evaluator foundation: đủ để shadow trace range/threat/resource nhưng chưa thực thi intent.
    /// Các score đều có factor rõ ràng để sau này tune, không giấu một rừng if trong HyouAI mới.
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

            float preferredMin = Mathf.Max(1f, safeClass.PreferredMinRange);
            float preferredMax = Mathf.Max(preferredMin + 1f, safeClass.PreferredMaxRange);
            float rangeEdge = Mathf.Max(4f, (preferredMax - preferredMin) * 0.3f);

            var candidates = new List<CandidateTrace>(7);
            if (!snapshot.HasTarget)
            {
                CombatIntent idle = CombatIntent.None(new StringName("no_target"));
                candidates.Add(BuildCandidate(
                    idle,
                    true,
                    1f,
                    string.Empty,
                    new Dictionary<string, float> { ["target"] = 0f }));
                blackboard.CurrentIntent = idle;
                blackboard.CurrentAnchor = Vector2.Zero;
                return BuildTrace(snapshot, idle, candidates, "Không có mục tiêu hợp lệ.");
            }

            float distance = snapshot.TargetDistance;
            float inBand = ResponseCurve.SmoothBand(distance, preferredMin, preferredMax, rangeEdge);
            float tooClose = 1f - Mathf.Clamp(distance / preferredMin, 0f, 1f);
            float tooFar = Mathf.Clamp((distance - preferredMax) / Mathf.Max(preferredMax, 1f), 0f, 1f);
            float safety = ResponseCurve.InverseLinear(snapshot.ThreatSeverity);
            float targetExposure = snapshot.TargetInRecovery ? 1f : 0.55f;
            float stability = GetStabilityBonus(blackboard, CombatIntentType.HoldRange);

            CombatIntent holdRange = MakeIntent(
                CombatIntentType.HoldRange,
                snapshot,
                string.Empty,
                preferredMin,
                preferredMax,
                0.22f,
                "range_in_band");
            float holdScore = Mathf.Clamp(
                inBand * (0.55f + 0.45f * safety) + stability,
                0f,
                1f);
            candidates.Add(BuildCandidate(
                holdRange,
                true,
                holdScore,
                string.Empty,
                new Dictionary<string, float>
                {
                    ["range_band"] = inBand,
                    ["safety"] = safety,
                    ["stability"] = stability
                }));

            CombatIntent approach = MakeIntent(
                CombatIntentType.Approach,
                snapshot,
                string.Empty,
                preferredMin,
                preferredMax,
                0.24f,
                "target_too_far");
            float approachScore = Mathf.Clamp(
                tooFar * (0.45f + 0.35f * safeDoctrine.Aggression + 0.20f * safeDoctrine.MobilityPreference)
                + GetStabilityBonus(blackboard, CombatIntentType.Approach),
                0f,
                1f);
            candidates.Add(BuildCandidate(
                approach,
                true,
                approachScore,
                string.Empty,
                new Dictionary<string, float>
                {
                    ["too_far"] = tooFar,
                    ["aggression"] = safeDoctrine.Aggression,
                    ["mobility"] = safeDoctrine.MobilityPreference
                }));

            CombatIntent backpedal = MakeIntent(
                CombatIntentType.Backpedal,
                snapshot,
                string.Empty,
                preferredMin,
                preferredMax,
                0.22f,
                "target_too_close");
            float retreatBias = Mathf.Clamp(
                0.35f * safeDoctrine.RetreatReadiness
                + 0.35f * safePersonality.SelfPreservation
                + 0.30f * emotion.Stress,
                0f,
                1f);
            float backpedalScore = Mathf.Clamp(
                tooClose * (0.55f + 0.45f * retreatBias)
                + GetStabilityBonus(blackboard, CombatIntentType.Backpedal),
                0f,
                1f);
            candidates.Add(BuildCandidate(
                backpedal,
                true,
                backpedalScore,
                string.Empty,
                new Dictionary<string, float>
                {
                    ["too_close"] = tooClose,
                    ["retreat_bias"] = retreatBias,
                    ["threat"] = snapshot.ThreatSeverity
                }));

            bool guardFeasible = snapshot.GuardRatio > 0.02f
                && (snapshot.SelfState == CombatStateId.Locomotion
                    || snapshot.SelfState == CombatStateId.Blocking);
            CombatIntent guard = MakeIntent(
                CombatIntentType.Guard,
                snapshot,
                string.Empty,
                preferredMin,
                preferredMax,
                0.16f,
                "incoming_threat");
            float guardScore = Mathf.Clamp(
                snapshot.ThreatSeverity
                * (0.55f + 0.25f * safePersonality.Discipline + 0.20f * safePersonality.SelfPreservation),
                0f,
                1f);
            candidates.Add(BuildCandidate(
                guard,
                guardFeasible,
                guardScore,
                guardFeasible ? string.Empty : "guard_unavailable",
                new Dictionary<string, float>
                {
                    ["threat"] = snapshot.ThreatSeverity,
                    ["guard_resource"] = snapshot.GuardRatio,
                    ["discipline"] = safePersonality.Discipline
                }));

            SkillData primarySkill = safeClass.GetPrimarySkill();
            bool hasSkill = primarySkill != null;
            bool enoughMana = !safeClass.UsesMana
                || primarySkill == null
                || snapshot.ManaRatio > 0.01f;
            bool castFeasible = hasSkill
                && snapshot.HasLineOfSight
                && enoughMana
                && snapshot.SelfState == CombatStateId.Locomotion;
            string castFailure = ResolveCastFailure(hasSkill, snapshot.HasLineOfSight, enoughMana, snapshot.SelfState);
            CombatIntent castPrimary = MakeIntent(
                CombatIntentType.CastPrimary,
                snapshot,
                primarySkill?.SkillId ?? string.Empty,
                preferredMin,
                preferredMax,
                0.46f,
                "primary_skill_window");
            float manaReadiness = safeClass.UsesMana
                ? ResponseCurve.Logistic(snapshot.ManaRatio, 0.25f, 12f)
                : 1f;
            float castScore = ResponseCurve.WeightedGeometricMean(
                new[] { Mathf.Max(0.001f, inBand), snapshot.HasLineOfSight ? 1f : 0.001f, manaReadiness, safety, targetExposure },
                new[] { 1.25f, 1.1f, 0.8f, 1.1f, 0.8f });
            castScore *= Mathf.Lerp(0.65f, 1f, safeDoctrine.RangeDiscipline);
            candidates.Add(BuildCandidate(
                castPrimary,
                castFeasible,
                castScore,
                castFailure,
                new Dictionary<string, float>
                {
                    ["range_band"] = inBand,
                    ["los"] = snapshot.HasLineOfSight ? 1f : 0f,
                    ["mana_ready"] = manaReadiness,
                    ["safety"] = safety,
                    ["target_exposure"] = targetExposure
                }));

            bool resourcesLow = (safeClass.UsesMana && snapshot.ManaRatio < 0.22f)
                || (safeClass.UsesStamina && snapshot.StaminaRatio < 0.2f);
            bool recoverFeasible = resourcesLow && snapshot.ThreatSeverity < 0.72f;
            CombatIntent recover = MakeIntent(
                CombatIntentType.RecoverResources,
                snapshot,
                string.Empty,
                preferredMin,
                preferredMax,
                0.32f,
                "resource_low");
            float resourceNeed = Mathf.Max(
                safeClass.UsesMana ? 1f - snapshot.ManaRatio : 0f,
                safeClass.UsesStamina ? 1f - snapshot.StaminaRatio : 0f);
            float recoverScore = Mathf.Clamp(
                resourceNeed
                * safety
                * Mathf.Lerp(0.55f, 1f, safeDoctrine.ResourceConservation),
                0f,
                1f);
            candidates.Add(BuildCandidate(
                recover,
                recoverFeasible,
                recoverScore,
                recoverFeasible ? string.Empty : "resources_or_safety_gate",
                new Dictionary<string, float>
                {
                    ["resource_need"] = resourceNeed,
                    ["safety"] = safety,
                    ["conservation"] = safeDoctrine.ResourceConservation
                }));

            bool protectFeasible = snapshot.HasLeader && snapshot.LeaderThreatened;
            CombatIntent protect = MakeIntent(
                CombatIntentType.ProtectLeader,
                snapshot,
                string.Empty,
                preferredMin,
                preferredMax,
                0.30f,
                "leader_threatened");
            float protectScore = Mathf.Clamp(
                (snapshot.LeaderThreatened ? 1f : 0f)
                * (0.5f * safeDoctrine.LeaderProtection
                    + 0.3f * safePersonality.Protectiveness
                    + 0.2f * emotion.Protectiveness),
                0f,
                1f);
            candidates.Add(BuildCandidate(
                protect,
                protectFeasible,
                protectScore,
                protectFeasible ? string.Empty : "leader_not_threatened",
                new Dictionary<string, float>
                {
                    ["leader_danger"] = snapshot.LeaderThreatened ? 1f : 0f,
                    ["doctrine_protect"] = safeDoctrine.LeaderProtection,
                    ["personality_protect"] = safePersonality.Protectiveness
                }));

            candidates.Sort(CompareCandidates);
            CombatIntent chosen = candidates.Count > 0 && candidates[0].Feasible
                ? candidates[0].Intent
                : CombatIntent.None(new StringName("no_feasible_candidate"));
            blackboard.CurrentIntent = chosen;
            blackboard.CurrentAnchor = chosen.DesiredAnchor;

            string summary = $"Chọn {chosen}; range={distance:0.0}, LOS={snapshot.HasLineOfSight}, threat={snapshot.ThreatSeverity:0.00}.";
            return BuildTrace(snapshot, chosen, candidates, summary);
        }

        private static CombatIntent MakeIntent(
            CombatIntentType type,
            in CombatSnapshot snapshot,
            string actionId,
            float preferredMin,
            float preferredMax,
            float commitment,
            string reason)
        {
            return new CombatIntent(
                type,
                new StringName(actionId ?? string.Empty),
                snapshot.TargetId,
                snapshot.TargetPosition,
                preferredMin,
                preferredMax,
                commitment,
                CombatInterruptMask.Dead
                    | CombatInterruptMask.Hitstun
                    | CombatInterruptMask.GuardBreak
                    | CombatInterruptMask.TargetInvalid,
                new StringName(reason));
        }

        private static CandidateTrace BuildCandidate(
            CombatIntent intent,
            bool feasible,
            float score,
            string failureReason,
            IReadOnlyDictionary<string, float> factors)
        {
            return new CandidateTrace(
                intent,
                feasible,
                feasible ? score : 0f,
                new StringName(failureReason ?? string.Empty),
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
                ? 0.08f
                : 0f;
        }

        private static int CompareCandidates(CandidateTrace left, CandidateTrace right)
        {
            if (left.Feasible != right.Feasible)
            {
                return left.Feasible ? -1 : 1;
            }

            return right.FinalScore.CompareTo(left.FinalScore);
        }

        private static string ResolveCastFailure(
            bool hasSkill,
            bool hasLineOfSight,
            bool enoughMana,
            CombatStateId selfState)
        {
            if (!hasSkill)
            {
                return "class_has_no_granted_skill";
            }
            if (!hasLineOfSight)
            {
                return "line_of_sight_blocked";
            }
            if (!enoughMana)
            {
                return "mana_unavailable";
            }
            if (selfState != CombatStateId.Locomotion)
            {
                return "state_blocks_cast";
            }

            return string.Empty;
        }
    }
}
