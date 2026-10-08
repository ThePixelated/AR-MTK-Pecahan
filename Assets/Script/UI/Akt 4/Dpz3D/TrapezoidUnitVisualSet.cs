using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "AR Pecahan/Unit Visual/Trapezoid")]
public class TrapezoidUnitVisualSet : UnitVisualSet
{
    [SerializeField] Material cellMaterial;     // material dengan shader ARPecahan/UnitCell

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int HighlightId = Shader.PropertyToID("_Highlight");
    static MaterialPropertyBlock mpb;

    public override GameObject Create(Transform parent)
    {
        if (cellMaterial == null) Debug.LogError($"[{name}] cellMaterial belum diisi", this);

        var go = new GameObject("Unit");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = cellMaterial;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    public override void Configure(GameObject unit, in UnitCellSpec spec, Color color)
    {
        unit.GetComponent<MeshFilter>().sharedMesh =
            DpzMeshes.TrapezoidPrism(spec.length, spec.thickness, spec.height, spec.inset);
        Write(unit, color, 0f, true);
    }

    public override void SetHighlight(GameObject unit, float amount) => Write(unit, default, amount, false);

    static void Write(GameObject unit, Color color, float highlight, bool setColor)
    {
        var mr = unit.GetComponent<MeshRenderer>();
        mpb ??= new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        if (setColor) mpb.SetColor(ColorId, color);
        mpb.SetFloat(HighlightId, highlight);
        mr.SetPropertyBlock(mpb);
    }
}