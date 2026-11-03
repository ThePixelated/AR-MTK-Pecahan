using UnityEngine;
using UnityEngine.Rendering;

// Satu wadah cairan (gelas, wadah, gelas hasil). Profil rongga hasil Auto-fit menggerakkan semuanya:
// mesh cairan, level isi, titik acuan tuang. Ganti model = Auto-fit ulang.
public class LiquidVessel : MonoBehaviour
{
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int PlanePointId = Shader.PropertyToID("_PlanePoint");
    static readonly int PlaneNormalId = Shader.PropertyToID("_PlaneNormal");
    static readonly int FloorYId = Shader.PropertyToID("_FloorY");
    static readonly int TopYId = Shader.PropertyToID("_TopY");
    static readonly int DivisionsId = Shader.PropertyToID("_Divisions");
    static readonly int StepId = Shader.PropertyToID("_Step");
    static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
    static readonly int TopLineId = Shader.PropertyToID("_TopLine");
    const int SliceCount = 96;

    [Header("Model")]
    [SerializeField] Transform glass;                    // objek yang punya MeshFilter kaca; ruang lokalnya = ruang profil
    [SerializeField] VesselProfile profile = new VesselProfile();
    [SerializeField, Range(0f, 0.3f)] float fillTopMargin = 0.06f;   // batas isi penuh = bibir dikurangi sekian % kedalaman

    [Header("Cairan")]
    [SerializeField] Material liquidMaterial;            // shader ARPecahan/LiquidBody
    [SerializeField, Range(0.9f, 1f)] float liquidInset = 0.994f;
    [SerializeField, Range(12, 96)] int liquidSegments = 48;
    [SerializeField] Color liquidColor = new Color(1f, 0.82f, 0.15f, 1f);   // sementara; 1b-2: dari LiquidStyle
    [SerializeField, Range(0f, 1f)] float fill;          // 0..1 dari kapasitas wadah (diatur game / VesselLab)

    [Header("Garis penyebut")]
    [SerializeField] LiquidStyle style;
    [SerializeField] Material scaleMaterial;                              // shader ARPecahan/VesselScale
    [SerializeField, Range(0.95f, 1f)] float scaleInnerFactor = 0.9975f;  // cangkang dalam: di antara cairan dan kaca
    [SerializeField, Range(1f, 1.05f)] float scaleOuterFactor = 1.004f;   // cangkang luar: tepat di luar kaca

    [Header("Goyangan permukaan (slosh)")]
    [SerializeField] bool sloshEnabled = true;
    [SerializeField] float sloshStiffness = 45f;
    [SerializeField] float sloshDamping = 5f;
    [SerializeField] float sloshGain = 1f;
    [SerializeField] float sloshMax = 0.3f;

    // ---- runtime ----
    GameObject liquidGO;
    MeshRenderer liquidRenderer;
    Mesh liquidMesh;
    MaterialPropertyBlock mpb;
    float[] sliceY, sliceR;
    float sliceDy, capacityVolume, radiusFactor;
    float cachedFill = -1f, cachedD;
    Vector3 cachedU;
    bool built;
    Vector3 slosh, sloshVel, lastPos, lastVel;
    bool hasLast;
    bool scaleErrorLogged;

    GameObject scaleInnerGO, scaleOuterGO;
    MeshRenderer scaleInnerR, scaleOuterR;
    Mesh scaleInnerMesh, scaleOuterMesh;
    MaterialPropertyBlock scaleMpb;
    int scaleDivisions = 1, scaleStep = 1;
    float glassApothem = 1f;

    public Transform Glass => glass;
    public VesselProfile Profile => profile;
    public float FillTopY => profile.floorY + profile.Depth * (1f - fillTopMargin);

    public float Fill { get => fill; set => fill = Mathf.Clamp01(value); }
    public Color LiquidColor { get => liquidColor; set => liquidColor = value; }
    public bool ScaleVisible { get; set; } = true;     // false: garis penyebut disembunyikan walau style menyalakannya (Media 3)

