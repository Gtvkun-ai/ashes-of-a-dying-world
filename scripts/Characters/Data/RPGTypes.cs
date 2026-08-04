namespace AshesofaDyingWorld.Core.Data
{
    public enum AttributeType
    {
        Strength,       // Sức mạnh (Ảnh hưởng dmg vật lý)
        Dexterity,      // Khéo léo (Ảnh hưởng tốc độ đánh, crit)
        Intelligence,   // Trí tuệ (Ảnh hưởng phép thuật, MP)
        Vitality,       // Thể chất (Ảnh hưởng HP)
        Spirit,         // Tinh thần (Ảnh hưởng hồi phục, kháng phép)
        Defense         // Phòng thủ
    }

    public enum StatType
    {
        MaxHP,
        MaxMP,
        MaxStamina,
        MaxSpirit
    }
    public enum EquipmentSlot
    {
        MainHand,   // Vũ khí chính (Kiếm, Gậy)
        OffHand,    // Vũ khí phụ (Khiên, Sách phép)
        Head,       // Mũ
        Body,       // Áo giáp
        Legs,       // Quần/Giày
        Accessory1, // Nhẫn 1
        Accessory2  // Nhẫn 2
    }
}