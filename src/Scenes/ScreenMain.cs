using Godot;
using System;

public partial class ScreenMain : Node2D
{
    [Export] public PackedScene PlayerScene { get; set; }

    private const string WorldPath = "res://scenes/world/Whispering Fields/Field1.tscn";
        
    private const string PlayerPath = "res://src/Entities/Player/Player_anim.tscn";

    private const string PartyHUDPath = "res://scenes/ui/PartyHUD.tscn";
    
    private const string GameMenuPath = "res://scenes/ui/GameMenuButton.tscn";

    private static readonly Vector2 DefaultSpawn = new(105f, 120f);

    private void _on_login_pressed()
    {
        var tree = GetTree();
        var world = GD.Load<PackedScene>(WorldPath).Instantiate<Node2D>();
        var playerInstance = GD.Load<PackedScene>(PlayerPath).Instantiate();
        
        // An toàn ép kiểu sang Player
        var player = playerInstance as Player;
        if (player == null)
        {
            GD.PrintErr("PlayerForAnimation scene không chứa Player script!");
            playerInstance.QueueFree();
            return;
        }
        
        var spawn = world.GetNodeOrNull<Node2D>("SpawnPoint");
        player.Position = spawn?.GlobalPosition ?? DefaultSpawn;
        world.AddChild(player);

        // Thêm PartyHUD vào world
        var partyHUD = GD.Load<PackedScene>(PartyHUDPath).Instantiate();
        world.AddChild(partyHUD);
        GD.Print("[ScreenMain] PartyHUD added to world");

        // Thêm GameMenuButton vào world (SỬA: Đúng path, không phải PartyHUDPath)
        var gameMenu = GD.Load<PackedScene>(GameMenuPath).Instantiate();
        world.AddChild(gameMenu);
        GD.Print("[ScreenMain] GameMenuButton added to world");

        // CHỈ THÊM 1 LẦN vào root
        tree.Root.AddChild(world);
        tree.CurrentScene.QueueFree();
        tree.CurrentScene = world;

        // Kích hoạt camera
        var cam = player.GetNodeOrNull<Camera2D>("follow");
        if (cam != null)
        {
            cam.Zoom = new Godot.Vector2(2f, 2f);
            cam.CallDeferred("make_current");
        }

        // Thiết lập player trong SceneManager
        var sceneManager = tree.Root.GetNodeOrNull<SceneManager>("SceneManager");
        if (sceneManager != null)
        {
            sceneManager.SetPlayer(player);
        }
        else
        {
            GD.PrintErr("Không tìm thấy SceneManager để set player");
        }
    }

    private void _on_exits_pressed() => GetTree().Quit();
}