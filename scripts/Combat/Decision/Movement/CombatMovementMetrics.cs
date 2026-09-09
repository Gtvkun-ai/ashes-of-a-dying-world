using Godot;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Metrics P1 cho movement benchmark.
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
        public ulong CollisionFrames { get; private set; }
        public ulong CollisionContacts { get; private set; }
        public ulong AvoidanceSubmissions { get; private set; }
        public ulong AvoidanceCorrections { get; private set; }
        public ulong PassingSideLocks { get; private set; }
        public ulong SolverMicroseconds { get; private set; }

        private ulong _solveStartUsec;

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
        public void RecordCollisionFrame(int contacts)
        {
            CollisionFrames++;
            CollisionContacts += (ulong)(contacts > 0 ? contacts : 0);
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
            CollisionFrames = 0;
            CollisionContacts = 0;
            AvoidanceSubmissions = 0;
            AvoidanceCorrections = 0;
            PassingSideLocks = 0;
            SolverMicroseconds = 0;
            _solveStartUsec = 0;
        }

        public string ToCompactString()
        {
            double averageUsec = SolverTicks > 0
                ? (double)SolverMicroseconds / SolverTicks
                : 0.0;
            return $"ticks={SolverTicks} cpu={averageUsec:0.0}us "
                + $"probe={ProbeSamples}+{ShapeProbeSamples}s mode={SparseProbeRefreshes}/{HalfProbeRefreshes}/{DenseProbeRefreshes} "
                + $"path={PathRequests}/{PathDirectionsUsed} reuse={PathReuseHits} defer={PathBudgetDeferrals} "
                + $"stuck={StuckEvents} collision={CollisionFrames}/{CollisionContacts} "
                + $"rvo={AvoidanceCorrections}/{AvoidanceSubmissions} pass={PassingSideLocks}";
        }
    }
}
