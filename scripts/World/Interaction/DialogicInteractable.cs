using Godot;
using AshesofaDyingWorld.Gameplay.Events;

namespace AshesofaDyingWorld.World.Interaction
{
    /// <summary>
    /// Interactable component khởi động một Dialogic timeline khi Player nhấn E.<br/>
    /// Đặt node này (hoặc kéo script lên) bất kỳ Node2D nào muốn có dialog.<br/>
    /// <br/>
    /// Workflow trong Godot Editor:<br/>
    /// 1. Thêm <c>DialogicInteractable</c> vào scene (hoặc dùng làm root Node2D).<br/>
    /// 2. Set <b>Timeline Name</b> = tên timeline Dialogic (vd: <c>hyou_intro</c>).<br/>
    /// 3. Set <b>Display Name</b> và <b>Prompt Verb</b> cho interaction prompt.<br/>
    /// 4. Chạy game → đi gần → nhấn E.<br/>
    /// </summary>
    public partial class DialogicInteractable : Node2D, IInteractable
    {
        // ── Nhận diện ──────────────────────────────────────────────────────
        [ExportGroup("Nhận diện")]

        /// <summary>Tên timeline Dialogic cần chạy. Vd: "hyou_intro" hoặc "res://data/timelines/hyou_intro.dtl".</summary>
        [Export] public string TimelineName { get; set; } = "";

        /// <summary>Tên hiển thị trong interaction prompt. Vd: "Hyou".</summary>
        [Export] public string DisplayName { get; set; } = "NPC";

        /// <summary>Động từ trong prompt. Vd: "Nói chuyện", "Đọc", "Kiểm tra".</summary>
        [Export] public string PromptVerb { get; set; } = "Nói chuyện";

        // ── Phạm vi ────────────────────────────────────────────────────────
        [ExportGroup("Phạm vi")]

        [Export(PropertyHint.Range, "8,200,1")]
        public float InteractionRadius { get; set; } = 56f;

        [Export] public bool PlayerOnly { get; set; } = true;

        // ── Hành vi ────────────────────────────────────────────────────────
        [ExportGroup("Hành vi")]

        /// <summary>
        /// Nếu true, dialog có thể kích hoạt lại nhiều lần.<br/>
        /// Nếu false, chỉ chạy timeline một lần duy nhất.
        /// </summary>
        [Export] public bool Replayable { get; set; } = true;

        /// <summary>
        /// Nếu true, emit <c>GameplayEventType.DialogueCompleted</c> sau khi dialog kết thúc.<br/>
        /// Dùng để quest tracking.
        /// </summary>
        [Export] public bool EmitDialogueCompletedEvent { get; set; } = true;

        /// <summary>ID để truyền vào GameplayEvent. Mặc định dùng tên Node.</summary>
        [Export] public string DialogueId { get; set; } = "";

        // ── State ──────────────────────────────────────────────────────────

        private bool _hasPlayed;
        private bool _dialogRunning;

        // ── IInteractable ──────────────────────────────────────────────────

        public Node2D InteractionAnchor => this;

        public override void _Ready()
        {
            AddToGroup(WorldInteractable.InteractableGroup);
        }

        public override void _ExitTree()
        {
            RemoveFromGroup(WorldInteractable.InteractableGroup);
        }

        public bool CanInteract(Node actor)
        {
            if (actor == null || !GodotObject.IsInstanceValid(actor))
            {
                return false;
            }

            // Dialog đang chạy → không cho kích hoạt thêm.
            if (_dialogRunning || DialogicBridge.IsDialogActive())
            {
                return false;
            }

            // One-shot: đã chạy 1 lần rồi → không replay.
            if (!Replayable && _hasPlayed)
            {
                return false;
            }

            if (PlayerOnly && !actor.IsInGroup("Player"))
            {
                return false;
            }

            if (actor is Node2D actor2D)
            {
                return actor2D.GlobalPosition.DistanceTo(GlobalPosition) <= Mathf.Max(8f, InteractionRadius);
            }

            return false;
        }

        public string GetInteractionPrompt(Node actor)
        {
            if (!Replayable && _hasPlayed)
            {
                return string.Empty;
            }

            string verb = string.IsNullOrWhiteSpace(PromptVerb) ? "Nói chuyện" : PromptVerb.Trim();
            string name = string.IsNullOrWhiteSpace(DisplayName) ? Name.ToString() : DisplayName.Trim();
            return $"{verb} {name}".Trim();
        }

        public bool TryInteract(Node actor)
        {
            if (!CanInteract(actor))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(TimelineName))
            {
                GD.PrintErr($"[DialogicInteractable] '{Name}': TimelineName chưa được set.");
                return false;
            }

            // Bắt đầu timeline.
            GodotObject layout = DialogicBridge.StartTimeline(TimelineName);
            if (layout == null)
            {
                GD.PrintErr($"[DialogicInteractable] '{Name}': Dialogic.start('{TimelineName}') thất bại.");
                return false;
            }

            _dialogRunning = true;
            _hasPlayed = true;

            // Subscribe one-shot: khi dialog kết thúc → cleanup + emit event.
            DialogicBridge.OnTimelineEnded(() => OnDialogEnded(actor));

            return true;
        }

        // ── Private ────────────────────────────────────────────────────────

        private void OnDialogEnded(Node actor)
        {
            _dialogRunning = false;

            if (!EmitDialogueCompletedEvent)
            {
                return;
            }

            if (actor == null || !GodotObject.IsInstanceValid(actor))
            {
                return;
            }

            string sourceId = string.IsNullOrWhiteSpace(DialogueId) ? Name.ToString() : DialogueId.Trim();

            GameplayEventBus.GetOrCreate(GetTree())?.Publish(new GameplayEvent(
                GameplayEventType.DialogueCompleted,
                actor,
                actor.Name.ToString(),
                sourceId,
                sourceId,
                1,
                GlobalPosition));
        }
    }
}
