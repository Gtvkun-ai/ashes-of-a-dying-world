using Godot;
using AshesofaDyingWorld.Combat.Data;

namespace AshesofaDyingWorld.Combat.Runtime
{
    /// <summary>
    /// Runtime status chung cho combatant. Hiện hỗ trợ slow, chill và freeze.
    /// Status không chứa logic skill cụ thể; HitProfile chỉ khai báo dữ liệu cần áp dụng.
    /// </summary>
    public sealed class CombatStatusController
    {
        public readonly struct ApplicationResult
        {
            public bool SlowApplied { get; }
            public bool ChillApplied { get; }
            public bool FreezeStarted { get; }

            public ApplicationResult(bool slowApplied, bool chillApplied, bool freezeStarted)
            {
                SlowApplied = slowApplied;
                ChillApplied = chillApplied;
                FreezeStarted = freezeStarted;
            }
        }

        private float _slowPercent;
        private float _slowRemaining;
        private int _chillStacks;
        private float _chillRemaining;
        private float _freezeRemaining;

        public bool IsSlowed => _slowRemaining > 0f && _slowPercent > 0f;
        public bool HasChill => _chillStacks > 0 && _chillRemaining > 0f;
        public bool IsFrozen => _freezeRemaining > 0f;
        public int ChillStacks => HasChill ? _chillStacks : 0;
        public float SlowRemaining => Mathf.Max(0f, _slowRemaining);
        public float ChillRemaining => Mathf.Max(0f, _chillRemaining);
        public float FreezeRemaining => Mathf.Max(0f, _freezeRemaining);
        public float MoveSpeedMultiplier => IsFrozen
            ? 0f
            : IsSlowed ? Mathf.Clamp(1f - _slowPercent / 100f, 0.1f, 1f) : 1f;
        public float AttackSpeedMultiplier => IsFrozen
            ? 0.05f
            : IsSlowed ? Mathf.Clamp(1f - _slowPercent * 0.45f / 100f, 0.35f, 1f) : 1f;

        public void Update(float delta)
        {
            float dt = Mathf.Max(0f, delta);

            if (_slowRemaining > 0f)
            {
                _slowRemaining -= dt;
                if (_slowRemaining <= 0f)
                {
                    _slowRemaining = 0f;
                    _slowPercent = 0f;
                }
            }

            if (_chillRemaining > 0f)
            {
                _chillRemaining -= dt;
                if (_chillRemaining <= 0f)
                {
                    _chillRemaining = 0f;
                    _chillStacks = 0;
                }
            }

            if (_freezeRemaining > 0f)
            {
                _freezeRemaining -= dt;
                if (_freezeRemaining <= 0f)
                {
                    _freezeRemaining = 0f;
                }
            }
        }

        public ApplicationResult Apply(HitProfileData profile)
        {
            if (profile == null)
            {
                return default;
            }

            bool slowApplied = false;
            bool chillApplied = false;
            bool freezeStarted = false;

            if (profile.SlowPercent > 0f && profile.SlowSeconds > 0f)
            {
                _slowPercent = Mathf.Max(_slowPercent, Mathf.Clamp(profile.SlowPercent, 0f, 90f));
                _slowRemaining = Mathf.Max(_slowRemaining, profile.SlowSeconds);
                slowApplied = true;
            }

            if (profile.ChillStacks > 0)
            {
                _chillStacks = Mathf.Clamp(_chillStacks + profile.ChillStacks, 0, 99);
                _chillRemaining = Mathf.Max(_chillRemaining, Mathf.Max(0.1f, profile.ChillSeconds));
                chillApplied = true;

                int freezeThreshold = Mathf.Max(1, profile.FreezeAtChillStacks);
                if (_chillStacks >= freezeThreshold && profile.FreezeSeconds > 0f)
                {
                    StartFreeze(profile.FreezeSeconds);
                    _chillStacks = 0;
                    _chillRemaining = 0f;
                    freezeStarted = true;
                }
            }
            else if (profile.FreezeSeconds > 0f && profile.FreezeOnHit)
            {
                StartFreeze(profile.FreezeSeconds);
                freezeStarted = true;
            }

            return new ApplicationResult(slowApplied, chillApplied, freezeStarted);
        }

        public bool ConsumeFrozenForShatter()
        {
            if (!IsFrozen)
            {
                return false;
            }

            _freezeRemaining = 0f;
            _chillStacks = 0;
            _chillRemaining = 0f;
            _slowPercent = 0f;
            _slowRemaining = 0f;
            return true;
        }

        public void Clear()
        {
            _slowPercent = 0f;
            _slowRemaining = 0f;
            _chillStacks = 0;
            _chillRemaining = 0f;
            _freezeRemaining = 0f;
        }

        public string GetCompactLabel()
        {
            if (IsFrozen)
            {
                return $"FROZEN {FreezeRemaining:0.0}s";
            }

            if (HasChill && IsSlowed)
            {
                return $"CHILL x{ChillStacks}  SLOW";
            }

            if (HasChill)
            {
                return $"CHILL x{ChillStacks}";
            }

            return IsSlowed ? "SLOW" : string.Empty;
        }

        private void StartFreeze(float seconds)
        {
            _freezeRemaining = Mathf.Max(_freezeRemaining, Mathf.Max(0.05f, seconds));
            _slowPercent = Mathf.Max(_slowPercent, 55f);
            _slowRemaining = Mathf.Max(_slowRemaining, _freezeRemaining + 0.35f);
        }
    }
}
