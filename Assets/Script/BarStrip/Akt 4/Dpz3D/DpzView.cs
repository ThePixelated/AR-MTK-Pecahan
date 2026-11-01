using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// Area Balok 3D. Dua bagian: ATAS = balok yang ditarik dari Kotak (urutan bebas), BAWAH = tebakan sisa dari bank (satu row),
// dipisah sectionGapRows baris kosong. Sumbu lokal: baris memanjang ke +X, baris berikutnya ke -Z (atau +Z).
public class DpzView : MonoBehaviour
{
    static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
    static readonly int BorderThicknessId = Shader.PropertyToID("_BorderThickness");
    static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    static readonly int HatchColorId = Shader.PropertyToID("_HatchColor");
    static readonly int HatchEnabledId = Shader.PropertyToID("_HatchEnabled");
    static readonly int HatchAngleId = Shader.PropertyToID("_HatchAngle");
    static readonly int HatchSpacingId = Shader.PropertyToID("_HatchSpacing");
    static readonly int HatchWidthId = Shader.PropertyToID("_HatchWidth");
    static readonly int HatchDashedId = Shader.PropertyToID("_HatchDashed");
    static readonly int HatchDashLengthId = Shader.PropertyToID("_HatchDashLength");
    static readonly int HatchDashGapId = Shader.PropertyToID("_HatchDashGap");
    static readonly int HatchPhaseId = Shader.PropertyToID("_HatchPhase");    // opsional: hanya dipakai kalau shader punya
    static readonly int MeshUnitId = Shader.PropertyToID("_MeshUnit");
    static readonly int AnchorTopId = Shader.PropertyToID("_AnchorTop");

    static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    [SerializeField] Activity4Controller controller;
    [SerializeField] DpzStyle style;
    [SerializeField] UnitVisualSet visuals;
    [SerializeField] Material decalMaterial;           // shader ARPecahan/TrackDecal

    [Header("Area (Plane dengan material shader ARPecahan/DpzArea, anak dari objek ini)")]
    [SerializeField] Transform outerArea;              // zona luar (catch-all)
    [SerializeField] Transform innerArea;              // area balok
    [SerializeField] Transform upperArea;              // bagian atas: balok dari Kotak (opsional)
    [SerializeField] Transform lowerArea;              // bagian bawah: tebakan sisa (opsional)
    [SerializeField] float areaMeshUnit = 10f;         // ukuran mesh bawaan di X dan Z (Plane Unity = 10, Quad/Cube = 1)
    [SerializeField] float areaScaleY = 1f;
    [SerializeField] float areaBaseY = 0f;
    [SerializeField] float innerAreaLift = 0.0002f;
    [SerializeField] float areaSmoothing = 12f;
    [SerializeField] bool growTowardPositiveZ = false; // default: baris baru ke arah -Z

    [Header("Raycast dan collider")]
    [SerializeField] LayerMask dpzMask;                // SATU layer khusus dpz (buat di Tags and Layers)
    [SerializeField] float colliderHeight = 0.02f;
    [SerializeField] float raycastMaxDistance = 5f;
    [SerializeField] bool closeSelectionOnUpdate = true;   // true: panel aksi menutup setiap ada perubahan area; false: hanya saat jumlah blok berubah

    struct SectionRect { public Vector2 center; public Vector2 size; }   // center = (x, z)

    sealed class AreaBinding
    {
        public Transform t;
        public MeshRenderer mr;
        public DpzAreaStyle cfg;
        public int order;
        public bool wasDynamic;
    }

    FractionSession session;
    bool bound;
    readonly List<DpzBlockView> blocks = new List<DpzBlockView>();        // bagian bawah (dpz)
    readonly List<DpzBlockView> upperBlocks = new List<DpzBlockView>();   // bagian atas
    //readonly List<DpzBlockView> carryBlocks = new List<DpzBlockView>();FC
    readonly List<DpzBlockView> carryBlocks = new List<DpzBlockView>();

    int highlightedBlock = -1;
    int liftedBlock = -1;
    int selectedBlock = -1;
    int mergeTarget = -1;
    int mergeCount;
    bool mergeGhostOn;
    bool predictedUpperExtra;
    bool tracked = true;   // false = marker hilang
    Vector2 innerSize, targetInnerSize;               // (lebar X, kedalaman Z)
    int layoutUpperRows = 1;
    SectionRect upperNow, upperTarget, lowerNow, lowerTarget;

    BoxCollider outerCol, innerCol;
    MaterialPropertyBlock areaMpb;
    AreaBinding[] areaBindings = new AreaBinding[0];
    AreaTitle outerTitle, innerTitle, upperTitle, lowerTitle;

    DpzBlockView preview;                  // slot blok baru di bagian bawah
    bool previewShown, previewGhostShown;
    Fraction previewPiece;
    int previewRow;

