using Godot;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.Core.Managers;

public partial class CharacterDetailUI : Panel
{
    private Label NameLabel;
    private Label LevelLabel;
    private Label RaceLabel;
    private Label StatsTextLabel;

    public override void _Ready()
    {
        // Tạo các Label bằng code
        var vbox = new VBoxContainer();
        AddChild(vbox);
        
        NameLabel = new Label();
        LevelLabel = new Label();
        RaceLabel = new Label();
        StatsTextLabel = new Label();
        
        vbox.AddChild(NameLabel);
        vbox.AddChild(LevelLabel);
        vbox.AddChild(RaceLabel);
        vbox.AddChild(StatsTextLabel);
        
        VisibilityChanged += OnVisibilityChanged;
    }

    private void OnVisibilityChanged()
    {
        if(Visible)
        {
            UpdateCharacterInfo();
        }
    }

    public void UpdateCharacterInfo()
    {
        //Lấy player hiện tại từ player Manager
        var activceIndex = PlayerManager.Instance.ActiveCharacterIndex; 
        if(activceIndex >= PlayerManager.Instance.PartyMembers.Count) return;

        PlayerStats currentStats = PlayerManager.Instance.PartyMembers[activceIndex];
        
        // Kiểm tra null để tránh lỗi
        if(currentStats == null || currentStats.ConfigData == null)
        {
            GD.PrintErr("CharacterDetailUI: Dữ liệu nhân vật không tồn tại!");
            NameLabel.Text = "Lỗi: Không có dữ liệu";
            return;
        }
        
        var Config = currentStats.ConfigData;
        
        // Kiểm tra Race có tồn tại không
        if(Config.CharacterRace == null)
        {
            RaceLabel.Text = "Tộc: Không xác định";
        }
        else
        {
            RaceLabel.Text = $"Tộc: {Config.CharacterRace.RaceName}";
        }

        // Hiển thị thông tin cơ bản
        NameLabel.Text = $"Tên: {Config.Name}"; 
        LevelLabel.Text = $"Cấp độ: {currentStats.CurrentLevel}";

        //Hiển thị thông tin chi tiết
        string details = "--- CHỈ SỐ ---\n";

        if(currentStats.FinalAttributes != null)
        {
            foreach (var attr in currentStats.FinalAttributes)
            {
                details += $"{attr.Key}: {attr.Value}\n";
            }
        }

        details += $"\nSát thương: {currentStats.AttackDamage}";
        details += $"\nPhòng thủ: {currentStats.Armor}";

        StatsTextLabel.Text = details;
    }
}
