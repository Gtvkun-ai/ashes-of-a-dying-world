using Godot;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Combat.Model;
using System.Collections.Generic;

namespace AshesofaDyingWorld.Entities.Player
{
    /// <summary>
    /// Nguồn dữ liệu chỉ số duy nhất cho mọi combatant.
    /// Player, đồng đội và quái đều dùng chung component này; combat không còn tự giữ HP riêng.
    /// </summary>
    public partial class PlayerStats : Node
    {
        [Signal] public delegate void StatsChangedEventHandler();
        [Signal] public delegate void DefeatedEventHandler();
        [Signal] public delegate void LevelChangedEventHandler(int newLevel);

        [ExportGroup("Identity and Progression")]
        [Export] public CharacterConfig ConfigData { get; set; }
        [Export] public EquipmentManager EquipmentMgr { get; set; }
        [Export] public bool AutoRegisterWithPlayerManager { get; set; } = true;
        [Export(PropertyHint.Range, "1,99,1")] public int InitialLevel { get; set; } = 1;

        [ExportGroup("Manual Profile")]
        [Export] public bool UseManualProfile { get; set; } = false;
        [Export] public CombatStatProfileData ManualProfileData { get; set; }
        // Các field dưới đây là fallback cho scene/resource cũ chưa chuyển sang CombatStatProfileData.
        [Export] public float ManualMaxHP { get; set; } = 100f;
        [Export] public float ManualMaxMP { get; set; } = 0f;
        [Export] public float ManualMaxStamina { get; set; } = 100f;
        [Export] public float ManualMaxGuard { get; set; } = 60f;
        [Export] public float ManualMaxPoise { get; set; } = 40f;
        [Export] public float ManualAttackPower { get; set; } = 10f;
        [Export] public float ManualMagicPower { get; set; } = 0f;
        [Export] public float ManualArmor { get; set; } = 0f;
        [Export] public float ManualMagicResistance { get; set; } = 0f;
        [Export] public float ManualAttackSpeed { get; set; } = 1f;

        [ExportGroup("Regeneration")]
        [Export] public float ManaRegenRate { get; set; } = 0f;
        [Export] public float StaminaRegenRate { get; set; } = 0f;
        [Export] public float GuardRegenRate { get; set; } = 16f;
        [Export] public float PoiseRegenRate { get; set; } = 12f;
        [Export] public float ManaRegenDelay { get; set; } = 2f;
        [Export] public float StaminaRegenDelay { get; set; } = 0.35f;
        [Export] public float GuardRegenDelay { get; set; } = 0.8f;
        [Export] public float PoiseRegenDelay { get; set; } = 1.1f;

        public int CurrentLevel { get; private set; } = 1;
        public int CurrentExperience { get; private set; } = 0;
        public bool IsAtMaxLevel => CurrentLevel >= Mathf.Max(1, ConfigData?.MaxLevel ?? 99);
        public int ExperienceToNextLevel => IsAtMaxLevel ? 0 : GetExperienceRequiredForNextLevel(CurrentLevel);
        public int ExperienceRemaining => IsAtMaxLevel
            ? 0
            : Mathf.Max(0, ExperienceToNextLevel - CurrentExperience);
        public float ExperienceProgress => IsAtMaxLevel || ExperienceToNextLevel <= 0
            ? 1f
            : Mathf.Clamp((float)CurrentExperience / ExperienceToNextLevel, 0f, 1f);

        public float CurrentHP { get; private set; }
        public float CurrentMP { get; private set; }
        public float CurrentStamina { get; private set; }
        public float CurrentGuard { get; private set; }
        public float CurrentPoise { get; private set; }

        public float MaxHP { get; private set; }
        public float MaxMP { get; private set; }
        public float MaxStamina { get; private set; }
        public float MaxGuard { get; private set; }
        public float MaxPoise { get; private set; }

        public Dictionary<AttributeType, int> FinalAttributes { get; private set; } = new();
        // AttackDamage được giữ làm alias tương thích cho PhysicalPower.
        public float AttackDamage { get; private set; }
        public float PhysicalPower { get; private set; }
        public float MagicPower { get; private set; }
        public float PrimaryPower => Mathf.Max(PhysicalPower, MagicPower);
        public float Armor { get; private set; }
        public float MagicResistance { get; private set; }
        public float AttackSpeed { get; private set; } = 1f;
        public float MitigationCurveConstant { get; private set; } = 100f;

