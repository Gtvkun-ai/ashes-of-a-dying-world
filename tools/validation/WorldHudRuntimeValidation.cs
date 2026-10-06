using System;
using Godot;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.UI.HUD;

namespace AshesofaDyingWorld.Validation
{
    /// <summary>
    /// Runtime regression scene for the world HUD stack. It deliberately uses the
    /// production services so layout assertions include their measured controls.
    /// </summary>
    public partial class WorldHudRuntimeValidation : Node2D
    {
        private static readonly Vector2I[] TargetViewports =
        {
            new(1600, 900),
            new(1280, 720)
        };

        private sealed partial class ValidationCombatCharacter : CombatCharacter
        {
        }

        public override async void _Ready()
        {
            try
            {
                foreach (Vector2I viewportSize in TargetViewports)
                {
                    await ValidateViewport(viewportSize);
                }

                SetMeta("validation_passed", true);
                GD.Print("World HUD runtime validation passed at 1600x900 and 1280x720.");
                GetTree().Quit();
            }
            catch (Exception exception)
            {
                GD.PushError($"World HUD runtime validation failed: {exception.Message}");
                GetTree().Quit(1);
            }
        }

        private async System.Threading.Tasks.Task ValidateViewport(Vector2I viewportSize)
        {
            GetWindow().Size = viewportSize;
            await NextFrame();

            PlayerManager.Instance?.ResetParty();
            var camera = new Camera2D
            {
                Name = "WorldHudValidationCamera",
                Position = new Vector2(viewportSize.X * 0.5f, viewportSize.Y * 0.5f)
            };
            AddChild(camera);
            camera.MakeCurrent();

            var source = new ValidationCombatCharacter
            {
                Name = "WorldHudValidationSource",
                Position = new Vector2(viewportSize.X * 0.42f, viewportSize.Y * 0.64f)
            };
            var target = new ValidationCombatCharacter
            {
                Name = "WorldHudValidationTarget",
                Position = new Vector2(viewportSize.X * 0.58f, viewportSize.Y * 0.64f)
            };
            var stats = new PlayerStats
            {
                Name = "PlayerStats",
                ConfigData = GD.Load<CharacterConfig>("res://data/characters/main.tres"),
                UseManualProfile = true,
                ManualMaxHP = 100f,
                ManualMaxStamina = 100f
            };
            target.AddChild(stats);
            AddChild(source);
            AddChild(target);
            await NextFrame();
            await NextFrame();

            target.Statuses?.Apply(new HitProfileData
            {
                SlowPercent = 45f,
                SlowSeconds = 8f,
                ChillStacks = 2,
                ChillSeconds = 8f,
                FreezeAtChillStacks = 99
            });

            EnemyHealthBarService health = EnemyHealthBarService.GetOrCreate(GetTree());
            CompanionTargetIndicatorService targetIndicator = CompanionTargetIndicatorService.GetOrCreate(GetTree());
            DamageNumberService damage = DamageNumberService.GetOrCreate(GetTree());
            FloatingProgressionHudService progression = FloatingProgressionHudService.GetOrCreate(GetTree());
            Require(health != null && targetIndicator != null && damage != null && progression != null,
                "World HUD services could not be created.");
            Require(stats.ConfigData != null, "Could not load the progression validation character data.");

            await NextFrame();

            health.RegisterEnemy(target, () => 62f, () => 100f, () => 9);
            health.NotifyDamaged(target);
            targetIndicator.SetTarget(source, target, clearShot: true);
            stats.GainExperience(10);
            await NextFrame();
            await NextFrame();

            Control healthWidget = health.GetNodeOrNull<Control>("EnemyHealthWidget");
            TextureRect targetMarker = FindDescendant<TextureRect>(targetIndicator);
            PanelContainer progressionWidget = FindDescendant<PanelContainer>(progression);
            Require(healthWidget != null, "EnemyHealthBarService did not create its health widget.");
            Require(targetMarker != null, "CompanionTargetIndicatorService did not create its target marker.");
            Require(progressionWidget != null && progressionWidget.Visible,
                "FloatingProgressionHudService did not show progression feedback.");

            Rect2 targetRect = VisualRect(targetMarker);
            Rect2 healthRect = VisualRect(healthWidget);
            Rect2 progressionRect = VisualRect(progressionWidget);
            AssertNoOverlap(
                targetRect,
                healthRect,
                progressionRect,
                "baseline world HUD lanes");
            AssertInsideBand(target, WorldHudLane.Target, targetRect, "target marker");
            AssertInsideBand(target, WorldHudLane.Health, healthRect, "health and status row");
            AssertInsideBand(target, WorldHudLane.Progression, progressionRect, "progression panel");

            await AssertDamageLifetime(target, damage, targetRect, healthRect, progressionRect, shattered: false);
            await AssertDamageLifetime(target, damage, targetRect, healthRect, progressionRect, shattered: true);

            targetIndicator.ClearTarget(source);
            health.UnregisterEnemy(target);
            source.QueueFree();
            target.QueueFree();
            camera.QueueFree();
            await NextFrame();
            await NextFrame();
        }

