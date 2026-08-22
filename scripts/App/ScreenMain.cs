using Godot;
using AshesofaDyingWorld.UI.HUD;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Core.Save;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.Gameplay.Events;
using AshesofaDyingWorld.Quests.Runtime;

public partial class ScreenMain : Node2D
{
    [Export] public PackedScene PlayerScene { get; set; }
    [Export] public bool AutoEquipStarterWeaponOnSpawn { get; set; } = false;

    private const string WorldPath = "res://scenes/world/whispering_fields/field_01.tscn";
    private const string LegacyWorldPath = "res://scenes/world/WhisperingFields/Field1.tscn";
    private const string PlayerPath = "res://scenes/characters/player/player.tscn";
    private const string PartyHUDPath = "res://scenes/ui/hud/party_hud.tscn";
    private const string GameMenuPath = "res://scenes/ui/menus/game_menu_button.tscn";

    private static readonly Vector2 DefaultSpawn = new(105f, 120f);
    private bool _isStartingGame = false;

    public override void _Ready()
    {
        // Runtime fallback: project zip không cần phụ thuộc autoload để settings/audio hoạt động.
        SettingsManager.GetOrCreate(GetTree());
        PlayerManager.GetOrCreate(GetTree());
        AudioManager.GetOrCreate(GetTree());
        GameplayEventBus.GetOrCreate(GetTree());
        QuestManager.GetOrCreate(GetTree());
    }

    private async void _on_login_pressed()
    {
        await StartGameFromSnapshotAsync(SaveManager.Instance?.LoadSnapshot());
    }

    public async System.Threading.Tasks.Task<Error> StartGameFromSnapshotAsync(SaveGameData saveSnapshot)
    {
        if (_isStartingGame)
        {
            return Error.Busy;
        }

        _isStartingGame = true;
        try
        {
            var tree = GetTree();
            PlayerManager.GetOrCreate(tree);
            AudioManager.GetOrCreate(tree);
            GameplayEventBus.GetOrCreate(tree);
            QuestManager questManager = QuestManager.GetOrCreate(tree);
            questManager?.InitializeFromDirectory();
            if (tree?.Root == null || tree.CurrentScene == null)
            {
                return Error.DoesNotExist;
            }

            string targetWorldPath = ResolveWorldScenePath(saveSnapshot?.ScenePath);

            var worldScene = GD.Load<PackedScene>(targetWorldPath);
            if (worldScene == null)
            {
                GD.PrintErr($"[ScreenMain] Cannot load world scene: {targetWorldPath}");
                return Error.FileNotFound;
            }

            var world = worldScene.Instantiate<Node2D>();

            if (EnemyHealthBarService.Instance == null)
            {
                var enemyHpService = new EnemyHealthBarService();
                tree.Root.AddChild(enemyHpService);
            }
            CombatFeedbackService.GetOrCreate(tree);
            DamageNumberService.GetOrCreate(tree);
            CompanionTargetIndicatorService.GetOrCreate(tree);
            SkillCooldownHudService.GetOrCreate(tree);
            FloatingProgressionHudService.GetOrCreate(tree);

            var playerScene = GD.Load<PackedScene>(PlayerPath);
            if (playerScene == null)
            {
                GD.PrintErr($"[ScreenMain] Cannot load player scene: {PlayerPath}");
                world.QueueFree();
                return Error.FileNotFound;
            }

            var playerInstance = playerScene.Instantiate();
            var player = playerInstance as Player;
            if (player == null)
            {
                GD.PrintErr("Player scene khong chua Player script!");
                playerInstance.QueueFree();
                world.QueueFree();
                return Error.CantCreate;
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
                sceneManager.ConfigurePlayerCamera(world);
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
                return Error.Ok;
            }

            if (AutoEquipStarterWeaponOnSpawn)
            {
                player.AutoEquipStarterWeapon();
            }

            return Error.Ok;
        }
        finally
        {
            _isStartingGame = false;
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

    private static string ResolveWorldScenePath(string savedScenePath)
    {
        if (string.IsNullOrWhiteSpace(savedScenePath))
        {
            return WorldPath;
        }

        string trimmedPath = savedScenePath.Trim();
        if (string.Equals(trimmedPath, LegacyWorldPath, System.StringComparison.OrdinalIgnoreCase))
        {
            return WorldPath;
        }

        if (ResourceLoader.Exists(trimmedPath))
        {
            return trimmedPath;
        }

        GD.PrintErr($"[ScreenMain] Saved world scene no longer exists, using default: {trimmedPath}");
        return WorldPath;
    }
}
