using TMPro;
using UnityEngine;

// Informasi isi wadah gabungan: air, sirup, dan total (tiga teks terpisah, statis di dunia; arah dan posisinya diatur dari scene).
// Satuan menempel pada angka ("3 cangkir"). Kapasitas wadah (mis. "11 / 20 cangkir") hanya tampil kalau Show Capacity menyala.
public class Media3InfoView : MonoBehaviour
{
    [SerializeField] TMP_Text waterTxt;
    [SerializeField] TMP_Text syrupTxt;
    [SerializeField] TMP_Text totalTxt;
    [SerializeField] string unit = "cangkir";
    [SerializeField] bool showCapacity = false;

    public void Show(int water, int syrup, int capacity = 0)
    {
        if (waterTxt) waterTxt.text = $"{water} {unit}";
        if (syrupTxt) syrupTxt.text = $"{syrup} {unit}";
        if (totalTxt)
        {
            int total = water + syrup;
            totalTxt.text = showCapacity && capacity > 0 ? $"{total} / {capacity} {unit}" : $"{total} {unit}";
        }
    }
}