using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Mencari komponen script yang hilang langsung dari file scene/prefab (YAML teks).
// Melaporkan objek, jalur hierarki, dan GUID script yang hilang. Simpan scene (Ctrl+S) dulu sebelum menjalankan.
public static class MissingScriptFinder
{
    static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(\d+)");
    static readonly Regex ScriptGuid = new Regex(@"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32}), type: \d\}");
    static readonly Regex FileId = new Regex(@"\{fileID: (-?\d+)\}");

    [MenuItem("Tools/AR Pecahan/Cari script hilang (scene dan prefab)")]
    static void Run()
    {
        var paths = new List<string>();
        foreach (string g in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })) paths.Add(AssetDatabase.GUIDToAssetPath(g));
        foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })) paths.Add(AssetDatabase.GUIDToAssetPath(g));

        var lost = new Dictionary<string, List<string>>();      // GUID tidak ada di project -> lokasi
        var unloadable = new Dictionary<string, List<string>>(); // script ada, class tidak termuat -> lokasi

        foreach (string p in paths) Scan(p, lost, unloadable);

        if (lost.Count == 0 && unloadable.Count == 0)
        {
            Debug.Log("[ScriptHilang] Tidak ada komponen bermasalah di scene dan prefab yang tersimpan.");
            return;
        }

        foreach (var kv in lost)
        {
            Debug.LogWarning($"[ScriptHilang] GUID {kv.Key} TIDAK ADA di project, dipakai {kv.Value.Count} kali. Contoh: {kv.Value[0]}\n" +
                             $"Cari nama script-nya di riwayat git:\n" +
                             $"git log --all --name-only --pretty=format:\"%h %ad %s\" --date=short -S\"{kv.Key}\" -- \"*.meta\"");
        }
        foreach (var kv in unloadable)
        {
            Debug.LogWarning($"[ScriptHilang] Script ADA tapi class-nya tidak termuat: {kv.Key} ({kv.Value.Count} kali, contoh: {kv.Value[0]}). " +
                             "Biasanya nama file tidak sama dengan nama class, atau ada compile error.");
        }
    }

    static void Scan(string assetPath, Dictionary<string, List<string>> lost, Dictionary<string, List<string>> unloadable)
    {
        string full = Path.GetFullPath(assetPath);
        if (!File.Exists(full)) return;

        var goName = new Dictionary<string, string>();   // id GameObject -> nama
        var tfOfGo = new Dictionary<string, string>();   // id GameObject -> id Transform
        var tfGo = new Dictionary<string, string>();     // id Transform -> id GameObject
        var tfFather = new Dictionary<string, string>(); // id Transform -> id Transform induk
        var monos = new List<KeyValuePair<string, string>>();   // (id GameObject, GUID script)

        string type = null, id = null, curGo = null;
        foreach (string line in File.ReadLines(full))
        {
            Match h = Header.Match(line);
            if (h.Success) { type = h.Groups[1].Value; id = h.Groups[2].Value; curGo = null; continue; }
            if (type == null) continue;
            string t = line.TrimStart();

            if (type == "1")
            {
                if (t.StartsWith("m_Name:")) goName[id] = t.Substring(7).Trim();
            }
            else if (type == "4" || type == "224")        // Transform / RectTransform
            {
                if (t.StartsWith("m_GameObject:")) { string go = FileId.Match(t).Groups[1].Value; tfGo[id] = go; tfOfGo[go] = id; }
                else if (t.StartsWith("m_Father:")) tfFather[id] = FileId.Match(t).Groups[1].Value;
            }
            else if (type == "114")                       // MonoBehaviour
            {
                if (t.StartsWith("m_GameObject:")) curGo = FileId.Match(t).Groups[1].Value;
                else if (t.StartsWith("m_Script:"))
                {
                    Match m = ScriptGuid.Match(t);
                    if (m.Success) monos.Add(new KeyValuePair<string, string>(curGo, m.Groups[1].Value));
                }
            }
        }

        string PathOf(string go)
        {
            var names = new List<string>();
            string cur = go;
            for (int guard = 0; cur != null && guard < 64; guard++)
            {
                names.Insert(0, goName.TryGetValue(cur, out string n) ? n : "?");
                if (!tfOfGo.TryGetValue(cur, out string tf) || !tfFather.TryGetValue(tf, out string father)
                    || father == "0" || !tfGo.TryGetValue(father, out string parentGo)) break;
                cur = parentGo;
            }
            return string.Join("/", names);
        }

        foreach (var m in monos)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(m.Value);
            string where = $"{assetPath} :: {PathOf(m.Key)}";

            if (string.IsNullOrEmpty(scriptPath))
            {
                if (!lost.TryGetValue(m.Value, out var list)) lost[m.Value] = list = new List<string>();
                list.Add(where);
            }
            else
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script != null && script.GetClass() == null)
                {
                    if (!unloadable.TryGetValue(scriptPath, out var list)) unloadable[scriptPath] = list = new List<string>();
                    list.Add(where);
                }
            }
        }
    }
}