    // Titik acuan (dunia) untuk tahap tuang
    public Vector3 RimCenterWorld => glass.TransformPoint(new Vector3(profile.axisXZ.x, profile.rimY, profile.axisXZ.y));
    public Vector3 FloorCenterWorld => glass.TransformPoint(new Vector3(profile.axisXZ.x, profile.floorY, profile.axisXZ.y));
    public bool HasSpout => profile.hasSpout;
    public Vector3 SpoutDirectionWorld => profile.hasSpout
        ? glass.TransformDirection(new Vector3(profile.spoutDirXZ.x, 0f, profile.spoutDirXZ.y)).normalized
        : Vector3.zero;

    // ---- dipakai tahap tuang ----
    public bool LiquidVisible { get; private set; }
    public Vector3 LiquidPlanePointWorld { get; private set; }
    public Vector3 LiquidPlaneNormalWorld { get; private set; } = Vector3.up;
    public Color LiquidColorNow => liquidColor;

    public float DepthWorld => profile.Depth * glass.lossyScale.y;
    public float RimRadiusWorld => profile.RadiusAt(profile.rimY) * glass.lossyScale.x;

    // Titik bibir (lokal kaca) untuk menuang ke arah dirWorld (horizontal); pakai ujung cerat kalau ada
    public Vector3 LipLocal(Vector3 dirWorld, bool useSpout)
    {
        if (useSpout && profile.hasSpout)
        {
            float tip = profile.outerRadius + profile.spoutReach;
            return new Vector3(profile.axisXZ.x + profile.spoutDirXZ.x * tip, profile.rimY, profile.axisXZ.y + profile.spoutDirXZ.y * tip);
        }
        Vector3 d = glass.InverseTransformDirection(dirWorld);
        d.y = 0f;
        d = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.right;
        float r = profile.RadiusAt(profile.rimY);
        return new Vector3(profile.axisXZ.x + d.x * r, profile.rimY, profile.axisXZ.y + d.z * r);
    }

    // Titik di permukaan cairan tepat di bawah pusat bibir (atau pusat lantai kalau kosong)
    public Vector3 LiquidSurfaceCenterWorld()
    {
        if (!LiquidVisible) return FloorCenterWorld;
        float denom = Vector3.Dot(LiquidPlaneNormalWorld, Vector3.up);
        if (Mathf.Abs(denom) < 1e-4f) return FloorCenterWorld;
        Vector3 top = RimCenterWorld;
        float t = Vector3.Dot(LiquidPlaneNormalWorld, LiquidPlanePointWorld - top) / denom;
        return top + Vector3.up * t;
    }

    public void SetGlass(Transform t) => glass = t;       // dipakai tool Auto-fit

    void OnEnable() => RebuildLiquid();
    void OnDisable() => DestroyLiquid();

    [ContextMenu("Bangun ulang cairan")]
    public void RebuildLiquid()
    {
        DestroyLiquid();

        if (glass == null || !profile.IsValid)
        {
            Debug.LogError($"[{name}] LiquidVessel: 'Glass' kosong atau profil belum diisi. Jalankan 'Auto-fit rongga dari mesh' dulu.", this);
            return;
        }
        if (liquidMaterial == null)
        {
            Debug.LogError($"[{name}] LiquidVessel: 'Liquid Material' kosong (pakai material dengan shader ARPecahan/LiquidBody).", this);
            return;
        }

        float apothem = profile.segments >= 6 ? Mathf.Cos(Mathf.PI / profile.segments) : 1f;   // sisi datar mesh kaca lebih dekat ke sumbu
        radiusFactor = apothem * liquidInset;
        glassApothem = apothem;

        liquidGO = new GameObject("Liquid");
        liquidGO.transform.SetParent(glass, false);
        liquidMesh = LiquidMeshBuilder.Revolve(profile, liquidSegments, radiusFactor);
        liquidGO.AddComponent<MeshFilter>().sharedMesh = liquidMesh;
        liquidRenderer = liquidGO.AddComponent<MeshRenderer>();
        liquidRenderer.sharedMaterial = liquidMaterial;
        liquidRenderer.shadowCastingMode = ShadowCastingMode.Off;
        liquidRenderer.receiveShadows = false;
        mpb = new MaterialPropertyBlock();

        BuildSlices();
        cachedFill = -1f;
        hasLast = false;
        built = true;     // cairan siap; jangan bergantung pada garis

        try { BuildScaleShells(); }
        catch (System.Exception e) { Debug.LogError($"[{name}] Garis penyebut gagal dibangun (cairan tetap jalan): {e}", this); }
    }

