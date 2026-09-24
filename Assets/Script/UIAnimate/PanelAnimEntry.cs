using UnityEngine;

[System.Serializable]
public class PanelAnimEntry
{
    public string Name;
    public RectTransform Target;
    public RectTransform OpenPivot;
    public RectTransform ClosePivot;
    public float Duration = 0.3f;
    public EaseType Ease = EaseType.InCubic;
    public AnimType Type;
    public int GoToNextIndex = -1;
}

public enum AnimType { OPEN, CLOSE }