using Godot;

namespace AshesofaDyingWorld.World.Navigation
{
    /// <summary>
    /// Cổng semantic nối hai tầng đi lại. FromPoint/ToPoint là hai đầu hành lang cầu thang.
    /// AI không "nhìn texture để đoán"; level author chỉ cần đặt link đúng một lần trong scene.
    /// </summary>
    public partial class StairLink2D : Node2D
    {
        [Export] public string LinkId { get; set; } = "stairs";
        [Export] public int FromElevation { get; set; } = 0;
        [Export] public int ToElevation { get; set; } = 1;
        [Export] public Vector2 FromPoint { get; set; } = Vector2.Zero;
        [Export] public Vector2 ToPoint { get; set; } = Vector2.Zero;
        [Export] public float EntryRadius { get; set; } = 22f;
        [Export] public float ExitRadius { get; set; } = 22f;
        [Export] public bool Bidirectional { get; set; } = true;

        public bool Connects(int fromElevation, int toElevation)
        {
            if (fromElevation == FromElevation && toElevation == ToElevation)
            {
                return true;
            }

            return Bidirectional
                && fromElevation == ToElevation
                && toElevation == FromElevation;
        }

        public Vector2 GetEntryPoint(int fromElevation)
        {
            return fromElevation == FromElevation
                ? ToGlobal(FromPoint)
                : ToGlobal(ToPoint);
        }

        public Vector2 GetExitPoint(int fromElevation)
        {
            return fromElevation == FromElevation
                ? ToGlobal(ToPoint)
                : ToGlobal(FromPoint);
        }

        public int GetDestinationElevation(int fromElevation)
        {
            return fromElevation == FromElevation ? ToElevation : FromElevation;
        }

        public float GetEntryRadius(int fromElevation)
        {
            return fromElevation == FromElevation ? Mathf.Max(4f, EntryRadius) : Mathf.Max(4f, ExitRadius);
        }

        public float GetExitRadius(int fromElevation)
        {
            return fromElevation == FromElevation ? Mathf.Max(4f, ExitRadius) : Mathf.Max(4f, EntryRadius);
        }
    }
}
