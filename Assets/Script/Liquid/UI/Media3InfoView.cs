using TMPro;
using UnityEngine;

// Informasi isi wadah gabungan: tiga baris (air, sirup, total) dalam cangkir. Statis di dunia (tanpa billboard);
// arah dan posisinya diatur dari scene. Ikon lewat TMP Sprite Asset yang sama dengan label perbandingan.
public class Media3InfoView : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] string waterSprite = "air";
    [SerializeField] string syrupSprite = "sirup";
    [SerializeField] string unit = "cangkir";
    [SerializeField] string totalLabel = "Total";

    public void Show(int water, int syrup)
    {
        if (!text) return;
        text.text = $"<sprite name=\"{waterSprite}\"> : {water} {unit}\n"
                  + $"<sprite name=\"{syrupSprite}\"> : {syrup} {unit}\n"
                  + $"{totalLabel} : {water + syrup} {unit}";
    }
}