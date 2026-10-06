using Godot;

namespace AshesofaDyingWorld.UI.HUD
{
    public enum WorldHudLane
    {
        Target,
        Health,
        Feedback,
        Progression
    }

    /// <summary>
    /// Keeps world-attached feedback in predictable bands above an actor.
    /// Widgets are measured before placement so their top edge, rather than a
    /// hard-coded point, participates in the lane separation.
    /// </summary>
    public static class WorldHudLayout
    {
        public const float LaneSpacing = 6f;
        private const float ActorClearance = 10f;

        // These are visual budgets, not per-service offsets. The feedback band
        // includes the largest damage-number animation path at the default scale.
        private static float ReservedBandHeight(WorldHudLane lane)
        {
            return lane switch
            {
                WorldHudLane.Target => 24f,
                WorldHudLane.Health => 40f,
                WorldHudLane.Feedback => 80f,
                WorldHudLane.Progression => 44f,
                _ => 0f
            };
        }

        private static float BandBottomDistance(WorldHudLane lane)
        {
            float distance = ActorClearance;
            for (int laneIndex = (int)WorldHudLane.Progression; laneIndex > (int)lane; laneIndex--)
            {
                distance += ReservedBandHeight((WorldHudLane)laneIndex) + LaneSpacing;
            }
            return distance;
        }

        public static Rect2 GetReservedBand(Node2D actor, WorldHudLane lane, float width = 1f)
        {
            if (actor == null)
            {
                return default;
            }

            Vector2 actorScreenPosition = actor.GetGlobalTransformWithCanvas().Origin;
            float height = ReservedBandHeight(lane);
            float bottom = actorScreenPosition.Y - BandBottomDistance(lane);
            float resolvedWidth = Mathf.Max(1f, width);
            return new Rect2(
                new Vector2(actorScreenPosition.X - resolvedWidth * 0.5f, bottom - height),
                new Vector2(resolvedWidth, height));
        }

        public static float GetFeedbackTravelLimit(Vector2 maximumVisualSize)
        {
            float visualHeight = Mathf.Max(1f, maximumVisualSize.Y);
            return Mathf.Max(0f, ReservedBandHeight(WorldHudLane.Feedback) - visualHeight);
        }

        public static Vector2 Resolve(Node2D actor, WorldHudLane lane, Vector2 widgetSize)
        {
            if (actor == null)
            {
                return Vector2.Zero;
            }

            Vector2 actorScreenPosition = actor.GetGlobalTransformWithCanvas().Origin;
            Vector2 size = new(Mathf.Max(1f, widgetSize.X), Mathf.Max(1f, widgetSize.Y));
            return actorScreenPosition
                - new Vector2(size.X * 0.5f, BandBottomDistance(lane) + size.Y);
        }
    }
}
