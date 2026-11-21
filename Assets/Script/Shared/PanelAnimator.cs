using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class PanelAnimator : MonoBehaviour
{
    [SerializeField] private List<PanelAnimEntry> entries = new List<PanelAnimEntry>();

    // Kunci = RectTransform yang digerakkan entri awal chain. OPEN dan CLOSE pada target yang sama
    // tidak pernah berjalan bersamaan: yang baru menggantikan yang lama, lanjut dari posisi saat ini.
    private readonly Dictionary<RectTransform, Coroutine> activeRuns = new Dictionary<RectTransform, Coroutine>();

    // Menjalankan entri ini lalu mengikuti chain (GoToNextIndex) seperti biasa
    public void Trigger(int startIndex) => Run(startIndex, followChain: true);

    // Menjalankan HANYA entri ini, GoToNextIndex diabaikan. Dipakai pemanggil yang mengatur sendiri
    // urutan open -> hold -> close (mis. NotificationView), supaya tidak bergantung pada isi chain di Inspector.
    public void TriggerSingle(int startIndex) => Run(startIndex, followChain: false);

    // Durasi satu entri (detik); 0 kalau indeks tidak ada
    public float DurationOf(int index) =>
        index >= 0 && index < entries.Count ? entries[index].Duration : 0f;

    private void Run(int startIndex, bool followChain)
    {
        if (startIndex < 0 || startIndex >= entries.Count)
        {
            Debug.LogError($"[{name}] PanelAnimator: indeks {startIndex} di luar jangkauan ({entries.Count} entri)", this);
            return;
        }

        RectTransform key = entries[startIndex].Target;
        if (key == null)
        {
            Debug.LogError($"[{name}] PanelAnimator: Target entri {startIndex} kosong", this);
            return;
        }

        if (activeRuns.TryGetValue(key, out var running) && running != null)
            StopCoroutine(running);

        activeRuns[key] = StartCoroutine(RunChain(startIndex, followChain));
    }

    private IEnumerator RunChain(int index, bool followChain)
    {
        while (index != -1)
        {
            var entry = entries[index];
            RectTransform pivot = entry.Type == AnimType.OPEN ? entry.OpenPivot : entry.ClosePivot;
            RectState to = RectState.From(pivot);

            yield return RectTween.Run(entry.Target, to, entry.Duration, entry.Ease);

            index = followChain ? entry.GoToNextIndex : -1;
        }
    }
}