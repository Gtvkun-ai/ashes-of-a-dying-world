using Godot;

namespace AshesofaDyingWorld.Combat.Model
{
    public sealed class HitResult
    {
        public bool Applied { get; init; }
        public HitRejectionReason RejectionReason { get; init; }
        public float RawDamage { get; init; }
        public float HpDamage { get; init; }
        public float GuardDamage { get; init; }
        public float PoiseDamage { get; init; }
        public bool WasBlocked { get; init; }
        public bool WasParried { get; init; }
        public bool GuardBroken { get; init; }
        public bool Staggered { get; init; }
        public bool Killed { get; init; }
        public bool Shattered { get; init; }
        public float HitstunSeconds { get; init; }
        public float ForcedStaggerSeconds { get; init; }
        public float HitStopSeconds { get; init; }
        public float HitFlashSeconds { get; init; }
        public float LaunchHeight { get; init; }
        public float LaunchDuration { get; init; }
        public Vector2 Knockback { get; init; }

        public static HitResult Rejected(HitRejectionReason reason)
        {
            return new HitResult
            {
                Applied = false,
                RejectionReason = reason
            };
        }
    }
}
