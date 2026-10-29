using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum NumberLineIndicator { None, Correct, Wrong }

// Panel Soal (prefab): tiga kotak pecahan (whole, pembilang, penyebut) yang dibuka numpad lewat tap.
// Dua pemakaian dari prefab yang sama: panel Soal (terikat ke NumberLineSoalInput) dan panel hasil (readOnly, hanya menampilkan).
public class NumberLineSoalPanel : MonoBehaviour
{
    [Header("Judul")]
    [SerializeField] TMP_Text titleText;          // titleTxt
    [SerializeField] string title = "Soal";       // kosong = "Panel A", "Panel B", ... menurut urutan (SetIndex)

    [Header("Mode")]
    [SerializeField] bool readOnly;               // true = panel hasil: kotak tidak bisa di-tap, hanya menampilkan

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
    int index;
    bool wired;

    public NumberLineSoalInput Input => input;
    public bool ReadOnly => readOnly;

    // Dikirim saat salah satu kotak di-tap: controller membuka numpad untuk kotak itu
    public event Action<NumberLineSoalPanel, NumberLineCell> CellTapped;

    void Awake() => Wire();

    // Menampilkan satu nilai (operan, posisi, hasil) dengan aturan sama: whole kosong bila tidak ada, hanya whole bila tepat bulat
    public void ShowValue(int whole, int numerator, int denominator) => ShowResult(whole, numerator, denominator);

    void Wire()
    {
        if (wired) return;
        wired = true;

        if (readOnly)
        {
            // Button dimatikan (bukan interactable=false) supaya tampilannya tetap normal, tanpa warna disabled
            if (wholeButton) wholeButton.enabled = false;
            if (numeratorButton) numeratorButton.enabled = false;
            if (denominatorButton) denominatorButton.enabled = false;
        }
        else
        {
            if (wholeButton) wholeButton.onClick.AddListener(() => CellTapped?.Invoke(this, NumberLineCell.Whole));
            if (numeratorButton) numeratorButton.onClick.AddListener(() => CellTapped?.Invoke(this, NumberLineCell.Numerator));
            if (denominatorButton) denominatorButton.onClick.AddListener(() => CellTapped?.Invoke(this, NumberLineCell.Denominator));
        }

        SetIndicator(NumberLineIndicator.None);
        RefreshTitle();
    }

    // ---------------- judul ----------------
    // Urutan panel (0 = A). Dipakai saat panel dibuat otomatis di mode Latihan.
    public void SetIndex(int newIndex)
    {
        index = newIndex;
        RefreshTitle();
    }

    public void SetTitle(string newTitle)
    {
        title = newTitle;
        RefreshTitle();
    }

    void RefreshTitle()
    {
        if (titleText) titleText.text = string.IsNullOrEmpty(title) ? $"Panel {NumberLineLabels.Letter(index)}" : title;
    }

    // ---------------- panel Soal ----------------
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

    // ---------------- panel hasil ----------------
    // Jawaban benar: whole, pembilang, penyebut terisi otomatis.
    // Whole dikosongkan ("") kalau tidak ada; tepat di bilangan bulat: hanya whole yang tampil, pecahannya dikosongkan.
    public void ShowResult(int whole, int numerator, int denominator)
    {
        Wire();
        bool wholeNumber = numerator == 0;
        if (wholeText) wholeText.text = (whole > 0 || wholeNumber) ? whole.ToString() : "";
        if (numeratorText) numeratorText.text = wholeNumber ? "" : numerator.ToString();
        if (denominatorText) denominatorText.text = wholeNumber ? "" : denominator.ToString();
    }

    // Belum ada hasil benar (atau jawaban salah): semua kotak ".."
    public void ShowEmptyResult()
    {
        Wire();
        if (wholeText) wholeText.text = emptyText;
        if (numeratorText) numeratorText.text = emptyText;
        if (denominatorText) denominatorText.text = emptyText;
    }

    // ---------------- indikator ----------------
    public void SetIndicator(NumberLineIndicator state)
    {
        if (!indicatorImage) return;
        indicatorImage.gameObject.SetActive(state != NumberLineIndicator.None);
        if (state == NumberLineIndicator.Correct) indicatorImage.sprite = correctSprite;
        else if (state == NumberLineIndicator.Wrong) indicatorImage.sprite = wrongSprite;
    }
}