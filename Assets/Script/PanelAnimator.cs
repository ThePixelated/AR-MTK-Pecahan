using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class PanelAnimator : MonoBehaviour
{
    [SerializeField] private List<PanelAnimEntry> entries = new List<PanelAnimEntry>();

    private readonly Dictionary<int, Coroutine> activeRuns = new Dictionary<int, Coroutine>();

    public void Trigger(int startIndex)
    {
        if (activeRuns.TryGetValue(startIndex, out var running) && running != null)
            StopCoroutine(running);

        activeRuns[startIndex] = StartCoroutine(RunChain(startIndex));
    }

    private IEnumerator RunChain(int index)
    {
        while (index != -1)
        {
            var entry = entries[index];
            RectTransform pivot = entry.Type == AnimType.OPEN ? entry.OpenPivot : entry.ClosePivot;
            RectState to = RectState.From(pivot);

            yield return RectTween.Run(entry.Target, to, entry.Duration, entry.Ease);

            index = entry.GoToNextIndex;
        }
    }
}