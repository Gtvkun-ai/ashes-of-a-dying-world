using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Đồng hồ thế giới duy nhất. Đây là state "thật" cần lưu save.
    /// Các giá trị ánh sáng / màu / gió là derived state và không được lưu ở đây.
    /// </summary>
    public partial class WorldClock : Node
    {
        [Signal]
        public delegate void DayChangedEventHandler(int currentDay);

        [Export(PropertyHint.Range, "0,23.999,0.01")]
        public float GameTimeHours { get; set; } = 12f;

        [Export(PropertyHint.Range, "1,999999,1")]
        public int CurrentDay { get; set; } = 1;

        /// <summary>
        /// Số giây trong game trôi qua trên mỗi giây thật.
        /// 60 = một phút game / giây thật, tức một ngày game kéo dài 24 phút thật.
        /// </summary>
        [Export(PropertyHint.Range, "0,86400,1")]
        public float TimeScale { get; set; } = 60f;

        [Export]
        public bool IsPaused { get; set; }

        public float NormalizedTimeOfDay => PositiveMod(GameTimeHours, 24f) / 24f;

        public override void _EnterTree()
        {
            // Clock phải tick trước WorldEnvironmentService để state của frame hiện tại không trễ một frame.
            ProcessPriority = -110;
        }

        public override void _Process(double delta)
        {
            if (IsPaused || TimeScale <= 0f)
            {
                return;
            }

            AdvanceGameSeconds((float)delta * TimeScale);
        }

        public void AdvanceGameSeconds(float gameSeconds)
        {
            if (Mathf.IsZeroApprox(gameSeconds))
            {
                return;
            }

            SetTime(CurrentDay, GameTimeHours + gameSeconds / 3600f);
        }

        public void SetTime(int day, float hour)
        {
            int safeDay = System.Math.Max(day, 1);
            float totalHours = (safeDay - 1) * 24f + hour;

            // Cho phép debug tua lùi nhưng không cho thời gian đi trước ngày 1.
            totalHours = Mathf.Max(totalHours, 0f);

            int nextDay = Mathf.FloorToInt(totalHours / 24f) + 1;
            float nextHour = PositiveMod(totalHours, 24f);
            bool dayChanged = nextDay != CurrentDay;

            CurrentDay = nextDay;
            GameTimeHours = nextHour;

            if (dayChanged)
            {
                EmitSignal(SignalName.DayChanged, CurrentDay);
            }
        }

        public void ResetClock(int day = 1, float hour = 12f)
        {
            SetTime(day, hour);
            IsPaused = false;
        }

        private static float PositiveMod(float value, float modulus)
        {
            float result = value % modulus;
            return result < 0f ? result + modulus : result;
        }
    }
}
