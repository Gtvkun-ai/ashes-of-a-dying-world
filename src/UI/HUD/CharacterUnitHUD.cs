using Godot;
using AshesofaDyingWorld.Entities.Player;

namespace AshesofaDyingWorld.UI.HUD
{
    public partial class CharacterUnitHUD : PanelContainer
    {
        private PlayerStats _targetStats;

        [Export] public TextureProgressBar HealthBar;
        [Export] public TextureProgressBar ManaBar;
        [Export] public TextureProgressBar StaminaBar;
        [Export] public Label NameLabel;
        [Export] public TextureRect Portrait;
        
        private TextureRect frameBackground;
        private ShaderMaterial shaderMaterial;
        private const string ShaderPath = "res://assets/shader/outline.gdshader";

        public override void _Ready()
        {
            if(Portrait == null)
            {
                Portrait = GetNode<TextureRect>("TextureRect/Portrait");
            }

            // Lấy TextureRect đã có trong scene (background frame)
            frameBackground = GetNode<TextureRect>("TextureRect");
            
            // Load shader và áp dụng vào frameBackground
            var shader = GD.Load<Shader>(ShaderPath);
            if (shader != null && frameBackground != null)
            {
                shaderMaterial = new ShaderMaterial();
                shaderMaterial.Shader = shader;
                frameBackground.Material = shaderMaterial;
                
                shaderMaterial.SetShaderParameter("line_thickness", 0.0f);
                shaderMaterial.SetShaderParameter("line_color", new Color(1.0f, 1.0f, 1.0f, 1.0f));                
                GD.Print("[CharacterUnitHUD] Shader applied to frameBackground");
            }
            else
            {
                GD.PrintErr($"[CharacterUnitHUD] Shader or frameBackground not found");
            }
        }

        public void SetTarget(PlayerStats stats)
        {
            if (_targetStats != null)
                _targetStats.StatsChanged -= UpdateUI;

            _targetStats = stats;
            if (_targetStats != null)
            {
                _targetStats.StatsChanged += UpdateUI;

                if (stats.ConfigData != null)
                {
                    if (NameLabel != null)
                        NameLabel.Text = stats.ConfigData.Name;
                    
                    if (Portrait != null && stats.ConfigData.Icon != null)
                        Portrait.Texture = stats.ConfigData.Icon;
                }

                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (_targetStats == null) return;

            if (HealthBar != null)
            {
                HealthBar.MaxValue = _targetStats.MaxHP; 
                HealthBar.Value = _targetStats.CurrentHP;
            }

            if (ManaBar != null)
            {
                ManaBar.MaxValue = _targetStats.MaxMP;
                ManaBar.Value = _targetStats.CurrentMP;
            }

            if (StaminaBar != null)
            {
                StaminaBar.MaxValue = _targetStats.MaxStamina; 
                StaminaBar.Value = _targetStats.CurrentStamina;
            }
        }

        public void ApplyHighlight(bool isSelected)
        {
            if (shaderMaterial != null)
            {
                shaderMaterial.SetShaderParameter("line_thickness", isSelected ? 20.0f : 0.0f);
                GD.Print($"[CharacterUnitHUD] Highlight {(isSelected ? "ON" : "OFF")}");
            }
        }
        
        public override void _ExitTree()
        {
            if (_targetStats != null)
                _targetStats.StatsChanged -= UpdateUI;
        }
    }
}