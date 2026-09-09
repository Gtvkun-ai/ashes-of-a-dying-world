using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Dynamic avoidance P0 dựa trên RVO của NavigationAgent2D.
    /// Nếu scene chưa có NavAgent/nav map, module tự fallback về preferred velocity, không phá AI cũ.
    /// </summary>
    internal sealed class CombatDynamicAvoidance
    {
        private readonly CombatCharacter _self;
        private readonly NavigationAgent2D _agent;
        private readonly CombatMovementMetrics _metrics;
        private readonly bool _enabled;
        private readonly float _sideBiasSign;

        private Vector2 _lastSafeVelocity;
        private ulong _lastSafeVelocityTickMs;
        private bool _subscribed;

        public CombatDynamicAvoidance(
            CombatCharacter self,
            NavigationAgent2D agent,
            CombatMovementMetrics metrics,
            bool enabled,
            float radius,
            float neighborDistance,
            int maxNeighbors,
            float timeHorizonAgents,
            float timeHorizonObstacles)
        {
            _self = self;
            _agent = agent;
            _metrics = metrics;
            _enabled = enabled && IsReady();
            _sideBiasSign = self != null && (self.GetInstanceId() & 1UL) == 0UL ? 1f : -1f;

            if (!_enabled)
            {
                return;
            }

            _agent.Radius = Mathf.Max(2f, radius);
            _agent.NeighborDistance = Mathf.Max(_agent.Radius * 2f, neighborDistance);
            _agent.MaxNeighbors = Mathf.Max(1, maxNeighbors);
            _agent.TimeHorizonAgents = Mathf.Max(0.1f, timeHorizonAgents);
            _agent.TimeHorizonObstacles = Mathf.Max(0.1f, timeHorizonObstacles);
            _agent.MaxSpeed = Mathf.Max(1f, self?.RunSpeed ?? 100f);
            // Chỉ đăng ký RVO khi motor thật sự cần chạy. ShadowMode/AI đứng yên không phải trả CPU vô ích.
            _agent.AvoidanceEnabled = false;
            _agent.VelocityComputed += OnVelocityComputed;
            _subscribed = true;
        }

        public Vector2 ResolveVelocity(Vector2 preferredVelocity)
        {
            if (!_enabled || !IsReady())
            {
                return preferredVelocity;
            }

            if (preferredVelocity.LengthSquared() <= 0.001f)
            {
                ResetVelocity();
                return Vector2.Zero;
            }

            if (!_agent.AvoidanceEnabled)
            {
                _agent.AvoidanceEnabled = true;
            }

            Vector2 biased = ApplyDeterministicSideBias(preferredVelocity);
            _agent.MaxSpeed = Mathf.Max(1f, biased.Length());
            _agent.Velocity = biased;
            _metrics?.RecordAvoidanceSubmission();

            // velocity_computed tới theo nhịp NavigationServer. Nếu dữ liệu đã quá cũ, dùng preferred velocity ngay.
            ulong now = Time.GetTicksMsec();
            if (_lastSafeVelocityTickMs == 0 || now - _lastSafeVelocityTickMs > 220)
            {
                return biased;
            }

            return _lastSafeVelocity;
        }

        public void ResetVelocity()
        {
            _lastSafeVelocity = Vector2.Zero;
            _lastSafeVelocityTickMs = 0;
            if (_enabled && IsReady())
            {
                _agent.Velocity = Vector2.Zero;
                _agent.AvoidanceEnabled = false;
            }
        }

        public void Dispose()
        {
            if (_subscribed && _agent != null && GodotObject.IsInstanceValid(_agent))
            {
                _agent.VelocityComputed -= OnVelocityComputed;
                _agent.AvoidanceEnabled = false;
            }
            _subscribed = false;
        }

        private void OnVelocityComputed(Vector2 safeVelocity)
        {
            _lastSafeVelocity = safeVelocity;
            _lastSafeVelocityTickMs = Time.GetTicksMsec();

            Vector2 requested = _agent?.Velocity ?? Vector2.Zero;
            if (requested.LengthSquared() > 0.001f
                && safeVelocity.DistanceSquaredTo(requested) > 16f)
            {
                _metrics?.RecordAvoidanceCorrection();
            }
        }

        private Vector2 ApplyDeterministicSideBias(Vector2 velocity)
        {
            float speed = velocity.Length();
            if (speed <= 0.001f)
            {
                return velocity;
            }

            Vector2 forward = velocity / speed;
            Vector2 side = new(-forward.Y, forward.X);

            // RVO có thể đối xứng hoàn hảo ở tình huống head-on. Bias cực nhỏ và ổn định theo instance id
            // khiến hai agent chọn phía vượt nhau nhất quán thay vì cùng lắc trái/phải.
            Vector2 biased = forward + side * (_sideBiasSign * 0.035f);
            return biased.Normalized() * speed;
        }

        private bool IsReady()
        {
            return _self != null
                && _agent != null
                && GodotObject.IsInstanceValid(_agent)
                && _agent.IsInsideTree();
        }
    }
}
