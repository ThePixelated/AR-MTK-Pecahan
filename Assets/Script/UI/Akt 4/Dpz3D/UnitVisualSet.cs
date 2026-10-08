using UnityEngine;

public readonly struct UnitCellSpec
{
    public readonly float length, thickness, height, inset;
    public UnitCellSpec(float length, float thickness, float height, float inset)
    { this.length = length; this.thickness = thickness; this.height = height; this.inset = inset; }
}

// Logika (session, layout, merge, collider) tidak peduli bentuk satuan; bentuknya ditukar lewat asset ini.
public abstract class UnitVisualSet : ScriptableObject
{
    // Origin lokal = sudut kiri-depan-bawah sel; sel memanjang ke +X dan -Z
    public abstract GameObject Create(Transform parent);
    public abstract void Configure(GameObject unit, in UnitCellSpec spec, Color color);
    public abstract void SetHighlight(GameObject unit, float amount);
}