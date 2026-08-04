namespace AshesofaDyingWorld.Quests.Runtime
{
    /// <summary>
    /// Cổng truy cập gọn cho gameplay. Ví dụ khi nhặt hoa:
    /// QuestService.AddProgress("flowers_on_ashes", "collect_purple_flower", 1);
    /// Không cần để các object trong thế giới biết panel UI nằm ở đâu.
    /// </summary>
    public static class QuestService
    {
        public static QuestManager Current { get; internal set; }

        public static bool AddProgress(string questId, string objectiveId, int amount = 1)
        {
            return Current?.AddObjectiveProgress(questId, objectiveId, amount) ?? false;
        }

        public static bool SetProgress(string questId, string objectiveId, int amount)
        {
            return Current?.SetObjectiveProgress(questId, objectiveId, amount) ?? false;
        }
    }
}
