using Godot;
using Godot.Collections; // dùng cho Dictionary
namespace AshesofaDyingWorld.Core.Data
{
    [GlobalClass] // Đánh dấu lớp này để có thể tạo tài nguyên trong Godot
    public partial class CharacterConfig : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string ID {get; set;}
        [Export] public string Name {get; set;}
        [Export] public Texture2D Icon {get; set;} // Icon tròn cho HUD/Menu
        [Export] public Texture2D Avatar {get; set;} // Ảnh minh hoạ lớn cho character detail

        [ExportGroup("Origin")]
        [Export] public RaceData CharacterRace {get; set;}

        [ExportGroup("Progression")]
        [Export(PropertyHint.Range, "1,99")] public int MaxLevel {get; set;} = 99;

        //Kỹ năng và combo riêng
        [ExportGroup("Combat Abilities")]
        [Export] public Array<SkillData> ActiveSkills {get; set;}
        [Export] public Array<SkillData> ComboSequence {get; set;} // Chuỗi kỹ năng combo

        [ExportGroup("Equipment")]
        [Export] public string WeaponID {get; set;}
        [Export] public string ArmorID {get; set;}

        //Tính toán chỉ số thực tế
        public int CalculateAttribute(AttributeType type, int currentlevel)
        {
            if(CharacterRace == null)
                return 0;

            return CharacterRace.GetAttributeAtLevel(type, currentlevel);
        }
    
        //Tính toán chỉ số phụ (Stat) ví dụ vitality -> MaxHP
        public float CalculateMaxHP(int currentlevel)
        {
            int vit = CalculateAttribute(AttributeType.Vitality, currentlevel);
            int str = CalculateAttribute(AttributeType.Strength, currentlevel);
            return (vit *10) + (str *2) + 100; //Công thức là dùng Vitality và Strength để tính MaxHP
        }
        public float CalculateMaxStamina(int currentLevel)
        {
            int vit = CalculateAttribute(AttributeType.Vitality, currentLevel);
            return (vit * 5) + 50;
        }

        public float CalculateMaxMP(int currentLevel)
        {
            int intl = CalculateAttribute(AttributeType.Intelligence, currentLevel);
            return (intl * 8) + 50;
        }
        
    }
    
}
