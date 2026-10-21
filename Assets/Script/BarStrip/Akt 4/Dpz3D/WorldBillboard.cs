using UnityEngine;

// Pasang di Canvas world space (panel aksi, Total) supaya selalu menghadap kamera dan mudah dipencet di AR
public class WorldBillboard : MonoBehaviour
{
    [SerializeField] Camera worldCamera;       // kosong = Camera.main
    [SerializeField] Transform upReference;    // isi DpzRoot: "atas" mengikuti bidang dpz; kosong = atas dunia
    [SerializeField] bool yawOnly = false;     // true: hanya berputar mengelilingi sumbu atas (tidak ikut miring ke kamera)

    void LateUpdate()
    {
        Camera cam = worldCamera ? worldCamera : Camera.main;
        if (cam == null) return;

        Vector3 up = upReference ? upReference.up : Vector3.up;
        Vector3 away = transform.position - cam.transform.position;     // sisi depan canvas = -Z, jadi +Z menjauhi kamera
        if (yawOnly) away = Vector3.ProjectOnPlane(away, up);
        if (away.sqrMagnitude < 1e-8f) return;

        transform.rotation = Quaternion.LookRotation(away, up);
    }
}