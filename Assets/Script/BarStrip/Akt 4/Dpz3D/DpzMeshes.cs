using System.Collections.Generic;
using UnityEngine;

public static class DpzMeshes
{
    static readonly Dictionary<(int, int, int, int), Mesh> prisms = new Dictionary<(int, int, int, int), Mesh>();
    static Mesh trackQuad;
    static Mesh trackQuadMirrored;

    // di ResetStatics():  trackQuadMirrored = null;

    // Sama dengan TrackQuad, tapi U dibalik: sel terisi mulai dari sisi kanan (Media 6)
    public static Mesh TrackQuadMirrored()
    {
        if (trackQuadMirrored) return trackQuadMirrored;
        var m = new Mesh { name = "TrackQuadMirrored" };
        m.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, -1), new Vector3(0, 0, -1) };
        m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        m.uv = new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds();
        trackQuadMirrored = m;
        return m;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { prisms.Clear(); trackQuad = null; trackQuadMirrored = null; }

    static int Q(float meters) => Mathf.RoundToInt(meters * 100000f);   // presisi 0,01 mm

    // Penampang trapesium (tampak samping): alas selebar length, sisi atas menyempit sebesar inset di kiri dan kanan.
    // Diekstrusi ke -Z sejauh thickness. Origin = sudut kiri-depan-bawah. Tanpa sisi bawah.
    public static Mesh TrapezoidPrism(float length, float thickness, float height, float inset)
    {
        float s = Mathf.Clamp(inset, 0f, 0.3f * length);
        var key = (Q(length), Q(thickness), Q(height), Q(s));
        if (prisms.TryGetValue(key, out Mesh cached) && cached) return cached;

        var v = new List<Vector3>(20);
        var n = new List<Vector3>(20);
        var t = new List<int>(30);
        float L = length, T = thickness, H = height;

        AddQuad(v, n, t, new Vector3(0, 0, 0), new Vector3(L, 0, 0), new Vector3(L - s, H, 0), new Vector3(s, H, 0), Vector3.forward);        // depan
        AddQuad(v, n, t, new Vector3(0, 0, -T), new Vector3(L, 0, -T), new Vector3(L - s, H, -T), new Vector3(s, H, -T), Vector3.back);        // belakang
        AddQuad(v, n, t, new Vector3(s, H, 0), new Vector3(L - s, H, 0), new Vector3(L - s, H, -T), new Vector3(s, H, -T), Vector3.up);         // atas
        AddQuad(v, n, t, new Vector3(0, 0, 0), new Vector3(s, H, 0), new Vector3(s, H, -T), new Vector3(0, 0, -T), new Vector3(-H, s, 0).normalized);   // miring kiri
        AddQuad(v, n, t, new Vector3(L, 0, 0), new Vector3(L, 0, -T), new Vector3(L - s, H, -T), new Vector3(L - s, H, 0), new Vector3(H, s, 0).normalized); // miring kanan

        var mesh = new Mesh { name = $"UnitCell {L * 1000f:0.##}mm" };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        prisms[key] = mesh;
        return mesh;
    }

    // Kuadran datar 1x1 di bidang XZ: x 0..1, z 0..-1, UV (x, -z). Diskalakan per blok.
    public static Mesh TrackQuad()
    {
        if (trackQuad) return trackQuad;
        var m = new Mesh { name = "TrackQuad" };
        m.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, -1), new Vector3(0, 0, -1) };
        m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds();
        trackQuad = m;
        return m;
    }


    // a,b,c,d berurutan mengelilingi sisi; arah segitiga disesuaikan otomatis supaya menghadap `normal`
    static void AddQuad(List<Vector3> v, List<Vector3> n, List<int> t,
                        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        for (int k = 0; k < 4; k++) n.Add(normal);

        if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0f)
        { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        else
        { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
    }
}