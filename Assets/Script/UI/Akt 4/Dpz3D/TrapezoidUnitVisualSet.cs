using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "AR Pecahan/Unit Visual/Trapezoid")]
public class TrapezoidUnitVisualSet : UnitVisualSet
{
    [SerializeField] Material cellMaterial;     // shader ARPecahan/UnitCell
    [SerializeField] Material ghostMaterial;    // shader ARPecahan/UnitCellGhost

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int HighlightId = Shader.PropertyToID("_Highlight");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static MaterialPropertyBlock mpb;

    static GameObject Build(string name, Transform parent, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    public override GameObject Create(Transform parent)
    {
        if (cellMaterial == null) Debug.LogError($"[{name}] cellMaterial belum diisi", this);
        return Build("Unit", parent, cellMaterial);
    }

    public override GameObject CreateGhost(Transform parent)
    {
        if (ghostMaterial == null) Debug.LogError($"[{name}] ghostMaterial belum diisi", this);
        var go = Build("UnitGhost", parent, ghostMaterial);
        go.GetComponent<MeshRenderer>().sortingOrder = 5;   // digambar setelah garis lantai dan area
        return go;
    }

    public override void Configure(GameObject unit, in UnitCellSpec spec, Color color)
    {
        SetMesh(unit, spec);
        var mr = unit.GetComponent<MeshRenderer>();
        mpb ??= new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        mpb.SetColor(ColorId, color);
        mpb.SetFloat(HighlightId, 0f);
        mr.SetPropertyBlock(mpb);
    }

    public override void SetHighlight(GameObject unit, float amount)
    {
        var mr = unit.GetComponent<MeshRenderer>();
        mpb ??= new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        mpb.SetFloat(HighlightId, amount);
        mr.SetPropertyBlock(mpb);
    }

    public override void ConfigureGhost(GameObject unit, in UnitCellSpec spec, Color color, float alpha)
    {
        SetMesh(unit, spec);
        var mr = unit.GetComponent<MeshRenderer>();
        mpb ??= new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        mpb.SetColor(ColorId, color);
        mpb.SetFloat(AlphaId, alpha);
        mr.SetPropertyBlock(mpb);
    }

    public override void SetGhostAlpha(GameObject unit, float alpha)
    {
        var mr = unit.GetComponent<MeshRenderer>();
        mpb ??= new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        mpb.SetFloat(AlphaId, alpha);
        mr.SetPropertyBlock(mpb);
    }

    static void SetMesh(GameObject unit, in UnitCellSpec spec) =>
        unit.GetComponent<MeshFilter>().sharedMesh =
            DpzMeshes.TrapezoidPrism(spec.length, spec.thickness, spec.height, spec.inset);
}