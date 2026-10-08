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
    static readonly int LineTexId = Shader.PropertyToID("_LineTex");

    public Fraction Piece { get; private set; }
    public int Index { get; private set; }

    DpzStyle style;
    UnitVisualSet visuals;
    Transform track;
    MeshRenderer trackRenderer;
    MaterialPropertyBlock mpb;
    readonly List<GameObject> units = new List<GameObject>();

    Color blockColor = Color.white;
    float highlight;
    float phase;
    float animSpeed;
    bool animating;
    bool hasPiece;

    public void Init(DpzStyle style, UnitVisualSet visuals, Material decalMaterial)
    {
        this.style = style;
        this.visuals = visuals;
        mpb = new MaterialPropertyBlock();

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

    public void Show(Fraction piece, int index, Color color, Vector3 localPosition)
    {
        Index = index;
        transform.localPosition = localPosition;

        bool same = hasPiece && piece.numerator == Piece.numerator
                             && piece.denominator == Piece.denominator && color == blockColor;
        Piece = piece; blockColor = color; hasPiece = true;
        if (same) return;

        int n = (int)piece.numerator;        // jumlah satuan terisi
        int d = (int)piece.denominator;      // jumlah sel di track
        float pitch = style.wholeLength / d;
        float cellLength = Mathf.Max(0.0001f, pitch - style.cellGap);   // panjang blok n/d = n * pitch - g, selalu sama untuk nilai yang sama
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
            visuals.SetHighlight(u, highlight);          // pertahankan highlight yang sedang aktif
        }

        track.localPosition = new Vector3(0f, style.baseLift, 0f);
        track.localScale = new Vector3(style.wholeLength, 1f, style.thickness);
        ApplyTrack();
    }

    public void SetHighlight(float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Approximately(amount, highlight)) return;
        highlight = amount;

        foreach (var u in units) if (u.activeSelf) visuals.SetHighlight(u, highlight);
        if (hasPiece) ApplyTrack();
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
        mpb.SetFloat(PhaseId, phase);
        mpb.SetTexture(LineTexId, style.lineTexture != null ? style.lineTexture : Texture2D.whiteTexture);
        trackRenderer.SetPropertyBlock(mpb);
    }

    void Update()
    {
        if (!animating) return;
        phase += animSpeed * Time.unscaledDeltaTime;
        trackRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(PhaseId, phase);
        trackRenderer.SetPropertyBlock(mpb);
    }
}