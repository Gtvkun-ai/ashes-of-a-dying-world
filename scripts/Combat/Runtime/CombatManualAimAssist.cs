using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Soft aim cho người chơi: hướng di chuyển (hoặc hướng nhìn khi đứng yên) vẫn là ý định chính.
    /// Chỉ kéo hướng attack về kẻ địch ở phía trước và trong tầm, không tự khóa mục tiêu sau lưng.
    /// Các NPC tự động KHÔNG sử dụng helper này.
    /// </summary>
    public static class CombatManualAimAssist
    {
        public static Vector2 Resolve(
            CombatCharacter actor,
            Vector2 inputDirection,
            float radius = 95f,
            float halfConeDegrees = 69f)
        {
            Vector2 intended = inputDirection.LengthSquared() > 0.001f
                ? inputDirection.Normalized()
                : (actor?.FacingDirection ?? Vector2.Down);
            if (actor == null || actor.GetTree() == null)
            {
                return intended;
            }

            // Giữ RMB để ngắm chuột thủ công toàn bộ 360°: ưu tiên hơn auto aim.
            if (Input.IsMouseButtonPressed(MouseButton.Right))
            {
                Vector2 mouseDirection = actor.GetGlobalMousePosition() - actor.CombatCenter;
                if (mouseDirection.LengthSquared() >= 12f * 12f)
                {
                    return mouseDirection.Normalized();
                }
            }

            // Có thể tắt soft aim ngay trên Inspector; RMB vẫn aim tay khi đã tắt.
            if (!actor.AutoAimEnabled)
            {
                return intended;
            }

            float effectiveRadius = Mathf.Max(24f, radius);
            float minimumDot = Mathf.Cos(Mathf.DegToRad(Mathf.Clamp(halfConeDegrees, 1f, 89f)));
            float bestScore = float.PositiveInfinity;
            Vector2 bestDirection = intended;

            foreach (Node node in actor.GetTree().GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter candidate || candidate == actor
                    || !GodotObject.IsInstanceValid(candidate) || !candidate.IsAlive
                    || !FactionRules.IsHostile(actor.Faction, candidate.Faction))
                {
                    continue;
                }

                Vector2 toTarget = candidate.CombatCenter - actor.CombatCenter;
                float distance = toTarget.Length();
                if (distance <= 0.001f || distance > effectiveRadius)
                {
                    continue;
                }

                Vector2 direction = toTarget / distance;
                float dot = intended.Dot(direction);
                if (dot < minimumDot)
                {
                    continue;
                }

                // Góc quan trọng hơn một khoảng cách nhỏ: không đổi mục tiêu loạn xạ
                // chỉ vì enemy khác gần hơn vài pixel. Id traversal ổn định trong frame.
                float score = (1f - dot) * 2.0f + distance / effectiveRadius * 0.45f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestDirection = direction;
                }
            }

            return bestDirection;
        }
    }
}
