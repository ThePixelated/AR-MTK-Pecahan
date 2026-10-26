using UnityEngine;

// Satu wadah yang bisa digrab dan jadi target tuang: titik asal, collider ray, indikator target.
// Pasang di objek yang sama dengan LiquidVessel.
public class VesselActor : MonoBehaviour
{
    [SerializeField] LiquidVessel vessel;
    [SerializeField] Transform home;                 // pivot asal; kosong = pose saat objek aktif pertama kali
    [SerializeField] GameObject targetIndicator;     // opsional: dinyalakan saat menjadi target tuang yang sah

    Vector3 homePos;
    Quaternion homeRot;
    Collider hitCollider;

    public LiquidVessel Vessel => vessel;
    public Vector3 HomePosition => home ? home.position : homePos;
    public Quaternion HomeRotation => home ? home.rotation : homeRot;

    void Awake()
    {
        if (!vessel) vessel = GetComponent<LiquidVessel>();
        homePos = transform.localPosition;
        homeRot = transform.localRotation;
        if (targetIndicator) targetIndicator.SetActive(false);

        //Debug.Log(gameObject.name + ": " + home.position);
        //Debug.Log(gameObject.name + ": " + homePos);
        //Debug.Log(gameObject.name + ": " + HomePosition);
    }

    // Dipanggil controller: kapsul pada rongga (ikut miring bersama kaca) di layer khusus untuk ray dan grab
    public void SetupCollider(int layer)
    {
        if (hitCollider) return;
        if (!vessel) vessel = GetComponent<LiquidVessel>();
        VesselProfile p = vessel != null ? vessel.Profile : null;
        if (p == null || !p.IsValid || vessel.Glass == null)
        {
            Debug.LogError($"[{name}] VesselActor: LiquidVessel/profil belum siap (jalankan Auto-fit).", this);
            return;
        }

        var go = new GameObject("PourCollider") { layer = layer };
        go.transform.SetParent(vessel.Glass, false);
        var cap = go.AddComponent<CapsuleCollider>();
        cap.direction = 1;
        cap.isTrigger = true;
        cap.center = new Vector3(p.axisXZ.x, (p.floorY + p.rimY) * 0.5f, p.axisXZ.y);
        cap.radius = p.outerRadius;
        cap.height = Mathf.Max(p.Depth, 2f * p.outerRadius);
        hitCollider = cap;
    }

    public void SetTargeted(bool on)
    {
        if (targetIndicator && targetIndicator.activeSelf != on) targetIndicator.SetActive(on);
    }

    // Titik di atas bibir wadah ini (sisi yang menghadap pemberi) tempat bibir pemberi diarahkan
    public Vector3 PourPointWorld(Vector3 fromDirWorld)
    {
        return vessel.RimCenterWorld - fromDirWorld * (vessel.RimRadiusWorld * 0.35f) + Vector3.up * (vessel.DepthWorld * 0.15f);
    }
}