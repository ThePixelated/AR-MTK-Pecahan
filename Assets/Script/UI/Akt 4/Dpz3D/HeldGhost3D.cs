using System.Collections.Generic;
using UnityEngine;

// Ghost semi-transparan yang mengikuti jari di atas bidang dpz saat ghost berada di luar HUD.
// Satu satuan (dari bank) atau satu blok n/d (blok dpz yang diangkat).
public class HeldGhost3D : MonoBehaviour
{
    [SerializeField] DpzView dpzView;          // acuan bidang dan orientasi
    [SerializeField] DpzStyle style;
    [SerializeField] UnitVisualSet visuals;    // objek ini sebaiknya tidak di-scale

    Transform root;
    readonly List<GameObject> cells = new List<GameObject>();
    Vector3 centerOffset;

    void EnsureRoot()
    {
        if (root) return;
        root = new GameObject("HeldGhost").transform;
        root.SetParent(transform, false);
        root.gameObject.SetActive(false);
    }

    public void Begin(Fraction piece, Color color)
    {
        EnsureRoot();

        int n = Mathf.Max(1, (int)piece.numerator);
        int d = Mathf.Max(1, (int)piece.denominator);
        float pitch = style.wholeLength / d;
        float len = Mathf.Max(0.0001f, pitch - style.cellGap);
        var spec = new UnitCellSpec(len, style.thickness, style.height, style.inset);

        while (cells.Count < n) cells.Add(visuals.CreateGhost(root));

        for (int i = 0; i < cells.Count; i++)
        {
            bool on = i < n;
            cells[i].SetActive(on);
            if (!on) continue;

            cells[i].transform.localPosition = new Vector3(i * pitch + style.cellGap * 0.5f, 0f, 0f);
            visuals.ConfigureGhost(cells[i], spec, color, style.heldGhostAlpha);
        }

        float total = n * pitch - style.cellGap;
        centerOffset = new Vector3(total * 0.5f, 0f, -style.thickness * 0.5f);   // origin sel = sudut kiri-depan-bawah
        root.gameObject.SetActive(false);                                        // baru tampil saat TryPlace berhasil
    }

    // Letakkan di titik potong ray layar dengan bidang dpz; false kalau tidak kena
    public bool TryPlace(Vector2 screenPos, Camera cam)
    {
        if (!root || cam == null) return false;

        Transform r = dpzView.transform;
        var plane = new Plane(r.up, r.position);
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!plane.Raycast(ray, out float dist)) { root.gameObject.SetActive(false); return false; }

        Vector3 point = ray.GetPoint(dist) + r.up * style.heldGhostLift;
        root.SetPositionAndRotation(point - r.rotation * centerOffset, r.rotation);
        root.gameObject.SetActive(true);
        return true;
    }

    public void Hide() { if (root && root.gameObject.activeSelf) root.gameObject.SetActive(false); }
}