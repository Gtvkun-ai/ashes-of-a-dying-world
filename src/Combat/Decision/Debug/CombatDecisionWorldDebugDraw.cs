using Godot;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Debug
{
    /// <summary>World gizmo tách khỏi CanvasLayer để tọa độ debug không bị lẫn UI.</summary>
    public partial class CombatDecisionWorldDebugDraw : Node2D
    {
        public CombatDecisionAgent Agent { get; set; }
        public bool ShowAnchor { get; set; }
        public bool ShowSlots { get; set; }

        public override void _Draw()
        {
            if (Agent?.LastTrace == null || Agent.ControlledCharacter == null)
            {
                return;
            }

            CombatSnapshot snapshot = Agent.LastTrace.Snapshot;
            Vector2 self = Agent.ControlledCharacter.GlobalPosition;
            Color anchorColor = new Color(0.32f, 0.86f, 1f, 0.85f);
            Color rangeColor = new Color(0.45f, 0.72f, 1f, 0.45f);

            if (ShowAnchor)
            {
                Vector2 anchor = Agent.Blackboard.CurrentAnchor;
                DrawLine(self, anchor, anchorColor, 2f, true);
                DrawCircle(anchor, 5f, anchorColor);
                if (snapshot.HasTarget && Agent.ClassProfile != null)
                {
                    DrawArc(snapshot.TargetPosition, Agent.ClassProfile.PreferredMinRange, 0f, Mathf.Tau, 48, rangeColor, 1.5f, true);
                    DrawArc(snapshot.TargetPosition, Agent.ClassProfile.PreferredMaxRange, 0f, Mathf.Tau, 48, rangeColor, 1.5f, true);
                }
            }

            if (!ShowSlots)
            {
                return;
            }

            for (int slot = 0; slot < 16; slot++)
            {
                float angle = Mathf.Tau * slot / 16f;
                Vector2 direction = Vector2.Right.Rotated(angle);
                bool selected = slot == Agent.LastMovementCommand.DirectionSlot;
                Color color = selected
                    ? new Color(0.25f, 1f, 0.55f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.16f);
                float length = selected ? 42f : 25f;
                DrawLine(self, self + direction * length, color, selected ? 3f : 1f, true);
            }
        }
    }
}
