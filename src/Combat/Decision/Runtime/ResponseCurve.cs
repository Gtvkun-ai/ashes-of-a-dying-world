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

        public static float SmoothBand(float value, float min, float max, float edge)
        {
            float safeMin = Mathf.Min(min, max);
            float safeMax = Mathf.Max(min, max);
            float safeEdge = Mathf.Max(0.001f, edge);
            float enter = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp((value - safeMin) / safeEdge, 0f, 1f));
            float exit = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp((value - safeMax + safeEdge) / safeEdge, 0f, 1f));
            return Mathf.Clamp(enter * exit, 0f, 1f);
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
