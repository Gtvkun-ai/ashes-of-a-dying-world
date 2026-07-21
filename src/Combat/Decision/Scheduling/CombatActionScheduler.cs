using Godot;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Runtime;
using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Decision.Scheduling
{
    /// <summary>
    /// Scheduler giữ commitment và hysteresis giữa các lần evaluator chấm điểm.
    /// Nó không execute mechanics; nhiệm vụ duy nhất là ngăn AI đổi ý mỗi physics tick.
    /// </summary>
    public sealed class CombatActionScheduler : ICombatActionScheduler
    {
        private readonly float _switchScoreMargin;
        private readonly float _emergencyScoreMargin;
        private readonly float _minimumSwitchCooldown;
        private readonly float _emergencyThreatThreshold;

        private CombatIntent _currentIntent = CombatIntent.None(new StringName("scheduler_empty"));
        private float _currentScore;
        private float _commitmentRemaining;
        private float _switchCooldownRemaining;
        private bool _hasCurrent;

        public CombatIntent CurrentIntent => _currentIntent;
        public float CurrentScore => _currentScore;
        public float CommitmentRemaining => _commitmentRemaining;

        public CombatActionScheduler(
            float switchScoreMargin,
            float emergencyScoreMargin,
            float minimumSwitchCooldown,
            float emergencyThreatThreshold)
        {
            _switchScoreMargin = Mathf.Clamp(switchScoreMargin, 0f, 1f);
            _emergencyScoreMargin = Mathf.Clamp(emergencyScoreMargin, 0f, 1f);
            _minimumSwitchCooldown = Mathf.Max(0f, minimumSwitchCooldown);
            _emergencyThreatThreshold = Mathf.Clamp(emergencyThreatThreshold, 0f, 1f);
        }

        public void Tick(float deltaSeconds)
        {
            float dt = Mathf.Max(0f, deltaSeconds);
            _commitmentRemaining = Mathf.Max(0f, _commitmentRemaining - dt);
            _switchCooldownRemaining = Mathf.Max(0f, _switchCooldownRemaining - dt);
        }

        public SchedulerDecision Resolve(DecisionTrace trace, in CombatSnapshot snapshot)
        {
            CombatIntent proposed = trace?.ChosenIntent
                ?? CombatIntent.None(new StringName("missing_trace"));
            float proposedScore = trace?.GetScore(proposed) ?? 0f;

            if (IsHardForcedState(snapshot.SelfState))
            {
                CombatIntent interrupted = CombatIntent.None(new StringName("hard_state_interrupt"));
                return Commit(proposed, proposedScore, interrupted, 0f, "hard_state_interrupt");
            }

            if (!_hasCurrent)
            {
                return Commit(proposed, proposedScore, proposed, proposedScore, "initial_commit");
            }

            // Idle cũng là một trạng thái hợp lệ. Trước đây Current=None bị xem như chưa từng commit,
            // nên log báo initial_commit lặp vô hạn dù Hyou chỉ đang theo Player.
            if (_currentIntent.IsNone && proposed.IsNone)
            {
                _currentScore = proposedScore;
                return BuildDecision(proposed, proposedScore, false, "same_idle");
            }

            if (_currentIntent.IsNone)
            {
                return Commit(proposed, proposedScore, proposed, proposedScore, "idle_exit");
            }

            if (CurrentTargetInvalid(snapshot))
            {
                return Commit(proposed, proposedScore, proposed, proposedScore, "target_invalid");
            }

            bool sameIntent = IsSameIntent(_currentIntent, proposed);
            CandidateTrace currentCandidate = default;
            bool currentFeasible = trace != null
                && trace.TryGetCandidate(_currentIntent, out currentCandidate)
                && currentCandidate.Feasible;
            float evaluatedCurrentScore = currentFeasible ? currentCandidate.FinalScore : 0f;

            if (sameIntent)
            {
                // Không reset commitment khi evaluator lặp lại cùng một ý định.
                // Nếu không, lock 0.24s sẽ bị kéo dài vô hạn mỗi 0.15s.
                _currentScore = proposedScore;
                return BuildDecision(proposed, proposedScore, false, "same_intent");
            }

            bool emergency = IsEmergencyProposal(proposed, proposedScore, evaluatedCurrentScore, snapshot);

            // Hard gate của evaluator luôn thắng commitment. Giữ một intent đã bất khả thi
            // chỉ khiến AI kiên định làm điều sai, một dạng kỷ luật khá vô dụng.
            if (!currentFeasible)
            {
                return Commit(proposed, proposedScore, proposed, proposedScore, "current_infeasible");
            }

            if (_commitmentRemaining > 0f && !emergency)
            {
                _currentScore = evaluatedCurrentScore;
                return BuildDecision(proposed, proposedScore, false, "commitment_lock");
            }

            if (emergency)
            {
                return Commit(proposed, proposedScore, proposed, proposedScore, "emergency_override");
            }

            if (_switchCooldownRemaining > 0f)
            {
                _currentScore = evaluatedCurrentScore;
                return BuildDecision(proposed, proposedScore, false, "switch_cooldown");
            }

            if (evaluatedCurrentScore <= 0.05f && proposedScore > evaluatedCurrentScore)
            {
                return Commit(proposed, proposedScore, proposed, proposedScore, "current_score_collapsed");
            }

            if (proposedScore >= evaluatedCurrentScore + _switchScoreMargin)
            {
                return Commit(proposed, proposedScore, proposed, proposedScore, "score_margin");
            }

            _currentScore = evaluatedCurrentScore;
            return BuildDecision(proposed, proposedScore, false, "stability_keep");
        }

        public void Reset()
        {
            _currentIntent = CombatIntent.None(new StringName("scheduler_reset"));
            _currentScore = 0f;
            _commitmentRemaining = 0f;
            _switchCooldownRemaining = 0f;
            _hasCurrent = false;
        }

        private SchedulerDecision Commit(
            CombatIntent proposed,
            float proposedScore,
            CombatIntent committed,
            float committedScore,
            string reason)
        {
            bool didSwitch = !_hasCurrent || !IsSameIntent(_currentIntent, committed);
            _currentIntent = committed;
            _currentScore = committedScore;
            _commitmentRemaining = committed.MinCommitmentSeconds;
            _switchCooldownRemaining = didSwitch ? _minimumSwitchCooldown : _switchCooldownRemaining;
            _hasCurrent = true;
            return new SchedulerDecision(
                proposed,
                committed,
                proposedScore,
                committedScore,
                didSwitch,
                new StringName(reason),
                _commitmentRemaining);
        }

        private SchedulerDecision BuildDecision(
            CombatIntent proposed,
            float proposedScore,
            bool didSwitch,
            string reason)
        {
            return new SchedulerDecision(
                proposed,
                _currentIntent,
                proposedScore,
                _currentScore,
                didSwitch,
                new StringName(reason),
                _commitmentRemaining);
        }

        private bool IsEmergencyProposal(
            CombatIntent proposed,
            float proposedScore,
            float currentScore,
            in CombatSnapshot snapshot)
        {
            bool emergencyType = proposed.Type == CombatIntentType.PanicEvade
                || proposed.Type == CombatIntentType.Backpedal
                || proposed.Type == CombatIntentType.Guard;
            bool currentAllowsEmergency = (_currentIntent.InterruptMask & CombatInterruptMask.EmergencyEvade) != 0;
            bool threatIsImmediate = snapshot.ThreatSeverity >= _emergencyThreatThreshold
                || (snapshot.ThreatEtaSeconds > 0f && snapshot.ThreatEtaSeconds <= 0.20f);
            return emergencyType
                && currentAllowsEmergency
                && threatIsImmediate
                && proposedScore >= currentScore + _emergencyScoreMargin;
        }

        private bool CurrentTargetInvalid(in CombatSnapshot snapshot)
        {
            if (!_currentIntent.TargetId.HasValue)
            {
                return false;
            }

            return !snapshot.TargetId.HasValue
                || snapshot.TargetId.Value != _currentIntent.TargetId.Value;
        }

        private static bool IsSameIntent(CombatIntent left, CombatIntent right)
        {
            return left.Type == right.Type
                && left.ActionId == right.ActionId
                && left.TargetId == right.TargetId;
        }

        private static bool IsHardForcedState(CombatStateId state)
        {
            return state == CombatStateId.Dead
                || state == CombatStateId.Hitstun
                || state == CombatStateId.Stagger
                || state == CombatStateId.GuardBreak
                || state == CombatStateId.BlockStun;
        }
    }
}
