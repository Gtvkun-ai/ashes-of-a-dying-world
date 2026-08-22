using Godot;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AshesofaDyingWorld.Core.Save
{
    public sealed class SaveGameData
    {
        // Version 8 bổ sung WorldEnvironment clock; save cũ vẫn tải với thời gian mặc định.
        public int Version { get; set; } = 8;
        public string SavedAtUtc { get; set; } = "";
        public string ScenePath { get; set; } = "";
        public Vector2SaveData PlayerPosition { get; set; } = new();
        public int ActiveCharacterIndex { get; set; } = 0;

        /// <summary>
        /// Thứ tự thành viên trong panel Tổ đội, lưu bằng CharacterConfig.ID.
        /// Không lưu NodePath vì node có thể thay đổi khi chuyển scene.
        /// </summary>
        public List<string> PartyOrderCharacterIds { get; set; } = new();
        public Dictionary<string, int> CompanionCommandModes { get; set; } = new();

        public PlayerSaveData Player { get; set; } = new();
        public List<PartySkillProgressSaveData> PartySkillProgress { get; set; } = new();
        public List<QuestProgressSaveData> QuestProgress { get; set; } = new();
        public string TrackedQuestId { get; set; } = "";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorldEnvironmentSaveData WorldEnvironment { get; set; }
    }

    public sealed class WorldEnvironmentSaveData
    {
        public int Day { get; set; } = 1;
        public float TimeOfDayHours { get; set; } = 12f;
    }

    public sealed class PlayerSaveData
    {
        public string CharacterConfigPath { get; set; } = "";
        public string CharacterId { get; set; } = "";
        public int Level { get; set; } = 1;
        public int Experience { get; set; } = 0;
        public float CurrentHP { get; set; }
        public float CurrentMP { get; set; }
        public float CurrentStamina { get; set; }
        public List<string> InventoryItemPaths { get; set; } = new();
        public List<EquippedItemSaveData> EquippedItems { get; set; } = new();

        /// <summary>
        /// Dữ liệu mới: chỉ lưu phần thay đổi theo người chơi.
        /// Tên, icon, damage, mana cost... luôn được đọc từ SkillData hiện hành.
        /// </summary>
        public List<SkillStateSaveData> SkillStates { get; set; } = new();
        public int UnspentSkillPoints { get; set; } = 0;

        /// <summary>
        /// Dữ liệu cũ của save version 2. Chỉ giữ để migrate, save mới không cần ghi nội dung vào đây.
        /// </summary>
        public List<SkillSaveData> ActiveSkills { get; set; } = new();

        public List<SkillCooldownSaveData> SkillCooldowns { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TimedSkillSaveData ActiveTimedSkill { get; set; }
    }

    /// <summary>Tiến trình kỹ năng của companion trong party.</summary>
    public sealed class PartySkillProgressSaveData
    {
        public string CharacterId { get; set; } = "";
        public int Level { get; set; } = 1;
        public int Experience { get; set; } = 0;
        public List<SkillStateSaveData> SkillStates { get; set; } = new();
        public int UnspentSkillPoints { get; set; }
    }

    /// <summary>Trạng thái một nhiệm vụ trong save version 4.</summary>
    public sealed class QuestProgressSaveData
    {
        public string QuestId { get; set; } = "";
        public int Status { get; set; } = 0;
        public bool IsNew { get; set; } = true;
        public List<QuestObjectiveProgressSaveData> Objectives { get; set; } = new();
    }

    /// <summary>Tiến độ của từng mục tiêu, tách khỏi Resource định nghĩa.</summary>
    public sealed class QuestObjectiveProgressSaveData
    {
        public string ObjectiveId { get; set; } = "";
        public int Progress { get; set; } = 0;
    }

    public sealed class EquippedItemSaveData
    {
        public int Slot { get; set; }
        public string ResourcePath { get; set; } = "";
        public string ItemId { get; set; } = "";
    }

    /// <summary>
    /// Trạng thái kỹ năng gọn nhẹ trong save version 3.
    /// </summary>
    public sealed class SkillStateSaveData
    {
        public string SkillId { get; set; } = "";
        public int Level { get; set; } = 1;
        public bool IsUnlocked { get; set; } = true;
        public int EquippedSlot { get; set; } = -1;
    }

    /// <summary>
    /// Schema legacy của save version 2.
    /// Không xóa ngay để người chơi vẫn tải được save cũ, nhưng code mới không dùng nó làm nguồn sự thật.
    /// </summary>
    public sealed class SkillSaveData
    {
        public string SkillKey { get; set; } = "";
        public string ResourcePath { get; set; } = "";
        public string IconPath { get; set; } = "";
        public string SkillName { get; set; } = "";
        public string Description { get; set; } = "";
        public int Category { get; set; } = 0;
        public int Element { get; set; } = 0;
        public int MaxLevel { get; set; } = 1;
        public int ExecutionType { get; set; } = 0;
        public string CombatActionPath { get; set; } = "";
        public bool CanUseWhileBlocking { get; set; }
        public float Duration { get; set; }
        public float MoveSpeedBonusPercent { get; set; }
        public float DexterityBonusPercent { get; set; }
        public float HealAmount { get; set; }
        public float RestoreStaminaAmount { get; set; }
        public float RestoreGuardAmount { get; set; }
        public float Cooldown { get; set; }
        public float DamageMultiplier { get; set; } = 1.0f;
        public int ManaCost { get; set; }
        public int StaminaCost { get; set; }
        public string AnimationName { get; set; } = "";
    }

    public sealed class SkillCooldownSaveData
    {
        public string SkillKey { get; set; } = "";
        public float Remaining { get; set; }
    }

    public sealed class TimedSkillSaveData
    {
        public string SkillKey { get; set; } = "";
        public float Remaining { get; set; }
        public float CooldownRemaining { get; set; }
    }

    public sealed class Vector2SaveData
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Vector2 ToVector2()
        {
            return new Vector2(X, Y);
        }

        public static Vector2SaveData FromVector2(Vector2 value)
        {
            return new Vector2SaveData
            {
                X = value.X,
                Y = value.Y
            };
        }
    }
}
