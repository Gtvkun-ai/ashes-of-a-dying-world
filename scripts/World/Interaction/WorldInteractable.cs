using Godot;
using AshesofaDyingWorld.Gameplay.Events;

namespace AshesofaDyingWorld.World.Interaction
{
    /// <summary>
    /// Interactable data-driven dùng trực tiếp trên root Node2D của prop.
    /// Nó chỉ phát GameplayEvent; không hard-code quest ID hay inventory logic.
    /// </summary>
    public partial class WorldInteractable : Node2D, IInteractable
    {
        public const string InteractableGroup = "Interactable";

        [ExportGroup("Nhận diện")]
        [Export] public string InteractionId { get; set; } = "";
        [Export] public string DisplayName { get; set; } = "Vật thể";
        [Export] public string PromptVerb { get; set; } = "Tương tác";

        [ExportGroup("Phạm vi")]
        [Export(PropertyHint.Range, "8,160,1")]
        public float InteractionRadius { get; set; } = 48f;
        [Export] public bool PlayerOnly { get; set; } = true;

        [ExportGroup("Sự kiện gameplay")]
        [Export] public GameplayEventType EventType { get; set; } = GameplayEventType.InteractionCompleted;
        [Export] public string TargetId { get; set; } = "";
        [Export] public int Amount { get; set; } = 1;

        [ExportGroup("Vòng đời")]
        [Export] public bool OneShot { get; set; } = true;
        [Export] public bool RemoveAfterInteraction { get; set; } = false;

        private bool _consumed;

        public Node2D InteractionAnchor => this;

        public override void _Ready()
        {
            AddToGroup(InteractableGroup);
        }

        public override void _ExitTree()
        {
            RemoveFromGroup(InteractableGroup);
        }

        public bool CanInteract(Node actor)
        {
            if (_consumed || actor == null || !GodotObject.IsInstanceValid(actor))
            {
                return false;
            }

            if (PlayerOnly && !actor.IsInGroup("Player"))
            {
                return false;
            }

            if (actor is Node2D actor2D)
            {
                return actor2D.GlobalPosition.DistanceTo(GlobalPosition) <= Mathf.Max(8f, InteractionRadius);
            }

            return false;
        }

        public string GetInteractionPrompt(Node actor)
        {
            string verb = string.IsNullOrWhiteSpace(PromptVerb) ? "Tương tác" : PromptVerb.Trim();
            string name = string.IsNullOrWhiteSpace(DisplayName) ? Name.ToString() : DisplayName.Trim();
            return $"{verb} {name}".Trim();
        }

        public bool TryInteract(Node actor)
        {
            if (!CanInteract(actor))
            {
                return false;
            }

            string sourceId = string.IsNullOrWhiteSpace(InteractionId)
                ? Name.ToString()
                : InteractionId.Trim();
            string targetId = string.IsNullOrWhiteSpace(TargetId)
                ? sourceId
                : TargetId.Trim();

            GameplayEventBus.GetOrCreate(GetTree())?.Publish(new GameplayEvent(
                EventType,
                actor,
                actor.Name.ToString(),
                targetId,
                sourceId,
                Mathf.Max(1, Amount),
                GlobalPosition));

            if (OneShot)
            {
                _consumed = true;
                RemoveFromGroup(InteractableGroup);
            }

            if (RemoveAfterInteraction)
            {
                Visible = false;
                QueueFree();
            }

            return true;
        }
    }
}
