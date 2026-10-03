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

        private static float LaneAnchor(WorldHudLane lane, Vector2 widgetSize)
        {
            float measuredHeight = Mathf.Max(1f, widgetSize.Y);
            return lane switch
            {
                WorldHudLane.Target => 150f + measuredHeight * 0.15f,
                WorldHudLane.Health => 96f + measuredHeight * 0.25f,
                WorldHudLane.Feedback => 52f + measuredHeight * 0.35f,
                WorldHudLane.Progression => measuredHeight * 0.25f,
                _ => 0f
            };
        }

        public static Vector2 Resolve(Node2D actor, WorldHudLane lane, Vector2 widgetSize, Vector2 extraOffset = default)
        {
            if (actor == null)
            {
                return extraOffset;
            }

            Vector2 actorScreenPosition = actor.GetGlobalTransformWithCanvas().Origin;
            Vector2 size = new(Mathf.Max(1f, widgetSize.X), Mathf.Max(1f, widgetSize.Y));
            float laneAnchor = LaneAnchor(lane, size);
            return actorScreenPosition
                + extraOffset
                - new Vector2(size.X * 0.5f, laneAnchor + size.Y);
        }
    }
}
