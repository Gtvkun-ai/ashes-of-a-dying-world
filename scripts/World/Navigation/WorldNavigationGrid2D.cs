using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.World.Maps;

namespace AshesofaDyingWorld.World.Navigation
{
    /// <summary>
    /// Shared walkability grid for a 2D world. It can load editor-baked data
    /// immediately or fall back to an incremental physics scan at runtime.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class WorldNavigationGrid2D : Node2D
    {
        public const string GroupName = "WorldNavigationGrid2D";

        private const byte NoManualOverride = 0;
        private const byte ForceWalkable = 1;
        private const byte ForceBlocked = 2;
        private const int EditorPaintWalkable = 1;
        private const int EditorPaintBlocked = 2;
        private const int EditorPaintErase = 3;

        private static readonly Color WalkablePreviewColor = new(0.12f, 0.78f, 0.42f, 0.14f);
        private static readonly Color BlockedPreviewColor = new(0.94f, 0.20f, 0.23f, 0.28f);
        private static readonly Color ForcedWalkablePreviewColor = new(0.05f, 0.78f, 0.96f, 0.38f);
        private static readonly Color ForcedBlockedPreviewColor = new(1.0f, 0.55f, 0.05f, 0.46f);
        private static readonly Color GridLinePreviewColor = new(0.88f, 0.94f, 1.0f, 0.16f);
        private static readonly Color BoundsPreviewColor = new(0.95f, 0.98f, 1.0f, 0.88f);

        private sealed class BakedGridSnapshot
        {
            public int SchemaVersion;
            public Rect2 WorldBounds;
            public Rect2I GridRegion;
            public float CellSize;
            public float AgentRadius;
            public uint CollisionMask;
            public byte[] BlockedCells = Array.Empty<byte>();
            public byte[] ManualOverrides = Array.Empty<byte>();

            public int CellCount => GridRegion.Size.X * GridRegion.Size.Y;
            public bool HasValidCellData => SchemaVersion == WorldNavigationGridData.CurrentSchemaVersion
                && GridRegion.Size.X > 0
                && GridRegion.Size.Y > 0
                && BlockedCells != null
                && BlockedCells.Length == CellCount;
        }

        private Resource _bakedData;

        [ExportGroup("Runtime")]
        [Export] public bool Enabled { get; set; } = true;
        [Export] public bool BuildAutomatically { get; set; } = true;
        [Export] public bool PreferBakedData { get; set; } = true;

        [ExportGroup("Grid")]
        [Export(PropertyHint.Range, "8,64,1")] public float GridCellSize { get; set; } = 20f;
        [Export(PropertyHint.Range, "2,32,1")] public float AgentRadius { get; set; } = 14f;
        [Export(PropertyHint.Layers2DPhysics)] public uint CollisionMask { get; set; } = 8;
        [Export(PropertyHint.Range, "1,24,1")] public int NearestOpenSearchRadius { get; set; } = 10;

        [ExportGroup("Runtime Fallback Build")]
        [Export(PropertyHint.Range, "64,4096,64")] public int CellsPerPhysicsFrame { get; set; } = 256;
        [Export(PropertyHint.Range, "0,8,1")] public int StartupPhysicsFrames { get; set; } = 2;

        [ExportGroup("Bounds")]
        [Export] public bool UseManualBounds { get; set; } = false;
        [Export] public Vector2 ManualBoundsPosition { get; set; } = Vector2.Zero;
        [Export] public Vector2 ManualBoundsSize { get; set; } = Vector2.Zero;

        [ExportGroup("Baked Data")]
        [Export]
        public Resource BakedData
        {
            get => _bakedData;
            set
            {
                if (_bakedData == value)
                {
                    return;
                }

                _bakedData = value;
                if (Engine.IsEditorHint() && IsInsideTree())
                {
                    ReloadEditorPreview();
                }
            }
        }

        [ExportGroup("Diagnostics")]
        [Export] public bool DebugLogging { get; set; } = false;

        [Signal]
        public delegate void EditorDataChangedEventHandler();

        private AStarGrid2D _grid;
        private CircleShape2D _clearanceShape;
        private PhysicsShapeQueryParameters2D _shapeQuery;
        private Rect2 _worldBounds;
        private Rect2I _gridRegion;
        private byte[] _baseBlockedCells = Array.Empty<byte>();
        private byte[] _manualOverrides = Array.Empty<byte>();
        private byte[] _workingBlockedCells;
        private int _buildCursor;
        private int _totalCells;
        private int _blockedCells;
        private int _startupFramesRemaining;
        private bool _buildStarted;
        private bool _writeBakedData;
        private bool _editorBakeRunning;
        private bool _loadedFromBake;
        private bool _editorPreviewVisible;
        private bool _editorBrushVisible;
        private Vector2I _editorBrushCell;
        private int _editorBrushRadius = 1;
        private int _editorBrushMode;
        private string _status = "world_grid=idle";

        public bool IsGridReady { get; private set; }
        public int GridCellCount => _totalCells;
        public int BlockedCellCount => _blockedCells;
        public Rect2 WorldBounds => _worldBounds;
        public float BuildProgress => _totalCells <= 0
            ? 0f
            : Mathf.Clamp(_buildCursor / (float)_totalCells, 0f, 1f);

        public override void _Ready()
        {
            if (Engine.IsEditorHint())
            {
                SetPhysicsProcess(false);
                ReloadEditorPreview();
                return;
            }

            AddToGroup(GroupName);
            SetPhysicsProcess(false);
            if (!Enabled || !BuildAutomatically)
            {
                _status = Enabled ? "world_grid=idle" : "world_grid=disabled";
                return;
            }

            if (PreferBakedData && TryLoadBakedGrid(createPhysicsQuery: true))
            {
                return;
            }

            QueuePhysicsBuild(writeBakedData: false);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (IsGridReady
                || (Engine.IsEditorHint() && !_editorBakeRunning)
                || (!Engine.IsEditorHint() && !Enabled))
            {
                SetPhysicsProcess(false);
                return;
            }

            if (_startupFramesRemaining > 0)
            {
                _startupFramesRemaining--;
                return;
            }

            if (!_buildStarted && !BeginPhysicsBuild())
            {
                FinishFailedBuild("world_grid=bounds_missing", "Could not resolve world bounds or physics space.");
                return;
            }

            BuildNextCells();
        }

        public void DrawEditorOverlay(Control viewportControl, Transform2D worldToViewport)
        {
            if (!Engine.IsEditorHint()
                || !_editorPreviewVisible
                || viewportControl == null
                || !IsGridReady
                || _totalCells <= 0
                || _baseBlockedCells.Length != _totalCells)
            {
                return;
            }

            DrawPreviewCells(viewportControl, worldToViewport);
            DrawPreviewGridLines(viewportControl, worldToViewport);
            viewportControl.DrawRect(
                TransformRect(_worldBounds, worldToViewport),
                BoundsPreviewColor,
                false,
                2f,
                true);
            DrawBrushPreview(viewportControl, worldToViewport);
        }

        public override void _ExitTree()
        {
            if (IsInGroup(GroupName))
            {
                RemoveFromGroup(GroupName);
            }

            DisposeBuildResources();
        }

        public void RequestRebuild()
        {
            if (Engine.IsEditorHint())
            {
                RequestEditorBake();
                return;
            }

            QueuePhysicsBuild(writeBakedData: false);
        }

        public bool RequestEditorBake()
        {
            if (!Engine.IsEditorHint() || !IsInsideTree() || _editorBakeRunning)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(ResolveBakedDataPath()))
            {
                GD.PushWarning("[WorldNavigationGrid] Save the scene before baking navigation data.");
                return false;
            }

            QueuePhysicsBuild(writeBakedData: true);
            return true;
        }

