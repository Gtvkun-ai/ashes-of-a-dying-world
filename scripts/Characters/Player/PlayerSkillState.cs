namespace AshesofaDyingWorld.Core.Skills
{
    /// <summary>
    /// Trạng thái tiến trình của một kỹ năng đối với riêng người chơi hiện tại.
    /// Không kế thừa Resource vì dữ liệu này thay đổi trong lúc chơi và phải được lưu vào save.
    /// </summary>
    public sealed class PlayerSkillState
    {
        public string SkillId { get; set; } = "";
        public int Level { get; set; } = 1;
        public bool IsUnlocked { get; set; } = true;

        /// <summary>
        /// -1 nghĩa là chưa trang bị; 0..3 tương ứng bốn slot kỹ năng.
        /// </summary>
        public int EquippedSlot { get; set; } = -1;

        public PlayerSkillState Clone()
        {
            return new PlayerSkillState
            {
                SkillId = SkillId,
                Level = Level,
                IsUnlocked = IsUnlocked,
                EquippedSlot = EquippedSlot
            };
        }
    }
}
