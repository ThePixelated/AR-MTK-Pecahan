using System.Collections;
using UnityEngine;

public static class RectTween
{
    public static IEnumerator Run(RectTransform target, RectState to, float duration, EaseType ease, System.Action onComplete = null)
    {
        RectState from = RectState.From(target);
        if (duration <= 0f)
        {
            to.ApplyTo(target);
            onComplete?.Invoke();
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            RectState.Lerp(from, to, Easing.Evaluate(ease, Mathf.Clamp01(t / duration))).ApplyTo(target);
            yield return null;
        }
        to.ApplyTo(target);
        onComplete?.Invoke();
    }
}