    DpzBlockView upperPreview;             // slot balok Kotak di bagian atas
    bool upperPreviewShown;
    Fraction upperPreviewPiece;
    int upperPreviewSlot;

    float GrowSign => growTowardPositiveZ ? 1f : -1f;
    float Pitch => style.thickness + style.rowGap;
    int DpzLayer
    {
        get
        {
            int v = dpzMask.value;
            return v == 0 ? gameObject.layer : Mathf.RoundToInt(Mathf.Log(v & -v, 2f));   // layer terendah di mask
        }
    }

    public float ColliderHeight => colliderHeight;
    public int BlockCount => blocks.Count;               // balok bagian bawah
    public int UpperBlockCount => upperBlocks.Count;
    public bool Tracked => tracked;

    // Dipanggil DpzTrackingGate: marker hilang = dpz tidak bisa disentuh (raycast tidak mengenai apa pun)
    public void SetTracked(bool value)
    {
        if (tracked == value) return;
        tracked = value;
        if (!value) { ClearHover(); ClearSelection(); }
    }

    void OnEnable() => Bind();
    void OnDisable() { ClearHover(); ClearSelection(); Unbind(); }

    void Bind()
    {
        if (bound) return;
        if (controller == null || style == null || visuals == null || decalMaterial == null)
        {
            Debug.LogError($"[{name}] DpzView: controller/style/visuals/decalMaterial belum lengkap", this);
            return;
        }
        if (dpzMask.value == 0) Debug.LogWarning($"[{name}] DpzView: dpzMask kosong, raycast dunia tidak akan menemukan dpz", this);

        session = controller.Session;
        session.Dpz.Changed += RefreshLower;
        session.Upper.Changed += RefreshUpper;
        bound = true;

        outerCol ??= CreateAreaCollider("OuterCatch", DpzColliderKind.Outer);
        innerCol ??= CreateAreaCollider("InnerCatch", DpzColliderKind.Inner);

        RefreshUpper();      // menghitung jumlah baris bagian atas dulu, baru bagian bawah ditempatkan
        RefreshLower();
        ApplyAreaVisuals();
        innerSize = targetInnerSize;   // tampil langsung dengan ukuran yang benar, tanpa animasi awal
        upperNow = upperTarget;
        lowerNow = lowerTarget;
        ApplyAreas();
    }

    void Unbind()
    {
        if (!bound) return;
        session.Dpz.Changed -= RefreshLower;
        session.Upper.Changed -= RefreshUpper;
        bound = false;
    }

