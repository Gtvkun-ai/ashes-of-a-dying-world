using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Projectiles;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Cửa duy nhất biến action event thành thay đổi trong world.
    /// CombatActionRunner không instantiate node; CombatCharacter cũng không hardcode tên spell.
    /// </summary>
    public static class CombatActionEventDispatcher
    {
        public static bool Dispatch(
            CombatCharacter owner,
            CombatActionData action,
            CombatActionEventData actionEvent,
            Vector2 direction)
        {
            if (owner == null || action == null || actionEvent == null)
            {
                return false;
            }

            switch (actionEvent.EventType)
            {
                case CombatActionEventType.SpawnProjectile:
                    if (actionEvent.ProjectileSpec == null)
                    {
                        GD.PushError(
                            $"[CombatActionEvent] SpawnProjectile thiếu spec "
                            + $"action={action.ActionId} event={actionEvent.EventId}");
                        return false;
                    }

                    bool spawned = CombatProjectileSpawner.Spawn(
                        owner,
                        action,
                        actionEvent.ProjectileSpec,
                        direction,
                        actionEvent.OriginSocketPath,
                        owner.Actions?.CurrentAimTarget) != null;
                    if (spawned)
                    {
                        CombatFeedbackService.GetOrCreate(owner.GetTree())?
                            .PlayActionEvent(owner, action, actionEvent);
                        GD.Print(
                            $"[CombatActionEvent] FIRED build=v8-action-event-spine "
                            + $"actor={owner.CombatantId} action={action.ActionId} "
                            + $"event={actionEvent.EventId} type={actionEvent.EventType}");
                    }
                    return spawned;

                case CombatActionEventType.PresentationCue:
                    // Cột sống đã có cue id và thời điểm chuẩn; audio/VFX service có thể bind sau
                    // mà không phải sửa ActionRunner. Không claim đã phát thứ chưa có service.
                    return true;

                case CombatActionEventType.SelfEffect:
                    // Reserved cho status/buff pipeline. Giữ event type trong data model nhưng
                    // không âm thầm giả lập effect ở đây.
                    return true;

                default:
                    GD.PushWarning(
                        $"[CombatActionEvent] Event chưa hỗ trợ action={action.ActionId} "
                        + $"type={actionEvent.EventType}");
                    return false;
            }
        }

        /// <summary>
        /// Cầu tương thích cho resource cũ chưa author Events. Resource mới không đi qua đây,
        /// nên không có chuyện event spawn một viên rồi legacy release spawn thêm viên thứ hai.
        /// </summary>
        public static bool DispatchLegacyDelivery(
            CombatCharacter owner,
            CombatActionData action,
            Vector2 direction)
        {
            if (owner == null
                || action == null
                || action.HasAuthoredEvents
                || action.DeliveryMode != CombatDeliveryMode.Projectile
                || action.ProjectileSpec == null)
            {
                return false;
            }

            return CombatProjectileSpawner.Spawn(
                owner,
                action,
                action.ProjectileSpec,
                direction,
                new NodePath("CastOrigin"),
                owner.Actions?.CurrentAimTarget) != null;
        }
    }
}