    void DestroyLiquid()
    {
        built = false;
        if (liquidGO) Destroy(liquidGO);
        if (liquidMesh) Destroy(liquidMesh);

        if (scaleInnerGO) Destroy(scaleInnerGO);
        if (scaleOuterGO) Destroy(scaleOuterGO);
        if (scaleInnerMesh) Destroy(scaleInnerMesh);
        if (scaleOuterMesh) Destroy(scaleOuterMesh);
        scaleInnerGO = scaleOuterGO = null; scaleInnerR = scaleOuterR = null; scaleMpb = null;

        liquidGO = null; liquidMesh = null; liquidRenderer = null;
    }

    // Irisan datar rongga untuk solver; kapasitas = volume sampai FillTopY
    void BuildSlices()
    {
        sliceY = new float[SliceCount];
        sliceR = new float[SliceCount];
        sliceDy = profile.Depth / SliceCount;
        capacityVolume = 0f;

        float top = FillTopY;
        for (int i = 0; i < SliceCount; i++)
        {
            float y = profile.floorY + (i + 0.5f) * sliceDy;
            float r = profile.RadiusAt(y) * radiusFactor;
            sliceY[i] = y;
            sliceR[i] = r;

            float frac = Mathf.Clamp01((top - (y - 0.5f * sliceDy)) / sliceDy);
            capacityVolume += frac * Mathf.PI * r * r * sliceDy;
        }
    }

    void LateUpdate()
    {
        if (!built) return;

        try { UpdateScale(); }    // garis tetap tampil walau cairan kosong
        catch (System.Exception e)
        {
            if (!scaleErrorLogged) { scaleErrorLogged = true; Debug.LogError($"[{name}] UpdateScale error: {e}", this); }
        }


        float f = Mathf.Clamp01(fill);
        bool visible = f > 0.0005f;
        if (liquidRenderer.enabled != visible) liquidRenderer.enabled = visible;
        LiquidVisible = visible;
        if (!visible) return;

        UpdateSlosh();
        Vector3 nWorld = (Vector3.up + slosh).normalized;                  // normal permukaan di dunia
        Vector3 uLocal = glass.InverseTransformDirection(nWorld).normalized;

        if (!Mathf.Approximately(f, cachedFill) || (uLocal - cachedU).sqrMagnitude > 1e-8f)
        {
            cachedD = LiquidPlaneSolver.SolveOffset(sliceY, sliceR, sliceDy, uLocal, f * capacityVolume);
            cachedFill = f;
            cachedU = uLocal;
        }

        // titik pada bidang (u . p' = d, p' relatif sumbu) lalu ke dunia
        Vector3 pLocal = new Vector3(profile.axisXZ.x + uLocal.x * cachedD, uLocal.y * cachedD, profile.axisXZ.y + uLocal.z * cachedD);

        liquidRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(ColorId, liquidColor);
        mpb.SetVector(PlanePointId, glass.TransformPoint(pLocal));
        mpb.SetVector(PlaneNormalId, nWorld);

        LiquidPlanePointWorld = glass.TransformPoint(pLocal);
        LiquidPlaneNormalWorld = nWorld;
        liquidRenderer.SetPropertyBlock(mpb);
    }

    public LiquidStyle Style => style;

    // Garis penyebut: divisions = jumlah bagian (garis di k/divisions); stepSections = gambar garis tiap sekian bagian
    // (gelas: p bagian tiap 1; wadah: T bagian tiap p)
    public void ConfigureScale(int divisions, int stepSections)
    {
        scaleDivisions = Mathf.Max(1, divisions);
        scaleStep = Mathf.Max(1, stepSections);
    }

    void BuildScaleShells()
    {
        if (scaleMaterial == null) return;     // garis opsional

        float outerR = profile.outerRadius * scaleOuterFactor;
        scaleInnerMesh = LiquidMeshBuilder.Shell(profile, liquidSegments,
            y => profile.RadiusAt(y) * glassApothem * scaleInnerFactor, profile.floorY, profile.rimY);
        scaleOuterMesh = LiquidMeshBuilder.Shell(profile, liquidSegments, y => outerR, profile.floorY, profile.rimY);

        scaleInnerGO = MakeShell("ScaleInner", scaleInnerMesh, out scaleInnerR);
        scaleOuterGO = MakeShell("ScaleOuter", scaleOuterMesh, out scaleOuterR);
        scaleMpb = new MaterialPropertyBlock();
    }

