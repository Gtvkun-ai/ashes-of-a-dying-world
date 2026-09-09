using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Global path P1: giữ NavigationAgent2D nhưng giảm repath thừa bằng corridor reuse + stagger.
    /// Target di chuyển ít sẽ dùng tiếp corridor cũ; target trôi đủ xa hoặc corridor quá cũ mới xin path mới.
    /// Global budget vẫn chặn burst nhiều AI trong cùng physics frame.
    /// </summary>
    internal sealed class CombatGlobalPathPlanner
    {
        private readonly CombatCharacter _self;
        private readonly NavigationAgent2D _agent;
        private readonly CombatMovementMetrics _metrics;
        private readonly float _navigationThreshold;
        private readonly float _targetRefreshDistanceSq;
        private readonly float _pathReuseDistanceSq;
        private readonly float _repathIntervalSeconds;
        private readonly float _maxTargetReuseSeconds;
        private readonly int _pathBudgetPerFrame;
        private readonly float _repathStaggerSeconds;

        private Vector2 _lastNavigationTarget = new(float.PositiveInfinity, float.PositiveInfinity);
        private float _nextRepathTime;
        private float _lastTargetCommitTime = float.NegativeInfinity;
        private bool _hasCommittedTarget;

        public CombatGlobalPathPlanner(
            CombatCharacter self,
            NavigationAgent2D agent,
            CombatMovementMetrics metrics,
            float arrivalDistance,
            float navigationThreshold,
            float targetRefreshDistance,
            float pathReuseDistance,
            float repathIntervalSeconds,
            float maxTargetReuseSeconds,
            int pathBudgetPerFrame)
        {
            _self = self;
            _agent = agent;
            _metrics = metrics;
            _navigationThreshold = Mathf.Max(arrivalDistance, navigationThreshold);
            float refreshDistance = Mathf.Max(2f, targetRefreshDistance);
            float reuseDistance = Mathf.Max(refreshDistance, pathReuseDistance);
            _targetRefreshDistanceSq = refreshDistance * refreshDistance;
            _pathReuseDistanceSq = reuseDistance * reuseDistance;
            _repathIntervalSeconds = Mathf.Max(0.05f, repathIntervalSeconds);
            _maxTargetReuseSeconds = Mathf.Max(_repathIntervalSeconds, maxTargetReuseSeconds);
            _pathBudgetPerFrame = Mathf.Max(1, pathBudgetPerFrame);

            ulong actorId = self?.GetInstanceId() ?? 0UL;
            _repathStaggerSeconds = ((actorId % 7UL) / 7f) * _repathIntervalSeconds;
            _nextRepathTime = NowSeconds() + _repathStaggerSeconds;

            if (IsReady())
            {
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
            bool forceRepath = false)
        {
            if (!IsReady() || anchorDistance < _navigationThreshold)
            {
                return fallbackDirection;
            }

            float timeSeconds = NowSeconds();
            float driftSq = _hasCommittedTarget
                ? _lastNavigationTarget.DistanceSquaredTo(navigationTarget)
                : float.PositiveInfinity;
            float age = timeSeconds - _lastTargetCommitTime;
            bool targetMeaningfullyChanged = !_hasCommittedTarget
                || driftSq > _pathReuseDistanceSq
                || (driftSq > _targetRefreshDistanceSq && age >= _maxTargetReuseSeconds);
            bool wantsRefresh = forceRepath || targetMeaningfullyChanged;
            bool canRefreshNow = forceRepath || timeSeconds >= _nextRepathTime;

            if (wantsRefresh && canRefreshNow)
            {
                if (CombatPathBudget.TryConsume(_pathBudgetPerFrame))
                {
                    _lastNavigationTarget = navigationTarget;
                    _agent.TargetPosition = navigationTarget;
                    _lastTargetCommitTime = timeSeconds;
                    _hasCommittedTarget = true;
                    _nextRepathTime = timeSeconds + _repathIntervalSeconds + _repathStaggerSeconds * 0.20f;
                    _metrics?.RecordPathRequest();
                }
                else
                {
                    // Không bỏ corridor cũ khi budget đầy. Reuse tốt hơn việc đứng chờ một frame path mới.
                    _metrics?.RecordPathBudgetDeferral();
                    if (_hasCommittedTarget)
                    {
                        _metrics?.RecordPathReuse();
                    }
                }
            }
            else if (_hasCommittedTarget)
            {
                _metrics?.RecordPathReuse();
            }

            if (_agent.IsNavigationFinished())
            {
                return fallbackDirection;
            }

            // Godot cần gọi đều trong physics update để corridor/waypoint nội bộ tiến đúng nhịp.
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

            Vector2 blended = fallback * 0.38f + nav * 0.62f;
            return blended.LengthSquared() > 0.001f ? blended.Normalized() : nav;
        }

        public void Reset()
        {
            _lastNavigationTarget = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            _nextRepathTime = NowSeconds() + _repathStaggerSeconds;
            _lastTargetCommitTime = float.NegativeInfinity;
            _hasCommittedTarget = false;
        }

        private static float NowSeconds()
        {
            return Time.GetTicksMsec() / 1000f;
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
