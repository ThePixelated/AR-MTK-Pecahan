using UnityEngine;

public enum EaseType { Linear, InCubic, OutCubic, OutBounce, InOutQuad }

public static class Easing
{
    public static float Evaluate(EaseType type, float t) => type switch
    {
        EaseType.Linear => t,
        EaseType.InCubic => t * t * t,
        EaseType.OutCubic => 1f - Mathf.Pow(1f - t, 3f),
        EaseType.OutBounce => OutBounce(t),
        EaseType.InOutQuad => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f,
        _ => t
    };

    private static float OutBounce(float t)
    {
        const float n1 = 7.5625f, d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
        if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
        t -= 2.625f / d1; return n1 * t * t + 0.984375f;
    }
}