    BoxCollider CreateAreaCollider(string objName, DpzColliderKind kind)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        go.layer = DpzLayer;
        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        var tag = go.AddComponent<DpzCollider>();
        tag.owner = this;
        tag.kind = kind;
        return col;
    }

    // ---------- tata letak ----------
    //int CarryCount => (style.showCarryBlock && session != null) ? (int)Mathf.Clamp(session.Upper.CarryWholes, 0, 4) : 0;
    //int UpperVisualCount => session.Upper.Count + CarryCount;

    int UpperVisualCount => session.Composed.Count;

    int UpperRowsFor(int visualCount)
    {
        int withEmpty = Mathf.Min(visualCount + style.minEmptyRows, GameRules.MaxUpperBlocks + 1);
        return Mathf.Max(style.minRows, Mathf.Max(visualCount, withEmpty));
    }

    int LowerSlot => layoutUpperRows + style.sectionGapRows;

    Vector3 BlockOrigin(int slot)
    {
        float z = GrowSign < 0f
            ? -(style.innerPadding + slot * Pitch)
            : style.innerPadding + slot * Pitch + style.thickness;
        return new Vector3(style.innerPadding, 0f, z);   // blok memanjang ke -Z dari origin-nya, jadi arah +Z digeser setebal blok
    }

    Vector3 LowerOrigin(int i) => BlockOrigin(LowerSlot + i);

    // Tepi baris yang paling dekat dengan sudut anchor, di sumbu Z
    float RowNear(int slot) => GrowSign < 0f ? -(style.innerPadding + slot * Pitch) : style.innerPadding + slot * Pitch;

    Vector2 ComputeInnerSize(int rows)
    {
        float w = style.wholeLength + 2f * style.innerPadding;
        float d = rows * style.thickness + (rows - 1) * style.rowGap + 2f * style.innerPadding;
        return new Vector2(w, d);
    }

    SectionRect ComputeSection(int firstSlot, int rows)
    {
        float near = RowNear(firstSlot) - GrowSign * style.sectionPadding;
        float far = RowNear(firstSlot + rows - 1) + GrowSign * (style.thickness + style.sectionPadding);
        return new SectionRect
        {
            center = new Vector2(style.innerPadding + style.wholeLength * 0.5f, (near + far) * 0.5f),
            size = new Vector2(style.wholeLength + 2f * style.sectionPadding, Mathf.Abs(far - near))
        };
    }

    // Saat hover Kotak yang bakal menambah blok dan toggle aktif, area langsung dihitung dengan satu baris lebih banyak
    void RecomputeTarget()
    {
        int visual = UpperVisualCount + ((style.areaPredictsOnHover && predictedUpperExtra) ? 1 : 0);
        int rows = UpperRowsFor(visual);
        bool changed = rows != layoutUpperRows;
        layoutUpperRows = rows;

        targetInnerSize = ComputeInnerSize(LowerSlot + 1);
        upperTarget = ComputeSection(0, layoutUpperRows);
        lowerTarget = ComputeSection(LowerSlot, 1);

        if (changed) { ClearSelection(); RelayoutLower(); }
        if (bound) ApplyTitles();
    }

    void SetPredictedUpperExtra(bool value)
    {
        if (predictedUpperExtra == value) return;
        predictedUpperExtra = value;
        RecomputeTarget();
    }

    DpzBlockView CreateBlock(DpzColliderKind kind = DpzColliderKind.Block)
    {
        var go = new GameObject("DpzBlock");
        go.transform.SetParent(transform, false);
        var b = go.AddComponent<DpzBlockView>();
        b.Init(style, visuals, decalMaterial, this, DpzLayer, kind);
        b.SetFillFromRight(session != null && session.Op == Operation.Subtract);
        return b;
    }

    // ---------- data -> tampilan: bagian atas ----------
    void RefreshUpper()
    {
        HideUpperPreview();
        predictedUpperExtra = false;
        if (closeSelectionOnUpdate) ClearSelection();

        var composed = session.Composed;
        var raw = session.Upper.Blocks;
        int count = composed.Count;

        while (upperBlocks.Count > count)
        {
            DpzBlockView extra = upperBlocks[upperBlocks.Count - 1];
            upperBlocks.RemoveAt(upperBlocks.Count - 1);
            if (extra) { extra.gameObject.SetActive(false); Destroy(extra.gameObject); }
        }

        for (int i = 0; i < count; i++)
        {
            if (i >= upperBlocks.Count) upperBlocks.Add(null);
            if (!upperBlocks[i]) upperBlocks[i] = CreateBlock(DpzColliderKind.UpperBlock);

            ComposedBlock cb = composed[i];
            upperBlocks[i].ShowComposed(cb.piece, i, CellColors(cb, raw), BlockOrigin(i), cb.kind == ComposedKind.Carry);
        }

        RecomputeTarget();
    }

    // Warna tiap sel = warna pecahan asalnya (bukan warna satuan bank); balok yang dikurangkan diberi tint
    Color[] CellColors(ComposedBlock cb, IReadOnlyList<AreaBlock> raw)
    {
        var colors = new Color[cb.cellSource.Length];
        for (int k = 0; k < colors.Length; k++)
        {
            AreaBlock b = raw[cb.cellSource[k]];
            Color c = controller.Palette.Get(b.piece.denominator);
            if (b.subtracted) c = Color.Lerp(c, style.subtractColor, style.subtractBlend);
            colors[k] = (cb.kind == ComposedKind.Carry && !style.carryUseSourceColors) ? style.carryColor : c;
        }
        return colors;
    }

    // Balok utuh otomatis dari jumlah row sebagian yang melewati 1; tidak bisa disentuh
    // Balok utuh otomatis dari jumlah row sebagian yang melewati 1; tidak bisa disentuh
    //void RefreshCarry()
    //{
    //    int need = CarryCount;

    //    while (carryBlocks.Count < need)
    //    {
    //        DpzBlockView b = CreateBlock(DpzColliderKind.UpperBlock);
    //        b.name = "DpzCarry";
    //        b.SetInteractive(false);
    //        carryBlocks.Add(b);
    //    }

    //    for (int k = 0; k < carryBlocks.Count; k++)
    //    {
    //        DpzBlockView b = carryBlocks[k];
    //        bool on = k < need;
    //        if (b.gameObject.activeSelf != on) b.gameObject.SetActive(on);
    //        if (!on) continue;

    //        Color color = style.carryMixedColor ? CarryMixColor(k) : style.carryColor;
    //        b.ShowCarry(session.Upper.Count + k, color, BlockOrigin(session.Upper.Count + k));
    //    }
    //}

    // Warna carry ke-k: row sebagian di area atas (urutan tampil) mengisi carry satu per satu;
    // tiap carry diwarnai rata-rata berbobot (menurut nilai) dari warna pecahan yang mengisinya
    //Color CarryMixColor(int k)
    //{
    //    int current = 0;
    //    float weight = 0f;
    //    Color accum = Color.black;

    //    foreach (AreaBlock b in session.Upper.Blocks)
    //    {
    //        if (b.subtracted || b.piece.numerator == b.piece.denominator) continue;     // hanya row sebagian yang membentuk carry

    //        float value = (float)b.piece.numerator / b.piece.denominator;
    //        Color c = controller.Palette.Get(b.piece.denominator);

    //        while (value > 1e-5f)
    //        {
    //            float take = Mathf.Min(value, 1f - weight);
    //            if (current == k) accum += c * take;
    //            weight += take;
    //            value -= take;

    //            if (weight >= 1f - 1e-5f)
    //            {
    //                if (current == k) return new Color(accum.r, accum.g, accum.b, 1f);
    //                current++;
    //                weight = 0f;
    //                accum = Color.black;
    //            }
    //        }
    //    }
    //    return style.carryColor;      // tidak seharusnya terjadi: carry selalu terisi penuh oleh pecahan
    //}

    // ---------- data -> tampilan: bagian bawah ----------
    void RefreshLower()
    {
        HidePreview();
        SetMergeFeedback(-1, 0, false);

        var pieces = session.Dpz.Pieces;
        int count = pieces.Count;
        int oldCount = blocks.Count;

        int removedAt = session.Dpz.LastRemovedIndex;
        bool reflow = style.reflowSpeed > 0f && removedAt >= 0 && count == oldCount - 1;

        while (blocks.Count > count)
        {
            DpzBlockView extra = blocks[blocks.Count - 1];
            blocks.RemoveAt(blocks.Count - 1);
            if (extra) { extra.gameObject.SetActive(false); Destroy(extra.gameObject); }
        }

        for (int i = 0; i < count; i++)
        {
            if (i >= blocks.Count) blocks.Add(null);
            if (!blocks[i]) blocks[i] = CreateBlock();   // masuk list SEBELUM Show

            Fraction p = pieces[i];
            blocks[i].Show(p, i, controller.Palette.Get(p.denominator), LowerOrigin(i));
            blocks[i].SetHighlight(i == highlightedBlock ? 1f : 0f);
        }

        if (reflow)
            for (int i = removedAt; i < count; i++) blocks[i].AnimateFrom(LowerOrigin(i + 1));

        if (highlightedBlock >= count) highlightedBlock = -1;
        if (liftedBlock >= count) liftedBlock = -1;

        // Pilihan dicabut kalau jumlah blok berubah; tetap kalau hanya isi blok yang berubah (-1 satuan, merge)
        if (selectedBlock >= 0 && (closeSelectionOnUpdate || oldCount != count || selectedBlock >= count)) SetSelection(-1);
    }

    // Bagian atas bertambah atau berkurang: bagian bawah meluncur ke slot barunya
    void RelayoutLower()
    {
        if (!bound) return;
        var pieces = session.Dpz.Pieces;
        for (int i = 0; i < blocks.Count && i < pieces.Count; i++)
            if (blocks[i]) blocks[i].Show(pieces[i], i, controller.Palette.Get(pieces[i].denominator), LowerOrigin(i));
    }

    void Update()
    {
        if (!bound) return;

        float k = 1f - Mathf.Exp(-areaSmoothing * Time.unscaledDeltaTime);
        bool moving = false;

        if ((innerSize - targetInnerSize).sqrMagnitude >= 1e-12f)
        {
            innerSize = Vector2.Lerp(innerSize, targetInnerSize, k);
            if ((innerSize - targetInnerSize).sqrMagnitude < 1e-10f) innerSize = targetInnerSize;
            moving = true;
        }
        moving |= StepSection(ref upperNow, upperTarget, k);
        moving |= StepSection(ref lowerNow, lowerTarget, k);
        if (moving) ApplyAreas();

        AnimateAreas(false);
    }

    static bool StepSection(ref SectionRect now, SectionRect target, float k)
    {
        bool same = (now.center - target.center).sqrMagnitude < 1e-12f && (now.size - target.size).sqrMagnitude < 1e-12f;
        if (same) return false;

        now.center = Vector2.Lerp(now.center, target.center, k);
        now.size = Vector2.Lerp(now.size, target.size, k);
        if ((now.center - target.center).sqrMagnitude < 1e-10f && (now.size - target.size).sqrMagnitude < 1e-10f) now = target;
        return true;
    }

    // Sudut anchor kedua area tetap; hanya sisi kanan dan sisi arah tumbuh yang bergerak
    void ApplyAreas()
    {
        Vector2 outer = innerSize + Vector2.one * (2f * style.outerMargin);
        float cx = innerSize.x * 0.5f;
        float cz = GrowSign * innerSize.y * 0.5f;

        PlaceArea(outerArea, outer, cx, cz, areaBaseY);
        PlaceArea(innerArea, innerSize, cx, cz, areaBaseY + innerAreaLift);
        PlaceArea(upperArea, upperNow.size, upperNow.center.x, upperNow.center.y, areaBaseY + innerAreaLift * 2f);
        PlaceArea(lowerArea, lowerNow.size, lowerNow.center.x, lowerNow.center.y, areaBaseY + innerAreaLift * 2f);
        SizeCollider(outerCol, outer, cx, cz);
        SizeCollider(innerCol, innerSize, cx, cz);
    }

    void PlaceArea(Transform area, Vector2 size, float cx, float cz, float y)
    {
        if (!area) return;
        area.localPosition = new Vector3(cx, y, cz);
        area.localScale = new Vector3(size.x / areaMeshUnit, areaScaleY, size.y / areaMeshUnit);
    }

    void SizeCollider(BoxCollider col, Vector2 size, float cx, float cz)
    {
        if (!col) return;
        col.center = new Vector3(cx, areaBaseY + colliderHeight * 0.5f, cz);
        col.size = new Vector3(size.x, colliderHeight, size.y);
    }

    // ---------- raycast dunia: prioritas Block dan UpperBlock > Inner > Outer ----------
    public bool Raycast(Vector2 screenPos, Camera cam, out DpzHit result)
    {
        result = default;
        if (!bound || !tracked || cam == null || dpzMask.value == 0) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        int n = Physics.RaycastNonAlloc(ray, hitBuffer, raycastMaxDistance, dpzMask, QueryTriggerInteraction.Collide);

        int bestPriority = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (!hitBuffer[i].collider.TryGetComponent(out DpzCollider tag) || tag.owner != this) continue;

            bool isBlock = tag.kind == DpzColliderKind.Block || tag.kind == DpzColliderKind.UpperBlock;
            int priority = isBlock ? 2 : (tag.kind == DpzColliderKind.Inner ? 1 : 0);
            float dist = hitBuffer[i].distance;
            if (priority > bestPriority || (priority == bestPriority && dist < bestDistance))
            {
                bestPriority = priority;
                bestDistance = dist;
                result = new DpzHit { kind = tag.kind, blockIndex = tag.blockIndex, point = hitBuffer[i].point };
            }
        }
        return bestPriority >= 0;
    }

    // Potongan dari bank: masuk ke bagian bawah
    public RejectReason TryDrop(Fraction piece, int blockIndex = -1) =>
        bound ? session.TryDropToDpz(piece, blockIndex) : RejectReason.SoalBelumDibentuk;

    // Row Kotak yang ditarik: masuk ke bagian atas
    public RejectReason TryMoveFromKotak(bool fromA, int rowIndex) =>
        bound ? session.TryMoveToArea(fromA, rowIndex) : RejectReason.SoalBelumDibentuk;

    // Balok bagian atas dikembalikan ke Kotak asalnya
    public bool ReturnUpperBlock(int composedIndex) => bound && session.ReturnComposed(composedIndex);

    // Gabung antar-blok dpz (balok yang diangkat dilepas di atas balok lain); mengembalikan jumlah satuan yang pindah
    public int TryMergeBlocks(int sourceIndex, int targetIndex) =>
        bound ? session.MergeDpzBlocks(sourceIndex, targetIndex) : 0;

    public bool TryGetBlockPiece(int index, out Fraction piece)
    {
        piece = default;
        if (!bound || index < 0 || index >= session.Dpz.Count) return false;
        piece = session.Dpz.Pieces[index];
        return true;
    }

    // Blok yang sedang diangkat: selnya disembunyikan, garis lantai highlight dan beranimasi (data tidak berubah sampai dilepas)
    public void SetLiftedBlock(int index)
    {
        if (index >= 0) ClearSelection();   // mengangkat balok mencabut pilihan

        if (liftedBlock >= 0 && liftedBlock < blocks.Count && blocks[liftedBlock]) blocks[liftedBlock].SetLifted(false);
        liftedBlock = index;
        if (index >= 0 && index < blocks.Count && blocks[index]) blocks[index].SetLifted(true);
    }

    // ---------- seleksi blok bagian bawah (tap-select) ----------
    public int SelectedBlock => selectedBlock;
    public event System.Action<int> SelectionChanged;   // index terpilih, atau -1

    public void SelectBlock(int index) => SetSelection(bound && index >= 0 && index < blocks.Count ? index : -1);
    public void ClearSelection() => SetSelection(-1);

    void SetSelection(int index)
    {
        if (index == selectedBlock) return;

        if (selectedBlock >= 0 && selectedBlock < blocks.Count && blocks[selectedBlock]) blocks[selectedBlock].SetSelected(false);
        selectedBlock = index;
        if (index >= 0 && index < blocks.Count && blocks[index]) blocks[index].SetSelected(true);

        SelectionChanged?.Invoke(selectedBlock);
    }

    // Ukuran satu baris blok: panjang = Whole Length (track penuh), lebar = tebal baris (meter)
    public Vector2 BlockFootprint => new Vector2(style.wholeLength, style.thickness);

    // Titik tengah atas blok bawah ke-index (koordinat lokal objek ini)
    public Vector3 BlockTopCenterLocal(int index, float extraLift)
    {
        Vector3 o = LowerOrigin(index);
        return new Vector3(o.x + style.wholeLength * 0.5f,
                           style.baseLift + style.height + extraLift,
                           o.z - style.thickness * 0.5f);   // blok memanjang ke -Z dari origin-nya
    }

    // ---------- shader area dan judul ----------
    void ApplyAreaVisuals()
    {
        areaBindings = new[]
        {
            new AreaBinding { t = outerArea, cfg = style.outerArea, order = -20 },   // transparan: luar digambar dulu, lalu dalam, lalu bagian
            new AreaBinding { t = innerArea, cfg = style.innerArea, order = -10 },
            new AreaBinding { t = upperArea, cfg = style.upperArea, order = -6 },
            new AreaBinding { t = lowerArea, cfg = style.lowerArea, order = -5 },
        };
        AnimateAreas(true);
        ApplyTitles();
    }

    // Kedip dan garis bergerak dihitung tiap frame hanya untuk area yang memintanya
    void AnimateAreas(bool force)
    {
        float t = Time.unscaledTime;
        foreach (AreaBinding a in areaBindings)
        {
            if (a == null || a.t == null) continue;

            DpzAreaStyle cfg = a.cfg;
            bool dynamic = cfg.visible && (cfg.flash || cfg.lineAnimated);
            if (!force && !dynamic && !a.wasDynamic) continue;
            a.wasDynamic = dynamic;

            float alpha = cfg.flash ? 1f - cfg.flashDepth * (0.5f + 0.5f * Mathf.Sin(t * cfg.flashSpeed * Mathf.PI * 2f)) : 1f;
            float phase = cfg.lineAnimated ? t * cfg.lineAnimSpeed : 0f;
            ApplyAreaMaterial(a, alpha, phase);
        }
    }

    static Color WithAlpha(Color c, float mul) { c.a *= mul; return c; }

    void ApplyAreaMaterial(AreaBinding a, float alphaMul, float phase)
    {
        if (a.mr == null) a.mr = a.t.GetComponent<MeshRenderer>();
        MeshRenderer mr = a.mr;
        if (!mr) return;

        DpzAreaStyle cfg = a.cfg;
        mr.enabled = cfg.visible;
        if (!cfg.visible) return;

        areaMpb ??= new MaterialPropertyBlock();
        mr.GetPropertyBlock(areaMpb);
        areaMpb.SetColor(BorderColorId, WithAlpha(cfg.borderColor, alphaMul));
        areaMpb.SetFloat(BorderThicknessId, cfg.borderThickness);
        areaMpb.SetColor(FillColorId, WithAlpha(cfg.fillColor, alphaMul));
        areaMpb.SetColor(HatchColorId, WithAlpha(cfg.hatchColor, alphaMul));
        areaMpb.SetFloat(HatchEnabledId, cfg.hatch ? 1f : 0f);
        areaMpb.SetFloat(HatchAngleId, cfg.hatchAngle);
        areaMpb.SetFloat(HatchSpacingId, cfg.hatchSpacing);
        areaMpb.SetFloat(HatchWidthId, cfg.hatchWidth);
        areaMpb.SetFloat(HatchDashedId, cfg.hatchDashed ? 1f : 0f);
        areaMpb.SetFloat(HatchDashLengthId, cfg.hatchDashLength);
        areaMpb.SetFloat(HatchDashGapId, cfg.hatchDashGap);
        areaMpb.SetFloat(MeshUnitId, areaMeshUnit);
        areaMpb.SetFloat(AnchorTopId, GrowSign < 0f ? 1f : 0f);
        if (mr.sharedMaterial != null && mr.sharedMaterial.HasProperty(HatchPhaseId)) areaMpb.SetFloat(HatchPhaseId, phase);
        mr.SetPropertyBlock(areaMpb);

        mr.sortingOrder = a.order;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void ApplyTitles()
    {
        outerTitle ??= new AreaTitle(transform, "OuterTitle");
        innerTitle ??= new AreaTitle(transform, "InnerTitle");
        upperTitle ??= new AreaTitle(transform, "UpperTitle");
        lowerTitle ??= new AreaTitle(transform, "LowerTitle");

        float m = style.outerMargin;
        float y = areaBaseY + innerAreaLift;
        float sx = style.innerPadding - style.sectionPadding;
        outerTitle.Apply(style.outerArea, new Vector3(-m, areaBaseY, -GrowSign * m), GrowSign);   // sudut anchor: sisi berlawanan arah tumbuh, di kiri
        innerTitle.Apply(style.innerArea, new Vector3(0f, y, 0f), GrowSign);
        upperTitle.Apply(style.upperArea, new Vector3(sx, y * 2f, RowNear(0) - GrowSign * style.sectionPadding), GrowSign);
        lowerTitle.Apply(style.lowerArea, new Vector3(sx, y * 2f, RowNear(LowerSlot) - GrowSign * style.sectionPadding), GrowSign);
    }

    // Judul rebah di bidang dpz, di luar area, menempel pada sudut anchor (tidak ikut bergeser saat area mengembang)
    sealed class AreaTitle
    {
        readonly RectTransform rt;
        readonly TextMeshProUGUI text;

        public AreaTitle(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            rt = (RectTransform)go.transform;
            rt.localScale = Vector3.one * 0.001f;               // 1 satuan canvas = 1 mm
            rt.localRotation = Quaternion.Euler(90f, 0f, 0f);   // rebah, terbaca dari atas

            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(rt, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            text = tgo.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
        }

        public void Apply(DpzAreaStyle cfg, Vector3 corner, float growSign)
        {
            bool show = cfg.visible && !string.IsNullOrEmpty(cfg.title);
            rt.gameObject.SetActive(show);
            if (!show) return;

            bool above = growSign < 0f;    // area tumbuh ke bawah: judul di atas pojok kiri-atas
            rt.sizeDelta = new Vector2(400f, Mathf.Max(8f, cfg.titleSizeMM * 1.6f));
            rt.pivot = new Vector2(0f, above ? 0f : 1f);
            rt.localPosition = corner + new Vector3(cfg.titleOffset.x, 0f, (above ? 1f : -1f) * cfg.titleOffset.y);

            text.text = cfg.title;
            text.color = cfg.titleColor;
            text.fontSize = Mathf.Max(0.5f, cfg.titleSizeMM);
            text.alignment = above ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.TopLeft;
        }
    }

    // ---------- umpan balik hover (satu sumber kebenaran: debug zone dan zona dunia memanggil ini) ----------
    // Potongan dari bank: bagian bawah
    public void SetHover(Fraction piece, int blockUnderPointer = -1, bool overInnerArea = true)
    {
        if (!bound) return;

        HideUpperPreview();
        SetPredictedUpperExtra(false);

        bool ghostAllowed = style.ghostScope == GhostScope.AllAreas
                         || (style.ghostScope == GhostScope.InnerAreaOnly && overInnerArea);

        int target = session.FindDpzMergeTarget(piece, blockUnderPointer);
        if (target >= 0)
        {
            SetHighlightedBlock(target);                 // blok sejenis yang belum penuh: garis blok itu highlight, tanpa preview
            HidePreview();
            SetMergeFeedback(target, 1, ghostAllowed);   // sel berikutnya: isian prediksi (dan ghost satuan sesuai scope)
            return;
        }

        SetHighlightedBlock(-1);
        SetMergeFeedback(-1, 0, false);

        if (session.PreviewDropToDpz(piece, blockUnderPointer) == RejectReason.None)
            ShowPreview(piece, ghostAllowed);            // slot blok baru: track animasi, sel pertama prediksi, ghost satuan
        else
            HidePreview();                               // ditolak: tanpa preview
    }

    // Row Kotak yang diseret: bagian atas
    public void SetHoverKotak(Fraction piece, bool fromA, int rowIndex)
    {
        if (!bound) return;

        SetHighlightedBlock(-1);
        SetMergeFeedback(-1, 0, false);
        HidePreview();

        if (session.PreviewMoveToArea(fromA, rowIndex) == RejectReason.None)
        {
            ShowUpperPreview(piece);
            SetPredictedUpperExtra(true);
        }
        else
        {
            HideUpperPreview();
            SetPredictedUpperExtra(false);
        }
    }

    // Balok dpz yang diangkat (sourceIndex) sedang di atas balok dpz lain (targetIndex)
    public void SetHoverLifted(int sourceIndex, int targetIndex)
    {
        if (!bound) return;

        int count = session.PreviewMergeDpzBlocks(sourceIndex, targetIndex);   // hanya yang muat
        if (count <= 0) { ClearHover(); return; }                              // penyebut beda, penuh, atau balok yang sama

        bool ghostAllowed = style.ghostScope != GhostScope.Off;                // pointer di atas balok = area dalam
        HidePreview();
        HideUpperPreview();
        SetHighlightedBlock(targetIndex);
        SetMergeFeedback(targetIndex, count, ghostAllowed);                    // sebanyak sel yang akan masuk
        SetPredictedUpperExtra(false);
    }

    public void ClearHover()
    {
        SetHighlightedBlock(-1);
        SetMergeFeedback(-1, 0, false);
        HidePreview();
        HideUpperPreview();
        SetPredictedUpperExtra(false);
    }

    public void SetHighlightedBlock(int index)
    {
        if (index == highlightedBlock) return;

        if (highlightedBlock >= 0 && highlightedBlock < blocks.Count && blocks[highlightedBlock])
            blocks[highlightedBlock].SetHighlight(0f);

        highlightedBlock = index;
        if (index >= 0 && index < blocks.Count && blocks[index]) blocks[index].SetHighlight(1f);
    }

    // Umpan balik merge di blok target: unitCount sel berikutnya diberi isian prediksi, dan ghost kalau diizinkan
    void SetMergeFeedback(int blockIndex, int unitCount, bool ghostAllowed)
    {
        bool ghost = blockIndex >= 0 && ghostAllowed;
        if (blockIndex == mergeTarget && unitCount == mergeCount && ghost == mergeGhostOn) return;

        if (mergeTarget >= 0 && mergeTarget < blocks.Count && blocks[mergeTarget])
        {
            blocks[mergeTarget].HideGhostUnit();
            blocks[mergeTarget].SetPreviewRange(-1, 0);
        }

        mergeTarget = blockIndex;
        mergeCount = unitCount;
        mergeGhostOn = ghost;

        if (blockIndex >= 0 && blockIndex < blocks.Count && blocks[blockIndex])
        {
            DpzBlockView b = blocks[blockIndex];
            int first = (int)b.Piece.numerator;   // sel kosong pertama
            int count = Mathf.Max(1, unitCount);
            b.SetPreviewRange(first, count);
            if (ghost) b.ShowGhostUnits(first, count, (int)b.Piece.denominator,
                                        controller.Palette.Get(b.Piece.denominator), style.ghostAlpha);
        }
    }

    void ShowPreview(Fraction piece, bool ghostAllowed)
    {
        int row = blocks.Count;   // slot kosong berikutnya di bagian bawah
        if (previewShown && previewRow == row && previewGhostShown == ghostAllowed
            && previewPiece.numerator == piece.numerator && previewPiece.denominator == piece.denominator) return;

        if (!preview) { preview = CreateBlock(); preview.name = "DpzPreview"; }
        preview.gameObject.SetActive(true);

        Color c = controller.Palette.Get(piece.denominator);
        preview.ShowPreview(piece, c, LowerOrigin(row));
        if (ghostAllowed) preview.ShowGhostUnit(0, (int)piece.denominator, c, style.ghostAlpha);
        else preview.HideGhostUnit();

        previewShown = true; previewGhostShown = ghostAllowed; previewPiece = piece; previewRow = row;
    }

    void HidePreview()
    {
        if (!previewShown) return;
        previewShown = false;
        if (preview) { preview.HideGhostUnit(); preview.gameObject.SetActive(false); }
    }

    void ShowUpperPreview(Fraction piece)
    {
        int slot = UpperVisualCount;   // slot kosong berikutnya di bagian atas
        if (upperPreviewShown && upperPreviewSlot == slot
            && upperPreviewPiece.numerator == piece.numerator && upperPreviewPiece.denominator == piece.denominator) return;

        if (!upperPreview) { upperPreview = CreateBlock(DpzColliderKind.UpperBlock); upperPreview.name = "DpzUpperPreview"; }
        upperPreview.gameObject.SetActive(true);
        upperPreview.ShowPreview(piece, controller.Palette.Get(piece.denominator), BlockOrigin(slot));

        upperPreviewShown = true; upperPreviewPiece = piece; upperPreviewSlot = slot;
    }

    void HideUpperPreview()
    {
        if (!upperPreviewShown) return;
        upperPreviewShown = false;
        if (upperPreview) upperPreview.gameObject.SetActive(false);
    }

    [ContextMenu("Debug: Refresh Visuals")]   // klik setelah mengubah DpzStyle saat Play
    void DebugRefreshVisuals()
    {
        foreach (var b in blocks) if (b) b.InvalidateVisual();
        foreach (var b in upperBlocks) if (b) b.InvalidateVisual();
        //foreach (var b in carryBlocks) if (b) b.InvalidateVisual();
        if (!bound) return;
        RefreshUpper();
        RefreshLower();
        ApplyAreaVisuals();
    }

    [ContextMenu("Debug: Toggle highlight blok 0")]   // tes highlight tanpa perlu drag
    void DebugToggleHighlight()
    {
        if (blocks.Count == 0) { Debug.Log("[Dpz] belum ada blok bawah", this); return; }
        SetHighlightedBlock(highlightedBlock == 0 ? -1 : 0);
        Debug.Log($"[Dpz] {blocks[0].DebugInfo()}", this);
    }

    [ContextMenu("Debug: Cetak status")]
    void DebugPrintState() =>
        Debug.Log($"[Dpz] bound={bound} atas={upperBlocks.Count} bawah={blocks.Count} barisAtas={layoutUpperRows} carry={session.Composed.Count} " +
                  $"dpzMask={dpzMask.value} layer={DpzLayer} outerCol={(outerCol != null)} innerCol={(innerCol != null)}", this);
}