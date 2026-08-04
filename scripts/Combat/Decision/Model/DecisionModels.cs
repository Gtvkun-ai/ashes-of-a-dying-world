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
        public TacticalActionTag Tags { get; }
        public IReadOnlyDictionary<string, float> Factors { get; }

        public CandidateTrace(
            CombatIntent intent,
            bool feasible,
            float finalScore,
            StringName failureReason,
            TacticalActionTag tags,
            IReadOnlyDictionary<string, float> factors)
        {
            Intent = intent;
            Feasible = feasible;
            FinalScore = Mathf.Clamp(finalScore, 0f, 1f);
            FailureReason = failureReason;
            Tags = tags;
            Factors = factors ?? new Dictionary<string, float>();
        }
    }

    /// <summary>
    /// Kết quả evaluator. ChosenIntent ở đây là đề xuất chiến thuật;
    /// scheduler mới là nơi quyết định intent nào thực sự được giữ qua nhiều tick.
    /// </summary>
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

        public bool TryGetCandidate(CombatIntent intent, out CandidateTrace result)
        {
            for (int i = 0; i < Candidates.Count; i++)
            {
                CandidateTrace candidate = Candidates[i];
                if (candidate.Intent.Type == intent.Type
                    && candidate.Intent.ActionId == intent.ActionId)
                {
                    result = candidate;
                    return true;
                }
            }

            result = default;
            return false;
        }

        public float GetScore(CombatIntent intent)
        {
            return TryGetCandidate(intent, out CandidateTrace candidate) && candidate.Feasible
                ? candidate.FinalScore
                : 0f;
        }

        public string ToCompactString(int maxCandidates = 3, int maxRejected = 2)
        {
            var builder = new StringBuilder();
            builder.Append("target=").Append(Snapshot.TargetId?.ToString() ?? "none");
            builder.Append(" distance=").Append(Snapshot.TargetDistance.ToString("0.0"));
            builder.Append(" los=").Append(Snapshot.HasLineOfSight ? "yes" : "no");
            builder.Append(" threat=").Append(Snapshot.ThreatSeverity.ToString("0.00"));
            builder.Append(" leaderDanger=").Append(Snapshot.LeaderThreatened ? "yes" : "no");

            int count = Math.Min(Math.Max(0, maxCandidates), Candidates.Count);
            if (count > 0)
            {
                builder.Append(" top=[");
                int written = 0;
                for (int i = 0; i < Candidates.Count && written < count; i++)
                {
                    CandidateTrace candidate = Candidates[i];
                    if (!candidate.Feasible)
                    {
                        continue;
                    }

                    if (written > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(candidate.Intent.Type)
                        .Append(':')
                        .Append(candidate.FinalScore.ToString("0.00"));
                    written++;
                }
                builder.Append(']');
            }

            int rejectedWritten = 0;
            for (int i = 0; i < Candidates.Count && rejectedWritten < Math.Max(0, maxRejected); i++)
            {
                CandidateTrace candidate = Candidates[i];
                if (candidate.Feasible)
                {
                    continue;
                }

                builder.Append(rejectedWritten == 0 ? " rejected=[" : ", ")
                    .Append(candidate.Intent.Type)
                    .Append('(')
                    .Append(candidate.FailureReason)
                    .Append(')');
                rejectedWritten++;
            }
            if (rejectedWritten > 0)
            {
                builder.Append(']');
            }

            return builder.ToString();
        }

        public string ToDetailedString(int maxCandidates = 8)
        {
            var builder = new StringBuilder();
            builder.Append("state=").Append(Snapshot.SelfState)
                .Append(" hp=").Append(Snapshot.Health)
                .Append(" mp=").Append(Snapshot.Mana)
                .Append(" stamina=").Append(Snapshot.Stamina)
                .Append(" guard=").Append(Snapshot.Guard)
                .AppendLine();
            builder.AppendLine(ToCompactString(maxCandidates, maxCandidates));

            int count = Math.Min(Math.Max(0, maxCandidates), Candidates.Count);
            for (int i = 0; i < count; i++)
            {
                CandidateTrace candidate = Candidates[i];
                builder.Append("  ")
                    .Append(candidate.Intent)
                    .Append(" feasible=").Append(candidate.Feasible)
                    .Append(" score=").Append(candidate.FinalScore.ToString("0.000"));
                if (!candidate.Feasible)
                {
                    builder.Append(" reason=").Append(candidate.FailureReason);
                }

                foreach (KeyValuePair<string, float> factor in candidate.Factors)
                {
                    builder.Append(' ')
                        .Append(factor.Key)
                        .Append('=')
                        .Append(factor.Value.ToString("0.00"));
                }
                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Dấu vết scheduler: evaluator đề xuất gì, scheduler giữ gì và vì sao.
    /// </summary>
    public readonly struct SchedulerDecision
    {
        public CombatIntent ProposedIntent { get; }
        public CombatIntent CommittedIntent { get; }
        public float ProposedScore { get; }
        public float CommittedScore { get; }
        public bool DidSwitch { get; }
        public StringName ReasonKey { get; }
        public float CommitmentRemaining { get; }

        public SchedulerDecision(
            CombatIntent proposedIntent,
            CombatIntent committedIntent,
            float proposedScore,
            float committedScore,
            bool didSwitch,
            StringName reasonKey,
            float commitmentRemaining)
        {
            ProposedIntent = proposedIntent;
            CommittedIntent = committedIntent;
            ProposedScore = Mathf.Clamp(proposedScore, 0f, 1f);
            CommittedScore = Mathf.Clamp(committedScore, 0f, 1f);
            DidSwitch = didSwitch;
            ReasonKey = reasonKey;
            CommitmentRemaining = Mathf.Max(0f, commitmentRemaining);
        }

        public string ToCompactString()
        {
            return $"proposed={ProposedIntent}:{ProposedScore:0.00} "
                + $"committed={CommittedIntent}:{CommittedScore:0.00} "
                + $"lock={CommitmentRemaining:0.00}s switch={ReasonKey}";
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
