using Godot;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
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

        [ExportGroup("Identity and Progression")]
        [Export] public CharacterConfig ConfigData { get; set; }
        [Export] public EquipmentManager EquipmentMgr { get; set; }
        [Export] public bool AutoRegisterWithPlayerManager { get; set; } = true;
        [Export(PropertyHint.Range, "1,99,1")] public int InitialLevel { get; set; } = 1;

        [ExportGroup("Manual Profile")]
        [Export] public bool UseManualProfile { get; set; } = false;
        [Export] public float ManualMaxHP { get; set; } = 100f;
        [Export] public float ManualMaxMP { get; set; } = 0f;
        [Export] public float ManualMaxStamina { get; set; } = 100f;
        [Export] public float ManualMaxGuard { get; set; } = 60f;
        [Export] public float ManualMaxPoise { get; set; } = 40f;
        [Export] public float ManualAttackPower { get; set; } = 10f;
        [Export] public float ManualArmor { get; set; } = 0f;
        [Export] public float ManualAttackSpeed { get; set; } = 1f;

        [ExportGroup("Regeneration")]
        [Export] public float StaminaRegenRate { get; set; } = 10f;
        [Export] public float GuardRegenRate { get; set; } = 16f;
        [Export] public float PoiseRegenRate { get; set; } = 12f;
        [Export] public float StaminaRegenDelay { get; set; } = 0.45f;
        [Export] public float GuardRegenDelay { get; set; } = 0.8f;
        [Export] public float PoiseRegenDelay { get; set; } = 1.1f;

        public int CurrentLevel { get; private set; } = 1;

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
        public float AttackDamage { get; private set; }
        public float Armor { get; private set; }
        public float AttackSpeed { get; private set; } = 1f;

        // Modifier được tách theo nguồn để skill/status không giẫm lên nhau.
        // Bản cũ chỉ có một giá trị mỗi attribute nên buff A tắt là tiện tay xóa luôn buff B.
        private readonly Dictionary<string, Dictionary<AttributeType, int>> _temporaryAttributeBonuses = new();
        private bool _resourcesInitialized;
        private bool _defeatSignalSent;
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

        public bool ConsumeStamina(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (amount <= 0f)
            {
                return true;
            }

            _staminaRegenDelayRemaining = StaminaRegenDelay;
            if (CurrentStamina + 0.001f < amount)
            {
                return false;
            }

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

        public void UpdateRegeneration(float delta, bool allowStamina, bool allowGuard, bool allowPoise)
        {
            float dt = Mathf.Max(0f, delta);
            _staminaRegenDelayRemaining = Mathf.Max(0f, _staminaRegenDelayRemaining - dt);
            _guardRegenDelayRemaining = Mathf.Max(0f, _guardRegenDelayRemaining - dt);
            _poiseRegenDelayRemaining = Mathf.Max(0f, _poiseRegenDelayRemaining - dt);

            bool changed = false;
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

        public void SetCurrentLevel(int level)
        {
            int maxLevel = ConfigData?.MaxLevel ?? 99;
            CurrentLevel = Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));
            RecalculateStats();
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
            foreach (AttributeType attribute in System.Enum.GetValues(typeof(AttributeType)))
            {
                FinalAttributes[attribute] = GetTemporaryAttributeBonus(attribute);
            }

            MaxHP = Mathf.Max(1f, ManualMaxHP);
            MaxMP = Mathf.Max(0f, ManualMaxMP);
            MaxStamina = Mathf.Max(0f, ManualMaxStamina);
            MaxGuard = Mathf.Max(0f, ManualMaxGuard);
            MaxPoise = Mathf.Max(0f, ManualMaxPoise);
            AttackDamage = Mathf.Max(0f, ManualAttackPower);
            Armor = Mathf.Max(0f, ManualArmor);
            AttackSpeed = Mathf.Clamp(ManualAttackSpeed, 0.25f, 4f);
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

            // Tính từ FinalAttributes để equipment, skill và status modifier thật sự có tác dụng.
            MaxHP = vitality * 10f + strength * 2f + 100f;
            MaxMP = intelligence * 8f + 50f;
            MaxStamina = vitality * 5f + 50f;
            MaxGuard = 40f + defense * 5f + vitality * 1.5f;
            MaxPoise = 25f + vitality * 4f + defense * 2f;

            float weaponDamage = EquipmentMgr?.GetTotalBaseValue(EquipmentSlot.MainHand) ?? 0f;
            AttackDamage = weaponDamage + strength * 2.5f;

            float equipmentArmor = (EquipmentMgr?.GetTotalBaseValue(EquipmentSlot.Body) ?? 0f)
                + (EquipmentMgr?.GetTotalBaseValue(EquipmentSlot.Head) ?? 0f);
            Armor = equipmentArmor + defense * 1.5f;

            float weaponWeight = 1f;
            var weapon = EquipmentMgr?.GetEquippedItem(EquipmentSlot.MainHand);
            if (weapon != null && weapon.WeaponWeight > 0f)
            {
                weaponWeight = weapon.WeaponWeight;
            }

            float dexterityFactor = 1f + dexterity * 0.02f;
            float weightFactor = 1f / Mathf.Max(0.5f, weaponWeight);
            AttackSpeed = Mathf.Clamp(dexterityFactor * weightFactor, 0.5f, 3f);
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
