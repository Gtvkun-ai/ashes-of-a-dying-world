using Godot;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    public enum CombatMovementMode
    {
        Hold = 0,
        Approach = 1,
        Backpedal = 2,
        StrafeLeft = 3,
        StrafeRight = 4,
        RetreatToAnchor = 5,
        FollowFormation = 6,
        PanicEvade = 7
    }

    /// <summary>
    /// Tactical layer nói actor muốn đứng ở đâu; solver mới quyết định đi bằng hướng nào.
    /// </summary>
    public readonly struct CombatPose
    {
        public Vector2 Anchor { get; }
        public float DesiredRangeMin { get; }
        public float DesiredRangeMax { get; }
        public bool FaceTarget { get; }
        public CombatMovementMode Mode { get; }

        public CombatPose(
            Vector2 anchor,
            float desiredRangeMin,
            float desiredRangeMax,
            bool faceTarget,
            CombatMovementMode mode)
        {
            Anchor = anchor;
            DesiredRangeMin = Mathf.Max(0f, desiredRangeMin);
            DesiredRangeMax = Mathf.Max(DesiredRangeMin, desiredRangeMax);
            FaceTarget = faceTarget;
            Mode = mode;
        }
    }

    public readonly struct MovementCommand
    {
        public Vector2 Direction { get; }
        public bool WantsRun { get; }
        public bool PreserveFacing { get; }
        public Vector2 FacePosition { get; }
        public int DirectionSlot { get; }
        public float Score { get; }

        public bool HasMovement => Direction.LengthSquared() > 0.001f;

        public MovementCommand(
            Vector2 direction,
            bool wantsRun,
            bool preserveFacing,
            Vector2 facePosition,
            int directionSlot,
            float score)
        {
            Direction = direction.LengthSquared() <= 0.001f ? Vector2.Zero : direction.Normalized();
            WantsRun = wantsRun;
            PreserveFacing = preserveFacing;
            FacePosition = facePosition;
            DirectionSlot = directionSlot;
            Score = Mathf.Clamp(score, 0f, 1f);
        }

        public static MovementCommand Stop(Vector2 facePosition)
        {
            return new MovementCommand(Vector2.Zero, false, true, facePosition, -1, 1f);
        }
    }
}
