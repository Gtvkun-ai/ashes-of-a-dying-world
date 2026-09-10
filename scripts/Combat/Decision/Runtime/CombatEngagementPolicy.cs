using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Luật giữ companion gần leader trong lúc chọn mục tiêu.
    /// Sensor có thể nhìn xa, nhưng "nhìn thấy" không đồng nghĩa với "phải lao vào".
    /// </summary>
    public sealed class CombatEngagementPolicy
    {
        public float SensorRadius { get; }
        public float PassiveAcquireRadius { get; }
        public float TargetRetentionRadius { get; }
        public float LeaderPassiveEngagementRadius { get; }
        public float LeaderCombatLeashRadius { get; }
        public float ForceFollowLeaderDistance { get; }
        public bool UseLeaderLeash { get; }

        public CombatEngagementPolicy(
            float sensorRadius,
            bool useLeaderLeash,
            float passiveAcquireRadius,
            float targetRetentionRadius,
            float leaderPassiveEngagementRadius,
            float leaderCombatLeashRadius,
            float forceFollowLeaderDistance)
        {
            SensorRadius = Mathf.Max(1f, sensorRadius);
            UseLeaderLeash = useLeaderLeash;
            PassiveAcquireRadius = Mathf.Clamp(passiveAcquireRadius, 1f, SensorRadius);
            TargetRetentionRadius = Mathf.Max(PassiveAcquireRadius, targetRetentionRadius);
            LeaderPassiveEngagementRadius = Mathf.Max(1f, leaderPassiveEngagementRadius);
            LeaderCombatLeashRadius = Mathf.Max(LeaderPassiveEngagementRadius, leaderCombatLeashRadius);
            ForceFollowLeaderDistance = Mathf.Max(1f, forceFollowLeaderDistance);
        }

        public float GetPassiveQueryRadius(CombatCharacter leader)
        {
            return HasUsableLeader(leader) && UseLeaderLeash
                ? PassiveAcquireRadius
                : SensorRadius;
        }

        public float GetRetentionRadius(CombatCharacter leader)
        {
            return HasUsableLeader(leader) && UseLeaderLeash
                ? TargetRetentionRadius
                : SensorRadius * 1.25f;
        }

        /// <summary>
        /// Mục tiêu tự phát chỉ hợp lệ khi vừa gần companion vừa nằm trong vùng chiến đấu của leader.
        /// Nếu companion đã bị kéo quá xa leader thì ưu tiên tuyệt đối quay về follow.
        /// </summary>
        public bool AllowsPassiveAcquire(
            CombatCharacter self,
            CombatCharacter leader,
            CombatCharacter target)
        {
            if (!IsUsable(self) || !IsUsable(target))
            {
                return false;
            }

            if (!HasUsableLeader(leader) || !UseLeaderLeash)
            {
                return self.CombatCenter.DistanceSquaredTo(target.CombatCenter)
                    <= SensorRadius * SensorRadius;
            }

            if (self.CombatCenter.DistanceSquaredTo(leader.CombatCenter)
                > ForceFollowLeaderDistance * ForceFollowLeaderDistance)
            {
                return false;
            }

            return self.CombatCenter.DistanceSquaredTo(target.CombatCenter)
                    <= PassiveAcquireRadius * PassiveAcquireRadius
                && leader.CombatCenter.DistanceSquaredTo(target.CombatCenter)
                    <= LeaderPassiveEngagementRadius * LeaderPassiveEngagementRadius;
        }

        /// <summary>
        /// Sau khi đã engage cho phép hysteresis rộng hơn một chút, nhưng target vẫn không được
        /// kéo companion ra khỏi "bong bóng" chiến đấu quanh leader.
        /// </summary>
        public bool AllowsRetain(
            CombatCharacter self,
            CombatCharacter leader,
            CombatCharacter target,
            bool leaderEmergency = false)
        {
            if (!IsUsable(self) || !IsUsable(target))
            {
                return false;
            }

            if (!HasUsableLeader(leader) || !UseLeaderLeash)
            {
                float fallback = SensorRadius * 1.25f;
                return self.CombatCenter.DistanceSquaredTo(target.CombatCenter) <= fallback * fallback;
            }

            // Quái đang áp sát/đe dọa leader là ngoại lệ hợp lệ: companion có thể chạy về phía leader
            // dù bản thân hiện đang ở xa do vừa path qua obstacle/cầu thang.
            if (leaderEmergency)
            {
                return true;
            }

            if (self.CombatCenter.DistanceSquaredTo(leader.CombatCenter)
                > ForceFollowLeaderDistance * ForceFollowLeaderDistance)
            {
                return false;
            }

            return self.CombatCenter.DistanceSquaredTo(target.CombatCenter)
                    <= TargetRetentionRadius * TargetRetentionRadius
                && leader.CombatCenter.DistanceSquaredTo(target.CombatCenter)
                    <= LeaderCombatLeashRadius * LeaderCombatLeashRadius;
        }

        public bool IsInsideLeaderEmergencyRadius(
            CombatCharacter leader,
            CombatCharacter target,
            float leaderDangerRadius)
        {
            return HasUsableLeader(leader)
                && IsUsable(target)
                && leader.CombatCenter.DistanceSquaredTo(target.CombatCenter)
                    <= leaderDangerRadius * leaderDangerRadius;
        }

        private static bool HasUsableLeader(CombatCharacter leader)
        {
            return IsUsable(leader) && leader.IsAlive;
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
