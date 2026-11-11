#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AudioLibraryBuilder
{
    const string Root = "Assets/Audio";
    const string CuesDir = Root + "/Cues";
    const string ClipsDir = Root + "/Clips";
    const string LibraryPath = Root + "/AudioLibrary.asset";
    const string SettingsPath = Root + "/AudioSettings.asset";

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    [MenuItem("AR Pecahan/Audio/Buat atau perbarui cue dan library")]
    public static void Build()
    {
        EnsureFolder(CuesDir);
        EnsureFolder(ClipsDir);

        var settings = AssetDatabase.LoadAssetAtPath<AudioSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<AudioSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        var list = new List<AudioCue>();
        int created = 0;
        foreach (AudioId id in Enum.GetValues(typeof(AudioId)))
        {
            if (id == AudioId.None) continue;

            string path = $"{CuesDir}/{id}.asset";
            var cue = AssetDatabase.LoadAssetAtPath<AudioCue>(path);
            if (cue == null)
            {
                cue = ScriptableObject.CreateInstance<AudioCue>();
                if (AudioCueDefaults.TryGet(id, out AudioCueDefaults.Row r))
                {
                    cue.bus = r.bus; cue.loop = r.loop; cue.volume = r.volume; cue.pitchJitter = r.pitchJitter;
                    cue.spatialBlend = r.spatialBlend; cue.cooldown = r.cooldown; cue.maxVoices = r.maxVoices;
                    cue.priority = r.priority; cue.fadeIn = r.fadeIn; cue.fadeOut = r.fadeOut;
                    cue.duckAmount = r.duckAmount; cue.duckSeconds = r.duckSeconds; cue.stopWhenMuffled = r.stopWhenMuffled;
                }
                AssetDatabase.CreateAsset(cue, path);
                created++;
            }
            cue.id = id;
            EditorUtility.SetDirty(cue);
            list.Add(cue);
        }

        var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        library.SetCues(list);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        int linked = LinkClips(false);
        Debug.Log($"[Audio] {list.Count} cue ({created} baru), {linked} cue dihubungkan ke klip. Library: {LibraryPath}", library);
    }

    [MenuItem("AR Pecahan/Audio/Hubungkan klip dari Assets/Audio/Clips")]
    public static void LinkClipsMenu()
    {
        int n = LinkClips(false);
        Debug.Log($"[Audio] {n} cue yang tadinya kosong sekarang punya klip.");
    }

    [MenuItem("AR Pecahan/Audio/Hubungkan klip (timpa semua)")]
    public static void LinkClipsOverwriteMenu()
    {
        if (!EditorUtility.DisplayDialog("Timpa klip", "Daftar klip semua cue diganti dengan isi Assets/Audio/Clips. Lanjutkan?", "Ya", "Batal")) return;
        int n = LinkClips(true);
        Debug.Log($"[Audio] {n} cue diperbarui klipnya.");
    }

    // Nama file: <AudioId>.ext atau <AudioId>_<angka>.ext (mis. Ui_Click_01.wav)
    static int LinkClips(bool overwrite)
    {
        var byId = new Dictionary<AudioId, List<AudioClip>>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipsDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);

            string baseName = file;
            int us = file.LastIndexOf('_');
            if (us > 0 && us < file.Length - 1 && IsDigits(file, us + 1)) baseName = file.Substring(0, us);

            if (!Enum.TryParse(baseName, out AudioId id) || id == AudioId.None) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;
            if (!byId.TryGetValue(id, out List<AudioClip> clips)) { clips = new List<AudioClip>(); byId[id] = clips; }
            clips.Add(clip);
        }

        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioCue", new[] { CuesDir }))
        {
            var cue = AssetDatabase.LoadAssetAtPath<AudioCue>(AssetDatabase.GUIDToAssetPath(guid));
            if (cue == null || !byId.TryGetValue(cue.id, out List<AudioClip> clips)) continue;
            if (!overwrite && cue.HasClips) continue;

            clips.Sort((x, y) => string.Compare(x.name, y.name, StringComparison.Ordinal));
            var entries = new AudioClipEntry[clips.Count];
            for (int i = 0; i < clips.Count; i++) entries[i] = new AudioClipEntry { clip = clips[i], weight = 1f };
            cue.clips = entries;
            EditorUtility.SetDirty(cue);
            changed++;
        }
        AssetDatabase.SaveAssets();
        return changed;
    }

    static bool IsDigits(string s, int from)
    {
        for (int i = from; i < s.Length; i++) if (!char.IsDigit(s[i])) return false;
        return true;
    }

    [MenuItem("AR Pecahan/Audio/Laporan cue tanpa klip")]
    public static void Report()
    {
        int empty = 0, total = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioCue", new[] { CuesDir }))
        {
            var cue = AssetDatabase.LoadAssetAtPath<AudioCue>(AssetDatabase.GUIDToAssetPath(guid));
            if (cue == null) continue;
            total++;
            if (cue.HasClips) continue;
            empty++;
            Debug.Log($"[Audio] belum ada klip: {cue.id} ({cue.bus})", cue);
        }
        Debug.Log($"[Audio] {total - empty} dari {total} cue sudah punya klip, {empty} belum.");
    }
}
#endif