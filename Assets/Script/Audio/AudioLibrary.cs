using System;
using System.Collections.Generic;
using UnityEngine;

// Tabel AudioId -> AudioCue. Satu aset untuk seluruh aplikasi (dibuat alat Editor).
[CreateAssetMenu(menuName = "AR Pecahan/Audio/Library")]
public class AudioLibrary : ScriptableObject
{
    [SerializeField] List<AudioCue> cues = new List<AudioCue>();
    Dictionary<AudioId, AudioCue> map;

    public IReadOnlyList<AudioCue> Cues => cues;

    public void SetCues(List<AudioCue> list)
    {
        cues = list;
        map = null;
    }

    public bool TryGet(AudioId id, out AudioCue cue)
    {
        if (map == null) Build();
        return map.TryGetValue(id, out cue) && cue != null;
    }

    void Build()
    {
        map = new Dictionary<AudioId, AudioCue>();
        foreach (AudioCue c in cues)
        {
            if (c == null || c.id == AudioId.None) continue;
            map[c.id] = c;        // id ganda: yang terakhir menang
        }
    }

    void OnEnable() => map = null;
    void OnValidate() => map = null;

    [ContextMenu("Periksa library")]
    void Inspect()
    {
        Build();
        int missing = 0, empty = 0;
        foreach (AudioId id in Enum.GetValues(typeof(AudioId)))
        {
            if (id == AudioId.None) continue;
            if (!map.TryGetValue(id, out AudioCue c) || c == null) { Debug.LogWarning($"[{name}] cue {id} belum ada di library.", this); missing++; }
            else if (!c.HasClips) empty++;
        }
        Debug.Log($"[{name}] {map.Count} cue terdaftar, {missing} belum ada, {empty} belum punya klip.", this);
    }
}