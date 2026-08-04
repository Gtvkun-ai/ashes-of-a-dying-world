using Godot;
using Godot.Collections;

namespace AshesofaDyingWorld.Combat.Data
{
    [GlobalClass]
    public partial class WeaponMovesetData : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string MovesetId { get; set; } = "unarmed";

        [ExportGroup("Actions")]
        [Export] public Array<CombatActionData> LightCombo { get; set; } = new();

        [ExportGroup("Guard")]
        [Export] public string GuardAnimationTemplate { get; set; } = "block_{dir}";
        [Export(PropertyHint.Range, "0,0.95,0.01")]
        public float GuardDamageReduction { get; set; } = 0.35f;
        [Export] public float GuardArcDegrees { get; set; } = 150f;
        [Export] public float GuardMoveSpeedMultiplier { get; set; } = 0.35f;
        [Export] public float GuardStaminaPerDamage { get; set; } = 0.5f;
        [Export] public float GuardRecoveryDelay { get; set; } = 0.8f;

        public string ResolveGuardAnimation(string direction)
        {
            string safeDirection = string.IsNullOrWhiteSpace(direction) ? "down" : direction;
            return (GuardAnimationTemplate ?? string.Empty).Replace("{dir}", safeDirection);
        }

        public CombatActionData GetLightAction(int index)
        {
            if (LightCombo == null || LightCombo.Count == 0)
            {
                return null;
            }

            int safeIndex = Mathf.Clamp(index, 0, LightCombo.Count - 1);
            return LightCombo[safeIndex];
        }
    }
}
