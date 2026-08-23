using Godot;

namespace AshesofaDyingWorld.World.Environment
{
    /// <summary>
    /// Runtime hub cho toàn bộ môi trường 2D.
    ///
    /// Pattern tham khảo từ Weather System 2D: một hub giữ state chung, còn map/material là consumer.
    /// Service nằm ở SceneTree.Root nên sống xuyên scene mà không cần bắt buộc autoload.
    /// </summary>
    public partial class WorldEnvironmentService : Node
    {
        private const string RuntimeNodeName = "WorldEnvironmentService";
        private const string DefaultProfilePath = "res://data/world/environment/default_environment.tres";

        public static WorldEnvironmentService Instance { get; private set; }

        public WorldClock Clock { get; private set; }
        public EnvironmentState CurrentState { get; } = new();
        public EnvironmentProfile ActiveProfile { get; private set; }

        private EnvironmentWeatherSample _weatherCurrent = new() { WindMultiplier = 1f };
        private EnvironmentWeatherSample _weatherFrom = new() { WindMultiplier = 1f };
        private EnvironmentWeatherSample _weatherTarget = new() { WindMultiplier = 1f };
        private float _weatherTransitionElapsed;
        private float _weatherTransitionDuration;

        public override void _EnterTree()
        {
            Instance = this;
            ProcessPriority = -100;
        }

        public override void _Ready()
        {
            EnsureClock();
            EnsureDebugController();

            EnvironmentProfile defaultProfile = ResourceLoader.Exists(DefaultProfilePath)
                ? GD.Load<EnvironmentProfile>(DefaultProfilePath)
                : null;

            SetProfile(defaultProfile, snapToDefaultWeather: true);
            PublishState();
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void _Process(double delta)
        {
            StepWeather((float)delta);
            PublishState();
        }

        public static WorldEnvironmentService GetOrCreate(SceneTree tree)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                return Instance;
            }

            if (tree?.Root == null)
            {
                return null;
            }

            WorldEnvironmentService existing = tree.Root.GetNodeOrNull<WorldEnvironmentService>(RuntimeNodeName);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var service = new WorldEnvironmentService
            {
                Name = RuntimeNodeName
            };
            tree.Root.AddChild(service);
            return service;
        }

        public void SetProfile(EnvironmentProfile profile, bool snapToDefaultWeather = false)
        {
            if (profile == null)
            {
                return;
            }

            ActiveProfile = profile;

            if (snapToDefaultWeather && profile.DefaultWeather != null)
            {
                SetWeather(profile.DefaultWeather, 0f);
            }

            PublishState();
        }

        public void SetWeather(EnvironmentWeatherPreset preset, float transitionSeconds = 4f)
        {
            EnvironmentWeatherSample target = EnvironmentWeatherSample.FromPreset(preset);

            if (transitionSeconds <= 0f)
            {
                _weatherCurrent = target;
                _weatherFrom = target;
                _weatherTarget = target;
                _weatherTransitionElapsed = 0f;
                _weatherTransitionDuration = 0f;
                PublishState();
                return;
            }

            _weatherFrom = _weatherCurrent;
            _weatherTarget = target;
            _weatherTransitionElapsed = 0f;
            _weatherTransitionDuration = transitionSeconds;
        }

        public void RestoreClock(int day, float hour)
        {
            EnsureClock();
            Clock.SetTime(day, hour);
            PublishState();
        }

        public void ResetForNewGame()
        {
            EnsureClock();
            Clock.ResetClock();

            if (ActiveProfile?.DefaultWeather != null)
            {
                SetWeather(ActiveProfile.DefaultWeather, 0f);
            }
            else
            {
                _weatherCurrent = new EnvironmentWeatherSample { WindMultiplier = 1f };
                _weatherFrom = _weatherCurrent;
                _weatherTarget = _weatherCurrent;
            }

            PublishState();
        }

