namespace AshesofaDyingWorld.UI.Theme
{
    public static class UiMotion
    {
        public static float ResolveDuration(float normalSeconds)
        {
            return normalSeconds <= 0f || IsReducedMotionEnabled() ? 0f : normalSeconds;
        }

        private static bool IsReducedMotionEnabled()
        {
            return AshesofaDyingWorld.Core.Managers.SettingsManager.Instance?.CurrentSettings?.ReducedMotion ?? false;
        }
    }
}
