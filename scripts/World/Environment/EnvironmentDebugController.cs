using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// QA controller cực nhẹ cho Environment Core. Chỉ được tạo ở debug build.
    /// Không dựng panel, không thêm InputMap, không làm runtime release phình lên.
    ///
    /// Ctrl+F9  : 06:00 -> 12:00 -> 18:00 -> 00:00
    /// Ctrl+F10 : 0x -> 1x -> 60x -> 600x
    /// Ctrl+F12 : clear -> rainy -> stormy
    /// </summary>
    public partial class EnvironmentDebugController : Node
    {
        private static readonly float[] TimePresets = { 6f, 12f, 18f, 0f };
        private static readonly float[] TimeScales = { 0f, 1f, 60f, 600f };
        private static readonly string[] WeatherPaths =
        {
            "res://data/world/environment/weather/clear.tres",
            "res://data/world/environment/weather/rainy.tres",
            "res://data/world/environment/weather/stormy.tres"
        };
        private static readonly string[] WeatherNames = { "clear", "rainy", "stormy" };

        private WorldEnvironmentService _environment;
        private int _timePresetIndex = 1;
        private int _timeScaleIndex = 2;
        private int _weatherIndex;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            _environment = GetParent() as WorldEnvironmentService;
            if (_environment == null)
            {
                GD.PushWarning("[EnvironmentDebug] WorldEnvironmentService not found.");
                SetProcessUnhandledInput(false);
                return;
            }

            GD.Print("[EnvironmentDebug] ACTIVE | Ctrl+F9 time | Ctrl+F10 speed | Ctrl+F12 weather");
            PrintState("boot");
        }

        public override void _UnhandledInput(InputEvent inputEvent)
        {
            if (_environment == null
                || inputEvent is not InputEventKey key
                || !key.Pressed
                || key.Echo
                || !key.CtrlPressed)
            {
                return;
            }

            switch (key.Keycode)
            {
                case Key.F9:
                    CycleTimePreset();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F10:
                    CycleTimeScale();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F12:
                    CycleWeather();
                    GetViewport().SetInputAsHandled();
                    break;
            }
        }

        private void CycleTimePreset()
        {
            _timePresetIndex = (_timePresetIndex + 1) % TimePresets.Length;
            float hour = TimePresets[_timePresetIndex];
            _environment.RestoreClock(_environment.Clock?.CurrentDay ?? 1, hour);
            PrintState($"time={hour:00}:00");
        }

        private void CycleTimeScale()
        {
            if (_environment.Clock == null)
            {
                return;
            }

            _timeScaleIndex = (_timeScaleIndex + 1) % TimeScales.Length;
            float scale = TimeScales[_timeScaleIndex];
            _environment.Clock.TimeScale = scale;
            _environment.Clock.IsPaused = Mathf.IsZeroApprox(scale);
            PrintState($"time_scale={scale:0}x");
        }

        private void CycleWeather()
        {
            _weatherIndex = (_weatherIndex + 1) % WeatherPaths.Length;
            string path = WeatherPaths[_weatherIndex];
            EnvironmentWeatherPreset preset = ResourceLoader.Exists(path)
                ? GD.Load<EnvironmentWeatherPreset>(path)
                : null;

            if (preset == null)
            {
                GD.PushWarning($"[EnvironmentDebug] Missing weather preset: {path}");
                return;
            }

            _environment.SetWeather(preset, transitionSeconds: 1.25f);
            PrintState($"weather={WeatherNames[_weatherIndex]}");
        }

        private void PrintState(string action)
        {
            EnvironmentState state = _environment.CurrentState;
            GD.Print(
                $"[EnvironmentDebug] {action} | day={state.Day} " +
                $"hour={state.TimeOfDayHours:00.00} daylight={state.Daylight:0.00} " +
                $"night={state.NightFactor:0.00} wind={state.WindStrength:0.00} " +
                $"rain={state.RainAmount:0.00} wet={state.Wetness:0.00}");
        }
    }
}
