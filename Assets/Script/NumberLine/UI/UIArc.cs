using UnityEngine;
using UnityEngine.UI;

// Busur setengah elips untuk UI: pita dengan ketebalan tetap (piksel), opsional putus-putus.
// Pivot (0.5, 0): dasar busur di tepi bawah rect, puncaknya di tepi atas.
public class UIArc : MaskableGraphic
{
    [SerializeField] float thickness = 4f;
    [SerializeField] bool dashed;
    [SerializeField] float dashLength = 10f;
    [SerializeField] float gapLength = 6f;

    public void Configure(float newThickness, bool newDashed, float newDashLength, float newGapLength)
    {
        if (Mathf.Approximately(thickness, newThickness) && dashed == newDashed &&
            Mathf.Approximately(dashLength, newDashLength) && Mathf.Approximately(gapLength, newGapLength)) return;

        thickness = newThickness;
        dashed = newDashed;
        dashLength = newDashLength;
        gapLength = newGapLength;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        float rx = rect.width * 0.5f;
        float ry = rect.height;
        if (rx <= 0.5f || ry <= 0.5f || thickness <= 0f) return;

        // jumlah segmen mengikuti panjang busur (kira-kira satu segmen per 3 piksel)
        float estimate = Mathf.PI * Mathf.Sqrt((rx * rx + ry * ry) * 0.5f);
        int n = Mathf.Clamp(Mathf.CeilToInt(estimate / 3f), 12, 128);

        // titik-titik dari ujung kiri (sudut pi) ke ujung kanan (sudut 0); pusat elips di dasar tengah rect
        Vector2 center = new Vector2(rect.center.x, rect.yMin);
        var points = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float angle = Mathf.PI * (1f - (float)i / n);
            points[i] = center + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
        }

        // normal tiap titik dari arah garis singgung supaya pita menyambung mulus
        var normals = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            Vector2 tangent = points[Mathf.Min(i + 1, n)] - points[Mathf.Max(i - 1, 0)];
            tangent = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector2.right;
            normals[i] = new Vector2(-tangent.y, tangent.x);
        }

        // panjang kumulatif sepanjang busur untuk menentukan dash
        var length = new float[n + 1];
        for (int i = 1; i <= n; i++) length[i] = length[i - 1] + Vector2.Distance(points[i - 1], points[i]);

        float half = thickness * 0.5f;
        float period = dashLength + gapLength;

        for (int i = 0; i < n; i++)
        {
            if (dashed && period > 0.01f)
            {
                float middle = (length[i] + length[i + 1]) * 0.5f;
                if (middle % period >= dashLength) continue;       // bagian jeda
            }

            int b = vh.currentVertCount;
            AddVertex(vh, points[i] + normals[i] * half);
            AddVertex(vh, points[i] - normals[i] * half);
            AddVertex(vh, points[i + 1] + normals[i + 1] * half);
            AddVertex(vh, points[i + 1] - normals[i + 1] * half);
            vh.AddTriangle(b, b + 1, b + 3);
            vh.AddTriangle(b, b + 3, b + 2);
        }
    }

    void AddVertex(VertexHelper vh, Vector2 position)
    {
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = position;
        vh.AddVert(v);
    }
}