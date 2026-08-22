using Godot;
using AshesofaDyingWorld.Gameplay.Events;

namespace AshesofaDyingWorld.Quests.Data
{
    /// <summary>
    /// Dữ liệu định nghĩa một mục tiêu của nhiệm vụ.
    /// Đây là Resource dùng chung, không chứa tiến độ riêng của từng save.
    /// </summary>
    [GlobalClass]
    public partial class QuestObjectiveData : Resource
    {
        [ExportGroup("Nhận diện")]
        [Export] public string ObjectiveId { get; set; } = "";
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";

        [ExportGroup("Tiến độ")]
        [Export] public int RequiredAmount { get; set; } = 1;
        [Export] public int InitialProgress { get; set; } = 0;

        [ExportGroup("Gameplay event binding")]
        [Export] public GameplayEventType ProgressEventType { get; set; } = GameplayEventType.None;
        [Export] public string ProgressTargetId { get; set; } = "";
        [Export] public string ProgressSourceId { get; set; } = "";

        [ExportGroup("Hiển thị")]
        [Export] public string UnitLabel { get; set; } = "";
        [Export] public string RevealAfterObjectiveId { get; set; } = "";
    }
}
