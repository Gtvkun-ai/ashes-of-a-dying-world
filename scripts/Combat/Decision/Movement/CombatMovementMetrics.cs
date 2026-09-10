using Godot;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Metrics P2 cho movement benchmark.
    /// Ngoài baseline collision/stuck/CPU của P0, P1 đo thêm path reuse/budget deferral,
    /// mức probe 4/8/16 hướng, ShapeCast thân và số lần khóa passing-side head-on.
    /// </summary>
    public sealed class CombatMovementMetrics
    {
        public ulong SolverTicks { get; private set; }
        public ulong ProbeSamples { get; private set; }
        public ulong ShapeProbeSamples { get; private set; }
        public ulong SparseProbeRefreshes { get; private set; }
        public ulong HalfProbeRefreshes { get; private set; }
        public ulong DenseProbeRefreshes { get; private set; }
        public ulong PathRequests { get; private set; }
        public ulong PathDirectionsUsed { get; private set; }
        public ulong PathReuseHits { get; private set; }
        public ulong PathBudgetDeferrals { get; private set; }
        public ulong StuckEvents { get; private set; }
        // Raw = mọi slide contact Godot báo. Blocking = contact thật sự cắt tiến độ về phía trước.
        // Benchmark P2.1 dùng Blocking làm quality gate; Raw chỉ giữ để chẩn đoán.
        public ulong RawCollisionFrames { get; private set; }
        public ulong RawCollisionContacts { get; private set; }
        public ulong BlockingCollisionFrames { get; private set; }
        public ulong BlockingCollisionContacts { get; private set; }

        // Alias tương thích debug cũ: từ P2.1 CollisionFrames/Contacts nghĩa là blocking collision.
        public ulong CollisionFrames => BlockingCollisionFrames;
        public ulong CollisionContacts => BlockingCollisionContacts;
        public ulong AvoidanceSubmissions { get; private set; }
        public ulong AvoidanceCorrections { get; private set; }
        public ulong PassingSideLocks { get; private set; }
        public ulong SolverMicroseconds { get; private set; }
        public ulong MotorTicks { get; private set; }
        public ulong MotorMicroseconds { get; private set; }

        private ulong _solveStartUsec;
        private ulong _motorStartUsec;

        public void BeginSolve()
        {
            SolverTicks++;
            _solveStartUsec = Time.GetTicksUsec();
        }

        public void EndSolve()
        {
            ulong now = Time.GetTicksUsec();
            if (now >= _solveStartUsec)
            {
                SolverMicroseconds += now - _solveStartUsec;
            }
        }

        /// <summary>
        /// P2 đo luôn local motor/RVO ở physics-rate. P0/P1 chỉ đo Solve() nên CPU benchmark trước đây
        /// chưa phản ánh phần chạy 30-60 Hz quan trọng nhất của locomotion.
        /// </summary>
        public void BeginMotor()
        {
            MotorTicks++;
            _motorStartUsec = Time.GetTicksUsec();
        }

        public void EndMotor()
        {
            ulong now = Time.GetTicksUsec();
            if (now >= _motorStartUsec)
            {
                MotorMicroseconds += now - _motorStartUsec;
            }
        }

        public void RecordProbe(int count = 1) => ProbeSamples += (ulong)(count > 0 ? count : 0);
        public void RecordShapeProbe() => ShapeProbeSamples++;
        public void RecordProbeRefresh(int sampledRayCount)
        {
            if (sampledRayCount >= CombatStaticClearance.DirectionCount)
            {
                DenseProbeRefreshes++;
            }
            else if (sampledRayCount >= CombatStaticClearance.DirectionCount / 2)
            {
                HalfProbeRefreshes++;
            }
            else
            {
                SparseProbeRefreshes++;
            }
        }
        public void RecordPathRequest() => PathRequests++;
        public void RecordPathDirectionUsed() => PathDirectionsUsed++;
        public void RecordPathReuse() => PathReuseHits++;
        public void RecordPathBudgetDeferral() => PathBudgetDeferrals++;
        public void RecordStuck() => StuckEvents++;
        public void RecordPassingSideLock() => PassingSideLocks++;
        public void RecordRawCollisionFrame(int contacts)
        {
            RawCollisionFrames++;
            RawCollisionContacts += (ulong)(contacts > 0 ? contacts : 0);
        }

        public void RecordBlockingCollisionFrame(int contacts)
        {
            BlockingCollisionFrames++;
            BlockingCollisionContacts += (ulong)(contacts > 0 ? contacts : 0);
        }
        public void RecordAvoidanceSubmission() => AvoidanceSubmissions++;
        public void RecordAvoidanceCorrection() => AvoidanceCorrections++;

        public void Reset()
        {
            SolverTicks = 0;
            ProbeSamples = 0;
            ShapeProbeSamples = 0;
            SparseProbeRefreshes = 0;
            HalfProbeRefreshes = 0;
            DenseProbeRefreshes = 0;
            PathRequests = 0;
            PathDirectionsUsed = 0;
            PathReuseHits = 0;
            PathBudgetDeferrals = 0;
            StuckEvents = 0;
            RawCollisionFrames = 0;
            RawCollisionContacts = 0;
            BlockingCollisionFrames = 0;
            BlockingCollisionContacts = 0;
            AvoidanceSubmissions = 0;
            AvoidanceCorrections = 0;
            PassingSideLocks = 0;
            SolverMicroseconds = 0;
            MotorTicks = 0;
            MotorMicroseconds = 0;
            _solveStartUsec = 0;
            _motorStartUsec = 0;
        }

        public string ToCompactString()
        {
            double averageUsec = SolverTicks > 0
                ? (double)SolverMicroseconds / SolverTicks
                : 0.0;
            double averageMotorUsec = MotorTicks > 0
                ? (double)MotorMicroseconds / MotorTicks
                : 0.0;
            return $"ticks={SolverTicks}/{MotorTicks} cpu={averageUsec:0.0}/{averageMotorUsec:0.0}us "
                + $"probe={ProbeSamples}+{ShapeProbeSamples}s mode={SparseProbeRefreshes}/{HalfProbeRefreshes}/{DenseProbeRefreshes} "
                + $"path={PathRequests}/{PathDirectionsUsed} reuse={PathReuseHits} defer={PathBudgetDeferrals} "
                + $"stuck={StuckEvents} block={BlockingCollisionFrames}/{BlockingCollisionContacts} raw={RawCollisionFrames}/{RawCollisionContacts} "
                + $"rvo={AvoidanceCorrections}/{AvoidanceSubmissions} pass={PassingSideLocks}";
        }
    }
}