        public bool IsEditorBakeRunning()
        {
            return Engine.IsEditorHint() && _editorBakeRunning;
        }

        public bool HasBakedGridData()
        {
            return IsGridReady
                && _loadedFromBake
                && _baseBlockedCells.Length == _totalCells
                && _manualOverrides.Length == _totalCells;
        }

        public string GetEditorStatusText()
        {
            if (_editorBakeRunning)
            {
                return _buildStarted
                    ? $"Baking {BuildProgress:0%}"
                    : "Bake queued";
            }

            if (!IsGridReady)
            {
                return _bakedData == null ? "No baked data" : "Baked data is stale";
            }

            if (_status == "world_grid=bake_save_failed")
            {
                return "Bake ready | save failed";
            }

            int edits = CountManualOverrides();
            return $"{_totalCells:N0} cells | {_blockedCells:N0} blocked | {edits:N0} edits";
        }

        public void SetEditorPreviewVisible(bool visible)
        {
            if (!Engine.IsEditorHint())
            {
                return;
            }

            _editorPreviewVisible = visible;
            if (!visible)
            {
                _editorBrushVisible = false;
            }

            QueueRedraw();
        }

        public void SetEditorBrushPreview(Vector2 worldPosition, int brushRadius, int paintMode)
        {
            if (!Engine.IsEditorHint()
                || !_editorPreviewVisible
                || paintMode == 0
                || !IsGridReady)
            {
                ClearEditorBrushPreview();
                return;
            }

            Vector2I cell = WorldToCell(worldPosition);
            bool visible = _gridRegion.HasPoint(cell);
            int radius = Mathf.Clamp(brushRadius, 1, 8);
            if (_editorBrushVisible == visible
                && _editorBrushCell == cell
                && _editorBrushRadius == radius
                && _editorBrushMode == paintMode)
            {
                return;
            }

            _editorBrushVisible = visible;
            _editorBrushCell = cell;
            _editorBrushRadius = radius;
            _editorBrushMode = paintMode;
            QueueRedraw();
        }

        public void ClearEditorBrushPreview()
        {
            if (!_editorBrushVisible)
            {
                return;
            }

            _editorBrushVisible = false;
            QueueRedraw();
        }

