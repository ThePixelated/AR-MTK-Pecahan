// ============================================================================
//  ScriptAuditor.cs  -  Union Project Auditor / Script Auditor
//  Letakkan di folder Editor, mis. Assets/UnionInteractive/ProjectAuditor/Editor/
//  (bersama ObjectsAuditor.cs dan asmdef). Menu: Tools > Union Project Auditor > Open Window
//
//  Script Auditor:
//   1. Bedah component script buatan sendiri di scene / prefab / asset.
//   2. Evaluasi field serialized -> referensi kosong (potensi NullReferenceException).
//   3. Cari penyebab error build-only, mis. "different serialization layout when loading":
//      script tidak ikut player, #if UNITY_EDITOR pada field/class, Missing Script, GUID/.meta rusak.
//   4. Scan source *.cs, Build Settings, Resources, YAML (GUID putus), file .meta.
//   5. Fix aman (konfirmasi + backup otomatis ke Bin, bisa di-restore).
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnionInteractive.Toolkit.ProjectAuditor
{
    // ------------------------------------------------------------------ MODEL
    public enum Sev { Info = 0, Warning = 1, Error = 2 }
    public enum SceneScope { None = 0, OpenScenes = 1, BuildScenes = 2 }

    [Serializable]
    public class AuditCandidate
    {
        public string label;
        public string scriptPath;
    }

    [Serializable]
    public class AuditIssue
    {
        public Sev severity;
        public string category;
        public string message;
        public string detail;
        public string assetPath;   // script / scene / prefab
        public int line;           // untuk script
        public string objectPath;  // hierarchy path di scene / prefab

        // Fix otomatis (kosong = tidak ada fix)
        public string fix;
        public string fixArg1;
        public string fixArg2;

        // Pengelompokan (expandable) & kandidat script untuk re-link
        public string group;
        public string info;   // keterangan tambahan (tidak ikut identitas Key)
        public List<AuditCandidate> candidates = new List<AuditCandidate>();

        public bool CanFix { get { return !string.IsNullOrEmpty(fix); } }

        // Identitas stabil (tanpa nomor baris) untuk fitur Exclude/Ignore.
        public string Key()
        {
            return string.Join("|", new[] { category ?? "", assetPath ?? "", objectPath ?? "", message ?? "", detail ?? "" });
        }
    }

    [Serializable]
    public class AuditOptions
    {
        public string scriptsRoot = "Assets/Scripts";
        public string excludeFolders = "Assets/TextMesh Pro;Assets/Vuforia;Assets/Plugins;Assets/Samples";
        public string ignoreFieldPatterns = "";   // contoh: debug*, _optional*, Player.backup
        public bool sourceScan = true;
        public bool playerAvailability = true;
        public bool buildSettings = true;
        public bool resourcesCheck = true;
        public bool assetScan = true;
        public bool metaCheck = true;
        public string logoGuid = "";           // logo header (Texture2D) - dipilih di Opsi
        public string originalProject = "";   // salinan project lama (opsional) untuk bandingkan GUID
        public SceneScope sceneScope = SceneScope.BuildScenes;
        public bool scanPrefabs = true;
        public bool scanUnityEventsAll = true;
        public bool includePackages = false;
        public bool inventory = false;
        public bool preBuildCheck = true;
        public bool preBuildAbort = false;

        const string Key = "ScriptAudit.Options.v1";

        public static AuditOptions Load()
        {
            string j = EditorPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(j)) return new AuditOptions();
            try { return JsonUtility.FromJson<AuditOptions>(j) ?? new AuditOptions(); }
            catch { return new AuditOptions(); }
        }

        public void Save() { EditorPrefs.SetString(Key, JsonUtility.ToJson(this)); }
        public AuditOptions Clone() { return JsonUtility.FromJson<AuditOptions>(JsonUtility.ToJson(this)); }
    }

    // ------------------------------------------------------------------ UTIL
    static class AuditUtil
    {
        public static string Norm(string p) { return (p ?? "").Replace('\\', '/'); }

        public static bool IsExcluded(string path, AuditOptions o)
        {
            if (string.IsNullOrWhiteSpace(o.excludeFolders)) return false;
            foreach (var part in o.excludeFolders.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string p = Norm(part.Trim()).TrimEnd('/');
                if (p.Length == 0) continue;
                if (path.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static string DetectScriptsRoot()
        {
            string[] cands = { "Assets/Scripts", "Assets/Script", "Assets/_Scripts", "Assets/_Script", "Assets/Source" };
            foreach (var c in cands) if (AssetDatabase.IsValidFolder(c)) return c;
            return "Assets";
        }

        public static string HierarchyPath(Transform t)
        {
            var list = new List<string>();
            for (; t != null; t = t.parent) list.Add(t.name);
            list.Reverse();
            return string.Join("/", list);
        }

        public static string PPtrType(string t)
        {
            if (string.IsNullOrEmpty(t)) return "?";
            int a = t.IndexOf('$');
            int b = t.LastIndexOf('>');
            return (a >= 0 && b > a) ? t.Substring(a + 1, b - a - 1) : t;
        }

        public static string DisplayPath(string p) { return p.Replace(".Array.data[", "["); }

        public static GameObject FindInRoots(IEnumerable<GameObject> roots, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string[] parts = path.Split('/');
            foreach (var r in roots)
            {
                if (r.name != parts[0]) continue;
                var t = r.transform;
                for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        public static bool IsEditorOnlyTagged(GameObject g)
        {
            for (var t = g.transform; t != null; t = t.parent)
                if (t.CompareTag("EditorOnly")) return true;
            return false;
        }
    }

    // ---------------------------------------------------------- SOURCE SCANNER
    public sealed class SrcRef { public string value; public string file; public int line; }

    public sealed class SourceResult
    {
        public List<SrcRef> sceneNames = new List<SrcRef>();
        public List<SrcRef> resourcePaths = new List<SrcRef>();
        public int files;
    }

    static class SourceScanner
    {
        enum Region { None = 0, Other = 1, PlayerOnly = 2, EditorOnly = 3 }

        sealed class SrcRule
        {
            public Regex rx; public Sev sev; public string cat; public string msg;
            public SrcRule(string pattern, Sev sev, string cat, string msg)
            {
                rx = new Regex(pattern, RegexOptions.Compiled);
                this.sev = sev; this.cat = cat; this.msg = msg;
            }
        }

        static readonly Regex IfEditorRx = new Regex(@"^\s*(!)?\s*UNITY_EDITOR\w*\s*$", RegexOptions.Compiled);
        static readonly Regex ClassRx = new Regex(@"\bclass\s+(\w+)\s*(?:<[^>]*>)?\s*(?::\s*([^{\r\n]+))?", RegexOptions.Compiled);
        static readonly Regex SerAttrRx = new Regex(@"\[[^\]]*\b(?:SerializeField|SerializeReference)\b[^\]]*\]", RegexOptions.Compiled);
        static readonly Regex PublicFieldRx = new Regex(
            @"^\s*(?:\[[^\]]*\]\s*)*public\s+(?!static\b|const\b|readonly\b|event\b|abstract\b|override\b|virtual\b|class\b|struct\b|enum\b|delegate\b|interface\b)[\w<>\[\],\.\? ]+?\s+\w+\s*(?:=[^;]+)?;\s*$",
            RegexOptions.Compiled);
        static readonly Regex EditorApiRx = new Regex(
            @"^\s*using\s+UnityEditor\b|\bUnityEditor\s*\.|\bAssetDatabase\s*\.|\bEditorUtility\s*\.|\bEditorApplication\s*\.",
            RegexOptions.Compiled);
        static readonly Regex LoadSceneRx = new Regex(@"\bLoadScene(?:Async)?\s*\(\s*""([^""]+)""", RegexOptions.Compiled);
        static readonly Regex ResLoadRx = new Regex(@"\bResources\s*\.\s*Load(?:All)?\s*(?:<[^>]*>)?\s*\(\s*""([^""]+)""", RegexOptions.Compiled);

        static readonly SrcRule[] Rules =
        {
            new SrcRule(@"\bGameObject\s*\.\s*Find\s*\([^)]*\)\s*\.", Sev.Warning, "NullRef",
                "GameObject.Find(...) langsung di-dereference. Return null kalau objek tidak ada, inactive, atau namanya beda."),
            new SrcRule(@"\bGameObject\s*\.\s*Find\s*\((?![^)]*\)\s*\.)", Sev.Info, "NullRef",
                "GameObject.Find: null kalau objek inactive / belum ada. Cek urutan Awake/Start dan transisi scene."),
            new SrcRule(@"\b(?:FindGameObjectWithTag|FindWithTag)\s*\([^)]*\)\s*\.", Sev.Warning, "NullRef",
                "FindWithTag(...) langsung di-dereference. Null kalau tag tidak dipakai objek aktif manapun."),
            new SrcRule(@"\b(?:FindObjectOfType|FindFirstObjectByType|FindAnyObjectByType)\s*<[^>]*>\s*\([^)]*\)\s*\.", Sev.Warning, "NullRef",
                "FindObjectOfType/FindFirstObjectByType langsung di-dereference. Null kalau komponen tidak ada di scene aktif."),
            new SrcRule(@"\bGetComponent(?:InChildren|InParent)?\s*<[^>]*>\s*\([^)]*\)\s*\.", Sev.Warning, "NullRef",
                "GetComponent<>() langsung di-dereference tanpa null-check. Pertimbangkan TryGetComponent."),
            new SrcRule(@"(?<!GameObject)\s*\.\s*Find\s*\(\s*""[^""]*""\s*\)\s*\.", Sev.Warning, "NullRef",
                "transform.Find(\"...\") langsung di-dereference. Null kalau nama child salah/berubah."),
            new SrcRule(@"\bCamera\s*\.\s*main\s*\.", Sev.Warning, "NullRef",
                "Camera.main null kalau tidak ada kamera ber-tag MainCamera. Di scene AR/Vuforia, pastikan ARCamera bertag MainCamera."),
            new SrcRule(@"\bResources\s*\.\s*Load\s*(?:<[^>]*>)?\s*\([^)]*\)\s*\.", Sev.Warning, "NullRef",
                "Resources.Load(...) langsung di-dereference. Null kalau path salah (di Android path case-sensitive)."),
            new SrcRule(@"\bApplication\s*\.\s*dataPath\b", Sev.Warning, "Editor vs Build",
                "Application.dataPath di Android menunjuk ke APK, bukan folder biasa. File I/O System.IO ke sini gagal di build."),
            new SrcRule(@"(?:\b(?:File|Directory|StreamReader|FileStream)\b.*\bApplication\s*\.\s*streamingAssetsPath|\bApplication\s*\.\s*streamingAssetsPath.*\b(?:File|Directory|StreamReader|FileStream)\b)",
                Sev.Warning, "Editor vs Build",
                "StreamingAssets di Android ada di dalam APK; System.IO tidak bisa membacanya. Pakai UnityWebRequest."),
            new SrcRule(@"\bShader\s*\.\s*Find\s*\(", Sev.Warning, "Editor vs Build",
                "Shader.Find hanya bekerja kalau shader ikut build (Always Included Shaders / dipakai material). Bisa null/pink di APK."),
            new SrcRule(@"\b(?:SendMessage|BroadcastMessage|SendMessageUpwards|Invoke|InvokeRepeating|StartCoroutine)\s*\(\s*""", Sev.Info, "String Call",
                "Pemanggilan method via string: rawan typo/rename dan bisa kena stripping. Pertimbangkan nameof()/delegate."),
            new SrcRule(@"\bType\s*\.\s*GetType\s*\(|\bActivator\s*\.\s*CreateInstance|\.\s*GetMethod\s*\(|\.\s*GetField\s*\(|\.\s*GetProperty\s*\(|\bAssembly\s*\.\s*Load",
                Sev.Info, "Reflection",
                "Reflection: dengan IL2CPP + managed stripping, member bisa terhapus. Pakai [Preserve] atau link.xml."),
            new SrcRule(@"\bDontDestroyOnLoad\s*\(", Sev.Info, "Scene Transition",
                "DontDestroyOnLoad: objek bertahan antar scene. Cek duplikasi saat kembali ke main menu & referensi ke objek scene lama."),
            new SrcRule(@"\b\w+\s*\.\s*Instance\s*\.\s*\w+", Sev.Info, "Scene Transition",
                "Akses singleton .Instance: pastikan sudah di-set (Awake) dan tidak ikut hancur saat load scene."),
        };

        // ---- preprocessor handling
        static Region KindOf(string expr)
        {
            int c = expr.IndexOf("//", StringComparison.Ordinal);
            if (c >= 0) expr = expr.Substring(0, c);
            var m = IfEditorRx.Match(expr);
            if (!m.Success) return Region.Other;
            return m.Groups[1].Success ? Region.PlayerOnly : Region.EditorOnly;
        }

        static void HandleDirective(string t, List<Region> stack)
        {
            if (t.StartsWith("#if", StringComparison.Ordinal)) stack.Add(KindOf(t.Substring(3)));
            else if (t.StartsWith("#elif", StringComparison.Ordinal)) { if (stack.Count > 0) stack[stack.Count - 1] = Region.Other; }
            else if (t.StartsWith("#else", StringComparison.Ordinal))
            {
                if (stack.Count == 0) return;
                var k = stack[stack.Count - 1];
                stack[stack.Count - 1] = k == Region.EditorOnly ? Region.PlayerOnly : (k == Region.PlayerOnly ? Region.EditorOnly : Region.Other);
            }
            else if (t.StartsWith("#endif", StringComparison.Ordinal)) { if (stack.Count > 0) stack.RemoveAt(stack.Count - 1); }
        }

        static Region Effective(List<Region> stack)
        {
            Region r = Region.None;
            for (int i = 0; i < stack.Count; i++) if (stack[i] > r) r = stack[i];
            return r;
        }

        static string StripComments(string s, ref bool inBlock)
        {
            var sb = new StringBuilder(s.Length);
            bool inStr = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                char n = i + 1 < s.Length ? s[i + 1] : '\0';
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (c == '\\' && n != '\0') { sb.Append(n); i++; }
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '/' && n == '/') break;
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static string Short(string s) { s = s.Trim(); return s.Length > 110 ? s.Substring(0, 110) + "..." : s; }

        public static SourceResult Scan(AuditOptions o, HashSet<string> playerFiles, List<AuditIssue> issues)
        {
            var res = new SourceResult();
            string root = AuditUtil.Norm(o.scriptsRoot).TrimEnd('/');
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                issues.Add(new AuditIssue
                {
                    severity = Sev.Error, category = "Source Scan",
                    message = "Folder scripts '" + o.scriptsRoot + "' tidak ditemukan. Atur path-nya di window."
                });
                return res;
            }

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int fi = 0; fi < files.Length; fi++)
            {
                if (fi % 25 == 0)
                    EditorUtility.DisplayProgressBar("Script Auditor", "Scan source: " + Path.GetFileName(files[fi]), (float)fi / files.Length);

                string path = AuditUtil.Norm(files[fi]);
                if (AuditUtil.IsExcluded(path, o)) continue;
                if (playerFiles != null && !playerFiles.Contains(path)) continue; // tidak ikut build -> tidak relevan

                string[] lines;
                try { lines = File.ReadAllLines(path); } catch { continue; }
                res.files++;

                string fileName = Path.GetFileNameWithoutExtension(path);
                bool editorFolder = path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
                var stack = new List<Region>();
                var monoClasses = new List<string>();
                bool inBlock = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = StripComments(lines[i], ref inBlock);
                    string t = line.Trim();
                    if (t.Length == 0) continue;
                    if (t[0] == '#') { HandleDirective(t, stack); continue; }

                    Region region = Effective(stack);
                    int ln = i + 1;

                    // 1. API UnityEditor di luar #if UNITY_EDITOR
                    if (!editorFolder && region != Region.EditorOnly && EditorApiRx.IsMatch(line))
                        issues.Add(new AuditIssue
                        {
                            severity = Sev.Error, category = "Editor API",
                            message = "API UnityEditor dipakai di script runtime di luar #if UNITY_EDITOR. Build bisa gagal compile / script tidak lengkap.",
                            detail = Short(t), assetPath = path, line = ln
                        });

                    // 2. deklarasi class
                    var cm = ClassRx.Match(t);
                    if (cm.Success)
                    {
                        string cname = cm.Groups[1].Value;
                        string bases = cm.Groups[2].Value;
                        bool mono = bases.Contains("MonoBehaviour") || bases.Contains("ScriptableObject");
                        if (mono)
                        {
                            if (region == Region.EditorOnly)
                                issues.Add(new AuditIssue
                                {
                                    severity = Sev.Error, category = "Serialization Layout",
                                    message = "Class '" + cname + "' (MonoBehaviour/ScriptableObject) dibungkus #if UNITY_EDITOR. " +
                                              "Di Editor class ada, di build TIDAK ada -> component yang ter-attach jadi 'script unknown' " +
                                              "(persis pola error 'different serialization layout').",
                                    detail = Short(t), assetPath = path, line = ln
                                });
                            else if (!t.Contains("abstract"))
                                monoClasses.Add(cname);
                        }
                    }

                    // 3. field serialized di dalam blok kondisional
                    if (region != Region.None)
                    {
                        bool ser = SerAttrRx.IsMatch(line) || (PublicFieldRx.IsMatch(line) && !line.Contains("=>"));
                        if (ser && !line.Contains("NonSerialized"))
                        {
                            bool edOnly = region == Region.EditorOnly || region == Region.PlayerOnly;
                            issues.Add(new AuditIssue
                            {
                                severity = edOnly ? Sev.Error : Sev.Warning,
                                category = "Serialization Layout",
                                message = edOnly
                                    ? "Field serialized berada di dalam #if UNITY_EDITOR / #if !UNITY_EDITOR. Layout serialisasi Editor != Player. " +
                                      "Ini penyebab klasik 'different serialization layout when loading'."
                                    : "Field serialized berada di dalam blok #if kondisional. Layout bisa berbeda antar platform/konfigurasi build.",
                                detail = Short(t), assetPath = path, line = ln
                            });
                        }
                    }

                    // 4. heuristik runtime (kode yang tidak jalan di build dilewati)
                    if (region != Region.EditorOnly)
                    {
                        for (int r = 0; r < Rules.Length; r++)
                        {
                            if (!Rules[r].rx.IsMatch(line)) continue;
                            issues.Add(new AuditIssue
                            {
                                severity = Rules[r].sev, category = Rules[r].cat, message = Rules[r].msg,
                                detail = Short(t), assetPath = path, line = ln
                            });
                        }

                        var sm = LoadSceneRx.Match(line);
                        if (sm.Success) res.sceneNames.Add(new SrcRef { value = sm.Groups[1].Value, file = path, line = ln });
                        var rm = ResLoadRx.Match(line);
                        if (rm.Success) res.resourcePaths.Add(new SrcRef { value = rm.Groups[1].Value, file = path, line = ln });
                    }
                }

                // 5. level file
                if (monoClasses.Count > 0 && !monoClasses.Contains(fileName))
                    issues.Add(new AuditIssue
                    {
                        severity = Sev.Warning, category = "Script Naming",
                        message = "File '" + fileName + ".cs' tidak berisi MonoBehaviour/ScriptableObject bernama '" + fileName +
                                  "' (ada: " + string.Join(", ", monoClasses) + "). Nama class harus sama dengan nama file agar bisa di-attach.",
                        assetPath = path
                    });
                else if (monoClasses.Count > 1)
                    issues.Add(new AuditIssue
                    {
                        severity = Sev.Info, category = "Script Naming",
                        message = "Ada " + monoClasses.Count + " MonoBehaviour dalam satu file (" + string.Join(", ", monoClasses) +
                                  "). Hanya yang namanya sama dengan nama file yang bisa di-attach dengan aman.",
                        assetPath = path
                    });
            }
            return res;
        }
    }

    // ------------------------------------------------------- PLAYER AVAILABILITY
    [Serializable]
    class AsmDefData
    {
        public string name;
        public string[] includePlatforms;
        public string[] excludePlatforms;
        public string[] defineConstraints;
    }

    static class PlayerCheck
    {
        // Daftar source file yang BENAR-BENAR dikompilasi ke player untuk build target aktif.
        public static HashSet<string> GetPlayerFiles()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var asm in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
                    foreach (var f in asm.sourceFiles) set.Add(AuditUtil.Norm(f));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UnionAuditor] Gagal membaca assembly player: " + e.Message);
                return null;
            }
            return set.Count > 0 ? set : null;
        }

        static string WhyNotInPlayer(string path)
        {
            var reasons = new List<string>();
            if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                reasons.Add("berada di dalam folder 'Editor'");

            string asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromScriptPath(path);
            if (!string.IsNullOrEmpty(asmdef))
            {
                try
                {
                    var d = JsonUtility.FromJson<AsmDefData>(File.ReadAllText(asmdef));
                    if (d != null)
                    {
                        if (d.includePlatforms != null && d.includePlatforms.Length > 0)
                            reasons.Add(d.includePlatforms.All(x => x == "Editor")
                                ? "asmdef '" + d.name + "' hanya include platform Editor"
                                : "asmdef '" + d.name + "' includePlatforms: " + string.Join(", ", d.includePlatforms));
                        if (d.excludePlatforms != null && d.excludePlatforms.Length > 0)
                            reasons.Add("asmdef '" + d.name + "' excludePlatforms: " + string.Join(", ", d.excludePlatforms));
                        if (d.defineConstraints != null && d.defineConstraints.Length > 0)
                            reasons.Add("asmdef '" + d.name + "' defineConstraints: " + string.Join(", ", d.defineConstraints));
                    }
                }
                catch { reasons.Add("asmdef: " + asmdef); }
            }
            if (reasons.Count == 0) reasons.Add("penyebab tidak terdeteksi, cek asmdef / define constraints / folder");
            return string.Join("; ", reasons);
        }

        public static void CheckUsedScripts(AuditOptions o, HashSet<string> playerFiles, List<AuditIssue> issues)
        {
            if (playerFiles == null)
            {
                issues.Add(new AuditIssue
                {
                    severity = Sev.Info, category = "Player Availability",
                    message = "Daftar assembly player tidak bisa dibaca; pengecekan 'script ikut build?' dilewati."
                });
                return;
            }

            // container = scene di Build Settings + aset di folder Resources
            var containers = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled && File.Exists(s.path)) containers.Add(AuditUtil.Norm(s.path));
            foreach (var p in AssetDatabase.GetAllAssetPaths())
            {
                if (!p.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (p.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    containers.Add(p);
            }

            var usedBy = new Dictionary<string, List<string>>();
            for (int i = 0; i < containers.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Script Auditor", "Dependensi: " + Path.GetFileName(containers[i]), (float)i / Math.Max(1, containers.Count));
                foreach (var dep in AssetDatabase.GetDependencies(containers[i], true))
                {
                    if (!dep.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                    string d = AuditUtil.Norm(dep);
                    List<string> list;
                    if (!usedBy.TryGetValue(d, out list)) { list = new List<string>(); usedBy[d] = list; }
                    list.Add(Path.GetFileName(containers[i]));
                }
            }

            foreach (var kv in usedBy)
            {
                string sp = kv.Key;
                bool inAssets = sp.StartsWith("Assets/", StringComparison.Ordinal);
                if (!inAssets && !o.includePackages) continue;

                string users = string.Join(", ", kv.Value.Distinct().Take(3).ToArray());
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(sp);

                if (ms != null && ms.GetClass() == null)
                    issues.Add(new AuditIssue
                    {
                        severity = Sev.Error, category = "Missing Class",
                        message = "Script dipakai oleh [" + users + "] tapi tidak punya class valid di Editor " +
                                  "(nama file != nama class, error compile, atau class dibungkus #if).",
                        assetPath = sp
                    });

                if (!playerFiles.Contains(sp))
                    issues.Add(new AuditIssue
                    {
                        severity = Sev.Error, category = "Not In Player Build",
                        message = "Script dipakai oleh [" + users + "] tetapi TIDAK dikompilasi ke player (" + WhyNotInPlayer(sp) + "). " +
                                  "Di APK component-nya jadi 'script unknown' -> cocok dengan error 'different serialization layout'.",
                        assetPath = sp
                    });
            }
        }
    }

    // --------------------------------------------------- BUILD SETTINGS + RESOURCES
    static class BuildCheck
    {
        static IEnumerable<string> Forms(string scenePath)
        {
            string noExt = scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                ? scenePath.Substring(0, scenePath.Length - 6) : scenePath;
            yield return Path.GetFileNameWithoutExtension(scenePath);
            yield return noExt;
            if (noExt.StartsWith("Assets/", StringComparison.Ordinal)) yield return noExt.Substring(7);
            yield return scenePath;
        }

        static bool Match(string v, string scenePath, StringComparison cmp)
        {
            return Forms(scenePath).Any(f => string.Equals(f, v, cmp));
        }

        static string CorrectForm(string v, string scenePath)
        {
            return Forms(scenePath).FirstOrDefault(f => string.Equals(f, v, StringComparison.OrdinalIgnoreCase));
        }

        public static void Run(SourceResult src, List<AuditIssue> issues)
        {
            var scenes = EditorBuildSettings.scenes;
            var enabled = scenes.Where(s => s.enabled).Select(s => AuditUtil.Norm(s.path)).ToList();
            if (enabled.Count == 0)
                issues.Add(new AuditIssue { severity = Sev.Warning, category = "Build Settings", message = "Tidak ada scene yang aktif di Build Settings." });

            foreach (var s in scenes)
                if (!File.Exists(s.path))
                    issues.Add(new AuditIssue
                    {
                        severity = Sev.Error, category = "Build Settings",
                        message = "Scene di Build Settings tidak ditemukan di disk: " + s.path, assetPath = s.path,
                        fix = "removeBuildScene", fixArg1 = AuditUtil.Norm(s.path)
                    });

            if (src == null) return;
            foreach (var r in src.sceneNames)
            {
                if (enabled.Any(p => Match(r.value, p, StringComparison.Ordinal))) continue;

                string caseFix = null;
                foreach (var p in enabled) { caseFix = CorrectForm(r.value, p); if (caseFix != null) break; }
                bool caseOnly = caseFix != null;

                string disabledPath = null;
                if (!caseOnly)
                    disabledPath = scenes.Where(s => !s.enabled).Select(s => AuditUtil.Norm(s.path))
                                         .FirstOrDefault(p => Match(r.value, p, StringComparison.OrdinalIgnoreCase));

                var issue = new AuditIssue
                {
                    severity = caseOnly ? Sev.Warning : Sev.Error, category = "Scene Transition",
                    assetPath = r.file, line = r.line
                };
                if (caseOnly)
                {
                    issue.message = "LoadScene(\"" + r.value + "\") beda huruf besar/kecil dengan nama scene di Build Settings.";
                    issue.fix = "replaceLiteral"; issue.fixArg1 = r.value; issue.fixArg2 = caseFix;
                }
                else if (disabledPath != null)
                {
                    issue.message = "LoadScene(\"" + r.value + "\"): scene ada di Build Settings tapi checkbox-nya OFF.";
                    issue.fix = "enableScene"; issue.fixArg1 = disabledPath;
                }
                else
                {
                    issue.message = "LoadScene(\"" + r.value + "\"): scene tidak ada di Build Settings (atau nama beda). Load akan gagal di build.";
                }
                issues.Add(issue);
            }
        }
    }

    static class ResourcesCheck
    {
        public static void Run(SourceResult src, List<AuditIssue> issues)
        {
            if (src.resourcePaths.Count == 0) return;
            var exact = new HashSet<string>();
            var ciMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            const string marker = "/Resources/";
            foreach (var p in AssetDatabase.GetAllAssetPaths())
            {
                if (!p.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                int idx = p.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;
                string rel = AuditUtil.Norm(Path.ChangeExtension(p.Substring(idx + marker.Length), null));
                exact.Add(rel);
                if (!ciMap.ContainsKey(rel)) ciMap[rel] = rel;
            }

            foreach (var r in src.resourcePaths)
            {
                string v = AuditUtil.Norm(r.value).Trim('/');
                if (exact.Contains(v)) continue;
                string correct;
                bool caseOnly = ciMap.TryGetValue(v, out correct);
                var issue = new AuditIssue
                {
                    severity = caseOnly ? Sev.Error : Sev.Warning, category = "Resources",
                    message = caseOnly
                        ? "Resources.Load(\"" + r.value + "\"): beda huruf besar/kecil dengan aset aslinya. Bisa lolos di Editor (Windows) tapi null di Android."
                        : "Resources.Load(\"" + r.value + "\"): tidak ditemukan di folder Resources manapun.",
                    assetPath = r.file, line = r.line
                };
                if (caseOnly) { issue.fix = "replaceLiteral"; issue.fixArg1 = r.value; issue.fixArg2 = correct; }
                issues.Add(issue);
            }
        }
    }

    // ------------------------------------------------------------ .META SCANNER
    static class MetaScanner
    {
        static readonly Regex GuidRx = new Regex("^[0-9a-f]{32}$", RegexOptions.Compiled);

        public static string ReadGuid(string metaPath)
        {
            try
            {
                using (var r = new StreamReader(metaPath))
                {
                    for (int i = 0; i < 12; i++)
                    {
                        string l = r.ReadLine();
                        if (l == null) break;
                        if (!l.StartsWith("guid:", StringComparison.Ordinal)) continue;
                        string g = l.Substring(5).Trim().ToLowerInvariant();
                        return GuidRx.IsMatch(g) ? g : null;
                    }
                }
            }
            catch { }
            return null;
        }

        // aturan Unity: file/folder berawalan '.', berakhiran '~', '.tmp', atau bernama 'cvs' diabaikan
        static bool IgnoredByUnity(string path)
        {
            foreach (var seg in path.Split('/'))
            {
                if (seg.Length == 0) continue;
                if (seg[0] == '.' || seg.EndsWith("~", StringComparison.Ordinal) ||
                    seg.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                    seg.Equals("cvs", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void Run(AuditOptions o, List<AuditIssue> issues)
        {
            if (!Directory.Exists("Assets")) return;
            var entries = Directory.GetFileSystemEntries("Assets", "*", SearchOption.AllDirectories)
                                   .Select(e => AuditUtil.Norm(e)).ToList();
            var set = new HashSet<string>(entries);
            var byGuid = new Dictionary<string, List<string>>();

            for (int i = 0; i < entries.Count; i++)
            {
                if (i % 500 == 0)
                    EditorUtility.DisplayProgressBar("Script Auditor", "Cek .meta ...", (float)i / Math.Max(1, entries.Count));

                string e = entries[i];
                if (IgnoredByUnity(e)) continue;

                if (e.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    string asset = e.Substring(0, e.Length - 5);
                    if (AuditUtil.IsExcluded(asset, o)) continue;

                    if (!set.Contains(asset))
                    {
                        issues.Add(new AuditIssue
                        {
                            severity = Sev.Warning, category = "Orphan .meta",
                            message = "File .meta tanpa file/folder pasangannya: " + e + " (aset dihapus/dipindah tanpa .meta-nya).",
                            assetPath = AuditUtil.Norm(Path.GetDirectoryName(e))
                        });
                        continue;
                    }

                    string g = ReadGuid(e);
                    if (g == null)
                    {
                        issues.Add(new AuditIssue
                        {
                            severity = Sev.Error, category = "Meta Rusak",
                            message = "File .meta tidak punya GUID yang valid: " + e, assetPath = asset
                        });
                        continue;
                    }

                    List<string> list;
                    if (!byGuid.TryGetValue(g, out list)) { list = new List<string>(); byGuid[g] = list; }
                    list.Add(e);

                    string dbGuid = AssetDatabase.AssetPathToGUID(asset);
                    if (!string.IsNullOrEmpty(dbGuid) && dbGuid != g)
                        issues.Add(new AuditIssue
                        {
                            severity = Sev.Error, category = "Meta Mismatch",
                            message = "GUID di file .meta (" + g.Substring(0, 8) + "...) berbeda dengan GUID yang dipakai Unity (" +
                                      dbGuid.Substring(0, 8) + "...). .meta diubah dari luar Unity dan belum di-refresh.",
                            assetPath = asset
                        });
                }
                else
                {
                    if (AuditUtil.IsExcluded(e, o)) continue;
                    if (!set.Contains(e + ".meta"))
                        issues.Add(new AuditIssue
                        {
                            severity = Sev.Warning, category = "Missing .meta",
                            message = "File/folder tanpa .meta: " + e + ". Unity akan membuat GUID baru, sehingga GUID berbeda di tiap mesin/clone.",
                            assetPath = e
                        });
                }
            }

            foreach (var kv in byGuid)
            {
                if (kv.Value.Count < 2) continue;
                string g8 = kv.Key.Substring(0, 8);
                foreach (var m in kv.Value)
                    issues.Add(new AuditIssue
                    {
                        severity = Sev.Error, category = "Duplicate GUID",
                        group = "GUID kembar " + g8 + "...  (" + kv.Value.Count + " file)",
                        message = "GUID sama dengan: " + string.Join(", ", kv.Value.Where(x => x != m).Select(x => Path.GetFileName(x)).Take(3).ToArray()) +
                                  ". Unity hanya memakai salah satunya; referensi bisa menunjuk ke file yang salah.",
                        assetPath = m.Substring(0, m.Length - 5),
                        detail = "guid: " + kv.Key
                    });
            }
        }
    }

    // ------------------------------------------------------------ SALINAN ASLI (opsional)
    // Membaca .meta dari salinan project lama (mis. hasil ekstrak Project.rar) -> peta GUID lama -> path lama.
    static class OriginalIndex
    {
        public static Dictionary<string, string> Build(string folder)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return map;

            string root = Path.GetFullPath(folder.TrimEnd('/', '\\'));
            string assets = Directory.Exists(Path.Combine(root, "Assets")) ? Path.Combine(root, "Assets") : root;
            assets = Path.GetFullPath(assets);
            string baseDir = Directory.GetParent(assets).FullName;

            foreach (var m in Directory.GetFiles(assets, "*.meta", SearchOption.AllDirectories))
            {
                string g = MetaScanner.ReadGuid(m);
                if (g == null || map.ContainsKey(g)) continue;
                string full = m.Substring(0, m.Length - 5);
                map[g] = AuditUtil.Norm(full.Substring(baseDir.Length)).TrimStart('/');
            }
            return map;
        }

        // path lama -> path sekarang (path sama, atau nama file unik)
        public static string ResolveCurrent(string oldRel)
        {
            if ((File.Exists(oldRel) || Directory.Exists(oldRel)) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(oldRel))) return oldRel;
            string fileName = Path.GetFileName(oldRel);
            string name = Path.GetFileNameWithoutExtension(oldRel);
            var hits = AssetDatabase.FindAssets(name, new[] { "Assets" })
                .Select(g => AuditUtil.Norm(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase))
                .Distinct().ToList();
            return hits.Count == 1 ? hits[0] : null;
        }
    }

    // ------------------------------------------------------------ YAML ASSET / GUID SCANNER
    // Membaca YAML (.asset .unity .prefab .mat .controller .anim): m_Script putus + referensi GUID putus.
    static class AssetScanner
    {
        static readonly string[] YamlExts = { ".asset", ".unity", ".prefab", ".mat", ".controller", ".anim", ".overrideController" };
        static readonly Regex ScriptRx = new Regex(@"m_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*(\d+)\}", RegexOptions.Compiled);
        static readonly Regex RefRx = new Regex(@"\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*(\d+)\}", RegexOptions.Compiled);
        static readonly Regex KeyRx = new Regex(@"^\s*(?:-\s+)?([A-Za-z_][\w\.]*)\s*:", RegexOptions.Compiled);
        static readonly Regex FieldRx = new Regex(@"^  ([A-Za-z_]\w*):", RegexOptions.Compiled);
        static readonly HashSet<string> StdKeys = new HashSet<string>
        {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
            "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"
        };
        const long MaxBytes = 20L * 1024 * 1024;
        const int MaxRefIssuesPerAsset = 25;

        sealed class RefInfo { public string key; public string guid; public int line; }
        sealed class AssetInfo
        {
            public string path;
            public string scriptGuid;   // m_Script pertama (untuk .asset)
            public Dictionary<string, HashSet<string>> fieldsByGuid = new Dictionary<string, HashSet<string>>();
            public List<RefInfo> refs = new List<RefInfo>();
        }
        sealed class ScriptCand { public string path; public string className; public bool isSO; public HashSet<string> fields; }

        internal static bool IsYaml(string p)
        {
            foreach (var e in YamlExts) if (p.EndsWith(e, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool IsZeroGuid(string g) { return g.StartsWith("0000000000000000", StringComparison.Ordinal); }

        static AssetInfo Parse(string path, string[] lines)
        {
            var info = new AssetInfo { path = path };
            bool mono = false;
            string cur = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                if (l.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    mono = l.StartsWith("--- !u!114", StringComparison.Ordinal);
                    cur = null;
                    continue;
                }

                var sm = ScriptRx.Match(l);
                if (sm.Success)
                {
                    if (mono)
                    {
                        cur = sm.Groups[2].Value.ToLowerInvariant();
                        if (info.scriptGuid == null) info.scriptGuid = cur;
                        if (!info.fieldsByGuid.ContainsKey(cur)) info.fieldsByGuid[cur] = new HashSet<string>();
                    }
                    continue;
                }

                if (mono && cur != null)
                {
                    var fm = FieldRx.Match(l);
                    if (fm.Success && !StdKeys.Contains(fm.Groups[1].Value)) info.fieldsByGuid[cur].Add(fm.Groups[1].Value);
                }

                foreach (Match m in RefRx.Matches(l))
                {
                    string g = m.Groups[2].Value.ToLowerInvariant();
                    if (IsZeroGuid(g)) continue;
                    var km = KeyRx.Match(l);
                    info.refs.Add(new RefInfo { key = km.Success ? km.Groups[1].Value : "(elemen array)", guid = g, line = i + 1 });
                }
            }
            return info;
        }

        static List<string> GetContainers()
        {
            var containers = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled && File.Exists(s.path)) containers.Add(AuditUtil.Norm(s.path));
            foreach (var p in AssetDatabase.GetAllAssetPaths())
            {
                if (!p.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (p.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    containers.Add(p);
            }
            return containers;
        }

        // null = tidak dipakai build
        static string UsersOf(string assetPath, List<string> containers, Dictionary<string, HashSet<string>> cache)
        {
            if (assetPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0) return "folder Resources";
            var hit = new List<string>();
            foreach (var c in containers)
            {
                if (c == assetPath) continue;
                HashSet<string> deps;
                if (!cache.TryGetValue(c, out deps))
                {
                    deps = new HashSet<string>(AssetDatabase.GetDependencies(c, true).Select(x => AuditUtil.Norm(x)));
                    cache[c] = deps;
                }
                if (deps.Contains(assetPath)) hit.Add(Path.GetFileName(c));
            }
            if (hit.Count == 0) return null;
            var d = hit.Distinct().ToList();
            return string.Join(", ", d.Take(3).ToArray()) + (d.Count > 3 ? ", +" + (d.Count - 3) : "");
        }

        static HashSet<string> SerializedFieldNames(Type t)
        {
            var set = new HashSet<string>();
            for (var c = t; c != null && c != typeof(ScriptableObject) && c != typeof(MonoBehaviour) && c != typeof(object); c = c.BaseType)
            {
                foreach (var f in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsNotSerialized) continue;
                    if (f.IsPublic ||
                        f.GetCustomAttributes(typeof(SerializeField), true).Length > 0 ||
                        f.GetCustomAttributes(typeof(SerializeReference), true).Length > 0)
                        set.Add(f.Name);
                }
            }
            return set;
        }

        static List<ScriptCand> BuildCands()
        {
            var list = new List<ScriptCand>();
            foreach (var g in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }))
            {
                string sp = AuditUtil.Norm(AssetDatabase.GUIDToAssetPath(g));
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(sp);
                var t = ms != null ? ms.GetClass() : null;
                if (t == null || t.IsAbstract || t.IsGenericTypeDefinition) continue;
                bool so = typeof(ScriptableObject).IsAssignableFrom(t);
                bool mb = typeof(MonoBehaviour).IsAssignableFrom(t);
                if (!so && !mb) continue;
                list.Add(new ScriptCand { path = sp, className = t.Name, isSO = so, fields = SerializedFieldNames(t) });
            }
            return list;
        }

        // Kandidat dinilai dari: nama class == nama asset, dan irisan nama field YAML vs field serialized class.
        static List<AuditCandidate> SuggestFor(HashSet<string> yamlFields, string assetName, bool wantSO, List<ScriptCand> cands)
        {
            var scored = new List<KeyValuePair<double, AuditCandidate>>();
            foreach (var c in cands)
            {
                if (c.isSO != wantSO) continue;
                int inter = yamlFields.Count(f => c.fields.Contains(f));
                int union = yamlFields.Count + c.fields.Count - inter;
                double j = union == 0 ? 0.0 : (double)inter / union;
                bool nameMatch = !string.IsNullOrEmpty(assetName) && string.Equals(c.className, assetName, StringComparison.OrdinalIgnoreCase);
                double score = Math.Min(1.0, j + (nameMatch ? 0.5 : 0.0));
                if (score < 0.15 && !nameMatch) continue;
                scored.Add(new KeyValuePair<double, AuditCandidate>(score, new AuditCandidate
                {
                    scriptPath = c.path,
                    label = Path.GetFileName(c.path) + "  -  cocok " + Mathf.RoundToInt((float)score * 100f) + "% (" +
                            inter + "/" + yamlFields.Count + " field" + (nameMatch ? ", nama sama" : "") + ")"
                }));
            }
            return scored.OrderByDescending(x => x.Key).Take(5).Select(x => x.Value).ToList();
        }

        static void AddOriginalCandidate(List<AuditCandidate> list, string guid, Dictionary<string, string> orig)
        {
            if (orig == null) return;
            string oldRel;
            if (!orig.TryGetValue(guid, out oldRel)) return;
            string cur = OriginalIndex.ResolveCurrent(oldRel);
            if (cur == null) return;
            list.Insert(0, new AuditCandidate
            {
                scriptPath = cur,
                label = "Dari salinan asli: " + oldRel + "  ->  sekarang: " + cur + "  (path/nama cocok)"
            });
        }

        public static void Run(AuditOptions o, List<AuditIssue> issues)
        {
            var paths = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && IsYaml(p) && !AuditUtil.IsExcluded(p, o))
                .ToList();

            // ---- pass 1: baca & parse semua YAML
            var infos = new List<AssetInfo>();
            int skipped = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                if (i % 20 == 0)
                    EditorUtility.DisplayProgressBar("Script Auditor", "Scan YAML: " + Path.GetFileName(paths[i]), (float)i / Math.Max(1, paths.Count));
                try
                {
                    var fi = new FileInfo(paths[i]);
                    if (!fi.Exists) continue;
                    if (fi.Length > MaxBytes) { skipped++; continue; }
                    string[] lines = File.ReadAllLines(paths[i]);
                    if (lines.Length == 0 || !lines[0].StartsWith("%YAML", StringComparison.Ordinal)) { skipped++; continue; }
                    infos.Add(Parse(paths[i], lines));
                }
                catch { skipped++; }
            }
            if (skipped > 0)
                issues.Add(new AuditIssue
                {
                    severity = Sev.Info, category = "Asset Scan",
                    message = skipped + " file YAML dilewati (binary / lebih dari 20 MB / tidak terbaca). Set Asset Serialization ke Force Text agar bisa dicek."
                });

            var containers = GetContainers();
            var containerSet = new HashSet<string>(containers);
            var depCache = new Dictionary<string, HashSet<string>>();
            var brokenPaths = new HashSet<string>();
            Dictionary<string, string> orig = string.IsNullOrWhiteSpace(o.originalProject) ? null : OriginalIndex.Build(o.originalProject);
            List<ScriptCand> cands = null;
            var assetInfos = infos.Where(a => a.path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)).ToList();

            // ---- pass 2: m_Script pada .asset
            foreach (var a in assetInfos)
            {
                if (a.scriptGuid == null) continue;
                string name = Path.GetFileNameWithoutExtension(a.path);
                string sp = IsZeroGuid(a.scriptGuid) ? "" : AuditUtil.Norm(AssetDatabase.GUIDToAssetPath(a.scriptGuid));

                if (string.IsNullOrEmpty(sp))
                {
                    brokenPaths.Add(a.path);
                    string users = UsersOf(a.path, containers, depCache);
                    var issue = new AuditIssue
                    {
                        severity = users != null ? Sev.Error : Sev.Warning,
                        category = "Missing Script (Asset)",
                        message = "Asset '" + name + "' Missing Script: script dengan GUID " + a.scriptGuid.Substring(0, 8) +
                                  "... tidak ditemukan di project (script dihapus/dibuat ulang, atau .meta-nya hilang/berubah saat dipindah). " +
                                  (users != null ? "DIPAKAI build lewat [" + users + "] -> di APK jadi 'script unknown'."
                                                 : "Tidak terlihat dipakai scene build manapun."),
                        detail = "m_Script guid: " + a.scriptGuid,
                        assetPath = a.path,
                        fixArg1 = a.scriptGuid
                    };
                    if (cands == null) cands = BuildCands();
                    issue.candidates = SuggestFor(a.fieldsByGuid[a.scriptGuid], name, true, cands);
                    AddOriginalCandidate(issue.candidates, a.scriptGuid, orig);
                    issues.Add(issue);
                }
                else if (sp.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(sp);
                    var cls = ms != null ? ms.GetClass() : null;
                    if (ms != null && cls == null)
                    {
                        brokenPaths.Add(a.path);
                        issues.Add(new AuditIssue
                        {
                            severity = Sev.Error, category = "Missing Class (Asset)",
                            message = "Asset '" + name + "' menunjuk ke script '" + Path.GetFileName(sp) +
                                      "' tapi file itu tidak punya class valid (nama class != nama file, error compile, atau class dibungkus #if).",
                            assetPath = a.path, detail = "script: " + sp
                        });
                    }
                    else if (cls != null && !typeof(ScriptableObject).IsAssignableFrom(cls))
                    {
                        issues.Add(new AuditIssue
                        {
                            severity = Sev.Warning, category = "Missing Class (Asset)",
                            message = "Asset '" + name + "' menunjuk ke script '" + Path.GetFileName(sp) + "' yang class-nya (" + cls.Name + ") bukan ScriptableObject.",
                            assetPath = a.path, detail = "script: " + sp
                        });
                    }
                }
            }

            // ---- pass 3: referensi putus per field di dalam .asset (expandable per asset)
            foreach (var a in assetInfos)
            {
                string name = Path.GetFileNameWithoutExtension(a.path);
                string group = "Referensi bermasalah di " + name + "  (" + a.path + ")";
                int count = 0, extra = 0;
                foreach (var r in a.refs)
                {
                    string rp = AuditUtil.Norm(AssetDatabase.GUIDToAssetPath(r.guid));
                    string msg = null; Sev sev = Sev.Error;

                    if (string.IsNullOrEmpty(rp))
                        msg = "Field '" + r.key + "' menunjuk GUID " + r.guid.Substring(0, 8) +
                              "... yang tidak ada di project (target dihapus, atau .meta berubah saat dipindah).";
                    else if (brokenPaths.Contains(rp) && rp != a.path)
                    {
                        msg = "Field '" + r.key + "' menunjuk ke asset '" + Path.GetFileName(rp) + "' yang Missing Script.";
                        sev = Sev.Warning;
                    }
                    if (msg == null) continue;

                    if (++count > MaxRefIssuesPerAsset) { extra++; continue; }
                    issues.Add(new AuditIssue
                    {
                        severity = sev, category = "Broken Reference (Asset)", group = group,
                        message = msg, assetPath = a.path, line = r.line
                    });
                }
                if (extra > 0)
                    issues.Add(new AuditIssue
                    {
                        severity = Sev.Info, category = "Broken Reference (Asset)", group = group,
                        message = "+" + extra + " referensi bermasalah lainnya di asset ini tidak ditampilkan.", assetPath = a.path
                    });
            }

            // ---- pass 4: tingkat GUID untuk SEMUA tipe YAML (scene, prefab, material, ...) + remap
            var missingScripts = new Dictionary<string, List<AssetInfo>>();
            var danglingFiles = new Dictionary<string, List<string>>();
            var danglingKeys = new Dictionary<string, HashSet<string>>();

            foreach (var a in infos)
            {
                foreach (var g in a.fieldsByGuid.Keys)
                {
                    if (IsZeroGuid(g) || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g))) continue;
                    List<AssetInfo> l;
                    if (!missingScripts.TryGetValue(g, out l)) { l = new List<AssetInfo>(); missingScripts[g] = l; }
                    l.Add(a);
                }
                foreach (var r in a.refs)
                {
                    if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(r.guid))) continue;
                    List<string> l;
                    if (!danglingFiles.TryGetValue(r.guid, out l)) { l = new List<string>(); danglingFiles[r.guid] = l; danglingKeys[r.guid] = new HashSet<string>(); }
                    if (!l.Contains(a.path)) l.Add(a.path);
                    danglingKeys[r.guid].Add(r.key);
                }
            }

            foreach (var kv in missingScripts)
            {
                var files = kv.Value.Select(x => x.path).Distinct().ToList();
                if (files.All(f => f.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))) continue; // sudah dilaporkan per-asset
                bool wantSO = kv.Value[0].path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
                var fields = new HashSet<string>();
                foreach (var x in kv.Value) HashSetUnion(fields, x.fieldsByGuid[kv.Key]);

                if (cands == null) cands = BuildCands();
                var issue = MakeGuidIssue(kv.Key, files, null, "Missing Script (GUID)",
                    "Script dengan GUID " + kv.Key.Substring(0, 8) + "... tidak ditemukan di project. Component yang memakainya jadi Missing Script. " +
                    "Pilih kandidat di bawah untuk me-remap GUID ini di semua file sekaligus.",
                    containerSet, containers, depCache);
                issue.candidates = SuggestFor(fields, null, wantSO, cands);
                AddOriginalCandidate(issue.candidates, kv.Key, orig);
                issues.Add(issue);
            }

            foreach (var kv in danglingFiles)
            {
                if (missingScripts.ContainsKey(kv.Key)) continue;
                bool onlyAssets = kv.Value.All(f => f.EndsWith(".asset", StringComparison.OrdinalIgnoreCase));
                bool hasOrig = orig != null && orig.ContainsKey(kv.Key);
                if (onlyAssets && !hasOrig) continue; // sudah tampil per-field di grup asset

                var issue = MakeGuidIssue(kv.Key, kv.Value, danglingKeys[kv.Key], "Broken Reference (GUID)",
                    "Aset dengan GUID " + kv.Key.Substring(0, 8) + "... tidak ada di project, tetapi masih dirujuk file lain " +
                    "(aset target dihapus, atau .meta-nya berubah saat dipindah).",
                    containerSet, containers, depCache);
                AddOriginalCandidate(issue.candidates, kv.Key, orig);
                issues.Add(issue);
            }
        }

        static void HashSetUnion(HashSet<string> into, HashSet<string> from) { foreach (var x in from) into.Add(x); }

        static AuditIssue MakeGuidIssue(string guid, List<string> files, HashSet<string> keys, string category, string message,
                                        HashSet<string> containerSet, List<string> containers, Dictionary<string, HashSet<string>> depCache)
        {
            bool inBuild = files.Take(15).Any(f => containerSet.Contains(f) || UsersOf(f, containers, depCache) != null);
            string info = files.Count + " file: " + string.Join(", ", files.Take(6).Select(f => Path.GetFileName(f)).ToArray()) + (files.Count > 6 ? ", ..." : "");
            if (keys != null && keys.Count > 0) info += "  |  field: " + string.Join(", ", keys.Take(6).ToArray());
            return new AuditIssue
            {
                severity = inBuild ? Sev.Error : Sev.Warning,
                category = category,
                message = message + (inBuild ? " Dipakai oleh scene/prefab yang ikut build." : ""),
                detail = "guid: " + guid,
                info = info,
                assetPath = files[0],
                fixArg1 = guid
            };
        }
    }

    // ------------------------------------------------------------ OBJECT SCANNER
    public sealed class ObjectScanner
    {
        readonly AuditOptions o;
        readonly List<AuditIssue> issues;
        readonly List<Regex> ignore = new List<Regex>();
        public int objects, components, userComponents;

        public ObjectScanner(AuditOptions o, List<AuditIssue> issues)
        {
            this.o = o; this.issues = issues;
            foreach (var p in (o.ignoreFieldPatterns ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string s = p.Trim();
                if (s.Length == 0) continue;
                ignore.Add(new Regex("^" + Regex.Escape(s).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase));
            }
        }

        void Add(Sev sev, string cat, string msg, string container, string hp, string detail = null)
        {
            issues.Add(new AuditIssue { severity = sev, category = cat, message = msg, assetPath = container, objectPath = hp, detail = detail });
        }

        bool IsIgnored(string typeName, string path)
        {
            if (ignore.Count == 0) return false;
            string top = path;
            int d = top.IndexOfAny(new[] { '.', '[' });
            if (d >= 0) top = top.Substring(0, d);
            return ignore.Any(r => r.IsMatch(top) || r.IsMatch(typeName + "." + top));
        }

        public void ScanScene(Scene scene, string scenePath)
        {
            foreach (var root in scene.GetRootGameObjects()) ScanHierarchy(root, scenePath);
        }

        public void ScanHierarchy(GameObject root, string container)
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                var go = tr.gameObject;
                objects++;
                string hp = AuditUtil.HierarchyPath(tr);
                var comps = go.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (comps[i] == null)
                    {
                        issues.Add(new AuditIssue
                        {
                            severity = Sev.Error, category = "Missing Script",
                            message = "Component #" + i + " pada '" + go.name + "' adalah Missing Script (script dihapus / GUID rusak / class tidak ditemukan).",
                            assetPath = container, objectPath = hp,
                            fix = "removeMissingScripts"
                        });
                        continue;
                    }
                    components++;
                    InspectComponent(comps[i], go, container, hp);
                }
            }
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

        static bool HasMethod(Type t, string name)
        {
            const BindingFlags f = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            return t.GetMethods(f).Any(m => m.Name == name);
        }

        void InspectComponent(Component c, GameObject go, string container, string hp)
        {
            if (c is Transform) return;

            var mb = c as MonoBehaviour;
            bool userScript = false;
            if (mb != null)
            {
                var ms = MonoScript.FromMonoBehaviour(mb);
                string sp = ms != null ? AuditUtil.Norm(AssetDatabase.GetAssetPath(ms)) : null;
                if (!string.IsNullOrEmpty(sp))
                {
                    bool inAssets = sp.StartsWith("Assets/", StringComparison.Ordinal);
                    userScript = (inAssets || o.includePackages) && !AuditUtil.IsExcluded(sp, o);
                }
            }
            if (!userScript && !o.scanUnityEventsAll) return;
            if (userScript) userComponents++;

            string typeName = c.GetType().Name;
            int refs = 0, nulls = 0;
            bool goEditorOnly = AuditUtil.IsEditorOnlyTagged(go);

            using (var so = new SerializedObject(c))
            {
                var prop = so.GetIterator();
                bool enter = true;
                while (prop.NextVisible(enter))
                {
                    enter = ShouldEnter(prop);
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;

                    string path = prop.propertyPath;
                    if (path == "m_Script") continue;

                    // UnityEvent persistent calls
                    if (path.Contains("m_PersistentCalls"))
                    {
                        if (path.EndsWith(".m_Target", StringComparison.Ordinal))
                            CheckEventTarget(so, prop, typeName, container, hp);
                        continue;
                    }

                    if (!userScript || IsIgnored(typeName, path)) continue;

                    refs++;
                    var val = prop.objectReferenceValue;
                    if (val == null)
                    {
                        nulls++;
                        Add(Sev.Warning, "Unassigned Reference",
                            typeName + "." + AuditUtil.DisplayPath(path) + " kosong (None/Missing). Tipe: " + AuditUtil.PPtrType(prop.type) +
                            ". Potensi NullReferenceException kalau tidak di-set lewat kode.",
                            container, hp);
                    }
                    else if (!goEditorOnly)
                    {
                        GameObject tg = val as GameObject;
                        if (tg == null) { var tc = val as Component; if (tc != null) tg = tc.gameObject; }
                        if (tg != null && AuditUtil.IsEditorOnlyTagged(tg))
                            Add(Sev.Error, "EditorOnly Reference",
                                typeName + "." + AuditUtil.DisplayPath(path) + " menunjuk ke '" + tg.name +
                                "' yang bertag EditorOnly. Objek itu DIBUANG saat build -> referensi jadi null di APK.",
                                container, hp);
                    }
                }
            }

            if (userScript && o.inventory)
                Add(Sev.Info, "Inventory", typeName + ": " + refs + " object-reference, " + nulls + " kosong", container, hp);
        }

        void CheckEventTarget(SerializedObject so, SerializedProperty targetProp, string owner, string container, string hp)
        {
            string p = targetProp.propertyPath;
            string basePath = p.Substring(0, p.Length - "m_Target".Length);
            var mName = so.FindProperty(basePath + "m_MethodName");
            string method = mName != null ? mName.stringValue : "";
            if (string.IsNullOrEmpty(method)) return;

            string evPath = AuditUtil.DisplayPath(basePath.TrimEnd('.'));
            var target = targetProp.objectReferenceValue;
            if (target == null)
                Add(Sev.Error, "UnityEvent",
                    owner + ": event '" + evPath + "' memanggil '" + method + "' tapi target kosong. Panggilan diam-diam tidak jalan.",
                    container, hp);
            else if (!HasMethod(target.GetType(), method))
                Add(Sev.Error, "UnityEvent",
                    owner + ": event '" + evPath + "' memanggil '" + method + "' tapi method itu tidak ada di " + target.GetType().Name +
                    " (di-rename / dihapus?).",
                    container, hp);
        }
    }

    // ------------------------------------------------------------------ RUNNER
    public static class AuditRunner
    {
        public static string LastSummary = "";

        public static List<AuditIssue> Run(AuditOptions o, bool scanObjects)
        {
            var issues = new List<AuditIssue>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string objInfo = "";
            try
            {
                var player = PlayerCheck.GetPlayerFiles();
                SourceResult src = null;

                if (o.sourceScan) src = SourceScanner.Scan(o, player, issues);
                if (o.playerAvailability) PlayerCheck.CheckUsedScripts(o, player, issues);
                if (o.buildSettings) BuildCheck.Run(src, issues);
                if (o.resourcesCheck && src != null) ResourcesCheck.Run(src, issues);
                if (o.assetScan) AssetScanner.Run(o, issues);
                if (o.metaCheck) MetaScanner.Run(o, issues);

                if (scanObjects)
                {
                    var sc = new ObjectScanner(o, issues);
                    if (o.sceneScope == SceneScope.OpenScenes) ScanOpenScenes(sc);
                    else if (o.sceneScope == SceneScope.BuildScenes) ScanBuildScenes(sc, issues);
                    if (o.scanPrefabs) ScanPrefabs(o, sc);
                    objInfo = " | " + sc.objects + " GameObject, " + sc.components + " component (" + sc.userComponents + " script sendiri)";
                }

                LastSummary = "Build target: " + EditorUserBuildSettings.activeBuildTarget +
                              " | " + (src != null ? src.files + " file .cs" : "source scan off") + objInfo +
                              " | " + sw.Elapsed.TotalSeconds.ToString("0.0") + " dtk";
            }
            finally { EditorUtility.ClearProgressBar(); }

            return issues
                .OrderByDescending(i => (int)i.severity)
                .ThenBy(i => i.category)
                .ThenBy(i => i.assetPath ?? "")
                .ThenBy(i => i.line)
                .ToList();
        }

        static void ScanOpenScenes(ObjectScanner sc)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) sc.ScanScene(s, AuditUtil.Norm(s.path));
            }
        }

        static void ScanBuildScenes(ObjectScanner sc, List<AuditIssue> issues)
        {
            var paths = EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(s.path))
                .Select(s => AuditUtil.Norm(s.path)).ToList();
            if (paths.Count == 0) return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                issues.Add(new AuditIssue { severity = Sev.Info, category = "Scene Scan", message = "Scan scene dibatalkan (perubahan scene belum disimpan)." });
                return;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Script Auditor", "Scan scene: " + Path.GetFileName(paths[i]), (float)i / paths.Count);
                    var scene = EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Single);
                    sc.ScanScene(scene, paths[i]);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        static void ScanPrefabs(AuditOptions o, ObjectScanner sc)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AuditUtil.Norm(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (AuditUtil.IsExcluded(path, o)) continue;
                if (i % 10 == 0)
                    EditorUtility.DisplayProgressBar("Script Auditor", "Scan prefab: " + Path.GetFileName(path), (float)i / guids.Length);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null) sc.ScanHierarchy(root, path);
            }
        }

        public static string BuildReport(List<AuditIssue> issues)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Union Project Auditor Report - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Unity " + Application.unityVersion + " | " + LastSummary);
            sb.AppendLine("Error: " + issues.Count(i => i.severity == Sev.Error) +
                          " | Warning: " + issues.Count(i => i.severity == Sev.Warning) +
                          " | Info: " + issues.Count(i => i.severity == Sev.Info));
            sb.AppendLine(new string('-', 80));
            foreach (var i in issues)
            {
                sb.AppendLine("[" + i.severity + "] [" + i.category + "] " + i.message);
                if (!string.IsNullOrEmpty(i.assetPath)) sb.AppendLine("    at " + i.assetPath + (i.line > 0 ? ":" + i.line : ""));
                if (!string.IsNullOrEmpty(i.objectPath)) sb.AppendLine("    obj " + i.objectPath);
                if (!string.IsNullOrEmpty(i.detail)) sb.AppendLine("    > " + i.detail);
            }
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------ PRE-BUILD HOOK
    public class AuditPreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return -100; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            var o = AuditOptions.Load();
            if (!o.preBuildCheck) return;

            var q = o.Clone();           // quick mode: tanpa buka scene/prefab
            var issues = AuditRunner.Run(q, false);
            var errors = issues.Where(i => i.severity == Sev.Error && !AuditExclusions.Contains(i)).ToList();

            foreach (var e in errors)
                Debug.LogError("[UnionAuditor] [" + e.category + "] " + e.message +
                               (string.IsNullOrEmpty(e.assetPath) ? "" : "  (" + e.assetPath + (e.line > 0 ? ":" + e.line : "") + ")"));

            if (errors.Count > 0 && o.preBuildAbort)
                throw new BuildFailedException("[UnionAuditor] " + errors.Count + " error terdeteksi sebelum build. Lihat Console / Script Auditor window.");
        }
    }

    // ------------------------------------------------------------ BACKUP / BIN
    [Serializable] public class BuildSceneData { public string path; public bool enabled; }

    [Serializable]
    public class BackupEntry
    {
        public string id;
        public string time;
        public string description;
        public List<string> files = new List<string>();
        public bool hasBuildScenes;
        public List<BuildSceneData> buildScenes = new List<BuildSceneData>();
    }

    [Serializable] public class AuditIssueList { public List<AuditIssue> items = new List<AuditIssue>(); }

    // Snapshot file sebelum Fix. Disimpan di UserSettings/ (di luar Assets & Library, biasanya tidak ikut Git).
    public static class BackupStore
    {
        const int MaxEntries = 50;
        public static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }
        public static string RootPath { get { return Path.Combine(ProjectRoot, "UserSettings", "ScriptAuditorBackups"); } }

        public static BackupEntry Create(string description, IEnumerable<string> files, bool includeBuildScenes)
        {
            var e = new BackupEntry
            {
                id = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"),
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                description = description,
                hasBuildScenes = includeBuildScenes
            };
            string dir = Path.Combine(RootPath, e.id);
            Directory.CreateDirectory(dir);

            if (files != null)
            {
                foreach (var f in files.Select(x => AuditUtil.Norm(x)).Distinct())
                {
                    string src = Path.Combine(ProjectRoot, f);
                    if (!File.Exists(src)) continue;
                    string dst = Path.Combine(dir, "files", f);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.Copy(src, dst, true);
                    e.files.Add(f);
                }
            }
            if (includeBuildScenes)
                foreach (var s in EditorBuildSettings.scenes)
                    e.buildScenes.Add(new BuildSceneData { path = s.path, enabled = s.enabled });

            File.WriteAllText(Path.Combine(dir, "manifest.json"), JsonUtility.ToJson(e, true));
            Prune();
            return e;
        }

        public static List<BackupEntry> List()
        {
            var res = new List<BackupEntry>();
            if (!Directory.Exists(RootPath)) return res;
            foreach (var d in Directory.GetDirectories(RootPath))
            {
                string m = Path.Combine(d, "manifest.json");
                if (!File.Exists(m)) continue;
                try { var e = JsonUtility.FromJson<BackupEntry>(File.ReadAllText(m)); if (e != null) res.Add(e); }
                catch { }
            }
            return res.OrderByDescending(x => x.id).ToList();
        }

        // Restore juga membuat backup dari kondisi SAAT INI dulu, jadi restore bisa dibatalkan.
        public static void Restore(BackupEntry e)
        {
            Create("Sebelum restore: " + e.description, e.files, e.hasBuildScenes);

            foreach (var rel in e.files)
            {
                string src = Path.Combine(RootPath, e.id, "files", rel);
                string dst = Path.Combine(ProjectRoot, rel);
                if (!File.Exists(src)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst, true);
            }
            if (e.hasBuildScenes)
                EditorBuildSettings.scenes = e.buildScenes.Select(b => new EditorBuildSettingsScene(b.path, b.enabled)).ToArray();

            AssetDatabase.Refresh();
        }

        public static void Delete(BackupEntry e)
        {
            string dir = Path.Combine(RootPath, e.id);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        public static void Clear()
        {
            if (Directory.Exists(RootPath)) Directory.Delete(RootPath, true);
        }

        static void Prune()
        {
            var all = List();
            for (int i = MaxEntries; i < all.Count; i++) Delete(all[i]);
        }
    }

    // ------------------------------------------------------------ EXCLUDED STORE
    // Disimpan per-project di UserSettings/ScriptAuditorExcluded.json
    static class AuditExclusions
    {
        static List<AuditIssue> items;
        static HashSet<string> keys;

        static string FilePath { get { return Path.Combine(BackupStore.ProjectRoot, "UserSettings", "ScriptAuditorExcluded.json"); } }

        static void Ensure()
        {
            if (items != null) return;
            items = new List<AuditIssue>();
            try
            {
                if (File.Exists(FilePath))
                {
                    var w = JsonUtility.FromJson<AuditIssueList>(File.ReadAllText(FilePath));
                    if (w != null && w.items != null) items = w.items;
                }
            }
            catch { }
            keys = new HashSet<string>(items.Select(i => i.Key()));
        }

        static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(new AuditIssueList { items = items }, true));
        }

        static AuditIssue Copy(AuditIssue i) { return JsonUtility.FromJson<AuditIssue>(JsonUtility.ToJson(i)); }

        public static List<AuditIssue> Items { get { Ensure(); return items; } }
        public static bool Contains(AuditIssue i) { Ensure(); return keys.Contains(i.Key()); }

        public static void Add(AuditIssue i)
        {
            Ensure();
            if (!keys.Add(i.Key())) return;
            items.Add(Copy(i));
            Save();
        }

        public static void Remove(string key)
        {
            Ensure();
            items.RemoveAll(x => x.Key() == key);
            keys.Remove(key);
            Save();
        }

        // Perbarui salinan (mis. nomor baris) dengan hasil scan terbaru.
        public static void Refresh(IEnumerable<AuditIssue> scan)
        {
            Ensure();
            if (items.Count == 0) return;
            var fresh = new Dictionary<string, AuditIssue>();
            foreach (var i in scan) { string k = i.Key(); if (!fresh.ContainsKey(k)) fresh[k] = i; }
            for (int n = 0; n < items.Count; n++)
            {
                AuditIssue f;
                if (fresh.TryGetValue(items[n].Key(), out f)) items[n] = Copy(f);
            }
            Save();
        }
    }

    // ------------------------------------------------------------------ FIX ENGINE
    static class FixEngine
    {
        public static string Describe(AuditIssue it)
        {
            switch (it.fix)
            {
                case "enableScene": return "Centang scene '" + it.fixArg1 + "' di Build Settings.";
                case "removeBuildScene": return "Hapus entri scene yang file-nya tidak ada dari Build Settings: " + it.fixArg1;
                case "replaceLiteral": return "Ganti \"" + it.fixArg1 + "\" menjadi \"" + it.fixArg2 + "\" pada baris ini.";
                case "removeMissingScripts": return "Hapus semua component Missing Script dari GameObject '" + it.objectPath + "'.";
                case "remapGuid": return "Ganti GUID lama dengan GUID aset tujuan di semua file YAML yang memakainya (ada konfirmasi + backup).";
                case "relinkScript": return "Re-link m_Script asset ini (GUID " + (it.fixArg1 ?? "").Substring(0, Math.Min(8, (it.fixArg1 ?? "").Length)) + "...) ke script: " + it.fixArg2;
            }
            return "";
        }

        static bool Confirm(string what)
        {
            return EditorUtility.DisplayDialog("Script Auditor - Fix",
                what + "\n\nBackup otomatis dibuat dulu (lihat 'Backups / Bin') sehingga bisa di-restore.\n" +
                "Untuk perubahan banyak, sebaiknya commit ke Git dulu.", "Fix", "Batal");
        }

        public static bool Apply(AuditIssue it, out string result)
        {
            result = "";
            if (string.IsNullOrEmpty(it.fix)) { result = "Tidak ada fix otomatis untuk item ini."; return false; }
            if (it.fix != "remapGuid" && !Confirm(Describe(it) + (string.IsNullOrEmpty(it.assetPath) ? "" : "\n\n" + it.assetPath)))
            {
                result = "Dibatalkan.";
                return false;
            }
            try
            {
                switch (it.fix)
                {
                    case "enableScene": return SetSceneEnabled(it.fixArg1, out result);
                    case "removeBuildScene": return RemoveBuildScene(it.fixArg1, out result);
                    case "replaceLiteral": return ReplaceLiteral(it, out result);
                    case "removeMissingScripts": return RemoveMissing(it, out result);
                    case "remapGuid": return RemapGuid(it, out result);
                    case "relinkScript": return RelinkScript(it, out result);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = e.Message;
                return false;
            }
            result = "Jenis fix tidak dikenal: " + it.fix;
            return false;
        }

        static bool SetSceneEnabled(string path, out string result)
        {
            var arr = EditorBuildSettings.scenes;
            if (!arr.Any(s => AuditUtil.Norm(s.path) == path)) { result = "Scene tidak ada di Build Settings: " + path; return false; }
            BackupStore.Create("Build Settings: aktifkan " + path, null, true);
            foreach (var s in arr) if (AuditUtil.Norm(s.path) == path) s.enabled = true;
            EditorBuildSettings.scenes = arr;
            result = "Scene diaktifkan di Build Settings: " + path;
            return true;
        }

        static bool RemoveBuildScene(string path, out string result)
        {
            var arr = EditorBuildSettings.scenes;
            if (!arr.Any(s => AuditUtil.Norm(s.path) == path)) { result = "Entri sudah tidak ada: " + path; return false; }
            BackupStore.Create("Build Settings: hapus entri " + path, null, true);
            EditorBuildSettings.scenes = arr.Where(s => AuditUtil.Norm(s.path) != path).ToArray();
            result = "Entri dihapus dari Build Settings: " + path;
            return true;
        }

        static bool ReplaceLiteral(AuditIssue it, out string result)
        {
            string file = it.assetPath;
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) { result = "File tidak ditemukan: " + file; return false; }

            string oldLit = "\"" + it.fixArg1 + "\"";
            string newLit = "\"" + it.fixArg2 + "\"";
            string text = File.ReadAllText(file);

            // cari awal baris ke-(line)
            int idx = it.line - 1, start = 0;
            for (int n = 0; n < idx; n++)
            {
                int nl = text.IndexOf('\n', start);
                if (nl < 0) { result = "Baris sudah berubah. Jalankan audit ulang."; return false; }
                start = nl + 1;
            }
            int end = text.IndexOf('\n', start);
            if (end < 0) end = text.Length;
            string lineTxt = text.Substring(start, end - start);
            if (!lineTxt.Contains(oldLit)) { result = "Baris sudah berubah. Jalankan audit ulang."; return false; }

            BackupStore.Create("Ganti " + oldLit + " -> " + newLit + " di " + file + ":" + it.line, new[] { file }, false);
            text = text.Substring(0, start) + lineTxt.Replace(oldLit, newLit) + text.Substring(end);
            File.WriteAllText(file, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(file);
            result = "Diganti pada " + file + ":" + it.line;
            return true;
        }

        static List<string> FilesWithGuid(string guid)
        {
            var res = new List<string>();
            foreach (var p in AssetDatabase.GetAllAssetPaths())
            {
                if (!p.StartsWith("Assets/", StringComparison.Ordinal) || !AssetScanner.IsYaml(p)) continue;
                try { if (File.ReadAllText(p).Contains(guid)) res.Add(p); } catch { }
            }
            return res;
        }

        // Ganti GUID lama -> GUID aset tujuan di SEMUA file YAML yang memakainya (satu GUID = satu operasi).
        static bool RemapGuid(AuditIssue it, out string result)
        {
            string oldG = it.fixArg1, newG = it.fixArg2;
            if (string.IsNullOrEmpty(oldG) || string.IsNullOrEmpty(newG)) { result = "GUID lama/baru tidak valid."; return false; }
            string target = AssetDatabase.GUIDToAssetPath(newG);
            if (string.IsNullOrEmpty(target)) { result = "Aset tujuan tidak ditemukan."; return false; }

            var files = FilesWithGuid(oldG);
            if (files.Count == 0) { result = "Tidak ada file yang memakai GUID lama (mungkin sudah diperbaiki)."; return false; }

            if (!EditorUtility.DisplayDialog("Script Auditor - Fix",
                    "Ganti GUID " + oldG.Substring(0, Math.Min(8, oldG.Length)) + "... menjadi " + newG.Substring(0, 8) + "...\nTujuan: " + target +
                    "\n\nAkan mengubah " + files.Count + " file:\n" + string.Join("\n", files.Take(8).ToArray()) + (files.Count > 8 ? "\n..." : "") +
                    "\n\nBackup otomatis dibuat dulu (lihat 'Backups / Bin').\nSimpan/tutup scene yang sedang diedit dulu. Disarankan commit ke Git.",
                    "Fix", "Batal")) { result = "Dibatalkan."; return false; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { result = "Dibatalkan."; return false; }

            BackupStore.Create("Remap GUID " + oldG.Substring(0, Math.Min(8, oldG.Length)) + " -> " + newG.Substring(0, 8) + " (" + target + ", " + files.Count + " file)", files, false);
            foreach (var f in files)
            {
                string text = File.ReadAllText(f);
                text = text.Replace("guid: " + oldG, "guid: " + newG);
                File.WriteAllText(f, text, new UTF8Encoding(false));
            }
            AssetDatabase.Refresh();
            result = files.Count + " file di-remap ke " + target;
            return true;
        }

        static bool RelinkScript(AuditIssue it, out string result)
        {
            string file = it.assetPath, oldGuid = it.fixArg1, scriptPath = it.fixArg2;
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) { result = "Asset tidak ditemukan: " + file; return false; }
            string newGuid = AssetDatabase.AssetPathToGUID(scriptPath);
            if (string.IsNullOrEmpty(newGuid)) { result = "Script tujuan tidak valid: " + scriptPath; return false; }

            string text = File.ReadAllText(file);
            var rx = new Regex(@"(m_Script:\s*\{fileID:\s*)-?\d+(,\s*guid:\s*)" + Regex.Escape(oldGuid) + @"(,\s*type:\s*3\})");
            if (!rx.IsMatch(text)) { result = "m_Script lama tidak ditemukan lagi di file. Jalankan audit ulang."; return false; }

            BackupStore.Create("Re-link script " + Path.GetFileName(file) + " -> " + Path.GetFileName(scriptPath), new[] { file }, false);
            text = rx.Replace(text, "${1}11500000${2}" + newGuid + "${3}");
            File.WriteAllText(file, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(file);
            result = "Asset di-link ke " + scriptPath;
            return true;
        }

        static bool RemoveMissing(AuditIssue it, out string result)
        {
            string path = it.assetPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { result = "Aset tidak ditemukan: " + path; return false; }
            int removed = 0;

            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { result = "Dibatalkan."; return false; }
                var setup = EditorSceneManager.GetSceneManagerSetup();
                try
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var go = AuditUtil.FindInRoots(scene.GetRootGameObjects(), it.objectPath);
                    if (go == null) { result = "Objek tidak ditemukan: " + it.objectPath; return false; }

                    BackupStore.Create("Hapus Missing Script '" + it.objectPath + "' di " + path, new[] { path }, false);
                    removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    if (removed > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
                finally
                {
                    if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }
            else
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var go = AuditUtil.FindInRoots(new[] { root }, it.objectPath);
                    if (go == null) { result = "Objek tidak ditemukan: " + it.objectPath; return false; }

                    BackupStore.Create("Hapus Missing Script '" + it.objectPath + "' di " + path, new[] { path }, false);
                    removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    if (removed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            result = removed + " Missing Script dihapus dari '" + it.objectPath + "'.";
            return removed > 0;
        }
    }

    // -------------------------------------------------------------------- WINDOW
    public class UnionProjectAuditorWindow : EditorWindow
    {
        // ---- Branding (ubah di sini). Logo dipilih lewat Opsi > "Logo (header)".
        public const string ToolName = "Union Project Auditor";
        public const string BrandTitle = "Union Studio";
        public const string BrandDescription = "...";
        static readonly string[] TabLabels = { "Script Auditor", "Objects Auditor" };

        // Modul Objects Auditor mendaftarkan diri di sini ([InitializeOnLoad] di ObjectsAuditor.cs).
        public static Action<EditorWindow> ObjectsAuditDraw;

        sealed class Section
        {
            public bool open;
            public bool showErr = true, showWarn = true, showInfo;
            public string search = "";
            public Vector2 scroll;
        }

        AuditOptions o;
        List<AuditIssue> issues = new List<AuditIssue>();        // hasil scan terakhir
        List<AuditIssue> problems = new List<AuditIssue>();      // issues - excluded
        List<AuditIssue> excludedList = new List<AuditIssue>();  // isi store excluded
        HashSet<string> lastKeys = new HashSet<string>();
        List<BackupEntry> backups = new List<BackupEntry>();
        bool scanned, optionsFold;
        string status = "";
        int tab;
        Vector2 mainScroll, backupScroll;
        readonly Section secProblems = new Section { open = true };
        readonly Section secExcluded = new Section { open = false };
        bool backupsOpen;
        readonly Dictionary<string, bool> groupOpen = new Dictionary<string, bool>();
        readonly Dictionary<string, bool> candOpen = new Dictionary<string, bool>();
        readonly HashSet<string> selected = new HashSet<string>();
        float listHeight = 260f;
        Texture2D logoTex;
        string logoGuidCached;
        GUIStyle wrap, small, titleStyle, descStyle, toggleWrap;
        static readonly Color SelColor = new Color(0.17f, 0.36f, 0.53f, 0.65f);

        [MenuItem("Tools/Union Project Auditor/Open Window")]
        static void Open()
        {
            var w = GetWindow<UnionProjectAuditorWindow>(ToolName);
            w.minSize = new Vector2(520, 460);
        }

        [MenuItem("Tools/Union Project Auditor/Quick Audit -> Console")]
        static void QuickConsole()
        {
            var opt = AuditOptions.Load();
            var list = AuditRunner.Run(opt, false).Where(x => !AuditExclusions.Contains(x)).ToList();
            foreach (var i in list.Where(x => x.severity == Sev.Error))
                Debug.LogError("[UnionAuditor] [" + i.category + "] " + i.message + "  (" + i.assetPath + (i.line > 0 ? ":" + i.line : "") + ")");
            foreach (var i in list.Where(x => x.severity == Sev.Warning))
                Debug.LogWarning("[UnionAuditor] [" + i.category + "] " + i.message + "  (" + i.assetPath + (i.line > 0 ? ":" + i.line : "") + ")");
            Debug.Log("[UnionAuditor] Selesai. " + AuditRunner.LastSummary);
        }

        void OnEnable()
        {
            o = AuditOptions.Load();
            backups = BackupStore.List();
            Recompute();
        }

        void OnDisable() { if (o != null) o.Save(); }

        void Recompute()
        {
            problems = issues.Where(i => !AuditExclusions.Contains(i)).ToList();
            excludedList = AuditExclusions.Items.ToList();
            lastKeys = new HashSet<string>(issues.Select(i => i.Key()));
        }

        void Run(bool full)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Script Auditor", "Hentikan Play Mode dulu.", "OK");
                return;
            }
            o.Save();
            status = "";
            try
            {
                issues = AuditRunner.Run(o, full);
                AuditExclusions.Refresh(issues);
                scanned = true;
                Recompute();
            }
            catch (Exception e) { Debug.LogException(e); status = "Audit gagal: " + e.Message; }
            finally { EditorUtility.ClearProgressBar(); }
            Repaint();
        }

        void Notify(string msg) { ShowNotification(new GUIContent(msg), 1.2); }

        // ------------------------------------------------------------ frame
        void InitStyles()
        {
            if (wrap != null) return;
            wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
            small = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            descStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            toggleWrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
        }

        Texture2D LoadLogo()
        {
            string g = o != null ? o.logoGuid : "";
            if (logoGuidCached != g) { logoGuidCached = g; logoTex = null; }
            if (logoTex == null && !string.IsNullOrEmpty(g))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!string.IsNullOrEmpty(p)) logoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            return logoTex;
        }

        void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Rect r = GUILayoutUtility.GetRect(52, 52, GUILayout.Width(52), GUILayout.Height(52));
                var tex = LoadLogo();
                if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
                else GUI.Box(r, "LOGO");

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(BrandTitle, titleStyle);
                    GUILayout.Label(BrandDescription, descStyle);
                }
            }
        }

        void DrawTabs()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                tab = GUILayout.Toolbar(tab, TabLabels, GUI.skin.button, GUI.ToolbarButtonSize.FitToContents);
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(2);
        }

        void OnGUI()
        {
            if (o == null) o = AuditOptions.Load();
            InitStyles();
            HandleKeys();
            listHeight = Mathf.Clamp(position.height * 0.5f, 170f, 700f);

            DrawHeader();
            DrawTabs();

            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            if (tab == 1)
            {
                if (ObjectsAuditDraw != null) ObjectsAuditDraw(this);
                else EditorGUILayout.HelpBox("Modul Objects Auditor belum terpasang. Letakkan ObjectsAuditor.cs di folder yang sama dengan ScriptAuditor.cs.", MessageType.Info);
            }
            else DrawScriptAudit();
            EditorGUILayout.EndScrollView();
        }

        void DrawScriptAudit()
        {
            EditorGUILayout.HelpBox("Build target aktif: " + EditorUserBuildSettings.activeBuildTarget + "  |  Unity " + Application.unityVersion +
                                    "\nCek 'Not In Player Build' mengikuti build target aktif. Switch ke Android dulu sebelum audit.", MessageType.None);

            optionsFold = EditorGUILayout.Foldout(optionsFold, "Opsi", true);
            if (optionsFold) DrawOptions();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Quick Audit (tanpa buka scene)", GUILayout.Height(28))) Run(false);
                if (GUILayout.Button("Full Audit (+ scene & prefab)", GUILayout.Height(28))) Run(true);
            }

            if (!string.IsNullOrEmpty(AuditRunner.LastSummary)) EditorGUILayout.LabelField(AuditRunner.LastSummary, small);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);

            DrawSection("Problems", problems, secProblems, false);
            DrawSection("Excluded / Ignored", excludedList, secExcluded, true);
            DrawBackups();
        }

        // ------------------------------------------------------------ copy / selection
        static string FormatIssue(AuditIssue i)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[" + i.severity + "] [" + i.category + "] " + i.message);
            if (!string.IsNullOrEmpty(i.assetPath)) sb.AppendLine("    at " + i.assetPath + (i.line > 0 ? ":" + i.line : ""));
            if (!string.IsNullOrEmpty(i.objectPath)) sb.AppendLine("    obj " + i.objectPath);
            if (!string.IsNullOrEmpty(i.detail)) sb.AppendLine("    > " + i.detail);
            if (!string.IsNullOrEmpty(i.info)) sb.AppendLine("    " + i.info);
            return sb.ToString().TrimEnd();
        }

        static string FormatBackup(BackupEntry b)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Backup] " + b.description);
            sb.AppendLine("    " + b.time + "  |  " + b.files.Count + " file" + (b.hasBuildScenes ? " + Build Settings" : ""));
            foreach (var f in b.files) sb.AppendLine("    " + f);
            return sb.ToString().TrimEnd();
        }

        void HandleKeys()
        {
            var e = Event.current;
            if (tab != 0 || selected.Count == 0) return;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.C && (e.control || e.command) && !EditorGUIUtility.editingTextField)
            {
                var sb = new StringBuilder();
                int n = 0;
                foreach (var i in problems) if (selected.Contains("p:" + i.Key())) { sb.AppendLine(FormatIssue(i)); n++; }
                foreach (var i in excludedList) if (selected.Contains("x:" + i.Key())) { sb.AppendLine(FormatIssue(i)); n++; }
                foreach (var b in backups) if (selected.Contains("b:" + b.id)) { sb.AppendLine(FormatBackup(b)); n++; }
                if (n > 0)
                {
                    EditorGUIUtility.systemCopyBuffer = sb.ToString().TrimEnd();
                    Notify("Tersalin: " + n + " item");
                }
                e.Use();
            }
        }

        // klik = pilih (biru), Ctrl+klik = tambah/hapus pilihan
        void RowClick(Rect rect, string id)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !rect.Contains(e.mousePosition)) return;
            if (e.control || e.command) { if (!selected.Add(id)) selected.Remove(id); }
            else { selected.Clear(); selected.Add(id); }
            GUI.FocusControl(null);
            e.Use();
            Repaint();
        }

        static GUIContent CopyIcon()
        {
            var c = EditorGUIUtility.IconContent("TreeEditor.Duplicate");
            if (c != null && c.image != null) return new GUIContent(c.image, "Salin item ini");
            return new GUIContent("Cp", "Salin item ini");
        }

        // ------------------------------------------------------------ sections
        static bool Pass(AuditIssue i, Section st)
        {
            if (i.severity == Sev.Error && !st.showErr) return false;
            if (i.severity == Sev.Warning && !st.showWarn) return false;
            if (i.severity == Sev.Info && !st.showInfo) return false;
            if (string.IsNullOrEmpty(st.search)) return true;
            string s = st.search.ToLowerInvariant();
            return (i.message ?? "").ToLowerInvariant().Contains(s) ||
                   (i.category ?? "").ToLowerInvariant().Contains(s) ||
                   (i.assetPath ?? "").ToLowerInvariant().Contains(s) ||
                   (i.objectPath ?? "").ToLowerInvariant().Contains(s) ||
                   (i.detail ?? "").ToLowerInvariant().Contains(s);
        }

        void DrawSection(string title, List<AuditIssue> list, Section st, bool isExcluded)
        {
            st.open = EditorGUILayout.BeginFoldoutHeaderGroup(st.open, title + " (" + list.Count + ")");
            if (st.open)
            {
                int ec = list.Count(i => i.severity == Sev.Error);
                int wc = list.Count(i => i.severity == Sev.Warning);
                int ic = list.Count(i => i.severity == Sev.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    st.showErr = GUILayout.Toggle(st.showErr, "Error (" + ec + ")", "Button", GUILayout.Width(95));
                    st.showWarn = GUILayout.Toggle(st.showWarn, "Warning (" + wc + ")", "Button", GUILayout.Width(105));
                    st.showInfo = GUILayout.Toggle(st.showInfo, "Info (" + ic + ")", "Button", GUILayout.Width(85));
                    st.search = EditorGUILayout.TextField(st.search, EditorStyles.toolbarSearchField);
                    if (GUILayout.Button("Copy", GUILayout.Width(50)))
                    {
                        EditorGUIUtility.systemCopyBuffer = AuditRunner.BuildReport(list);
                        Notify("Tersalin: " + list.Count + " item");
                    }
                    if (!isExcluded && GUILayout.Button("Export", GUILayout.Width(55))) Export(list);
                }

                var shown = list.Where(i => Pass(i, st)).Take(300).ToList();
                st.scroll = EditorGUILayout.BeginScrollView(st.scroll, GUILayout.Height(listHeight));
                foreach (var it in shown.Where(i => string.IsNullOrEmpty(i.group))) DrawIssue(it, isExcluded);
                foreach (var g in shown.Where(i => !string.IsNullOrEmpty(i.group)).GroupBy(i => i.group).ToList())
                {
                    string gk = (isExcluded ? "x|" : "p|") + g.Key;
                    bool gopen;
                    groupOpen.TryGetValue(gk, out gopen);
                    gopen = EditorGUILayout.Foldout(gopen, g.Key + "  (" + g.Count() + ")", true);
                    groupOpen[gk] = gopen;
                    if (!gopen) continue;
                    EditorGUI.indentLevel++;
                    foreach (var it in g) DrawIssue(it, isExcluded);
                    EditorGUI.indentLevel--;
                }
                if (shown.Count == 0)
                    EditorGUILayout.LabelField(list.Count == 0 ? (isExcluded ? "Belum ada yang di-exclude." : (scanned ? "Tidak ada masalah." : "Belum ada hasil. Jalankan audit.")) : "Tidak ada hasil untuk filter ini.", small);
                if (shown.Count >= 300) EditorGUILayout.LabelField("Menampilkan 300 pertama. Pakai filter/search atau Export.", small);
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DrawIssue(AuditIssue it, bool isExcluded)
        {
            string sid = (isExcluded ? "x:" : "p:") + it.Key();
            bool isSel = selected.Contains(sid);
            Rect rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (Event.current.type == EventType.Repaint && isSel) EditorGUI.DrawRect(rect, SelColor);

            using (new EditorGUILayout.HorizontalScope())
            {
                string icon = it.severity == Sev.Error ? "console.erroricon.sml" :
                              it.severity == Sev.Warning ? "console.warnicon.sml" : "console.infoicon.sml";
                GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(20), GUILayout.Height(18));
                GUILayout.Label("[" + it.category + "]", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
                if (isExcluded && scanned && !lastKeys.Contains(it.Key()))
                    GUILayout.Label("(tidak muncul di scan terakhir)", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();

                if (!isExcluded && it.CanFix &&
                    GUILayout.Button(new GUIContent("Fix", FixEngine.Describe(it)), GUILayout.Width(40)))
                {
                    var cap = it;
                    EditorApplication.delayCall += () => ApplyFix(cap);
                }

                bool on = GUILayout.Toggle(isExcluded,
                    new GUIContent("Ignore", isExcluded ? "Hilangkan centang untuk mengembalikan ke Problems" : "Centang untuk memindahkan ke Excluded / Ignored"),
                    GUILayout.Width(62));
                if (on != isExcluded)
                {
                    var cap = it; bool toExcluded = on;
                    EditorApplication.delayCall += () => SetExcluded(cap, toExcluded);
                }

                if (!string.IsNullOrEmpty(it.assetPath) && GUILayout.Button("Go", GUILayout.Width(40))) Navigate(it);
            }
            EditorGUILayout.LabelField(it.message, wrap);

            if (!string.IsNullOrEmpty(it.detail)) EditorGUILayout.LabelField("> " + it.detail, small);
            if (!string.IsNullOrEmpty(it.info)) EditorGUILayout.LabelField(it.info, small);

            if (!isExcluded && (it.category == "Missing Script (Asset)" || it.category == "Missing Script (GUID)" || it.category == "Broken Reference (GUID)"))
            {
                if (it.candidates != null && it.candidates.Count > 0)
                {
                    string ck = it.Key();
                    bool co;
                    candOpen.TryGetValue(ck, out co);
                    co = EditorGUILayout.Foldout(co, "Kandidat (" + it.candidates.Count + ")", true);
                    candOpen[ck] = co;
                    if (co)
                    {
                        foreach (var cd in it.candidates)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Space(14);
                                EditorGUILayout.LabelField(cd.label, small);
                                if (GUILayout.Button(new GUIContent("Pakai", "Remap GUID lama ke aset ini di semua file yang memakainya"), GUILayout.Width(50)))
                                {
                                    var parent = it; var cand = cd;
                                    EditorApplication.delayCall += () => ApplyCandidate(parent, cand);
                                }
                            }
                        }
                    }
                }
                else EditorGUILayout.LabelField("Tidak ada kandidat otomatis. Isi 'Salinan project asli' di Opsi, atau perbaiki manual.", small);
            }

            // baris bawah: lokasi (kiri) + tombol copy kecil (kanan bawah)
            using (new EditorGUILayout.HorizontalScope())
            {
                string loc = it.assetPath ?? "";
                if (it.line > 0) loc += ":" + it.line;
                if (!string.IsNullOrEmpty(it.objectPath)) loc += "   >   " + it.objectPath;
                EditorGUILayout.LabelField(loc, small);
                if (GUILayout.Button(CopyIcon(), EditorStyles.miniButton, GUILayout.Width(26), GUILayout.Height(18)))
                {
                    EditorGUIUtility.systemCopyBuffer = FormatIssue(it);
                    Notify("Tersalin: 1 item");
                }
            }
            EditorGUILayout.EndVertical();
            RowClick(rect, sid);
        }

        void SetExcluded(AuditIssue it, bool toExcluded)
        {
            if (toExcluded) AuditExclusions.Add(it);
            else AuditExclusions.Remove(it.Key());
            Recompute();
            Repaint();
        }

        void ApplyFix(AuditIssue it)
        {
            string res;
            bool ok = FixEngine.Apply(it, out res);
            status = (ok ? "Fix OK: " : "Fix tidak dijalankan: ") + res + (ok ? "  (jalankan audit ulang untuk memastikan)" : "");
            if (ok)
            {
                string k = it.Key();
                issues.RemoveAll(i => i.Key() == k);
                Recompute();
                backups = BackupStore.List();
            }
            Repaint();
        }

        void ApplyCandidate(AuditIssue parent, AuditCandidate c)
        {
            var fx = new AuditIssue
            {
                fix = "remapGuid", assetPath = parent.assetPath, objectPath = parent.objectPath,
                fixArg1 = parent.fixArg1, fixArg2 = AssetDatabase.AssetPathToGUID(c.scriptPath)
            };
            string res;
            bool ok = FixEngine.Apply(fx, out res);
            status = (ok ? "Fix OK: " : "Fix tidak dijalankan: ") + res + (ok ? "  (jalankan audit ulang untuk memastikan)" : "");
            if (ok)
            {
                string k = parent.Key();
                issues.RemoveAll(i => i.Key() == k);
                Recompute();
                backups = BackupStore.List();
            }
            Repaint();
        }

        // ------------------------------------------------------------ backups / bin
        void DrawBackups()
        {
            backupsOpen = EditorGUILayout.BeginFoldoutHeaderGroup(backupsOpen, "Backups / Bin (" + backups.Count + ")");
            if (backupsOpen)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh")) backups = BackupStore.List();
                    if (GUILayout.Button("Buka folder"))
                    {
                        Directory.CreateDirectory(BackupStore.RootPath);
                        EditorUtility.RevealInFinder(BackupStore.RootPath);
                    }
                    if (GUILayout.Button("Kosongkan Bin")) EditorApplication.delayCall += ClearBin;
                }

                backupScroll = EditorGUILayout.BeginScrollView(backupScroll, GUILayout.Height(listHeight * 0.8f));
                if (backups.Count == 0) EditorGUILayout.LabelField("Belum ada backup. Backup dibuat otomatis setiap kali Fix dijalankan.", small);
                foreach (var b in backups)
                {
                    string sid = "b:" + b.id;
                    bool isSel = selected.Contains(sid);
                    Rect rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    if (Event.current.type == EventType.Repaint && isSel) EditorGUI.DrawRect(rect, SelColor);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(b.description, wrap);
                        if (GUILayout.Button("Restore", GUILayout.Width(65)))
                        {
                            var cap = b;
                            EditorApplication.delayCall += () => DoRestore(cap);
                        }
                        if (GUILayout.Button("Hapus", GUILayout.Width(50)))
                        {
                            var cap = b;
                            EditorApplication.delayCall += () => { BackupStore.Delete(cap); backups = BackupStore.List(); Repaint(); };
                        }
                    }
                    foreach (var f in b.files.Take(4)) EditorGUILayout.LabelField("   " + f, small);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(b.time + "  |  " + b.files.Count + " file" + (b.hasBuildScenes ? " + Build Settings" : ""), small);
                        if (GUILayout.Button(CopyIcon(), EditorStyles.miniButton, GUILayout.Width(26), GUILayout.Height(18)))
                        {
                            EditorGUIUtility.systemCopyBuffer = FormatBackup(b);
                            Notify("Tersalin: 1 item");
                        }
                    }
                    EditorGUILayout.EndVertical();
                    RowClick(rect, sid);
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void ClearBin()
        {
            if (!EditorUtility.DisplayDialog("Kosongkan Bin", "Hapus SEMUA backup Union Project Auditor? Ini tidak bisa dibatalkan.", "Hapus semua", "Batal")) return;
            BackupStore.Clear();
            backups = BackupStore.List();
            Repaint();
        }

        void DoRestore(BackupEntry b)
        {
            string files = b.files.Count > 0 ? string.Join("\n", b.files.Take(8).ToArray()) : "(hanya Build Settings)";
            if (!EditorUtility.DisplayDialog("Restore backup",
                    "Kembalikan kondisi sebelum perubahan ini?\n\n" + b.description + "\n\n" + files +
                    "\n\nKondisi saat ini disimpan dulu sebagai backup baru, jadi restore ini juga bisa dibatalkan." +
                    "\nJika scene terkait sedang terbuka dan ada perubahan belum tersimpan, simpan/tutup dulu.",
                    "Restore", "Batal")) return;
            try
            {
                BackupStore.Restore(b);
                status = "Restore OK: " + b.description + "  (jalankan audit ulang)";
            }
            catch (Exception e) { Debug.LogException(e); status = "Restore gagal: " + e.Message; }
            backups = BackupStore.List();
            Repaint();
        }

        // ------------------------------------------------------------ options
        bool Tog(string label, bool v) { return EditorGUILayout.ToggleLeft(new GUIContent(label), v, toggleWrap); }

        void DrawOptions()
        {
            float lw = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 150;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    o.scriptsRoot = EditorGUILayout.TextField("Scripts folder", o.scriptsRoot);
                    if (GUILayout.Button("Auto", GUILayout.Width(45))) o.scriptsRoot = AuditUtil.DetectScriptsRoot();
                    if (GUILayout.Button("...", GUILayout.Width(30)))
                    {
                        string p = EditorUtility.OpenFolderPanel("Pilih folder Scripts", "Assets", "");
                        if (!string.IsNullOrEmpty(p))
                        {
                            p = AuditUtil.Norm(p);
                            string proj = AuditUtil.Norm(Directory.GetParent(Application.dataPath).FullName);
                            if (p.StartsWith(proj + "/", StringComparison.OrdinalIgnoreCase)) o.scriptsRoot = p.Substring(proj.Length + 1);
                        }
                    }
                }
                o.excludeFolders = EditorGUILayout.TextField("Exclude folders", o.excludeFolders);
                o.ignoreFieldPatterns = EditorGUILayout.TextField("Ignore fields", o.ignoreFieldPatterns);
                EditorGUILayout.LabelField("Exclude: pisah dengan ';'  |  Ignore fields: nama field atau Class.field, boleh wildcard (debug*, _opt*)", small);

                GUILayout.Space(4);
                o.sourceScan = Tog("Scan source .cs (#if, pola kode berisiko)", o.sourceScan);
                o.playerAvailability = Tog("Cek script yang dipakai scene/prefab ikut dikompilasi ke player", o.playerAvailability);
                o.buildSettings = Tog("Cek Build Settings dan LoadScene", o.buildSettings);
                o.resourcesCheck = Tog("Cek Resources.Load (huruf besar/kecil path)", o.resourcesCheck);
                o.assetScan = Tog("Scan YAML (scene, prefab, asset): script dan GUID putus", o.assetScan);
                o.metaCheck = Tog("Cek file .meta (orphan, hilang, GUID kembar)", o.metaCheck);

                GUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    o.originalProject = EditorGUILayout.TextField("Salinan project asli", o.originalProject);
                    if (GUILayout.Button("...", GUILayout.Width(30)))
                    {
                        string op = EditorUtility.OpenFolderPanel("Pilih root project lama (atau folder Assets-nya)", "", "");
                        if (!string.IsNullOrEmpty(op)) o.originalProject = op;
                    }
                }
                EditorGUILayout.LabelField("Opsional: folder hasil ekstrak project lama (punya .meta asli). Dipakai untuk menebak GUID lama -> aset sekarang.", small);

                GUILayout.Space(4);
                o.sceneScope = (SceneScope)EditorGUILayout.EnumPopup("Scan scene (Full)", o.sceneScope);
                o.scanPrefabs = Tog("Scan semua prefab (Full)", o.scanPrefabs);
                o.scanUnityEventsAll = Tog("Cek UnityEvent di semua component", o.scanUnityEventsAll);
                o.includePackages = Tog("Sertakan script dari Packages", o.includePackages);
                o.inventory = Tog("Inventaris tiap component (Info)", o.inventory);
                o.preBuildCheck = Tog("Audit otomatis sebelum build", o.preBuildCheck);
                using (new EditorGUI.DisabledScope(!o.preBuildCheck))
                    o.preBuildAbort = Tog("Batalkan build jika ada Error", o.preBuildAbort);

                GUILayout.Space(4);
                var cur = LoadLogo();
                var nl = (Texture2D)EditorGUILayout.ObjectField("Logo (header)", cur, typeof(Texture2D), false);
                if (nl != cur)
                {
                    o.logoGuid = nl != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(nl)) : "";
                    logoTex = nl;
                    logoGuidCached = o.logoGuid;
                }
            }
            EditorGUIUtility.labelWidth = lw;
        }

        // ------------------------------------------------------------ navigation / export
        static void Navigate(AuditIssue it)
        {
            if (string.IsNullOrEmpty(it.assetPath)) return;
            var obj = AssetDatabase.LoadAssetAtPath<Object>(it.assetPath);
            if (obj == null) return;

            if (it.assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.OpenAsset(obj, Math.Max(it.line, 1));
                return;
            }

            if (it.assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                var scene = SceneManager.GetSceneByPath(it.assetPath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    scene = EditorSceneManager.OpenScene(it.assetPath, OpenSceneMode.Single);
                }
                var go = AuditUtil.FindInRoots(scene.GetRootGameObjects(), it.objectPath);
                if (go != null) { Selection.activeGameObject = go; EditorGUIUtility.PingObject(go); }
                else { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
                return;
            }

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        void Export(List<AuditIssue> list)
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "AuditReports");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "ScriptAudit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(file, AuditRunner.BuildReport(list), Encoding.UTF8);
            EditorUtility.RevealInFinder(file);
        }
    }
}