        private void EnsureClock()
        {
            if (Clock != null && GodotObject.IsInstanceValid(Clock))
            {
                return;
            }

            Clock = GetNodeOrNull<WorldClock>("WorldClock");
            if (Clock != null)
            {
                return;
            }

            Clock = new WorldClock
            {
                Name = "WorldClock"
            };
            AddChild(Clock);
        }


        private void EnsureDebugController()
        {
            if (!OS.IsDebugBuild() || GetNodeOrNull<EnvironmentDebugController>("EnvironmentDebugController") != null)
            {
                return;
            }

            AddChild(new EnvironmentDebugController
            {
                Name = "EnvironmentDebugController"
            });
        }

        private void PublishState()
        {
            RebuildState();
        }

        private void StepWeather(float delta)
        {
            if (_weatherTransitionDuration <= 0f)
            {
                return;
            }

            _weatherTransitionElapsed += Mathf.Max(delta, 0f);
            float t = Mathf.Clamp(_weatherTransitionElapsed / _weatherTransitionDuration, 0f, 1f);
            // Smoothstep tránh weather đổi tốc độ đột ngột ở đầu/cuối transition.
            t = t * t * (3f - 2f * t);
            _weatherCurrent = EnvironmentWeatherSample.Lerp(_weatherFrom, _weatherTarget, t);

            if (_weatherTransitionElapsed >= _weatherTransitionDuration)
            {
                _weatherCurrent = _weatherTarget;
                _weatherTransitionElapsed = 0f;
                _weatherTransitionDuration = 0f;
            }
        }

        private void RebuildState()
        {
            EnsureClock();

            EnvironmentProfile profile = ActiveProfile;
            if (profile == null)
            {
                CurrentState.Day = Clock.CurrentDay;
                CurrentState.TimeOfDayHours = Clock.GameTimeHours;
                CurrentState.TimeOfDay01 = Clock.NormalizedTimeOfDay;
                CurrentState.Daylight = 1f;
                CurrentState.NightFactor = 0f;
                CurrentState.SunDirection = Vector2.Down;
                CurrentState.SunElevation = 1f;
                CurrentState.SunEnergy = 0.18f;
                CurrentState.SunColor = Colors.White;
                CurrentState.MoonDirection = Vector2.Down;
                CurrentState.MoonElevation = 0f;
                CurrentState.MoonEnergy = 0f;
                CurrentState.MoonColor = new Color(0.70f, 0.79f, 1f, 1f);
                CurrentState.KeyLightDirection = Vector2.Down;
                CurrentState.KeyLightElevation = 1f;
                CurrentState.KeyLightStrength01 = 1f;
                CurrentState.KeyLightColor = Colors.White;
                CurrentState.AmbientColor = new Color(0.84f, 0.84f, 0.84f, 1f);
                CurrentState.ShadowStrength = 0.55f;
                CurrentState.WaterShimmerStrength = 0.12f;
                CurrentState.WaterRippleStrength = 0.08f;
                return;
            }

            float hour = Clock.GameTimeHours;
            float time01 = Clock.NormalizedTimeOfDay;
            float daylight = CalculateDaylight(hour, profile.SunriseHour, profile.SunsetHour);
            float night = 1f - daylight;

            Color ambient = profile.SampleAmbient(time01);
            ambient = ApplyWeatherPalette(ambient, _weatherCurrent.Darken, _weatherCurrent.Desaturate);

            // Tách ambient khỏi direct light: nếu noon ambient = trắng tuyệt đối thì DirectionalLight2D
            // không còn khoảng tương phản để tạo chiều sâu. Direct sun/moon sẽ bù phần sáng còn lại.
            float ambientStrength = Mathf.Lerp(
                profile.NightAmbientStrength,
                profile.DayAmbientStrength,
                daylight);
            ambient = ScaleRgb(ambient, ambientStrength);

            CelestialSolver.Sample celestial = CelestialSolver.Evaluate(
                hour,
                profile,
                daylight,
                night,
                _weatherCurrent.Cloudiness);

            float wetness = Mathf.Clamp(
                Mathf.Max(Mathf.Max(profile.BaseWetness, _weatherCurrent.Wetness), _weatherCurrent.Rain * 0.85f),
                0f,
                1f);

            CurrentState.Day = Clock.CurrentDay;
            CurrentState.TimeOfDayHours = hour;
            CurrentState.TimeOfDay01 = time01;
            CurrentState.Daylight = daylight;
            CurrentState.NightFactor = night;
            CurrentState.SunDirection = celestial.SunDirection;
            CurrentState.SunElevation = celestial.SunElevation;
            CurrentState.SunEnergy = celestial.SunEnergy;
            CurrentState.SunColor = celestial.SunColor;
            CurrentState.MoonDirection = celestial.MoonDirection;
            CurrentState.MoonElevation = celestial.MoonElevation;
            CurrentState.MoonEnergy = celestial.MoonEnergy;
            CurrentState.MoonColor = celestial.MoonColor;
            CurrentState.KeyLightDirection = celestial.KeyDirection;
            CurrentState.KeyLightElevation = celestial.KeyElevation;
            CurrentState.KeyLightStrength01 = celestial.KeyStrength01;
            CurrentState.KeyLightColor = celestial.KeyColor;
            CurrentState.AmbientColor = ambient;
            CurrentState.WindStrength = Mathf.Max(0f, profile.BaseWindStrength * _weatherCurrent.WindMultiplier);
            CurrentState.RainAmount = Mathf.Clamp(_weatherCurrent.Rain, 0f, 1f);
            CurrentState.Wetness = wetness;
            CurrentState.FogAmount = Mathf.Clamp(_weatherCurrent.Fog, 0f, 1f);
            CurrentState.Cloudiness = Mathf.Clamp(_weatherCurrent.Cloudiness, 0f, 1f);
            CurrentState.ShadowStrength = Mathf.Clamp(profile.ShadowStrength, 0f, 1f);
            CurrentState.WaterShimmerStrength = Mathf.Max(0f, profile.WaterShimmerStrength);
            CurrentState.WaterRippleStrength = Mathf.Max(0f, profile.WaterRippleStrength);
            CurrentState.WeatherDarken = Mathf.Clamp(_weatherCurrent.Darken, 0f, 1f);
            CurrentState.WeatherDesaturate = Mathf.Clamp(_weatherCurrent.Desaturate, 0f, 1f);
        }

