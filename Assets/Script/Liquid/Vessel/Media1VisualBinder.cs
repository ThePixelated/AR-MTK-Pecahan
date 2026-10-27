using System.Collections.Generic;
using UnityEngine;

// Menghubungkan Media1Session (data) ke LiquidVessel (tampilan): level isi, warna per tingkat encer, garis penyebut.
// Bagian "Uji" untuk mencoba tanpa UI; digantikan controller Media 1 di tahap 1d.
public class Media1VisualBinder : LiquidBinderBase
{
    [SerializeField] LiquidStyle style;
    [SerializeField] LiquidVessel vesselA, vesselB, vesselW;
    [SerializeField] LiquidVessel[] finalCupVessels;      // disiapkan di scene (nonaktif); diaktifkan sesuai jumlah gelas akhir
    [SerializeField] float fillSpeed = 1f;                // level mengikuti data (rasio per detik)

    [Header("Uji (sementara)")]
    [SerializeField] bool createTestSession = true;
    [SerializeField, Range(2, 10)] int testDenominator = 6;
    [SerializeField, Range(1, 10)] int testWater = 3;
    [SerializeField, Range(1, 10)] int testSyrup = 5;
    [SerializeField, Range(1, 5)] int testPrediction = 2;

    Media1Session session;
    readonly List<LiquidContainer> containers = new List<LiquidContainer>();
    readonly List<LiquidVessel> vessels = new List<LiquidVessel>();
    readonly Dictionary<LiquidVessel, float> shown = new Dictionary<LiquidVessel, float>();
    float pourWindowEnd = -1f;

    public Media1Session Session => session;
    public override IPourSession PourSession => session;

    // 3) tambahkan override
    //public override void BeginPourWindow(float seconds) => pourWindowEnd = Time.time + seconds;

    // 4) tambahkan override
    //public override bool TryGetContainer(LiquidVessel v, out LiquidContainer c)

    // Selama jendela ini, level bergerak merata sampai tepat selesai di akhir durasi tuang
    public override void BeginPourWindow(float seconds) => pourWindowEnd = Time.time + seconds;

    public override bool TryGetContainer(LiquidVessel v, out LiquidContainer c)
    {
        for (int i = 0; i < vessels.Count; i++)
            if (vessels[i] == v) { c = containers[i]; return true; }
        c = null;
        return false;
    }

    void Start() { if (createTestSession) Bind(new Media1Session()); }
    void OnDestroy() => Unbind();

    public void Bind(Media1Session s)
    {
        Unbind();
        session = s;
        session.Changed += Refresh;
        session.FinalCupsChanged += Refresh;
        Refresh();
    }

    void Unbind()
    {
        if (session == null) return;
        session.Changed -= Refresh;
        session.FinalCupsChanged -= Refresh;
        session = null;
    }

    void Add(LiquidContainer c, LiquidVessel v) { containers.Add(c); vessels.Add(v); }

    void Refresh()
    {
        containers.Clear();
        vessels.Clear();
        Add(session.A, vesselA);
        Add(session.B, vesselB);
        Add(session.W, vesselW);
        for (int i = 0; i < finalCupVessels.Length; i++)
        {
            bool active = i < session.FinalCups.Count;
            if (finalCupVessels[i]) finalCupVessels[i].gameObject.SetActive(active);
            if (active) Add(session.FinalCups[i], finalCupVessels[i]);
            else if (finalCupVessels[i]) shown.Remove(finalCupVessels[i]);
        }

        int levels = session.Limits.DilutionLevels;
        int p = session.IsConfigured ? session.Config.Value.denominator : 1;
        for (int i = 0; i < containers.Count; i++)
        {
            LiquidVessel v = vessels[i];
            if (v == null) continue;
            LiquidContainer c = containers[i];

            bool isW = c == session.W;
            v.ConfigureScale(isW ? (int)Mathf.Max(1, c.CapacitySections) : p, isW ? p : 1);   // gelas: p bagian tiap 1; wadah: T bagian tiap p
            if (style != null && !c.IsEmpty)    // wadah kosong mempertahankan warna terakhir sampai levelnya habis
                v.LiquidColor = style.ColorFor(LiquidColorLevel.Level(c.Content, levels), levels);
        }
    }

    void Update()
    {
        float remaining = pourWindowEnd - Time.time;
        for (int i = 0; i < vessels.Count; i++)
        {
            LiquidVessel v = vessels[i];
            if (!v) continue;

            shown.TryGetValue(v, out float cur);
            float target = containers[i].FillRatio;
            float speed = remaining > 0.02f ? Mathf.Abs(target - cur) / remaining : fillSpeed;
            cur = Mathf.MoveTowards(cur, target, speed * Time.deltaTime);
            shown[v] = cur;
            v.Fill = cur;
        }
    }

    // ---- menu uji (klik kanan komponen saat Play) ----
    void EnsureSession() { if (session == null) Bind(new Media1Session()); }

    void TestPour(LiquidContainer from, LiquidContainer to)
    {
        EnsureSession();
        PourError e = session.TryPour(from, to);
        if (e != PourError.None) Debug.Log($"[Media1Uji] tuang ditolak: {e}", this);
    }

    [ContextMenu("Uji: terapkan konfigurasi")]
    void TestApply()
    {
        EnsureSession();
        ConfigError e = session.ApplyConfig(testDenominator, testWater, testSyrup);
        if (e != ConfigError.None) Debug.Log($"[Media1Uji] konfigurasi ditolak: {e}", this);
    }

    [ContextMenu("Uji: tuang A (air) ke W")] void TestPourA() { EnsureSession(); TestPour(session.A, session.W); }
    [ContextMenu("Uji: tuang B (sirup) ke W")] void TestPourB() { EnsureSession(); TestPour(session.B, session.W); }

    [ContextMenu("Uji: tuang W ke gelas akhir")]
    void TestPourToCup()
    {
        EnsureSession();
        int idx = session.FirstOpenFinalCupIndex;
        if (idx >= session.FinalCups.Count) { Debug.Log("[Media1Uji] tidak ada gelas akhir yang terbuka", this); return; }
        TestPour(session.W, session.FinalCups[idx]);
    }

    [ContextMenu("Uji: prediksi gelas akhir")]
    void TestPredict()
    {
        EnsureSession();
        PredictResult r = session.TryPredict(testPrediction);
        Debug.Log($"[Media1Uji] prediksi {testPrediction}: {(r.error != PredictError.None ? r.error.ToString() : (r.correct ? "BENAR, gelas akhir muncul" : "SALAH"))}", this);
    }

    [ContextMenu("Uji: reset")] void TestReset() { EnsureSession(); session.ResetAll(); }
}