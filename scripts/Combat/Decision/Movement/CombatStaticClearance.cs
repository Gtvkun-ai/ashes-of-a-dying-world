using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Static clearance P1: context steering 16 hướng nhưng chi phí thích nghi theo tình huống.
    /// - vùng trống: 4 ray / refresh;
    /// - tốc độ cao / có tín hiệu nguy cơ: 8 ray;
    /// - gần vật cản, corner hoặc stuck: đủ 16 ray.
    ///
    /// Thêm một ShapeCast tròn ở hướng tiến để đại diện bề rộng thân. Ray nhìn được khe hẹp,
    /// ShapeCast trả lời câu quan trọng hơn: "cả thân có lọt qua không?".
    /// </summary>
    internal sealed class CombatStaticClearance
    {
        public const int DirectionCount = 16;

        private readonly CombatCharacter _self;
        private readonly uint _obstacleMask;
        private readonly float _probeDistance;
        private readonly CombatMovementMetrics _metrics;
        private readonly bool _useForwardShape;
        private readonly float _bodyProbeRadius;
        private readonly RayCast2D[] _rays = new RayCast2D[DirectionCount];
        private readonly float[] _dangerCache = new float[DirectionCount];

        private Node2D _rig;
        private ShapeCast2D _forwardShape;
        private int _probePhase;
        private int _shapePhase;
        private bool _wasNearObstacle;
        private float _lastStrongestDanger;
        private Vector2 _shapeDirection = Vector2.Right;
        private float _shapeDanger;
        private ulong _lastRefreshPhysicsFrame = ulong.MaxValue;

        public CombatStaticClearance(
            CombatCharacter self,
            uint obstacleMask,
            float probeDistance,
            CombatMovementMetrics metrics,
            bool useForwardShape = true,
            float bodyProbeRadius = 7f)
        {
            _self = self;
            _obstacleMask = obstacleMask;
            _probeDistance = Mathf.Max(8f, probeDistance);
            _metrics = metrics;
            _useForwardShape = useForwardShape;
            _bodyProbeRadius = Mathf.Clamp(bodyProbeRadius, 2f, _probeDistance * 0.40f);
            BuildSensorRig();
        }

        public void Refresh(
            Vector2 selfPosition,
            Vector2 desiredDirection,
            bool forceDense,
            float speedFraction = 0.65f)
        {
            ulong physicsFrame = Engine.GetPhysicsFrames();
            if (physicsFrame == _lastRefreshPhysicsFrame)
            {
                return;
            }
            _lastRefreshPhysicsFrame = physicsFrame;

            float normalizedSpeed = Mathf.Clamp(speedFraction, 0f, 1.5f);
            int sampleCount = ResolveSampleCount(forceDense, normalizedSpeed);
            int stride = DirectionCount / sampleCount;
            int phase = stride > 1 ? _probePhase % stride : 0;
            float strongest = 0f;

            for (int slot = 0; slot < DirectionCount; slot++)
            {
                bool sample = stride == 1 || slot % stride == phase;
                if (!sample)
                {
                    // Ray cũ được decay nhẹ để một va chạm đã đi qua không ám cache mãi.
                    _dangerCache[slot] *= sampleCount <= 4 ? 0.88f : 0.95f;
                    strongest = Mathf.Max(strongest, _dangerCache[slot]);
                    continue;
                }

                RayCast2D ray = _rays[slot];
                float danger = 0f;
                if (ray != null && GodotObject.IsInstanceValid(ray) && ray.IsInsideTree())
                {
                    float angle = Mathf.Tau * slot / DirectionCount;
                    Vector2 direction = Vector2.Right.Rotated(angle);
                    float forwardAlignment = desiredDirection.LengthSquared() > 0.001f
                        ? Mathf.Max(0f, direction.Dot(desiredDirection.Normalized()))
                        : 0f;
                    float lookAheadScale = 0.78f + 0.30f * normalizedSpeed + 0.20f * forwardAlignment;
                    ray.TargetPosition = direction * (_probeDistance * lookAheadScale);
                    ray.ForceRaycastUpdate();
                    _metrics?.RecordProbe();
                    if (ray.IsColliding())
                    {
                        float maxDistance = Mathf.Max(1f, ray.TargetPosition.Length());
                        float hitDistance = selfPosition.DistanceTo(ray.GetCollisionPoint());
                        danger = 1f - Mathf.Clamp(hitDistance / maxDistance, 0f, 1f);
                    }
                }

                _dangerCache[slot] = danger;
                strongest = Mathf.Max(strongest, danger);
            }

            _metrics?.RecordProbeRefresh(sampleCount);
            _probePhase++;

            RefreshForwardShape(desiredDirection, forceDense, normalizedSpeed);
            strongest = Mathf.Max(strongest, _shapeDanger);
            _lastStrongestDanger = strongest;
            _wasNearObstacle = strongest > 0.08f;
        }

        public float GetDanger(int slot)
        {
            int center = Wrap(slot);
            int left1 = Wrap(slot - 1);
            int right1 = Wrap(slot + 1);
            int left2 = Wrap(slot - 2);
            int right2 = Wrap(slot + 2);

            float danger = _dangerCache[center];
            danger = Mathf.Max(danger, _dangerCache[left1] * 0.72f);
            danger = Mathf.Max(danger, _dangerCache[right1] * 0.72f);
            danger = Mathf.Max(danger, _dangerCache[left2] * 0.34f);
            danger = Mathf.Max(danger, _dangerCache[right2] * 0.34f);

            if (_shapeDanger > 0f)
            {
                float angle = Mathf.Tau * center / DirectionCount;
                Vector2 direction = Vector2.Right.Rotated(angle);
                float alignment = direction.Dot(_shapeDirection);
                if (alignment > 0.20f)
                {
                    float shapeWeight = Mathf.Clamp((alignment - 0.20f) / 0.80f, 0f, 1f);
                    danger = Mathf.Max(danger, _shapeDanger * shapeWeight);
                }
            }

            return Mathf.Clamp(danger, 0f, 1f);
        }

        public bool IsNearObstacle => _wasNearObstacle;

        public void Dispose()
        {
            if (_rig != null && GodotObject.IsInstanceValid(_rig))
            {
                _rig.QueueFree();
            }
            _rig = null;
            _forwardShape = null;
        }

        private int ResolveSampleCount(bool forceDense, float speedFraction)
        {
            if (forceDense || _wasNearObstacle || _lastStrongestDanger > 0.16f)
            {
                return DirectionCount;
            }

            if (speedFraction >= 0.78f || _lastStrongestDanger > 0.025f)
            {
                return DirectionCount / 2;
            }

            return DirectionCount / 4;
        }

        private void RefreshForwardShape(Vector2 desiredDirection, bool forceDense, float speedFraction)
        {
            if (!_useForwardShape
                || _forwardShape == null
                || !GodotObject.IsInstanceValid(_forwardShape)
                || !_forwardShape.IsInsideTree()
                || desiredDirection.LengthSquared() <= 0.001f)
            {
                _shapeDanger *= 0.82f;
                return;
            }

            // ShapeCast đắt hơn ray, nên vùng trống chỉ chạy 1/3 refresh; gần obstacle/stuck thì chạy liên tục.
            bool shouldSample = forceDense || _wasNearObstacle || speedFraction >= 0.82f || (_shapePhase++ % 3 == 0);
            if (!shouldSample)
            {
                _shapeDanger *= 0.90f;
                return;
            }

            _shapeDirection = desiredDirection.Normalized();
            float lookAhead = _probeDistance * (0.72f + 0.34f * Mathf.Clamp(speedFraction, 0f, 1.25f));
            _forwardShape.TargetPosition = _shapeDirection * lookAhead;
            _forwardShape.ForceShapecastUpdate();
            _metrics?.RecordShapeProbe();

            if (!_forwardShape.IsColliding())
            {
                _shapeDanger = 0f;
                return;
            }

            float safeFraction = Mathf.Clamp(_forwardShape.GetClosestCollisionSafeFraction(), 0f, 1f);
            _shapeDanger = Mathf.Clamp(1f - safeFraction, 0f, 1f);
        }

        private void BuildSensorRig()
        {
            if (_self == null || !_self.IsInsideTree())
            {
                return;
            }

            _rig = new Node2D { Name = "CombatStaticClearanceRuntime" };
            for (int slot = 0; slot < DirectionCount; slot++)
            {
                float angle = Mathf.Tau * slot / DirectionCount;
                Vector2 direction = Vector2.Right.Rotated(angle);
                var ray = new RayCast2D
                {
                    Name = $"ClearanceRay{slot:00}",
                    Enabled = true,
                    TargetPosition = direction * _probeDistance,
                    CollisionMask = _obstacleMask,
                    CollideWithAreas = false,
                    CollideWithBodies = true,
                    ExcludeParent = false
                };
                _rig.AddChild(ray);
                ray.AddException(_self);
                _rays[slot] = ray;
            }

            if (_useForwardShape)
            {
                _forwardShape = new ShapeCast2D
                {
                    Name = "ForwardBodyClearance",
                    Enabled = true,
                    Shape = new CircleShape2D { Radius = _bodyProbeRadius },
                    TargetPosition = Vector2.Right * _probeDistance,
                    CollisionMask = _obstacleMask,
                    CollideWithAreas = false,
                    CollideWithBodies = true,
                    ExcludeParent = false,
                    MaxResults = 4,
                    Margin = 0.5f
                };
                _rig.AddChild(_forwardShape);
                _forwardShape.AddException(_self);
            }

            _self.CallDeferred("add_child", _rig);
        }

        private static int Wrap(int slot)
        {
            int wrapped = slot % DirectionCount;
            return wrapped < 0 ? wrapped + DirectionCount : wrapped;
        }
    }
}
