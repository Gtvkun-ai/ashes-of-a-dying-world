using Godot;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using System.Collections.Generic;

namespace AshesofaDyingWorld.Entities.Player
{
    public partial class PlayerStats : Node
    {
        [Export] public CharacterConfig ConfigData;
        [Export] public EquipmentManager EquipmentMgr; // Kéo node EquipmentManager vào đây

        public int CurrentLevel { get; private set; } = 1;

        // Chỉ số cuối cùng (Base + Equipment + Buffs)
        public Dictionary<AttributeType, int> FinalAttributes { get; private set; } = new();
        
        // Các chỉ số chiến đấu
        public float AttackDamage { get; private set; }
        public float Armor { get; private set; }

        public void RecalculateStats()
        {
            if (ConfigData == null) return;

            // 1. Tính Attribute (Str, Dex...)
            foreach (AttributeType attr in System.Enum.GetValues(typeof(AttributeType)))
            {
                // Chỉ số gốc từ Tộc & Level
                int baseVal = ConfigData.CalculateAttribute(attr, CurrentLevel);
                
                // Chỉ số cộng từ Trang bị
                int equipVal = EquipmentMgr != null ? EquipmentMgr.GetTotalAttributeBonus(attr) : 0;

                FinalAttributes[attr] = baseVal + equipVal;
            }

            // 2. Tính Derived Stats (HP, MP) từ FinalAttributes
            // Ví dụ: MaxHP = (Vit * 10) + (Str * 2)
            float maxHP = (FinalAttributes[AttributeType.Vitality] * 10) + 
                          (FinalAttributes[AttributeType.Strength] * 2);
            
            // 3. Tính Combat Stats (Dmg, Armor)
            CalculateCombatStats();

            GD.Print($"Stats Updated. STR: {FinalAttributes[AttributeType.Strength]} (Base) + Equipment");
        }

        private void CalculateCombatStats()
        {
            // Sát thương = Dmg Vũ khí + (Str * hệ số)
            float weaponDmg = EquipmentMgr != null ? EquipmentMgr.GetTotalBaseValue(EquipmentSlot.MainHand) : 0;
            float strBonus = FinalAttributes[AttributeType.Strength] * 2.5f; // Ví dụ 1 Str = 2.5 Dmg
            AttackDamage = weaponDmg + strBonus;

            // Giáp = Giáp Áo + Giáp Mũ + (Dex/2)
            float equipArmor = EquipmentMgr != null ? 
                               (EquipmentMgr.GetTotalBaseValue(EquipmentSlot.Body) + 
                                EquipmentMgr.GetTotalBaseValue(EquipmentSlot.Head)) : 0;
            Armor = equipArmor + (FinalAttributes[AttributeType.Defense] * 1.5f);
        }
    }
}