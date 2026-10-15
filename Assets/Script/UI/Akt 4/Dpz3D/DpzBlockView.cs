using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DpzBlockView : MonoBehaviour
{
    static readonly int DenomId = Shader.PropertyToID("_Denominator");
    static readonly int FilledId = Shader.PropertyToID("_Filled");
    static readonly int LengthId = Shader.PropertyToID("_TrackLength");
    static readonly int WidthId = Shader.PropertyToID("_TrackWidth");
    static readonly int LineColorId = Shader.PropertyToID("_LineColor");
    static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
    static readonly int StyleId = Shader.PropertyToID("_Style");
    static readonly int DashLenId = Shader.PropertyToID("_DashLength");
    static readonly int DashGapId = Shader.PropertyToID("_DashGap");
    static readonly int TexTileId = Shader.PropertyToID("_TexTile");
    static readonly int PhaseId = Shader.PropertyToID("_Phase");
    static readonly int EmptyAlphaId = Shader.PropertyToID("_EmptyAlpha");
    static readonly int FilledAlphaId = Shader.PropertyToID("_FilledAlpha");
    static readonly int PreviewCellId = Shader.PropertyToID("_PreviewCell");
    static readonly int PreviewCountId = Shader.PropertyToID("_PreviewCount");
    static readonly int LineTexId = Shader.PropertyToID("_LineTex");

    public Fraction Piece { get; private set; }
    public int Index { get; private set; }

    DpzStyle style;
    UnitVisualSet visuals;
    DpzView owner;
    Transform track;
    MeshRenderer trackRenderer;
    MaterialPropertyBlock mpb;
    BoxCollider boxCollider;
    DpzCollider colliderTag;
    readonly List<GameObject> units = new List<GameObject>();

    readonly List<GameObject> ghostUnits = new List<GameObject>();
    int ghostCount;
    bool ghostActive;
    float ghostBaseAlpha;
    float ghostPhase;

    Color blockColor = Color.white;
    float highlight;       // efektif = terbesar dari hover, terpilih, diangkat
    float hoverAmount;     // dari hover (DpzView.SetHighlightedBlock)
    bool selected;         // dari tap-select
    bool lifted;           // sedang diangkat
    float phase;
    float animSpeed;
    bool animating;
    bool hasPiece;
    bool isPreview;
    Vector3 desiredPos;    // posisi tujuan (lokal); transform meluncur ke sini saat reflow
    bool placed;           // sudah pernah diletakkan (kemunculan pertama langsung di tempat)
    int previewCell = -1;
    int previewCount = 1;

    public void Init(DpzStyle style, UnitVisualSet visuals, Material decalMaterial, DpzView owner, int layer)
    {
        this.style = style;
        this.visuals = visuals;
        this.owner = owner;
        mpb = new MaterialPropertyBlock();

        gameObject.layer = layer;
        boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        colliderTag = gameObject.AddComponent<DpzCollider>();
        colliderTag.owner = owner;
        colliderTag.kind = DpzColliderKind.Block;

        var go = new GameObject("Track");
        track = go.transform;
        track.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = DpzMeshes.TrackQuad();
        trackRenderer = go.AddComponent<MeshRenderer>();
        trackRenderer.sharedMaterial = decalMaterial;
        trackRenderer.shadowCastingMode = ShadowCastingMode.Off;
        trackRenderer.receiveShadows = false;
    }

    // Paksa gambar ulang di Show berikutnya (setelah DpzStyle diubah)
    public void InvalidateVisual() => hasPiece = false;

    // Mulai meluncur dari posisi 'from' menuju posisi tujuan (animasi reflow setelah ada baris yang hilang)
    public void AnimateFrom(Vector3 from)
    {
        if (style.reflowSpeed <= 0f) return;
        transform.localPosition = from;
    }

    void SizeCollider()
    {
        float h = owner.ColliderHeight;
        boxCollider.center = new Vector3(style.wholeLength * 0.5f, h * 0.5f, -style.thickness * 0.5f);
        boxCollider.size = new Vector3(style.wholeLength, h, style.thickness);
    }

    float CellHighlight => style.highlightCells ? highlight : 0f;

    public void Show(Fraction piece, int index, Color color, Vector3 localPosition)
    {
        Index = index;
        isPreview = false;
        colliderTag.blockIndex = index;
        boxCollider.enabled = true;
        desiredPos = localPosition;
        if (!placed || style.reflowSpeed <= 0f) { transform.localPosition = localPosition; placed = true; }   // pertama kali atau tanpa animasi: langsung

        bool same = hasPiece && piece.numerator == Piece.numerator
                             && piece.denominator == Piece.denominator && color == blockColor;
        Piece = piece; blockColor = color; hasPiece = true;
        if (same) return;

        SizeCollider();

        int n = (int)piece.numerator;        // jumlah satuan terisi
        int d = (int)piece.denominator;      // jumlah sel di track
        float pitch = style.wholeLength / d;
        float cellLength = Mathf.Max(0.0001f, pitch - style.cellGap);   // panjang blok n/d = n * pitch - g
        var spec = new UnitCellSpec(cellLength, style.thickness, style.height, style.inset);

        while (units.Count < n) units.Add(visuals.Create(transform));

        for (int i = 0; i < units.Count; i++)
        {
            GameObject u = units[i];
            bool on = i < n && !lifted;
            if (u.activeSelf != on) u.SetActive(on);
            if (i >= n) continue;

            u.transform.localPosition = new Vector3(i * pitch + style.cellGap * 0.5f, style.baseLift, 0f);
            visuals.Configure(u, spec, color);
            visuals.SetHighlight(u, CellHighlight);
        }

        track.localPosition = new Vector3(0f, style.baseLift, 0f);
        track.localScale = new Vector3(style.wholeLength, 1f, style.thickness);
        ApplyTrack();
    }

    // Preview slot blok baru: hanya garis lantai (d sel); sel pertama ditandai prediksi. Tanpa collider.
    public void ShowPreview(Fraction piece, Color color, Vector3 localPosition)
    {
        desiredPos = localPosition;
        placed = true;
        transform.localPosition = localPosition;
        boxCollider.enabled = false;
        foreach (var u in units) if (u.activeSelf) u.SetActive(false);

        Piece = piece; blockColor = color;
        hasPiece = false;                      // kalau dipakai Show lagi, gambar ulang penuh
        isPreview = true;
        previewCell = 0;
        previewCount = 1;
        highlight = 1f;

        track.localPosition = new Vector3(0f, style.baseLift, 0f);
        track.localScale = new Vector3(style.wholeLength, 1f, style.thickness);
        ApplyTrack();
    }

    // Tandai rentang sel prediksi di track blok ini (startCell < 0 = tidak ada)
    public void SetPreviewRange(int startCell, int count)
    {
        if (startCell == previewCell && count == previewCount) return;
        previewCell = startCell;
        previewCount = count;
        if (hasPiece || isPreview) ApplyTrack();
    }

    public void SetPreviewCell(int cell) => SetPreviewRange(cell, cell < 0 ? 0 : 1);

    // Blok sedang diangkat: sel disembunyikan; garis lantainya highlight dan beranimasi sebagai tanda "balok ini yang diangkat"
    public void SetLifted(bool value)
    {
        if (lifted == value) return;
        lifted = value;

        if (hasPiece)
        {
            int n = (int)Piece.numerator;
            for (int i = 0; i < units.Count; i++)
            {
                bool on = i < n && !lifted;
                if (units[i].activeSelf != on) units[i].SetActive(on);
            }
        }
        ApplyEffectiveHighlight();
    }

    // Ghost satuan semi-transparan sebanyak count, mulai dari sel startCell (0-based), pada track berpenyebut denominator
    public void ShowGhostUnits(int startCell, int count, int denominator, Color color, float alpha)
    {
        count = Mathf.Max(1, count);
        while (ghostUnits.Count < count) ghostUnits.Add(visuals.CreateGhost(transform));

        float pitch = style.wholeLength / denominator;
        var spec = new UnitCellSpec(Mathf.Max(0.0001f, pitch - style.cellGap), style.thickness, style.height, style.inset);
        Color c = style.ghostForceColor ? style.ghostColor : color;

        for (int i = 0; i < ghostUnits.Count; i++)
        {
            GameObject g = ghostUnits[i];
            bool on = i < count;
            if (g.activeSelf != on) g.SetActive(on);
            if (!on) continue;

            g.transform.localPosition = new Vector3((startCell + i) * pitch + style.cellGap * 0.5f, style.baseLift, 0f);
            visuals.ConfigureGhost(g, spec, c, alpha);
        }

        ghostCount = count;
        ghostActive = true;
        ghostBaseAlpha = alpha;
        ghostPhase = 0f;
    }

    public void ShowGhostUnit(int cellIndex, int denominator, Color color, float alpha) =>
        ShowGhostUnits(cellIndex, 1, denominator, color, alpha);

    public void HideGhostUnit()
    {
        ghostActive = false;
        foreach (var g in ghostUnits) if (g.activeSelf) g.SetActive(false);
    }

    // Highlight dari hover. Garis lantai selalu ikut; sel ikut terang hanya kalau style.highlightCells aktif.
    public void SetHighlight(float amount)
    {
        hoverAmount = Mathf.Clamp01(amount);
        ApplyEffectiveHighlight();
    }

    public void SetSelected(bool value)
    {
        if (selected == value) return;
        selected = value;
        ApplyEffectiveHighlight();
    }

    void ApplyEffectiveHighlight()
    {
        float effective = Mathf.Max(hoverAmount, (selected || lifted) ? 1f : 0f);
        if (Mathf.Approximately(effective, highlight)) return;
        highlight = effective;

        if (style.highlightCells)
            foreach (var u in units) if (u.activeSelf) visuals.SetHighlight(u, highlight);
        if (hasPiece || isPreview) ApplyTrack();
    }

    public string DebugInfo()
    {
        int active = 0;
        foreach (var u in units) if (u.activeSelf) active++;
        return $"blok #{Index} {Piece.numerator}/{Piece.denominator} | highlight={highlight:0.00} | selected={selected} lifted={lifted} | highlightCells={style.highlightCells} | sel aktif={active}/{units.Count}";
    }

    void ApplyTrack()
    {
        Color line = style.forceLineColor ? style.forcedLineColor : blockColor;
        line = Color.Lerp(line, Color.white, highlight * style.highlightBrighten);

        bool highlighted = highlight > 0.001f;
        animSpeed = (style.animateOnlyWhenHighlighted && !highlighted) ? 0f : style.animationSpeed;
        animating = Mathf.Abs(animSpeed) > 0.0001f;

        trackRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(DenomId, Piece.denominator);
        mpb.SetFloat(FilledId, isPreview ? 0f : Piece.numerator);   // preview: tidak ada sel terisi, sel prediksi ditandai terpisah
        mpb.SetFloat(LengthId, style.wholeLength);
        mpb.SetFloat(WidthId, style.thickness);
        mpb.SetColor(LineColorId, line);
        mpb.SetFloat(LineWidthId, style.lineWidth);
        mpb.SetFloat(StyleId, (float)style.lineStyle);
        mpb.SetFloat(DashLenId, style.dashLength);
        mpb.SetFloat(DashGapId, style.dashGap);
        mpb.SetFloat(TexTileId, Mathf.Max(0.0001f, style.textureTile));
        mpb.SetFloat(EmptyAlphaId, style.emptyCellAlpha);
        mpb.SetFloat(FilledAlphaId, style.previewFilledAlpha);
        mpb.SetFloat(PreviewCellId, previewCell);
        mpb.SetFloat(PreviewCountId, previewCount);
        mpb.SetFloat(PhaseId, phase);
        mpb.SetTexture(LineTexId, style.lineTexture != null ? style.lineTexture : Texture2D.whiteTexture);
        trackRenderer.SetPropertyBlock(mpb);
    }

    void Update()
    {
        if (style.reflowSpeed > 0f && (transform.localPosition - desiredPos).sqrMagnitude > 1e-10f)
        {
            float k = 1f - Mathf.Exp(-style.reflowSpeed * Time.unscaledDeltaTime);
            Vector3 p = Vector3.Lerp(transform.localPosition, desiredPos, k);
            transform.localPosition = (p - desiredPos).sqrMagnitude < 1e-10f ? desiredPos : p;
        }

        if (animating)
        {
            phase += animSpeed * Time.unscaledDeltaTime;
            trackRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(PhaseId, phase);
            trackRenderer.SetPropertyBlock(mpb);
        }

        if (ghostActive && style.ghostPulseSpeed > 0.001f)
        {
            ghostPhase += Time.unscaledDeltaTime * style.ghostPulseSpeed * Mathf.PI * 2f;
            float k = 0.5f + 0.5f * Mathf.Sin(ghostPhase);
            float alpha = ghostBaseAlpha * (1f - style.ghostPulseAmount * k);
            for (int i = 0; i < ghostCount && i < ghostUnits.Count; i++) visuals.SetGhostAlpha(ghostUnits[i], alpha);
        }
    }
}