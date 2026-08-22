using Godot;

namespace AshesofaDyingWorld.World.Interaction
{
    /// <summary>
    /// Contract tối thiểu cho NPC/chest/flower/shrine/quest object.
    /// Sensor của player không cần biết object cụ thể là loại gì.
    /// </summary>
    public interface IInteractable
    {
        Node2D InteractionAnchor { get; }
        float InteractionRadius { get; }
        bool CanInteract(Node actor);
        string GetInteractionPrompt(Node actor);
        bool TryInteract(Node actor);
    }
}
