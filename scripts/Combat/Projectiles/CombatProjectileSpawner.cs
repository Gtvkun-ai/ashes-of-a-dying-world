using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Runtime;

namespace AshesofaDyingWorld.Combat.Projectiles
{
    /// <summary>
    /// Điểm tạo projectile duy nhất. Projectile AI có thể giữ target thật trong lúc cast,
    /// rồi tính lại intercept ngay frame release để không bắn vào vị trí cũ của mục tiêu.
    /// </summary>
    public static class CombatProjectileSpawner
    {
        private const float MaxPredictiveLeadSeconds = 0.65f;

        public static CombatProjectile2D Spawn(
            CombatCharacter attacker,
            CombatActionData action,
            ProjectileSpecData spec,
            Vector2 direction,
            NodePath originSocketPath = default,
            CombatCharacter aimTarget = null,
            float damageMultiplier = 1f)
        {
            if (attacker == null || action == null || spec == null || !attacker.IsInsideTree())
            {
                return null;
            }

            Node worldParent = attacker.GetTree().CurrentScene ?? attacker.GetParent();
            if (worldParent == null)
            {
                return null;
            }

            Vector2 origin = ResolveOrigin(attacker, originSocketPath);
            Vector2 safeDirection = ResolveReleaseAim(attacker, aimTarget, origin, spec, direction);
            var projectile = new CombatProjectile2D();
            projectile.Initialize(
                attacker,
                action,
                spec,
                safeDirection,
                damageMultiplier,
                aimTarget);
            worldParent.AddChild(projectile);

            // SpawnOffset là khoảng hở presentation, không được phép biến thành một cú
            // "teleport xuyên target". Ở cự ly gần trước đây Ice Bolt có thể spawn 20 px
            // về phía trước trong khi slime chỉ cách hơn 20 px một chút -> viên đạn bắt đầu
            // ở cạnh/sau hurtbox rồi bay tiếp, nhìn như aim ngu dù target đang đứng ngay đó.
            float spawnOffset = ResolveSafeSpawnOffset(
                origin,
                aimTarget,
                spec,
                Mathf.Max(0f, spec.SpawnOffset));
            projectile.GlobalPosition = origin + safeDirection * spawnOffset;
            return projectile;
        }

        private static float ResolveSafeSpawnOffset(
            Vector2 origin,
            CombatCharacter target,
            ProjectileSpecData spec,
            float requestedOffset)
        {
            if (requestedOffset <= 0f
                || target == null
                || !GodotObject.IsInstanceValid(target)
                || target.IsQueuedForDeletion()
                || !target.IsAlive)
            {
                return Mathf.Max(0f, requestedOffset);
            }

            float distanceToTarget = origin.DistanceTo(target.CombatCenter);

            // Chừa projectile radius + một ít margin trước tâm target. Nếu target ở quá gần,
            // offset sẽ tự co về 0 thay vì spawn viên đạn ở phía bên kia đối tượng.
            float targetClearance = Mathf.Max(6f, (spec?.Radius ?? 0f) * 1.25f);
            float maximumSafeOffset = Mathf.Max(0f, distanceToTarget - targetClearance);
            return Mathf.Min(Mathf.Max(0f, requestedOffset), maximumSafeOffset);
        }

        private static Vector2 ResolveReleaseAim(
            CombatCharacter attacker,
            CombatCharacter target,
            Vector2 origin,
            ProjectileSpecData spec,
            Vector2 fallbackDirection)
        {
            Vector2 fallback = fallbackDirection.LengthSquared() > 0.001f
                ? fallbackDirection.Normalized()
                : attacker.FacingDirection;
            if (fallback.LengthSquared() <= 0.001f)
            {
                fallback = Vector2.Down;
            }

            if (target == null
                || !GodotObject.IsInstanceValid(target)
                || target.IsQueuedForDeletion()
                || !target.IsAlive
                || !FactionRules.IsHostile(attacker.Faction, target.Faction))
            {
                return fallback;
            }

            Vector2 relative = target.CombatCenter - origin;
            if (relative.LengthSquared() <= 0.001f)
            {
                return fallback;
            }

            float projectileSpeed = Mathf.Max(1f, spec.Speed);
            Vector2 targetVelocity = target.Velocity;
            float interceptTime = SolveInterceptTime(relative, targetVelocity, projectileSpeed);
            float maxLead = Mathf.Min(
                Mathf.Max(0.05f, spec.Lifetime),
                MaxPredictiveLeadSeconds);
            interceptTime = Mathf.Clamp(interceptTime, 0f, maxLead);

            Vector2 predictedPosition = target.CombatCenter + targetVelocity * interceptTime;
            Vector2 aimed = predictedPosition - origin;
            return aimed.LengthSquared() > 0.001f ? aimed.Normalized() : fallback;
        }

        private static float SolveInterceptTime(Vector2 relative, Vector2 targetVelocity, float projectileSpeed)
        {
            float a = targetVelocity.LengthSquared() - projectileSpeed * projectileSpeed;
            float b = 2f * relative.Dot(targetVelocity);
            float c = relative.LengthSquared();

            if (Mathf.Abs(a) <= 0.0001f)
            {
                if (Mathf.Abs(b) <= 0.0001f)
                {
                    return Mathf.Sqrt(c) / projectileSpeed;
                }

                float linearTime = -c / b;
                return linearTime > 0f ? linearTime : Mathf.Sqrt(c) / projectileSpeed;
            }

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return Mathf.Sqrt(c) / projectileSpeed;
            }

            float root = Mathf.Sqrt(discriminant);
            float denominator = 2f * a;
            float t1 = (-b - root) / denominator;
            float t2 = (-b + root) / denominator;
            float best = float.PositiveInfinity;
            if (t1 > 0f) best = t1;
            if (t2 > 0f) best = Mathf.Min(best, t2);
            return float.IsFinite(best) ? best : Mathf.Sqrt(c) / projectileSpeed;
        }

        private static Vector2 ResolveOrigin(CombatCharacter attacker, NodePath originSocketPath)
        {
            string socketPath = originSocketPath.ToString();
            if (!string.IsNullOrWhiteSpace(socketPath))
            {
                Node2D socket = attacker.GetNodeOrNull<Node2D>(originSocketPath);
                if (socket != null)
                {
                    return socket.GlobalPosition;
                }
            }

            return attacker.CombatCenter;
        }
    }
}
