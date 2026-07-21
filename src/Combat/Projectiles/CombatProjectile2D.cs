using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Runtime;

namespace AshesofaDyingWorld.Combat.Projectiles
{
    /// <summary>
    /// Projectile runtime chung cho combat. Projectile sweep bằng ShapeCast2D để không
    /// xuyên mục tiêu khi tốc độ cao, rồi vẫn chuyển damage qua CombatResolver hiện có.
    /// </summary>
    public partial class CombatProjectile2D : Node2D
    {
        private readonly HashSet<ulong> _hitTargets = new();

        private CombatCharacter _attacker;
        private CombatActionData _action;
        private ProjectileSpecData _spec;
        private Vector2 _direction = Vector2.Right;
        private ShapeCast2D _shapeCast;
        private float _lifeRemaining;
        private int _targetHits;
        private bool _initialized;

        public string ProjectileId => _spec?.ProjectileId ?? "projectile";

        public void Initialize(
            CombatCharacter attacker,
            CombatActionData action,
            ProjectileSpecData spec,
            Vector2 direction)
        {
            _attacker = attacker;
            _action = action;
            _spec = spec;
            _direction = direction.LengthSquared() <= 0.001f
                ? Vector2.Right
                : direction.Normalized();
            _lifeRemaining = Mathf.Max(0.05f, spec?.Lifetime ?? 0.05f);
            _initialized = attacker != null && action != null && spec != null;
        }

        public override void _Ready()
        {
            if (!_initialized || !IsUsable(_attacker))
            {
                QueueFree();
                return;
            }

            Name = $"Projectile_{ProjectileId}";
            ZIndex = 20;
            Rotation = _direction.Angle();

            var shape = new CircleShape2D
            {
                Radius = Mathf.Max(1f, _spec.Radius)
            };
            _shapeCast = new ShapeCast2D
            {
                Name = "Sweep",
                Shape = shape,
                Enabled = true,
                CollideWithAreas = true,
                CollideWithBodies = true,
                CollisionMask = _spec.HurtboxCollisionMask | _spec.WorldCollisionMask,
                TargetPosition = Vector2.Zero
            };
            AddChild(_shapeCast);
            _shapeCast.AddException(_attacker);
            QueueRedraw();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!_initialized || _shapeCast == null || !IsUsable(_attacker))
            {
                QueueFree();
                return;
            }

            float dt = Mathf.Max(0f, (float)delta);
            Vector2 step = _direction * Mathf.Max(0f, _spec.Speed) * dt;
            if (step.LengthSquared() > 0.0001f && Sweep(step))
            {
                return;
            }

            GlobalPosition += step;
            _lifeRemaining -= dt;
            if (_lifeRemaining <= 0f)
            {
                QueueFree();
            }
        }

        public override void _Draw()
        {
            if (_spec == null)
            {
                return;
            }

            float radius = Mathf.Max(2f, _spec.VisualWidth);
            float length = Mathf.Max(radius * 2f, _spec.VisualLength);

            // Vệt kéo nằm phía sau hướng bay vì Node2D đã xoay theo direction.
            DrawLine(new Vector2(-length, 0f), Vector2.Zero, _spec.GlowColor, radius * 1.5f, true);
            DrawCircle(Vector2.Zero, radius * 1.65f, _spec.GlowColor);
            DrawCircle(Vector2.Zero, radius, _spec.CoreColor);
        }

        private bool Sweep(Vector2 step)
        {
            _shapeCast.TargetPosition = ToLocal(GlobalPosition + step);
            _shapeCast.ForceShapecastUpdate();

            var collisions = new List<(float Along, Node Node)>();
            int collisionCount = _shapeCast.GetCollisionCount();
            for (int index = 0; index < collisionCount; index++)
            {
                GodotObject colliderObject = _shapeCast.GetCollider(index);
                if (colliderObject is not Node colliderNode)
                {
                    continue;
                }

                Vector2 point = _shapeCast.GetCollisionPoint(index);
                float along = Mathf.Max(0f, (point - GlobalPosition).Dot(_direction));
                collisions.Add((along, colliderNode));
            }

            collisions.Sort((left, right) => left.Along.CompareTo(right.Along));
            foreach (var collision in collisions)
            {
                Node colliderNode = collision.Node;
                CombatCharacter target = FindCombatCharacter(colliderNode);
                if (target == null)
                {
                    if (_spec.StopOnWorldCollision)
                    {
                        GlobalPosition += step;
                        QueueFree();
                        return true;
                    }

                    continue;
                }

                if (target == _attacker
                    || !target.IsAlive
                    || !FactionRules.CanDamage(_attacker.Faction, target.Faction))
                {
                    continue;
                }

                ulong targetId = target.GetInstanceId();
                if (!_hitTargets.Add(targetId))
                {
                    continue;
                }

                HitProfileData hitProfile = _spec.HitProfileOverride ?? _action.HitProfile;
                if (hitProfile == null)
                {
                    QueueFree();
                    return true;
                }

                var result = _attacker.TryResolveHit(
                    target,
                    _action,
                    hitProfile,
                    GlobalPosition,
                    _direction);
                if (!result.Applied)
                {
                    continue;
                }

                _targetHits++;
                int maxHits = Mathf.Max(1, _spec.MaxTargetHits);
                if (!_spec.PierceTargets || _targetHits >= maxHits)
                {
                    GlobalPosition += step;
                    QueueFree();
                    return true;
                }
            }

            return false;
        }

        private static CombatCharacter FindCombatCharacter(Node node)
        {
            Node cursor = node;
            for (int depth = 0; cursor != null && depth < 8; depth++)
            {
                if (cursor is CombatCharacter combatCharacter)
                {
                    return combatCharacter;
                }

                cursor = cursor.GetParent();
            }

            return null;
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
