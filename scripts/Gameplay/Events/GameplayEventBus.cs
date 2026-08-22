using Godot;
using System;

namespace AshesofaDyingWorld.Gameplay.Events
{
    /// <summary>
    /// Event spine toàn game. Đặt ở /root để sống qua scene transition.
    /// Emitter chỉ Publish; consumer subscribe Published. Không nối trực tiếp world -> quest -> inventory.
    /// </summary>
    public partial class GameplayEventBus : Node
    {
        public const string RuntimeNodeName = "GameplayEventBus";

        public static GameplayEventBus Current { get; private set; }
        public event Action<GameplayEvent> Published;

        [Export] public bool DebugLogging { get; set; } = false;

        public static GameplayEventBus GetOrCreate(SceneTree tree)
        {
            if (Current != null && GodotObject.IsInstanceValid(Current))
            {
                return Current;
            }

            if (tree?.Root == null)
            {
                return null;
            }

            GameplayEventBus existing = tree.Root.GetNodeOrNull<GameplayEventBus>(RuntimeNodeName);
            if (existing != null)
            {
                Current = existing;
                return existing;
            }

            var created = new GameplayEventBus { Name = RuntimeNodeName };
            tree.Root.AddChild(created);
            return created;
        }

        public override void _Ready()
        {
            Current = this;
        }

        public override void _ExitTree()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public void Publish(GameplayEvent gameplayEvent)
        {
            if (gameplayEvent == null || gameplayEvent.Type == GameplayEventType.None)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(gameplayEvent.ScenePath))
            {
                gameplayEvent.ScenePath = GetTree()?.CurrentScene?.SceneFilePath ?? string.Empty;
            }

            if (DebugLogging)
            {
                GD.Print(
                    $"[GameplayEvent] type={gameplayEvent.Type} actor={gameplayEvent.ActorId} " +
                    $"target={gameplayEvent.TargetId} source={gameplayEvent.SourceId} amount={gameplayEvent.Amount}");
            }

            Action<GameplayEvent> listeners = Published;
            if (listeners == null)
            {
                return;
            }

            foreach (Delegate listener in listeners.GetInvocationList())
            {
                if (listener is not Action<GameplayEvent> handler)
                {
                    continue;
                }

                try
                {
                    handler(gameplayEvent);
                }
                catch (Exception ex)
                {
                    // Một consumer lỗi không được phép làm hỏng combat/interaction emitter.
                    GD.PrintErr($"[GameplayEventBus] Consumer failed for {gameplayEvent.Type}: {ex.Message}");
                }
            }
        }
    }
}
