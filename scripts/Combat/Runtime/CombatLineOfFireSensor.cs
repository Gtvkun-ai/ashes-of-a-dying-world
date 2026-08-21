using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Loại vật đầu tiên nằm trong hành lang bắn.
    /// Tách rõ world / ally / hostile để AI có thể quyết định reposition thay vì chỉ nhận bool mơ hồ.
    /// </summary>
    public enum LineOfFireBlockerType
    {
        Invalid = 0,
        Clear = 1,
        World = 2,
        Ally = 3,
        Hostile = 4,
        NeutralActor = 5
    }

    /// <summary>
    /// Kết quả query đường bắn. ReachesTarget chỉ true khi projectile corridor chạm target
    /// trước mọi vật cản khác. Đây là semantics phù hợp với projectile không xuyên mục tiêu.
    /// </summary>
    public readonly struct LineOfFireResult
    {
        public LineOfFireBlockerType BlockerType { get; }
        public CombatCharacter BlockerActor { get; }
        public Vector2 CollisionPoint { get; }
        public float DistanceFromOrigin { get; }

        public bool IsValid => BlockerType != LineOfFireBlockerType.Invalid;
        public bool ReachesTarget => BlockerType == LineOfFireBlockerType.Clear;
        public bool HasFriendlyFireRisk => BlockerType == LineOfFireBlockerType.Ally;
        public bool IsWorldBlocked => BlockerType == LineOfFireBlockerType.World;

        public LineOfFireResult(
            LineOfFireBlockerType blockerType,
            CombatCharacter blockerActor,
            Vector2 collisionPoint,
            float distanceFromOrigin)
        {
            BlockerType = blockerType;
            BlockerActor = blockerActor;
            CollisionPoint = collisionPoint;
            DistanceFromOrigin = Mathf.Max(0f, distanceFromOrigin);
        }

        public static LineOfFireResult Invalid => new(
            LineOfFireBlockerType.Invalid,
            null,
            Vector2.Zero,
            0f);

        public static LineOfFireResult Clear(Vector2 point, float distance) => new(
            LineOfFireBlockerType.Clear,
            null,
            point,
            distance);
    }

    /// <summary>
    /// Sensor đường bắn dùng ShapeCast2D với đúng bán kính projectile.
    ///
    /// Điểm quan trọng: AI và projectile thật dùng cùng Radius + HurtboxCollisionMask + WorldCollisionMask.
    /// Nhờ vậy AI không còn raycast một sợi tóc rồi nghĩ bắn lọt, trong khi viên đạn rộng lại đập vào cây.
    /// </summary>
    public partial class CombatLineOfFireSensor : Node2D
    {
        private const float DefaultProjectileRadius = 6f;
        private const uint DefaultHurtboxMask = 16;
        private const uint DefaultWorldMask = 8;

        private ShapeCast2D _shapeCast;
        private CircleShape2D _shape;

        public override void _Ready()
        {
            EnsureSensor();
        }

        public LineOfFireResult Query(
            CombatCharacter shooter,
            CombatCharacter intendedTarget,
            ProjectileSpecData projectileSpec = null)
        {
            return QueryFromOrigin(
                shooter,
                shooter?.CombatCenter ?? Vector2.Zero,
                intendedTarget,
                projectileSpec);
        }

        /// <summary>
        /// Query từ một vị trí giả định. AI dùng nó để thử trước hai firing slot trái/phải
        /// rồi chọn phía có hành lang bắn sạch, thay vì strafe ngẫu nhiên tới khi hết xui.
        /// </summary>
        public LineOfFireResult QueryFromOrigin(
            CombatCharacter shooter,
            Vector2 origin,
            CombatCharacter intendedTarget,
            ProjectileSpecData projectileSpec = null)
        {
            if (!IsUsable(shooter)
                || !IsUsable(intendedTarget)
                || !shooter.IsInsideTree()
                || !intendedTarget.IsAlive)
            {
                return LineOfFireResult.Invalid;
            }

            EnsureSensor();
            if (_shapeCast == null || _shape == null)
            {
                return LineOfFireResult.Invalid;
            }

            Vector2 destination = intendedTarget.CombatCenter;

            // Projectile thật sinh ra lệch về phía trước caster. Query cũng dịch origin cùng SpawnOffset
            // để đồng đội đứng sát phía sau Hyou không bị hiểu nhầm là đang chắn viên đạn.
            Vector2 initialDelta = destination - origin;
            if (initialDelta.LengthSquared() > 0.001f && projectileSpec != null)
            {
                float safeSpawnOffset = Mathf.Min(
                    Mathf.Max(0f, projectileSpec.SpawnOffset),
                    Mathf.Max(0f, initialDelta.Length() - 1f));
                origin += initialDelta.Normalized() * safeSpawnOffset;
            }

            Vector2 delta = destination - origin;
            float distance = delta.Length();
            if (distance <= 0.001f)
            {
                return LineOfFireResult.Clear(destination, 0f);
            }

            float radius = Mathf.Max(1f, projectileSpec?.Radius ?? DefaultProjectileRadius);
            uint hurtboxMask = projectileSpec?.HurtboxCollisionMask ?? DefaultHurtboxMask;
            uint worldMask = projectileSpec?.WorldCollisionMask ?? DefaultWorldMask;

            // Sensor là child của một Node thường (CombatDecisionAgent), nên Position ở đây
            // chính là tọa độ canvas/world và không ăn scale 2x của sprite nhân vật.
            Position = origin;
            Rotation = 0f;
            _shape.Radius = radius;
            _shapeCast.CollisionMask = hurtboxMask | worldMask;
            _shapeCast.TargetPosition = delta;
            _shapeCast.ClearExceptions();
            _shapeCast.AddException(shooter);
            _shapeCast.ForceShapecastUpdate();

            Vector2 direction = delta / distance;
            var hits = new List<(float Along, Node Collider, Vector2 Point)>();
            int count = _shapeCast.GetCollisionCount();
            for (int index = 0; index < count; index++)
            {
                if (_shapeCast.GetCollider(index) is not Node collider)
                {
                    continue;
                }

                Vector2 point = _shapeCast.GetCollisionPoint(index);
                float along = Mathf.Max(0f, (point - origin).Dot(direction));
                hits.Add((along, collider, point));
            }

            hits.Sort((left, right) => left.Along.CompareTo(right.Along));
            int hostileHitsBeforeTarget = 0;
            foreach (var hit in hits)
            {
                CombatCharacter actor = FindCombatCharacter(hit.Collider);
                if (actor == shooter)
                {
                    // Hurtbox của chính shooter vẫn có thể xuất hiện dù body đã nằm trong exception.
                    continue;
                }

                if (actor == intendedTarget)
                {
                    return LineOfFireResult.Clear(hit.Point, hit.Along);
                }

                if (actor != null)
                {
                    if (FactionRules.AreAllies(shooter.Faction, actor.Faction))
                    {
                        if (FactionRules.CanDamage(shooter.Faction, actor.Faction))
                        {
                            // Đồng minh party có friendly fire nên đây là blocker chiến thuật thật.
                            return new LineOfFireResult(
                                LineOfFireBlockerType.Ally,
                                actor,
                                hit.Point,
                                hit.Along);
                        }

                        // Ally không nhận friendly fire (ví dụ Enemy -> Enemy) cũng bị projectile runtime bỏ qua.
                        continue;
                    }

                    if (FactionRules.IsHostile(shooter.Faction, actor.Faction))
                    {
                        hostileHitsBeforeTarget++;
                        bool canPierceThisActor = projectileSpec?.PierceTargets == true
                            && hostileHitsBeforeTarget < Mathf.Max(1, projectileSpec.MaxTargetHits);
                        if (canPierceThisActor)
                        {
                            // Projectile thật cũng tiếp tục sau hit này, nên prediction phải làm y hệt.
                            continue;
                        }

                        return new LineOfFireResult(
                            LineOfFireBlockerType.Hostile,
                            actor,
                            hit.Point,
                            hit.Along);
                    }

                    // Actor trung lập / không damageable không làm projectile dừng trong runtime hiện tại.
                    // Vẫn giữ classification nếu sau này policy thay đổi, nhưng bây giờ cho corridor đi tiếp.
                    continue;
                }

                // Collider không thuộc CombatCharacter thì coi là world geometry: cây, đá, tường, TileMap...
                // Nếu projectile được author xuyên world, prediction cũng không được tự ý chặn nó.
                if (projectileSpec?.StopOnWorldCollision == false)
                {
                    continue;
                }

                return new LineOfFireResult(
                    LineOfFireBlockerType.World,
                    null,
                    hit.Point,
                    hit.Along);
            }

            // Không có collider nào trước target. Trường hợp target hurtbox chưa query được trong frame này
            // vẫn xem là clear vì hành lang từ origin tới CombatCenter không bị chắn.
            return LineOfFireResult.Clear(destination, distance);
        }

        private void EnsureSensor()
        {
            if (_shapeCast != null)
            {
                return;
            }

            _shape = new CircleShape2D { Radius = DefaultProjectileRadius };
            _shapeCast = new ShapeCast2D
            {
                Name = "ProjectileCorridor",
                Shape = _shape,
                Enabled = true,
                CollideWithAreas = true,
                CollideWithBodies = true,
                CollisionMask = DefaultHurtboxMask | DefaultWorldMask,
                TargetPosition = Vector2.Zero
            };
            AddChild(_shapeCast);
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
