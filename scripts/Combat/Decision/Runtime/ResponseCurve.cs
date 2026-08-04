using Godot;
using System;

namespace AshesofaDyingWorld.Combat.Decision.Runtime
{
    /// <summary>
    /// Thư viện curve nhỏ dùng chung. Mọi input/output đều được kẹp 0..1 để trace dễ đọc và tune.
    /// </summary>
    public static class ResponseCurve
    {
        public static float Linear(float value)
        {
            return Mathf.Clamp(value, 0f, 1f);
        }

        public static float InverseLinear(float value)
        {
            return 1f - Linear(value);
        }

        /// <summary>
        /// Band có plateau thật bên trong [min, max] và falloff mềm ở hai mép.
        /// Bản cũ trả 0 ngay tại min/max, khiến 138 px gần như bị xem là ngoài band 105-140.
        /// </summary>
        public static float SmoothBand(float value, float min, float max, float edge)
        {
            float safeMin = Mathf.Min(min, max);
            float safeMax = Mathf.Max(min, max);
            float safeEdge = Mathf.Max(0.001f, edge);

            if (value < safeMin)
            {
                return SmoothRamp(value, safeMin - safeEdge, safeMin);
            }

            if (value <= safeMax)
            {
                return 1f;
            }

            return 1f - SmoothRamp(value, safeMax, safeMax + safeEdge);
        }

        /// <summary>
        /// Ramp mềm từ 0 ở start tới 1 ở end, dùng cho approach/retreat pressure.
        /// </summary>
        public static float SmoothRamp(float value, float start, float end)
        {
            float safeStart = Mathf.Min(start, end);
            float safeEnd = Mathf.Max(start, end);
            float width = Mathf.Max(0.001f, safeEnd - safeStart);
            float t = Mathf.Clamp((value - safeStart) / width, 0f, 1f);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        public static float InverseSmoothRamp(float value, float start, float end)
        {
            return 1f - SmoothRamp(value, start, end);
        }

        public static float Logistic(float value, float midpoint = 0.5f, float steepness = 10f)
        {
            float x = Mathf.Clamp(value, 0f, 1f);
            return 1f / (1f + Mathf.Exp(-steepness * (x - midpoint)));
        }

        public static float Bell(float value, float mean = 0.5f, float sigma = 0.15f)
        {
            float x = Mathf.Clamp(value, 0f, 1f);
            float safeSigma = Mathf.Max(0.001f, sigma);
            float distance = (x - mean) / safeSigma;
            return Mathf.Exp(-0.5f * distance * distance);
        }

        public static float WeightedGeometricMean(
            ReadOnlySpan<float> scores,
            ReadOnlySpan<float> weights,
            float epsilon = 0.001f)
        {
            int count = Math.Min(scores.Length, weights.Length);
            if (count <= 0)
            {
                return 0f;
            }

            float sumWeights = 0f;
            float sumLog = 0f;
            for (int i = 0; i < count; i++)
            {
                float weight = Mathf.Max(0.001f, weights[i]);
                float score = Mathf.Clamp(scores[i], epsilon, 1f);
                sumWeights += weight;
                sumLog += weight * Mathf.Log(score);
            }

            return sumWeights <= 0f ? 0f : Mathf.Exp(sumLog / sumWeights);
        }
    }
}
