using Godot;

namespace AshesofaDyingWorld.Core.Data
{
    /// <summary>
    /// Công thức sức mạnh dùng chung cho nhân vật có RaceData.
    /// Tách khỏi code runtime để có thể tune bằng YARD/Inspector mà không cần sửa C#.
    /// </summary>
    [GlobalClass]
    public partial class PowerBalanceData : Resource
    {
        [ExportGroup("Health")]
        [Export] public float BaseHP { get; set; } = 80f;
        [Export] public float HPPerVitality { get; set; } = 8f;
        [Export] public float HPPerStrength { get; set; } = 1f;

        [ExportGroup("Mana")]
        [Export] public float BaseMP { get; set; } = 30f;
        [Export] public float MPPerIntelligence { get; set; } = 4f;
        [Export] public float MPPerSpirit { get; set; } = 2f;
        [Export] public float BaseManaRegen { get; set; } = 0.5f;
        [Export] public float ManaRegenPerSpirit { get; set; } = 0.08f;
        [Export] public float ManaRegenDelay { get; set; } = 2f;

        [ExportGroup("Stamina")]
        [Export] public float BaseStamina { get; set; } = 60f;
        [Export] public float StaminaPerVitality { get; set; } = 3f;
        [Export] public float StaminaPerDexterity { get; set; } = 1f;
        [Export] public float BaseStaminaRegen { get; set; } = 12f;
        [Export] public float StaminaRegenPerDexterity { get; set; } = 0.5f;
        [Export] public float StaminaRegenPerVitality { get; set; } = 0.15f;
        [Export] public float StaminaRegenDelay { get; set; } = 0.35f;

        [ExportGroup("Guard and Poise")]
        [Export] public float BaseGuard { get; set; } = 35f;
        [Export] public float GuardPerDefense { get; set; } = 4f;
        [Export] public float GuardPerVitality { get; set; } = 1.5f;
        [Export] public float BasePoise { get; set; } = 20f;
        [Export] public float PoisePerVitality { get; set; } = 3f;
        [Export] public float PoisePerDefense { get; set; } = 1.5f;
        [Export] public float GuardRegenRate { get; set; } = 14f;
        [Export] public float PoiseRegenRate { get; set; } = 10f;
        [Export] public float GuardRegenDelay { get; set; } = 0.8f;
        [Export] public float PoiseRegenDelay { get; set; } = 1.1f;

        [ExportGroup("Offense")]
        [Export] public float PhysicalPowerPerStrength { get; set; } = 2f;
        [Export] public float MagicPowerPerIntelligence { get; set; } = 2f;
        [Export] public float MagicPowerPerSpirit { get; set; } = 0.5f;

        [ExportGroup("Defense")]
        [Export] public float ArmorPerDefense { get; set; } = 1.5f;
        [Export] public float MagicResistancePerSpirit { get; set; } = 0.8f;
        [Export] public float MagicResistancePerDefense { get; set; } = 0.4f;
        [Export] public float MitigationCurveConstant { get; set; } = 100f;

        [ExportGroup("Attack Speed")]
        [Export] public float BaseAttackSpeed { get; set; } = 0.9f;
        [Export] public float AttackSpeedPerDexterity { get; set; } = 0.0125f;
        [Export] public float MinimumWeaponWeightForSpeed { get; set; } = 0.7f;
        [Export] public float MinimumAttackSpeed { get; set; } = 0.6f;
        [Export] public float MaximumAttackSpeed { get; set; } = 2.2f;

        [ExportGroup("Level Progression")]
        [Export(PropertyHint.Range, "1,100000,1")] public int BaseExperienceToNextLevel { get; set; } = 100;
        [Export(PropertyHint.Range, "1,2,0.01")] public float ExperienceGrowthMultiplier { get; set; } = 1.12f;
        [Export(PropertyHint.Range, "1,100,1")] public int ExperienceRoundingStep { get; set; } = 5;

        public int CalculateExperienceToNextLevel(int currentLevel)
        {
            int safeLevel = Mathf.Max(1, currentLevel);
            float safeGrowth = Mathf.Max(1f, ExperienceGrowthMultiplier);
            float raw = Mathf.Max(1f, BaseExperienceToNextLevel)
                * Mathf.Pow(safeGrowth, safeLevel - 1);
            int step = Mathf.Max(1, ExperienceRoundingStep);
            return Mathf.Max(1, Mathf.RoundToInt(raw / step) * step);
        }

        public float CalculateMaxHP(int vitality, int strength)
        {
            return Mathf.Max(1f, BaseHP + vitality * HPPerVitality + strength * HPPerStrength);
        }

        public float CalculateMaxMP(int intelligence, int spirit)
        {
            return Mathf.Max(0f, BaseMP + intelligence * MPPerIntelligence + spirit * MPPerSpirit);
        }

        public float CalculateMaxStamina(int vitality, int dexterity)
        {
            return Mathf.Max(0f, BaseStamina + vitality * StaminaPerVitality + dexterity * StaminaPerDexterity);
        }

        public float CalculateMaxGuard(int defense, int vitality)
        {
            return Mathf.Max(0f, BaseGuard + defense * GuardPerDefense + vitality * GuardPerVitality);
        }

        public float CalculateMaxPoise(int vitality, int defense)
        {
            return Mathf.Max(0f, BasePoise + vitality * PoisePerVitality + defense * PoisePerDefense);
        }

        public float CalculatePhysicalPower(float weaponDamage, int strength)
        {
            return Mathf.Max(0f, weaponDamage + strength * PhysicalPowerPerStrength);
        }

        public float CalculateMagicPower(int intelligence, int spirit)
        {
            return Mathf.Max(0f, intelligence * MagicPowerPerIntelligence + spirit * MagicPowerPerSpirit);
        }

        public float CalculateArmor(float equipmentArmor, int defense)
        {
            return Mathf.Max(0f, equipmentArmor + defense * ArmorPerDefense);
        }

        public float CalculateMagicResistance(int spirit, int defense)
        {
            return Mathf.Max(0f,
                spirit * MagicResistancePerSpirit
                + defense * MagicResistancePerDefense);
        }

        public float CalculateManaRegen(int spirit)
        {
            return Mathf.Max(0f, BaseManaRegen + spirit * ManaRegenPerSpirit);
        }

        public float CalculateStaminaRegen(int dexterity, int vitality)
        {
            return Mathf.Max(0f,
                BaseStaminaRegen
                + dexterity * StaminaRegenPerDexterity
                + vitality * StaminaRegenPerVitality);
        }

        public float CalculateAttackSpeed(int dexterity, float weaponWeight)
        {
            float dexterityFactor = BaseAttackSpeed + dexterity * AttackSpeedPerDexterity;
            float safeWeight = Mathf.Max(MinimumWeaponWeightForSpeed, weaponWeight);
            float weightFactor = 1f / Mathf.Sqrt(safeWeight);
            return Mathf.Clamp(
                dexterityFactor * weightFactor,
                MinimumAttackSpeed,
                MaximumAttackSpeed);
        }
    }
}
