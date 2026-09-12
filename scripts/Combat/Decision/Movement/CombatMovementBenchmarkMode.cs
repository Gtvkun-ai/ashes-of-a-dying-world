namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Loại bài đo movement. Tách riêng vì mỗi bài có "điều kiện thành công" khác nhau:
    /// - StaticPath: đi tới một điểm đứng yên, hợp để đo success/path stretch.
    /// - Follow: bám một formation anchor đang chạy, KHÔNG được abort chỉ vì target di chuyển.
    /// - CombatPositioning: vào vùng chiến thuật quanh target/anchor động.
    /// </summary>
    public enum CombatMovementBenchmarkMode
    {
        StaticPath = 0,
        Follow = 1,
        CombatPositioning = 2
    }
}
