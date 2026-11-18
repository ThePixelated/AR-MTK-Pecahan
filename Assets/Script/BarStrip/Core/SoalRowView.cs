using TMPro;
using UnityEngine;

// Baris Soal Media 4 dan 6: [A] [+/-] [B] [=] [?].
// A dan B tampil (Panel Soal prefab, Read Only menyala) setelah tombol +/- membentuk soal; kotak "?" (Panel Soal prefab, Read Only MATI)
// diisi siswa lewat numpad. Satu instance per halaman; tidak punya aturan sendiri: semuanya mengikuti FractionSession.
public class SoalRowView : MonoBehaviour
{
    [SerializeField] Activity4Controller controller;
    [SerializeField] NumberLineSoalPanel panelA;           // operan pertama (Read Only)
    [SerializeField] NumberLineSoalPanel panelB;           // operan kedua (Read Only)
    [SerializeField] NumberLineSoalPanel answerPanel;      // kotak "?" (Read Only dimatikan)
    [SerializeField] NumberLineNumpad numpad;
    [SerializeField] TMP_Text operatorText;                // opsional: diisi "+" atau "−" menurut operasi halaman ini

    FractionSession session;
    readonly NumberLineSoalInput answerInput = new NumberLineSoalInput();
    bool bound;

    void OnEnable() => Bind();
    void OnDisable() => Unbind();

    void Bind()
    {
        if (bound) return;
        if (controller == null || answerPanel == null || numpad == null)
        {
            Debug.LogError($"[{name}] SoalRowView: Controller, Answer Panel, dan Numpad harus diisi.", this);
            return;
        }

        session = controller.Session;
        session.StateChanged += Refresh;
        answerInput.Changed += OnAnswerEdited;
        answerPanel.Bind(answerInput);
        answerPanel.CellTapped += OnCellTapped;
        numpad.Overflowed += OnOverflow;
        numpad.Entered += OnNumpadEntered;
        numpad.Closed += OnNumpadClosed;
        bound = true;

        if (operatorText) operatorText.text = session.Op == Operation.Add ? "+" : "−";
        Refresh();
    }

    void Unbind()
    {
        if (!bound) return;

        session.StateChanged -= Refresh;
        answerInput.Changed -= OnAnswerEdited;
        answerPanel.CellTapped -= OnCellTapped;
        numpad.Overflowed -= OnOverflow;
        numpad.Entered -= OnNumpadEntered;
        numpad.Closed -= OnNumpadClosed;
        numpad.PageGone();
        bound = false;
    }

    // ---------------- tampilan mengikuti sesi ----------------
    void Refresh()
    {
        if (session.CurrentPhase == Phase.Menyusun)
        {
            // soal belum dibentuk (atau dibatalkan karena Kotak diubah): semuanya "..", jawaban dikosongkan
            if (panelA) panelA.ShowEmptyResult();
            if (panelB) panelB.ShowEmptyResult();
            ClearAnswer();
        }
        else
        {
            Show(panelA, session.Current.a);
            Show(panelB, session.Current.b);
        }

        // tanda benar atau salah hanya berlaku sampai area atau jawaban diubah lagi
        if (session.CurrentPhase != Phase.Dicek) answerPanel.SetIndicator(NumberLineIndicator.None);
    }

    // Nilai pecahan ditampilkan sebagai bilangan campuran: whole, pembilang sisa, penyebut
    static void Show(NumberLineSoalPanel panel, Fraction f)
    {
        if (panel == null) return;
        long whole = f.WholePart;
        long rem = f.numerator - whole * f.denominator;
        panel.ShowValue((int)whole, (int)rem, (int)f.denominator);
    }

    // ---------------- jawaban siswa ----------------
    void OnCellTapped(NumberLineSoalPanel panel, NumberLineCell cell)
    {
        if (session.CurrentPhase == Phase.Menyusun)
        {
            Feedback.Show(Messages.For(RejectReason.SoalBelumDibentuk), FeedbackKind.Info);
            return;
        }

        numpad.Open(answerInput, cell);
        panel.SetFocusedCell(cell);
    }

    void OnNumpadClosed() => answerPanel.SetFocusedCell(null);

    void OnOverflow(int max) => Feedback.Show(NumberLineMessages.CellOverflow(max), FeedbackKind.Warning);

    // Enter: penyebut 0 dikosongkan dengan notifikasi
    void OnNumpadEntered()
    {
        if (answerInput.Evaluate(out _) != NumberLineSoalStatus.Impossible) return;

        answerInput.Clear();
        Feedback.Show(NumberLineMessages.SoalImpossible, FeedbackKind.Warning);
    }

    void OnAnswerEdited() => answerPanel.SetIndicator(NumberLineIndicator.None);

    public NumberLineSoalStatus ReadAnswer(out Fraction value) => answerInput.Evaluate(out value);

    // Jawaban seperti yang diketik siswa: "1 3/4", "7/4", atau "2"
    public string AnswerText()
    {
        string w = answerInput.TextOf(NumberLineCell.Whole);
        string n = answerInput.TextOf(NumberLineCell.Numerator);
        string d = answerInput.TextOf(NumberLineCell.Denominator);

        if (n.Length == 0 && d.Length == 0) return w;
        return w.Length == 0 ? $"{n}/{d}" : $"{w} {n}/{d}";
    }

    public void ClearAnswer()
    {
        if (numpad) numpad.Close();
        answerInput.Clear();
    }

    public void CloseNumpad()
    {
        if (numpad) numpad.Close();
    }

    public void ShowAnswerIndicator(bool correct) =>
        answerPanel.SetIndicator(correct ? NumberLineIndicator.Correct : NumberLineIndicator.Wrong);
}
