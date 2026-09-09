using Godot;
using AshesofaDyingWorld.Combat.Decision.Movement;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Debug
{
    /// <summary>
    /// Xuất benchmark movement P2 ra JSON để so A/B giữa các lần chạy.
    /// Có cả payload per-agent và aggregate scene cho circle-swap / crossing / doorway.
    /// File chỉ chứa telemetry debug, không chạm save game/schema gameplay.
    /// </summary>
    public static class MovementBenchmarkExporter
    {
        public static string ExportLatest(CombatDecisionAgent agent, string destination = "")
        {
            if (agent == null || agent.ControlledCharacter == null)
            {
                return string.Empty;
            }

            CombatMovementBenchmarkSnapshot snapshot = agent.GetMovementBenchmarkSnapshot();
            string actorId = agent.ControlledCharacter.CombatantId ?? "actor";
            string safeActor = actorId.Replace("/", "_").Replace("\\", "_");
            string safeLabel = Sanitize(snapshot.Label);
            string path = string.IsNullOrWhiteSpace(destination)
                ? $"user://movement_benchmark_{safeActor}_{safeLabel}_{(long)Time.GetUnixTimeFromSystem()}.json"
                : destination;

            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"[MovementBenchmark] Không mở được file: {path}");
                return string.Empty;
            }

            file.StoreString(Json.Stringify(BuildAgentPayload(agent, snapshot), "  "));
            string absolute = ProjectSettings.GlobalizePath(path);
            GD.Print($"[MovementBenchmark] EXPORT actor={actorId} path={absolute}");
            return absolute;
        }

        public static string ExportScene(SceneTree tree, string destination = "")
        {
            if (tree == null)
            {
                return string.Empty;
            }

            var agents = new Godot.Collections.Array();
            int agentCount = 0;
            int successful = 0;
            int failed = 0;
            int aborted = 0;
            ulong sampledFrames = 0;
            ulong collisionFrames = 0;
            ulong collisionContacts = 0;
            float weightedSeconds = 0f;
            float weightedStretch = 0f;
            float weightedJitter = 0f;
            float weightedCpu = 0f;
            ulong weightedCpuTicks = 0;
            bool anyCompleted = false;
            bool allAgentsHaveSamples = true;
            bool allHardGatesPass = true;
            float weightedScore = 0f;
            string sceneLabel = "movement";

            foreach (Node node in tree.GetNodesInGroup("CombatDecisionAgent"))
            {
                if (node is not CombatDecisionAgent agent
                    || agent.ControlledCharacter == null
                    || !agent.Enabled
                    || !agent.UseDecisionCore
                    || agent.ShadowMode)
                {
                    continue;
                }

                CombatMovementBenchmarkSnapshot snapshot = agent.GetMovementBenchmarkSnapshot();
                if (agentCount == 0 && !string.IsNullOrWhiteSpace(snapshot.Label))
                {
                    sceneLabel = snapshot.Label;
                }

                agents.Add(BuildAgentPayload(agent, snapshot));
                agentCount++;
                successful += snapshot.SuccessfulSegments;
                failed += snapshot.FailedSegments;
                aborted += snapshot.AbortedSegments;
                sampledFrames += snapshot.SampledPhysicsFrames;
                collisionFrames += snapshot.CollisionFrames;
                collisionContacts += snapshot.CollisionContacts;

                int completed = snapshot.CompletedSegments;
                if (completed > 0)
                {
                    anyCompleted = true;
                    weightedSeconds += snapshot.AverageSeconds * completed;
                    weightedStretch += snapshot.AveragePathStretch * completed;
                    weightedJitter += snapshot.AverageJitterDegrees * completed;
                    weightedScore += snapshot.Score * completed;
                    allHardGatesPass &= snapshot.HardGatePass;
                }
                else
                {
                    allAgentsHaveSamples = false;
                }

                ulong cpuTicks = snapshot.SolverTicks + snapshot.MotorTicks;
                if (cpuTicks > 0)
                {
                    weightedCpu += snapshot.AverageCpuUsec * cpuTicks;
                    weightedCpuTicks += cpuTicks;
                }
            }

            if (agentCount <= 0)
            {
                GD.PushWarning("[MovementBenchmark] Không có CombatDecisionAgent để export.");
                return string.Empty;
            }

            int totalCompleted = successful + failed;
            float successRate = totalCompleted > 0 ? (float)successful / totalCompleted : 0f;
            float collisionRate = sampledFrames > 0 ? (float)collisionFrames / sampledFrames : 0f;
            float avgSeconds = totalCompleted > 0 ? weightedSeconds / totalCompleted : 0f;
            float avgStretch = totalCompleted > 0 ? weightedStretch / totalCompleted : 0f;
            float avgJitter = totalCompleted > 0 ? weightedJitter / totalCompleted : 0f;
            float avgCpu = weightedCpuTicks > 0 ? weightedCpu / weightedCpuTicks : 0f;
            float sceneScore = totalCompleted > 0 ? weightedScore / totalCompleted : 0f;
            string gate = !anyCompleted || !allAgentsHaveSamples
                ? "WAIT"
                : (allHardGatesPass ? "PASS" : "FAIL");
            if (gate != "PASS")
            {
                sceneScore = 0f;
            }

            var payload = new Godot.Collections.Dictionary
            {
                ["schema"] = "combat_movement_benchmark_scene_v1",
                ["label"] = sceneLabel,
                ["agent_count"] = agentCount,
                ["hard_gate"] = gate,
                ["score"] = sceneScore,
                ["aggregate"] = new Godot.Collections.Dictionary
                {
                    ["success_segments"] = successful,
                    ["failed_segments"] = failed,
                    ["aborted_target_moved"] = aborted,
                    ["success_rate"] = successRate,
                    ["collision_frames"] = (long)collisionFrames,
                    ["collision_contacts"] = (long)collisionContacts,
                    ["collision_rate"] = collisionRate,
                    ["average_seconds"] = avgSeconds,
                    ["average_path_stretch"] = avgStretch,
                    ["average_jitter_degrees"] = avgJitter,
                    ["average_cpu_usec"] = avgCpu
                },
                ["agents"] = agents
            };

            string safeLabel = Sanitize(sceneLabel);
            string path = string.IsNullOrWhiteSpace(destination)
                ? $"user://movement_benchmark_scene_{safeLabel}_{(long)Time.GetUnixTimeFromSystem()}.json"
                : destination;

            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"[MovementBenchmark] Không mở được file: {path}");
                return string.Empty;
            }

            file.StoreString(Json.Stringify(payload, "  "));
            string absolute = ProjectSettings.GlobalizePath(path);
            GD.Print($"[MovementBenchmark] EXPORT SCENE agents={agentCount} gate={gate} path={absolute}");
            return absolute;
        }

        private static Godot.Collections.Dictionary BuildAgentPayload(
            CombatDecisionAgent agent,
            CombatMovementBenchmarkSnapshot snapshot)
        {
            float elapsed = Mathf.Max(0.001f, snapshot.ElapsedSeconds);
            string gate = snapshot.CompletedSegments <= 0
                ? "WAIT"
                : (snapshot.HardGatePass ? "PASS" : "FAIL");

            return new Godot.Collections.Dictionary
            {
                ["schema"] = "combat_movement_benchmark_v1",
                ["runtime_build"] = agent.RuntimeBuildId,
                ["actor"] = agent.ControlledCharacter?.CombatantId ?? "unknown",
                ["label"] = snapshot.Label,
                ["running"] = snapshot.IsRunning,
                ["elapsed_seconds"] = snapshot.ElapsedSeconds,
                ["hard_gate"] = gate,
                ["score"] = snapshot.Score,
                ["thresholds"] = new Godot.Collections.Dictionary
                {
                    ["min_success_rate"] = snapshot.MinSuccessRate,
                    ["max_collision_rate"] = snapshot.MaxCollisionRate,
                    ["cpu_budget_usec"] = snapshot.CpuBudgetUsec
                },
                ["quality"] = new Godot.Collections.Dictionary
                {
                    ["success_segments"] = snapshot.SuccessfulSegments,
                    ["failed_segments"] = snapshot.FailedSegments,
                    ["aborted_target_moved"] = snapshot.AbortedSegments,
                    ["success_rate"] = snapshot.SuccessRate,
                    ["collision_frames"] = (long)snapshot.CollisionFrames,
                    ["collision_contacts"] = (long)snapshot.CollisionContacts,
                    ["collision_rate"] = snapshot.CollisionRate,
                    ["average_seconds"] = snapshot.AverageSeconds,
                    ["average_path_stretch"] = snapshot.AveragePathStretch,
                    ["average_jitter_degrees"] = snapshot.AverageJitterDegrees,
                    ["average_cpu_usec"] = snapshot.AverageCpuUsec
                },
                ["rates_hz"] = new Godot.Collections.Dictionary
                {
                    ["tactical_solve"] = snapshot.SolverTicks / elapsed,
                    ["local_motor"] = snapshot.MotorTicks / elapsed,
                    ["path_request"] = snapshot.PathRequests / elapsed,
                    ["rvo_submit"] = snapshot.AvoidanceSubmissions / elapsed
                },
                ["counters"] = new Godot.Collections.Dictionary
                {
                    ["sampled_physics_frames"] = (long)snapshot.SampledPhysicsFrames,
                    ["solver_ticks"] = (long)snapshot.SolverTicks,
                    ["motor_ticks"] = (long)snapshot.MotorTicks,
                    ["path_requests"] = (long)snapshot.PathRequests,
                    ["path_reuse_hits"] = (long)snapshot.PathReuseHits,
                    ["path_budget_deferrals"] = (long)snapshot.PathBudgetDeferrals,
                    ["ray_probe_samples"] = (long)snapshot.ProbeSamples,
                    ["shape_probe_samples"] = (long)snapshot.ShapeProbeSamples,
                    ["rvo_submissions"] = (long)snapshot.AvoidanceSubmissions,
                    ["rvo_corrections"] = (long)snapshot.AvoidanceCorrections,
                    ["passing_side_locks"] = (long)snapshot.PassingSideLocks,
                    ["stuck_events"] = (long)snapshot.StuckEvents
                },
                ["movement_config"] = agent.GetMovementBenchmarkConfigPayload()
            };
        }

        private static string Sanitize(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "movement" : value.Trim();
            foreach (char invalid in new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', ' ' })
            {
                safe = safe.Replace(invalid, '_');
            }
            return safe;
        }
    }
}
