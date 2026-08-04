using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Model;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Dự báo ngắn hạn cho combat thật. Thay vì chỉ hỏi "địch có gần không",
    /// predictor đọc action đang chạy, reach, lunge và phase để biết lúc nào Hyou phải né.
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
            CombatActionData action = target.Actions?.CurrentAction;
            bool meleeAction = action != null && action.DeliveryMode == CombatDeliveryMode.MeleeHitbox;

            float actionReach = ResolveActionDangerRange(target, action);
            float effectiveDangerRange = Mathf.Max(_dangerRange, actionReach);
            float distancePressure = ResponseCurve.InverseSmoothRamp(
                targetDistance,
                Mathf.Max(12f, effectiveDangerRange * 0.30f),
                effectiveDangerRange);

            float statePressure = state switch
            {
                CombatStateId.AttackActive => 1f,
                CombatStateId.AttackStartup => 0.94f,
                CombatStateId.AttackRecovery => 0.08f,
                _ => 0.16f
            };

            if (!facingSelf)
            {
                statePressure *= 0.18f;
            }

            float severity = Mathf.Clamp(distancePressure * statePressure, 0f, 1f);
            float eta = state switch
            {
                CombatStateId.AttackActive => 0.04f,
                CombatStateId.AttackStartup => Mathf.Clamp(action?.StartupSeconds ?? 0.24f, 0.08f, 0.42f),
                CombatStateId.AttackRecovery => 0.70f,
                _ => 0.85f
            };

            bool threateningAttack = facingSelf
                && meleeAction
                && (state == CombatStateId.AttackStartup || state == CombatStateId.AttackActive)
                && targetDistance <= effectiveDangerRange + 10f;
            bool blockable = threateningAttack && severity >= 0.18f;
            bool dodgeable = threateningAttack
                && (state == CombatStateId.AttackStartup || severity >= 0.48f)
                && eta <= 0.36f;

            return new ThreatAssessment(
                eta,
                severity,
                incoming,
                blockable,
                dodgeable);
        }

        private float ResolveActionDangerRange(CombatCharacter target, CombatActionData action)
        {
            if (action == null || action.DeliveryMode != CombatDeliveryMode.MeleeHitbox)
            {
                return _dangerRange;
            }

            HitProfileData hit = action.HitProfile;
            float hitboxHalfExtent = hit == null
                ? 10f
                : Mathf.Max(hit.HitboxSize.X, hit.HitboxSize.Y) * 0.5f;
            float reach = hit?.Reach ?? 12f;
            float lunge = Mathf.Max(0f, action.LungeSpeed)
                * Mathf.Max(0f, target.ActionLungeMultiplier)
                * Mathf.Clamp(action.StartupSeconds + action.ActiveSeconds, 0.08f, 0.65f);

            // CombatCenter/collider của sprite scale 2 cần một margin nhỏ. Đây là prediction margin,
            // không thay đổi hitbox thật nên tránh kiểu "né vì một đòn không thể chạm" quá xa.
            return Mathf.Max(_dangerRange, reach + hitboxHalfExtent + lunge + 18f);
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
