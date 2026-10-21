using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Membaca mesh kaca dan mengisi VesselProfile: sumbu, lantai, bibir, radius dalam per tinggi, jumlah sisi, cerat.
// Mengandalkan normal mesh: permukaan dalam rongga = normal menunjuk ke sumbu (otomatis mendeteksi normal terbalik).
// Tidak butuh vertex di tengah dinding: dinding lurus dengan dua cincin pun cukup.
public static class VesselAutoFit
{
    const int Bins = 40;

    public static string Fit(LiquidVessel vessel)
    {
        Transform glass = vessel.Glass;
        if (glass == null) return "Field 'Glass' kosong.";

        MeshFilter mf = glass.GetComponent<MeshFilter>();
        if (mf == null)
        {
            mf = glass.GetComponentInChildren<MeshFilter>();
            if (mf != null) { glass = mf.transform; vessel.SetGlass(glass); }
        }
        if (mf == null || mf.sharedMesh == null) return "Tidak ditemukan MeshFilter dengan mesh pada 'Glass'.";

        Mesh mesh = mf.sharedMesh;
        if (!mesh.isReadable) return "Mesh tidak readable. Aktifkan Read/Write di Import Settings FBX (tab Model), Apply, lalu coba lagi.";

        Vector3[] vs = mesh.vertices;
        Vector3[] ns = mesh.normals;
        if (ns == null || ns.Length != vs.Length) return "Mesh tidak punya normals.";

        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector3 v in vs) { minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y); }
        float height = maxY - minY;
        if (height <= 0f) return "Tinggi mesh nol.";

        // 1) sumbu putar: rata-rata x,z vertex di 80% bagian bawah (cincin penuh dan simetris; bibir dan cerat tidak ikut)
        float yCut = minY + 0.8f * height;
        double sx = 0, sz = 0; int sc = 0;
        foreach (Vector3 v in vs)
            if (v.y <= yCut) { sx += v.x; sz += v.z; sc++; }
        Vector2 axis = new Vector2((float)(sx / sc), (float)(sz / sc));

        // 2) pass pertama: kumpulkan radius vertex dinding yang normalnya menghadap sumbu / menghadap luar
        var negR = new List<float>();
        var posR = new List<float>();
        for (int i = 0; i < vs.Length; i++)
        {
            float dx = vs[i].x - axis.x, dz = vs[i].z - axis.y;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            if (r < 1e-6f || Mathf.Abs(ns[i].y) >= 0.8f) continue;

            float dn = (ns[i].x * dx + ns[i].z * dz) / r;
            if (dn < -0.3f) negR.Add(r);
            else if (dn > 0.3f) posR.Add(r);
        }
        if (negR.Count == 0 || posR.Count == 0)
            return $"Dinding dalam dan luar tidak keduanya ditemukan (vertex menghadap sumbu: {negR.Count}, menghadap luar: {posR.Count}, total {vs.Length}). "
                 + "Mesh mungkin solid (tanpa rongga) atau normalnya tidak biasa.";

        // dinding dalam = yang radiusnya lebih kecil. Kalau normal model terbalik, peran kedua kelompok ditukar.
        bool flip = Median(negR) > Median(posR);

        // 3) pass kedua: dinding dalam, bibir, radius, jumlah sisi
        var innerRs = new List<float>();
        var outerRs = new List<float>();
        var ringAngles = new Dictionary<int, HashSet<int>>();
        float rimY = float.MinValue;
        float maxR = 0f;
        float ringStep = height * 0.002f;

        for (int i = 0; i < vs.Length; i++)
        {
            float dx = vs[i].x - axis.x, dz = vs[i].z - axis.y;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            maxR = Mathf.Max(maxR, r);
            if (r < 1e-6f || Mathf.Abs(ns[i].y) >= 0.8f) continue;

            float dn = (ns[i].x * dx + ns[i].z * dz) / r;
            bool inner = flip ? dn > 0.3f : dn < -0.3f;
            bool outer = flip ? dn < -0.3f : dn > 0.3f;

            if (inner)
            {
                innerRs.Add(r);
                rimY = Mathf.Max(rimY, vs[i].y);

                int key = Mathf.RoundToInt((vs[i].y - minY) / ringStep);
                if (!ringAngles.TryGetValue(key, out HashSet<int> set)) ringAngles[key] = set = new HashSet<int>();
                set.Add(Mathf.RoundToInt(Mathf.Atan2(dz, dx) * Mathf.Rad2Deg * 4f));
            }
            else if (outer)
            {
                outerRs.Add(r);
            }
        }

        float innerR = Median(innerRs);
        float outerR = Median(outerRs);

        // 4) lantai rongga: titik terendah yang normalnya menghadap atas (ke dalam rongga), di dalam radius dinding
        float floorY = float.MaxValue;
        for (int i = 0; i < vs.Length; i++)
        {
            bool up = flip ? ns[i].y < -0.5f : ns[i].y > 0.5f;
            if (!up) continue;

            float dx = vs[i].x - axis.x, dz = vs[i].z - axis.y;
            if (Mathf.Sqrt(dx * dx + dz * dz) <= innerR * 1.02f) floorY = Mathf.Min(floorY, vs[i].y);
        }
        if (floorY == float.MaxValue) return "Lantai rongga tidak ditemukan (tidak ada permukaan menghadap atas di dalam dinding).";

        float depth = rimY - floorY;
        if (depth <= 0f) return $"Kedalaman rongga tidak valid (lantai y={floorY:0.####}, bibir y={rimY:0.####}).";

        // 5) radius dalam per ketinggian: radius terbesar di tiap bin dari permukaan rongga (dinding dalam + lantai)
        float binW = depth / Bins;
        var binMax = new float[Bins];
        for (int k = 0; k < Bins; k++) binMax[k] = -1f;

        for (int i = 0; i < vs.Length; i++)
        {
            float y = vs[i].y;
            if (y < floorY - 1e-6f || y > rimY + 1e-6f) continue;

            float dx = vs[i].x - axis.x, dz = vs[i].z - axis.y;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            float dn = r > 1e-6f ? (ns[i].x * dx + ns[i].z * dz) / r : 0f;

            bool inner = Mathf.Abs(ns[i].y) < 0.8f && (flip ? dn > 0.3f : dn < -0.3f);
            bool up = flip ? ns[i].y < -0.5f : ns[i].y > 0.5f;
            bool floorPart = up && y <= floorY + 0.12f * depth && r <= innerR * 1.02f;
            if (!inner && !floorPart) continue;

            int b = Mathf.Clamp((int)((y - floorY) / binW), 0, Bins - 1);
            binMax[b] = Mathf.Max(binMax[b], r);
        }

        int firstValid = -1;
        for (int k = 0; k < Bins; k++) if (binMax[k] >= 0f) { firstValid = k; break; }
        if (firstValid < 0) return "Tidak ada permukaan rongga yang bisa dibaca.";

        // bin kosong (dinding lurus panjang tanpa vertex di tengah) diisi interpolasi dari tetangganya
        for (int k = 0; k < Bins; k++)
        {
            if (binMax[k] >= 0f) continue;
            float left = k > 0 ? binMax[k - 1] : binMax[firstValid];
            int next = k + 1;
            while (next < Bins && binMax[next] < 0f) next++;
            float right = next < Bins ? binMax[next] : left;
            binMax[k] = Mathf.Lerp(left, right, 1f / (next - k + 1));
        }

        var samples = new ProfilePoint[Bins + 2];
        samples[0] = new ProfilePoint(floorY, binMax[0]);
        for (int k = 0; k < Bins; k++) samples[k + 1] = new ProfilePoint(floorY + (k + 0.5f) * binW, binMax[k]);
        samples[Bins + 1] = new ProfilePoint(rimY, binMax[Bins - 1]);

        // 6) jumlah sisi: cincin dengan sudut berbeda terbanyak
        int segments = 0;
        foreach (var kv in ringAngles) segments = Mathf.Max(segments, kv.Value.Count);
        if (segments < 8) segments = 48;

        // 7) cerat: ada vertex yang jauh melewati radius luar badan
        bool hasSpout = maxR > 1.12f * outerR;
        Vector2 spoutDir = Vector2.zero;
        float reach = 0f;
        if (hasSpout)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < vs.Length; i++)
            {
                float dx = vs[i].x - axis.x, dz = vs[i].z - axis.y;
                if (Mathf.Sqrt(dx * dx + dz * dz) > 0.97f * maxR) sum += new Vector2(dx, dz);
            }
            spoutDir = sum.sqrMagnitude > 1e-12f ? sum.normalized : Vector2.right;
            reach = maxR - outerR;
        }

        VesselProfile p = vessel.Profile;
        p.axisXZ = axis;
        p.floorY = floorY;
        p.rimY = rimY;
        p.outerRadius = outerR;
        p.segments = segments;
        p.hasSpout = hasSpout;
        p.spoutDirXZ = spoutDir;
        p.spoutReach = reach;
        p.samples = samples;

        return $"Auto-fit OK ({mesh.name}, {vs.Length} vertex{(flip ? ", normal terbalik terdeteksi dan dikoreksi" : "")}): "
             + $"radius dalam {innerR:0.####} | radius luar {outerR:0.####} | lantai y={floorY:0.####} | bibir y={rimY:0.####} | kedalaman {depth:0.####} | "
             + $"rasio radius/kedalaman {innerR / depth:0.###} | sisi {segments} | "
             + (hasSpout ? $"cerat YA arah ({spoutDir.x:0.00}, {spoutDir.y:0.00}) jangkauan {reach:0.####}" : "cerat tidak ada")
             + $" | sampel {samples.Length}";
    }

    // Geser model supaya titik tengah dasar rongga berada tepat di origin objek LiquidVessel (pivot masuk akal)
    public static string CenterPivot(LiquidVessel vessel)
    {
        Transform glass = vessel.Glass;
        VesselProfile p = vessel.Profile;
        if (glass == null || !p.IsValid) return "Jalankan Auto-fit dulu.";

        Transform root = vessel.transform;
        Transform branch = glass;
        while (branch != null && branch.parent != root) branch = branch.parent;
        if (branch == null) return "'Glass' harus berada di dalam hierarki objek LiquidVessel.";

        Vector3 basePoint = root.InverseTransformPoint(glass.TransformPoint(new Vector3(p.axisXZ.x, p.floorY, p.axisXZ.y)));
        Undo.RecordObject(branch, "Pusatkan pivot");
        branch.localPosition -= basePoint;
        return $"Pivot dipusatkan: model digeser {(-basePoint).ToString("0.####")}";
    }

    static float Median(List<float> list)
    {
        list.Sort();
        return list[list.Count / 2];
    }
}