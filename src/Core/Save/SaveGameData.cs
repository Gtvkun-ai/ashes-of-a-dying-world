using Godot;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AshesofaDyingWorld.Core.Save
{
    public sealed class SaveGameData
    {
        public int Version { get; set; } = 1;
        public string SavedAtUtc { get; set; } = "";
        public string ScenePath { get; set; } = "";
        public Vector2SaveData PlayerPosition { get; set; } = new();
        public int ActiveCharacterIndex { get; set; } = 0;
        public PlayerSaveData Player { get; set; } = new();
    }

    public sealed class PlayerSaveData
    {
        public string CharacterConfigPath { get; set; } = "";
        public string CharacterId { get; set; } = "";
        public int Level { get; set; } = 1;
        public float CurrentHP { get; set; }
        public float CurrentMP { get; set; }
        public float CurrentStamina { get; set; }
        public List<string> InventoryItemPaths { get; set; } = new();
        public List<EquippedItemSaveData> EquippedItems { get; set; } = new();
        public List<SkillSaveData> ActiveSkills { get; set; } = new();
        public List<SkillCooldownSaveData> SkillCooldowns { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TimedSkillSaveData ActiveTimedSkill { get; set; }
    }

    public sealed class EquippedItemSaveData
    {
        public int Slot { get; set; }
        public string ResourcePath { get; set; } = "";
        public string ItemId { get; set; } = "";
    }

    public sealed class SkillSaveData
    {
        public string SkillKey { get; set; } = "";
        public string ResourcePath { get; set; } = "";
        public string IconPath { get; set; } = "";
        public string SkillName { get; set; } = "";
        public string Description { get; set; } = "";
        public float Duration { get; set; }
        public float MoveSpeedBonusPercent { get; set; }
        public float DexterityBonusPercent { get; set; }
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
