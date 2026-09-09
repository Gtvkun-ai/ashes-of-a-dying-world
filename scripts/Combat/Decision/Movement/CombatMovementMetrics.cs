using Godot;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Bộ đếm baseline cho P0 movement.
    /// Mục tiêu không phải profiler thay thế Godot Profiler, mà là cho biết AI đang tốn tiền ở đâu:
    /// path request, probe vật cản, số lần kẹt và số lần RVO phải sửa preferred velocity.
    /// </summary>
    public sealed class CombatMovementMetrics
    {
        public ulong SolverTicks { get; private set; }
        public ulong ProbeSamples { get; private set; }
        public ulong PathRequests { get; private set; }
        public ulong PathDirectionsUsed { get; private set; }
        public ulong StuckEvents { get; private set; }
        public ulong CollisionFrames { get; private set; }
        public ulong CollisionContacts { get; private set; }
        public ulong AvoidanceSubmissions { get; private set; }
        public ulong AvoidanceCorrections { get; private set; }
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
        public void RecordPathRequest() => PathRequests++;
        public void RecordPathDirectionUsed() => PathDirectionsUsed++;
        public void RecordStuck() => StuckEvents++;
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
            PathRequests = 0;
            PathDirectionsUsed = 0;
            StuckEvents = 0;
            CollisionFrames = 0;
            CollisionContacts = 0;
            AvoidanceSubmissions = 0;
            AvoidanceCorrections = 0;
            SolverMicroseconds = 0;
            _solveStartUsec = 0;
        }

        public string ToCompactString()
        {
            double averageUsec = SolverTicks > 0
                ? (double)SolverMicroseconds / SolverTicks
                : 0.0;
            return $"ticks={SolverTicks} cpu={averageUsec:0.0}us "
                + $"probes={ProbeSamples} paths={PathRequests}/{PathDirectionsUsed} "
                + $"stuck={StuckEvents} collision={CollisionFrames}/{CollisionContacts} "
                + $"rvo={AvoidanceCorrections}/{AvoidanceSubmissions}";
        }
    }
}
