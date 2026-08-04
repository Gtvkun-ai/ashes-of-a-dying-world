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

        [ExportGroup("Delivery")]
        [Export] public CombatDeliveryMode DeliveryMode { get; set; } = CombatDeliveryMode.MeleeHitbox;
        [Export] public HitProfileData HitProfile { get; set; }
        [Export] public ProjectileSpecData ProjectileSpec { get; set; }

        [ExportGroup("Action Events")]
        [Export] public Array<CombatActionEventData> Events { get; set; } = new();

        public bool HasAuthoredEvents => Events != null && Events.Count > 0;

        public string ResolveAnimation(string direction)
        {
            string safeDirection = string.IsNullOrWhiteSpace(direction) ? "down" : direction;
            return (AnimationTemplate ?? string.Empty).Replace("{dir}", safeDirection);
        }
    }
}
