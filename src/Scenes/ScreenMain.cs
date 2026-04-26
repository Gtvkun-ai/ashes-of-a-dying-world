using Godot;
using AshesofaDyingWorld.UI.HUD;
using AshesofaDyingWorld.Core.Managers;

public partial class ScreenMain : Node2D
{
    [Export] public PackedScene PlayerScene { get; set; }
    [Export] public bool AutoEquipStarterWeaponOnSpawn { get; set; } = false;

    private const string WorldPath = "res://scenes/world/WhisperingFields/Field1.tscn";
    private const string PlayerPath = "res://src/Entities/Player/Player_anim.tscn";
    private const string PartyHUDPath = "res://scenes/ui/PartyHUD.tscn";
    private const string GameMenuPath = "res://scenes/ui/GameMenuButton.tscn";

    private static readonly Vector2 DefaultSpawn = new(105f, 120f);

    private async void _on_login_pressed()
    {
        var tree = GetTree();
        var saveSnapshot = SaveManager.Instance?.LoadSnapshot();
        string targetWorldPath = saveSnapshot?.ScenePath ?? WorldPath;

        var worldScene = GD.Load<PackedScene>(targetWorldPath);
        if (worldScene == null)
        {
            GD.PrintErr($"[ScreenMain] Cannot load world scene: {targetWorldPath}");
            return;
        }

        var world = worldScene.Instantiate<Node2D>();

        if (EnemyHealthBarService.Instance == null)
        {
            var enemyHpService = new EnemyHealthBarService();
            tree.Root.AddChild(enemyHpService);
        }

        var playerScene = GD.Load<PackedScene>(PlayerPath);
        if (playerScene == null)
        {
            GD.PrintErr($"[ScreenMain] Cannot load player scene: {PlayerPath}");
            world.QueueFree();
            return;
        }

        var playerInstance = playerScene.Instantiate();
        var player = playerInstance as Player;
        if (player == null)
        {
            GD.PrintErr("Player scene khong chua Player script!");
            playerInstance.QueueFree();
            world.QueueFree();
            return;
        }

        var spawn = world.GetNodeOrNull<Node2D>("SpawnPoint");
        Vector2 spawnPosition = saveSnapshot?.PlayerPosition?.ToVector2()
            ?? spawn?.GlobalPosition
            ?? DefaultSpawn;
        player.Position = spawnPosition;
        world.AddChild(player);

        tree.Root.AddChild(world);
        tree.CurrentScene.QueueFree();
        tree.CurrentScene = world;

        var cam = player.GetNodeOrNull<Camera2D>("follow");
        if (cam != null)
        {
            cam.Zoom = new Vector2(2f, 2f);
            cam.CallDeferred("make_current");
        }

        var sceneManager = tree.Root.GetNodeOrNull<SceneManager>("SceneManager");
        if (sceneManager != null)
        {
            sceneManager.SetPlayer(player);
            sceneManager.EnsureWorldUi(world);
        }
        else
        {
            GD.PrintErr("Khong tim thay SceneManager de set player");
            AddWorldUiFallback(world);
        }

        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        if (saveSnapshot != null && SaveManager.Instance != null)
        {
            SaveManager.Instance.ApplyLoadedGame(player, saveSnapshot);
            return;
        }

        if (AutoEquipStarterWeaponOnSpawn)
        {
            player.AutoEquipStarterWeapon();
        }
    }

    private void _on_exits_pressed()
    {
        GetTree().Quit();
    }

    private void AddWorldUiFallback(Node world)
    {
        var partyHudScene = GD.Load<PackedScene>(PartyHUDPath);
        if (partyHudScene != null && world.GetNodeOrNull("PartyHUD") == null)
        {
            world.AddChild(partyHudScene.Instantiate());
        }

        var gameMenuScene = GD.Load<PackedScene>(GameMenuPath);
        if (gameMenuScene != null && world.GetNodeOrNull("GameMenuButton") == null)
        {
            world.AddChild(gameMenuScene.Instantiate());
        }
    }
}