    GameObject MakeShell(string goName, Mesh mesh, out MeshRenderer mr)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(glass, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = scaleMaterial;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    void UpdateScale()
    {
        if (scaleMpb == null) return;
        ApplyScale(scaleInnerR, ScaleVisible && style != null && style.showInnerLines);
        ApplyScale(scaleOuterR, ScaleVisible && style != null && style.showOuterLines);
    }

    void ApplyScale(MeshRenderer mr, bool on)
    {
        if (!mr) return;
        if (mr.enabled != on) mr.enabled = on;
        if (!on) return;

        mr.GetPropertyBlock(scaleMpb);
        scaleMpb.SetColor(ColorId, style.lineColor);
        scaleMpb.SetFloat(FloorYId, profile.floorY);
        scaleMpb.SetFloat(TopYId, FillTopY);
        scaleMpb.SetFloat(DivisionsId, scaleDivisions);
        scaleMpb.SetFloat(StepId, scaleStep);
        scaleMpb.SetFloat(LineWidthId, style.lineWidth);
        scaleMpb.SetFloat(TopLineId, style.topLine ? 1f : 0f);
        mr.SetPropertyBlock(scaleMpb);
    }

    // Pegas teredam: permukaan sedikit miring saat wadah dipercepat, lalu tenang lagi
    void UpdateSlosh()
    {
        if (!sloshEnabled) { slosh = Vector3.zero; sloshVel = Vector3.zero; hasLast = false; return; }

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        Vector3 pos = glass.position;
        if (!hasLast) { lastPos = pos; lastVel = Vector3.zero; hasLast = true; }

        Vector3 vel = (pos - lastPos) / dt;
        Vector3 acc = (vel - lastVel) / dt;
        lastPos = pos; lastVel = vel;

        Vector3 accH = Vector3.ClampMagnitude(new Vector3(acc.x, 0f, acc.z) / 9.81f, 2f);
        sloshVel += (-sloshStiffness * slosh - sloshDamping * sloshVel + sloshGain * accH) * dt;
        slosh += sloshVel * dt;
        if (slosh.sqrMagnitude > sloshMax * sloshMax) slosh = slosh.normalized * sloshMax;
    }

    [ContextMenu("Debug: Cetak status cairan")]
    void DebugStatus()
    {
        string liquidInfo = "TIDAK ADA", mp = "-", glassInfo = "-";
        if (liquidRenderer)
        {
            var tmp = new MaterialPropertyBlock();
            liquidRenderer.GetPropertyBlock(tmp);
            liquidInfo = $"enabled={liquidRenderer.enabled}, shader={(liquidRenderer.sharedMaterial ? liquidRenderer.sharedMaterial.shader.name : "null")}";
            mp = $"PlanePoint={tmp.GetVector(PlanePointId)} PlaneNormal={tmp.GetVector(PlaneNormalId)} Color={tmp.GetColor(ColorId)}";
        }
        if (glass)
        {
            var gr = glass.GetComponent<Renderer>();
            if (gr && gr.sharedMaterial) glassInfo = $"{gr.sharedMaterial.name} / {gr.sharedMaterial.shader.name} / queue {gr.sharedMaterial.renderQueue}";
        }

        Debug.Log($"[Vessel] {name} | built={built} fill={fill:0.00} profilValid={profile.IsValid} posisiDunia={(glass ? glass.position.ToString() : "-")} | "
                + $"cairan: {liquidInfo} | {mp} | garis: dalam={(scaleInnerR != null ? scaleInnerR.enabled.ToString() : "tidak ada")} "
                + $"luar={(scaleOuterR != null ? scaleOuterR.enabled.ToString() : "tidak ada")} style={(style != null)} "
                + $"scaleMat={(scaleMaterial != null)} liquidMat={(liquidMaterial != null)} | kaca: {glassInfo}", this);
    }
}