using Godot;
using AshesofaDyingWorld.Combat.Decision.Movement;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Debug
{
    /// <summary>
    /// Export benchmark P2.1. Schema v2 cố ý ghi rõ "blocking collision" và "raw slide contact"
    /// để JSON không còn làm người đọc tưởng mọi lần chạm tường đều là pathfinding failure.
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
            int qualitySampleAgents = 0;
            int successful = 0;
            int failed = 0;
            int aborted = 0;
            ulong sampledFrames = 0;
            ulong motorTicksTotal = 0;
            ulong rawCollisionFrames = 0;
            ulong rawCollisionContacts = 0;
            ulong blockingCollisionFrames = 0;
            ulong blockingCollisionContacts = 0;
            ulong followFrames = 0;
            ulong followInsideFrames = 0;
            float weightedFollowError = 0f;
            float weightedFollowP95 = 0f;
            float weightedCpu = 0f;
            ulong weightedCpuTicks = 0;
            float weightedSuccess = 0f;
            float weightedScore = 0f;
            ulong weightedQualitySamples = 0;
            bool allHardGatesPass = true;
            string sceneLabel = "movement";
            string sceneMode = string.Empty;
            bool mixedModes = false;

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
                if (agentCount == 0)
                {
                    sceneLabel = string.IsNullOrWhiteSpace(snapshot.Label) ? "movement" : snapshot.Label;
                    sceneMode = snapshot.Mode.ToString();
                }
                else if (sceneMode != snapshot.Mode.ToString())
                {
                    mixedModes = true;
                }

                agents.Add(BuildAgentPayload(agent, snapshot));
                agentCount++;
                successful += snapshot.SuccessfulSegments;
                failed += snapshot.FailedSegments;
                aborted += snapshot.AbortedSegments;
                sampledFrames += snapshot.SampledPhysicsFrames;
                motorTicksTotal += snapshot.MotorTicks;
                rawCollisionFrames += snapshot.RawCollisionFrames;
                rawCollisionContacts += snapshot.RawCollisionContacts;
                blockingCollisionFrames += snapshot.BlockingCollisionFrames;
                blockingCollisionContacts += snapshot.BlockingCollisionContacts;

                if (snapshot.HasQualitySample)
                {
                    qualitySampleAgents++;
                    allHardGatesPass &= snapshot.HardGatePass;
                }
                else
                {
                    allHardGatesPass = false;
                }

                ulong qualityWeight = snapshot.Mode == CombatMovementBenchmarkMode.Follow
                    ? snapshot.FollowSampleFrames
                    : (snapshot.CompletedSegments > 0 ? (ulong)snapshot.CompletedSegments : 1UL);
                if (qualityWeight > 0)
                {
                    weightedSuccess += snapshot.SuccessRate * qualityWeight;
                    weightedScore += snapshot.Score * qualityWeight;
                    weightedQualitySamples += qualityWeight;
                }

                if (snapshot.FollowSampleFrames > 0)
                {
                    followFrames += snapshot.FollowSampleFrames;
                    followInsideFrames += snapshot.FollowInsideBandFrames;
                    weightedFollowError += snapshot.AverageFollowError * snapshot.FollowSampleFrames;
                    weightedFollowP95 += snapshot.P95FollowError * snapshot.FollowSampleFrames;
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

            float successRate = weightedQualitySamples > 0 ? weightedSuccess / weightedQualitySamples : 0f;
            ulong collisionSampleFrames = motorTicksTotal > 0 ? motorTicksTotal : sampledFrames;
            float rawCollisionRate = collisionSampleFrames > 0 ? (float)rawCollisionFrames / collisionSampleFrames : 0f;
            float blockingCollisionRate = collisionSampleFrames > 0 ? (float)blockingCollisionFrames / collisionSampleFrames : 0f;
            float followBandRate = followFrames > 0 ? (float)followInsideFrames / followFrames : 0f;
            float avgFollowError = followFrames > 0 ? weightedFollowError / followFrames : 0f;
            float avgFollowP95 = followFrames > 0 ? weightedFollowP95 / followFrames : 0f;
            float avgCpu = weightedCpuTicks > 0 ? weightedCpu / weightedCpuTicks : 0f;
            float sceneScore = weightedQualitySamples > 0 ? weightedScore / weightedQualitySamples : 0f;
            string gate = qualitySampleAgents < agentCount
                ? "WAIT"
                : (allHardGatesPass ? "PASS" : "FAIL");
            if (gate != "PASS")
            {
                sceneScore = 0f;
            }

            var payload = new Godot.Collections.Dictionary
            {
                ["schema"] = "combat_movement_benchmark_scene_v2",
                ["label"] = sceneLabel,
                ["mode"] = mixedModes ? "Mixed" : sceneMode,
                ["agent_count"] = agentCount,
                ["quality_sample_agents"] = qualitySampleAgents,
                ["hard_gate"] = gate,
                ["score"] = sceneScore,
                ["aggregate"] = new Godot.Collections.Dictionary
                {
                    ["success_segments"] = successful,
                    ["failed_segments"] = failed,
                    ["aborted_segments"] = aborted,
                    ["primary_success_rate"] = successRate,
                    ["sampled_physics_frames"] = (long)sampledFrames,
                    ["blocking_collision_frames"] = (long)blockingCollisionFrames,
                    ["blocking_collision_contacts"] = (long)blockingCollisionContacts,
                    ["blocking_collision_rate"] = blockingCollisionRate,
                    ["raw_slide_contact_frames"] = (long)rawCollisionFrames,
                    ["raw_slide_contacts"] = (long)rawCollisionContacts,
                    ["raw_slide_contact_rate"] = rawCollisionRate,
                    ["follow_sample_frames"] = (long)followFrames,
                    ["follow_band_rate"] = followBandRate,
                    ["average_follow_error_px"] = avgFollowError,
                    ["average_agent_p95_follow_error_px"] = avgFollowP95,
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
            string gate = !snapshot.HasQualitySample
                ? "WAIT"
                : (snapshot.HardGatePass ? "PASS" : "FAIL");

            var quality = new Godot.Collections.Dictionary
            {
                ["success_segments"] = snapshot.SuccessfulSegments,
                ["failed_segments"] = snapshot.FailedSegments,
                ["aborted_segments"] = snapshot.AbortedSegments,
                ["primary_success_rate"] = snapshot.SuccessRate,
                ["average_seconds"] = snapshot.AverageSeconds,
                ["average_path_stretch"] = snapshot.AveragePathStretch,
                ["average_jitter_degrees"] = snapshot.AverageJitterDegrees,
                ["average_cpu_usec"] = snapshot.AverageCpuUsec,
                ["blocking_collision"] = new Godot.Collections.Dictionary
                {
                    ["frames"] = (long)snapshot.BlockingCollisionFrames,
                    ["contacts"] = (long)snapshot.BlockingCollisionContacts,
                    ["rate"] = snapshot.BlockingCollisionRate
                },
                ["raw_slide_contact"] = new Godot.Collections.Dictionary
                {
                    ["frames"] = (long)snapshot.RawCollisionFrames,
                    ["contacts"] = (long)snapshot.RawCollisionContacts,
                    ["rate"] = snapshot.RawCollisionRate
                }
            };

            if (snapshot.Mode == CombatMovementBenchmarkMode.Follow)
            {
                quality["follow"] = new Godot.Collections.Dictionary
                {
                    ["sample_frames"] = (long)snapshot.FollowSampleFrames,
                    ["inside_band_frames"] = (long)snapshot.FollowInsideBandFrames,
                    ["band_distance_px"] = snapshot.FollowBandDistance,
                    ["band_rate"] = snapshot.FollowBandRate,
                    ["average_error_px"] = snapshot.AverageFollowError,
                    ["p95_error_px"] = snapshot.P95FollowError,
                    ["max_error_px"] = snapshot.MaxFollowError,
                    ["catchup_events"] = snapshot.FollowCatchupEvents,
                    ["average_catchup_seconds"] = snapshot.AverageCatchupSeconds,
                    ["max_catchup_seconds"] = snapshot.MaxCatchupSeconds
                };
            }

            return new Godot.Collections.Dictionary
            {
                ["schema"] = "combat_movement_benchmark_v2",
                ["runtime_build"] = agent.RuntimeBuildId,
                ["actor"] = agent.ControlledCharacter?.CombatantId ?? "unknown",
                ["label"] = snapshot.Label,
                ["mode"] = snapshot.Mode.ToString(),
                ["running"] = snapshot.IsRunning,
                ["elapsed_seconds"] = snapshot.ElapsedSeconds,
                ["hard_gate"] = gate,
                ["score"] = snapshot.Score,
                ["thresholds"] = new Godot.Collections.Dictionary
                {
                    ["min_static_or_positioning_success_rate"] = snapshot.MinSuccessRate,
                    ["min_follow_band_rate"] = snapshot.MinFollowBandRate,
                    ["max_blocking_collision_rate"] = snapshot.MaxBlockingCollisionRate,
                    ["cpu_budget_usec"] = snapshot.CpuBudgetUsec
                },
                ["quality"] = quality,
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
                ["topology"] = agent.GetMovementTopologySummary(),
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
