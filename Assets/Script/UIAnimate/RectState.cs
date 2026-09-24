using UnityEngine;

[System.Serializable]
public struct RectState
{
    public Vector2 anchoredPosition;
    public Vector2 sizeDelta;

    public static RectState From(RectTransform rt) => new RectState
    {
        anchoredPosition = rt.anchoredPosition,
        sizeDelta = rt.sizeDelta
    };

    public static RectState Lerp(RectState a, RectState b, float t) => new RectState
    {
        anchoredPosition = Vector2.LerpUnclamped(a.anchoredPosition, b.anchoredPosition, t),
        sizeDelta = Vector2.LerpUnclamped(a.sizeDelta, b.sizeDelta, t)
    };

    public void ApplyTo(RectTransform rt)
    {
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
    }
}