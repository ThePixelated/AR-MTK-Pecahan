using System.Collections.Generic;
using UnityEngine;

// Mesh badan cairan: profil rongga diputar mengelilingi sumbu. Radius dikecilkan (apotema x inset) supaya tidak menembus kaca.
public static class LiquidMeshBuilder
{
    public static Mesh Revolve(VesselProfile p, int segments, float radiusFactor)
    {
        var ring = new List<Vector2>();                       // (r, y); titik pertama = pusat lantai
        ring.Add(new Vector2(0f, p.floorY));
        foreach (ProfilePoint s in p.samples) ring.Add(new Vector2(s.r * radiusFactor, s.y));

        // tutup datar di bibir: supaya saat gelas miring jauh tidak ada ujung terbuka yang kelihatan tembus
        Vector2 top = ring[ring.Count - 1];
        ring.Add(top);                          // duplikat sudut supaya normal dinding dan normal tutup terpisah
        ring.Add(new Vector2(0f, top.y));       // pusat tutup

        int rows = ring.Count, cols = segments + 1;
        var verts = new Vector3[rows * cols];
        var norms = new Vector3[rows * cols];

        for (int j = 0; j < rows; j++)
        {
            Vector2 prev = ring[Mathf.Max(j - 1, 0)];
            Vector2 next = ring[Mathf.Min(j + 1, rows - 1)];
            Vector2 tan = next - prev;                                         // (dr, dy)
            Vector2 n2 = new Vector2(tan.y, -tan.x);                           // menunjuk keluar dari rongga
            n2 = n2.sqrMagnitude < 1e-12f ? Vector2.right : n2.normalized;

            for (int i = 0; i < cols; i++)
            {
                float ang = 2f * Mathf.PI * i / segments;
                float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                verts[j * cols + i] = new Vector3(p.axisXZ.x + ring[j].x * cs, ring[j].y, p.axisXZ.y + ring[j].x * sn);
                norms[j * cols + i] = new Vector3(n2.x * cs, n2.y, n2.x * sn);
            }
        }

        var tris = new List<int>(rows * segments * 6);
        for (int j = 0; j < rows - 1; j++)
        {
            for (int i = 0; i < segments; i++)
            {
                int a = j * cols + i, b = a + 1, c = (j + 1) * cols + i + 1, d = (j + 1) * cols + i;
                AddTri(tris, verts, norms, a, b, c);
                AddTri(tris, verts, norms, a, c, d);
            }
        }

        var mesh = new Mesh { name = "LiquidBody" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Cangkang tipis (tabung terbuka) untuk garis penyebut. radiusAt(y) = radius cangkang pada tinggi y.
    public static Mesh Shell(VesselProfile p, int segments, System.Func<float, float> radiusAt, float yFrom, float yTo, int rows = 24)
    {
        int cols = segments + 1;
        var verts = new Vector3[(rows + 1) * cols];
        var norms = new Vector3[(rows + 1) * cols];
        for (int j = 0; j <= rows; j++)
        {
            float y = Mathf.Lerp(yFrom, yTo, j / (float)rows);
            float rad = radiusAt(y);
            for (int i = 0; i < cols; i++)
            {
                float ang = 2f * Mathf.PI * i / segments;
                float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                verts[j * cols + i] = new Vector3(p.axisXZ.x + rad * cs, y, p.axisXZ.y + rad * sn);
                norms[j * cols + i] = new Vector3(cs, 0f, sn);
            }
        }

        var tris = new List<int>(rows * segments * 6);
        for (int j = 0; j < rows; j++)
            for (int i = 0; i < segments; i++)
            {
                int a = j * cols + i, b = a + 1, c = (j + 1) * cols + i + 1, d = (j + 1) * cols + i;
                AddTri(tris, verts, norms, a, b, c);
                AddTri(tris, verts, norms, a, c, d);
            }

        var mesh = new Mesh { name = "VesselScaleShell" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Arah segitiga disesuaikan supaya sisi depan menghadap keluar (shader memakai VFACE untuk membedakan permukaan cairan)
    static void AddTri(List<int> tris, Vector3[] v, Vector3[] n, int i0, int i1, int i2)
    {
        Vector3 cross = Vector3.Cross(v[i1] - v[i0], v[i2] - v[i0]);
        if (cross.sqrMagnitude < 1e-16f) return;                              // segitiga kolaps di pusat lantai
        if (Vector3.Dot(cross, n[i0] + n[i1] + n[i2]) < 0f) { tris.Add(i0); tris.Add(i2); tris.Add(i1); }
        else { tris.Add(i0); tris.Add(i1); tris.Add(i2); }
    }
}