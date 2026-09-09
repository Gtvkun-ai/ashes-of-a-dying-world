using System.Collections.Generic;
using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Dynamic avoidance P1 dựa trên RVO của NavigationAgent2D.
    /// Spatial hash chỉ làm broad-phase để phát hiện tình huống head-on; RVO vẫn là solver va chạm động chính.
    /// Khi gặp một cặp đối đầu trực diện, hai actor khóa cùng "quy tắc passing side" theo pair-id trong một khoảng ngắn,
    /// tránh flip trái/phải giữa các frame và giảm deadlock ở doorway/crossing.
    /// </summary>
    internal sealed class CombatDynamicAvoidance
    {
        private readonly CombatCharacter _self;
        private readonly NavigationAgent2D _agent;
        private readonly CombatMovementMetrics _metrics;
        private readonly bool _enabled;
        private readonly float _defaultSideBiasSign;
        private readonly float _neighborDistance;
        private readonly float _passingLockSeconds;
        private readonly float _headOnBiasStrength;
        private readonly List<CombatCharacter> _neighborBuffer = new(16);

        private Vector2 _lastSafeVelocity;
        private ulong _lastSafeVelocityTickMs;
        private bool _subscribed;
        private ulong _passingPartnerId;
        private float _passingSideSign;
        private ulong _passingLockUntilMs;

        public CombatDynamicAvoidance(
            CombatCharacter self,
            NavigationAgent2D agent,
            CombatMovementMetrics metrics,
            bool enabled,
            float radius,
            float neighborDistance,
            int maxNeighbors,
            float timeHorizonAgents,
            float timeHorizonObstacles,
            float passingLockSeconds = 0.75f,
            float headOnBiasStrength = 0.09f)
        {
            _self = self;
            _agent = agent;
            _metrics = metrics;
            _enabled = enabled && IsReady();
            _defaultSideBiasSign = self != null && (self.GetInstanceId() & 1UL) == 0UL ? 1f : -1f;
            _neighborDistance = Mathf.Max(radius * 2f, neighborDistance);
            _passingLockSeconds = Mathf.Clamp(passingLockSeconds, 0.20f, 2f);
            _headOnBiasStrength = Mathf.Clamp(headOnBiasStrength, 0.025f, 0.20f);

            if (!_enabled)
            {
                return;
            }

            _agent.Radius = Mathf.Max(2f, radius);
            _agent.NeighborDistance = _neighborDistance;
            _agent.MaxNeighbors = Mathf.Max(1, maxNeighbors);
            _agent.TimeHorizonAgents = Mathf.Max(0.1f, timeHorizonAgents);
            _agent.TimeHorizonObstacles = Mathf.Max(0.1f, timeHorizonObstacles);
            _agent.MaxSpeed = Mathf.Max(1f, self?.RunSpeed ?? 100f);
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

            float sideSign = ResolvePassingSideSign(preferredVelocity, out bool hasPassingLock);
            Vector2 biased = ApplyDeterministicSideBias(preferredVelocity, sideSign, hasPassingLock);
            _agent.MaxSpeed = Mathf.Max(1f, biased.Length());
            _agent.Velocity = biased;
            _metrics?.RecordAvoidanceSubmission();

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
            _passingPartnerId = 0;
            _passingLockUntilMs = 0;
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

        private float ResolvePassingSideSign(Vector2 preferredVelocity, out bool hasPassingLock)
        {
            ulong now = Time.GetTicksMsec();
            if (_passingLockUntilMs > now && _passingPartnerId != 0)
            {
                hasPassingLock = true;
                return _passingSideSign;
            }

            hasPassingLock = false;
            _passingPartnerId = 0;
            _passingLockUntilMs = 0;

            SceneTree tree = _self?.GetTree();
            if (tree == null || preferredVelocity.LengthSquared() <= 1f)
            {
                return _defaultSideBiasSign;
            }

            CombatSpatialIndex.QueryRadius(tree, _self.CombatCenter, _neighborDistance, _neighborBuffer);
            Vector2 forward = preferredVelocity.Normalized();
            CombatCharacter best = null;
            float bestScore = 0f;

            for (int i = 0; i < _neighborBuffer.Count; i++)
            {
                CombatCharacter other = _neighborBuffer[i];
                if (other == null || other == _self || !other.IsAlive)
                {
                    continue;
                }

                Vector2 delta = other.CombatCenter - _self.CombatCenter;
                float distance = delta.Length();
                if (distance <= 0.001f || distance > _neighborDistance)
                {
                    continue;
                }

                Vector2 toOther = delta / distance;
                float directlyAhead = forward.Dot(toOther);
                if (directlyAhead < 0.58f)
                {
                    continue;
                }

                Vector2 otherVelocity = other.Velocity;
                if (otherVelocity.LengthSquared() < 25f)
                {
                    continue;
                }

                Vector2 otherForward = otherVelocity.Normalized();
                float opposing = (-forward).Dot(otherForward);
                if (opposing < 0.52f)
                {
                    continue;
                }

                Vector2 relativeVelocity = preferredVelocity - otherVelocity;
                float closingSpeed = relativeVelocity.Dot(toOther);
                if (closingSpeed <= 4f)
                {
                    continue;
                }

                float proximity = 1f - Mathf.Clamp(distance / _neighborDistance, 0f, 1f);
                float score = 0.45f * directlyAhead + 0.35f * opposing + 0.20f * proximity;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = other;
                }
            }

            if (best == null)
            {
                return _defaultSideBiasSign;
            }

            _passingPartnerId = best.GetInstanceId();
            _passingSideSign = ResolvePairSide(_self.GetInstanceId(), _passingPartnerId);
            _passingLockUntilMs = now + (ulong)Mathf.RoundToInt(_passingLockSeconds * 1000f);
            _metrics?.RecordPassingSideLock();
            hasPassingLock = true;
            return _passingSideSign;
        }

        private Vector2 ApplyDeterministicSideBias(Vector2 velocity, float sideSign, bool passingLocked)
        {
            float speed = velocity.Length();
            if (speed <= 0.001f)
            {
                return velocity;
            }

            Vector2 forward = velocity / speed;
            Vector2 side = new(-forward.Y, forward.X);
            float strength = passingLocked ? _headOnBiasStrength : 0.018f;
            Vector2 biased = forward + side * (sideSign * strength);
            return biased.Normalized() * speed;
        }

        private static float ResolvePairSide(ulong leftId, ulong rightId)
        {
            ulong min = leftId < rightId ? leftId : rightId;
            ulong max = leftId < rightId ? rightId : leftId;
            ulong hash = min ^ (max << 1) ^ (min >> 3) ^ (max >> 5);
            return (hash & 1UL) == 0UL ? 1f : -1f;
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
