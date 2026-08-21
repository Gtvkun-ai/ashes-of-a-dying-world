using Godot;

namespace AshesofaDyingWorld.Core.Data
{
    /// <summary>
    /// Stat profile trực tiếp cho enemy/NPC không dùng RaceData progression.
    /// Dùng Resource để HP, power, regen của quái có thể tune trong YARD thay vì nằm trong scene.
    /// </summary>
    [GlobalClass]
    public partial class CombatStatProfileData : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string ProfileId { get; set; } = "combatant";

        [ExportGroup("Progression Reward")]
        [Export(PropertyHint.Range, "0,100000,1")] public int ExperienceReward { get; set; } = 0;

        [ExportGroup("Attributes")]
        [Export] public int Strength { get; set; } = 0;
        [Export] public int Dexterity { get; set; } = 0;
        [Export] public int Intelligence { get; set; } = 0;
        [Export] public int Vitality { get; set; } = 0;
        [Export] public int Spirit { get; set; } = 0;
        [Export] public int Defense { get; set; } = 0;

        [ExportGroup("Resources")]
        [Export] public float MaxHP { get; set; } = 100f;
        [Export] public float MaxMP { get; set; } = 0f;
        [Export] public float MaxStamina { get; set; } = 0f;
        [Export] public float MaxGuard { get; set; } = 0f;
        [Export] public float MaxPoise { get; set; } = 30f;

        [ExportGroup("Power")]
        [Export] public float PhysicalPower { get; set; } = 10f;
        [Export] public float MagicPower { get; set; } = 0f;
        [Export] public float Armor { get; set; } = 0f;
        [Export] public float MagicResistance { get; set; } = 0f;
        [Export] public float AttackSpeed { get; set; } = 1f;

        [ExportGroup("Regeneration")]
        [Export] public float ManaRegenRate { get; set; } = 0f;
        [Export] public float StaminaRegenRate { get; set; } = 0f;
        [Export] public float GuardRegenRate { get; set; } = 0f;
        [Export] public float PoiseRegenRate { get; set; } = 8f;
        [Export] public float ManaRegenDelay { get; set; } = 2f;
        [Export] public float StaminaRegenDelay { get; set; } = 0.35f;
        [Export] public float GuardRegenDelay { get; set; } = 0.8f;
        [Export] public float PoiseRegenDelay { get; set; } = 1.1f;

        public int GetAttribute(AttributeType type)
        {
            return type switch
            {
                AttributeType.Strength => Strength,
                AttributeType.Dexterity => Dexterity,
                AttributeType.Intelligence => Intelligence,
                AttributeType.Vitality => Vitality,
                AttributeType.Spirit => Spirit,
                AttributeType.Defense => Defense,
                _ => 0
            };
        }
    }
}
