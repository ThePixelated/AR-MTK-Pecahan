using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

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

    // Sumbu lokal: baris memanjang ke +X, baris berikutnya ke -Z (atau +Z). Origin = sudut anchor area dalam.
    FractionSession session;
    bool bound;
    readonly List<DpzBlockView> blocks = new List<DpzBlockView>();
    int highlightedBlock = -1;
    int mergeGhostBlock = -1;
    bool predictedExtra;
    Vector2 innerSize, targetInnerSize;               // (lebar X, kedalaman Z)

    BoxCollider outerCol, innerCol;
    MaterialPropertyBlock areaMpb;
    AreaTitle outerTitle, innerTitle;

    DpzBlockView preview;
    bool previewShown, previewGhostShown;
    Fraction previewPiece;
    int previewRow;

    float GrowSign => growTowardPositiveZ ? 1f : -1f;
    int DpzLayer
    {
        get
        {
            int v = dpzMask.value;
            return v == 0 ? gameObject.layer : Mathf.RoundToInt(Mathf.Log(v & -v, 2f));   // layer terendah di mask
        }
    }

    public float ColliderHeight => colliderHeight;
    public int BlockCount => blocks.Count;

    void OnEnable() => Bind();
    void OnDisable() { ClearHover(); Unbind(); }

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
        session.Dpz.Changed += Refresh;
        bound = true;

        outerCol ??= CreateAreaCollider("OuterCatch", DpzColliderKind.Outer);
        innerCol ??= CreateAreaCollider("InnerCatch", DpzColliderKind.Inner);

        Refresh();
        ApplyAreaVisuals();
        innerSize = targetInnerSize;   // tampil langsung dengan ukuran yang benar, tanpa animasi awal
        ApplyAreas();
    }

    void Unbind()
    {
        if (!bound) return;
        session.Dpz.Changed -= Refresh;
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

    // Data -> tampilan. Blok dipakai ulang; hanya tambah atau kurangi di ujung.
    void Refresh()
    {
        HidePreview();
        SetMergeGhost(-1, false);
        predictedExtra = false;

        var pieces = session.Dpz.Pieces;
        int count = pieces.Count;

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
            blocks[i].Show(p, i, controller.Palette.Get(p.denominator), BlockOrigin(i));
            blocks[i].SetHighlight(i == highlightedBlock ? 1f : 0f);
        }

        if (highlightedBlock >= count) highlightedBlock = -1;
        RecomputeTarget();
    }

    // baris area = max(minRows, jumlah blok, min(jumlah blok + minEmptyRows, batas blok))
    int ComputeRows(int count)
    {
        int withEmpty = Mathf.Min(count + style.minEmptyRows, GameRules.MaxDpzBlocks);
        return Mathf.Max(style.minRows, Mathf.Max(count, withEmpty));
    }

    // Saat hover yang bakal menambah blok dan toggle aktif, area langsung dihitung dengan satu blok lebih banyak
    void RecomputeTarget()
    {
        int count = blocks.Count + ((style.areaPredictsOnHover && predictedExtra) ? 1 : 0);
        targetInnerSize = ComputeInnerSize(ComputeRows(count));
    }

    void SetPredictedExtra(bool value)
    {
        if (predictedExtra == value) return;
        predictedExtra = value;
        RecomputeTarget();
    }

    DpzBlockView CreateBlock()
    {
        var go = new GameObject("DpzBlock");
        go.transform.SetParent(transform, false);
        var b = go.AddComponent<DpzBlockView>();
        b.Init(style, visuals, decalMaterial, this, DpzLayer);
        return b;
    }

    Vector2 ComputeInnerSize(int rows)
    {
        float w = style.wholeLength + 2f * style.innerPadding;
        float d = rows * style.thickness + (rows - 1) * style.rowGap + 2f * style.innerPadding;
        return new Vector2(w, d);
    }

    // Posisi lokal origin blok ke-i. Blok memanjang ke -Z dari origin-nya, jadi arah +Z digeser setebal blok.
    Vector3 BlockOrigin(int i)
    {
        float pitch = style.thickness + style.rowGap;
        float z = GrowSign < 0f
            ? -(style.innerPadding + i * pitch)
            : style.innerPadding + i * pitch + style.thickness;
        return new Vector3(style.innerPadding, 0f, z);
    }

    void Update()
    {
        if (!bound) return;
        if ((innerSize - targetInnerSize).sqrMagnitude < 1e-12f) return;

        float k = 1f - Mathf.Exp(-areaSmoothing * Time.unscaledDeltaTime);
        innerSize = Vector2.Lerp(innerSize, targetInnerSize, k);
        if ((innerSize - targetInnerSize).sqrMagnitude < 1e-10f) innerSize = targetInnerSize;
        ApplyAreas();
    }

    // Sudut anchor kedua area tetap; hanya sisi kanan dan sisi arah tumbuh yang bergerak
    void ApplyAreas()
    {
        Vector2 outer = innerSize + Vector2.one * (2f * style.outerMargin);
        float cx = innerSize.x * 0.5f;
        float cz = GrowSign * innerSize.y * 0.5f;

        PlaceArea(outerArea, outer, cx, cz, areaBaseY);
        PlaceArea(innerArea, innerSize, cx, cz, areaBaseY + innerAreaLift);
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

    // ---------- raycast dunia: prioritas Block > Inner > Outer ----------
    public bool Raycast(Vector2 screenPos, Camera cam, out DpzHit result)
    {
        result = default;
        if (!bound || cam == null || dpzMask.value == 0) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        int n = Physics.RaycastNonAlloc(ray, hitBuffer, raycastMaxDistance, dpzMask, QueryTriggerInteraction.Collide);

        int bestPriority = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (!hitBuffer[i].collider.TryGetComponent(out DpzCollider tag) || tag.owner != this) continue;

            int priority = tag.kind == DpzColliderKind.Block ? 2 : (tag.kind == DpzColliderKind.Inner ? 1 : 0);
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

    public RejectReason TryDrop(Fraction piece, int blockIndex = -1) =>
        bound ? session.TryDropToDpz(piece, blockIndex) : RejectReason.SoalBelumDibentuk;

    // ---------- shader area dan judul ----------
    void ApplyAreaVisuals()
    {
        ApplyAreaMaterial(outerArea, style.outerArea, -20);   // transparan: luar digambar dulu, lalu dalam
        ApplyAreaMaterial(innerArea, style.innerArea, -10);

        outerTitle ??= new AreaTitle(transform, "OuterTitle");
        innerTitle ??= new AreaTitle(transform, "InnerTitle");

        float m = style.outerMargin;
        Vector3 outerCorner = new Vector3(-m, areaBaseY, -GrowSign * m);   // sudut anchor: sisi berlawanan arah tumbuh, di kiri
        Vector3 innerCorner = new Vector3(0f, areaBaseY + innerAreaLift, 0f);
        outerTitle.Apply(style.outerArea, outerCorner, GrowSign);
        innerTitle.Apply(style.innerArea, innerCorner, GrowSign);
    }

    void ApplyAreaMaterial(Transform area, DpzAreaStyle cfg, int sortingOrder)
    {
        if (!area) return;
        var mr = area.GetComponent<MeshRenderer>();
        if (!mr) return;

        areaMpb ??= new MaterialPropertyBlock();
        mr.GetPropertyBlock(areaMpb);
        areaMpb.SetColor(BorderColorId, cfg.borderColor);
        areaMpb.SetFloat(BorderThicknessId, cfg.borderThickness);
        areaMpb.SetColor(FillColorId, cfg.fillColor);
        areaMpb.SetColor(HatchColorId, cfg.hatchColor);
        areaMpb.SetFloat(HatchEnabledId, cfg.hatch ? 1f : 0f);
        areaMpb.SetFloat(HatchAngleId, cfg.hatchAngle);
        areaMpb.SetFloat(HatchSpacingId, cfg.hatchSpacing);
        areaMpb.SetFloat(HatchWidthId, cfg.hatchWidth);
        areaMpb.SetFloat(HatchDashedId, cfg.hatchDashed ? 1f : 0f);
        areaMpb.SetFloat(HatchDashLengthId, cfg.hatchDashLength);
        areaMpb.SetFloat(HatchDashGapId, cfg.hatchDashGap);
        areaMpb.SetFloat(MeshUnitId, areaMeshUnit);
        areaMpb.SetFloat(AnchorTopId, GrowSign < 0f ? 1f : 0f);
        mr.SetPropertyBlock(areaMpb);

        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
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
            bool show = !string.IsNullOrEmpty(cfg.title);
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
    public void SetHover(Fraction piece, int blockUnderPointer = -1, bool overInnerArea = true)
    {
        if (!bound) return;

        bool ghostAllowed = style.ghostScope == GhostScope.AllAreas
                         || (style.ghostScope == GhostScope.InnerAreaOnly && overInnerArea);

        int target = session.FindDpzMergeTarget(piece, blockUnderPointer);
        if (target >= 0)
        {
            SetHighlightedBlock(target);        // blok sejenis yang belum penuh: highlight blok itu, tanpa preview
            HidePreview();
            SetMergeGhost(target, ghostAllowed);   // sel berikutnya tampil sebagai ghost: blok terlihat penuh
            SetPredictedExtra(false);
            return;
        }

        SetHighlightedBlock(-1);
        SetMergeGhost(-1, false);

        if (session.PreviewDropToDpz(piece, blockUnderPointer) == RejectReason.None)
        {
            ShowPreview(piece, ghostAllowed);   // slot blok baru: track animasi plus ghost satuan
            SetPredictedExtra(true);
        }
        else
        {
            HidePreview();                      // ditolak: tanpa preview
            SetPredictedExtra(false);
        }
    }

    public void ClearHover()
    {
        SetHighlightedBlock(-1);
        SetMergeGhost(-1, false);
        HidePreview();
        SetPredictedExtra(false);
    }

    public void SetHighlightedBlock(int index)
    {
        if (index == highlightedBlock) return;

        if (highlightedBlock >= 0 && highlightedBlock < blocks.Count && blocks[highlightedBlock])
            blocks[highlightedBlock].SetHighlight(0f);

        highlightedBlock = index;
        if (index >= 0 && index < blocks.Count && blocks[index]) blocks[index].SetHighlight(1f);
    }

    void SetMergeGhost(int blockIndex, bool allowed)
    {
        int want = allowed ? blockIndex : -1;
        if (want == mergeGhostBlock) return;

        if (mergeGhostBlock >= 0 && mergeGhostBlock < blocks.Count && blocks[mergeGhostBlock])
            blocks[mergeGhostBlock].HideGhostUnit();

        mergeGhostBlock = want;
        if (want >= 0 && want < blocks.Count && blocks[want])
        {
            DpzBlockView b = blocks[want];
            b.ShowGhostUnit((int)b.Piece.numerator, (int)b.Piece.denominator,
                            controller.Palette.Get(b.Piece.denominator), style.ghostAlpha);
        }
    }

    void ShowPreview(Fraction piece, bool ghostAllowed)
    {
        int row = blocks.Count;   // baris kosong berikutnya
        if (previewShown && previewRow == row && previewGhostShown == ghostAllowed
            && previewPiece.numerator == piece.numerator && previewPiece.denominator == piece.denominator) return;

        if (!preview) { preview = CreateBlock(); preview.name = "DpzPreview"; }
        preview.gameObject.SetActive(true);

        Color c = controller.Palette.Get(piece.denominator);
        preview.ShowPreview(piece, c, BlockOrigin(row), style.previewFilledAlpha);
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

    [ContextMenu("Debug: Refresh Visuals")]   // klik setelah mengubah DpzStyle saat Play
    void DebugRefreshVisuals()
    {
        foreach (var b in blocks) if (b) b.InvalidateVisual();
        if (!bound) return;
        Refresh();
        ApplyAreaVisuals();
    }
}