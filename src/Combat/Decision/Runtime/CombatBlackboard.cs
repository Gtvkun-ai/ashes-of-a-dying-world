using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Decision.Model;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Bộ nhớ ngắn hạn của một combatant. Không save xuống ổ đĩa.
    /// Blackboard giữ continuity; scheduler là lớp duy nhất được quyền khóa/interrupt CurrentIntent.
    /// </summary>
    public sealed class CombatBlackboard
    {
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

        public Dictionary<StringName, float> FailedActionCooldowns { get; } = new();
        public Queue<DecisionTrace> RecentTraces { get; } = new();

        public void Tick(float deltaSeconds)
        {
            float dt = Mathf.Max(0f, deltaSeconds);
            IntentLockRemaining = Mathf.Max(0f, IntentLockRemaining - dt);
            IntentCooldownRemaining = Mathf.Max(0f, IntentCooldownRemaining - dt);
            OrbitDwellRemaining = Mathf.Max(0f, OrbitDwellRemaining - dt);
            RecentDamageWindow = Mathf.Max(0f, RecentDamageWindow - dt);
            RecentBlockedHitsWindow = Mathf.Max(0f, RecentBlockedHitsWindow - dt);
            RecentCastInterruptsWindow = Mathf.Max(0f, RecentCastInterruptsWindow - dt);

            if (FailedActionCooldowns.Count == 0)
            {
                return;
            }

            var keys = new List<StringName>(FailedActionCooldowns.Keys);
            foreach (StringName key in keys)
            {
                float remaining = FailedActionCooldowns[key] - dt;
                if (remaining <= 0f)
                {
                    FailedActionCooldowns.Remove(key);
                }
                else
                {
                    FailedActionCooldowns[key] = remaining;
                }
            }
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
            FailedActionCooldowns.Clear();
            RecentTraces.Clear();
        }
    }
}
