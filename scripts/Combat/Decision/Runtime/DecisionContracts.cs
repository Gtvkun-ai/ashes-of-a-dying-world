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

        bool HasLineOfSight(CombatCharacter self, CombatCharacter target);
    }

    public interface IThreatPredictor
    {
        ThreatAssessment EvaluateThreats(
            CombatCharacter self,
            CombatCharacter target,
            float targetDistance);
    }

    public interface ICombatActionScheduler
    {
        CombatIntent CurrentIntent { get; }
        float CurrentScore { get; }
        float CommitmentRemaining { get; }

        void Tick(float deltaSeconds);
        SchedulerDecision Resolve(DecisionTrace trace, in CombatSnapshot snapshot);
        void Reset();
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
