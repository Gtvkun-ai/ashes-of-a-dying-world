using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Tầng global path của P0.
    /// NavigationAgent2D chỉ trả hướng waypoint đường dài; local steering vẫn quyết định né sát tường/đứng combat.
    /// </summary>
    internal sealed class CombatGlobalPathPlanner
    {
        private readonly CombatCharacter _self;
        private readonly NavigationAgent2D _agent;
        private readonly CombatMovementMetrics _metrics;
        private readonly float _navigationThreshold;
        private readonly float _targetRefreshDistanceSq;
        private readonly float _repathIntervalSeconds;
        private readonly int _pathBudgetPerFrame;

        private Vector2 _lastNavigationTarget = new(float.PositiveInfinity, float.PositiveInfinity);
        private float _nextRepathTime;

        public CombatGlobalPathPlanner(
            CombatCharacter self,
            NavigationAgent2D agent,
            CombatMovementMetrics metrics,
            float arrivalDistance,
            float navigationThreshold,
            float targetRefreshDistance,
            float repathIntervalSeconds,
            int pathBudgetPerFrame)
        {
            _self = self;
            _agent = agent;
            _metrics = metrics;
            _navigationThreshold = Mathf.Max(arrivalDistance, navigationThreshold);
            float refreshDistance = Mathf.Max(2f, targetRefreshDistance);
            _targetRefreshDistanceSq = refreshDistance * refreshDistance;
            _repathIntervalSeconds = Mathf.Max(0.05f, repathIntervalSeconds);
            _pathBudgetPerFrame = Mathf.Max(1, pathBudgetPerFrame);

            if (IsReady())
            {
                // Khoảng waypoint vừa đủ để CharacterBody2D không overshoot rồi repath liên tục.
                _agent.PathDesiredDistance = Mathf.Max(5f, arrivalDistance * 1.25f);
                _agent.TargetDesiredDistance = Mathf.Max(5f, arrivalDistance);
                _agent.PathMaxDistance = Mathf.Max(32f, navigationThreshold * 1.5f);
                _agent.SimplifyPath = true;
                _agent.SimplifyEpsilon = 3f;
            }
        }

        public Vector2 ResolveDirection(
            Vector2 selfPosition,
            Vector2 navigationTarget,
            Vector2 fallbackDirection,
            float anchorDistance,
            float timeSeconds,
            bool forceRepath = false)
        {
            if (!IsReady() || anchorDistance < _navigationThreshold)
            {
                return fallbackDirection;
            }

            bool targetChanged = _lastNavigationTarget.DistanceSquaredTo(navigationTarget) > _targetRefreshDistanceSq;
            bool canRefreshNow = forceRepath || timeSeconds >= _nextRepathTime;
            if ((forceRepath || targetChanged) && canRefreshNow && CombatPathBudget.TryConsume(_pathBudgetPerFrame))
            {
                _lastNavigationTarget = navigationTarget;
                _agent.TargetPosition = navigationTarget;
                _nextRepathTime = timeSeconds + _repathIntervalSeconds;
                _metrics?.RecordPathRequest();
            }

            if (_agent.IsNavigationFinished())
            {
                return fallbackDirection;
            }

            // Godot yêu cầu GetNextPathPosition() được gọi đều để NavigationAgent cập nhật corridor nội bộ.
            Vector2 next = _agent.GetNextPathPosition();
            Vector2 navDirection = next - selfPosition;
            if (navDirection.LengthSquared() <= 0.001f)
            {
                return fallbackDirection;
            }

            _metrics?.RecordPathDirectionUsed();
            Vector2 nav = navDirection.Normalized();
            Vector2 fallback = fallbackDirection.LengthSquared() > 0.001f
                ? fallbackDirection.Normalized()
                : nav;

            // Global path chiếm ưu thế vừa phải. Local solver vẫn được quyền sửa hướng để giữ spacing/combat lane.
            Vector2 blended = fallback * 0.38f + nav * 0.62f;
            return blended.LengthSquared() > 0.001f ? blended.Normalized() : nav;
        }

        public void Reset()
        {
            _lastNavigationTarget = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            _nextRepathTime = 0f;
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
