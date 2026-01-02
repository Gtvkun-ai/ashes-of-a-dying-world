using Godot;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class CharacterUnitHUD : PanelContainer
    {
        private PlayerStats _targetStats;

        // Đổi từ ProgressBar sang TextureProgressBar để fix lỗi hiển thị
        [Export] public TextureProgressBar HealthBar;
        [Export] public TextureProgressBar ManaBar;
        [Export] public TextureProgressBar StaminaBar;
        [Export] public Label NameLabel;

        public override void _Ready()
        {
            // Không cần tìm node con thủ công nữa
        }

        public void SetTarget(PlayerStats stats)
        {
            if (_targetStats != null)
                _targetStats.StatsChanged -= UpdateUI;

            _targetStats = stats;
            if (_targetStats != null)
            {
                _targetStats.StatsChanged += UpdateUI;

                if (NameLabel != null && stats.ConfigData != null)
                    NameLabel.Text = stats.ConfigData.Name;

                UpdateUI();
            }
        }

private void UpdateUI()
        {
            if (_targetStats == null) return;

            // 1. CẬP NHẬT THANH MÁU
            if (HealthBar != null)
            {
                // Quan trọng: Phải gán MaxValue trước!
                HealthBar.MaxValue = _targetStats.MaxHP; 
                HealthBar.Value = _targetStats.CurrentHP;
            }

            // 2. CẬP NHẬT THANH MANA
            if (ManaBar != null)
            {
                ManaBar.MaxValue = _targetStats.MaxMP;
                ManaBar.Value = _targetStats.CurrentMP;
            }

            // 3. CẬP NHẬT THANH STAMINA
            if (StaminaBar != null)
            {
                // SỬA LỖI Ở ĐÂY: Gán MaxValue bằng MaxStamina thật của nhân vật (90)
                // Nếu không gán, nó mặc định là 100 -> Hiển thị sai lệch
                StaminaBar.MaxValue = _targetStats.MaxStamina; 
                StaminaBar.Value = _targetStats.CurrentStamina;
            }
        }
        public void SetHighlight(bool isSelected)
        {
            Modulate = isSelected ? new Color(1, 1, 0.5f) : Colors.White;
        }

        public override void _ExitTree()
        {
            if (_targetStats != null)
                _targetStats.StatsChanged -= UpdateUI;
        }
    }
}