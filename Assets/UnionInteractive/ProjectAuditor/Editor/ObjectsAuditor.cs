// ============================================================================
//  ObjectsAuditor.cs  -  Union Project Auditor / Objects Auditor
//  Letakkan satu folder dengan ScriptAuditor.cs (folder Editor, assembly yang sama).
//
//  Modul terpisah dengan konfigurasi sendiri. Mendaftarkan diri ke tab "Objects Auditor"
//  lewat UnionProjectAuditorWindow.ObjectsAuditDraw.
//
//  Fitur (v1, read-only - tidak mengubah apa pun di project):
//   1. Find Referrers  : siapa saja yang mereferensikan sebuah objek/asset (GameObject path + Asset path + field).
//   2. Outgoing        : objek ini mereferensikan apa saja (kosong / lintas scene / EditorOnly).
//   3. Script Usage    : script ini terpasang di GameObject mana saja.
//   4. Names & Tags    : Find("X") / Tag / Layer berbasis string vs objek yang benar-benar ada.
//   5. Reference Index : hasil scan disimpan (UserSettings), pencarian berikutnya instan.
//   6. Impact Preview  : dampak rename/hapus (referensi serialized + pemanggilan string).
//   7. Pins            : watchlist objek penting + perubahan jumlah referensinya.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnionInteractive.Toolkit.ProjectAuditor.Objects
{
    // ------------------------------------------------------------------ OPTIONS
    public enum ObjScope { OpenScenes = 0, BuildScenes = 1, AllProjectScenes = 2 }

    [Serializable]
    public class PinEntry
    {
        public string label;
        public string kind;
        public string container;
        public string objPath;
        public int lastCount;
    }

    [Serializable]
    public class ObjOptions
    {
        public ObjScope scope = ObjScope.BuildScenes;       // default: scene Build Settings
        public bool includePrefabs = true;                  // default: semua prefab di Assets
        public bool builtInToAssets = false;                // referensi komponen bawaan -> aset ikut diindeks
        public string excludeFolders = "Assets/TextMesh Pro;Assets/Vuforia;Assets/Plugins;Assets/Samples";
        public string scriptsRoot = "";                     // kosong = auto-detect (untuk Names & Tags)
        public bool matchComponents = true;                 // referensi ke komponen dihitung sebagai referensi ke GameObject-nya
        public bool includeChildren = true;                 // target prefab asset: termasuk child-nya
        public bool exactComponent = false;                 // target komponen: hanya tipe komponen itu
        public int maxRows = 500;
        public List<PinEntry> pins = new List<PinEntry>();
        public List<string> ignored = new List<string>();

        const string Key = "UnionAuditor.Objects.Options.v1";

        public static ObjOptions Load()
        {
            string j = EditorPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(j)) return new ObjOptions();
            try { return JsonUtility.FromJson<ObjOptions>(j) ?? new ObjOptions(); }
            catch { return new ObjOptions(); }
        }

        public void Save() { EditorPrefs.SetString(Key, JsonUtility.ToJson(this)); }
    }

    // ------------------------------------------------------------------ INDEX MODEL
    [Serializable]
    public class RefEdge
    {
        public string srcContainer;   // scene/prefab pemilik referensi
        public string srcObject;      // hierarchy path GameObject pemilik
        public string srcComponent;   // tipe component pemilik
        public string field;          // nama field (property path)
        public string tgtKind;        // "scene" | "asset"
        public string tgtContainer;   // path scene / path asset
        public string tgtObject;      // hierarchy path GameObject target ("" untuk asset non-GameObject)
        public string tgtComponent;   // tipe component target ("" jika GameObject/asset)
    }

    [Serializable]
    public class CompRecord
    {
        public string container, objectPath, type, scriptPath;
        public int refs, nulls;
        public bool inactive;
    }

    [Serializable]
    public class GoRecord
    {
        public string container, path, name, tag, prefabSource;
        public bool inactive;
    }

    [Serializable]
    public class StrCall
    {
        public string file, kind, literal;
        public int line;
    }

    [Serializable]
    public class ObjIndex
    {
        public string builtAt;
        public string scopeInfo;
        public List<string> containers = new List<string>();
        public List<RefEdge> edges = new List<RefEdge>();
        public List<CompRecord> comps = new List<CompRecord>();
        public List<GoRecord> gos = new List<GoRecord>();
        public List<StrCall> calls = new List<StrCall>();
    }

    [Serializable]
    public class TargetId
    {
        public string kind;       // "scene" | "asset"
        public string container;
        public string objPath;
        public string name;
        public string compType;

        public string Label()
        {
            return name + "   (" + container + (string.IsNullOrEmpty(objPath) ? "" : " : " + objPath) + ")";
        }

        public static TargetId From(Object obj)
        {
            if (obj == null) return null;
            var t = new TargetId { name = obj.name, compType = "" };
            var go = obj as GameObject;
            var comp = obj as Component;
            if (go == null && comp != null) { go = comp.gameObject; t.compType = comp.GetType().Name; }

            if (EditorUtility.IsPersistent(obj))
            {
                t.kind = "asset";
                t.container = AuditUtil.Norm(AssetDatabase.GetAssetPath(obj));
                t.objPath = go != null ? AuditUtil.HierarchyPath(go.transform) : "";
                return t;
            }
            if (go != null)
            {
                t.kind = "scene";
                t.container = ScenePathOf(go);
                t.objPath = AuditUtil.HierarchyPath(go.transform);
                return t;
            }
            return null;
        }

        public static string ScenePathOf(GameObject go)
        {
            string p = go.scene.IsValid() ? go.scene.path : "";
            return string.IsNullOrEmpty(p) ? "(untitled)" : AuditUtil.Norm(p);
        }
    }

    // ------------------------------------------------------------------ INDEX STORE
    static class ObjIndexStore
    {
        static string FilePath { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "UserSettings", "UnionAuditorObjectsIndex.json"); } }

        public static ObjIndex Load()
        {
            try { if (File.Exists(FilePath)) return JsonUtility.FromJson<ObjIndex>(File.ReadAllText(FilePath)); }
            catch { }
            return null;
        }

        public static void Save(ObjIndex idx)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(idx));
        }

        public static void Delete() { if (File.Exists(FilePath)) File.Delete(FilePath); }

        public static bool IsStale(ObjIndex idx)
        {
            DateTime built;
            if (idx == null || !DateTime.TryParse(idx.builtAt, out built)) return true;
            foreach (var c in idx.containers)
                if (File.Exists(c) && File.GetLastWriteTime(c) > built) return true;
            return false;
        }
    }

    // ------------------------------------------------------------------ INDEX BUILDER
    static class ObjIndexBuilder
    {
        const int MaxEdges = 250000;
        static readonly HashSet<string> SkipProps = new HashSet<string>
        { "m_Script", "m_GameObject", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset" };

        static readonly KeyValuePair<string, Regex>[] StringRx =
        {
            new KeyValuePair<string, Regex>("GameObject.Find", new Regex(@"\bGameObject\s*\.\s*Find\s*\(\s*""([^""]+)""", RegexOptions.Compiled)),
            new KeyValuePair<string, Regex>("Transform.Find", new Regex(@"(?<!GameObject)\s*\.\s*Find\s*\(\s*""([^""]+)""", RegexOptions.Compiled)),
            new KeyValuePair<string, Regex>("Tag", new Regex(@"\b(?:FindGameObjectWithTag|FindGameObjectsWithTag|FindWithTag|CompareTag)\s*\(\s*""([^""]+)""", RegexOptions.Compiled)),
            new KeyValuePair<string, Regex>("Layer", new Regex(@"\bLayerMask\s*\.\s*NameToLayer\s*\(\s*""([^""]+)""", RegexOptions.Compiled)),
        };

        public static bool Excluded(string path, string list)
        {
            if (string.IsNullOrWhiteSpace(list)) return false;
            foreach (var part in list.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string p = AuditUtil.Norm(part.Trim()).TrimEnd('/');
                if (p.Length == 0) continue;
                if (path.Equals(p, StringComparison.OrdinalIgnoreCase) || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static ObjIndex Build(ObjOptions o)
        {
            var idx = new ObjIndex
            {
                builtAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                scopeInfo = o.scope + (o.includePrefabs ? " + prefab" : "")
            };
            try
            {
                if (!ScanScenes(o, idx)) return null;
                if (o.includePrefabs) ScanPrefabs(o, idx);
                ScanStrings(o, idx);
            }
            finally { EditorUtility.ClearProgressBar(); }
            return idx;
        }

        static bool ScanScenes(ObjOptions o, ObjIndex idx)
        {
            if (o.scope == ObjScope.OpenScenes)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (!s.isLoaded) continue;
                    string sp = string.IsNullOrEmpty(s.path) ? "(untitled)" : AuditUtil.Norm(s.path);
                    ScanScene(s, sp, o, idx);
                }
                return true;
            }

            List<string> paths;
            if (o.scope == ObjScope.BuildScenes)
                paths = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => AuditUtil.Norm(s.path)).ToList();
            else
                paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(g => AuditUtil.Norm(AssetDatabase.GUIDToAssetPath(g))).ToList();
            paths = paths.Where(p => !Excluded(p, o.excludeFolders)).ToList();
            if (paths.Count == 0) return true;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Objects Auditor", "Index scene: " + Path.GetFileName(paths[i]), (float)i / paths.Count);
                    var scene = EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Single);
                    ScanScene(scene, paths[i], o, idx);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            return true;
        }

        static void ScanScene(Scene scene, string path, ObjOptions o, ObjIndex idx)
        {
            idx.containers.Add(path);
            foreach (var root in scene.GetRootGameObjects()) ScanHierarchy(root, path, o, idx);
        }

        static void ScanPrefabs(ObjOptions o, ObjIndex idx)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AuditUtil.Norm(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (Excluded(path, o.excludeFolders)) continue;
                if (i % 10 == 0)
                    EditorUtility.DisplayProgressBar("Objects Auditor", "Index prefab: " + Path.GetFileName(path), (float)i / guids.Length);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                idx.containers.Add(path);
                ScanHierarchy(root, path, o, idx);
            }
        }

        static bool ActiveChain(Transform t)
        {
            for (; t != null; t = t.parent) if (!t.gameObject.activeSelf) return false;
            return true;
        }

        static string SafeTag(GameObject go)
        {
            try { return go.tag; } catch { return ""; }
        }

        static void ScanHierarchy(GameObject root, string container, ObjOptions o, ObjIndex idx)
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                var go = tr.gameObject;
                string hp = AuditUtil.HierarchyPath(tr);
                bool inactive = !ActiveChain(tr);

                string prefabSrc = "";
                if (!EditorUtility.IsPersistent(go) && PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (src != null) prefabSrc = AuditUtil.Norm(AssetDatabase.GetAssetPath(src));
                }

                idx.gos.Add(new GoRecord { container = container, path = hp, name = go.name, tag = SafeTag(go), inactive = inactive, prefabSource = prefabSrc });

                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null || c is Transform) continue;
                    ScanComponent(c, container, hp, inactive, o, idx);
                }
            }
        }

        static bool IsUser(Component c, ObjOptions o, out string scriptPath)
        {
            scriptPath = null;
            var mb = c as MonoBehaviour;
            if (mb == null) return false;
            var ms = MonoScript.FromMonoBehaviour(mb);
            if (ms == null) return false;
            string p = AuditUtil.Norm(AssetDatabase.GetAssetPath(ms));
            scriptPath = p;
            return p.StartsWith("Assets/", StringComparison.Ordinal) && !Excluded(p, o.excludeFolders);
        }

        static bool ShouldEnter(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.String:
                case SerializedPropertyType.AnimationCurve:
                case SerializedPropertyType.Gradient:
                    return false;
            }
            if (p.isArray && p.arraySize > 1000) return false;
            return true;
        }

        // target Object -> (kind, container, objPath, compType)
        public static bool Resolve(Object val, out string kind, out string container, out string objPath, out string compType)
        {
            kind = container = objPath = compType = "";
            var go = val as GameObject;
            var comp = val as Component;
            if (go == null && comp != null) { go = comp.gameObject; compType = comp.GetType().Name; }

            if (EditorUtility.IsPersistent(val))
            {
                string ap = AuditUtil.Norm(AssetDatabase.GetAssetPath(val));
                if (!ap.StartsWith("Assets/", StringComparison.Ordinal) && !ap.StartsWith("Packages/", StringComparison.Ordinal)) return false;
                kind = "asset"; container = ap;
                if (go != null) objPath = AuditUtil.HierarchyPath(go.transform);
                return true;
            }
            if (go != null)
            {
                kind = "scene"; container = TargetId.ScenePathOf(go); objPath = AuditUtil.HierarchyPath(go.transform);
                return true;
            }
            return false;
        }

        static void ScanComponent(Component c, string container, string hp, bool inactive, ObjOptions o, ObjIndex idx)
        {
            string scriptPath;
            bool user = IsUser(c, o, out scriptPath);
            string typeName = c.GetType().Name;
            int refs = 0, nulls = 0;

            using (var so = new SerializedObject(c))
            {
                var p = so.GetIterator();
                bool enter = true;
                while (p.NextVisible(enter))
                {
                    enter = ShouldEnter(p);
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    string path = p.propertyPath;
                    if (SkipProps.Contains(path)) continue;
                    bool isEventPart = path.Contains("m_PersistentCalls");

                    var val = p.objectReferenceValue;
                    if (val == null)
                    {
                        if (user && !isEventPart) { refs++; nulls++; }
                        continue;
                    }
                    if (user && !isEventPart) refs++;
                    if (idx.edges.Count >= MaxEdges) continue;

                    string kind, tc, to, tcomp;
                    if (!Resolve(val, out kind, out tc, out to, out tcomp)) continue;
                    if (kind == "asset" && !user && !o.builtInToAssets) continue;

                    idx.edges.Add(new RefEdge
                    {
                        srcContainer = container, srcObject = hp, srcComponent = typeName,
                        field = AuditUtil.DisplayPath(path),
                        tgtKind = kind, tgtContainer = tc, tgtObject = to, tgtComponent = tcomp
                    });
                }
            }

            if (user)
                idx.comps.Add(new CompRecord { container = container, objectPath = hp, type = typeName, scriptPath = scriptPath, refs = refs, nulls = nulls, inactive = inactive });
        }

        static void ScanStrings(ObjOptions o, ObjIndex idx)
        {
            string root = string.IsNullOrEmpty(o.scriptsRoot) ? AuditUtil.DetectScriptsRoot() : AuditUtil.Norm(o.scriptsRoot).TrimEnd('/');
            if (!Directory.Exists(root)) return;
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = AuditUtil.Norm(files[i]);
                if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 || Excluded(path, o.excludeFolders)) continue;
                string[] lines;
                try { lines = File.ReadAllLines(path); } catch { continue; }
                for (int ln = 0; ln < lines.Length; ln++)
                {
                    string l = lines[ln];
                    if (l.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                    foreach (var kv in StringRx)
                        foreach (Match m in kv.Value.Matches(l))
                            idx.calls.Add(new StrCall { file = path, line = ln + 1, kind = kv.Key, literal = m.Groups[1].Value });
                }
            }
        }
    }

    // ------------------------------------------------------------------ QUERIES
    static class ObjQueries
    {
        static AuditIssue Row(Sev sev, string cat, string msg, string container, string obj, string detail = null, string group = null, int line = 0)
        {
            return new AuditIssue { severity = sev, category = cat, message = msg, assetPath = container, objectPath = obj, detail = detail, group = group, line = line };
        }

        static bool MatchTarget(RefEdge e, TargetId t, ObjOptions o)
        {
            if (e.tgtKind != t.kind || e.tgtContainer != t.container) return false;

            if (t.kind == "scene")
            {
                if (e.tgtObject != t.objPath) return false;
                if (!o.matchComponents && !string.IsNullOrEmpty(e.tgtComponent)) return false;
            }
            else if (!string.IsNullOrEmpty(t.objPath))
            {
                bool same = e.tgtObject == t.objPath;
                bool child = !string.IsNullOrEmpty(e.tgtObject) && e.tgtObject.StartsWith(t.objPath + "/", StringComparison.Ordinal);
                if (!(same || (o.includeChildren && child))) return false;
            }
            if (o.exactComponent && !string.IsNullOrEmpty(t.compType) && e.tgtComponent != t.compType) return false;
            return true;
        }

        public static int CountReferrers(ObjIndex idx, TargetId t, ObjOptions o)
        {
            int n = 0;
            foreach (var e in idx.edges) if (MatchTarget(e, t, o)) n++;
            if (t.kind == "asset") n += idx.gos.Count(g => g.prefabSource == t.container);
            return n;
        }

        public static List<AuditIssue> FindReferrers(ObjIndex idx, TargetId t, ObjOptions o)
        {
            var rows = new List<AuditIssue>();
            foreach (var e in idx.edges)
            {
                if (!MatchTarget(e, t, o)) continue;
                rows.Add(Row(Sev.Info, "Referrer",
                    "GameObject: " + e.srcObject,
                    e.srcContainer, e.srcObject,
                    "Component: " + e.srcComponent + "." + e.field + (string.IsNullOrEmpty(e.tgtComponent) ? "" : "   ->   " + e.tgtComponent),
                    "Referensi serialized"));
            }

            if (t.kind == "asset")
                foreach (var g in idx.gos)
                    if (g.prefabSource == t.container)
                        rows.Add(Row(Sev.Info, "Prefab Instance", "Prefab instance: " + g.path, g.container, g.path,
                            "Instance dari " + t.container, "Prefab instance"));

            return rows;
        }

        // Referensi aset lewat teks GUID (seperti "Find References In Project") - untuk target berupa aset.
        public static List<AuditIssue> FindAssetUsers(TargetId t, ObjOptions o, int cap)
        {
            var rows = new List<AuditIssue>();
            if (t.kind != "asset") return rows;
            string guid = AssetDatabase.AssetPathToGUID(t.container);
            if (string.IsNullOrEmpty(guid)) return rows;

            var yamlExt = new[] { ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim", ".overrideController" };
            foreach (var p in AssetDatabase.GetAllAssetPaths())
            {
                if (rows.Count >= cap) break;
                if (!p.StartsWith("Assets/", StringComparison.Ordinal) || p == t.container) continue;
                if (!yamlExt.Any(x => p.EndsWith(x, StringComparison.OrdinalIgnoreCase))) continue;
                if (ObjIndexBuilder.Excluded(p, o.excludeFolders)) continue;
                try
                {
                    if (File.ReadAllText(p).Contains(guid))
                        rows.Add(Row(Sev.Info, "Asset Reference", "Aset: " + p, p, null, "Memuat GUID " + guid.Substring(0, 8) + "... milik " + t.container, "Dirujuk oleh aset (teks GUID)"));
                }
                catch { }
            }
            return rows;
        }

        public static List<AuditIssue> Impact(ObjIndex idx, TargetId t)
        {
            var rows = new List<AuditIssue>();
            string name = t.name ?? "";
            foreach (var c in idx.calls)
            {
                if (c.kind != "GameObject.Find" && c.kind != "Transform.Find") continue;
                string leaf = c.literal.Contains("/") ? c.literal.Substring(c.literal.LastIndexOf('/') + 1) : c.literal;
                if (leaf != name) continue;
                rows.Add(Row(Sev.Warning, "String Call",
                    c.kind + "(\"" + c.literal + "\") memakai nama '" + name + "'. Rename/hapus objek ini akan membuatnya null.",
                    c.file, null, null, "Pemanggilan string yang bergantung pada nama ini", c.line));
            }
            return rows;
        }

        public static List<AuditIssue> ScriptUsage(ObjIndex idx, string scriptPath)
        {
            var rows = new List<AuditIssue>();
            foreach (var c in idx.comps)
            {
                if (c.scriptPath != scriptPath) continue;
                rows.Add(Row(c.nulls > 0 ? Sev.Warning : Sev.Info, "Script Usage",
                    "GameObject: " + c.objectPath + (c.inactive ? "   [inactive]" : ""),
                    c.container, c.objectPath,
                    c.refs + " object-reference, " + c.nulls + " kosong", c.container));
            }
            return rows;
        }

        // ---- live: apa yang direferensikan oleh sebuah GameObject
        public static List<AuditIssue> Outgoing(GameObject go, bool includeChildren, ObjOptions o)
        {
            var rows = new List<AuditIssue>();
            string container = EditorUtility.IsPersistent(go) ? AuditUtil.Norm(AssetDatabase.GetAssetPath(go)) : TargetId.ScenePathOf(go);
            var trs = includeChildren ? go.GetComponentsInChildren<Transform>(true) : new[] { go.transform };

            foreach (var tr in trs)
            {
                string owner = AuditUtil.HierarchyPath(tr);
                bool ownerEditorOnly = AuditUtil.IsEditorOnlyTagged(tr.gameObject);
                foreach (var c in tr.GetComponents<Component>())
                {
                    if (c == null)
                    {
                        rows.Add(Row(Sev.Error, "Missing Script", "Component Missing Script pada '" + tr.name + "'", container, owner, null, owner));
                        continue;
                    }
                    if (c is Transform) continue;

                    var mb = c as MonoBehaviour;
                    bool user = false;
                    if (mb != null)
                    {
                        var ms = MonoScript.FromMonoBehaviour(mb);
                        string sp = ms != null ? AuditUtil.Norm(AssetDatabase.GetAssetPath(ms)) : "";
                        user = sp.StartsWith("Assets/", StringComparison.Ordinal);
                    }
                    string typeName = c.GetType().Name;
                    string group = owner + "  :  " + typeName;

                    using (var so = new SerializedObject(c))
                    {
                        var p = so.GetIterator();
                        bool enter = true;
                        while (p.NextVisible(enter))
                        {
                            enter = !(p.propertyType == SerializedPropertyType.String || p.propertyType == SerializedPropertyType.AnimationCurve ||
                                      p.propertyType == SerializedPropertyType.Gradient || (p.isArray && p.arraySize > 1000));
                            if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                            string path = p.propertyPath;
                            if (path == "m_Script" || path == "m_GameObject" || path.StartsWith("m_CorrespondingSourceObject") ||
                                path.StartsWith("m_PrefabInstance") || path.StartsWith("m_PrefabAsset")) continue;
                            string field = typeName + "." + AuditUtil.DisplayPath(path);

                            var val = p.objectReferenceValue;
                            if (val == null)
                            {
                                if (user && !path.Contains("m_PersistentCalls"))
                                    rows.Add(Row(Sev.Warning, "Outgoing", field + "   ->   KOSONG (None/Missing). Tipe: " + AuditUtil.PPtrType(p.type), container, owner, null, group));
                                continue;
                            }

                            string kind, tc, to, tcomp;
                            if (!ObjIndexBuilder.Resolve(val, out kind, out tc, out to, out tcomp))
                            {
                                rows.Add(Row(Sev.Info, "Outgoing", field + "   ->   " + val.name + "  (" + val.GetType().Name + ")", container, owner, null, group));
                                continue;
                            }

                            string tgt = kind == "scene" ? "[scene] " + tc + " : " + to : "[asset] " + tc + (string.IsNullOrEmpty(to) ? "" : " : " + to);
                            if (!string.IsNullOrEmpty(tcomp)) tgt += "  (" + tcomp + ")";

                            Sev sev = Sev.Info;
                            string note = null;
                            if (kind == "scene" && tc != container && !EditorUtility.IsPersistent(go))
                            { sev = Sev.Warning; note = "Referensi LINTAS SCENE: tidak ikut tersimpan, akan null saat scene dimuat sendiri."; }
                            else
                            {
                                var tgo = val as GameObject;
                                if (tgo == null) { var tcmp = val as Component; if (tcmp != null) tgo = tcmp.gameObject; }
                                if (tgo != null && !ownerEditorOnly && AuditUtil.IsEditorOnlyTagged(tgo))
                                { sev = Sev.Error; note = "Target bertag EditorOnly: dibuang saat build, jadi null di APK."; }
                            }
                            rows.Add(Row(sev, "Outgoing", field + "   ->   " + tgt, container, owner, note, group));
                        }
                    }
                }
            }
            return rows;
        }

        // ---- Names & Tags
        public static List<AuditIssue> NamesAndTags(ObjIndex idx)
        {
            var rows = new List<AuditIssue>();
            var definedTags = new HashSet<string>(InternalEditorUtility.tags);
            const string runtimeNote = "Objek yang dibuat saat runtime tidak ikut terhitung.";

            foreach (var c in idx.calls)
            {
                string where = c.file;
                if (c.kind == "GameObject.Find")
                {
                    bool isPath = c.literal.Contains("/");
                    var all = idx.gos.Where(g => isPath ? g.path == c.literal : g.name == c.literal).ToList();
                    int active = all.Count(g => !g.inactive);
                    if (all.Count == 0)
                        rows.Add(Row(Sev.Error, "Name Missing", "GameObject.Find(\"" + c.literal + "\"): tidak ada objek dengan nama/path ini di scope index. " + runtimeNote, where, null, null, "GameObject.Find tanpa objek", c.line));
                    else if (active == 0)
                        rows.Add(Row(Sev.Warning, "Name Inactive", "GameObject.Find(\"" + c.literal + "\"): hanya ada pada objek inactive. Find mengabaikan objek inactive -> null.", where, null, null, "GameObject.Find ke objek inactive", c.line));
                    else if (active > 1)
                        rows.Add(Row(Sev.Warning, "Name Ambiguous", "GameObject.Find(\"" + c.literal + "\"): " + active + " objek aktif bernama sama. Yang dikembalikan tidak terjamin.", where, null, null, "Nama ambigu", c.line));
                }
                else if (c.kind == "Transform.Find")
                {
                    string leaf = c.literal.Contains("/") ? c.literal.Substring(c.literal.LastIndexOf('/') + 1) : c.literal;
                    if (!idx.gos.Any(g => g.name == leaf))
                        rows.Add(Row(Sev.Error, "Name Missing", "transform.Find(\"" + c.literal + "\"): tidak ada objek bernama '" + leaf + "' di scope index. " + runtimeNote, where, null, null, "transform.Find tanpa objek", c.line));
                }
                else if (c.kind == "Tag")
                {
                    if (!definedTags.Contains(c.literal))
                        rows.Add(Row(Sev.Error, "Tag Undefined", "Tag \"" + c.literal + "\" tidak terdaftar di Tags & Layers.", where, null, null, "Tag tidak terdaftar", c.line));
                    else if (!idx.gos.Any(g => g.tag == c.literal))
                        rows.Add(Row(Sev.Info, "Tag Unused", "Tag \"" + c.literal + "\" terdaftar tapi tidak dipakai objek apa pun di scope index. " + runtimeNote, where, null, null, "Tag tanpa objek", c.line));
                }
                else if (c.kind == "Layer")
                {
                    if (LayerMask.NameToLayer(c.literal) < 0)
                        rows.Add(Row(Sev.Error, "Layer Undefined", "Layer \"" + c.literal + "\" tidak terdaftar (NameToLayer = -1).", where, null, null, "Layer tidak terdaftar", c.line));
                }
            }

            // nama sibling kembar (membuat transform.Find / path ambigu)
            foreach (var grp in idx.gos.GroupBy(g => g.container + "::" + g.path).Where(g => g.Count() > 1))
            {
                var first = grp.First();
                rows.Add(Row(Sev.Info, "Duplicate Sibling", "Ada " + grp.Count() + " objek dengan path yang sama: " + first.path, first.container, first.path, null, "Nama sibling kembar"));
            }

            return rows
                .OrderByDescending(r => (int)r.severity)
                .ThenBy(r => r.category)
                .ThenBy(r => r.assetPath ?? "")
                .ThenBy(r => r.line)
                .ToList();
        }
    }

    // ------------------------------------------------------------------ BOOTSTRAP
    [InitializeOnLoad]
    static class ObjectsAuditorBootstrap
    {
        static ObjectsAuditorBootstrap() { UnionProjectAuditorWindow.ObjectsAuditDraw = ObjectsAuditorPanel.Draw; }
    }

    // ------------------------------------------------------------------ PANEL (UI)
    static class ObjectsAuditorPanel
    {
        sealed class ListState
        {
            public Vector2 scroll;
            public string search = "";
            public bool showErr = true, showWarn = true, showInfo = true;
        }

        static readonly string[] SubTabs = { "Find Referrers", "Outgoing", "Script Usage", "Names & Tags", "Pins", "Settings" };
        static ObjOptions opt;
        static ObjIndex index;
        static bool indexLoaded;
        static int sub;
        static string status = "";

        static TargetId findTarget;
        static Object findObject;
        static List<AuditIssue> findRows = new List<AuditIssue>();
        static List<AuditIssue> impactRows = new List<AuditIssue>();
        static List<AuditIssue> assetUserRows = new List<AuditIssue>();

        static GameObject outObject;
        static bool outChildren;
        static List<AuditIssue> outRows = new List<AuditIssue>();

        static MonoScript usageScript;
        static List<AuditIssue> usageRows = new List<AuditIssue>();

        static List<AuditIssue> nameRows = new List<AuditIssue>();
        static bool showIgnored;

        static readonly Dictionary<string, ListState> listStates = new Dictionary<string, ListState>();
        static readonly Dictionary<string, bool> groupOpen = new Dictionary<string, bool>();
        static readonly HashSet<string> selected = new HashSet<string>();
        static GUIStyle wrap, small;
        static readonly Color SelColor = new Color(0.17f, 0.36f, 0.53f, 0.65f);

        static void Init()
        {
            if (opt == null) opt = ObjOptions.Load();
            if (!indexLoaded) { index = ObjIndexStore.Load(); indexLoaded = true; }
            if (wrap == null)
            {
                wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
                small = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            }
        }

        static ListState State(string id)
        {
            ListState s;
            if (!listStates.TryGetValue(id, out s)) { s = new ListState(); listStates[id] = s; }
            return s;
        }

        // -------------------------------------------------------------- entry
        public static void Draw(EditorWindow host)
        {
            Init();
            HandleKeys(host);
            DrawIndexBar(host);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                sub = GUILayout.Toolbar(sub, SubTabs, GUI.skin.button, GUI.ToolbarButtonSize.FitToContents);
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(4);

            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);

            float listHeight = Mathf.Clamp(host.position.height * 0.45f, 160f, 600f);
            switch (sub)
            {
                case 0: DrawFind(host, listHeight); break;
                case 1: DrawOutgoing(host, listHeight); break;
                case 2: DrawUsage(host, listHeight); break;
                case 3: DrawNames(host, listHeight); break;
                case 4: DrawPins(host); break;
                default: DrawSettings(); break;
            }
        }

        static bool NeedIndex()
        {
            if (index != null) return false;
            EditorGUILayout.HelpBox("Index belum ada. Klik 'Build Index' di atas dulu (scope: " + opt.scope + (opt.includePrefabs ? " + semua prefab" : "") + ").", MessageType.Warning);
            return true;
        }

        // -------------------------------------------------------------- index bar
        static void DrawIndexBar(EditorWindow host)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string info;
                    if (index == null) info = "Index: belum dibuat";
                    else
                    {
                        info = "Index: " + index.builtAt + "  |  " + index.scopeInfo + "  |  " + index.edges.Count + " referensi, " +
                               index.gos.Count + " GameObject, " + index.comps.Count + " script-component";
                        if (ObjIndexStore.IsStale(index)) info += "   [USANG: ada scene/prefab yang berubah sejak index dibuat]";
                    }
                    EditorGUILayout.LabelField(info, small);
                    if (GUILayout.Button(index == null ? "Build Index" : "Rebuild", GUILayout.Width(90)))
                        EditorApplication.delayCall += () => BuildIndex(host);
                }
            }
        }

        static void BuildIndex(EditorWindow host)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Objects Auditor", "Hentikan Play Mode dulu.", "OK");
                return;
            }
            opt.Save();
            try
            {
                var idx = ObjIndexBuilder.Build(opt);
                if (idx == null) { status = "Build index dibatalkan."; }
                else
                {
                    ObjIndexStore.Save(idx);
                    index = idx;
                    status = "Index selesai: " + idx.edges.Count + " referensi, " + idx.gos.Count + " GameObject.";
                }
            }
            catch (Exception e) { Debug.LogException(e); status = "Build index gagal: " + e.Message; }
            finally { EditorUtility.ClearProgressBar(); }
            host.Repaint();
        }

        // -------------------------------------------------------------- Find Referrers
        static void DrawFind(EditorWindow host, float h)
        {
            if (NeedIndex()) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                var newObj = EditorGUILayout.ObjectField("Target", findObject, typeof(Object), true);
                if (newObj != findObject) { findObject = newObj; }
                if (GUILayout.Button("Pakai Selection", GUILayout.Width(110))) findObject = Selection.activeObject;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                bool mc = EditorGUILayout.ToggleLeft("Referensi ke komponen = referensi ke GameObject", opt.matchComponents, GUILayout.Width(300));
                bool ic = EditorGUILayout.ToggleLeft("Termasuk child (prefab)", opt.includeChildren, GUILayout.Width(170));
                bool ec = EditorGUILayout.ToggleLeft("Hanya tipe komponen ini", opt.exactComponent);
                if (mc != opt.matchComponents || ic != opt.includeChildren || ec != opt.exactComponent)
                { opt.matchComponents = mc; opt.includeChildren = ic; opt.exactComponent = ec; opt.Save(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(findObject == null))
                    if (GUILayout.Button("Cari Referrers", GUILayout.Height(24))) RunFind(TargetId.From(findObject));
                using (new EditorGUI.DisabledScope(findObject == null))
                    if (GUILayout.Button("Pin target ini", GUILayout.Width(110), GUILayout.Height(24))) PinTarget(TargetId.From(findObject));
            }

            if (findTarget == null) { EditorGUILayout.LabelField("Pilih objek (scene atau asset), lalu klik Cari Referrers.", small); return; }

            EditorGUILayout.LabelField("Target: " + findTarget.Label(), wrap);
            EditorGUILayout.LabelField(findRows.Count + " referensi serialized/instance  |  " + assetUserRows.Count + " aset memuat GUID-nya  |  " +
                                       impactRows.Count + " pemanggilan string bergantung pada namanya", small);
            if (findTarget.kind == "scene")
                EditorGUILayout.LabelField("Catatan: index dibuat dari scene yang tersimpan di disk. Objek yang belum disimpan/di-rebuild bisa belum tercatat.", small);

            var all = new List<AuditIssue>();
            all.AddRange(findRows);
            all.AddRange(assetUserRows);
            all.AddRange(impactRows);
            DrawRows(host, all, "find", false, h);
        }

        static void RunFind(TargetId t)
        {
            if (t == null) { status = "Objek ini tidak bisa dicari (bukan GameObject/komponen/aset)."; return; }
            findTarget = t;
            findRows = ObjQueries.FindReferrers(index, t, opt);
            assetUserRows = ObjQueries.FindAssetUsers(t, opt, opt.maxRows);
            impactRows = ObjQueries.Impact(index, t);
            status = "";
        }

        // -------------------------------------------------------------- Outgoing
        static void DrawOutgoing(EditorWindow host, float h)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                outObject = (GameObject)EditorGUILayout.ObjectField("GameObject", outObject, typeof(GameObject), true);
                if (GUILayout.Button("Pakai Selection", GUILayout.Width(110))) outObject = Selection.activeGameObject;
            }
            outChildren = EditorGUILayout.ToggleLeft("Termasuk semua child", outChildren);
            using (new EditorGUI.DisabledScope(outObject == null))
                if (GUILayout.Button("Scan Outgoing References", GUILayout.Height(24)))
                    outRows = ObjQueries.Outgoing(outObject, outChildren, opt);

            EditorGUILayout.LabelField("Live: membaca objek sekarang (tidak memakai index). Referensi kosong hanya dilaporkan untuk script buatan sendiri.", small);
            DrawRows(host, outRows, "outgoing", false, h);
        }

        // -------------------------------------------------------------- Script Usage
        static void DrawUsage(EditorWindow host, float h)
        {
            if (NeedIndex()) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                usageScript = (MonoScript)EditorGUILayout.ObjectField("Script", usageScript, typeof(MonoScript), false);
                if (GUILayout.Button("Pakai Selection", GUILayout.Width(110)) && Selection.activeObject is MonoScript) usageScript = (MonoScript)Selection.activeObject;
            }
            using (new EditorGUI.DisabledScope(usageScript == null))
                if (GUILayout.Button("Cari Pemakaian Script", GUILayout.Height(24)))
                    usageRows = ObjQueries.ScriptUsage(index, AuditUtil.Norm(AssetDatabase.GetAssetPath(usageScript)));
            DrawRows(host, usageRows, "usage", false, h);
        }

        // -------------------------------------------------------------- Names & Tags
        static void DrawNames(EditorWindow host, float h)
        {
            if (NeedIndex()) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Jalankan Cek Names & Tags", GUILayout.Height(24))) nameRows = ObjQueries.NamesAndTags(index);
                showIgnored = EditorGUILayout.ToggleLeft("Tampilkan yang di-ignore (" + opt.ignored.Count + ")", showIgnored, GUILayout.Width(210));
            }
            EditorGUILayout.LabelField("Mencocokkan GameObject.Find / transform.Find / Tag / Layer berbasis string dengan objek di scope index.", small);
            var shown = nameRows.Where(r => showIgnored || !opt.ignored.Contains(r.Key())).ToList();
            DrawRows(host, shown, "names", true, h);
        }

        // -------------------------------------------------------------- Pins
        static void PinTarget(TargetId t)
        {
            if (t == null) return;
            if (opt.pins.Any(p => p.kind == t.kind && p.container == t.container && p.objPath == t.objPath)) { status = "Sudah ada di Pins."; return; }
            int n = index != null ? ObjQueries.CountReferrers(index, t, opt) : 0;
            opt.pins.Add(new PinEntry { label = t.name, kind = t.kind, container = t.container, objPath = t.objPath, lastCount = n });
            opt.Save();
            status = "Di-pin: " + t.name + " (" + n + " referensi)";
        }

        static void DrawPins(EditorWindow host)
        {
            EditorGUILayout.LabelField("Watchlist objek penting. Selisih dibandingkan dengan baseline terakhir.", small);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pin Selection", GUILayout.Width(110))) PinTarget(TargetId.From(Selection.activeObject));
            }
            if (opt.pins.Count == 0) { EditorGUILayout.LabelField("Belum ada pin.", small); return; }

            PinEntry removeMe = null;
            foreach (var p in opt.pins)
            {
                var t = new TargetId { kind = p.kind, container = p.container, objPath = p.objPath, name = p.label, compType = "" };
                int now = index != null ? ObjQueries.CountReferrers(index, t, opt) : -1;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(p.label, EditorStyles.boldLabel);
                        if (GUILayout.Button("Cari", GUILayout.Width(50)) && index != null)
                        { sub = 0; findObject = null; RunFind(t); }
                        if (GUILayout.Button("Baseline", GUILayout.Width(70)) && now >= 0) { p.lastCount = now; opt.Save(); }
                        if (GUILayout.Button("Unpin", GUILayout.Width(55))) removeMe = p;
                    }
                    string delta = now < 0 ? "index belum ada" : (now == p.lastCount ? "tidak berubah" : (now > p.lastCount ? "+" : "") + (now - p.lastCount) + " dari baseline");
                    EditorGUILayout.LabelField("Referensi sekarang: " + (now < 0 ? "-" : now.ToString()) + "  (baseline " + p.lastCount + ")  -  " + delta, small);
                    EditorGUILayout.LabelField(p.container + (string.IsNullOrEmpty(p.objPath) ? "" : " : " + p.objPath), small);
                }
            }
            if (removeMe != null) { opt.pins.Remove(removeMe); opt.Save(); }
        }

        // -------------------------------------------------------------- Settings
        static void DrawSettings()
        {
            float lw = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 170;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                opt.scope = (ObjScope)EditorGUILayout.EnumPopup("Scope scene", opt.scope);
                opt.includePrefabs = EditorGUILayout.ToggleLeft("Sertakan semua prefab di Assets", opt.includePrefabs);
                opt.builtInToAssets = EditorGUILayout.ToggleLeft("Indeks referensi komponen bawaan -> aset (lebih besar)", opt.builtInToAssets);
                opt.excludeFolders = EditorGUILayout.TextField("Exclude folders", opt.excludeFolders);
                using (new EditorGUILayout.HorizontalScope())
                {
                    opt.scriptsRoot = EditorGUILayout.TextField("Scripts folder (Names)", opt.scriptsRoot);
                    if (GUILayout.Button("Auto", GUILayout.Width(45))) opt.scriptsRoot = "";
                }
                opt.maxRows = EditorGUILayout.IntSlider("Maks baris aset", opt.maxRows, 50, 2000);
                if (EditorGUI.EndChangeCheck()) opt.Save();
            }
            EditorGUILayout.LabelField("Scope 'Build Scenes' / 'All Project Scenes' membuka scene satu per satu saat Build Index (ada prompt simpan dulu), lalu mengembalikan scene setup.", small);
            EditorGUILayout.LabelField("Kosongkan 'Scripts folder' untuk auto-detect (Assets/Scripts, Assets/Script, ...).", small);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Hapus Index")) { ObjIndexStore.Delete(); index = null; status = "Index dihapus."; }
                if (GUILayout.Button("Kosongkan Ignore (" + opt.ignored.Count + ")")) { opt.ignored.Clear(); opt.Save(); }
            }
            EditorGUIUtility.labelWidth = lw;
        }

        // -------------------------------------------------------------- shared rows
        static string Format(AuditIssue i)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[" + i.severity + "] [" + i.category + "] " + i.message);
            if (!string.IsNullOrEmpty(i.assetPath)) sb.AppendLine("    at " + i.assetPath + (i.line > 0 ? ":" + i.line : ""));
            if (!string.IsNullOrEmpty(i.objectPath)) sb.AppendLine("    obj " + i.objectPath);
            if (!string.IsNullOrEmpty(i.detail)) sb.AppendLine("    > " + i.detail);
            return sb.ToString().TrimEnd();
        }

        static void HandleKeys(EditorWindow host)
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.C && (e.control || e.command) &&
                selected.Count > 0 && !EditorGUIUtility.editingTextField)
            {
                var all = new List<AuditIssue>();
                all.AddRange(findRows); all.AddRange(assetUserRows); all.AddRange(impactRows);
                all.AddRange(outRows); all.AddRange(usageRows); all.AddRange(nameRows);
                var sb = new StringBuilder();
                int n = 0;
                foreach (var i in all)
                    if (selected.Contains(Id(i))) { sb.AppendLine(Format(i)); n++; }
                if (n > 0)
                {
                    EditorGUIUtility.systemCopyBuffer = sb.ToString().TrimEnd();
                    host.ShowNotification(new GUIContent("Tersalin: " + n + " item"), 1.2);
                }
                e.Use();
            }
        }

        static string Id(AuditIssue i) { return i.Key() + "#" + i.line; }

        static void DrawRows(EditorWindow host, List<AuditIssue> rows, string listId, bool allowIgnore, float height)
        {
            var st = State(listId);
            int ec = rows.Count(i => i.severity == Sev.Error);
            int wc = rows.Count(i => i.severity == Sev.Warning);
            int ic = rows.Count(i => i.severity == Sev.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                st.showErr = GUILayout.Toggle(st.showErr, "Error (" + ec + ")", "Button", GUILayout.Width(95));
                st.showWarn = GUILayout.Toggle(st.showWarn, "Warning (" + wc + ")", "Button", GUILayout.Width(105));
                st.showInfo = GUILayout.Toggle(st.showInfo, "Info (" + ic + ")", "Button", GUILayout.Width(85));
                st.search = EditorGUILayout.TextField(st.search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("Copy", GUILayout.Width(50)))
                {
                    EditorGUIUtility.systemCopyBuffer = AuditRunner.BuildReport(rows);
                    host.ShowNotification(new GUIContent("Tersalin: " + rows.Count + " item"), 1.2);
                }
                if (GUILayout.Button("Export", GUILayout.Width(55))) Export(rows);
            }

            var shown = rows.Where(i =>
            {
                if (i.severity == Sev.Error && !st.showErr) return false;
                if (i.severity == Sev.Warning && !st.showWarn) return false;
                if (i.severity == Sev.Info && !st.showInfo) return false;
                if (string.IsNullOrEmpty(st.search)) return true;
                string s = st.search.ToLowerInvariant();
                return (i.message ?? "").ToLowerInvariant().Contains(s) || (i.assetPath ?? "").ToLowerInvariant().Contains(s) ||
                       (i.objectPath ?? "").ToLowerInvariant().Contains(s) || (i.detail ?? "").ToLowerInvariant().Contains(s) ||
                       (i.category ?? "").ToLowerInvariant().Contains(s);
            }).Take(opt.maxRows).ToList();

            st.scroll = EditorGUILayout.BeginScrollView(st.scroll, GUILayout.Height(height));
            foreach (var r in shown.Where(i => string.IsNullOrEmpty(i.group))) DrawRow(host, r, allowIgnore);
            foreach (var g in shown.Where(i => !string.IsNullOrEmpty(i.group)).GroupBy(i => i.group).ToList())
            {
                string gk = listId + "|" + g.Key;
                bool open;
                groupOpen.TryGetValue(gk, out open);
                open = EditorGUILayout.Foldout(open, g.Key + "  (" + g.Count() + ")", true);
                groupOpen[gk] = open;
                if (!open) continue;
                EditorGUI.indentLevel++;
                foreach (var r in g) DrawRow(host, r, allowIgnore);
                EditorGUI.indentLevel--;
            }
            if (shown.Count == 0) EditorGUILayout.LabelField(rows.Count == 0 ? "Belum ada hasil." : "Tidak ada hasil untuk filter ini.", small);
            EditorGUILayout.EndScrollView();
        }

        static GUIContent CopyIcon()
        {
            var c = EditorGUIUtility.IconContent("TreeEditor.Duplicate");
            if (c != null && c.image != null) return new GUIContent(c.image, "Salin item ini");
            return new GUIContent("Cp", "Salin item ini");
        }

        static void DrawRow(EditorWindow host, AuditIssue it, bool allowIgnore)
        {
            string id = Id(it);
            bool isSel = selected.Contains(id);
            Rect rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (Event.current.type == EventType.Repaint && isSel) EditorGUI.DrawRect(rect, SelColor);

            using (new EditorGUILayout.HorizontalScope())
            {
                string icon = it.severity == Sev.Error ? "console.erroricon.sml" : it.severity == Sev.Warning ? "console.warnicon.sml" : "console.infoicon.sml";
                GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(20), GUILayout.Height(18));
                GUILayout.Label("[" + it.category + "]", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
                if (allowIgnore)
                {
                    bool ig = opt.ignored.Contains(it.Key());
                    bool on = GUILayout.Toggle(ig, new GUIContent("Ignore", "Sembunyikan item ini dari daftar"), GUILayout.Width(62));
                    if (on != ig)
                    {
                        string k = it.Key();
                        EditorApplication.delayCall += () => { if (on) opt.ignored.Add(k); else opt.ignored.Remove(k); opt.Save(); host.Repaint(); };
                    }
                }
                if (!string.IsNullOrEmpty(it.assetPath) && GUILayout.Button("Go", GUILayout.Width(40))) Navigate(it);
            }
            EditorGUILayout.LabelField(it.message, wrap);
            if (!string.IsNullOrEmpty(it.detail)) EditorGUILayout.LabelField(it.detail, small);

            using (new EditorGUILayout.HorizontalScope())
            {
                string loc = it.assetPath ?? "";
                if (it.line > 0) loc += ":" + it.line;
                EditorGUILayout.LabelField(loc, small);
                if (GUILayout.Button(CopyIcon(), EditorStyles.miniButton, GUILayout.Width(26), GUILayout.Height(18)))
                {
                    EditorGUIUtility.systemCopyBuffer = Format(it);
                    host.ShowNotification(new GUIContent("Tersalin: 1 item"), 1.2);
                }
            }
            EditorGUILayout.EndVertical();

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                if (e.control || e.command) { if (!selected.Add(id)) selected.Remove(id); }
                else { selected.Clear(); selected.Add(id); }
                GUI.FocusControl(null);
                e.Use();
                host.Repaint();
            }
        }

        static void Export(List<AuditIssue> rows)
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "AuditReports");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "ObjectsAudit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(file, AuditRunner.BuildReport(rows), Encoding.UTF8);
            EditorUtility.RevealInFinder(file);
        }

        static void Navigate(AuditIssue it)
        {
            string path = it.assetPath;
            if (string.IsNullOrEmpty(path) || path == "(untitled)") return;
            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj == null) return;

            if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) { AssetDatabase.OpenAsset(obj, Math.Max(it.line, 1)); return; }

            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                }
                var go = AuditUtil.FindInRoots(scene.GetRootGameObjects(), it.objectPath);
                if (go != null) { Selection.activeGameObject = go; EditorGUIUtility.PingObject(go); }
                else { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
                return;
            }

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }
    }
}
