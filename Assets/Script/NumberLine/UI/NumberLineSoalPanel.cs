using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum NumberLineIndicator { None, Correct, Wrong }

// Panel Soal (prefab): tiga kotak pecahan (whole, pembilang, penyebut) yang dibuka numpad lewat tap.
// Terikat ke satu NumberLineSoalInput. Indikator benar/salah dipakai mode Latihan; Eksplorasi membiarkannya tersembunyi.
public class NumberLineSoalPanel : MonoBehaviour
{
    [Header("Kotak pecahan")]
    [SerializeField] Button wholeButton;          // PembilangKiriBtn
    [SerializeField] Button numeratorButton;      // PembilganBtn
    [SerializeField] Button denominatorButton;    // PenyebutBtn
    [SerializeField] TMP_Text wholeText;          // numValTxt masing-masing
    [SerializeField] TMP_Text numeratorText;
    [SerializeField] TMP_Text denominatorText;
    [SerializeField] string emptyText = "..";
    [SerializeField] GameObject[] focusMarks;     // opsional, urut: Whole, Numerator, Denominator

    [Header("Indikator benar/salah")]
    [SerializeField] Image indicatorImage;        // IndicatorImg (tersembunyi secara default)
    [SerializeField] Sprite correctSprite;
    [SerializeField] Sprite wrongSprite;

    NumberLineSoalInput input;
    bool wired;

    public NumberLineSoalInput Input => input;

    // Dikirim saat salah satu kotak di-tap: controller membuka numpad untuk kotak itu
    public event Action<NumberLineSoalPanel, NumberLineCell> CellTapped;

    void Awake() => Wire();

    void Wire()
    {
        if (wired) return;
        wired = true;
        if (wholeButton) wholeButton.onClick.AddListener(() => CellTapped?.Invoke(this, NumberLineCell.Whole));
        if (numeratorButton) numeratorButton.onClick.AddListener(() => CellTapped?.Invoke(this, NumberLineCell.Numerator));
        if (denominatorButton) denominatorButton.onClick.AddListener(() => CellTapped?.Invoke(this, NumberLineCell.Denominator));
        SetIndicator(NumberLineIndicator.None);
    }

    public void Bind(NumberLineSoalInput newInput)
    {
        Wire();
        Unbind();
        input = newInput;
        input.Changed += Refresh;
        Refresh();
    }

    public void Unbind()
    {
        if (input != null) input.Changed -= Refresh;
        input = null;
    }

    void OnDestroy() => Unbind();

    void Refresh()
    {
        if (input == null) return;
        if (wholeText) wholeText.text = Display(NumberLineCell.Whole);
        if (numeratorText) numeratorText.text = Display(NumberLineCell.Numerator);
        if (denominatorText) denominatorText.text = Display(NumberLineCell.Denominator);
    }

    string Display(NumberLineCell cell)
    {
        string t = input.TextOf(cell);
        return string.IsNullOrEmpty(t) ? emptyText : t;
    }

    public void SetFocusedCell(NumberLineCell? cell)
    {
        if (focusMarks == null) return;
        for (int i = 0; i < focusMarks.Length; i++)
            if (focusMarks[i]) focusMarks[i].SetActive(cell.HasValue && (int)cell.Value == i);
    }

    public void SetIndicator(NumberLineIndicator state)
    {
        if (!indicatorImage) return;
        indicatorImage.gameObject.SetActive(state != NumberLineIndicator.None);
        if (state == NumberLineIndicator.Correct) indicatorImage.sprite = correctSprite;
        else if (state == NumberLineIndicator.Wrong) indicatorImage.sprite = wrongSprite;
    }
}