using Godot;

namespace AshesofaDyingWorld.World.Camera
{
    public partial class PlayerCameraZoom : Camera2D
    {
        private static float _sharedTargetZoom = 2f;

        [Export(PropertyHint.Range, "0.6,8,0.05")]
        public float MinZoom { get; set; } = 0.35f;

        [Export(PropertyHint.Range, "0.6,8,0.05")]
        public float MaxZoom { get; set; } = 3.4f;

        [Export(PropertyHint.Range, "0.01,1,0.01")]
        public float WheelStep { get; set; } = 0.18f;

        [Export(PropertyHint.Range, "1,30,0.5")]
        public float SmoothSpeed { get; set; } = 12f;

        [Export(PropertyHint.Range, "0.6,8,0.05")]
        public float DefaultZoom { get; set; } = 2f;

        [Export(PropertyHint.Range, "0.1,2,0.05")]
        public float OverviewZoom { get; set; } = 0.35f;

        private bool _overviewMode;

        public override void _Ready()
        {
            float current = Mathf.Abs(Zoom.X) > 0.001f ? Zoom.X : DefaultZoom;
            if (!Mathf.IsEqualApprox(_sharedTargetZoom, DefaultZoom))
            {
                current = _sharedTargetZoom;
            }
            _sharedTargetZoom = ClampZoom(current);
            Zoom = Vector2.One * _sharedTargetZoom;
        }

        public override void _UnhandledInput(InputEvent inputEvent)
        {
            if (!IsActiveCamera())
            {
                return;
            }

            if (inputEvent is InputEventMouseButton mouse && mouse.Pressed)
            {
                if (mouse.ButtonIndex == MouseButton.WheelUp)
                {
                    AdjustZoom(WheelStep);
                    GetViewport().SetInputAsHandled();
                }
                else if (mouse.ButtonIndex == MouseButton.WheelDown)
                {
                    AdjustZoom(-WheelStep);
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

            if (inputEvent is not InputEventKey key || !key.Pressed || key.Echo)
            {
                return;
            }

            if (key.Keycode == Key.Equal || key.Keycode == Key.KpAdd)
            {
                AdjustZoom(WheelStep);
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.Minus || key.Keycode == Key.KpSubtract)
            {
                AdjustZoom(-WheelStep);
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.Key0 || key.Keycode == Key.Kp0)
            {
                _overviewMode = false;
                SetTargetZoom(DefaultZoom);
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.F6)
            {
                _overviewMode = !_overviewMode;
                SetTargetZoom(_overviewMode ? OverviewZoom : DefaultZoom);
                GetViewport().SetInputAsHandled();
            }
        }

        public override void _Process(double delta)
        {
            if (!Enabled && !IsCurrent())
            {
                return;
            }

            _sharedTargetZoom = ClampZoom(_sharedTargetZoom);
            float t = 1f - Mathf.Exp(-SmoothSpeed * (float)delta);
            Zoom = Zoom.Lerp(Vector2.One * _sharedTargetZoom, t);
        }

        public void SetTargetZoom(float value)
        {
            _sharedTargetZoom = ClampZoom(value);
        }

        private void AdjustZoom(float amount)
        {
            _overviewMode = false;
            SetTargetZoom(_sharedTargetZoom + amount);
        }

        private float ClampZoom(float value)
        {
            float min = Mathf.Min(MinZoom, MaxZoom);
            float max = Mathf.Max(MinZoom, MaxZoom);
            return Mathf.Clamp(value, min, max);
        }

        private bool IsActiveCamera()
        {
            return Enabled && GetViewport()?.GetCamera2D() == this;
        }
    }
}
