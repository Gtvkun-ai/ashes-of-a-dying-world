using Godot;
using System;

namespace AshesofaDyingWorld.World.Navigation
{
    /// <summary>
    /// Serialized collision bake and hand-painted overrides for one world grid.
    /// </summary>
    [GlobalClass]
    public partial class WorldNavigationGridData : Resource
    {
        public const int CurrentSchemaVersion = 1;

        [Export] public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        [Export] public Rect2 WorldBounds { get; set; }
        [Export] public Rect2I GridRegion { get; set; }
        [Export] public float CellSize { get; set; }
        [Export] public float AgentRadius { get; set; }
        [Export(PropertyHint.Layers2DPhysics)] public uint CollisionMask { get; set; }
        [Export] public byte[] BlockedCells { get; set; } = Array.Empty<byte>();
        [Export] public byte[] ManualOverrides { get; set; } = Array.Empty<byte>();

        public int CellCount => GridRegion.Size.X * GridRegion.Size.Y;

        public bool HasValidCellData()
        {
            return SchemaVersion == CurrentSchemaVersion
                && GridRegion.Size.X > 0
                && GridRegion.Size.Y > 0
                && BlockedCells != null
                && BlockedCells.Length == CellCount;
        }
    }
}