        private async System.Threading.Tasks.Task AssertDamageLifetime(
            Node2D target,
            DamageNumberService damage,
            Rect2 targetRect,
            Rect2 healthRect,
            Rect2 progressionRect,
            bool shattered)
        {
            damage.ShowDamage(target, 138f, shattered: shattered, ice: false, blocked: false);
            await NextFrame();

            int sampleCount = Mathf.CeilToInt(damage.Lifetime * (shattered ? 1.15f : 1f) * 60f) + 3;
            for (int sample = 0; sample < sampleCount; sample++)
            {
                Label damageLabel = FindDescendant<Label>(damage);
                if (damageLabel == null)
                {
                    break;
                }

                Rect2 feedback = VisualRect(damageLabel);
                AssertInsideBand(target, WorldHudLane.Feedback, feedback,
                    shattered ? "shattered damage" : "normal damage");
                AssertNoOverlap(
                    targetRect,
                    healthRect,
                    feedback,
                    progressionRect,
                    shattered ? "shattered damage lifetime" : "normal damage lifetime");
                await NextDamageSample();
            }

            Require(FindDescendant<Label>(damage) == null,
                shattered ? "Shattered damage did not finish its Lifetime." : "Normal damage did not finish its Lifetime.");
        }

        private static void AssertNoOverlap(Rect2 target, Rect2 health, Rect2 progression, string scenario)
        {
            Require(target.End.Y + WorldHudLayout.LaneSpacing <= health.Position.Y + 0.1f,
                $"{scenario}: target overlaps health.");
            Require(health.End.Y + WorldHudLayout.LaneSpacing <= progression.Position.Y + 0.1f,
                $"{scenario}: health overlaps progression.");
        }

        private static void AssertNoOverlap(Rect2 target, Rect2 health, Rect2 feedback, Rect2 progression, string scenario)
        {
            Require(target.End.Y + WorldHudLayout.LaneSpacing <= health.Position.Y + 0.1f,
                $"{scenario}: target overlaps health.");
            Require(health.End.Y + WorldHudLayout.LaneSpacing <= feedback.Position.Y + 0.1f,
                $"{scenario}: health overlaps feedback: health={health}, feedback={feedback}.");
            Require(feedback.End.Y + WorldHudLayout.LaneSpacing <= progression.Position.Y + 0.1f,
                $"{scenario}: feedback overlaps progression.");
        }

        private static void AssertInsideBand(Node2D actor, WorldHudLane lane, Rect2 rect, string name)
        {
            Rect2 band = WorldHudLayout.GetReservedBand(actor, lane, rect.Size.X);
            Require(rect.Position.Y >= band.Position.Y - 0.1f && rect.End.Y <= band.End.Y + 0.1f,
                $"{name} escaped the {lane} reserved band: rect={rect}, band={band}.");
        }

        private static Rect2 VisualRect(Control control)
        {
            Vector2 minimum = control.GetCombinedMinimumSize();
            Vector2 unscaledSize = new(
                Mathf.Max(control.Size.X, minimum.X),
                Mathf.Max(control.Size.Y, minimum.Y));
            Vector2 scale = control.Scale.Abs();
            return new Rect2(control.GetGlobalTransformWithCanvas().Origin, unscaledSize * scale);
        }

        private static T FindDescendant<T>(Node parent) where T : Node
        {
            foreach (Node child in parent.GetChildren())
            {
                if (child is T match)
                {
                    return match;
                }

                T descendant = FindDescendant<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }
            return null;
        }

        private async System.Threading.Tasks.Task NextFrame()
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        private async System.Threading.Tasks.Task NextDamageSample()
        {
            SceneTreeTimer timer = GetTree().CreateTimer(1f / 60f);
            await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
