using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.World.Navigation;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// P3 semantic router theo cao độ. Nó không thay NavigationAgent/RVO mà đặt waypoint topology
    /// ở trước chúng: khác tầng -> chân cầu thang -> đầu cầu thang -> mục tiêu thật.
    /// </summary>
    public sealed class CombatElevationRouter
    {
        public readonly struct RouteResult
        {
            public Vector2 MovementTarget { get; }
            public bool IsTopologyRouted { get; }
            public bool HasRoute { get; }
            public int SelfElevation { get; }
            public int TargetElevation { get; }
            public string LinkId { get; }
            public string Phase { get; }

            public RouteResult(
                Vector2 movementTarget,
                bool isTopologyRouted,
                bool hasRoute,
                int selfElevation,
                int targetElevation,
                string linkId,
                string phase)
            {
                MovementTarget = movementTarget;
                IsTopologyRouted = isTopologyRouted;
                HasRoute = hasRoute;
                SelfElevation = selfElevation;
                TargetElevation = targetElevation;
                LinkId = linkId ?? string.Empty;
                Phase = phase ?? "direct";
            }
        }

        private readonly CombatCharacter _self;
        private readonly bool _enabled;
        private readonly bool _failClosed;
        private readonly bool _debugLogging;

        private WorldNavigationTopology2D _topology;
        private StairLink2D _activeLink;
        private int _activeFromElevation;
        private int _activeToElevation;
        private bool _traversing;
        private string _lastSummary = "topology=unresolved";

        public CombatElevationRouter(
            CombatCharacter self,
            bool enabled,
            bool failClosed,
            bool debugLogging)
        {
            _self = self;
            _enabled = enabled;
            _failClosed = failClosed;
            _debugLogging = debugLogging;
        }

        public RouteResult Resolve(
            Vector2 selfPosition,
            Vector2 finalMovementTarget,
            Vector2 semanticTargetPosition)
        {
            if (!_enabled || _self == null)
            {
                return Direct(finalMovementTarget, 0, 0);
            }

            ResolveTopologyIfNeeded();
            if (_topology == null || !GodotObject.IsInstanceValid(_topology) || !_topology.Enabled)
            {
                _lastSummary = "topology=missing fallback=direct";
                return Direct(finalMovementTarget, 0, 0);
            }

            int selfElevation = _topology.ResolveElevation(selfPosition);
            int targetElevation = _topology.ResolveElevation(semanticTargetPosition);

            if (_activeLink != null && !GodotObject.IsInstanceValid(_activeLink))
            {
                ClearActiveLink();
            }

            // Target đổi sang tầng khác giữa chừng: hủy route cũ và tính lại ngay.
            if (_activeLink != null && targetElevation != _activeToElevation)
            {
                ClearActiveLink();
            }

            if (_activeLink != null)
            {
                Vector2 entry = _activeLink.GetEntryPoint(_activeFromElevation);
                Vector2 exit = _activeLink.GetExitPoint(_activeFromElevation);
                float entryRadius = _activeLink.GetEntryRadius(_activeFromElevation);
                float exitRadius = _activeLink.GetExitRadius(_activeFromElevation);

                if (!_traversing && selfPosition.DistanceTo(entry) <= entryRadius)
                {
                    _traversing = true;
                    LogTransition("ENTER", _activeLink, _activeFromElevation, _activeToElevation);
                }

                if (_traversing)
                {
                    bool reachedExit = selfPosition.DistanceTo(exit) <= exitRadius;
                    bool enteredDestinationRegion = selfElevation == _activeToElevation
                        && selfPosition.DistanceTo(entry) > entryRadius;
                    if (reachedExit || enteredDestinationRegion)
                    {
                        LogTransition("EXIT", _activeLink, _activeFromElevation, _activeToElevation);
                        ClearActiveLink();
                        _lastSummary = $"topology={selfElevation}->{targetElevation} phase=direct";
                        return Direct(finalMovementTarget, selfElevation, targetElevation);
                    }

                    _lastSummary = $"topology={_activeFromElevation}->{_activeToElevation} link={_activeLink.LinkId} phase=traverse";
                    return new RouteResult(
                        exit,
                        true,
                        true,
                        selfElevation,
                        targetElevation,
                        _activeLink.LinkId,
                        "traverse");
                }

                _lastSummary = $"topology={_activeFromElevation}->{_activeToElevation} link={_activeLink.LinkId} phase=approach";
                return new RouteResult(
                    entry,
                    true,
                    true,
                    selfElevation,
                    targetElevation,
                    _activeLink.LinkId,
                    "approach");
            }

            if (selfElevation == targetElevation)
            {
                // Spacing có thể tạo anchor lệch qua mép cliff dù target thật vẫn cùng tầng.
                // Không cho anchor chiến thuật kéo actor xuống tầng khác một cách "xuyên mép".
                int anchorElevation = _topology.ResolveElevation(finalMovementTarget);
                if (anchorElevation != targetElevation)
                {
                    _lastSummary = $"topology={selfElevation} phase=direct anchor=semantic-clamp";
                    return Direct(semanticTargetPosition, selfElevation, targetElevation);
                }

                _lastSummary = $"topology={selfElevation} phase=direct";
                return Direct(finalMovementTarget, selfElevation, targetElevation);
            }

            if (_topology.TryFindBestLink(
                selfElevation,
                targetElevation,
                selfPosition,
                semanticTargetPosition,
                out StairLink2D link,
                out Vector2 bestEntry,
                out Vector2 bestExit))
            {
                _activeLink = link;
                _activeFromElevation = selfElevation;
                _activeToElevation = targetElevation;
                _traversing = selfPosition.DistanceTo(bestEntry) <= link.GetEntryRadius(selfElevation);
                LogTransition(_traversing ? "ENTER" : "ROUTE", link, selfElevation, targetElevation);

                Vector2 waypoint = _traversing ? bestExit : bestEntry;
                string phase = _traversing ? "traverse" : "approach";
                _lastSummary = $"topology={selfElevation}->{targetElevation} link={link.LinkId} phase={phase}";
                return new RouteResult(
                    waypoint,
                    true,
                    true,
                    selfElevation,
                    targetElevation,
                    link.LinkId,
                    phase);
            }

            // Khác tầng nhưng không có connector: fail-closed tránh AI cứ đâm cliff vô hạn.
            _lastSummary = $"topology={selfElevation}->{targetElevation} route=MISSING failClosed={_failClosed}";
            if (_debugLogging)
            {
                GD.PushWarning($"[CombatElevationRouter] actor={_self.CombatantId} không có StairLink nối elevation {selfElevation}->{targetElevation}.");
            }

            return new RouteResult(
                _failClosed ? selfPosition : finalMovementTarget,
                true,
                !_failClosed,
                selfElevation,
                targetElevation,
                string.Empty,
                "missing");
        }

        public void Reset()
        {
            ClearActiveLink();
            _topology = null;
            _lastSummary = "topology=reset";
        }

        public string ToCompactString() => _lastSummary;

        private RouteResult Direct(Vector2 target, int selfElevation, int targetElevation)
        {
            return new RouteResult(
                target,
                false,
                true,
                selfElevation,
                targetElevation,
                string.Empty,
                "direct");
        }

        private void ResolveTopologyIfNeeded()
        {
            if (_topology != null && GodotObject.IsInstanceValid(_topology))
            {
                return;
            }

            _topology = WorldNavigationTopology2D.FindFor(_self);
        }

        private void ClearActiveLink()
        {
            _activeLink = null;
            _activeFromElevation = 0;
            _activeToElevation = 0;
            _traversing = false;
        }

        private void LogTransition(string action, StairLink2D link, int from, int to)
        {
            if (!_debugLogging || link == null)
            {
                return;
            }

            GD.Print($"[CombatElevationRouter] actor={_self.CombatantId} {action} link={link.LinkId} elevation={from}->{to}");
        }
    }
}