        public bool PaintEditorCells(Vector2 worldPosition, int paintMode, int brushRadius)
        {
            if (!Engine.IsEditorHint()
                || !HasBakedGridData()
                || paintMode < EditorPaintWalkable
                || paintMode > EditorPaintErase)
            {
                return false;
            }

            Vector2I center = WorldToCell(worldPosition);
            if (!_gridRegion.HasPoint(center))
            {
                return false;
            }

            byte nextOverride = paintMode switch
            {
                EditorPaintWalkable => ForceWalkable,
                EditorPaintBlocked => ForceBlocked,
                _ => NoManualOverride
            };
            int radius = Mathf.Clamp(brushRadius, 1, 8);
            int reach = radius - 1;
            float radiusSquared = Mathf.Max(0.25f, (radius - 0.35f) * (radius - 0.35f));
            bool changed = false;
            for (int y = -reach; y <= reach; y++)
            {
                for (int x = -reach; x <= reach; x++)
                {
                    if (x * x + y * y > radiusSquared)
                    {
                        continue;
                    }

                    Vector2I cell = center + new Vector2I(x, y);
                    if (!_gridRegion.HasPoint(cell))
                    {
                        continue;
                    }

                    int index = CellToIndex(cell);
                    if (_manualOverrides[index] == nextOverride)
                    {
                        continue;
                    }

                    bool wasBlocked = ResolveBlocked(index);
                    _manualOverrides[index] = nextOverride;
                    bool isBlocked = ResolveBlocked(index);
                    if (wasBlocked != isBlocked)
                    {
                        _grid.SetPointSolid(cell, isBlocked);
                        _blockedCells += isBlocked ? 1 : -1;
                    }

                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

            PersistManualOverrides();
            SetEditorBrushPreview(worldPosition, radius, paintMode);
            NotifyEditorDataChanged();
            return true;
        }

        public byte[] GetManualOverrideSnapshot()
        {
            return (byte[])_manualOverrides.Clone();
        }

        public void ApplyManualOverrideSnapshot(byte[] snapshot)
        {
            if (!Engine.IsEditorHint() || !HasBakedGridData())
            {
                return;
            }

            byte[] normalized = NormalizeManualOverrides(snapshot, _totalCells);
            if (ArraysEqual(_manualOverrides, normalized))
            {
                return;
            }

            _manualOverrides = normalized;
            ReapplyAllSolidStates();
            PersistManualOverrides();
            SaveEditorData();
            NotifyEditorDataChanged();
        }

        public bool ClearManualOverrides()
        {
            if (!Engine.IsEditorHint() || !HasBakedGridData() || CountManualOverrides() <= 0)
            {
                return false;
            }

            _manualOverrides = new byte[_totalCells];
            ReapplyAllSolidStates();
            PersistManualOverrides();
            NotifyEditorDataChanged();
            return true;
        }

        public bool SaveEditorData()
        {
            if (!Engine.IsEditorHint() || _bakedData == null)
            {
                return false;
            }

            WorldNavigationGridData writableData = EnsureWritableBakedData();
            writableData.ManualOverrides = CountManualOverrides() > 0
                ? _manualOverrides
                : Array.Empty<byte>();
            writableData.EmitChanged();
            string savePath = ResolveBakedDataPath();
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return false;
            }

            Error saveError = SaveBakedResource(savePath);
            if (saveError == Error.Ok)
            {
                return true;
            }

            GD.PushError($"[WorldNavigationGrid] Could not save editor data to {savePath}: {saveError}");
            return false;
        }

        public bool TryFindPath(
            Vector2 startPosition,
            Vector2 targetPosition,
            out Vector2[] path,
            out float pathDistance)
        {
            path = Array.Empty<Vector2>();
            pathDistance = float.PositiveInfinity;
            if (!IsGridReady || _grid == null)
            {
                return false;
            }

            Vector2I requestedTargetCell = WorldToCell(targetPosition);
            Vector2I startCell = ClampCellToRegion(WorldToCell(startPosition));
            Vector2I targetCell = ClampCellToRegion(requestedTargetCell);
            if (_grid.IsPointSolid(startCell)
                && !TryFindNearestOpen(startCell, startPosition, out startCell))
            {
                return false;
            }

            if (_grid.IsPointSolid(targetCell)
                && !TryFindNearestOpen(targetCell, targetPosition, out targetCell))
            {
                return false;
            }

            Vector2[] rawPath = _grid.GetPointPath(startCell, targetCell, false);
            if (rawPath.Length <= 0)
            {
                return false;
            }

            var points = new List<Vector2>(rawPath.Length + 1);
            for (int i = 0; i < rawPath.Length; i++)
            {
                if (points.Count <= 0
                    || points[points.Count - 1].DistanceSquaredTo(rawPath[i]) > 0.01f)
                {
                    points.Add(rawPath[i]);
                }
            }

            Vector2 resolvedTarget = CellToWorld(targetCell);
            if (targetCell == requestedTargetCell
                && !IsPositionBlocked(targetPosition)
                && !IsCorridorBlocked(points[points.Count - 1], targetPosition))
            {
                resolvedTarget = targetPosition;
            }

            if (points[points.Count - 1].DistanceSquaredTo(resolvedTarget) > 0.01f)
            {
                points.Add(resolvedTarget);
            }

            pathDistance = MeasurePath(startPosition, points);
            path = points.ToArray();
            return true;
        }

        public bool TryEstimateTravelTime(
            Vector2 startPosition,
            Vector2 targetPosition,
            float movementSpeed,
            out float travelSeconds,
            out float pathDistance)
        {
            travelSeconds = float.PositiveInfinity;
            if (movementSpeed <= 0.001f
                || !TryFindPath(startPosition, targetPosition, out _, out pathDistance))
            {
                pathDistance = float.PositiveInfinity;
                return false;
            }

            travelSeconds = pathDistance / movementSpeed;
            return true;
        }

        public string ToCompactString()
        {
            return IsGridReady
                ? $"world_grid=ready source={(_loadedFromBake ? "baked" : "physics")} "
                    + $"cells={_totalCells} blocked={_blockedCells}"
                : $"{_status} progress={BuildProgress:0%}";
        }

        public static WorldNavigationGrid2D FindFor(Node context)
        {
            if (context?.GetTree() == null)
            {
                return null;
            }

            if (context is not CanvasItem contextCanvas)
            {
                return null;
            }

            World2D contextWorld = contextCanvas.GetWorld2D();
            GameLevel contextLevel = FindOwningLevel(context);
            WorldNavigationGrid2D worldFallback = null;
            foreach (Node node in context.GetTree().GetNodesInGroup(GroupName))
            {
                if (node is WorldNavigationGrid2D grid
                    && GodotObject.IsInstanceValid(grid)
                    && grid.Enabled
                    && grid.IsInsideTree()
                    && grid.GetWorld2D() == contextWorld)
                {
                    if (contextLevel != null && FindOwningLevel(grid) == contextLevel)
                    {
                        return grid;
                    }

                    worldFallback ??= grid;
                }
            }

            return contextLevel == null ? worldFallback : null;
        }

        private void ReloadEditorPreview()
        {
            if (!Engine.IsEditorHint())
            {
                return;
            }

            ResetGridState();
            if (!TryLoadBakedGrid(createPhysicsQuery: false))
            {
                _status = _bakedData == null ? "world_grid=no_bake" : "world_grid=bake_stale";
            }

            QueueRedraw();
        }

        private void QueuePhysicsBuild(bool writeBakedData)
        {
            ResetGridState();
            _writeBakedData = writeBakedData;
            _editorBakeRunning = Engine.IsEditorHint() && writeBakedData;
            _startupFramesRemaining = _editorBakeRunning
                ? 0
                : Mathf.Max(0, StartupPhysicsFrames);
            _status = _editorBakeRunning ? "world_grid=bake_queued" : "world_grid=rebuild_queued";
            if (IsInsideTree())
            {
                SetPhysicsProcess(_editorBakeRunning || Enabled);
            }

            QueueRedraw();
        }

        private bool BeginPhysicsBuild()
        {
            if (!TryResolveGridLayout(out Rect2 bounds, out Rect2I region, out float cellSize)
                || GetWorld2D()?.DirectSpaceState == null)
            {
                return false;
            }

            InitializeGrid(bounds, region, cellSize, createPhysicsQuery: true);
            _manualOverrides = TryReadBakedData(out BakedGridSnapshot existingData)
                && CanReuseManualOverrides(existingData, region, cellSize)
                ? NormalizeManualOverrides(existingData.ManualOverrides, _totalCells)
                : new byte[_totalCells];
            _workingBlockedCells = _writeBakedData ? new byte[_totalCells] : null;
            _baseBlockedCells = new byte[_totalCells];
            _buildCursor = 0;
            _blockedCells = 0;
            _buildStarted = true;
            _status = _editorBakeRunning ? "world_grid=baking" : "world_grid=building";
            return true;
        }

        private void BuildNextCells()
        {
            int budget = _editorBakeRunning
                ? Mathf.Max(1024, CellsPerPhysicsFrame)
                : Mathf.Max(1, CellsPerPhysicsFrame);
            int end = Mathf.Min(_totalCells, _buildCursor + budget);
            while (_buildCursor < end)
            {
                Vector2I cell = IndexToCell(_buildCursor);
                Vector2 worldPosition = CellToWorld(cell);
                bool blockedByCollision = !IsInsideUsableBounds(worldPosition)
                    || IsPositionBlocked(worldPosition);
                _baseBlockedCells[_buildCursor] = blockedByCollision ? (byte)1 : (byte)0;
                if (_workingBlockedCells != null)
                {
                    _workingBlockedCells[_buildCursor] = _baseBlockedCells[_buildCursor];
                }

                bool blocked = ResolveBlocked(_buildCursor);
                if (blocked)
                {
                    _grid.SetPointSolid(cell, true);
                    _blockedCells++;
                }

                _buildCursor++;
            }

            if (_buildCursor < _totalCells)
            {
                return;
            }

            IsGridReady = true;
            _loadedFromBake = _writeBakedData;
            _status = "world_grid=ready";
            SetPhysicsProcess(false);
            if (_writeBakedData)
            {
                CommitBakedData();
            }

            _editorBakeRunning = false;
            _writeBakedData = false;
            if (Engine.IsEditorHint())
            {
                DisposePhysicsQueryResources();
                NotifyEditorDataChanged();
            }
            else if (DebugLogging)
            {
                PrintReadyDiagnostic(_loadedFromBake ? "baked" : "physics");
            }
        }

        private bool TryLoadBakedGrid(bool createPhysicsQuery)
        {
            if (!TryReadBakedData(out BakedGridSnapshot data)
                || !data.HasValidCellData
                || !TryResolveGridLayout(out Rect2 bounds, out Rect2I region, out float cellSize)
                || !IsBakedDataCompatible(data, bounds, region, cellSize))
            {
                return false;
            }

            InitializeGrid(bounds, region, cellSize, createPhysicsQuery);
            _baseBlockedCells = (byte[])data.BlockedCells.Clone();
            _manualOverrides = NormalizeManualOverrides(data.ManualOverrides, _totalCells);

            _blockedCells = 0;
            for (int index = 0; index < _totalCells; index++)
            {
                bool blocked = ResolveBlocked(index);
                if (!blocked)
                {
                    continue;
                }

                _grid.SetPointSolid(IndexToCell(index), true);
                _blockedCells++;
            }

            _buildCursor = _totalCells;
            _buildStarted = true;
            IsGridReady = true;
            _loadedFromBake = true;
            _status = "world_grid=baked";
            SetPhysicsProcess(false);
            if (!Engine.IsEditorHint() && DebugLogging)
            {
                PrintReadyDiagnostic("baked");
            }

            return true;
        }

        private void InitializeGrid(
            Rect2 bounds,
            Rect2I region,
            float cellSize,
            bool createPhysicsQuery)
        {
            _worldBounds = bounds;
            _gridRegion = region;
            _totalCells = region.Size.X * region.Size.Y;
            _grid = new AStarGrid2D
            {
                Region = region,
                CellSize = Vector2.One * cellSize,
                Offset = Vector2.One * (cellSize * 0.5f),
                DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles,
                DefaultComputeHeuristic = AStarGrid2D.Heuristic.Octile,
                DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile,
                JumpingEnabled = false
            };
            _grid.Update();

            if (!createPhysicsQuery)
            {
                return;
            }

            _clearanceShape = new CircleShape2D
            {
                Radius = Mathf.Max(2f, AgentRadius)
            };
            _shapeQuery = new PhysicsShapeQueryParameters2D
            {
                Shape = _clearanceShape,
                CollisionMask = CollisionMask,
                CollideWithAreas = false,
                CollideWithBodies = true,
                Margin = 0.5f
            };
        }

        private void CommitBakedData()
        {
            string savePath = ResolveBakedDataPath();
            _baseBlockedCells = _workingBlockedCells ?? _baseBlockedCells;
            _workingBlockedCells = null;
            WorldNavigationGridData writableData = EnsureWritableBakedData();
            writableData.SchemaVersion = WorldNavigationGridData.CurrentSchemaVersion;
            writableData.WorldBounds = _worldBounds;
            writableData.GridRegion = _gridRegion;
            writableData.CellSize = Mathf.Max(4f, GridCellSize);
            writableData.AgentRadius = AgentRadius;
            writableData.CollisionMask = CollisionMask;
            writableData.BlockedCells = _baseBlockedCells;
            writableData.ManualOverrides = CountManualOverrides() > 0
                ? _manualOverrides
                : Array.Empty<byte>();
            writableData.EmitChanged();
            Error saveError = SaveBakedResource(savePath);
            if (saveError != Error.Ok)
            {
                _status = "world_grid=bake_save_failed";
                GD.PushError($"[WorldNavigationGrid] Could not save baked data to {savePath}: {saveError}");
            }
            NotifyPropertyListChanged();
        }

        private void PersistManualOverrides()
        {
            if (_bakedData == null)
            {
                return;
            }

            WorldNavigationGridData writableData = EnsureWritableBakedData();
            writableData.ManualOverrides = CountManualOverrides() > 0
                ? _manualOverrides
                : Array.Empty<byte>();
            writableData.EmitChanged();
        }

        private WorldNavigationGridData EnsureWritableBakedData()
        {
            string savePath = ResolveBakedDataPath();
            if (_bakedData is WorldNavigationGridData typedData
                && (string.IsNullOrWhiteSpace(typedData.ResourcePath)
                    || string.Equals(typedData.ResourcePath, savePath, StringComparison.Ordinal)))
            {
                return typedData;
            }

            var writableData = new WorldNavigationGridData
            {
                SchemaVersion = WorldNavigationGridData.CurrentSchemaVersion,
                WorldBounds = _worldBounds,
                GridRegion = _gridRegion,
                CellSize = Mathf.Max(4f, GridCellSize),
                AgentRadius = AgentRadius,
                CollisionMask = CollisionMask,
                BlockedCells = _baseBlockedCells,
                ManualOverrides = CountManualOverrides() > 0
                    ? _manualOverrides
                    : Array.Empty<byte>()
            };
            _bakedData = writableData;
            NotifyPropertyListChanged();
            return writableData;
        }

        private string ResolveBakedDataPath()
        {
            Node sceneRoot = Owner;
            if (sceneRoot == null)
            {
                sceneRoot = this;
                while (sceneRoot.GetParent() != null
                    && sceneRoot.GetParent() != GetTree()?.Root)
                {
                    sceneRoot = sceneRoot.GetParent();
                }
            }

            string scenePath = sceneRoot?.SceneFilePath;
            return string.IsNullOrWhiteSpace(scenePath)
                ? string.Empty
                : scenePath.GetBaseName() + ".navigation_grid.tres";
        }

        private Error SaveBakedResource(string savePath)
        {
            if (!string.Equals(_bakedData.ResourcePath, savePath, StringComparison.Ordinal))
            {
                _bakedData.TakeOverPath(savePath);
            }

            return ResourceSaver.Save(_bakedData, savePath);
        }

        private void NotifyEditorDataChanged()
        {
            QueueRedraw();
            if (Engine.IsEditorHint())
            {
                EmitSignal(SignalName.EditorDataChanged);
            }
        }

        private void FinishFailedBuild(string status, string reason)
        {
            _status = status;
            _editorBakeRunning = false;
            _writeBakedData = false;
            SetPhysicsProcess(false);
            DisposeBuildResources();
            GD.PushWarning($"[WorldNavigationGrid] {reason}");
        }

        private void ResetGridState()
        {
            DisposeBuildResources();
            IsGridReady = false;
            _buildStarted = false;
            _buildCursor = 0;
            _totalCells = 0;
            _blockedCells = 0;
            _workingBlockedCells = null;
            _baseBlockedCells = Array.Empty<byte>();
            _manualOverrides = Array.Empty<byte>();
            _loadedFromBake = false;
            _editorBrushVisible = false;
        }

        private bool TryResolveGridLayout(
            out Rect2 bounds,
            out Rect2I region,
            out float cellSize)
        {
            region = default;
            cellSize = Mathf.Max(4f, GridCellSize);
            if (!TryResolveWorldBounds(out bounds)
                || bounds.Size.X <= 0f
                || bounds.Size.Y <= 0f)
            {
                return false;
            }

            int minX = Mathf.FloorToInt(bounds.Position.X / cellSize);
            int minY = Mathf.FloorToInt(bounds.Position.Y / cellSize);
            int maxX = Mathf.CeilToInt(bounds.End.X / cellSize);
            int maxY = Mathf.CeilToInt(bounds.End.Y / cellSize);
            region = new Rect2I(
                minX,
                minY,
                Mathf.Max(1, maxX - minX),
                Mathf.Max(1, maxY - minY));
            return true;
        }

        private bool TryResolveWorldBounds(out Rect2 bounds)
        {
            if (UseManualBounds && ManualBoundsSize.X > 0f && ManualBoundsSize.Y > 0f)
            {
                Vector2 cornerA = ToGlobal(ManualBoundsPosition);
                Vector2 cornerB = ToGlobal(ManualBoundsPosition + new Vector2(ManualBoundsSize.X, 0f));
                Vector2 cornerC = ToGlobal(ManualBoundsPosition + new Vector2(0f, ManualBoundsSize.Y));
                Vector2 cornerD = ToGlobal(ManualBoundsPosition + ManualBoundsSize);
                Vector2 minimum = new(
                    Mathf.Min(Mathf.Min(cornerA.X, cornerB.X), Mathf.Min(cornerC.X, cornerD.X)),
                    Mathf.Min(Mathf.Min(cornerA.Y, cornerB.Y), Mathf.Min(cornerC.Y, cornerD.Y)));
                Vector2 maximum = new(
                    Mathf.Max(Mathf.Max(cornerA.X, cornerB.X), Mathf.Max(cornerC.X, cornerD.X)),
                    Mathf.Max(Mathf.Max(cornerA.Y, cornerB.Y), Mathf.Max(cornerC.Y, cornerD.Y)));
                bounds = new Rect2(minimum, maximum - minimum);
                return true;
            }

            Node cursor = GetParent();
            while (cursor != null)
            {
                if (cursor is GameLevel level && level.TryGetCameraBounds(out bounds))
                {
                    return true;
                }

                cursor = cursor.GetParent();
            }

            bounds = default;
            return false;
        }

        private bool TryReadBakedData(out BakedGridSnapshot data)
        {
            data = null;
            if (_bakedData == null)
            {
                return false;
            }

            if (_bakedData is WorldNavigationGridData typedData)
            {
                data = new BakedGridSnapshot
                {
                    SchemaVersion = typedData.SchemaVersion,
                    WorldBounds = typedData.WorldBounds,
                    GridRegion = typedData.GridRegion,
                    CellSize = typedData.CellSize,
                    AgentRadius = typedData.AgentRadius,
                    CollisionMask = typedData.CollisionMask,
                    BlockedCells = typedData.BlockedCells ?? Array.Empty<byte>(),
                    ManualOverrides = typedData.ManualOverrides ?? Array.Empty<byte>()
                };
                return true;
            }

            try
            {
                data = new BakedGridSnapshot
                {
                    SchemaVersion = _bakedData.Get(nameof(WorldNavigationGridData.SchemaVersion)).As<int>(),
                    WorldBounds = _bakedData.Get(nameof(WorldNavigationGridData.WorldBounds)).As<Rect2>(),
                    GridRegion = _bakedData.Get(nameof(WorldNavigationGridData.GridRegion)).As<Rect2I>(),
                    CellSize = _bakedData.Get(nameof(WorldNavigationGridData.CellSize)).As<float>(),
                    AgentRadius = _bakedData.Get(nameof(WorldNavigationGridData.AgentRadius)).As<float>(),
                    CollisionMask = _bakedData.Get(nameof(WorldNavigationGridData.CollisionMask)).As<uint>(),
                    BlockedCells = _bakedData.Get(nameof(WorldNavigationGridData.BlockedCells)).As<byte[]>(),
                    ManualOverrides = _bakedData.Get(nameof(WorldNavigationGridData.ManualOverrides)).As<byte[]>()
                };
                data.BlockedCells ??= Array.Empty<byte>();
                data.ManualOverrides ??= Array.Empty<byte>();
                return true;
            }
            catch (Exception exception)
            {
                if (DebugLogging)
                {
                    GD.PushWarning($"[WorldNavigationGrid] Could not read baked data: {exception.Message}");
                }
                data = null;
                return false;
            }
        }

        private bool IsBakedDataCompatible(
            BakedGridSnapshot data,
            Rect2 bounds,
            Rect2I region,
            float cellSize)
        {
            return data != null
                && data.HasValidCellData
                && string.Equals(
                    _bakedData.ResourcePath,
                    ResolveBakedDataPath(),
                    StringComparison.Ordinal)
                && data.GridRegion == region
                && Mathf.IsEqualApprox(data.CellSize, cellSize)
                && Mathf.IsEqualApprox(data.AgentRadius, AgentRadius)
                && data.CollisionMask == CollisionMask
                && RectApproximatelyEqual(data.WorldBounds, bounds);
        }

        private static bool CanReuseManualOverrides(
            BakedGridSnapshot data,
            Rect2I region,
            float cellSize)
        {
            return data != null
                && data.GridRegion == region
                && Mathf.IsEqualApprox(data.CellSize, cellSize)
                && data.ManualOverrides != null
                && data.ManualOverrides.Length == region.Size.X * region.Size.Y;
        }

        private static bool RectApproximatelyEqual(Rect2 left, Rect2 right)
        {
            return left.Position.DistanceSquaredTo(right.Position) <= 0.01f
                && left.Size.DistanceSquaredTo(right.Size) <= 0.01f;
        }

        private static GameLevel FindOwningLevel(Node node)
        {
            Node cursor = node;
            while (cursor != null)
            {
                if (cursor is GameLevel level)
                {
                    return level;
                }

                cursor = cursor.GetParent();
            }

            return null;
        }

        private bool IsInsideUsableBounds(Vector2 point)
        {
            float radius = _clearanceShape?.Radius ?? Mathf.Max(2f, AgentRadius);
            return point.X >= _worldBounds.Position.X + radius
                && point.Y >= _worldBounds.Position.Y + radius
                && point.X < _worldBounds.End.X - radius
                && point.Y < _worldBounds.End.Y - radius;
        }

        private bool IsPositionBlocked(Vector2 worldPosition)
        {
            if (_shapeQuery == null || GetWorld2D()?.DirectSpaceState == null)
            {
                return true;
            }

            _shapeQuery.Transform = new Transform2D(0f, worldPosition);
            return GetWorld2D().DirectSpaceState.IntersectShape(_shapeQuery, 1).Count > 0;
        }

        private bool IsCorridorBlocked(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            if (delta.LengthSquared() <= 4f)
            {
                return false;
            }

            Vector2 perpendicular = new Vector2(-delta.Y, delta.X).Normalized()
                * (_clearanceShape?.Radius ?? AgentRadius);
            return IsRayBlocked(from, to)
                || IsRayBlocked(from + perpendicular, to + perpendicular)
                || IsRayBlocked(from - perpendicular, to - perpendicular);
        }

        private bool IsRayBlocked(Vector2 from, Vector2 to)
        {
            if (GetWorld2D()?.DirectSpaceState == null)
            {
                return true;
            }

            PhysicsRayQueryParameters2D query = PhysicsRayQueryParameters2D.Create(
                from,
                to,
                CollisionMask);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            return GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0;
        }

        private bool TryFindNearestOpen(
            Vector2I origin,
            Vector2 referencePosition,
            out Vector2I openCell)
        {
            int searchRadius = Mathf.Max(1, NearestOpenSearchRadius);
            for (int radius = 1; radius <= searchRadius; radius++)
            {
                float bestDistanceSq = float.PositiveInfinity;
                Vector2I bestCell = origin;
                bool found = false;
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                        {
                            continue;
                        }

                        Vector2I candidate = origin + new Vector2I(x, y);
                        if (!_gridRegion.HasPoint(candidate) || _grid.IsPointSolid(candidate))
                        {
                            continue;
                        }

                        Vector2 candidatePosition = CellToWorld(candidate);
                        float distanceSq = candidatePosition.DistanceSquaredTo(referencePosition);
                        if (IsRayBlocked(referencePosition, candidatePosition))
                        {
                            distanceSq += GridCellSize * GridCellSize * 4f;
                        }
                        if (distanceSq < bestDistanceSq)
                        {
                            bestDistanceSq = distanceSq;
                            bestCell = candidate;
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    openCell = bestCell;
                    return true;
                }
            }

            openCell = origin;
            return false;
        }

        private void ReapplyAllSolidStates()
        {
            _blockedCells = 0;
            for (int index = 0; index < _totalCells; index++)
            {
                bool blocked = ResolveBlocked(index);
                _grid.SetPointSolid(IndexToCell(index), blocked);
                if (blocked)
                {
                    _blockedCells++;
                }
            }
        }

        private bool ResolveBlocked(int index)
        {
            return _manualOverrides[index] switch
            {
                ForceWalkable => false,
                ForceBlocked => true,
                _ => _baseBlockedCells[index] != 0
            };
        }

        private void DrawPreviewCells(CanvasItem target, Transform2D worldToViewport)
        {
            float cellSize = Mathf.Max(4f, GridCellSize);
            for (int localY = 0; localY < _gridRegion.Size.Y; localY++)
            {
                int rowStart = localY * _gridRegion.Size.X;
                int runStart = 0;
                int runState = GetPreviewState(rowStart);
                for (int localX = 1; localX <= _gridRegion.Size.X; localX++)
                {
                    int nextState = localX < _gridRegion.Size.X
                        ? GetPreviewState(rowStart + localX)
                        : -1;
                    if (nextState == runState)
                    {
                        continue;
                    }

                    Vector2 position = new(
                        (_gridRegion.Position.X + runStart) * cellSize,
                        (_gridRegion.Position.Y + localY) * cellSize);
                    Vector2 size = new((localX - runStart) * cellSize, cellSize);
                    Rect2 screenRect = TransformRect(new Rect2(position, size), worldToViewport);
                    target.DrawRect(screenRect, GetPreviewColor(runState), true);
                    runStart = localX;
                    runState = nextState;
                }
            }
        }

        private void DrawPreviewGridLines(CanvasItem target, Transform2D worldToViewport)
        {
            float cellSize = Mathf.Max(4f, GridCellSize);
            float left = _gridRegion.Position.X * cellSize;
            float top = _gridRegion.Position.Y * cellSize;
            float right = _gridRegion.End.X * cellSize;
            float bottom = _gridRegion.End.Y * cellSize;
            for (int x = _gridRegion.Position.X; x <= _gridRegion.End.X; x++)
            {
                float worldX = x * cellSize;
                target.DrawLine(
                    worldToViewport * new Vector2(worldX, top),
                    worldToViewport * new Vector2(worldX, bottom),
                    GridLinePreviewColor);
            }

            for (int y = _gridRegion.Position.Y; y <= _gridRegion.End.Y; y++)
            {
                float worldY = y * cellSize;
                target.DrawLine(
                    worldToViewport * new Vector2(left, worldY),
                    worldToViewport * new Vector2(right, worldY),
                    GridLinePreviewColor);
            }
        }

        private void DrawBrushPreview(CanvasItem target, Transform2D worldToViewport)
        {
            if (!_editorBrushVisible || _editorBrushMode == 0)
            {
                return;
            }

            Color color = _editorBrushMode switch
            {
                EditorPaintWalkable => ForcedWalkablePreviewColor,
                EditorPaintBlocked => ForcedBlockedPreviewColor,
                _ => BoundsPreviewColor
            };
            color.A = 0.96f;
            int reach = _editorBrushRadius - 1;
            float radiusSquared = Mathf.Max(
                0.25f,
                (_editorBrushRadius - 0.35f) * (_editorBrushRadius - 0.35f));
            float cellSize = Mathf.Max(4f, GridCellSize);
            for (int y = -reach; y <= reach; y++)
            {
                for (int x = -reach; x <= reach; x++)
                {
                    if (x * x + y * y > radiusSquared)
                    {
                        continue;
                    }

                    Vector2I cell = _editorBrushCell + new Vector2I(x, y);
                    if (!_gridRegion.HasPoint(cell))
                    {
                        continue;
                    }

                    Rect2 rect = new(
                        new Vector2(cell.X * cellSize, cell.Y * cellSize),
                        Vector2.One * cellSize);
                    Rect2 screenRect = TransformRect(rect.Grow(-1f), worldToViewport);
                    target.DrawRect(screenRect, color, false, 2f, true);
                }
            }
        }

        private static Rect2 TransformRect(Rect2 rect, Transform2D transform)
        {
            Vector2 first = transform * rect.Position;
            Vector2 second = transform * rect.End;
            Vector2 position = new(
                Mathf.Min(first.X, second.X),
                Mathf.Min(first.Y, second.Y));
            Vector2 end = new(
                Mathf.Max(first.X, second.X),
                Mathf.Max(first.Y, second.Y));
            return new Rect2(position, end - position);
        }

        private int GetPreviewState(int index)
        {
            return _manualOverrides[index] switch
            {
                ForceWalkable => 2,
                ForceBlocked => 3,
                _ => _baseBlockedCells[index] != 0 ? 1 : 0
            };
        }

        private static Color GetPreviewColor(int state)
        {
            return state switch
            {
                1 => BlockedPreviewColor,
                2 => ForcedWalkablePreviewColor,
                3 => ForcedBlockedPreviewColor,
                _ => WalkablePreviewColor
            };
        }

        private int CountManualOverrides()
        {
            int count = 0;
            for (int i = 0; i < _manualOverrides.Length; i++)
            {
                if (_manualOverrides[i] != NoManualOverride)
                {
                    count++;
                }
            }

            return count;
        }

        private int CellToIndex(Vector2I cell)
        {
            Vector2I local = cell - _gridRegion.Position;
            return local.Y * _gridRegion.Size.X + local.X;
        }

        private Vector2I IndexToCell(int index)
        {
            int localX = index % _gridRegion.Size.X;
            int localY = index / _gridRegion.Size.X;
            return _gridRegion.Position + new Vector2I(localX, localY);
        }

        private Vector2I WorldToCell(Vector2 point)
        {
            float cellSize = Mathf.Max(4f, GridCellSize);
            return new Vector2I(
                Mathf.FloorToInt(point.X / cellSize),
                Mathf.FloorToInt(point.Y / cellSize));
        }

        private Vector2 CellToWorld(Vector2I cell)
        {
            float cellSize = Mathf.Max(4f, GridCellSize);
            return new Vector2(
                (cell.X + 0.5f) * cellSize,
                (cell.Y + 0.5f) * cellSize);
        }

        private Vector2I ClampCellToRegion(Vector2I cell)
        {
            return new Vector2I(
                Mathf.Clamp(cell.X, _gridRegion.Position.X, _gridRegion.End.X - 1),
                Mathf.Clamp(cell.Y, _gridRegion.Position.Y, _gridRegion.End.Y - 1));
        }

        private static byte[] NormalizeManualOverrides(byte[] source, int expectedLength)
        {
            if (source == null || source.Length != expectedLength)
            {
                return new byte[expectedLength];
            }

            var normalized = (byte[])source.Clone();
            for (int i = 0; i < normalized.Length; i++)
            {
                if (normalized[i] > ForceBlocked)
                {
                    normalized[i] = NoManualOverride;
                }
            }

            return normalized;
        }

        private static bool ArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static float MeasurePath(Vector2 startPosition, List<Vector2> points)
        {
            float distance = 0f;
            Vector2 previous = startPosition;
            for (int i = 0; i < points.Count; i++)
            {
                distance += previous.DistanceTo(points[i]);
                previous = points[i];
            }

            return distance;
        }

        private void PrintReadyDiagnostic(string source)
        {
            GD.Print($"[WorldNavigationGrid] READY source={source} cells={_totalCells} "
                + $"blocked={_blockedCells} cell={GridCellSize:0.#} "
                + $"radius={AgentRadius:0.#} bounds={_worldBounds}");
        }

        private void DisposePhysicsQueryResources()
        {
            _shapeQuery?.Dispose();
            _shapeQuery = null;
            _clearanceShape?.Dispose();
            _clearanceShape = null;
        }

        private void DisposeBuildResources()
        {
            DisposePhysicsQueryResources();
            _grid?.Dispose();
            _grid = null;
        }
    }
}
