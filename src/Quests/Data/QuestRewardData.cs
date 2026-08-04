using Godot;

namespace AshesofaDyingWorld.Quests.Data
{
    public enum QuestRewardType
    {
        Experience = 0,
        Gold = 1,
        Item = 2,
        SkillPoint = 3
    }

    /// <summary>
    /// Dữ liệu phần thưởng dùng để trình bày trong nhật ký nhiệm vụ.
    /// Logic cộng thưởng có thể nối vào QuestManager khi hệ thống XP/vàng hoàn thiện.
    /// </summary>
    [GlobalClass]
    public partial class QuestRewardData : Resource
    {
        [Export] public QuestRewardType RewardType { get; set; } = QuestRewardType.Experience;
        [Export] public string DisplayName { get; set; } = "";
        [Export] public int Amount { get; set; } = 0;
        [Export] public Texture2D Icon { get; set; }
        [Export] public string ItemResourcePath { get; set; } = "";
    }
}
