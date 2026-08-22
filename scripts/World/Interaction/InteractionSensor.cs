using Godot;
using System;

namespace AshesofaDyingWorld.World.Interaction
{
    /// <summary>
    /// Sensor runtime của player. Dùng group scan có cadence nhỏ để prop không cần collision shape riêng.
    /// Khi map lớn lên có thể thay implementation bằng Area2D mà không đổi IInteractable.
    /// </summary>
    public partial class InteractionSensor : Node
    {
        [Export(PropertyHint.Range, "16,200,1")]
        public float SearchRadius { get; set; } = 56f;
        [Export(PropertyHint.Range, "0.03,0.5,0.01")]
        public float RefreshInterval { get; set; } = 0.08f;

        public Node2D Actor { get; set; }
        public Func<bool> InputEnabled { get; set; }

        private IInteractable _current;
        private float _refreshRemaining;
        private float _interactCooldown;

        public override void _Process(double delta)
        {
            _interactCooldown = Mathf.Max(0f, _interactCooldown - (float)delta);

            if (!CanUseInteraction())
            {
                SetCurrent(null);
                return;
            }

            if (_interactCooldown <= 0f
                && InputMap.HasAction("interact")
                && Input.IsActionJustPressed("interact"))
            {
                TryInteractCurrent();
            }

            _refreshRemaining -= (float)delta;
            if (_refreshRemaining <= 0f)
            {
                _refreshRemaining = Mathf.Max(0.03f, RefreshInterval);
                RefreshNearest();
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // E luôn là fallback. Cooldown chặn double-fire nếu action "interact" cũng đang bind E.
            if (_interactCooldown > 0f || !CanUseInteraction())
            {
                return;
            }

            if (@event is InputEventKey key
                && key.Pressed
                && !key.Echo
                && (key.Keycode == Key.E || key.PhysicalKeycode == Key.E))
            {
                TryInteractCurrent();
                GetViewport()?.SetInputAsHandled();
            }
        }

        public override void _ExitTree()
        {
            InteractionPromptHud.Current?.HidePrompt();
        }

        private bool CanUseInteraction()
        {
            return Actor != null
                && GodotObject.IsInstanceValid(Actor)
                && IsInsideTree()
                && (InputEnabled?.Invoke() ?? true);
        }

        private void RefreshNearest()
        {
            IInteractable best = null;
            float bestDistanceSq = float.MaxValue;
            float sensorRadiusSq = SearchRadius * SearchRadius;

            foreach (Node node in GetTree().GetNodesInGroup(WorldInteractable.InteractableGroup))
            {
                if (node is not IInteractable candidate)
                {
                    continue;
                }

                Node2D anchor = candidate.InteractionAnchor;
                if (anchor == null || !GodotObject.IsInstanceValid(anchor) || !candidate.CanInteract(Actor))
                {
                    continue;
                }

                float distanceSq = Actor.GlobalPosition.DistanceSquaredTo(anchor.GlobalPosition);
                float objectRadius = Mathf.Min(SearchRadius, Mathf.Max(8f, candidate.InteractionRadius));
                if (distanceSq > sensorRadiusSq || distanceSq > objectRadius * objectRadius)
                {
                    continue;
                }

                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    best = candidate;
                }
            }

            SetCurrent(best);
        }

        private void SetCurrent(IInteractable next)
        {
            _current = next;
            InteractionPromptHud hud = InteractionPromptHud.GetOrCreate(GetTree());
            if (_current == null)
            {
                hud?.HidePrompt();
                return;
            }

            string prompt = _current.GetInteractionPrompt(Actor);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                hud?.HidePrompt();
            }
            else
            {
                hud?.ShowPrompt(prompt);
            }
        }

        private void TryInteractCurrent()
        {
            if (!CanUseInteraction())
            {
                SetCurrent(null);
                return;
            }

            Node2D anchor = _current?.InteractionAnchor;
            if (_current == null || anchor == null || !GodotObject.IsInstanceValid(anchor) || !_current.CanInteract(Actor))
            {
                RefreshNearest();
            }

            anchor = _current?.InteractionAnchor;
            if (_current == null || anchor == null || !GodotObject.IsInstanceValid(anchor))
            {
                return;
            }

            if (_current.TryInteract(Actor))
            {
                _interactCooldown = 0.12f;
                SetCurrent(null);
                _refreshRemaining = 0f;
            }
        }
    }
}
