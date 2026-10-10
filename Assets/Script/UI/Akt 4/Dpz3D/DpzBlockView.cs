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

    GameObject ghostUnit;
    bool ghostActive;
    float ghostBaseAlpha;
    float ghostPhase;

    Color blockColor = Color.white;
    float highlight;
    float phase;
    float animSpeed;
    bool animating;
    bool hasPiece;
    bool isPreview;
    float previewFilledAlpha;

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

    void SizeCollider()
    {
        float h = owner.ColliderHeight;
        boxCollider.center = new Vector3(style.wholeLength * 0.5f, h * 0.5f, -style.thickness * 0.5f);
        boxCollider.size = new Vector3(style.wholeLength, h, style.thickness);
    }

    public void Show(Fraction piece, int index, Color color, Vector3 localPosition)
    {
        Index = index;
        isPreview = false;
        colliderTag.blockIndex = index;
        boxCollider.enabled = true;
        transform.localPosition = localPosition;

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
            bool on = i < n;
            if (u.activeSelf != on) u.SetActive(on);
            if (!on) continue;

            u.transform.localPosition = new Vector3(i * pitch + style.cellGap * 0.5f, style.baseLift, 0f);
            visuals.Configure(u, spec, color);
            visuals.SetHighlight(u, highlight);
        }

        track.localPosition = new Vector3(0f, style.baseLift, 0f);
        track.localScale = new Vector3(style.wholeLength, 1f, style.thickness);
        ApplyTrack();
    }

    // Preview slot blok baru: hanya garis lantai (d sel), selalu highlight; tanpa collider supaya tidak tertangkap raycast
    public void ShowPreview(Fraction piece, Color color, Vector3 localPosition, float filledAlpha)
    {
        transform.localPosition = localPosition;
        boxCollider.enabled = false;
        foreach (var u in units) if (u.activeSelf) u.SetActive(false);

        Piece = piece; blockColor = color;
        hasPiece = false;                      // kalau dipakai Show lagi, gambar ulang penuh
        isPreview = true;
        previewFilledAlpha = filledAlpha;
        highlight = 1f;

        track.localPosition = new Vector3(0f, style.baseLift, 0f);
        track.localScale = new Vector3(style.wholeLength, 1f, style.thickness);
        ApplyTrack();
    }

    // Ghost satuan semi-transparan di sel cellIndex (0-based) pada track berpenyebut denominator
    public void ShowGhostUnit(int cellIndex, int denominator, Color color, float alpha)
    {
        if (!ghostUnit) ghostUnit = visuals.CreateGhost(transform);

        float pitch = style.wholeLength / denominator;
        var spec = new UnitCellSpec(Mathf.Max(0.0001f, pitch - style.cellGap), style.thickness, style.height, style.inset);

        ghostUnit.SetActive(true);
        ghostUnit.transform.localPosition = new Vector3(cellIndex * pitch + style.cellGap * 0.5f, style.baseLift, 0f);
        visuals.ConfigureGhost(ghostUnit, spec, style.ghostForceColor ? style.ghostColor : color, alpha);

        ghostActive = true;
        ghostBaseAlpha = alpha;
        ghostPhase = 0f;
    }

    public void HideGhostUnit()
    {
        ghostActive = false;
        if (ghostUnit && ghostUnit.activeSelf) ghostUnit.SetActive(false);
    }

    public void SetHighlight(float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Approximately(amount, highlight)) return;
        highlight = amount;

        foreach (var u in units) if (u.activeSelf) visuals.SetHighlight(u, highlight);
        if (hasPiece || isPreview) ApplyTrack();
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
        mpb.SetFloat(FilledId, Piece.numerator);
        mpb.SetFloat(LengthId, style.wholeLength);
        mpb.SetFloat(WidthId, style.thickness);
        mpb.SetColor(LineColorId, line);
        mpb.SetFloat(LineWidthId, style.lineWidth);
        mpb.SetFloat(StyleId, (float)style.lineStyle);
        mpb.SetFloat(DashLenId, style.dashLength);
        mpb.SetFloat(DashGapId, style.dashGap);
        mpb.SetFloat(TexTileId, Mathf.Max(0.0001f, style.textureTile));
        mpb.SetFloat(EmptyAlphaId, style.emptyCellAlpha);
        mpb.SetFloat(FilledAlphaId, isPreview ? previewFilledAlpha : 0f);
        mpb.SetFloat(PhaseId, phase);
        mpb.SetTexture(LineTexId, style.lineTexture != null ? style.lineTexture : Texture2D.whiteTexture);
        trackRenderer.SetPropertyBlock(mpb);
    }

    void Update()
    {
        if (animating)
        {
            phase += animSpeed * Time.unscaledDeltaTime;
            trackRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(PhaseId, phase);
            trackRenderer.SetPropertyBlock(mpb);
        }

        if (ghostActive && ghostUnit && style.ghostPulseSpeed > 0.001f)
        {
            ghostPhase += Time.unscaledDeltaTime * style.ghostPulseSpeed * Mathf.PI * 2f;
            float k = 0.5f + 0.5f * Mathf.Sin(ghostPhase);
            visuals.SetGhostAlpha(ghostUnit, ghostBaseAlpha * (1f - style.ghostPulseAmount * k));
        }
    }
}