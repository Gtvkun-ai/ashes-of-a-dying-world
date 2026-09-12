using Godot;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Budget path toàn cục theo physics frame.
    /// Khi nhiều AI cùng đổi target, không cho tất cả ép NavigationServer tính lại path cùng một frame.
    /// </summary>
    internal static class CombatPathBudget
    {
        private static ulong _lastPhysicsFrame = ulong.MaxValue;
        private static int _usedThisFrame;

        public static bool TryConsume(int maxRequestsPerPhysicsFrame)
        {
            int safeBudget = Mathf.Max(1, maxRequestsPerPhysicsFrame);
            ulong frame = Engine.GetPhysicsFrames();
            if (frame != _lastPhysicsFrame)
            {
                _lastPhysicsFrame = frame;
                _usedThisFrame = 0;
            }

            if (_usedThisFrame >= safeBudget)
            {
                return false;
            }

            _usedThisFrame++;
            return true;
        }
    }
}
