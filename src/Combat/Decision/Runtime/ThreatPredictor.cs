using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Dự báo ngắn hạn đủ dùng cho shadow mode. Không giả vờ là hệ tiên tri hoàn chỉnh:
    /// projectile incoming và action timing chính xác sẽ được bổ sung cùng projectile/scheduler.
    /// </summary>
    public sealed class ThreatPredictor : IThreatPredictor
    {
        private readonly float _dangerRange;
        private readonly float _facingDotThreshold;

        public ThreatPredictor(float dangerRange, float facingDotThreshold)
        {
            _dangerRange = Mathf.Max(1f, dangerRange);
            _facingDotThreshold = Mathf.Clamp(facingDotThreshold, -1f, 1f);
        }

        public ThreatAssessment EvaluateThreats(
            CombatCharacter self,
            CombatCharacter target,
            float targetDistance)
        {
            if (!IsUsable(self) || !IsUsable(target) || target.StateMachine == null)
            {
                return ThreatAssessment.None;
            }

            Vector2 toSelf = self.CombatCenter - target.CombatCenter;
            Vector2 incoming = toSelf.LengthSquared() > 0.001f
                ? toSelf.Normalized()
                : Vector2.Zero;
            bool facingSelf = incoming != Vector2.Zero
                && target.FacingDirection.Dot(incoming) >= _facingDotThreshold;

            CombatStateId state = target.StateMachine.Current;
            float distancePressure = 1f - Mathf.Clamp(targetDistance / _dangerRange, 0f, 1f);
            float statePressure = state switch
            {
                CombatStateId.AttackActive => 1f,
                CombatStateId.AttackStartup => 0.82f,
                CombatStateId.AttackRecovery => 0.12f,
                _ => 0.22f
            };

            if (!facingSelf)
            {
                statePressure *= 0.25f;
            }

            float severity = Mathf.Clamp(distancePressure * statePressure, 0f, 1f);
            float eta = state switch
            {
                CombatStateId.AttackActive => 0.05f,
                CombatStateId.AttackStartup => 0.22f,
                _ => 0.65f
            };

            return new ThreatAssessment(
                eta,
                severity,
                incoming,
                blockable: facingSelf && severity > 0.1f,
                dodgeable: severity > 0.35f);
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
