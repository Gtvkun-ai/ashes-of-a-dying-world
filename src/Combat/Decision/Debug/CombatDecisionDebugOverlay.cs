using Godot;
using System.Text;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Debug
{
    /// <summary>
    /// Overlay QA nhẹ, tự dựng UI. F6 panel, F7 anchor/range,
    /// F8 movement slots, F11 dump JSON.
    /// </summary>
    public partial class CombatDecisionDebugOverlay : CanvasLayer
    {
        [Export] public NodePath AgentPath { get; set; } = new NodePath("../CombatDecisionAgent");
        [Export] public bool Enabled { get; set; } = true;
        [Export] public bool VisibleByDefault { get; set; } = false;
        [Export] public float RefreshSeconds { get; set; } = 0.15f;
        [Export] public int MaxCandidates { get; set; } = 6;

        private CombatDecisionAgent _agent;
        private PanelContainer _panel;
        private Label _label;
        private CombatDecisionWorldDebugDraw _worldDraw;
        private float _refreshRemaining;
        private bool _showPanel;
        private bool _showAnchor;
        private bool _showSlots;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            _agent = GetNodeOrNull<CombatDecisionAgent>(AgentPath);
            if (_agent == null)
            {
                GD.PushWarning("[DecisionOverlay] Không tìm thấy CombatDecisionAgent.");
                return;
            }

            BuildPanel();
            _showPanel = VisibleByDefault;
            _panel.Visible = _showPanel;

            _worldDraw = new CombatDecisionWorldDebugDraw
            {
                Name = $"DecisionWorldDebug_{_agent.ControlledCharacter?.CombatantId ?? "actor"}",
                Agent = _agent
            };
            Node world = GetTree().CurrentScene ?? GetParent();
            world?.AddChild(_worldDraw);
            UpdateWorldFlags();
        }

        public override void _Process(double delta)
        {
            if (!Enabled || _agent == null)
            {
                return;
            }

            _refreshRemaining -= Mathf.Max(0f, (float)delta);
            if (_refreshRemaining <= 0f)
            {
                _refreshRemaining = Mathf.Max(0.05f, RefreshSeconds);
                if (_showPanel)
                {
                    _label.Text = BuildText();
                }
            }

            _worldDraw?.QueueRedraw();
        }

        public override void _UnhandledInput(InputEvent inputEvent)
        {
            if (!Enabled
                || inputEvent is not InputEventKey key
                || !key.Pressed
                || key.Echo)
            {
                return;
            }

            switch (key.Keycode)
            {
                case Key.F6:
                    _showPanel = !_showPanel;
                    if (_panel != null)
                    {
                        _panel.Visible = _showPanel;
                    }
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F7:
                    _showAnchor = !_showAnchor;
                    UpdateWorldFlags();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F8:
                    _showSlots = !_showSlots;
                    UpdateWorldFlags();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F11:
                    DecisionTraceExporter.ExportLatest(_agent);
                    GetViewport().SetInputAsHandled();
                    break;
            }
        }

        public override void _ExitTree()
        {
            if (_worldDraw != null && GodotObject.IsInstanceValid(_worldDraw))
            {
                _worldDraw.QueueFree();
            }
        }

        private void BuildPanel()
        {
            _panel = new PanelContainer
            {
                Name = "DecisionPanel",
                OffsetLeft = 16f,
                OffsetTop = 16f,
                OffsetRight = 660f,
                OffsetBottom = 430f,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(_panel);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_top", 10);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_bottom", 10);
            _panel.AddChild(margin);

            _label = new Label
            {
                Text = "Decision trace chưa có dữ liệu.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(610f, 380f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _label.AddThemeFontSizeOverride("font_size", 14);
            margin.AddChild(_label);
        }

        private string BuildText()
        {
            if (_agent.LastTrace == null)
            {
                return "COMBAT DECISION CORE\nĐang chờ decision trace...\n\nF6 panel  F7 anchor  F8 slots  F11 dump JSON";
            }

            DecisionTrace trace = _agent.LastTrace;
            CombatSnapshot snapshot = trace.Snapshot;
            var builder = new StringBuilder();
            builder.Append("HYOU / ").Append((_agent.ClassProfile?.ClassId ?? "UNASSIGNED").ToUpperInvariant()).AppendLine();
            builder.Append("Mode: ").Append(_agent.ShadowMode ? "shadow" : "live")
                .Append("  State: ").Append(snapshot.SelfState)
                .Append("  Target: ").Append(snapshot.TargetId?.ToString() ?? "none").AppendLine();
            builder.Append("Distance: ").Append(snapshot.TargetDistance.ToString("0.0"))
                .Append("  LOS: ").Append(snapshot.HasLineOfSight ? "yes" : "no")
                .Append("  Threat: ").Append(snapshot.ThreatSeverity.ToString("0.00"))
                .Append("  ETA: ").Append(snapshot.ThreatEtaSeconds.ToString("0.00")).AppendLine();
            builder.Append("MP: ").Append(snapshot.Mana)
                .Append("  Stamina: ").Append(snapshot.Stamina)
                .Append("  Guard: ").Append(snapshot.Guard).AppendLine();
            builder.Append("Proposed: ").Append(_agent.LastScheduledDecision.ProposedIntent)
                .Append(" (").Append(_agent.LastScheduledDecision.ProposedScore.ToString("0.00")).Append(")").AppendLine();
            builder.Append("Committed: ").Append(_agent.LastScheduledDecision.CommittedIntent)
                .Append(" (").Append(_agent.LastScheduledDecision.CommittedScore.ToString("0.00"))
                .Append(") lock=").Append(_agent.LastScheduledDecision.CommitmentRemaining.ToString("0.00"))
                .Append(" reason=").Append(_agent.LastScheduledDecision.ReasonKey).AppendLine();
            builder.Append("Move: ").Append(_agent.LastMovementCommand.Direction)
                .Append(" slot=").Append(_agent.LastMovementCommand.DirectionSlot)
                .Append(" score=").Append(_agent.LastMovementCommand.Score.ToString("0.00"))
                .Append(" run=").Append(_agent.LastMovementCommand.WantsRun ? "yes" : "no").AppendLine();
            builder.AppendLine("Candidates:");

            int written = 0;
            for (int index = 0; index < trace.Candidates.Count && written < Mathf.Max(1, MaxCandidates); index++)
            {
                CandidateTrace candidate = trace.Candidates[index];
                builder.Append(candidate.Feasible ? "  " : "  X ")
                    .Append(candidate.FinalScore.ToString("0.00")).Append(' ')
                    .Append(candidate.Intent.Type);
                if (!string.IsNullOrWhiteSpace(candidate.Intent.ActionId.ToString()))
                {
                    builder.Append('(').Append(candidate.Intent.ActionId).Append(')');
                }
                if (!candidate.Feasible)
                {
                    builder.Append("  ").Append(candidate.FailureReason);
                }
                builder.AppendLine();
                written++;
            }

            builder.AppendLine();
            builder.Append("F6 panel  F7 anchor[").Append(_showAnchor ? "on" : "off")
                .Append("]  F8 slots[").Append(_showSlots ? "on" : "off")
                .Append("]  F11 dump JSON");
            return builder.ToString();
        }

        private void UpdateWorldFlags()
        {
            if (_worldDraw == null)
            {
                return;
            }
            _worldDraw.ShowAnchor = _showAnchor;
            _worldDraw.ShowSlots = _showSlots;
            _worldDraw.QueueRedraw();
        }

    }
}
