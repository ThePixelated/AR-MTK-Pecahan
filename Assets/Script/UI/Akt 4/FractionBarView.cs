using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FractionBarView : MonoBehaviour
{
    [SerializeField] RectTransform barFill;        // RectTransform milik BarImg
    [SerializeField] Image barImage;               // Image milik BarImg
    [SerializeField] RectTransform dividerPort;    // DeviderPort (stretch penuh = track utuh)
    [SerializeField] Image dividerTemplate;        // DeviderImg: anak DeviderPort, NONAKTIF, dipakai sebagai cetakan
    [SerializeField] float dividerThickness = 2f;  // piksel canvas

    [Header("Batas divider (kartu bank: isi, row Kotak: biarkan 0)")]
    [SerializeField] int spriteFromDenominator = 0;   // 0 = nonaktif (selalu divider). Penyebut >= nilai ini: tanpa divider, diganti overflowSprite
    [SerializeField] Image overflowSprite;            // opsional: sprite pengganti divider (sprite diisi sendiri), anak track, awalnya nonaktif

    readonly List<Image> dividers = new List<Image>();

    public void Show(Fraction piece, Color color)
    {
        long d = piece.denominator;
        if (d <= 0) return;

        // Bar = n/d dari track: ujungnya selalu jatuh tepat di divider ke-n
        float frac = Mathf.Clamp01((float)piece.numerator / d);
        barFill.anchorMin = new Vector2(0f, 0f);
        barFill.anchorMax = new Vector2(frac, 1f);
        barFill.offsetMin = Vector2.zero;
        barFill.offsetMax = Vector2.zero;
        if (barImage) barImage.color = color;

        bool tooDense = spriteFromDenominator > 0 && d >= spriteFromDenominator;
        SetDividers(tooDense ? 0 : (int)(d - 1), (int)d);   // d-1 garis di seluruh track

        if (overflowSprite)
        {
            overflowSprite.raycastTarget = false;
            bool show = tooDense && overflowSprite.sprite != null;
            if (overflowSprite.gameObject.activeSelf != show) overflowSprite.gameObject.SetActive(show);
        }
    }

    void SetDividers(int count, int d)
    {
        if (!dividerPort || !dividerTemplate) return;

        while (dividers.Count < count)                       // pool tumbuh sesuai kebutuhan, tidak pernah dibuang
        {
            Image img = Instantiate(dividerTemplate, dividerPort);
            img.raycastTarget = false;
            dividers.Add(img);
        }

        for (int i = 0; i < dividers.Count; i++)
        {
            Image img = dividers[i];
            bool on = i < count;
            if (img.gameObject.activeSelf != on) img.gameObject.SetActive(on);
            if (!on) continue;

            float x = (i + 1f) / d;                          // posisi k/d dari lebar track
            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(x, 0f);
            rt.anchorMax = new Vector2(x, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(dividerThickness, 0f);
        }
    }
}