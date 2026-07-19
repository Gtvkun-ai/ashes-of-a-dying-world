using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Decision.Model
{
    public readonly struct ActionCandidate
    {
        public CombatIntent Intent { get; }
        public float Score { get; }
        public bool Feasible { get; }
        public StringName FailureReason { get; }
        public TacticalActionTag Tags { get; }

        public ActionCandidate(
            CombatIntent intent,
            float score,
            bool feasible,
            StringName failureReason,
            TacticalActionTag tags)
        {
            Intent = intent;
            Score = Mathf.Clamp(score, 0f, 1f);
            Feasible = feasible;
            FailureReason = failureReason;
            Tags = tags;
        }
    }

    public readonly struct CandidateTrace
    {
        public CombatIntent Intent { get; }
        public bool Feasible { get; }
        public float FinalScore { get; }
        public StringName FailureReason { get; }
        public IReadOnlyDictionary<string, float> Factors { get; }

        public CandidateTrace(
            CombatIntent intent,
            bool feasible,
            float finalScore,
            StringName failureReason,
            IReadOnlyDictionary<string, float> factors)
        {
            Intent = intent;
            Feasible = feasible;
            FinalScore = Mathf.Clamp(finalScore, 0f, 1f);
            FailureReason = failureReason;
            Factors = factors ?? new Dictionary<string, float>();
        }
    }

    public sealed class DecisionTrace
    {
        public CombatSnapshot Snapshot { get; }
        public CombatIntent ChosenIntent { get; }
        public IReadOnlyList<CandidateTrace> Candidates { get; }
        public string Summary { get; }

        public DecisionTrace(
            in CombatSnapshot snapshot,
            CombatIntent chosenIntent,
            IReadOnlyList<CandidateTrace> candidates,
            string summary)
        {
            Snapshot = snapshot;
            ChosenIntent = chosenIntent;
            Candidates = candidates ?? Array.Empty<CandidateTrace>();
            Summary = summary ?? string.Empty;
        }

        public string ToCompactString(int maxCandidates = 3)
        {
            var builder = new StringBuilder();
            builder.Append("intent=").Append(ChosenIntent);
            builder.Append(" target=").Append(Snapshot.TargetId?.ToString() ?? "none");
            builder.Append(" distance=").Append(Snapshot.TargetDistance.ToString("0.0"));
            builder.Append(" threat=").Append(Snapshot.ThreatSeverity.ToString("0.00"));

            int count = Math.Min(Math.Max(0, maxCandidates), Candidates.Count);
            if (count > 0)
            {
                builder.Append(" top=[");
                for (int i = 0; i < count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    CandidateTrace candidate = Candidates[i];
                    builder.Append(candidate.Intent.Type)
                        .Append(':')
                        .Append(candidate.FinalScore.ToString("0.00"));
                }
                builder.Append(']');
            }

            for (int i = 0; i < Candidates.Count; i++)
            {
                CandidateTrace candidate = Candidates[i];
                if (candidate.Feasible)
                {
                    continue;
                }

                builder.Append(" rejected=")
                    .Append(candidate.Intent.Type)
                    .Append('(')
                    .Append(candidate.FailureReason)
                    .Append(')');
                break;
            }

            return builder.ToString();
        }
    }

    public readonly struct ThreatAssessment
    {
        public float EtaSeconds { get; }
        public float Severity { get; }
        public Vector2 IncomingDirection { get; }
        public bool Blockable { get; }
        public bool Dodgeable { get; }

        public ThreatAssessment(
            float etaSeconds,
            float severity,
            Vector2 incomingDirection,
            bool blockable,
            bool dodgeable)
        {
            EtaSeconds = Mathf.Max(0f, etaSeconds);
            Severity = Mathf.Clamp(severity, 0f, 1f);
            IncomingDirection = incomingDirection;
            Blockable = blockable;
            Dodgeable = dodgeable;
        }

        public static ThreatAssessment None => new ThreatAssessment(0f, 0f, Vector2.Zero, false, false);
    }

    public readonly struct CombatRoleAssignment
    {
        public CombatRoleId PrimaryRole { get; }
        public CombatRoleId SecondaryRole { get; }
        public CombatCharacter PriorityTarget { get; }
        public CombatCharacter ProtectedActor { get; }
        public Vector2 AnchorPosition { get; }
        public float HoldSeconds { get; }

        public CombatRoleAssignment(
            CombatRoleId primaryRole,
            CombatRoleId secondaryRole,
            CombatCharacter priorityTarget,
            CombatCharacter protectedActor,
            Vector2 anchorPosition,
            float holdSeconds)
        {
            PrimaryRole = primaryRole;
            SecondaryRole = secondaryRole;
            PriorityTarget = priorityTarget;
            ProtectedActor = protectedActor;
            AnchorPosition = anchorPosition;
            HoldSeconds = Mathf.Max(0f, holdSeconds);
        }
    }

    public readonly struct CombatEmotionState
    {
        public float Stress { get; }
        public float Confidence { get; }
        public float Protectiveness { get; }

        public CombatEmotionState(float stress, float confidence, float protectiveness)
        {
            Stress = Mathf.Clamp(stress, 0f, 1f);
            Confidence = Mathf.Clamp(confidence, 0f, 1f);
            Protectiveness = Mathf.Clamp(protectiveness, 0f, 1f);
        }
    }
}
