using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.World.Navigation;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Physics-backed planner for maps that do not provide a NavigationRegion2D.
    /// It reuses the map's shared grid first, then builds a local fallback when needed.
    /// </summary>
    internal sealed class CombatGridPathPlanner
    {
        private const float CellSize = 20f;
        private const float WaypointReachDistance = 15f;
        private const float TargetRefreshDistance = 30f;
        private const float RepathIntervalSeconds = 0.32f;
        private const float DirectCheckIntervalSeconds = 0.16f;
        private const int NearestOpenSearchRadius = 5;

        private static readonly float[] SearchMargins = { 160f, 320f, 640f };

        private readonly CombatCharacter _self;
        private readonly uint _obstacleMask;
        private readonly CombatMovementMetrics _metrics;
        private readonly CircleShape2D _clearanceShape;
        private readonly PhysicsShapeQueryParameters2D _shapeQuery;
        private readonly Dictionary<Vector2I, bool> _solidCache = new();
        private readonly List<Vector2> _waypoints = new();

        private WorldNavigationGrid2D _sharedGrid;
        private int _waypointIndex;
        private Vector2 _committedTarget;
        private bool _hasCommittedTarget;
        private float _nextRepathTime;
        private float _nextDirectCheckTime;
        private string _activePathSource = "none";
        private string _status = "grid=idle";

        public CombatGridPathPlanner(
            CombatCharacter self,
            uint obstacleMask,
            float bodyProbeRadius,
            CombatMovementMetrics metrics)
        {
            _self = self;
            _obstacleMask = obstacleMask;
            _metrics = metrics;

            float worldScale = self != null
                ? Mathf.Max(Mathf.Abs(self.GlobalScale.X), Mathf.Abs(self.GlobalScale.Y))
                : 1f;
            float clearanceRadius = Mathf.Clamp(bodyProbeRadius * Mathf.Max(1f, worldScale), 6f, 16f);
            _clearanceShape = new CircleShape2D { Radius = clearanceRadius };
            _shapeQuery = new PhysicsShapeQueryParameters2D
            {
                Shape = _clearanceShape,
                CollisionMask = _obstacleMask,
                CollideWithAreas = false,
                CollideWithBodies = true,
                Margin = 0.5f
            };
        }

        public bool TryResolveDirection(
            Vector2 selfPosition,
            Vector2 targetPosition,
            out Vector2 direction,
            bool forceRepath = false)
        {
            direction = Vector2.Zero;
            if (!IsReady() || selfPosition.DistanceSquaredTo(targetPosition) <= 4f)
            {
                ClearPath("grid=idle");
                return false;
            }

            AdvanceWaypoints(selfPosition);
            float now = NowSeconds();
            bool hasPath = HasActivePath;
            bool targetChanged = !_hasCommittedTarget
                || _committedTarget.DistanceSquaredTo(targetPosition)
                    >= TargetRefreshDistance * TargetRefreshDistance;

            if (hasPath && now >= _nextDirectCheckTime)
            {
                _nextDirectCheckTime = now + DirectCheckIntervalSeconds;
                if (!IsCorridorBlocked(selfPosition, targetPosition))
                {
                    ClearPath("grid=direct");
                    return false;
                }
            }

            bool needsPath = forceRepath || !hasPath || targetChanged;
            if (needsPath && (forceRepath || now >= _nextRepathTime))
            {
                _nextRepathTime = now + RepathIntervalSeconds;
                _nextDirectCheckTime = now + DirectCheckIntervalSeconds;

                if (!IsCorridorBlocked(selfPosition, targetPosition))
                {
                    ClearPath("grid=direct");
                    return false;
                }

                _metrics?.RecordGridPathRequest();
                if (TryBuildPath(selfPosition, targetPosition))
                {
                    _committedTarget = targetPosition;
                    _hasCommittedTarget = true;
                    _metrics?.RecordGridPathSuccess();
                }
                else
                {
                    _metrics?.RecordGridPathFailure();
                    ClearPath("grid=failed");
                    return false;
                }
            }

            AdvanceWaypoints(selfPosition);
            if (!HasActivePath)
            {
                return false;
            }

            Vector2 toWaypoint = _waypoints[_waypointIndex] - selfPosition;
            if (toWaypoint.LengthSquared() <= 0.001f)
            {
                return false;
            }

            direction = toWaypoint.Normalized();
            _metrics?.RecordGridPathDirectionUsed();
            _status = $"grid=active source={_activePathSource} "
                + $"waypoint={_waypointIndex + 1}/{_waypoints.Count} cache={_solidCache.Count}";
            return true;
        }

        public void Reset()
        {
            _solidCache.Clear();
            _nextRepathTime = 0f;
            _nextDirectCheckTime = 0f;
            ClearPath("grid=reset");
        }

        public string ToCompactString()
        {
            ResolveSharedGrid();
            string sharedStatus = _sharedGrid?.ToCompactString() ?? "world_grid=missing";
            return _status + " " + sharedStatus;
        }

        public void Dispose()
        {
            ClearPath("grid=disposed");
            _shapeQuery.Dispose();
            _clearanceShape.Dispose();
        }

        private bool TryBuildPath(Vector2 startPosition, Vector2 targetPosition)
        {
            if (TryBuildSharedPath(startPosition, targetPosition))
            {
                return true;
            }

            Vector2I originalStart = WorldToCell(startPosition);
            Vector2I originalTarget = WorldToCell(targetPosition);

            for (int attempt = 0; attempt < SearchMargins.Length; attempt++)
            {
                Rect2I region = BuildSearchRegion(originalStart, originalTarget, SearchMargins[attempt]);
                if (region.Size.X <= 1 || region.Size.Y <= 1)
                {
                    continue;
                }

                using var grid = new AStarGrid2D
                {
                    Region = region,
                    CellSize = new Vector2(CellSize, CellSize),
                    Offset = Vector2.One * (CellSize * 0.5f),
                    DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles,
                    DefaultComputeHeuristic = AStarGrid2D.Heuristic.Octile,
                    DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile,
                    JumpingEnabled = false
                };
                grid.Update();
                PopulateSolids(grid, region);

                Vector2I startCell = ClampCellToRegion(originalStart, region);
                // The actor can be touching a wall while requesting recovery. Keeping its
                // current cell open gives A* a legal first step out of contact.
                grid.SetPointSolid(startCell, false);

                Vector2I targetCell = ClampCellToRegion(originalTarget, region);
                if (grid.IsPointSolid(targetCell)
                    && !TryFindNearestOpen(grid, targetCell, region, out targetCell))
                {
                    continue;
                }

                Vector2[] rawPath = grid.GetPointPath(startCell, targetCell, false);
                if (rawPath.Length < 2)
                {
                    continue;
                }

                BuildSimplifiedWaypoints(startPosition, targetPosition, rawPath, 1);
                if (_waypoints.Count <= 0)
                {
                    continue;
                }

                _waypointIndex = 0;
                _activePathSource = "local";
                _status = $"grid=ready points={_waypoints.Count} margin={SearchMargins[attempt]:0}";
                return true;
            }

            return false;
        }

        private bool TryBuildSharedPath(Vector2 startPosition, Vector2 targetPosition)
        {
            ResolveSharedGrid();

            if (_sharedGrid == null)
            {
                return false;
            }

            if (!_sharedGrid.IsGridReady)
            {
                _status = $"grid=shared_building progress={_sharedGrid.BuildProgress:0%}";
                return false;
            }

            if (!_sharedGrid.TryFindPath(
                startPosition,
                targetPosition,
                out Vector2[] sharedPath,
                out float pathDistance))
            {
                _status = "grid=shared_unreachable";
                return false;
            }

            BuildSimplifiedWaypoints(startPosition, targetPosition, sharedPath, 0);
            if (_waypoints.Count <= 0)
            {
                return false;
            }

            _waypointIndex = 0;
            _activePathSource = "shared";
            _status = $"grid=shared points={_waypoints.Count} distance={pathDistance:0}";
            return true;
        }

        private void ResolveSharedGrid()
        {
            if (_sharedGrid == null
                || !GodotObject.IsInstanceValid(_sharedGrid)
                || !_sharedGrid.IsInsideTree())
            {
                _sharedGrid = WorldNavigationGrid2D.FindFor(_self);
            }
        }

        private Rect2I BuildSearchRegion(Vector2I start, Vector2I target, float marginPixels)
        {
            int marginCells = Mathf.CeilToInt(marginPixels / CellSize);
            int minX = Mathf.Min(start.X, target.X) - marginCells;
            int minY = Mathf.Min(start.Y, target.Y) - marginCells;
            int maxX = Mathf.Max(start.X, target.X) + marginCells;
            int maxY = Mathf.Max(start.Y, target.Y) + marginCells;

            if (_self.TryGetLevelBounds(out Rect2 bounds))
            {
                float halfCell = CellSize * 0.5f;
                int boundsMinX = Mathf.CeilToInt((bounds.Position.X - halfCell) / CellSize);
                int boundsMinY = Mathf.CeilToInt((bounds.Position.Y - halfCell) / CellSize);
                int boundsMaxX = Mathf.FloorToInt((bounds.End.X - halfCell) / CellSize);
                int boundsMaxY = Mathf.FloorToInt((bounds.End.Y - halfCell) / CellSize);
                minX = Mathf.Max(minX, boundsMinX);
                minY = Mathf.Max(minY, boundsMinY);
                maxX = Mathf.Min(maxX, boundsMaxX);
                maxY = Mathf.Min(maxY, boundsMaxY);
            }

            return new Rect2I(
                minX,
                minY,
                Mathf.Max(0, maxX - minX + 1),
                Mathf.Max(0, maxY - minY + 1));
        }

        private void PopulateSolids(AStarGrid2D grid, Rect2I region)
        {
            Vector2I end = region.End;
            for (int y = region.Position.Y; y < end.Y; y++)
            {
                for (int x = region.Position.X; x < end.X; x++)
                {
                    Vector2I cell = new(x, y);
                    if (!_solidCache.TryGetValue(cell, out bool solid))
                    {
                        solid = IsPositionBlocked(CellToWorld(cell));
                        _solidCache[cell] = solid;
                        _metrics?.RecordGridCellSample();
                    }

                    if (solid)
                    {
                        grid.SetPointSolid(cell, true);
                    }
                }
            }
        }

        private void BuildSimplifiedWaypoints(
            Vector2 startPosition,
            Vector2 targetPosition,
            Vector2[] rawPath,
            int firstCandidateIndex)
        {
            _waypoints.Clear();
            var candidates = new List<Vector2>(rawPath.Length + 1);
            for (int i = Mathf.Clamp(firstCandidateIndex, 0, rawPath.Length); i < rawPath.Length; i++)
            {
                candidates.Add(rawPath[i]);
            }

            if (candidates.Count > 0
                && candidates[candidates.Count - 1].DistanceSquaredTo(targetPosition) > 0.01f
                && !IsCorridorBlocked(candidates[candidates.Count - 1], targetPosition))
            {
                candidates.Add(targetPosition);
            }

            Vector2 anchor = startPosition;
            int cursor = 0;
            while (cursor < candidates.Count)
            {
                int farthestVisible = cursor;
                for (int i = candidates.Count - 1; i > cursor; i--)
                {
                    if (!IsCorridorBlocked(anchor, candidates[i]))
                    {
                        farthestVisible = i;
                        break;
                    }
                }

                Vector2 waypoint = candidates[farthestVisible];
                _waypoints.Add(waypoint);
                anchor = waypoint;
                cursor = farthestVisible + 1;
            }
        }

        private void AdvanceWaypoints(Vector2 selfPosition)
        {
            float reachSq = WaypointReachDistance * WaypointReachDistance;
            while (HasActivePath
                && selfPosition.DistanceSquaredTo(_waypoints[_waypointIndex]) <= reachSq)
            {
                _waypointIndex++;
            }
        }

        private bool TryFindNearestOpen(
            AStarGrid2D grid,
            Vector2I origin,
            Rect2I region,
            out Vector2I openCell)
        {
            for (int radius = 1; radius <= NearestOpenSearchRadius; radius++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                        {
                            continue;
                        }

                        Vector2I candidate = origin + new Vector2I(x, y);
                        if (region.HasPoint(candidate) && !grid.IsPointSolid(candidate))
                        {
                            openCell = candidate;
                            return true;
                        }
                    }
                }
            }

            openCell = origin;
            return false;
        }

        private bool IsPositionBlocked(Vector2 worldPosition)
        {
            _shapeQuery.Transform = new Transform2D(0f, worldPosition);
            return _self.GetWorld2D().DirectSpaceState.IntersectShape(_shapeQuery, 1).Count > 0;
        }

        private bool IsCorridorBlocked(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            if (delta.LengthSquared() <= 4f)
            {
                return false;
            }

            Vector2 perpendicular = new(-delta.Y, delta.X);
            perpendicular = perpendicular.Normalized() * _clearanceShape.Radius;
            return IsRayBlocked(from, to)
                || IsRayBlocked(from + perpendicular, to + perpendicular)
                || IsRayBlocked(from - perpendicular, to - perpendicular);
        }

        private bool IsRayBlocked(Vector2 from, Vector2 to)
        {
            PhysicsRayQueryParameters2D query = PhysicsRayQueryParameters2D.Create(
                from,
                to,
                _obstacleMask);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            return _self.GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0;
        }

        private static Vector2I WorldToCell(Vector2 point)
        {
            return new Vector2I(
                Mathf.FloorToInt(point.X / CellSize),
                Mathf.FloorToInt(point.Y / CellSize));
        }

        private static Vector2 CellToWorld(Vector2I cell)
        {
            return new Vector2(
                (cell.X + 0.5f) * CellSize,
                (cell.Y + 0.5f) * CellSize);
        }

        private static Vector2I ClampCellToRegion(Vector2I cell, Rect2I region)
        {
            return new Vector2I(
                Mathf.Clamp(cell.X, region.Position.X, region.End.X - 1),
                Mathf.Clamp(cell.Y, region.Position.Y, region.End.Y - 1));
        }

        private bool IsReady()
        {
            return _self != null
                && GodotObject.IsInstanceValid(_self)
                && _self.IsInsideTree()
                && _self.GetWorld2D()?.DirectSpaceState != null;
        }

        private bool HasActivePath => _waypointIndex < _waypoints.Count;

        private void ClearPath(string status)
        {
            _waypoints.Clear();
            _waypointIndex = 0;
            _hasCommittedTarget = false;
            _activePathSource = "none";
            _status = status;
        }

        private static float NowSeconds()
        {
            return Time.GetTicksMsec() / 1000f;
        }
    }
}