        // Modifier được tách theo nguồn để skill/status không giẫm lên nhau.
        // Bản cũ chỉ có một giá trị mỗi attribute nên buff A tắt là tiện tay xóa luôn buff B.
        private readonly Dictionary<string, Dictionary<AttributeType, int>> _temporaryAttributeBonuses = new();
        private bool _resourcesInitialized;
        private bool _defeatSignalSent;
        private float _manaRegenDelayRemaining;
        private float _staminaRegenDelayRemaining;
        private float _guardRegenDelayRemaining;
        private float _poiseRegenDelayRemaining;

        public override void _EnterTree()
        {
            RegisterWithPlayerManager();
        }

        public override void _Ready()
        {
            CurrentLevel = Mathf.Max(1, InitialLevel);
            RecalculateStats();
            FillAllResources();
            _resourcesInitialized = true;
            RegisterWithPlayerManager();
            EmitSignal(SignalName.StatsChanged);
        }

        public override void _ExitTree()
        {
            PlayerManager.Instance?.UnregisterMember(this);
        }


        /// <summary>
        /// Trừ mana theo cùng contract với stamina và đồng thời khởi động regen delay.
        /// Trước đây AbilityRunner gọi ChangeMP(-cost), khiến mana không có policy hồi nhất quán.
        /// </summary>
        public bool ConsumeMana(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (amount <= 0f)
            {
                return true;
            }

            if (MaxMP <= 0f || CurrentMP + 0.001f < amount)
            {
                return false;
            }

            _manaRegenDelayRemaining = ManaRegenDelay;
            CurrentMP = Mathf.Max(0f, CurrentMP - amount);
            EmitSignal(SignalName.StatsChanged);
            return true;
        }

        public bool ConsumeStamina(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (amount <= 0f)
            {
                return true;
            }

            if (CurrentStamina + 0.001f < amount)
            {
                return false;
            }

            _staminaRegenDelayRemaining = StaminaRegenDelay;
            CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
            EmitSignal(SignalName.StatsChanged);
            return true;
        }

        public bool ConsumeGuard(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (amount <= 0f || MaxGuard <= 0f)
            {
                return MaxGuard > 0f;
            }

            _guardRegenDelayRemaining = GuardRegenDelay;
            bool enough = CurrentGuard + 0.001f >= amount;
            CurrentGuard = Mathf.Max(0f, CurrentGuard - amount);
            EmitSignal(SignalName.StatsChanged);
            return enough;
        }

        public bool ConsumePoise(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (amount <= 0f || MaxPoise <= 0f)
            {
                return MaxPoise > 0f;
            }

            _poiseRegenDelayRemaining = PoiseRegenDelay;
            bool enough = CurrentPoise + 0.001f >= amount;
            CurrentPoise = Mathf.Max(0f, CurrentPoise - amount);
            EmitSignal(SignalName.StatsChanged);
            return enough;
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || CurrentHP <= 0f)
            {
                return;
            }

            CurrentHP = Mathf.Max(0f, CurrentHP - amount);
            EmitSignal(SignalName.StatsChanged);
            if (CurrentHP <= 0f && !_defeatSignalSent)
            {
                _defeatSignalSent = true;
                EmitSignal(SignalName.Defeated);
            }
        }

        public void ChangeHP(float amount)
        {
            float previous = CurrentHP;
            CurrentHP = Mathf.Clamp(CurrentHP + amount, 0f, MaxHP);
            if (CurrentHP > 0f)
            {
                _defeatSignalSent = false;
            }

            EmitSignal(SignalName.StatsChanged);
            if (previous > 0f && CurrentHP <= 0f && !_defeatSignalSent)
            {
                _defeatSignalSent = true;
                EmitSignal(SignalName.Defeated);
            }
        }

        public void ChangeMP(float amount)
        {
            if (amount < 0f)
            {
                _manaRegenDelayRemaining = ManaRegenDelay;
            }

            CurrentMP = Mathf.Clamp(CurrentMP + amount, 0f, MaxMP);
            EmitSignal(SignalName.StatsChanged);
        }

        public void ChangeStamina(float amount)
        {
            if (amount < 0f)
            {
                _staminaRegenDelayRemaining = StaminaRegenDelay;
            }

            CurrentStamina = Mathf.Clamp(CurrentStamina + amount, 0f, MaxStamina);
            EmitSignal(SignalName.StatsChanged);
        }

        public void ChangeGuard(float amount)
        {
            CurrentGuard = Mathf.Clamp(CurrentGuard + amount, 0f, MaxGuard);
            EmitSignal(SignalName.StatsChanged);
        }

