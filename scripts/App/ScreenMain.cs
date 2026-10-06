using Godot;
using AshesofaDyingWorld.UI.HUD;
using AshesofaDyingWorld.Core.Managers;
using AshesofaDyingWorld.Core.Save;
using AshesofaDyingWorld.Combat.Runtime;
using AshesofaDyingWorld.Gameplay.Events;
using AshesofaDyingWorld.Quests.Runtime;
using AshesofaDyingWorld.UI.Shared;
using AshesofaDyingWorld.UI.Theme;
using AshesofaDyingWorld.World.Environment;

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
    private SaveGameData _currentSnapshot;
    private Control _mainUi;
    private Button _continueButton;
    private Button _newGameButton;
    private Button _settingsButton;
    private Button _exitButton;
    private SettingsPanel _settingsPanel;
    private ConfirmationDialog _newGameConfirmation;
    private Viewport _viewport;

    public override void _Ready()
    {
        CacheMainMenuControls();
        _viewport = GetViewport();
        if (_viewport != null)
        {
            _viewport.SizeChanged += FitRootControlsToViewport;
        }
        FitRootControlsToViewport();
        // Runtime fallback: project zip không cần phụ thuộc autoload để settings/audio hoạt động.
        CallDeferred(nameof(BootstrapRuntimeServices));
        CallDeferred(nameof(RefreshMainMenuStateFromSave));
    }

    private void CacheMainMenuControls()
    {
        _mainUi = GetNodeOrNull<Control>("MainUi");
        UiThemeFactory.Apply(_mainUi);

        const string railPath = "MainUi/SafeMargin/Row/MenuSurface/RailMargin/Rail";
        _continueButton = GetNodeOrNull<Button>($"{railPath}/login");
        _newGameButton = GetNodeOrNull<Button>($"{railPath}/new_game");
        _settingsButton = GetNodeOrNull<Button>($"{railPath}/settings");
        _exitButton = GetNodeOrNull<Button>($"{railPath}/exits");
        _settingsPanel = GetNodeOrNull<SettingsPanel>("SettingsPanel");
        _newGameConfirmation = GetNodeOrNull<ConfirmationDialog>("NewGameConfirmation");

        PixelButtonSkin.ApplyPrimary(_continueButton, PixelButtonSkin.LargeActionHeight);
        PixelButtonSkin.ApplySecondary(_newGameButton, PixelButtonSkin.RegularHeight);
        PixelButtonSkin.ApplySecondary(_settingsButton, PixelButtonSkin.RegularHeight);
        PixelButtonSkin.ApplyDanger(_exitButton, PixelButtonSkin.RegularHeight);

        if (_settingsButton != null)
        {
            _settingsButton.Icon = UiGlyphResolver.Resolve(UiGlyph.Settings);
            _settingsButton.IconAlignment = HorizontalAlignment.Left;
        }

        if (_exitButton != null)
        {
            _exitButton.Icon = UiGlyphResolver.Resolve(UiGlyph.Exit);
            _exitButton.IconAlignment = HorizontalAlignment.Left;
        }

        ApplyMainMenuTypography(railPath);

        if (_newGameConfirmation != null)
        {
            _newGameConfirmation.Theme = UiThemeFactory.GetSharedTheme();
            _newGameConfirmation.Confirmed += OnNewGameConfirmed;
        }
    }

    private void FitRootControlsToViewport()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        FitTopLevelControl(_mainUi, viewportSize);
        FitTopLevelControl(_settingsPanel, viewportSize);
    }

    private static void FitTopLevelControl(Control control, Vector2 size)
    {
        if (control == null || size.X <= 0f || size.Y <= 0f)
        {
            return;
        }

        control.AnchorLeft = 0f;
        control.AnchorTop = 0f;
        control.AnchorRight = 0f;
        control.AnchorBottom = 0f;
        control.Position = Vector2.Zero;
        control.Size = size;
    }

    public override void _ExitTree()
    {
        if (_viewport != null)
        {
            _viewport.SizeChanged -= FitRootControlsToViewport;
            _viewport = null;
        }
    }

    private void ApplyMainMenuTypography(string railPath)
    {
        Label eyebrow = GetNodeOrNull<Label>($"{railPath}/Eyebrow");
        Label title = GetNodeOrNull<Label>($"{railPath}/Title");
        Label subtitle = GetNodeOrNull<Label>($"{railPath}/Subtitle");
        Label location = GetNodeOrNull<Label>($"{railPath}/Location");

        UiThemeFactory.ApplyText(eyebrow, UiTextRole.Label);
        UiThemeFactory.ApplyText(title, UiTextRole.ScreenTitle);
        UiThemeFactory.ApplyText(subtitle, UiTextRole.Body);
        UiThemeFactory.ApplyText(location, UiTextRole.Micro);

        title?.AddThemeFontSizeOverride("font_size", 36);
        eyebrow?.AddThemeColorOverride("font_color", UiTokens.Accent);
    }

    private void RefreshMainMenuStateFromSave()
    {
        RefreshMainMenuState(SaveManager.Instance?.LoadSnapshot());
    }

    public void RefreshMainMenuState(SaveGameData snapshot)
    {
        _currentSnapshot = snapshot;
        bool hasSave = snapshot != null;

        if (_continueButton != null)
        {
            _continueButton.Text = hasSave ? "Tiếp tục" : "Bắt đầu";
            _continueButton.Visible = true;
        }

        if (_newGameButton != null)
        {
            _newGameButton.Visible = hasSave;
        }

        CallDeferred(nameof(FocusPrimaryAction));
    }

    private void FocusPrimaryAction()
    {
        if (_continueButton?.IsVisibleInTree() == true)
        {
            _continueButton.GrabFocus();
        }
    }

    public void BootstrapRuntimeServices()
    {
        SettingsManager.GetOrCreate(GetTree());
        PlayerManager.GetOrCreate(GetTree());
        AudioManager.GetOrCreate(GetTree());
        GameplayEventBus.GetOrCreate(GetTree());
        QuestManager.GetOrCreate(GetTree());
        WorldEnvironmentService.GetOrCreate(GetTree());
    }

    private async void _on_login_pressed()
    {
        await StartGameFromSnapshotAsync(_currentSnapshot);
    }

    private async void _on_new_game_pressed()
    {
        await StartNewGameAsync();
    }

    private void _on_settings_pressed()
    {
        _settingsPanel?.Show();
    }

    public async System.Threading.Tasks.Task<Error> StartNewGameAsync()
    {
        bool saveExists = _currentSnapshot != null || SaveManager.Instance?.HasSaveGame() == true;
        if (saveExists)
        {
            _newGameConfirmation?.PopupCentered();
            return Error.Busy;
        }

        return await StartGameFromSnapshotAsync(null);
    }

    private async void OnNewGameConfirmed()
    {
        _currentSnapshot = null;
        await StartGameFromSnapshotAsync(null);
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
            WorldEnvironmentService environment = WorldEnvironmentService.GetOrCreate(tree);
            if (saveSnapshot?.WorldEnvironment != null)
            {
                environment?.RestoreClock(
                    saveSnapshot.WorldEnvironment.Day,
                    saveSnapshot.WorldEnvironment.TimeOfDayHours);
            }
            else
            {
                environment?.ResetForNewGame();
            }

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

            EnsureWorldHudServices(tree);

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

    private static void EnsureWorldHudServices(SceneTree tree)
    {
        if (tree?.Root == null)
        {
            return;
        }

        if (EnemyHealthBarService.Instance == null)
        {
            tree.Root.AddChild(new EnemyHealthBarService());
        }

        CombatFeedbackService.GetOrCreate(tree);
        DamageNumberService.GetOrCreate(tree);
        CompanionTargetIndicatorService.GetOrCreate(tree);
        FloatingProgressionHudService.GetOrCreate(tree);
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
