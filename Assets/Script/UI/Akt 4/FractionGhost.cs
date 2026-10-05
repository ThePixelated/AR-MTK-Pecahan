using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class FractionGhost : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] Image background;

    public void Setup(Fraction piece, FractionPalette palette)
    {
        label.text = FractionFormat.Label(piece);
        background.color = palette.Get(piece.denominator);

        var cg = GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;   // wajib: kalau gak, ghost menutupi row/zona di bawahnya
        cg.interactable = false;
        cg.alpha = 0.9f;
    }
}