        public void ChangePoise(float amount)
        {
            CurrentPoise = Mathf.Clamp(CurrentPoise + amount, 0f, MaxPoise);
            EmitSignal(SignalName.StatsChanged);
        }

        // Overload cũ được giữ để code ngoài repo không gãy trong một lần rollout.
        public void UpdateRegeneration(float delta, bool allowStamina, bool allowGuard, bool allowPoise)
        {
            UpdateRegeneration(delta, allowStamina, allowGuard, allowPoise, allowMana: false);
        }

        public void UpdateRegeneration(
            float delta,
            bool allowStamina,
            bool allowGuard,
            bool allowPoise,
            bool allowMana)
        {
            float dt = Mathf.Max(0f, delta);
            _manaRegenDelayRemaining = Mathf.Max(0f, _manaRegenDelayRemaining - dt);
            _staminaRegenDelayRemaining = Mathf.Max(0f, _staminaRegenDelayRemaining - dt);
            _guardRegenDelayRemaining = Mathf.Max(0f, _guardRegenDelayRemaining - dt);
            _poiseRegenDelayRemaining = Mathf.Max(0f, _poiseRegenDelayRemaining - dt);

            bool changed = false;
            if (allowMana
                && MaxMP > 0f
                && _manaRegenDelayRemaining <= 0f
                && CurrentMP < MaxMP)
            {
                CurrentMP = Mathf.Min(MaxMP, CurrentMP + ManaRegenRate * dt);
                changed = true;
            }

            if (allowStamina && _staminaRegenDelayRemaining <= 0f && CurrentStamina < MaxStamina)
            {
                CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + StaminaRegenRate * dt);
                changed = true;
            }

            if (allowGuard && _guardRegenDelayRemaining <= 0f && CurrentGuard < MaxGuard)
            {
                CurrentGuard = Mathf.Min(MaxGuard, CurrentGuard + GuardRegenRate * dt);
                changed = true;
            }

            if (allowPoise && _poiseRegenDelayRemaining <= 0f && CurrentPoise < MaxPoise)
            {
                CurrentPoise = Mathf.Min(MaxPoise, CurrentPoise + PoiseRegenRate * dt);
                changed = true;
            }

