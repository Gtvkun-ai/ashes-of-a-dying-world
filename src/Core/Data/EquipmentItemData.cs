using Godot;
using System;
using Godot.Collections;
namespace AshesofaDyingWorld.Core.Data
{
    public partial class EquipmentItemData : Resource
    {
        [ExportGroup("General Info")] 
        [Export] public string ID {get; set;}
        [Export] public string ItemName {get; set;}
        [Export] public Texture2D Icon {get; set;}
        [Export] public EquipmentSlot SlotType {get; set;} // Loại trang bị

        [ExportGroup("Requirements")]
        [Export] public int MinLevel { get; set; } = 1;
        [Export] public string RequiredClass {get; set;} = "All";

        [ExportGroup("Base Stats")]
        // Sát thương cơ bản hoặc phòng thủ cơ bản
        [Export] public float BaseValue {get; set;} =  0;

        // Thuộc tính cộng thêm khi trang bị
        [ExportGroup("Bonus Attributes")]
        [Export] public Dictionary<AttributeType, int> AttributeBonuses { get; set; } = new();
        
        [ExportGroup("Unique Effects")]
        [Export] public Array<string> PassiveSkills { get; set; }
    }
}
