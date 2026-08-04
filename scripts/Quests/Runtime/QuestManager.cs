using Godot;
using System;
using System.Collections.Generic;
using AshesofaDyingWorld.Quests.Data;

namespace AshesofaDyingWorld.Quests.Runtime
{
    /// <summary>
    /// Quản lý định nghĩa và tiến độ nhiệm vụ tại runtime.
    /// Lớp này không dựng UI; QuestJournalPanel chỉ đọc dữ liệu từ đây để trình bày.
    /// </summary>
    public partial class QuestManager : Node
    {
        public const string DefaultQuestDirectory = "res://data/quests";

        private readonly Dictionary<string, QuestData> _definitions = new();
        private readonly Dictionary<string, QuestRuntimeState> _states = new();

        public event Action Changed;

        public string TrackedQuestId { get; private set; } = "";
        public IReadOnlyDictionary<string, QuestData> Definitions => _definitions;
        public IReadOnlyDictionary<string, QuestRuntimeState> States => _states;

        public override void _Ready()
        {
            QuestService.Current = this;
        }

        public override void _ExitTree()
        {
            if (QuestService.Current == this)
            {
                QuestService.Current = null;
            }
        }

        public void InitializeFromDirectory(string directoryPath = DefaultQuestDirectory)
        {
            _definitions.Clear();
            _states.Clear();

            foreach (string fileName in DirAccess.GetFilesAt(directoryPath))
            {
                if (!fileName.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)
                    && !fileName.EndsWith(".res", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string path = $"{directoryPath}/{fileName}";
                QuestData quest = ResourceLoader.Load<QuestData>(path);
                if (quest == null || string.IsNullOrWhiteSpace(quest.QuestId))
                {
                    GD.PrintErr($"[QuestManager] Bỏ qua resource nhiệm vụ không hợp lệ: {path}");
                    continue;
                }

                RegisterDefinition(quest);
            }

            Changed?.Invoke();
        }

        public void RegisterDefinition(QuestData quest)
        {
            if (quest == null || string.IsNullOrWhiteSpace(quest.QuestId))
            {
                return;
            }

            _definitions[quest.QuestId] = quest;
            if (!_states.ContainsKey(quest.QuestId))
            {
                _states[quest.QuestId] = CreateInitialState(quest);
            }
        }

        public QuestData GetDefinition(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId)
                && _definitions.TryGetValue(questId, out QuestData quest)
                    ? quest
                    : null;
        }

        public QuestRuntimeState GetState(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId)
                && _states.TryGetValue(questId, out QuestRuntimeState state)
                    ? state
                    : null;
        }

        public bool AcceptQuest(string questId, int characterLevel)
        {
            QuestData quest = GetDefinition(questId);
            QuestRuntimeState state = GetState(questId);
            if (quest == null || state == null || state.Status != QuestStatus.Available)
            {
                return false;
            }

            if (characterLevel < Mathf.Max(1, quest.RequiredCharacterLevel))
            {
                return false;
            }

            state.Status = QuestStatus.Active;
            state.IsNew = false;
            Changed?.Invoke();
            return true;
        }

        public bool ToggleTrackedQuest(string questId)
        {
            QuestRuntimeState state = GetState(questId);
            if (state == null || state.Status != QuestStatus.Active)
            {
                return false;
            }

            TrackedQuestId = TrackedQuestId == questId ? "" : questId;
            state.IsNew = false;
            Changed?.Invoke();
            return true;
        }

        public bool AddObjectiveProgress(string questId, string objectiveId, int amount)
        {
            QuestRuntimeState state = GetState(questId);
            if (state == null || state.Status != QuestStatus.Active || amount == 0)
            {
                return false;
            }

            int current = state.ObjectiveProgress.TryGetValue(objectiveId, out int value) ? value : 0;
            return SetObjectiveProgress(questId, objectiveId, current + amount);
        }

        public bool SetObjectiveProgress(string questId, string objectiveId, int amount)
        {
            QuestData quest = GetDefinition(questId);
            QuestRuntimeState state = GetState(questId);
            QuestObjectiveData objective = FindObjective(quest, objectiveId);
            if (quest == null || state == null || objective == null || state.Status != QuestStatus.Active)
            {
                return false;
            }

            int clamped = Mathf.Clamp(amount, 0, Mathf.Max(1, objective.RequiredAmount));
            state.ObjectiveProgress[objectiveId] = clamped;
            state.IsNew = false;

            if (AreAllObjectivesComplete(quest, state))
            {
                state.Status = QuestStatus.Completed;
                if (TrackedQuestId == questId)
                {
                    TrackedQuestId = "";
                }
            }

            Changed?.Invoke();
            return true;
        }

        public bool FailQuest(string questId)
        {
            QuestRuntimeState state = GetState(questId);
            if (state == null || state.Status != QuestStatus.Active)
            {
                return false;
            }

            state.Status = QuestStatus.Failed;
            if (TrackedQuestId == questId)
            {
                TrackedQuestId = "";
            }
            Changed?.Invoke();
            return true;
        }

