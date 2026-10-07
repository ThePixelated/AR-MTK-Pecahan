using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class PanelAnimator : MonoBehaviour
{
    [SerializeField] private List<PanelAnimEntry> entries = new List<PanelAnimEntry>();

    // Kunci = RectTransform yang digerakkan entri awal chain. OPEN dan CLOSE pada target yang sama
    // tidak pernah berjalan bersamaan: yang baru menggantikan yang lama, lanjut dari posisi saat ini.
    private readonly Dictionary<RectTransform, Coroutine> activeRuns = new Dictionary<RectTransform, Coroutine>();

    public void Trigger(int startIndex)
    {
        if (startIndex < 0 || startIndex >= entries.Count)
        {
            Debug.LogError($"[{name}] PanelAnimator.Trigger: indeks {startIndex} di luar jangkauan ({entries.Count} entri)", this);
            return;
        }

        RectTransform key = entries[startIndex].Target;
        if (key == null)
        {
            Debug.LogError($"[{name}] PanelAnimator.Trigger: Target entri {startIndex} kosong", this);
            return;
        }

        if (activeRuns.TryGetValue(key, out var running) && running != null)
            StopCoroutine(running);

        activeRuns[key] = StartCoroutine(RunChain(startIndex));
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