            if (changed)
            {
                EmitSignal(SignalName.StatsChanged);
            }
        }

        public void SetCharacterConfig(CharacterConfig config)
        {
            if (config == null)
            {
                return;
            }

            ConfigData = config;
            UseManualProfile = false;
            RecalculateStats();
        }

        public int GetExperienceRequiredForNextLevel(int level)
        {
            PowerBalanceData balance = ConfigData?.BalanceProfile;
            if (balance != null)
            {
                return balance.CalculateExperienceToNextLevel(level);
            }

            int safeLevel = Mathf.Max(1, level);
            float raw = 100f * Mathf.Pow(1.12f, safeLevel - 1);
            return Mathf.Max(1, Mathf.RoundToInt(raw / 5f) * 5);
        }

        /// <summary>
        /// Cộng XP theo dạng "XP trong level hiện tại". XP dư được carry sang level tiếp theo,
        /// không bị mất khi một phần thưởng vượt quá ngưỡng level-up.
        /// </summary>
        public int GainExperience(int amount)
        {
            if (amount <= 0 || ConfigData == null || IsAtMaxLevel)
            {
                return 0;
            }

            int previousLevel = CurrentLevel;
            long pending = (long)CurrentExperience + amount;
            int maxLevel = Mathf.Max(1, ConfigData.MaxLevel);

            while (CurrentLevel < maxLevel)
            {
                int required = Mathf.Max(1, GetExperienceRequiredForNextLevel(CurrentLevel));
                if (pending < required)
                {
                    break;
                }

                pending -= required;
                CurrentLevel++;
            }

            CurrentExperience = CurrentLevel >= maxLevel
                ? 0
                : (int)System.Math.Min(pending, int.MaxValue);

            if (CurrentLevel != previousLevel)
            {
                RecalculateStats();
                EmitSignal(SignalName.LevelChanged, CurrentLevel);
            }
            else
            {
                EmitSignal(SignalName.StatsChanged);
            }

            return CurrentLevel - previousLevel;
        }

        public void RestoreProgression(int level, int currentExperience)
        {
            int maxLevel = Mathf.Max(1, ConfigData?.MaxLevel ?? 99);
            CurrentLevel = Mathf.Clamp(level, 1, maxLevel);
            long pending = Mathf.Max(0, currentExperience);

            while (CurrentLevel < maxLevel)
            {
                int required = Mathf.Max(1, GetExperienceRequiredForNextLevel(CurrentLevel));
                if (pending < required)
                {
                    break;
                }

                pending -= required;
                CurrentLevel++;
            }

            CurrentExperience = CurrentLevel >= maxLevel
                ? 0
                : (int)System.Math.Min(pending, int.MaxValue);
            RecalculateStats();
        }

        public void SetCurrentLevel(int level)
        {
            int maxLevel = Mathf.Max(1, ConfigData?.MaxLevel ?? 99);
            int previousLevel = CurrentLevel;
            CurrentLevel = Mathf.Clamp(level, 1, maxLevel);
            CurrentExperience = CurrentLevel >= maxLevel
                ? 0
                : Mathf.Clamp(CurrentExperience, 0, Mathf.Max(0, GetExperienceRequiredForNextLevel(CurrentLevel) - 1));
            RecalculateStats();

            if (CurrentLevel != previousLevel)
            {
                EmitSignal(SignalName.LevelChanged, CurrentLevel);
            }
        }

        public void RestoreResourceValues(float hp, float mp, float stamina)
        {
            CurrentHP = Mathf.Clamp(hp, 0f, MaxHP);
            CurrentMP = Mathf.Clamp(mp, 0f, MaxMP);
            CurrentStamina = Mathf.Clamp(stamina, 0f, MaxStamina);
            CurrentGuard = MaxGuard;
            CurrentPoise = MaxPoise;
            _defeatSignalSent = CurrentHP <= 0f;
            EmitSignal(SignalName.StatsChanged);
        }

        public void FillAllResources()
        {
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
            CurrentStamina = MaxStamina;
            CurrentGuard = MaxGuard;
            CurrentPoise = MaxPoise;
            _defeatSignalSent = false;
            EmitSignal(SignalName.StatsChanged);
        }

        public void RecalculateStats()
        {
            if (UseManualProfile || ConfigData == null)
            {
                BuildManualProfile();
            }
            else
            {
                BuildCharacterProfile();
            }

            if (_resourcesInitialized)
            {
                CurrentHP = Mathf.Clamp(CurrentHP, 0f, MaxHP);
                CurrentMP = Mathf.Clamp(CurrentMP, 0f, MaxMP);
                CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, MaxStamina);
                CurrentGuard = Mathf.Clamp(CurrentGuard, 0f, MaxGuard);
                CurrentPoise = Mathf.Clamp(CurrentPoise, 0f, MaxPoise);
            }

            EmitSignal(SignalName.StatsChanged);
        }

        public int GetAttributeValue(AttributeType type)
        {
            return FinalAttributes.TryGetValue(type, out int value) ? value : 0;
        }

        public void SetTemporaryAttributeBonus(AttributeType type, int amount)
        {
            SetTemporaryAttributeBonus("legacy", type, amount);
        }

        public void SetTemporaryAttributeBonus(string sourceId, AttributeType type, int amount)
        {
            string safeSource = string.IsNullOrWhiteSpace(sourceId) ? "anonymous" : sourceId.Trim();
            if (!_temporaryAttributeBonuses.TryGetValue(safeSource, out var sourceBonuses))
            {
                if (amount == 0)
                {
                    return;
                }

                sourceBonuses = new Dictionary<AttributeType, int>();
                _temporaryAttributeBonuses[safeSource] = sourceBonuses;
            }

            if (amount == 0)
            {
                sourceBonuses.Remove(type);
                if (sourceBonuses.Count == 0)
                {
                    _temporaryAttributeBonuses.Remove(safeSource);
                }
            }
            else
            {
                sourceBonuses[type] = amount;
            }

            RecalculateStats();
        }

        public void ClearTemporaryAttributeBonuses(string sourceId)
        {
            if (!string.IsNullOrWhiteSpace(sourceId) && _temporaryAttributeBonuses.Remove(sourceId.Trim()))
            {
                RecalculateStats();
            }
        }

        public float GetAttackPower(PowerScalingType scaling, DamageType damageType)
        {
            PowerScalingType resolved = scaling;
            if (resolved == PowerScalingType.Auto)
            {
                resolved = damageType == DamageType.Magic || damageType == DamageType.Ice
                    ? PowerScalingType.Magic
                    : PowerScalingType.Physical;
            }

            return resolved switch
            {
                PowerScalingType.Magic => MagicPower,
                PowerScalingType.Highest => PrimaryPower,
                PowerScalingType.None => 0f,
                _ => PhysicalPower
            };
        }

        public float GetDamageResistance(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.True => 0f,
                DamageType.Magic => MagicResistance,
                DamageType.Ice => MagicResistance,
                _ => Armor
            };
        }

        public float GetKnockbackResistance()
        {
            int vitality = GetAttributeValue(AttributeType.Vitality);
            int defense = GetAttributeValue(AttributeType.Defense);
            return Mathf.Clamp((vitality * 0.0125f) + (defense * 0.0075f), 0f, 0.75f);
        }

        // API cũ được giữ để save/UI hoặc code ngoài không gãy trong một lần chuyển đổi.
        public float ComputeKnockbackChance(float baseChance)
        {
            return Mathf.Clamp(baseChance * (1f - GetKnockbackResistance()), 0f, 1f);
        }

        public float ComputeKnockbackForce(float baseForce)
        {
            return Mathf.Max(0f, baseForce) * (1f - GetKnockbackResistance());
        }

        private void BuildManualProfile()
        {
            CombatStatProfileData profile = ManualProfileData;
            foreach (AttributeType attribute in System.Enum.GetValues(typeof(AttributeType)))
            {
                int baseValue = profile?.GetAttribute(attribute) ?? 0;
                FinalAttributes[attribute] = baseValue + GetTemporaryAttributeBonus(attribute);
            }

            if (profile != null)
            {
                MaxHP = Mathf.Max(1f, profile.MaxHP);
                MaxMP = Mathf.Max(0f, profile.MaxMP);
                MaxStamina = Mathf.Max(0f, profile.MaxStamina);
                MaxGuard = Mathf.Max(0f, profile.MaxGuard);
                MaxPoise = Mathf.Max(0f, profile.MaxPoise);
                PhysicalPower = Mathf.Max(0f, profile.PhysicalPower);
                MagicPower = Mathf.Max(0f, profile.MagicPower);
                AttackDamage = PhysicalPower;
                Armor = Mathf.Max(0f, profile.Armor);
                MagicResistance = Mathf.Max(0f, profile.MagicResistance);
                AttackSpeed = Mathf.Clamp(profile.AttackSpeed, 0.25f, 4f);

                ManaRegenRate = Mathf.Max(0f, profile.ManaRegenRate);
                StaminaRegenRate = Mathf.Max(0f, profile.StaminaRegenRate);
                GuardRegenRate = Mathf.Max(0f, profile.GuardRegenRate);
                PoiseRegenRate = Mathf.Max(0f, profile.PoiseRegenRate);
                ManaRegenDelay = Mathf.Max(0f, profile.ManaRegenDelay);
                StaminaRegenDelay = Mathf.Max(0f, profile.StaminaRegenDelay);
                GuardRegenDelay = Mathf.Max(0f, profile.GuardRegenDelay);
                PoiseRegenDelay = Mathf.Max(0f, profile.PoiseRegenDelay);
                MitigationCurveConstant = 100f;
                return;
            }

            MaxHP = Mathf.Max(1f, ManualMaxHP);
            MaxMP = Mathf.Max(0f, ManualMaxMP);
            MaxStamina = Mathf.Max(0f, ManualMaxStamina);
            MaxGuard = Mathf.Max(0f, ManualMaxGuard);
            MaxPoise = Mathf.Max(0f, ManualMaxPoise);
            PhysicalPower = Mathf.Max(0f, ManualAttackPower);
            MagicPower = Mathf.Max(0f, ManualMagicPower);
            AttackDamage = PhysicalPower;
            Armor = Mathf.Max(0f, ManualArmor);
            MagicResistance = Mathf.Max(0f, ManualMagicResistance);
            AttackSpeed = Mathf.Clamp(ManualAttackSpeed, 0.25f, 4f);
            MitigationCurveConstant = 100f;
        }

        private void BuildCharacterProfile()
        {
            foreach (AttributeType attribute in System.Enum.GetValues(typeof(AttributeType)))
            {
                int baseValue = ConfigData.CalculateAttribute(attribute, CurrentLevel);
                int equipmentValue = EquipmentMgr?.GetTotalAttributeBonus(attribute) ?? 0;
                int temporaryValue = GetTemporaryAttributeBonus(attribute);
                FinalAttributes[attribute] = baseValue + equipmentValue + temporaryValue;
            }

            int vitality = GetAttributeValue(AttributeType.Vitality);
            int strength = GetAttributeValue(AttributeType.Strength);
            int defense = GetAttributeValue(AttributeType.Defense);
            int dexterity = GetAttributeValue(AttributeType.Dexterity);
            int intelligence = GetAttributeValue(AttributeType.Intelligence);
            int spirit = GetAttributeValue(AttributeType.Spirit);
            PowerBalanceData balance = ConfigData.BalanceProfile;

            float weaponDamage = EquipmentMgr?.GetTotalBaseValue(EquipmentSlot.MainHand) ?? 0f;
            float equipmentArmor = (EquipmentMgr?.GetTotalBaseValue(EquipmentSlot.Body) ?? 0f)
                + (EquipmentMgr?.GetTotalBaseValue(EquipmentSlot.Head) ?? 0f);

            float weaponWeight = 1f;
            var weapon = EquipmentMgr?.GetEquippedItem(EquipmentSlot.MainHand);
            if (weapon != null && weapon.WeaponWeight > 0f)
            {
                weaponWeight = weapon.WeaponWeight;
            }

            if (balance != null)
            {
                MaxHP = balance.CalculateMaxHP(vitality, strength);
                MaxMP = balance.CalculateMaxMP(intelligence, spirit);
                MaxStamina = balance.CalculateMaxStamina(vitality, dexterity);
                MaxGuard = balance.CalculateMaxGuard(defense, vitality);
                MaxPoise = balance.CalculateMaxPoise(vitality, defense);
                PhysicalPower = balance.CalculatePhysicalPower(weaponDamage, strength);
                MagicPower = balance.CalculateMagicPower(intelligence, spirit);
                Armor = balance.CalculateArmor(equipmentArmor, defense);
                MagicResistance = balance.CalculateMagicResistance(spirit, defense);
                AttackSpeed = balance.CalculateAttackSpeed(dexterity, weaponWeight);
                ManaRegenRate = balance.CalculateManaRegen(spirit);
                StaminaRegenRate = balance.CalculateStaminaRegen(dexterity, vitality);
                GuardRegenRate = Mathf.Max(0f, balance.GuardRegenRate);
                PoiseRegenRate = Mathf.Max(0f, balance.PoiseRegenRate);
                ManaRegenDelay = Mathf.Max(0f, balance.ManaRegenDelay);
                StaminaRegenDelay = Mathf.Max(0f, balance.StaminaRegenDelay);
                GuardRegenDelay = Mathf.Max(0f, balance.GuardRegenDelay);
                PoiseRegenDelay = Mathf.Max(0f, balance.PoiseRegenDelay);
                MitigationCurveConstant = Mathf.Max(1f, balance.MitigationCurveConstant);
            }
            else
            {
                // Fallback giữ cùng công thức mặc định của core_power.tres.
                MaxHP = 80f + vitality * 8f + strength;
                MaxMP = 30f + intelligence * 4f + spirit * 2f;
                MaxStamina = 60f + vitality * 3f + dexterity;
                MaxGuard = 35f + defense * 4f + vitality * 1.5f;
                MaxPoise = 20f + vitality * 3f + defense * 1.5f;
                PhysicalPower = weaponDamage + strength * 2f;
                MagicPower = intelligence * 2f + spirit * 0.5f;
                Armor = equipmentArmor + defense * 1.5f;
                MagicResistance = spirit * 0.8f + defense * 0.4f;
                ManaRegenRate = 0.5f + spirit * 0.08f;
                StaminaRegenRate = 12f + dexterity * 0.5f + vitality * 0.15f;
                GuardRegenRate = 14f;
                PoiseRegenRate = 10f;
                ManaRegenDelay = 2f;
                StaminaRegenDelay = 0.35f;
                GuardRegenDelay = 0.8f;
                PoiseRegenDelay = 1.1f;
                float dexterityFactor = 0.9f + dexterity * 0.0125f;
                float weightFactor = 1f / Mathf.Sqrt(Mathf.Max(0.7f, weaponWeight));
                AttackSpeed = Mathf.Clamp(dexterityFactor * weightFactor, 0.6f, 2.2f);
                MitigationCurveConstant = 100f;
            }

            AttackDamage = PhysicalPower;
        }


        private int GetTemporaryAttributeBonus(AttributeType type)
        {
            int total = 0;
            foreach (var source in _temporaryAttributeBonuses.Values)
            {
                if (source.TryGetValue(type, out int value))
                {
                    total += value;
                }
            }

            return total;
        }

        private void RegisterWithPlayerManager()
        {
            if (AutoRegisterWithPlayerManager)
            {
                PlayerManager.Instance?.RegisterMember(this);
            }
        }
    }
}
