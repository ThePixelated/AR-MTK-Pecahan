using UnityEngine;

// Ghost semi-transparan sebesar satu satuan yang mengikuti jari di atas bidang dpz saat ghost berada di luar HUD
public class HeldGhost3D : MonoBehaviour
{
    [SerializeField] DpzView dpzView;          // acuan bidang dan orientasi
    [SerializeField] DpzStyle style;
    [SerializeField] UnitVisualSet visuals;    // objek ini sebaiknya tidak di-scale

    GameObject unit;
    Vector3 centerOffset;

    public void Begin(Fraction piece, Color color)
    {
        if (!unit) unit = visuals.CreateGhost(transform);

        float pitch = style.wholeLength / piece.denominator;
        float len = Mathf.Max(0.0001f, pitch - style.cellGap);
        var spec = new UnitCellSpec(len, style.thickness, style.height, style.inset);

        visuals.ConfigureGhost(unit, spec, color, style.heldGhostAlpha);
        centerOffset = new Vector3(len * 0.5f, 0f, -style.thickness * 0.5f);   // origin sel = sudut kiri-depan-bawah
        unit.SetActive(false);
    }

    // Letakkan di titik potong ray layar dengan bidang dpz; false kalau tidak kena
    public bool TryPlace(Vector2 screenPos, Camera cam)
    {
        if (!unit || cam == null) return false;

        Transform r = dpzView.transform;
        var plane = new Plane(r.up, r.position);
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!plane.Raycast(ray, out float dist)) { unit.SetActive(false); return false; }

        Vector3 point = ray.GetPoint(dist) + r.up * style.heldGhostLift;
        unit.transform.SetPositionAndRotation(point - r.rotation * centerOffset, r.rotation);
        unit.SetActive(true);
        return true;
    }

    public void Hide() { if (unit && unit.activeSelf) unit.SetActive(false); }
}