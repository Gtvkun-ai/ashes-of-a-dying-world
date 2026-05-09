using Godot;
using System;
using Godot.Collections;
namespace AshesofaDyingWorld.Core.Data
{
    public enum InventoryItemCategory
    {
        Consumable,
        Material,
        Equipment,
        Quest,
        Other
    }

    public partial class EquipmentItemData : Resource
    {
        [ExportGroup("General Info")] 
        [Export] public string ID {get; set;}
        [Export] public string ItemName {get; set;}
        [Export] public Texture2D Icon {get; set;}
        [Export] public InventoryItemCategory InventoryCategory { get; set; } = InventoryItemCategory.Equipment;
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
        [Export] public EquipmentSlot SlotType {get; set;} // Loại trang bị

        [ExportGroup("Requirements")]
        [Export] public int MinLevel { get; set; } = 1;
        [Export] public string RequiredClass {get; set;} = "All";

        [ExportGroup("Base Stats")]
        // Sát thương cơ bản hoặc phòng thủ cơ bản
        [Export] public float BaseValue {get; set;} =  0;

        [ExportGroup("Visual")]
        // Scene chứa AnimatedSprite2D của vũ khí (VD: woodSword.tscn)
        [Export] public PackedScene WeaponScene {get; set;}

        [ExportGroup("Weapon Properties")]
        // Độ nặng của vũ khí: 1 = trung bình, >1 = nặng hơn, <1 = nhẹ hơn
        [Export] public float WeaponWeight { get; set; } = 1f;

        // Xác suất chặn đòn cơ bản của vũ khí (0..1)
        [Export] public float BlockChance { get; set; } = 0f;

        // Tỉ lệ giảm sát thương khi chặn thành công (0..1)
        [Export] public float BlockDamageReduction { get; set; } = 0.5f;

        // Hệ số stamina mất trên mỗi 1 sát thương đã chặn
        [Export] public float BlockStaminaPerDamage { get; set; } = 1.0f;

        // Thuộc tính cộng thêm khi trang bị
        [ExportGroup("Bonus Attributes")]
        [Export] public Dictionary<AttributeType, int> AttributeBonuses { get; set; } = new();
        
        [ExportGroup("Unique Effects")]
        [Export] public Array<string> PassiveSkills { get; set; }
    }
}
