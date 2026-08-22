using Godot;

namespace AshesofaDyingWorld.Gameplay.Events
{
    /// <summary>
    /// Payload nhẹ, typed, không phụ thuộc UI. Một hành động gameplay phát ra đúng một event;
    /// quest/loot/world-state có thể cùng nghe mà emitter không cần biết các consumer đó tồn tại.
    /// </summary>
    public sealed class GameplayEvent
    {
        public GameplayEventType Type { get; }
        public Node Actor { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public string SourceId { get; }
        public int Amount { get; }
        public Vector2 WorldPosition { get; }
        public string ScenePath { get; internal set; }

        public GameplayEvent(
            GameplayEventType type,
            Node actor = null,
            string actorId = "",
            string targetId = "",
            string sourceId = "",
            int amount = 1,
            Vector2? worldPosition = null,
            string scenePath = "")
        {
            Type = type;
            Actor = actor;
            ActorId = actorId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            Amount = amount;
            WorldPosition = worldPosition ?? Vector2.Zero;
            ScenePath = scenePath ?? string.Empty;
        }
    }
}
