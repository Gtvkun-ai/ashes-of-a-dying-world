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
		[Export] public VideoStream Avatar { get; set; } // Video ogv cho HUD/Menu
		[Export] public PackedScene BodyScene { get; set; } // Scene AnimatedSprite2D cho body nhân vật (VD: Hyou_body.tscn)
		[ExportGroup("Origin")]
		[Export] public RaceData CharacterRace {get; set;}

		[ExportGroup("Progression")]
		[Export(PropertyHint.Range, "1,99")] public int MaxLevel {get; set;} = 99;
		[Export] public PowerBalanceData BalanceProfile { get; set; }

		//Kỹ năng và combo riêng
		[ExportGroup("Combat Abilities")]
		[Export] public Array<SkillData> ActiveSkills {get; set;}
		[Export] public Array<SkillData> ComboSequence {get; set;} // Chuỗi kỹ năng combo

		[ExportGroup("Skill Development")]
		// Cây phát triển của panel Kỹ năng riêng trong menu chính.
		// Tab Kỹ năng của panel Nhân vật vẫn chỉ hiển thị state đã mở khóa.
		[Export] public CharacterSkillTreeData SkillTree { get; set; }

		[ExportGroup("Equipment")]
		[Export] public string WeaponID {get; set;}
		[Export] public string ArmorID {get; set;}


		[Export] public Color ThemeColor {get; set;} = new Color("#38bdf8"); // Màu mặc định (Xanh)
		[Export] public Texture2D BackgroundImage {get; set;} // Hình nền riêng

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
			return BalanceProfile != null
				? BalanceProfile.CalculateMaxHP(vit, str)
				: 80f + vit * 8f + str;
		}

		public float CalculateMaxStamina(int currentLevel)
		{
			int vit = CalculateAttribute(AttributeType.Vitality, currentLevel);
			int dex = CalculateAttribute(AttributeType.Dexterity, currentLevel);
			return BalanceProfile != null
				? BalanceProfile.CalculateMaxStamina(vit, dex)
				: 60f + vit * 3f + dex;
		}

		public float CalculateMaxMP(int currentLevel)
		{
			int intl = CalculateAttribute(AttributeType.Intelligence, currentLevel);
			int spirit = CalculateAttribute(AttributeType.Spirit, currentLevel);
			return BalanceProfile != null
				? BalanceProfile.CalculateMaxMP(intl, spirit)
				: 30f + intl * 4f + spirit * 2f;
		}
		
	}
	
}
