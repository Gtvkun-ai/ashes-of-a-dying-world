using Godot;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Debug
{
    /// <summary>
    /// Điều khiển benchmark cho cả scene. Circle-swap/crossing/doorway là bài test nhiều actor,
    /// nên start/stop một agent đơn lẻ sẽ cho số liệu méo.
    /// </summary>
    public static class MovementBenchmarkCoordinator
    {
        public static bool IsAnyRunning(SceneTree tree)
        {
            if (tree == null)
            {
                return false;
            }

            foreach (Node node in tree.GetNodesInGroup("CombatDecisionAgent"))
            {
                if (node is CombatDecisionAgent agent && agent.IsMovementBenchmarkRunning)
                {
                    return true;
                }
            }
            return false;
        }

        public static int StartAll(SceneTree tree)
        {
            if (tree == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Node node in tree.GetNodesInGroup("CombatDecisionAgent"))
            {
                if (node is not CombatDecisionAgent agent
                    || !agent.Enabled
                    || !agent.UseDecisionCore
                    || agent.ShadowMode)
                {
                    continue;
                }

                agent.StartMovementBenchmark();
                count++;
            }

            if (count <= 0)
            {
                GD.PushWarning("[MovementBenchmark] Không có agent live. Cần UseDecisionCore=true và ShadowMode=false để benchmark locomotion.");
            }
            else
            {
                GD.Print($"[MovementBenchmark] START ALL agents={count}");
            }
            return count;
        }

        public static int StopAll(SceneTree tree)
        {
            if (tree == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Node node in tree.GetNodesInGroup("CombatDecisionAgent"))
            {
                if (node is not CombatDecisionAgent agent || !agent.IsMovementBenchmarkRunning)
                {
                    continue;
                }

                agent.StopMovementBenchmark();
                count++;
            }

            GD.Print($"[MovementBenchmark] STOP ALL agents={count}");
            return count;
        }

        public static void ToggleAll(SceneTree tree)
        {
            if (IsAnyRunning(tree))
            {
                StopAll(tree);
            }
            else
            {
                StartAll(tree);
            }
        }
    }
}
