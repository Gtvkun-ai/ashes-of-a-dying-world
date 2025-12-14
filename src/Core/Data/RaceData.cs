using Godot;
using Godot.Collections;

namespace AshesofaDyingWorld.Core.Data
{
    [GlobalClass]
    public partial class RaceData : Resource
    {
        [Export] public string RaceName { get; set; } = "Human";

        // Chỉ số khởi đầu ở Level 1
        [ExportGroup("Base Attributes (Lvl 1)")]
        [Export] public Dictionary<AttributeType, int> BaseAttributes { get; set; } = new()
        {
            { AttributeType.Strength, 10 },
            { AttributeType.Dexterity, 10 },
            { AttributeType.Intelligence, 10 },
            { AttributeType.Vitality, 10 },
            { AttributeType.Spirit, 10 },
            { AttributeType.Defense, 5 }
        };

        // Chỉ số tăng thêm mỗi Level
        [ExportGroup("Growth Per Level")]
        [Export] public Dictionary<AttributeType, float> AttributeGrowth { get; set; } = new()
        {
            { AttributeType.Strength, 2.5f },     // Mỗi cấp tăng 2.5 Str
            { AttributeType.Dexterity, 1.0f },
            { AttributeType.Intelligence, 0.5f },
            { AttributeType.Vitality, 2.0f },
            { AttributeType.Spirit, 1.0f },
            { AttributeType.Defense, 0.5f }
        };

        // Tính toán chỉ số dựa trên Level hiện tại
        public int GetAttributeAtLevel(AttributeType type, int level)
        {
            if (!BaseAttributes.ContainsKey(type)) return 0;
            
            float growth = AttributeGrowth.ContainsKey(type) ? AttributeGrowth[type] : 0;
            // Công thức: Base + (Growth * (Level - 1))
            return Mathf.RoundToInt(BaseAttributes[type] + (growth * (level - 1)));
        }
    }
}