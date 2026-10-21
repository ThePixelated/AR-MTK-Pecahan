using TMPro;
using UnityEngine;

public class MixedFractionView : MonoBehaviour
{
    [SerializeField] TMP_Text wholeText;
    [SerializeField] GameObject fractionGroup;
    [SerializeField] TMP_Text numeratorText;
    [SerializeField] TMP_Text denominatorText;

    public void Clear()
    {
        wholeText.gameObject.SetActive(false);
        fractionGroup.SetActive(false);
    }

    public void ShowUnknown()
    {
        wholeText.text = "?";
        wholeText.gameObject.SetActive(true);
        fractionGroup.SetActive(false);
    }

    // Khusus nilai >= 0
    public void Show(Fraction f)
    {
        f = f.Reduce();
        long whole = f.WholePart, rem = f.RemainderNumerator;

        bool showWhole = whole > 0 || rem == 0;     // "0" hanya tampil kalau nilainya memang 0
        bool showFraction = rem != 0;

        wholeText.gameObject.SetActive(showWhole);
        if (showWhole) wholeText.text = whole.ToString();

        fractionGroup.SetActive(showFraction);
        if (showFraction)
        {
            numeratorText.text = rem.ToString();
            denominatorText.text = f.denominator.ToString();
        }
    }
}