        public int GetCompletedObjectiveCount(QuestData quest, QuestRuntimeState state)
        {
            if (quest?.Objectives == null || state == null)
            {
                return 0;
            }

            int completed = 0;
            foreach (QuestObjectiveData objective in quest.Objectives)
            {
                if (IsObjectiveComplete(objective, state))
                {
                    completed++;
                }
            }
            return completed;
        }

        public bool IsObjectiveComplete(QuestObjectiveData objective, QuestRuntimeState state)
        {
            if (objective == null || state == null)
            {
                return false;
            }

            int progress = state.ObjectiveProgress.TryGetValue(objective.ObjectiveId, out int value) ? value : 0;
            return progress >= Mathf.Max(1, objective.RequiredAmount);
        }

        public bool IsObjectiveVisible(QuestData quest, QuestRuntimeState state, QuestObjectiveData objective)
        {
            if (objective == null || string.IsNullOrWhiteSpace(objective.RevealAfterObjectiveId))
            {
                return true;
            }

            QuestObjectiveData prerequisite = FindObjective(quest, objective.RevealAfterObjectiveId);
            return prerequisite != null && IsObjectiveComplete(prerequisite, state);
        }

        public QuestObjectiveData GetCurrentObjective(string questId)
        {
            QuestData quest = GetDefinition(questId);
            QuestRuntimeState state = GetState(questId);
            if (quest?.Objectives == null || state == null)
            {
                return null;
            }

            foreach (QuestObjectiveData objective in quest.Objectives)
            {
                if (IsObjectiveVisible(quest, state, objective) && !IsObjectiveComplete(objective, state))
                {
                    return objective;
                }
            }
            return null;
        }

        public List<QuestProgressRecord> CaptureProgress()
        {
            var result = new List<QuestProgressRecord>();
            foreach (var pair in _states)
            {
                var record = new QuestProgressRecord
                {
                    QuestId = pair.Key,
                    Status = pair.Value.Status,
                    IsNew = pair.Value.IsNew
                };

                foreach (var progress in pair.Value.ObjectiveProgress)
                {
                    record.ObjectiveProgress[progress.Key] = progress.Value;
                }
                result.Add(record);
            }
            return result;
        }

        public void RestoreProgress(IReadOnlyList<QuestProgressRecord> records, string trackedQuestId)
        {
            if (records != null)
            {
                foreach (QuestProgressRecord record in records)
                {
                    if (record == null || !_definitions.TryGetValue(record.QuestId, out QuestData quest))
                    {
                        continue;
                    }

                    QuestRuntimeState state = CreateInitialState(quest);
                    state.Status = record.Status;
                    state.IsNew = record.IsNew;
                    if (record.ObjectiveProgress != null)
                    {
                        foreach (var progress in record.ObjectiveProgress)
                        {
                            QuestObjectiveData objective = FindObjective(quest, progress.Key);
                            if (objective == null) continue;
                            state.ObjectiveProgress[progress.Key] = Mathf.Clamp(
                                progress.Value,
                                0,
                                Mathf.Max(1, objective.RequiredAmount));
                        }
                    }
                    _states[record.QuestId] = state;
                }
            }

            TrackedQuestId = GetState(trackedQuestId)?.Status == QuestStatus.Active
                ? trackedQuestId
                : "";
            Changed?.Invoke();
        }

        private QuestRuntimeState CreateInitialState(QuestData quest)
        {
            var state = new QuestRuntimeState
            {
                QuestId = quest.QuestId,
                Status = quest.StartsActive ? QuestStatus.Active : QuestStatus.Available,
                IsNew = quest.StartsAsNew
            };

            if (quest.Objectives != null)
            {
                foreach (QuestObjectiveData objective in quest.Objectives)
                {
                    if (objective == null || string.IsNullOrWhiteSpace(objective.ObjectiveId)) continue;
                    state.ObjectiveProgress[objective.ObjectiveId] = Mathf.Clamp(
                        objective.InitialProgress,
                        0,
                        Mathf.Max(1, objective.RequiredAmount));
                }
            }
            return state;
        }

        private QuestObjectiveData FindObjective(QuestData quest, string objectiveId)
        {
            if (quest?.Objectives == null || string.IsNullOrWhiteSpace(objectiveId))
            {
                return null;
            }

            foreach (QuestObjectiveData objective in quest.Objectives)
            {
                if (objective?.ObjectiveId == objectiveId)
                {
                    return objective;
                }
            }
            return null;
        }

        private bool AreAllObjectivesComplete(QuestData quest, QuestRuntimeState state)
        {
            if (quest?.Objectives == null || quest.Objectives.Count == 0)
            {
                return false;
            }

            foreach (QuestObjectiveData objective in quest.Objectives)
            {
                if (!IsObjectiveComplete(objective, state))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
