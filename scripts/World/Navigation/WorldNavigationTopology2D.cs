using Godot;
using System.Collections.Generic;

namespace AshesofaDyingWorld.World.Navigation
{
    /// <summary>
    /// Registry topology của một map 2D: vùng cao độ + connector (cầu thang).
    /// P3 cố ý tách semantic topology khỏi collision và RVO:
    /// collision trả lời "đụng hay không", còn topology trả lời "muốn đổi tầng phải đi qua đâu".
    /// </summary>
    public partial class WorldNavigationTopology2D : Node2D
    {
        public const string GroupName = "WorldNavigationTopology2D";

        [Export] public bool Enabled { get; set; } = true;
        [Export] public int DefaultElevation { get; set; } = 0;
        [Export] public bool DebugLogging { get; set; } = false;

        private readonly List<ElevationRegion2D> _regions = new();
        private readonly List<StairLink2D> _links = new();

        public override void _Ready()
        {
            AddToGroup(GroupName);
            RefreshTopology();
        }

        public void RefreshTopology()
        {
            _regions.Clear();
            _links.Clear();
            CollectChildren(this);

            if (DebugLogging)
            {
                GD.Print($"[WorldTopology] READY regions={_regions.Count} links={_links.Count} defaultElevation={DefaultElevation}");
            }
        }

        public int ResolveElevation(Vector2 worldPoint)
        {
            if (!Enabled)
            {
                return DefaultElevation;
            }

            int resolved = DefaultElevation;
            int bestPriority = int.MinValue;
            for (int i = 0; i < _regions.Count; i++)
            {
                ElevationRegion2D region = _regions[i];
                if (region == null || !GodotObject.IsInstanceValid(region) || !region.ContainsWorldPoint(worldPoint))
                {
                    continue;
                }

                if (region.Priority >= bestPriority)
                {
                    bestPriority = region.Priority;
                    resolved = region.ElevationLevel;
                }
            }

            return resolved;
        }

        /// <summary>
        /// Chọn cầu thang trực tiếp rẻ nhất giữa hai tầng. Hiện field_01 chỉ có 0<->1.
        /// API này đã hỗ trợ nhiều cầu thang song song; nếu sau này có 3+ tầng nối chuỗi,
        /// router có thể nâng lên graph search mà không phải sửa collision/local avoidance.
        /// </summary>
        public bool TryFindBestLink(
            int fromElevation,
            int toElevation,
            Vector2 selfPosition,
            Vector2 targetPosition,
            out StairLink2D bestLink,
            out Vector2 bestEntry,
            out Vector2 bestExit)
        {
            bestLink = null;
            bestEntry = Vector2.Zero;
            bestExit = Vector2.Zero;
            if (!Enabled || fromElevation == toElevation)
            {
                return false;
            }

            float bestCost = float.PositiveInfinity;
            for (int i = 0; i < _links.Count; i++)
            {
                StairLink2D link = _links[i];
                if (link == null || !GodotObject.IsInstanceValid(link) || !link.Connects(fromElevation, toElevation))
                {
                    continue;
                }

                Vector2 entry = link.GetEntryPoint(fromElevation);
                Vector2 exit = link.GetExitPoint(fromElevation);
                float cost = selfPosition.DistanceTo(entry)
                    + entry.DistanceTo(exit)
                    + exit.DistanceTo(targetPosition);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestLink = link;
                    bestEntry = entry;
                    bestExit = exit;
                }
            }

            return bestLink != null;
        }

        public static WorldNavigationTopology2D FindFor(Node context)
        {
            SceneTree tree = context?.GetTree();
            if (tree == null)
            {
                return null;
            }

            foreach (Node node in tree.GetNodesInGroup(GroupName))
            {
                if (node is WorldNavigationTopology2D topology
                    && GodotObject.IsInstanceValid(topology)
                    && topology.Enabled)
                {
                    return topology;
                }
            }

            return null;
        }

        private void CollectChildren(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is ElevationRegion2D region)
                {
                    _regions.Add(region);
                }
                else if (child is StairLink2D link)
                {
                    _links.Add(link);
                }

                CollectChildren(child);
            }
        }
    }
}
