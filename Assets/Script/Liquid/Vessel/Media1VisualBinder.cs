using System.Collections.Generic;
using UnityEngine;

// Menghubungkan Media1Session (data) ke LiquidVessel (tampilan): level isi, warna per tingkat encer, garis penyebut,
// dan level botol yang sedang menuang. Informasi jumlah dituang ada di panel Informasi Larutan (controller), bukan label gelas.
// Bagian "Uji" untuk mencoba tanpa UI; di halaman, controller Media 1 yang membuat dan meng-Bind session.
public class Media1VisualBinder : LiquidBinderBase
{
    [SerializeField] LiquidStyle style;
    [SerializeField] LiquidVessel vesselA, vesselB, vesselW;
    [SerializeField] LiquidVessel[] finalCupVessels;      // disiapkan di scene (nonaktif), sebanyak Max Predict Cups; diaktifkan satu per satu oleh tombol Tambah Gelas
    [SerializeField] float fillSpeed = 1f;                // level mengikuti data (rasio per detik)

    [Header("Uji (sementara)")]
    [SerializeField] bool createTestSession = false;
    [SerializeField, Range(2, 10)] int testDenominator = 6;
    [SerializeField, Range(1, 5)] int testPrediction = 2;

    Media1Session session;
    readonly List<LiquidContainer> containers = new List<LiquidContainer>();
    readonly List<LiquidVessel> vessels = new List<LiquidVessel>();
    readonly Dictionary<LiquidVessel, float> shown = new Dictionary<LiquidVessel, float>();
    float pourWindowEnd = -1f;

    LiquidVessel bottle;                 // botol dari gambar yang sedang menuang
    float bottleFlowStart = -1f, bottleFlowLength = 1f;

    public Media1Session Session => session;
    public override IPourSession PourSession => session;

    // Selama jendela ini, level bergerak merata sampai tepat selesai di akhir durasi tuang
    public override void BeginPourWindow(float seconds)
    {
        pourWindowEnd = Time.time + seconds;
        if (bottle) { bottleFlowStart = Time.time; bottleFlowLength = Mathf.Max(0.05f, seconds); }
    }

    public override bool TryGetContainer(LiquidVessel v, out LiquidContainer c)
    {
        for (int i = 0; i < vessels.Count; i++)
            if (vessels[i] == v) { c = containers[i]; return true; }
        c = null;
        return false;
    }

    // Botol dari gambar air atau sirup: penuh, lalu isinya berkurang sepanjang tuangan
    public void SetBottle(LiquidVessel v, Color color)
    {
        bottle = v;
        bottleFlowStart = -1f;
        if (!v) return;
        v.ScaleVisible = false;
        v.Fill = 1f;
        v.LiquidColor = color;
    }

    public void ClearBottle()
    {
        bottle = null;
        bottleFlowStart = -1f;
    }

    // Warna larutan murni (air atau sirup), sama dengan warna di gelas A dan B
    public Color PureColor(LiquidKind kind)
    {
        if (style == null || session == null) return Color.white;
        int levels = session.Limits.DilutionLevels;
        Mixture m = kind == LiquidKind.Water ? Mixture.Water(1) : Mixture.Syrup(1);
        return style.ColorFor(LiquidColorLevel.Level(m, levels), levels);
    }

    void Start() { if (createTestSession && session == null) { Bind(new Media1Session()); session.SetPartition(testDenominator); } }
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

        if (session.FinalCups.Count > finalCupVessels.Length)
            Debug.LogWarning($"[{name}] Media1VisualBinder: {session.FinalCups.Count} gelas hasil tapi hanya {finalCupVessels.Length} objek gelas disiapkan di 'Final Cup Vessels'.", this);

        int levels = session.Limits.DilutionLevels;
        int p = session.Denominator > 0 ? session.Denominator : 1;
        for (int i = 0; i < containers.Count; i++)
        {
            LiquidVessel v = vessels[i];
            if (v == null) continue;
            LiquidContainer c = containers[i];

            bool isW = c == session.W;
            v.ConfigureScale(isW ? (int)Mathf.Max(1, c.CapacitySections) : p, 1);   // gelas: p garis; wadah: 2p garis (partisi 4 = 8 bagian), semuanya digambar
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

        if (bottle)
            bottle.Fill = bottleFlowStart < 0f ? 1f : 1f - Mathf.Clamp01((Time.time - bottleFlowStart) / bottleFlowLength);
    }

    // ---- menu uji (klik kanan komponen saat Play) ----
    void EnsureSession() { if (session == null) Bind(new Media1Session()); }

    [ContextMenu("Uji: atur partisi")]
    void TestPartition()
    {
        EnsureSession();
        ConfigError e = session.SetPartition(testDenominator);
        if (e != ConfigError.None) Debug.Log($"[Media1Uji] partisi ditolak: {e}", this);
    }

    [ContextMenu("Uji: tambah air (gelas A)")] void TestAddWater() { EnsureSession(); Debug.Log($"[Media1Uji] {session.TryAdd(LiquidKind.Water)}", this); }
    [ContextMenu("Uji: tambah sirup (gelas B)")] void TestAddSyrup() { EnsureSession(); Debug.Log($"[Media1Uji] {session.TryAdd(LiquidKind.Syrup)}", this); }
    [ContextMenu("Uji: gabungkan A ke wadah")] void TestMergeA() { EnsureSession(); Debug.Log($"[Media1Uji] {session.TryPourAll(session.A)}", this); }
    [ContextMenu("Uji: gabungkan B ke wadah")] void TestMergeB() { EnsureSession(); Debug.Log($"[Media1Uji] {session.TryPourAll(session.B)}", this); }

    [ContextMenu("Uji: tuang W ke gelas akhir")]
    void TestPourToCup()
    {
        EnsureSession();
        int idx = session.FirstOpenFinalCupIndex;
        if (idx >= session.FinalCups.Count) { Debug.Log("[Media1Uji] tidak ada gelas akhir yang terbuka", this); return; }
        Debug.Log($"[Media1Uji] {session.TryPour(session.W, session.FinalCups[idx])}", this);
    }

    [ContextMenu("Uji: prediksi gelas akhir")]
    void TestPredict()
    {
        EnsureSession();
        PredictResult r = session.TryPredict(testPrediction);
        Debug.Log($"[Media1Uji] prediksi {testPrediction}: {(r.error != PredictError.None ? r.error.ToString() : (r.correct ? "BENAR, gelas akhir muncul" : "SALAH"))}", this);
    }

    [ContextMenu("Uji: reset isi")] void TestReset() { EnsureSession(); session.ResetContent(); }
}