        private static float CalculateDaylight(float hour, float sunrise, float sunset)
        {
            sunrise = Mathf.Clamp(sunrise, 0f, 24f);
            sunset = Mathf.Clamp(sunset, 0f, 24f);

            if (sunset <= sunrise || hour <= sunrise || hour >= sunset)
            {
                return 0f;
            }

            float progress = (hour - sunrise) / (sunset - sunrise);
            float sunElevation = Mathf.Sin(progress * Mathf.Pi);
            // Mặt trời vừa mọc vẫn tối, sau đó tăng mềm thay vì bật sáng tuyến tính.
            return Smooth01(Mathf.Clamp(sunElevation / 0.22f, 0f, 1f));
        }

        private static Color ApplyWeatherPalette(Color color, float darken, float desaturate)
        {
            darken = Mathf.Clamp(darken, 0f, 1f);
            desaturate = Mathf.Clamp(desaturate, 0f, 1f);

            Color darkened = color.Lerp(new Color(0.22f, 0.25f, 0.32f, color.A), darken * 0.5f);
            float gray = darkened.R * 0.2126f + darkened.G * 0.7152f + darkened.B * 0.0722f;
            return darkened.Lerp(new Color(gray, gray, gray, darkened.A), desaturate * 0.6f);
        }

        private static Color ScaleRgb(Color color, float strength)
        {
            strength = Mathf.Max(strength, 0f);
            return new Color(
                Mathf.Clamp(color.R * strength, 0f, 1.5f),
                Mathf.Clamp(color.G * strength, 0f, 1.5f),
                Mathf.Clamp(color.B * strength, 0f, 1.5f),
                color.A);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}
