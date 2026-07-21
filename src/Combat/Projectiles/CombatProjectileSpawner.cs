using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Combat.Projectiles
{
    /// <summary>
    /// Điểm tạo projectile duy nhất. ActionRunner chỉ phát release event;
    /// spawner chịu trách nhiệm chọn world parent và đặt origin đúng CombatCenter.
    /// </summary>
    public static class CombatProjectileSpawner
    {
        public static CombatProjectile2D Spawn(
            CombatCharacter attacker,
            CombatActionData action,
            ProjectileSpecData spec,
            Vector2 direction)
        {
            if (attacker == null || action == null || spec == null || !attacker.IsInsideTree())
            {
                return null;
            }

            Vector2 safeDirection = direction.LengthSquared() <= 0.001f
                ? attacker.FacingDirection
                : direction.Normalized();
            if (safeDirection.LengthSquared() <= 0.001f)
            {
                safeDirection = Vector2.Down;
            }

            Node worldParent = attacker.GetTree().CurrentScene ?? attacker.GetParent();
            if (worldParent == null)
            {
                return null;
            }

            var projectile = new CombatProjectile2D();
            projectile.Initialize(attacker, action, spec, safeDirection);
            worldParent.AddChild(projectile);
            projectile.GlobalPosition = attacker.CombatCenter
                + safeDirection * Mathf.Max(0f, spec.SpawnOffset);
            return projectile;
        }
    }
}
