using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AshesofaDyingWorld.Core.Data;
using AshesofaDyingWorld.Core.Save;
using AshesofaDyingWorld.Core.Skills;
using AshesofaDyingWorld.Entities.Player;
using AshesofaDyingWorld.UI.Menus;
using AshesofaDyingWorld.Quests.Data;
using AshesofaDyingWorld.Quests.Runtime;

namespace AshesofaDyingWorld.Core.Managers
{
    public partial class SaveManager : Node
    {
        public static SaveManager Instance { get; private set; }

        private const string SavePath = "user://savegame.json";
        private const string MainScenePath = "res://scenes/app/screen_main.tscn";
        private const Key QuickSaveKey = Key.S;
        private const Key QuickLoadKey = Key.L;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            {
                return;
            }

            if (MatchesShortcut(keyEvent, QuickSaveKey, requireCtrl: true))
            {
                SaveGame();
                GetViewport().SetInputAsHandled(); // Ngăn chặn sự kiện tiếp tục nếu đã xử lý lưu game
                return;
            }

            if (MatchesShortcut(keyEvent, QuickLoadKey, requireCtrl: true))
            {
                _ = LoadGameAsync();
                GetViewport().SetInputAsHandled();  // Ngăn chặn sự kiện tiếp tục nếu đã xử lý tải game
            }
        }

        private static bool MatchesShortcut(
            InputEventKey keyEvent,
            Key key,
            bool requireCtrl = false,
            bool requireShift = false,
            bool requireAlt = false)
        {
            if (keyEvent.Keycode != key)
            {
                return false;
            }

            return keyEvent.CtrlPressed == requireCtrl
                && keyEvent.ShiftPressed == requireShift
                && keyEvent.AltPressed == requireAlt;
        }

        public bool HasSaveGame()
        {
            return FileAccess.FileExists(SavePath);
        }

        public SaveGameData LoadSnapshot()
        {
            if (!HasSaveGame())
            {
                return null;
            }

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read); // Sử dụng 'using' để đảm bảo file được đóng sau khi đọc
            if (file == null)
            {
                GD.PrintErr($"[SaveManager] Cannot open save for reading: {SavePath}");
                return null;
            }

            string json = file.GetAsText();
            if (string.IsNullOrWhiteSpace(json))
            {
                GD.PrintErr("[SaveManager] Save file is empty.");
                return null;
            }

