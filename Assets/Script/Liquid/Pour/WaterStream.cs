using UnityEngine;
using UnityEngine.Rendering;

// Aliran air: tabung melengkung (parabola) dari bibir gelas ke permukaan penerima. Mesh dihitung ulang tiap frame.
// Taruh di objek kosong di akar scene (tanpa skala).
public class WaterStream : MonoBehaviour
{
    static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] Material material;                 // shader ARPecahan/WaterStream
    [SerializeField, Range(8, 40)] int segments = 20;
    [SerializeField, Range(4, 12)] int sides = 6;
    [SerializeField, Range(0f, 1f)] float taper = 0.35f;

    MeshRenderer mr;
    Mesh mesh;
    MaterialPropertyBlock mpb;
    Vector3[] verts;
    Vector2[] uvs;

    void Awake()
    {
        if (material == null) Debug.LogError($"[{name}] WaterStream: 'Material' kosong (shader ARPecahan/WaterStream).", this);

        int cols = sides + 1;
        verts = new Vector3[(segments + 1) * cols];
        uvs = new Vector2[verts.Length];

        var tris = new int[segments * sides * 6];
        int k = 0;
        for (int i = 0; i < segments; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * cols + j, b = a + 1, c = (i + 1) * cols + j + 1, d = (i + 1) * cols + j;
                tris[k++] = a; tris[k++] = b; tris[k++] = c;
                tris[k++] = a; tris[k++] = c; tris[k++] = d;
            }

        mesh = new Mesh { name = "WaterStream" };
        mesh.MarkDynamic();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;

        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.enabled = false;
        mpb = new MaterialPropertyBlock();
    }

    void OnDestroy() { if (mesh) Destroy(mesh); }

    public void Hide() { if (mr) mr.enabled = false; }

    // Lintasan: horizontal linear, tinggi turun kuadratik (mulai mendatar, jatuh ke end)
    public void SetPath(Vector3 start, Vector3 end, float width, Color color)
    {
        if (mr == null) return;

        float h = Mathf.Max(start.y - end.y, 1e-4f);
        int cols = sides + 1;
        for (int i = 0; i <= segments; i++)
        {
            float u = i / (float)segments;
            Vector3 p = new Vector3(Mathf.Lerp(start.x, end.x, u), start.y - h * u * u, Mathf.Lerp(start.z, end.z, u));

            Vector3 raw = new Vector3(end.x - start.x, -2f * h * u, end.z - start.z);
            Vector3 t = raw.sqrMagnitude > 1e-10f ? raw.normalized : Vector3.down;
            Vector3 side = Vector3.Cross(t, Vector3.up);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(t, Vector3.right);
            side.Normalize();
            Vector3 bin = Vector3.Cross(t, side);
            float rad = 0.5f * width * (1f - taper * u);

            for (int j = 0; j < cols; j++)
            {
                float a = 2f * Mathf.PI * j / sides;
                Vector3 world = p + (side * Mathf.Cos(a) + bin * Mathf.Sin(a)) * rad;
                verts[i * cols + j] = transform.InverseTransformPoint(world);
                uvs[i * cols + j] = new Vector2(j / (float)sides, u);
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.RecalculateBounds();

        mr.GetPropertyBlock(mpb);
        mpb.SetColor(ColorId, color);
        mr.SetPropertyBlock(mpb);
        mr.enabled = true;
    }
}