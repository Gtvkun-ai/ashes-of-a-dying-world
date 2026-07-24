using Godot;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Debug
{
    /// <summary>Xuất trace debug ra JSON, không chạm save schema.</summary>
    public static class DecisionTraceExporter
    {
        public static string ExportLatest(CombatDecisionAgent agent, string destination = "")
        {
            if (agent?.LastTrace == null)
            {
                return string.Empty;
            }

            string actorId = agent.ControlledCharacter?.CombatantId ?? "actor";
            string safeActor = actorId.Replace("/", "_").Replace("\\", "_");
            string path = string.IsNullOrWhiteSpace(destination)
                ? $"user://combat_trace_{safeActor}_{(long)Time.GetUnixTimeFromSystem()}.json"
                : destination;

            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"[DecisionTrace] Không mở được file: {path}");
                return string.Empty;
            }

            file.StoreString(Json.Stringify(BuildPayload(agent), "  "));
            string absolute = ProjectSettings.GlobalizePath(path);
            GD.Print($"[DecisionTrace] DUMP actor={actorId} path={absolute}");
            return absolute;
        }

        private static Godot.Collections.Dictionary BuildPayload(CombatDecisionAgent agent)
        {
            DecisionTrace trace = agent.LastTrace;
            CombatSnapshot snapshot = trace.Snapshot;
            var candidates = new Godot.Collections.Array();
            for (int index = 0; index < trace.Candidates.Count; index++)
            {
                CandidateTrace candidate = trace.Candidates[index];
                var factors = new Godot.Collections.Dictionary();
                foreach (var pair in candidate.Factors)
                {
                    factors[pair.Key] = pair.Value;
                }

                candidates.Add(new Godot.Collections.Dictionary
                {
                    ["intent"] = candidate.Intent.Type.ToString(),
                    ["action_id"] = candidate.Intent.ActionId.ToString(),
                    ["feasible"] = candidate.Feasible,
                    ["score"] = candidate.FinalScore,
                    ["failure"] = candidate.FailureReason.ToString(),
                    ["tags"] = candidate.Tags.ToString(),
                    ["factors"] = factors
                });
            }

            return new Godot.Collections.Dictionary
            {
                ["schema"] = "combat_decision_trace_v1",
                ["actor"] = agent.ControlledCharacter?.CombatantId ?? "unknown",
                ["mode"] = agent.ShadowMode ? "shadow" : "live",
                ["class"] = agent.ClassProfile?.ClassId ?? "unassigned",
                ["scheduler"] = new Godot.Collections.Dictionary
                {
                    ["proposed"] = agent.LastScheduledDecision.ProposedIntent.Type.ToString(),
                    ["committed"] = agent.LastScheduledDecision.CommittedIntent.Type.ToString(),
                    ["proposed_score"] = agent.LastScheduledDecision.ProposedScore,
                    ["committed_score"] = agent.LastScheduledDecision.CommittedScore,
                    ["reason"] = agent.LastScheduledDecision.ReasonKey.ToString(),
                    ["lock_remaining"] = agent.LastScheduledDecision.CommitmentRemaining
                },
                ["snapshot"] = new Godot.Collections.Dictionary
                {
                    ["time"] = snapshot.TimeSeconds,
                    ["self_state"] = snapshot.SelfState.ToString(),
                    ["self_position"] = Vector(snapshot.SelfPosition),
                    ["hp_ratio"] = snapshot.HealthRatio,
                    ["mana_ratio"] = snapshot.ManaRatio,
                    ["stamina_ratio"] = snapshot.StaminaRatio,
                    ["guard_ratio"] = snapshot.GuardRatio,
                    ["target_id"] = snapshot.TargetId?.ToString() ?? "",
                    ["target_position"] = Vector(snapshot.TargetPosition),
                    ["distance"] = snapshot.TargetDistance,
                    ["line_of_sight"] = snapshot.HasLineOfSight,
                    ["threat_eta"] = snapshot.ThreatEtaSeconds,
                    ["threat_severity"] = snapshot.ThreatSeverity,
                    ["threat_blockable"] = snapshot.ThreatBlockable,
                    ["threat_dodgeable"] = snapshot.ThreatDodgeable,
                    ["leader_threatened"] = snapshot.LeaderThreatened,
                    ["safe_retreat"] = snapshot.HasSafeRetreatVector
                },
                ["movement"] = new Godot.Collections.Dictionary
                {
                    ["direction"] = Vector(agent.LastMovementCommand.Direction),
                    ["slot"] = agent.LastMovementCommand.DirectionSlot,
                    ["score"] = agent.LastMovementCommand.Score,
                    ["run"] = agent.LastMovementCommand.WantsRun,
                    ["anchor"] = Vector(agent.Blackboard.CurrentAnchor)
                },
                ["chosen_intent"] = trace.ChosenIntent.Type.ToString(),
                ["summary"] = trace.Summary,
                ["candidates"] = candidates
            };
        }

        private static Godot.Collections.Array Vector(Vector2 value)
        {
            return new Godot.Collections.Array { value.X, value.Y };
        }
    }
}
