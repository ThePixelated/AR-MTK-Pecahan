using System.Collections.Generic;
using UnityEngine;

// Menghubungkan Media3Session (data) ke LiquidVessel (tampilan): level isi, warna kontinu menurut rasio sirup, garis bagian.
// Bagian "Uji" untuk mencoba tanpa UI; di tahap 3c Media3Controller yang membuat dan meng-Bind session.
public class Media3VisualBinder : LiquidBinderBase
{
    [SerializeField] LiquidStyle style;
    [SerializeField] LiquidVessel vesselSyrup, vesselWater, vesselWadah;
    [SerializeField] float fillSpeed = 1f;       // level mengikuti data (rasio per detik)
    [SerializeField] float colorSpeed = 2f;      // warna mengikuti data (rasio sirup per detik)

    [Header("Uji (sementara)")]
    [SerializeField] bool createTestSession = true;
    [SerializeField, Range(1, 10)] int testSyrup = 3;
    [SerializeField, Range(1, 10)] int testWater = 5;

    class Shown
    {
        public float fill, ratio, targetRatio;
        public bool wasEmpty = true;
        public int divisions = -1;
    }

    Media3Session session;
    readonly List<LiquidContainer> containers = new List<LiquidContainer>();
    readonly List<LiquidVessel> vessels = new List<LiquidVessel>();
    readonly Dictionary<LiquidVessel, Shown> shown = new Dictionary<LiquidVessel, Shown>();
    float pourWindowEnd = -1f;

    public Media3Session Session => session;
    public override IPourSession PourSession => session;

    // Selama jendela ini, level dan warna bergerak merata sampai tepat selesai di akhir durasi tuang
    public override void BeginPourWindow(float seconds) => pourWindowEnd = Time.time + seconds;

    public override bool TryGetContainer(LiquidVessel v, out LiquidContainer c)
    {
        for (int i = 0; i < vessels.Count; i++)
            if (vessels[i] == v) { c = containers[i]; return true; }
        c = null;
        return false;
    }

    void Start()
    {
        if (!createTestSession) return;
        Bind(new Media3Session());
        session.ApplyConfig(testSyrup, testWater);
    }

    void OnDestroy() => Unbind();

    public void Bind(Media3Session s)
    {
        Unbind();
        session = s;
        session.Changed += Refresh;
        Refresh();
    }

    void Unbind()
    {
        if (session == null) return;
        session.Changed -= Refresh;
        session = null;
    }

    Shown GetShown(LiquidVessel v)
    {
        if (!shown.TryGetValue(v, out Shown s)) { s = new Shown(); shown[v] = s; }
        return s;
    }

    void Add(LiquidContainer c, LiquidVessel v) { containers.Add(c); vessels.Add(v); }

    static float Ratio(Mixture m)
    {
        Fraction c = m.Concentration;
        return c.denominator == 0 ? 0f : (float)c.numerator / c.denominator;
    }

    void Refresh()
    {
        containers.Clear();
        vessels.Clear();
        Add(session.Syrup, vesselSyrup);
        Add(session.Water, vesselWater);
        Add(session.W, vesselWadah);

        int max = session.Limits.MaxPerCup;
        for (int i = 0; i < containers.Count; i++)
        {
            LiquidVessel v = vessels[i];
            if (v == null) continue;
            LiquidContainer c = containers[i];
            Shown s = GetShown(v);

            // gelas: MaxPerCup bagian; wadah: 2 x MaxPerCup bagian; garis tiap 1 bagian. Dibangun ulang hanya kalau berubah.
            int divisions = c == session.W ? 2 * max : max;
            if (s.divisions != divisions)
            {
                v.ConfigureScale(divisions, 1);
                s.divisions = divisions;
            }

            if (!c.IsEmpty)
            {
                s.targetRatio = Ratio(c.Content);
                if (s.wasEmpty) s.ratio = s.targetRatio;     // cairan baru masuk: warna langsung benar, tanpa transisi dari sisa warna lama
            }
            s.wasEmpty = c.IsEmpty;
        }
    }

    void Update()
    {
        float remaining = pourWindowEnd - Time.time;
        bool inWindow = remaining > 0f;
        float window = Mathf.Max(remaining, 0.05f);
        float dt = Time.deltaTime;

        for (int i = 0; i < vessels.Count; i++)
        {
            LiquidVessel v = vessels[i];
            if (!v) continue;
            Shown s = GetShown(v);

            float targetFill = containers[i].FillRatio;
            float fillStep = inWindow ? Mathf.Abs(targetFill - s.fill) / window : fillSpeed;
            s.fill = Mathf.MoveTowards(s.fill, targetFill, fillStep * dt);
            v.Fill = s.fill;

            float ratioStep = inWindow ? Mathf.Abs(s.targetRatio - s.ratio) / window : colorSpeed;
            s.ratio = Mathf.MoveTowards(s.ratio, s.targetRatio, ratioStep * dt);
            if (style != null) v.LiquidColor = style.ColorForRatio(s.ratio);
        }
    }

    // ---- menu uji (klik kanan komponen saat Play) ----
    void EnsureSession() { if (session == null) Bind(new Media3Session()); }

    [ContextMenu("Uji: terapkan konfigurasi")]
    void TestApply()
    {
        EnsureSession();
        Media3ConfigError e = session.ApplyConfig(testSyrup, testWater);
        if (e != Media3ConfigError.None) Debug.Log($"[Media3Uji] konfigurasi ditolak: {e}", this);
    }

    [ContextMenu("Uji: Input (catat entri)")]
    void TestInput()
    {
        EnsureSession();
        Media3InputResult r = session.TryInput();
        Debug.Log($"[Media3Uji] input: {r}, jumlah entri = {session.Entries.Count}", this);
    }

    [ContextMenu("Uji: reset semua")] void TestResetAll() { EnsureSession(); session.ResetAll(); }

    [ContextMenu("Uji: cetak entri")]
    void TestPrint()
    {
        EnsureSession();
        for (int i = 0; i < session.Entries.Count; i++)
        {
            Media3Entry e = session.Entries[i];
            Debug.Log($"[Media3Uji] percobaan {i + 1}: sirup {e.syrupSections}, air {e.waterSections}", this);
        }
    }
}