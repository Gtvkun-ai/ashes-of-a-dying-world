using Godot;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class HUDManager : Control
    {
        [Export] public PlayerStats PlayerStatsNode;

        //Thanh máu, mana, stamina
        [ExportGroup("Bars")]
        [Export] public TextureProgressBar HealthBar;
        [Export] public TextureProgressBar ManaBar;
        [Export] public TextureProgressBar StaminaBar;

        [ExportGroup("Labels")]
        [Export] public Label HPText; 

        public override void _Ready()
        {
            if (PlayerStatsNode != null)
            {
                // Kết nối tín hiệu thay đổi chỉ số
                PlayerStatsNode.StatsChanged += UpdateHUD;                
                // Cập nhật lần đầu
                UpdateHUD();
            }
        }

        public void UpdateHUD()
        {
        // Cập nhật thanh máu
            UpdateBar(HealthBar, PlayerStatsNode.CurrentHP, PlayerStatsNode.MaxHP);
            // Cập nhật thanh Mana
            UpdateBar(ManaBar, PlayerStatsNode.CurrentMP, PlayerStatsNode.MaxMP);
            // Cập nhật thanh Stamina
            UpdateBar(StaminaBar, PlayerStatsNode.CurrentStamina, PlayerStatsNode.MaxStamina);        
        
        
        if (HPText != null)
        // Cập nhật text HP
                HPText.Text = $"{(int)PlayerStatsNode.CurrentHP}/{(int)PlayerStatsNode.MaxHP}";
        }

        //Hàm cập nhật thanh
        private void UpdateBar(TextureProgressBar bar, float current, float max)
        {
            if (bar != null)
            {
                bar.MaxValue = max;
                bar.Value = current;
            }
        }

        
    }
}