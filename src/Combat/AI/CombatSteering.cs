using Godot;

namespace AshesofaDyingWorld.Combat.AI
{
    /// <summary>
    /// Hình học điều hướng cho combat top-down dùng hướng đánh 4 chiều.
    ///
    /// Quy tắc quan trọng:
    /// - AI phải đứng vào một "làn đánh" ngang/dọc trước khi ra đòn.
    /// - Khi đã lọt quá gần mục tiêu, AI phải thoát hẳn ra ngoài vùng cấm rồi mới đánh lại.
    /// - Khi đi tới formation slot, AI không được chọn đường thẳng xuyên qua người dẫn đầu.
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
                bool tooClose,
                bool tooFar,
                bool laneAligned)
            {
                Facing = facing;
                DesiredPosition = desiredPosition;
                ForwardDistance = forwardDistance;
                LateralDistance = lateralDistance;
                DirectDistance = directDistance;
                CanAttack = canAttack;
                TooClose = tooClose;
                TooFar = tooFar;
                LaneAligned = laneAligned;
            }

            public Vector2 Facing { get; }
            public Vector2 DesiredPosition { get; }
            public float ForwardDistance { get; }
            public float LateralDistance { get; }
            public float DirectDistance { get; }
            public bool CanAttack { get; }
            public bool TooClose { get; }
            public bool TooFar { get; }
            public bool LaneAligned { get; }
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
            float safeMax = Mathf.Max(safeMin + 2f, maximumDistance);
            float safePreferred = Mathf.Clamp(preferredDistance, safeMin + 1f, safeMax - 1f);
            float safeLateral = Mathf.Max(1f, lateralTolerance);

            bool laneAligned = forwardDistance > 0f && lateralDistance <= safeLateral;
            bool tooClose = directDistance < safeMin || forwardDistance < safeMin;
            bool tooFar = forwardDistance > safeMax;
            bool canAttack = laneAligned && !tooClose && !tooFar;

            // Điểm đứng lý tưởng luôn nằm về phía actor đang tiếp cận. Nhờ vậy AI không
            // chọn một slot bên kia mục tiêu rồi cố chạy xuyên qua thân mục tiêu để tới đó.
            Vector2 desiredPosition = targetPosition - facing * safePreferred;
            return new CardinalApproach(
                facing,
                desiredPosition,
                forwardDistance,
                lateralDistance,
                directDistance,
                canAttack,
                tooClose,
                tooFar,
                laneAligned);
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

            // Hysteresis trục: không đổi ngang/dọc liên tục khi mục tiêu đứng gần đường chéo 45 độ.
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

        /// <summary>
        /// Bẻ đường đi sang tiếp tuyến khi hướng mong muốn đang chọc xuyên qua vùng cấm.
        /// Dùng cho formation để Hyou vòng quanh Player thay vì cắt thẳng qua người.
        /// </summary>
        public static Vector2 SteerAroundCircle(
            Vector2 desiredDirection,
            Vector2 actorPosition,
            Vector2 obstaclePosition,
            float avoidanceRadius,
            int preferredSideSign)
        {
            if (desiredDirection.LengthSquared() <= 0.0001f || avoidanceRadius <= 0f)
            {
                return desiredDirection;
            }

            Vector2 toObstacle = obstaclePosition - actorPosition;
            float distance = toObstacle.Length();
            if (distance <= 0.001f)
            {
                Vector2 fallback = preferredSideSign >= 0 ? Vector2.Right : Vector2.Left;
                return fallback;
            }

            Vector2 toward = toObstacle / distance;
            float headingIntoObstacle = desiredDirection.Normalized().Dot(toward);
            float influenceRadius = avoidanceRadius * 1.8f;
            if (distance >= influenceRadius || headingIntoObstacle <= 0.05f)
            {
                return desiredDirection.Normalized();
            }

            Vector2 tangent = new Vector2(-toward.Y, toward.X) * (preferredSideSign >= 0 ? 1f : -1f);
            Vector2 away = -toward;
            float proximity = 1f - Mathf.Clamp(distance / influenceRadius, 0f, 1f);
            Vector2 steered = desiredDirection.Normalized()
                + tangent * (0.9f + proximity * 1.5f)
                + away * proximity * 0.8f;
            return steered.LengthSquared() > 0.0001f ? steered.Normalized() : tangent;
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
