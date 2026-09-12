using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.World.Navigation;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Lý do target không được xem là "đang nhìn thấy".
    /// Tách riêng để debug được AI mất target vì range/FOV/cao độ hay vì world geometry.
    /// </summary>
    public enum CombatVisionBlockReason
    {
        None = 0,
        InvalidTarget = 1,
        OutOfRange = 2,
        OutsideFieldOfView = 3,
        DifferentElevation = 4,
        WorldOccluded = 5,
        SensorUnavailable = 6
    }

    public readonly struct CombatVisionResult
    {
        public bool Visible { get; }
        public CombatVisionBlockReason BlockReason { get; }
        public float Distance { get; }
        public int ObserverElevation { get; }
        public int TargetElevation { get; }

        public CombatVisionResult(
            bool visible,
            CombatVisionBlockReason blockReason,
            float distance,
            int observerElevation,
            int targetElevation)
        {
            Visible = visible;
            BlockReason = blockReason;
            Distance = Mathf.Max(0f, distance);
            ObserverElevation = observerElevation;
            TargetElevation = targetElevation;
        }
    }

    /// <summary>
    /// P3.2 perception sensor: awareness radius chỉ giúp tìm candidate; sensor này mới quyết định
    /// AI có THỰC SỰ nhìn thấy target hay không.
    ///
    /// Thứ tự gate: range -> elevation -> FOV -> world LOS.
    /// World LOS dùng mask riêng, không dùng projectile corridor vì đồng đội đứng giữa không làm Hyou
    /// "mù"; họ chỉ có thể chặn đường bắn ở tầng LineOfFire.
    /// </summary>
    public sealed class CombatVisionSensor
    {
        private readonly RayCast2D _ray;
        private readonly float _visionRange;
        private readonly float _fovDegrees;
        private readonly bool _requireLineOfSight;
        private readonly uint _occlusionMask;
        private readonly bool _requireSameElevation;

        private WorldNavigationTopology2D _topology;
        private long _queries;
        private long _visibleQueries;
        private long _rangeBlocks;
        private long _fovBlocks;
        private long _elevationBlocks;
        private long _occlusionBlocks;
        private CombatVisionBlockReason _lastReason = CombatVisionBlockReason.None;

        public CombatVisionSensor(
            RayCast2D ray,
            float visionRange,
            float fovDegrees,
            bool requireLineOfSight,
            uint occlusionMask,
            bool requireSameElevation)
        {
            _ray = ray;
            _visionRange = Mathf.Max(1f, visionRange);
            _fovDegrees = Mathf.Clamp(fovDegrees, 1f, 360f);
            _requireLineOfSight = requireLineOfSight;
            _occlusionMask = occlusionMask;
            _requireSameElevation = requireSameElevation;

            ConfigureRay();
        }

        public float VisionRange => _visionRange;

        public CombatVisionResult Evaluate(CombatCharacter observer, CombatCharacter target)
        {
            _queries++;
            if (!IsUsable(observer) || !IsUsable(target) || !target.IsAlive)
            {
                return Block(CombatVisionBlockReason.InvalidTarget, 0f, 0, 0);
            }

            Vector2 origin = observer.CombatCenter;
            Vector2 delta = target.CombatCenter - origin;
            float distance = delta.Length();
            if (distance > _visionRange)
            {
                _rangeBlocks++;
                return Block(CombatVisionBlockReason.OutOfRange, distance, 0, 0);
            }

            int observerElevation = 0;
            int targetElevation = 0;
            if (_requireSameElevation)
            {
                ResolveTopologyIfNeeded(observer);
                if (_topology != null && GodotObject.IsInstanceValid(_topology) && _topology.Enabled)
                {
                    observerElevation = _topology.ResolveElevation(origin);
                    targetElevation = _topology.ResolveElevation(target.CombatCenter);
                    if (observerElevation != targetElevation)
                    {
                        _elevationBlocks++;
                        return Block(
                            CombatVisionBlockReason.DifferentElevation,
                            distance,
                            observerElevation,
                            targetElevation);
                    }
                }
            }

            if (_fovDegrees < 359.5f && distance > 0.001f)
            {
                Vector2 facing = observer.FacingDirection;
                if (facing.LengthSquared() > 0.001f)
                {
                    float halfFovRadians = Mathf.DegToRad(_fovDegrees * 0.5f);
                    float minimumDot = Mathf.Cos(halfFovRadians);
                    float facingDot = facing.Normalized().Dot(delta / distance);
                    if (facingDot < minimumDot)
                    {
                        _fovBlocks++;
                        return Block(
                            CombatVisionBlockReason.OutsideFieldOfView,
                            distance,
                            observerElevation,
                            targetElevation);
                    }
                }
            }

            if (_requireLineOfSight)
            {
                if (_ray == null || !GodotObject.IsInstanceValid(_ray) || !_ray.IsInsideTree())
                {
                    return Block(
                        CombatVisionBlockReason.SensorUnavailable,
                        distance,
                        observerElevation,
                        targetElevation);
                }

                ConfigureRay();
                _ray.GlobalPosition = origin;
                _ray.TargetPosition = _ray.ToLocal(target.CombatCenter);
                _ray.ClearExceptions();
                _ray.AddException(observer);
                _ray.ForceRaycastUpdate();

                if (_ray.IsColliding())
                {
                    GodotObject collider = _ray.GetCollider();
                    if (!IsTargetCollider(collider, target))
                    {
                        _occlusionBlocks++;
                        return Block(
                            CombatVisionBlockReason.WorldOccluded,
                            distance,
                            observerElevation,
                            targetElevation);
                    }
                }
            }

            _visibleQueries++;
            _lastReason = CombatVisionBlockReason.None;
            return new CombatVisionResult(
                true,
                CombatVisionBlockReason.None,
                distance,
                observerElevation,
                targetElevation);
        }

        public bool CanSee(CombatCharacter observer, CombatCharacter target)
        {
            return Evaluate(observer, target).Visible;
        }

        public string ToCompactString()
        {
            return $"vision=q{_queries} visible={_visibleQueries} block(range={_rangeBlocks},fov={_fovBlocks},elev={_elevationBlocks},world={_occlusionBlocks}) last={_lastReason}";
        }

        public void ResetDiagnostics()
        {
            _queries = 0;
            _visibleQueries = 0;
            _rangeBlocks = 0;
            _fovBlocks = 0;
            _elevationBlocks = 0;
            _occlusionBlocks = 0;
            _lastReason = CombatVisionBlockReason.None;
        }

        private CombatVisionResult Block(
            CombatVisionBlockReason reason,
            float distance,
            int observerElevation,
            int targetElevation)
        {
            _lastReason = reason;
            return new CombatVisionResult(
                false,
                reason,
                distance,
                observerElevation,
                targetElevation);
        }

        private void ConfigureRay()
        {
            if (_ray == null || !GodotObject.IsInstanceValid(_ray))
            {
                return;
            }

            _ray.Enabled = true;
            _ray.CollisionMask = _occlusionMask;
            _ray.CollideWithAreas = false;
            _ray.CollideWithBodies = true;
            _ray.ExcludeParent = true;
        }

        private void ResolveTopologyIfNeeded(Node context)
        {
            if (_topology != null && GodotObject.IsInstanceValid(_topology))
            {
                return;
            }

            _topology = WorldNavigationTopology2D.FindFor(context);
        }

        private static bool IsTargetCollider(GodotObject collider, CombatCharacter target)
        {
            if (collider == target)
            {
                return true;
            }

            return collider is Node colliderNode && target.IsAncestorOf(colliderNode);
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
