using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Profiles;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    public interface ICombatPerception
    {
        CombatSnapshot BuildSnapshot(
            CombatCharacter self,
            CombatCharacter leader,
            CombatRoleAssignment? assignment,
            CombatBlackboard blackboard,
            float timeSeconds);
    }

    public interface IThreatPredictor
    {
        ThreatAssessment EvaluateThreats(
            CombatCharacter self,
            CombatCharacter target,
            float targetDistance);
    }

    public interface ITacticalEvaluator
    {
        DecisionTrace Evaluate(
            in CombatSnapshot snapshot,
            CombatBlackboard blackboard,
            CombatRoleAssignment? assignment,
            CombatClassProfile classProfile,
            CombatDoctrineProfile doctrine,
            CombatPersonalityProfile personality,
            CombatEmotionState emotion);
    }
}
