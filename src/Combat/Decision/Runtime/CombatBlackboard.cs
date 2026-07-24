using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Decision.Model;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Bộ nhớ ngắn hạn của một combatant. Không save xuống ổ đĩa.
    /// Blackboard giữ continuity, nhịp hành động và chống spam cùng một lựa chọn.
    /// Scheduler vẫn là lớp duy nhất được quyền khóa/interrupt CurrentIntent.
    /// </summary>
    public sealed class CombatBlackboard
    {
        private const int RecentIntentCapacity = 6;

        public ulong? CurrentTargetId { get; set; }
        public Vector2 LastKnownTargetPosition { get; set; }
        public float LastSeenTargetTime { get; set; }
        public CombatIntent? CurrentIntent { get; set; }
        public float IntentLockRemaining { get; set; }
        public float IntentCooldownRemaining { get; set; }
        public int OrbitSide { get; set; } = 1;
        public float OrbitDwellRemaining { get; set; }
        public Vector2 CurrentAnchor { get; set; }
        public float RecentDamageWindow { get; set; }
        public float RecentBlockedHitsWindow { get; set; }
        public float RecentCastInterruptsWindow { get; set; }

        /// <summary>
        /// Nhớ action vừa thực thi để evaluator có thể buộc một nhịp reposition/đổi bài
        /// thay vì cast cùng phép đến khi màn hình xin nghỉ phép.
        /// </summary>
        public StringName LastExecutedActionId { get; private set; } = new StringName(string.Empty);
        public int ConsecutiveActionUses { get; private set; }
        public float ActionMemoryRemaining { get; private set; }
        public float PanicEvadeCooldownRemaining { get; private set; }
        public bool PanicEvadeActive { get; private set; }

        // Runtime cooldown là dữ liệu chiến thuật, tách khỏi cooldown thật trong AbilityRunner.
        public Dictionary<StringName, float> ActionCooldowns { get; } = new();
        public Dictionary<StringName, float> FailedActionCooldowns { get; } = new();
        public Queue<DecisionTrace> RecentTraces { get; } = new();
        public Queue<CombatIntentType> RecentIntentTypes { get; } = new();

        public void Tick(float deltaSeconds)
        {
            float dt = Mathf.Max(0f, deltaSeconds);
            IntentLockRemaining = Mathf.Max(0f, IntentLockRemaining - dt);
            IntentCooldownRemaining = Mathf.Max(0f, IntentCooldownRemaining - dt);
            OrbitDwellRemaining = Mathf.Max(0f, OrbitDwellRemaining - dt);
            RecentDamageWindow = Mathf.Max(0f, RecentDamageWindow - dt);
            RecentBlockedHitsWindow = Mathf.Max(0f, RecentBlockedHitsWindow - dt);
            RecentCastInterruptsWindow = Mathf.Max(0f, RecentCastInterruptsWindow - dt);
            ActionMemoryRemaining = Mathf.Max(0f, ActionMemoryRemaining - dt);
            PanicEvadeCooldownRemaining = Mathf.Max(0f, PanicEvadeCooldownRemaining - dt);

            if (ActionMemoryRemaining <= 0f)
            {
                LastExecutedActionId = new StringName(string.Empty);
                ConsecutiveActionUses = 0;
            }

            TickCooldownDictionary(ActionCooldowns, dt);
            TickCooldownDictionary(FailedActionCooldowns, dt);
        }

        public void RecordCommittedIntent(CombatIntent intent, bool didSwitch)
        {
            if (!didSwitch)
            {
                return;
            }

            RecentIntentTypes.Enqueue(intent.Type);
            while (RecentIntentTypes.Count > RecentIntentCapacity)
            {
                RecentIntentTypes.Dequeue();
            }

            if (intent.Type != CombatIntentType.PanicEvade)
            {
                PanicEvadeActive = false;
            }

            // Một nhịp chủ động đổi vị trí là đủ để "thở" khỏi action vừa dùng.
            // Nó không xóa cooldown cơ khí, chỉ reset phạt lặp ở tầng chiến thuật.
            if (intent.Type == CombatIntentType.Reposition
                || intent.Type == CombatIntentType.StrafeLeft
                || intent.Type == CombatIntentType.StrafeRight
                || intent.Type == CombatIntentType.PanicEvade)
            {
                LastExecutedActionId = new StringName(string.Empty);
                ConsecutiveActionUses = 0;
                ActionMemoryRemaining = 0f;
            }

            if ((intent.Type == CombatIntentType.Reposition
                    || intent.Type == CombatIntentType.StrafeLeft
                    || intent.Type == CombatIntentType.StrafeRight)
                && OrbitDwellRemaining <= 0f)
            {
                OrbitSide = OrbitSide < 0 ? 1 : -1;
                OrbitDwellRemaining = 0.75f;
            }
        }

        public void RecordActionExecution(StringName actionId, float memorySeconds)
        {
            if (string.IsNullOrWhiteSpace(actionId.ToString()))
            {
                return;
            }

            if (ActionMemoryRemaining > 0f && LastExecutedActionId == actionId)
            {
                ConsecutiveActionUses++;
            }
            else
            {
                LastExecutedActionId = actionId;
                ConsecutiveActionUses = 1;
            }

            ActionMemoryRemaining = Mathf.Max(0.2f, memorySeconds);
        }

        public float GetActionRhythmMultiplier(StringName actionId)
        {
            if (ActionMemoryRemaining <= 0f || LastExecutedActionId != actionId)
            {
                return 1f;
            }

            return ConsecutiveActionUses switch
            {
                <= 0 => 1f,
                1 => 0.68f,
                2 => 0.48f,
                _ => 0.34f
            };
        }

        public float GetRecentIntentMultiplier(CombatIntentType type)
        {
            int count = 0;
            foreach (CombatIntentType recent in RecentIntentTypes)
            {
                if (recent == type)
                {
                    count++;
                }
            }

            return Mathf.Clamp(1f - count * 0.10f, 0.58f, 1f);
        }

        public bool IsFreshAction(StringName actionId)
        {
            return ActionMemoryRemaining > 0f && LastExecutedActionId == actionId;
        }

        public bool TryBeginPanicEvade(float cooldownSeconds)
        {
            if (PanicEvadeActive)
            {
                return true;
            }

            if (PanicEvadeCooldownRemaining > 0f)
            {
                return false;
            }

            PanicEvadeActive = true;
            PanicEvadeCooldownRemaining = Mathf.Max(0.25f, cooldownSeconds);
            return true;
        }

        public void PushTrace(DecisionTrace trace, int capacity)
        {
            if (trace == null)
            {
                return;
            }

            RecentTraces.Enqueue(trace);
            int safeCapacity = Mathf.Max(1, capacity);
            while (RecentTraces.Count > safeCapacity)
            {
                RecentTraces.Dequeue();
            }
        }

        public void Reset()
        {
            CurrentTargetId = null;
            LastKnownTargetPosition = Vector2.Zero;
            LastSeenTargetTime = 0f;
            CurrentIntent = null;
            IntentLockRemaining = 0f;
            IntentCooldownRemaining = 0f;
            OrbitSide = 1;
            OrbitDwellRemaining = 0f;
            CurrentAnchor = Vector2.Zero;
            RecentDamageWindow = 0f;
            RecentBlockedHitsWindow = 0f;
            RecentCastInterruptsWindow = 0f;
            LastExecutedActionId = new StringName(string.Empty);
            ConsecutiveActionUses = 0;
            ActionMemoryRemaining = 0f;
            PanicEvadeCooldownRemaining = 0f;
            PanicEvadeActive = false;
            ActionCooldowns.Clear();
            FailedActionCooldowns.Clear();
            RecentTraces.Clear();
            RecentIntentTypes.Clear();
        }

        private static void TickCooldownDictionary(Dictionary<StringName, float> cooldowns, float dt)
        {
            if (cooldowns.Count == 0)
            {
                return;
            }

            var keys = new List<StringName>(cooldowns.Keys);
            foreach (StringName key in keys)
            {
                float remaining = cooldowns[key] - dt;
                if (remaining <= 0f)
                {
                    cooldowns.Remove(key);
                }
                else
                {
                    cooldowns[key] = remaining;
                }
            }
        }
    }
}
