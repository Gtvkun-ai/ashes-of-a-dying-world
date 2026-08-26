using Godot;
using AshesofaDyingWorld.Combat.Model;
using Godot.Collections;

namespace AshesofaDyingWorld.Combat.Data
{
    [GlobalClass]
    public partial class CombatActionData : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string ActionId { get; set; } = "light_1";
        [Export] public CombatActionTag Tags { get; set; } = CombatActionTag.Light | CombatActionTag.Melee;

        [ExportGroup("Animation")]
        [Export] public string AnimationTemplate { get; set; } = "sword_{dir}";
        [Export] public int StartFrame { get; set; } = 0;
        [Export] public int ActiveStartFrame { get; set; } = 1;
        [Export] public int ActiveEndFrame { get; set; } = 2;
        [Export] public int EndFrame { get; set; } = 3;
        [Export] public float PlaybackSpeedMultiplier { get; set; } = 1f;
        [Export] public bool ScalePlaybackWithAttackSpeed { get; set; } = true;

        [ExportGroup("Timing Fallback")]
        [Export] public float StartupSeconds { get; set; } = 0.12f;
        [Export] public float ActiveSeconds { get; set; } = 0.12f;
        [Export] public float RecoverySeconds { get; set; } = 0.18f;
        [Export] public float InputBufferSeconds { get; set; } = 0.2f;

        [ExportGroup("Costs and Motion")]
        [Export] public float StaminaCost { get; set; } = 12f;
        [Export] public float LungeSpeed { get; set; } = 45f;
        [Export(PropertyHint.Range, "0,1,0.05")] public float MovementInputMultiplier { get; set; } = 0f;

        [ExportGroup("Delivery")]
        [Export] public CombatDeliveryMode DeliveryMode { get; set; } = CombatDeliveryMode.MeleeHitbox;
        [Export] public HitProfileData HitProfile { get; set; }
        [Export] public ProjectileSpecData ProjectileSpec { get; set; }

        [ExportGroup("Action Events")]
        [Export] public Array<CombatActionEventData> Events { get; set; } = new();

        public bool HasAuthoredEvents => Events != null && Events.Count > 0;

        /// <summary>
        /// Trả về projectile spec thật mà action sẽ spawn.
        /// Resource mới thường đặt spec trong Action Event, còn resource cũ có thể dùng field ProjectileSpec.
        /// Gom logic ở đây để AI, projectile runtime và debug không tự đoán theo hai đường dữ liệu khác nhau.
        /// </summary>
        public ProjectileSpecData ResolveProjectileSpec()
        {
            if (ProjectileSpec != null)
            {
                return ProjectileSpec;
            }

            if (Events == null)
            {
                return null;
            }

            foreach (CombatActionEventData actionEvent in Events)
            {
                if (actionEvent != null
                    && actionEvent.EventType == CombatActionEventType.SpawnProjectile
                    && actionEvent.ProjectileSpec != null)
                {
                    return actionEvent.ProjectileSpec;
                }
            }

            return null;
        }

        /// <summary>
        /// Trả về field spec thật mà action sẽ spawn.
        /// Presentation code dùng cùng một nguồn dữ liệu với SpawnField runtime để
        /// telegraph và field không tự giữ hai scale khác nhau.
        /// </summary>
        public CombatFieldSpecData ResolveFieldSpec()
        {
            if (Events == null)
            {
                return null;
            }

            foreach (CombatActionEventData actionEvent in Events)
            {
                if (actionEvent != null
                    && actionEvent.EventType == CombatActionEventType.SpawnField
                    && actionEvent.FieldSpec != null)
                {
                    return actionEvent.FieldSpec;
                }
            }

            return null;
        }

        public string ResolveAnimation(string direction)
        {
            string safeDirection = string.IsNullOrWhiteSpace(direction) ? "down" : direction;
            return (AnimationTemplate ?? string.Empty).Replace("{dir}", safeDirection);
        }
    }
}
