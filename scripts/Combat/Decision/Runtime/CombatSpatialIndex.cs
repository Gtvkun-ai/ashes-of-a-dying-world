using System.Collections.Generic;
using Godot;
using AshesofaDyingWorld.Combat.Actors;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Spatial hash P1 dùng chung cho toàn Decision Core.
    /// Thay vì mỗi AI quét toàn bộ group "Combatant", index chỉ rebuild tối đa 1 lần / physics frame,
    /// sau đó target selection, leader threat và head-on avoidance chỉ đọc các cell lân cận.
    ///
    /// Đây là broad-phase thuần deterministic: kết quả query được sort theo InstanceId để cùng input
    /// luôn cho cùng thứ tự candidate, hữu ích khi benchmark circle-swap / crossing / doorway.
    /// </summary>
    internal static class CombatSpatialIndex
    {
        private const float DefaultCellSize = 96f;

        private static readonly Dictionary<Vector2I, List<CombatCharacter>> Cells = new();
        private static readonly Dictionary<ulong, CombatCharacter> ById = new();

        private static SceneTree _tree;
        private static ulong _builtPhysicsFrame = ulong.MaxValue;
        private static float _cellSize = DefaultCellSize;

        public static ulong Rebuilds { get; private set; }
        public static ulong IndexedActors { get; private set; }
        public static ulong Queries { get; private set; }
        public static ulong CandidateVisits { get; private set; }

        public static CombatCharacter FindById(SceneTree tree, ulong instanceId)
        {
            EnsureBuilt(tree);
            return ById.TryGetValue(instanceId, out CombatCharacter actor) && IsUsable(actor)
                ? actor
                : null;
        }

        public static int QueryRadius(
            SceneTree tree,
            Vector2 center,
            float radius,
            List<CombatCharacter> results)
        {
            results?.Clear();
            if (results == null || tree == null || radius <= 0f)
            {
                return 0;
            }

            EnsureBuilt(tree);
            Queries++;

            float safeRadius = Mathf.Max(1f, radius);
            float radiusSq = safeRadius * safeRadius;
            Vector2I minCell = ToCell(center - Vector2.One * safeRadius);
            Vector2I maxCell = ToCell(center + Vector2.One * safeRadius);

            for (int y = minCell.Y; y <= maxCell.Y; y++)
            {
                for (int x = minCell.X; x <= maxCell.X; x++)
                {
                    if (!Cells.TryGetValue(new Vector2I(x, y), out List<CombatCharacter> bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        CombatCharacter actor = bucket[i];
                        CandidateVisits++;
                        if (!IsUsable(actor)
                            || actor.CombatCenter.DistanceSquaredTo(center) > radiusSq)
                        {
                            continue;
                        }

                        results.Add(actor);
                    }
                }
            }

            // Một candidate chỉ thuộc đúng một cell, nên không cần distinct. Sort giữ behavior deterministic.
            results.Sort(CompareActorId);
            return results.Count;
        }

        public static string GetDiagnosticsSummary()
        {
            double averageCandidates = Queries > 0
                ? (double)CandidateVisits / Queries
                : 0.0;
            return $"builds={Rebuilds} actors={IndexedActors} queries={Queries} cand/q={averageCandidates:0.0}";
        }

        private static void EnsureBuilt(SceneTree tree)
        {
            if (tree == null)
            {
                ClearForTree(null);
                return;
            }

            ulong frame = Engine.GetPhysicsFrames();
            if (_tree == tree && _builtPhysicsFrame == frame)
            {
                return;
            }

            if (_tree != tree)
            {
                ClearForTree(tree);
            }

            Cells.Clear();
            ById.Clear();
            IndexedActors = 0;

            foreach (Node node in tree.GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter actor || !IsUsable(actor))
                {
                    continue;
                }

                Vector2I cell = ToCell(actor.CombatCenter);
                if (!Cells.TryGetValue(cell, out List<CombatCharacter> bucket))
                {
                    bucket = new List<CombatCharacter>(8);
                    Cells[cell] = bucket;
                }

                bucket.Add(actor);
                ById[actor.GetInstanceId()] = actor;
                IndexedActors++;
            }

            _builtPhysicsFrame = frame;
            Rebuilds++;
        }

        private static void ClearForTree(SceneTree tree)
        {
            _tree = tree;
            _builtPhysicsFrame = ulong.MaxValue;
            Cells.Clear();
            ById.Clear();
        }

        private static Vector2I ToCell(Vector2 point)
        {
            int x = (int)Mathf.Floor(point.X / _cellSize);
            int y = (int)Mathf.Floor(point.Y / _cellSize);
            return new Vector2I(x, y);
        }

        private static int CompareActorId(CombatCharacter a, CombatCharacter b)
        {
            ulong left = a?.GetInstanceId() ?? 0UL;
            ulong right = b?.GetInstanceId() ?? 0UL;
            return left.CompareTo(right);
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
