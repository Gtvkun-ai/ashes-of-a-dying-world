using Godot;
using Godot.Collections;

namespace AshesofaDyingWorld.Quests.Data
{
    public enum QuestType
    {
        Main = 0,
        Side = 1,
        Character = 2
    }

    public enum QuestStatus
    {
        Available = 0,
        Active = 1,
        Completed = 2,
        Failed = 3
    }

    /// <summary>
    /// Định nghĩa bất biến của một nhiệm vụ.
    /// Tiến độ, trạng thái theo dõi và cờ "mới" được lưu ở QuestRuntimeState.
    /// </summary>
    [GlobalClass]
    public partial class QuestData : Resource
    {
        [ExportGroup("Nhận diện")]
        [Export] public string QuestId { get; set; } = "";
        [Export] public string Title { get; set; } = "";
        [Export] public Texture2D Icon { get; set; }
        [Export] public QuestType QuestType { get; set; } = QuestType.Side;
        [Export] public string Chapter { get; set; } = "";

        [ExportGroup("Nội dung")]
        [Export(PropertyHint.MultilineText)] public string Summary { get; set; } = "";
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
        [Export] public string QuestGiver { get; set; } = "";
        [Export] public string LocationName { get; set; } = "";
        [Export] public string MapHint { get; set; } = "";

        [ExportGroup("Điều kiện")]
        [Export] public int RequiredCharacterLevel { get; set; } = 1;
        [Export] public bool StartsActive { get; set; } = false;
        [Export] public bool StartsAsNew { get; set; } = true;

        [ExportGroup("Nội dung nhiệm vụ")]
        [Export] public Array<QuestObjectiveData> Objectives { get; set; } = new();
        [Export] public Array<QuestRewardData> Rewards { get; set; } = new();
    }
}
