using Godot;

namespace AshesofaDyingWorld.Combat.AI
{
    /// <summary>
    /// Hình học tiếp cận mục tiêu cho combat top-down dùng hướng đánh 4 chiều.
    /// AI không nên chỉ kiểm tra khoảng cách tròn rồi đứng chém ở góc chéo, vì hitbox
    /// hình chữ nhật phía trước sẽ không bao giờ chạm mục tiêu dù hai root rất gần nhau.
    /// </summary>
    internal static class CombatSteering
    {
        internal readonly struct CardinalApproach
        {
            public CardinalApproach(
                Vector2 facing,
                Vector2 desiredPosition,
                float forwardDistance,
                float lateralDistance,
                float directDistance,
                bool canAttack,
                bool tooClose)
            {
                Facing = facing;
                DesiredPosition = desiredPosition;
                ForwardDistance = forwardDistance;
                LateralDistance = lateralDistance;
                DirectDistance = directDistance;
                CanAttack = canAttack;
                TooClose = tooClose;
            }

            public Vector2 Facing { get; }
            public Vector2 DesiredPosition { get; }
            public float ForwardDistance { get; }
            public float LateralDistance { get; }
            public float DirectDistance { get; }
            public bool CanAttack { get; }
            public bool TooClose { get; }
        }

        public static CardinalApproach EvaluateCardinalApproach(
            Vector2 actorPosition,
            Vector2 targetPosition,
            Vector2 previousFacing,
            float preferredDistance,
            float minimumDistance,
            float maximumDistance,
            float lateralTolerance,
            float axisSwitchBias)
        {
            Vector2 toTarget = targetPosition - actorPosition;
            float directDistance = toTarget.Length();
            Vector2 facing = ResolveStableCardinalFacing(toTarget, previousFacing, axisSwitchBias);
            Vector2 lateralAxis = new Vector2(-facing.Y, facing.X);

            float forwardDistance = toTarget.Dot(facing);
            float lateralDistance = Mathf.Abs(toTarget.Dot(lateralAxis));
            float safeMin = Mathf.Max(0f, minimumDistance);
            float safeMax = Mathf.Max(safeMin + 1f, maximumDistance);
            float safePreferred = Mathf.Clamp(preferredDistance, safeMin + 1f, safeMax - 1f);
            float safeLateral = Mathf.Max(1f, lateralTolerance);

            bool tooClose = forwardDistance < safeMin || directDistance < safeMin;
            bool canAttack = !tooClose
                && forwardDistance <= safeMax
                && lateralDistance <= safeLateral;

            // Điểm đứng lý tưởng nằm cùng một trục cardinal với mục tiêu. Đây là phần
            // khiến AI tự "xếp làn" trước khi đánh thay vì đứng chéo rồi ngẩn người.
            Vector2 desiredPosition = targetPosition - facing * safePreferred;
            return new CardinalApproach(
                facing,
                desiredPosition,
                forwardDistance,
                lateralDistance,
                directDistance,
                canAttack,
                tooClose);
        }

        public static Vector2 ResolveStableCardinalFacing(
            Vector2 direction,
            Vector2 previousFacing,
            float axisSwitchBias = 1.25f)
        {
            Vector2 fallback = ToCardinal(previousFacing);
            if (direction.LengthSquared() <= 0.0001f)
            {
                return fallback == Vector2.Zero ? Vector2.Down : fallback;
            }

            float absX = Mathf.Abs(direction.X);
            float absY = Mathf.Abs(direction.Y);
            float bias = Mathf.Max(1f, axisSwitchBias);
            bool wasHorizontal = Mathf.Abs(fallback.X) > 0.5f;
            bool wasVertical = Mathf.Abs(fallback.Y) > 0.5f;

            if (wasHorizontal && absY <= absX * bias)
            {
                return new Vector2(Mathf.Sign(direction.X), 0f);
            }

            if (wasVertical && absX <= absY * bias)
            {
                return new Vector2(0f, Mathf.Sign(direction.Y));
            }

            return absX > absY
                ? new Vector2(Mathf.Sign(direction.X), 0f)
                : new Vector2(0f, Mathf.Sign(direction.Y));
        }

        public static Vector2 SafeAwayDirection(
            Vector2 actorPosition,
            Vector2 threatPosition,
            Vector2 fallback)
        {
            Vector2 away = actorPosition - threatPosition;
            if (away.LengthSquared() > 0.0001f)
            {
                return away.Normalized();
            }

            Vector2 safeFallback = fallback.LengthSquared() > 0.0001f
                ? fallback.Normalized()
                : Vector2.Left;
            return safeFallback;
        }

        public static Vector2 BlendSeparation(
            Vector2 desiredDirection,
            Vector2 actorPosition,
            Vector2 otherPosition,
            float separationRadius,
            float separationWeight,
            Vector2 fallbackAway)
        {
            Vector2 separation = actorPosition - otherPosition;
            float distance = separation.Length();
            if (distance >= separationRadius || separationRadius <= 0f)
            {
                return desiredDirection;
            }

            Vector2 away = distance > 0.001f
                ? separation / distance
                : SafeAwayDirection(actorPosition, otherPosition, fallbackAway);
            float strength = 1f - Mathf.Clamp(distance / separationRadius, 0f, 1f);
            Vector2 blended = desiredDirection + away * strength * Mathf.Max(0f, separationWeight);
            return blended.LengthSquared() > 0.0001f ? blended.Normalized() : away;
        }

        private static Vector2 ToCardinal(Vector2 value)
        {
            if (value.LengthSquared() <= 0.0001f)
            {
                return Vector2.Zero;
            }

            return Mathf.Abs(value.X) > Mathf.Abs(value.Y)
                ? new Vector2(Mathf.Sign(value.X), 0f)
                : new Vector2(0f, Mathf.Sign(value.Y));
        }
    }
}
