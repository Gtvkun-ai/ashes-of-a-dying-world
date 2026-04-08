using Godot;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Managers;
using System.Collections.Generic;

namespace AshesofaDyingWorld.Entities.Player
{
	public partial class PlayerStats : Node
	{
		//Sự kiện để thông báo khi chỉ số thay đổi
		[Signal] public delegate void StatsChangedEventHandler();
		[Export] public CharacterConfig ConfigData;
		[Export] public EquipmentManager EquipmentMgr; 

		public int CurrentLevel { get; private set; } = 1;

		//Các giá trị hp, mp, stamina hiện tại
		public float CurrentHP { get; private set; }
		public float CurrentMP { get; private set; }
		public float CurrentStamina { get; private set; }

		//Các giá trị tối đa hp, mp, stamina
		public float MaxHP { get; private set; }
		public float MaxMP { get; private set; }
		public float MaxStamina { get; private set; }

		// Chỉ số cuối cùng (Base + Equipment + Buffs)
		public Dictionary<AttributeType, int> FinalAttributes { get; private set; } = new();
		
		// Các chỉ số chiến đấu
		public float AttackDamage { get; private set; }
		public float Armor { get; private set; }
		public float AttackSpeed { get; private set; } = 1f; // Hệ số tốc độ đánh (1 = bình thường)

		// Tốc độ hồi Stamina
		[Export] public float StaminaRegenRate { get; set; } = 10f; // Hồi 10/giây

		public override void _Ready()
		{
			RecalculateStats();   

			//Đầy lại HP, MP, Stamina khi bắt đầu
			CurrentHP = MaxHP;
			CurrentMP = MaxMP;
			CurrentStamina = MaxStamina; 

			if (PlayerManager.Instance != null)
			{
				PlayerManager.Instance.RegisterMember(this); // Đăng ký với PlayerManager
			}
			
			// Phát tín hiệu SAU KHI đã set đầy đủ giá trị
			EmitSignal(SignalName.StatsChanged);
		}

		public override void _Process(double delta)
		{
			// Hồi Stamina nếu chưa đầy
			if (CurrentStamina < MaxStamina)
			{
				ChangeStamina(StaminaRegenRate * (float)delta);
			}
		}

		// Method tiêu hao Stamina
		public bool ConsumeStamina(float amount)
		{
			if (CurrentStamina >= amount)
			{
				ChangeStamina(-amount);
				return true;
			}
			return false; // Không đủ Stamina
		}

		// Method thay đổi Stamina
		public void ChangeStamina(float amount)
		{
			CurrentStamina = Mathf.Clamp(CurrentStamina + amount, 0, MaxStamina);
			EmitSignal(SignalName.StatsChanged);
		}

		// Method thay đổi HP
		public void ChangeHP(float amount)
		{
			CurrentHP = Mathf.Clamp(CurrentHP + amount, 0, MaxHP);
			EmitSignal(SignalName.StatsChanged);
		}

		// Method thay đổi MP
		public void ChangeMP(float amount)
		{
			CurrentMP = Mathf.Clamp(CurrentMP + amount, 0, MaxMP);
			EmitSignal(SignalName.StatsChanged);
		}

		public void RecalculateStats()
		{
			if (ConfigData == null) 
			{
				GD.PrintErr("PlayerStats: ConfigData is null!");
				return;
			}
			// Tính Attribute (Str, Dex...)
			foreach (AttributeType attr in System.Enum.GetValues(typeof(AttributeType)))
			{
				// Chỉ số gốc từ Tộc & Level
				int baseVal = ConfigData.CalculateAttribute(attr, CurrentLevel);
				
				// Chỉ số cộng từ Trang bị
				int equipVal = EquipmentMgr != null ? EquipmentMgr.GetTotalAttributeBonus(attr) : 0;

				FinalAttributes[attr] = baseVal + equipVal;
			}

			// Cập nhật Max HP, MP, Stamina từ Config TRƯỚC
			MaxHP = ConfigData.CalculateMaxHP(CurrentLevel);
			MaxMP = ConfigData.CalculateMaxMP(CurrentLevel);
			MaxStamina = ConfigData.CalculateMaxStamina(CurrentLevel);

			GD.Print($"[PlayerStats] MaxHP={MaxHP}, MaxMP={MaxMP}, MaxStamina={MaxStamina}");

			// Tính Combat Stats (Dmg, Armor)
			CalculateCombatStats();

			GD.Print($"Stats Updated. HP:{MaxHP} MP:{MaxMP} STR:{FinalAttributes[AttributeType.Strength]}");

			// Phát tín hiệu thông báo chỉ số đã thay đổi
			EmitSignal(SignalName.StatsChanged);
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

			// Tốc độ đánh: dựa trên Dexterity và độ nặng vũ khí
			int dex = FinalAttributes.ContainsKey(AttributeType.Dexterity)
				? FinalAttributes[AttributeType.Dexterity]
				: 0;

			// Mỗi 1 Dex cho ~2% tốc độ đánh
			float dexFactor = 1.0f + dex * 0.02f;

			// Lấy độ nặng vũ khí (1 = trung bình, >1 = nặng hơn => chậm hơn)
			float weaponWeight = 1.0f;
			if (EquipmentMgr != null)
			{
				var mainWeapon = EquipmentMgr.GetEquippedItem(EquipmentSlot.MainHand);
				if (mainWeapon != null && mainWeapon.WeaponWeight > 0f)
				{
					weaponWeight = mainWeapon.WeaponWeight;
				}
			}

			// Vũ khí càng nặng thì hệ số càng giảm
			float weightFactor = 1.0f / Mathf.Max(0.5f, weaponWeight); // Clamp để tránh chia cho số quá nhỏ

			// Hệ số tốc độ cuối cùng, giới hạn trong khoảng [0.5, 3]
			AttackSpeed = Mathf.Clamp(dexFactor * weightFactor, 0.5f, 3.0f);
		}

		// ----- Knockback resistance helpers -----

		// Tính tỉ lệ cuối cùng bị knockback khi trúng đòn, dựa trên Defense
		// baseChance: tỉ lệ cơ bản do đòn đánh quy định (0..1)
		public float ComputeKnockbackChance(float baseChance)
		{
			int def = 0;
			if (FinalAttributes != null &&
				FinalAttributes.TryGetValue(AttributeType.Defense, out int defVal))
			{
				def = defVal;
			}

			// Defense càng cao, chance càng giảm.
			// Ví dụ: mỗi 5 DEF giảm ~10% tỉ lệ (giới hạn tối thiểu 10%).
			float defenseFactor = 1.0f / (1.0f + def * 0.05f);
			float finalChance = Mathf.Clamp(baseChance * defenseFactor, 0.1f, 1.0f);
			return finalChance;
		}

		// Tính lực knockback cuối cùng (khoảng cách đẩy lùi), dựa trên Vitality
		// baseForce: lực cơ bản do đòn đánh quy định
		public float ComputeKnockbackForce(float baseForce)
		{
			int vit = 0;
			if (FinalAttributes != null &&
				FinalAttributes.TryGetValue(AttributeType.Vitality, out int vitVal))
			{
				vit = vitVal;
			}

			// Vitality càng cao, khoảng cách bị đẩy càng thấp.
			// Ví dụ: mỗi 5 VIT giảm ~10% lực knockback, nhưng không giảm quá 70%.
			float vitalityFactor = 1.0f / (1.0f + vit * 0.05f);
			vitalityFactor = Mathf.Clamp(vitalityFactor, 0.3f, 1.0f);
			return baseForce * vitalityFactor;
		}
	}
}
