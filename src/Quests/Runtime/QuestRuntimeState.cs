using System.Collections.Generic;
using AshesofaDyingWorld.Quests.Data;

namespace AshesofaDyingWorld.Quests.Runtime
{
    /// <summary>
    /// Trạng thái thay đổi theo người chơi. Không ghi tiến độ trực tiếp vào QuestData,
    /// nếu không mọi nơi dùng cùng Resource sẽ vô tình dùng chung một tiến trình.
    /// </summary>
    public sealed class QuestRuntimeState
    {
        public string QuestId { get; set; } = "";
        public QuestStatus Status { get; set; } = QuestStatus.Available;
        public bool IsNew { get; set; } = true;
        public Dictionary<string, int> ObjectiveProgress { get; set; } = new();
    }

    /// <summary>
    /// Bản ghi trung gian để SaveManager chuyển đổi sang DTO JSON.
    /// </summary>
    public sealed class QuestProgressRecord
    {
        public string QuestId { get; set; } = "";
        public QuestStatus Status { get; set; } = QuestStatus.Available;
        public bool IsNew { get; set; } = true;
        public Dictionary<string, int> ObjectiveProgress { get; set; } = new();
    }
}