            try
            {
                SaveGameData snapshot = JsonSerializer.Deserialize<SaveGameData>(json, _jsonOptions);
                MigrateLegacyPaths(snapshot);
                return snapshot;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveManager] Failed to parse save file: {ex.Message}");
                return null;
            }
        }

        // Phương thức lưu game, trả về Error.Ok nếu thành công hoặc lỗi cụ thể nếu thất bại
        private static void MigrateLegacyPaths(SaveGameData snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.ScenePath = NormalizeSavedResourcePath(snapshot.ScenePath);

            PlayerSaveData player = snapshot.Player;
            if (player == null)
            {
                return;
            }

            player.CharacterConfigPath = NormalizeSavedResourcePath(player.CharacterConfigPath);

            if (player.InventoryItemPaths != null)
            {
                for (int i = 0; i < player.InventoryItemPaths.Count; i++)
                {
                    player.InventoryItemPaths[i] = NormalizeSavedResourcePath(player.InventoryItemPaths[i]);
                }
            }

            if (player.EquippedItems != null)
            {
                foreach (EquippedItemSaveData item in player.EquippedItems)
                {
                    if (item != null)
                    {
                        item.ResourcePath = NormalizeSavedResourcePath(item.ResourcePath);
                    }
                }
            }

            if (player.ActiveSkills != null)
            {
                foreach (SkillSaveData skill in player.ActiveSkills)
                {
                    if (skill == null)
                    {
                        continue;
                    }

                    skill.ResourcePath = NormalizeSavedResourcePath(skill.ResourcePath);
                    skill.IconPath = NormalizeSavedResourcePath(skill.IconPath);
                    skill.CombatActionPath = NormalizeSavedResourcePath(skill.CombatActionPath);
                }
            }
        }

        private static string NormalizeSavedResourcePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path ?? string.Empty;
            }

            string trimmedPath = path.Trim();
            string lowerPath = trimmedPath.ToLowerInvariant();

            return lowerPath switch
            {
                "res://scenes/world/whisperingfields/field1.tscn" => "res://scenes/world/whispering_fields/field_01.tscn",
                "res://scenes/world/whispering fields/field1.tscn" => "res://scenes/world/whispering_fields/field_01.tscn",
                "res://assets/resources/data/characters/main.tres" => "res://data/characters/main.tres",
                "res://assets/resources/data/characters/hyou.tres" => "res://data/characters/hyou.tres",
                "res://assets/resources/data/weapons/sword/woodsword.tres" => "res://data/weapons/sword/wood_sword.tres",
                "res://assets/resources/data/icon/dex.tres" => "res://data/icons/dex.tres",
                "res://assets/resources/data/icon/str .tres" => "res://data/icons/str.tres",
                "res://assets/resources/data/icon/str.tres" => "res://data/icons/str.tres",
                "res://assets/resources/data/icon/def.tres" => "res://data/icons/def.tres",
                "res://assets/resources/data/icon/vit.tres" => "res://data/icons/vit.tres",
                "res://assets/resources/data/icon/int.tres" => "res://data/icons/int.tres",
                "res://assets/resources/data/icon/spi.tres" => "res://data/icons/spi.tres",
                "res://assets/resources/data/icon/default_skill.tres" => "res://data/icons/default_skill.tres",
                _ => trimmedPath
            };
        }

        public Error SaveGame()
        {
            SaveGameData snapshot = CaptureCurrentGame(); // Chụp lại trạng thái hiện tại của game để lưu
            if (snapshot == null)
            {
                return Error.Failed;
            }

            string json;
            try
            {
                json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveManager] Failed to serialize save data: {ex.Message}");
                return Error.Failed;
            }

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write); // Sử dụng 'using' để đảm bảo file được đóng sau khi ghi
            if (file == null)
            {
                Error openError = FileAccess.GetOpenError();
                GD.PrintErr($"[SaveManager] Cannot open save for writing: {openError}");
                return openError;
            }

            file.StoreString(json);
            file.Flush();
            return Error.Ok;
        }

        public async Task<Error> LoadGameAsync()
        {
            try
            {
                SaveGameData snapshot = LoadSnapshot();
                if (snapshot == null)
                {
                    return Error.FileNotFound;
                }

                SceneTree tree = GetTree();
                if (tree == null)
                {
                    return Error.DoesNotExist;
                }

                if (!ResourceLoader.Exists(MainScenePath))
                {
                    GD.PrintErr($"[SaveManager] Main scene is missing: {MainScenePath}");
                    return Error.FileNotFound;
                }

                SceneManager sceneManager = tree.Root.GetNodeOrNull<SceneManager>("SceneManager");
                if (sceneManager == null)
                {
                    GD.PrintErr("[SaveManager] Cannot load because SceneManager is missing.");
                    return Error.DoesNotExist;
                }

                tree.Paused = false;
                tree.Root?.GuiReleaseFocus();

                PlayerManager.Instance?.ResetParty();
                sceneManager.SetPlayer(null);

                Error sceneChangeError = tree.ChangeSceneToFile(MainScenePath);
                if (sceneChangeError != Error.Ok)
                {
                    return sceneChangeError;
                }

                const int maxFramesToWait = 30;
                Node currentScene = null;
                Button loginButton = null;

                for (int i = 0; i < maxFramesToWait; i++)
                {
                    await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

                    currentScene = tree.CurrentScene;
                    if (currentScene == null)
                    {
                        continue;
                    }

                    loginButton = currentScene.GetNodeOrNull<Button>("login");
                    if (loginButton != null)
                    {
                        break;
                    }
                }

                if (currentScene == null)
                {
                    GD.PrintErr("[SaveManager] Main scene failed to load after waiting.");
                    return Error.DoesNotExist;
                }

                if (loginButton == null)
                {
                    GD.PrintErr("[SaveManager] Login button not found on main scene after waiting.");
                    return Error.DoesNotExist;
                }

                loginButton.EmitSignal(Button.SignalName.Pressed);
                return Error.Ok;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveManager] Load failed with exception: {ex}");
                return Error.Failed;
            }
        }

        public void ApplyLoadedGame(Player player, SaveGameData snapshot)
        {
            if (player == null || snapshot?.Player == null)
            {
                return;
            }

            PlayerSaveData playerData = snapshot.Player;
            PlayerStats stats = player.GetStatsNode();
            if (stats == null)
            {
                GD.PrintErr("[SaveManager] PlayerStats is missing; cannot apply save.");
                return;
            }

            if (!string.IsNullOrEmpty(playerData.CharacterConfigPath))
            {
                CharacterConfig config = GD.Load<CharacterConfig>(playerData.CharacterConfigPath);
                if (config != null)
                {
                    stats.SetCharacterConfig(config);
                }
            }

            stats.RestoreProgression(playerData.Level, playerData.Experience);

            InventoryManager inventory = player.GetInventoryManager();
            inventory?.RestoreItems(playerData.InventoryItemPaths);

            EquipmentManager equipment = player.GetEquipmentManager();
            equipment?.RestoreEquipment(playerData.EquippedItems);

            player.RestoreSavedSkills(
                playerData.ActiveSkills,
                playerData.SkillStates,
                playerData.UnspentSkillPoints,
                playerData.SkillCooldowns,
                playerData.ActiveTimedSkill);

            RestorePartySkillProgress(player, snapshot.PartySkillProgress);

            stats.RestoreResourceValues(
                playerData.CurrentHP,
                playerData.CurrentMP,
                playerData.CurrentStamina);

            if (snapshot.PlayerPosition != null)
            {
                player.GlobalPosition = snapshot.PlayerPosition.ToVector2();
            }

            // Khôi phục thứ tự trước, sau đó mới áp dụng index đội trưởng đã lưu.
            // Làm ngược lại sẽ khiến index trỏ sang người khác sau khi reorder.
            PlayerManager.Instance?.RestorePartyOrder(snapshot.PartyOrderCharacterIds);
            PlayerManager.Instance?.RestoreCompanionCommands(snapshot.CompanionCommandModes);
            PlayerManager.Instance?.SetActiveCharacter(snapshot.ActiveCharacterIndex);

            // Đảm bảo người chơi không bị kẹt trạng thái sau khi load (đang attack/knockback/pause).
            player.ResetTransientStateAfterLoad();

            SceneTree tree = GetTree();
            if (tree != null)
            {
                tree.Paused = false;
                tree.Root?.GuiReleaseFocus();

                Node currentScene = tree.CurrentScene;
                if (currentScene != null)
                {
                    GameMenuButton gameMenu = currentScene.GetNodeOrNull<GameMenuButton>("GameMenuButton");
                    RestoreQuestProgress(gameMenu, snapshot.QuestProgress, snapshot.TrackedQuestId);
                    gameMenu?.ResetUiStateAfterLoad();
                }
            }
        }

        private List<PartySkillProgressSaveData> CapturePartySkillProgress(Player player)
        {
            var result = new List<PartySkillProgressSaveData>();
            if (PlayerManager.Instance == null) return result;

            PlayerStats playerStats = player?.GetStatsNode();
            foreach (PlayerStats member in PlayerManager.Instance.PartyMembers)
            {
                if (member == null || member == playerStats || member.ConfigData == null) continue;
                PlayerSkillCollection collection = SkillCollectionResolver.Resolve(member);

                var entry = new PartySkillProgressSaveData
                {
                    CharacterId = member.ConfigData.ID ?? "",
                    Level = member.CurrentLevel,
                    Experience = member.CurrentExperience,
                    UnspentSkillPoints = collection?.UnspentSkillPoints ?? 0
                };
                if (collection != null)
                {
                    foreach (PlayerSkillState state in collection.CaptureStates())
                    {
                        entry.SkillStates.Add(new SkillStateSaveData
                        {
                            SkillId = state.SkillId,
                            Level = state.Level,
                            IsUnlocked = state.IsUnlocked,
                            EquippedSlot = state.EquippedSlot
                        });
                    }
                }
                result.Add(entry);
            }
            return result;
        }

        private void RestorePartySkillProgress(Player player, IReadOnlyList<PartySkillProgressSaveData> saved)
        {
            if (saved == null || PlayerManager.Instance == null) return;
            foreach (PartySkillProgressSaveData entry in saved)
            {
                if (entry == null) continue;
                foreach (PlayerStats member in PlayerManager.Instance.PartyMembers)
                {
                    if (member == null || member == player?.GetStatsNode() || member.ConfigData?.ID != entry.CharacterId) continue;
                    member.RestoreProgression(entry.Level, entry.Experience);
                    PlayerSkillCollection collection = SkillCollectionResolver.Resolve(member);
                    var states = new List<PlayerSkillState>();
                    if (entry.SkillStates != null)
                    {
                        foreach (SkillStateSaveData state in entry.SkillStates)
                        {
                            states.Add(new PlayerSkillState
                            {
                                SkillId = state.SkillId, Level = state.Level,
                                IsUnlocked = state.IsUnlocked, EquippedSlot = state.EquippedSlot
                            });
                        }
                    }
                    collection?.RestoreStates(states, entry.UnspentSkillPoints);
                    break;
                }
            }
        }

        /// <summary>
        /// Chuyển trạng thái runtime của QuestManager sang DTO JSON.
        /// QuestData vẫn là nguồn sự thật cho tên, mô tả, mục tiêu và phần thưởng.
        /// </summary>
        private List<QuestProgressSaveData> CaptureQuestProgress(GameMenuButton gameMenu)
        {
            var result = new List<QuestProgressSaveData>();
            if (gameMenu == null) return result;

            foreach (QuestProgressRecord record in gameMenu.CaptureQuestProgress())
            {
                if (record == null) continue;
                var save = new QuestProgressSaveData
                {
                    QuestId = record.QuestId,
                    Status = (int)record.Status,
                    IsNew = record.IsNew
                };
                foreach (var objective in record.ObjectiveProgress)
                {
                    save.Objectives.Add(new QuestObjectiveProgressSaveData
                    {
                        ObjectiveId = objective.Key,
                        Progress = objective.Value
                    });
                }
                result.Add(save);
            }
            return result;
        }

        private void RestoreQuestProgress(
            GameMenuButton gameMenu,
            IReadOnlyList<QuestProgressSaveData> saved,
            string trackedQuestId)
        {
            if (gameMenu == null) return;

            var records = new List<QuestProgressRecord>();
            if (saved != null)
            {
                foreach (QuestProgressSaveData entry in saved)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.QuestId)) continue;
                    var record = new QuestProgressRecord
                    {
                        QuestId = entry.QuestId,
                        Status = Enum.IsDefined(typeof(QuestStatus), entry.Status)
                            ? (QuestStatus)entry.Status
                            : QuestStatus.Available,
                        IsNew = entry.IsNew
                    };
                    if (entry.Objectives != null)
                    {
                        foreach (QuestObjectiveProgressSaveData objective in entry.Objectives)
                        {
                            if (objective == null || string.IsNullOrWhiteSpace(objective.ObjectiveId)) continue;
                            record.ObjectiveProgress[objective.ObjectiveId] = objective.Progress;
                        }
                    }
                    records.Add(record);
                }
            }
            gameMenu.RestoreQuestProgress(records, trackedQuestId ?? string.Empty);
        }

        private SaveGameData CaptureCurrentGame()
        {
            SceneManager sceneManager = GetTree().Root.GetNodeOrNull<SceneManager>("SceneManager");
            Player player = sceneManager?.Player;
            PlayerStats stats = player?.GetStatsNode();

            if (player == null || stats == null)
            {
                GD.PrintErr("[SaveManager] Cannot save because player or PlayerStats is missing.");
                return null;
            }

            InventoryManager inventory = player.GetInventoryManager();
            EquipmentManager equipment = player.GetEquipmentManager();
            GameMenuButton gameMenu = GetTree().CurrentScene?.GetNodeOrNull<GameMenuButton>("GameMenuButton");

            return new SaveGameData
            {
                Version = 7,
                SavedAtUtc = DateTime.UtcNow.ToString("O"),
                ScenePath = GetTree().CurrentScene?.SceneFilePath ?? string.Empty,
                PlayerPosition = Vector2SaveData.FromVector2(player.GlobalPosition),
                ActiveCharacterIndex = PlayerManager.Instance?.ActiveCharacterIndex ?? 0,
                PartyOrderCharacterIds = PlayerManager.Instance?.CapturePartyOrder() ?? new List<string>(),
                CompanionCommandModes = PlayerManager.Instance?.CaptureCompanionCommands() ?? new Dictionary<string, int>(),
                PartySkillProgress = CapturePartySkillProgress(player),
                QuestProgress = CaptureQuestProgress(gameMenu),
                TrackedQuestId = gameMenu?.CaptureTrackedQuestId() ?? string.Empty,
                Player = new PlayerSaveData
                {
                    CharacterConfigPath = stats.ConfigData?.ResourcePath ?? string.Empty,
                    CharacterId = stats.ConfigData?.ID ?? string.Empty,
                    Level = stats.CurrentLevel,
                    Experience = stats.CurrentExperience,
                    CurrentHP = stats.CurrentHP,
                    CurrentMP = stats.CurrentMP,
                    CurrentStamina = stats.CurrentStamina,
                    InventoryItemPaths = inventory?.GetItemResourcePaths() ?? new(),
                    EquippedItems = equipment?.CaptureEquippedItems() ?? new(),
                    // Save v3 chỉ giữ state của người chơi; định nghĩa kỹ năng đọc lại từ Resource.
                    SkillStates = player.CaptureSkillStates(),
                    UnspentSkillPoints = player.GetUnspentSkillPoints(),
                    SkillCooldowns = player.CaptureSkillCooldowns(),
                    ActiveTimedSkill = player.CaptureActiveTimedSkill()
                }
            };
        }
    }
}
