namespace AshesofaDyingWorld.Gameplay.Events
{
    /// <summary>
    /// Bộ loại sự kiện gameplay dùng chung giữa world, combat, quest và các hệ thống runtime khác.
    /// Giữ enum ổn định vì QuestObjectiveData có thể lưu giá trị này trong Resource.
    /// </summary>
    public enum GameplayEventType
    {
        None = 0,
        EnemyDefeated = 1,
        ItemCollected = 2,
        InteractionCompleted = 3,
        AreaEntered = 4,
        ObjectInspected = 5,
        DialogueCompleted = 6,
        SceneEntered = 7,
        Custom = 100
    }
}
