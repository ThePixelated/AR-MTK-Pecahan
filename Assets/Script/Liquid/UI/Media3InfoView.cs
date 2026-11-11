using TMPro;
using UnityEngine;

// Informasi isi wadah gabungan: tiga baris (air, sirup, total) dalam cangkir. Statis di dunia (tanpa billboard);
// arah dan posisinya diatur dari scene. Ikon lewat TMP Sprite Asset yang sama dengan label perbandingan.
public class Media3InfoView : MonoBehaviour
{
    [SerializeField] TMP_Text waterTxt;
    [SerializeField] TMP_Text syrupTxt;
    [SerializeField] TMP_Text totalTxt;
    [SerializeField] string unit = "cangkir";

    public void Show(int water, int syrup)
    {
        if (!waterTxt) return;
            waterTxt.text = $"{water} {unit}";
        
        if (!syrupTxt) return;
            syrupTxt.text = $"{syrup} {unit}";
        
        if (!totalTxt) return;
            totalTxt.text = $"{water + syrup} {unit}";
    }
}