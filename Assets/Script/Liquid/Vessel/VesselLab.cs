using UnityEngine;

// Scene uji: slider isi, kemiringan, dan warna untuk semua wadah sekaligus
public class VesselLab : MonoBehaviour
{
    [SerializeField] LiquidVessel[] vessels;
    [SerializeField, Range(0f, 1f)] float fill = 0.5f;
    [SerializeField, Range(-90f, 90f)] float tiltX;
    [SerializeField, Range(-90f, 90f)] float tiltZ;
    [SerializeField] Color color = new Color(1f, 0.82f, 0.15f, 1f);

    void Update()
    {
        if (vessels == null) return;
        foreach (LiquidVessel v in vessels)
        {
            if (!v) continue;
            v.Fill = fill;
            v.LiquidColor = color;
            v.transform.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);   // miring terhadap pusat dasar rongga
        }
    }
}