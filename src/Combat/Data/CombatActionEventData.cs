using Godot;

namespace AshesofaDyingWorld.Combat.Data
{
    /// <summary>
    /// Loại event được action phát ra. Mechanics runner chỉ quyết định đúng thời điểm;
    /// dispatcher riêng mới quyết định event đó tạo projectile hay presentation cue.
    /// </summary>
    public enum CombatActionEventType
    {
        SpawnProjectile = 0,
        PresentationCue = 1,
        SelfEffect = 2
    }

    /// <summary>
    /// Event data-driven gắn vào CombatActionData.
    /// Có thể author theo frame cho animation thật, đồng thời có normalized time để
    /// fallback timing vẫn phát đúng khi animation chưa tồn tại.
    /// </summary>
    [GlobalClass]
    public partial class CombatActionEventData : Resource
    {
        [ExportGroup("Identity")]
        [Export] public string EventId { get; set; } = "action_event";
        [Export] public CombatActionEventType EventType { get; set; } = CombatActionEventType.SpawnProjectile;

        [ExportGroup("Trigger")]
        [Export] public int TriggerFrame { get; set; } = -1;
        [Export(PropertyHint.Range, "-1,1,0.001")]
        public float TriggerNormalizedTime { get; set; } = -1f;

        [ExportGroup("Payload")]
        [Export] public ProjectileSpecData ProjectileSpec { get; set; }
        [Export] public NodePath OriginSocketPath { get; set; } = new NodePath("CastOrigin");
        [Export] public StringName CueId { get; set; } = new StringName(string.Empty);

        public float ResolveNormalizedTrigger(CombatActionData action)
        {
            if (TriggerNormalizedTime >= 0f)
            {
                return Mathf.Clamp(TriggerNormalizedTime, 0f, 1f);
            }

            if (TriggerFrame >= 0 && action != null)
            {
                int start = action.StartFrame;
                int end = Mathf.Max(start + 1, action.EndFrame);
                return Mathf.Clamp((TriggerFrame - start) / (float)(end - start), 0f, 1f);
            }

            if (action != null)
            {
                int start = action.StartFrame;
                int end = Mathf.Max(start + 1, action.EndFrame);
                return Mathf.Clamp((action.ActiveStartFrame - start) / (float)(end - start), 0f, 1f);
            }

            return 0f;
        }

        public bool IsDueAtFrame(CombatActionData action, int currentFrame)
        {
            if (TriggerFrame >= 0)
            {
                return currentFrame >= TriggerFrame;
            }

            if (action == null)
            {
                return false;
            }

            int start = action.StartFrame;
            int end = Mathf.Max(start + 1, action.EndFrame);
            float normalized = Mathf.Clamp((currentFrame - start) / (float)(end - start), 0f, 1f);
            return normalized + 0.0001f >= ResolveNormalizedTrigger(action);
        }

        public bool IsDueAtNormalizedTime(CombatActionData action, float normalizedTime)
        {
            return Mathf.Clamp(normalizedTime, 0f, 1f) + 0.0001f >= ResolveNormalizedTrigger(action);
        }
    }
}
