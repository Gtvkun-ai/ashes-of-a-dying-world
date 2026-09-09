using Godot;

namespace AshesofaDyingWorld.Combat.Model
{
    /// <summary>
    /// Kết quả trung gian của hệ Impact/Reaction.
    /// Tách khỏi HitResult để công thức lực có thể test/tune mà không đụng tới damage resolver.
    /// </summary>
    public sealed class ImpactResolution
    {
        public ImpactReactionType Reaction { get; init; } = ImpactReactionType.Absorb;
        public float ImpactPower { get; init; }
        public float Stability { get; init; }
        public float ImpactRatio { get; init; }
        public float MassFactor { get; init; } = 1f;
        public float MomentumFactor { get; init; } = 1f;
        public float ControlResistance { get; init; }
        public float ClosingSpeed { get; init; }
        public float ReactionLockSeconds { get; init; }
        public float LaunchHeight { get; init; }
        public float LaunchDuration { get; init; }
        public Vector2 KnockbackVelocity { get; init; } = Vector2.Zero;
    }
}
