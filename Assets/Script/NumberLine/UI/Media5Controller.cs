using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Controller halaman Media 5. Menghubungkan garis bilangan, panel atur garis, baris soal (A - B = hasil), panel Jawaban,
// numpad, panel hewan, tombol, dan popup ke satu Media5Session. Satu instance per halaman.
//
// Dua keadaan (logika di Media5Session):
//   Explore: belum ada soal. Hewan ditarik dari PionGrabber, digeser, atau ditarik kembali. A dan B bisa diketik.
//   Locked : ada soal (Generate Soal atau Enter Soal). Hewan terkunci di A; Mundur, Ulangi, dan Cek Hasil aktif.
public class Media5Controller : MonoBehaviour
{
    [SerializeField] Media5Limits limits = new Media5Limits();
    [SerializeField] NumberLineView view;
    [SerializeField] LineSettingsPanel lineSettings;

    [Header("Baris soal: A - B = hasil")]
    [SerializeField] NumberLineSoalPanel operandAPanel;      // Read Only mati: diketik lewat numpad
    [SerializeField] NumberLineSoalPanel operandBPanel;      // Read Only mati
    [SerializeField] NumberLineSoalPanel resultPanel;        // Read Only nyala: ".." sampai Cek Hasil benar
    [SerializeField] TMP_Text operatorText;                  // "-"

    [Header("Jawaban siswa (bekas panel Posisi)")]
    [FormerlySerializedAs("positionPanel")]
    [SerializeField] NumberLineSoalPanel answerPanel;        // Read Only mati: diketik lewat numpad
    [SerializeField] string answerTitle = "Jawaban";

    [Header("Hewan dan numpad")]
    [SerializeField] PionGrabber pionGrabber;
    [SerializeField] NumberLineNumpad numpad;

    [Header("Tombol")]
    [SerializeField] HoldRepeatButton backwardButton;        // Mundur (tahan untuk mengulang)
    [SerializeField] Button retryButton;                     // Ulangi
    [SerializeField] Button checkButton;                     // Cek Hasil
    [SerializeField] Button generateButton;                  // GenerateSoalBtn
    [SerializeField] Button clearButton;                     // ClearSoalBtn
    [SerializeField] Button enterButton;                     // EnterBtn (Enter Soal)
    [SerializeField] Button resetButton;                     // Reset

    [SerializeField] SimpleConfirmDialog confirmDialog;

    Media5Session session;
    public Media5Session Session => session ??= new Media5Session(limits);

    NumberLineSoalInput inputA, inputB, inputAnswer;
    NumberLineSoalPanel focusedPanel;
    bool wired;

    void EnsureInputs()
    {
        if (inputA != null) return;
        int max = limits.Line.MaxCell;
        inputA = new NumberLineSoalInput(max);
        inputB = new NumberLineSoalInput(max);
        inputAnswer = new NumberLineSoalInput(max);
    }

    void OnEnable()
    {
        if (view == null || lineSettings == null || operandAPanel == null || operandBPanel == null || answerPanel == null)
        {
            Debug.LogError($"[{name}] Media5Controller: View, Line Settings, Operand A Panel, Operand B Panel, dan Answer Panel harus diisi.", this);
            return;
        }
        wired = true;

        EnsureInputs();
        operandAPanel.Bind(inputA);
        operandBPanel.Bind(inputB);
        answerPanel.Bind(inputAnswer);
        if (!string.IsNullOrEmpty(answerTitle)) answerPanel.SetTitle(answerTitle);
        if (resultPanel) resultPanel.ShowEmptyResult();

        inputA.Changed += OnOperandChanged;
        inputB.Changed += OnOperandChanged;
        inputAnswer.Changed += OnAnswerChanged;

        operandAPanel.CellTapped += OnCellTapped;
        operandBPanel.CellTapped += OnCellTapped;
        answerPanel.CellTapped += OnCellTapped;

        if (numpad)
        {
            numpad.Closed += OnNumpadClosed;
            numpad.Overflowed += OnNumpadOverflow;
        }

        view.PionDragged += OnPionDragged;
        if (pionGrabber)
        {
            pionGrabber.Dropped += OnPionDropped;
            pionGrabber.CallBackPressed += OnCallBackPressed;
        }

        lineSettings.Configure(limits.Line);
        lineSettings.LengthChosen += OnLengthChosen;
        lineSettings.PartisiChanged += OnPartisiChanged;

        if (backwardButton) backwardButton.Step += OnBackwardStep;
        Hook(retryButton, OnRetryPressed, true);
        Hook(checkButton, OnCheckPressed, true);
        Hook(generateButton, OnGeneratePressed, true);
        Hook(clearButton, OnClearPressed, true);
        Hook(enterButton, OnEnterPressed, true);
        Hook(resetButton, OnResetPressed, true);

        Session.Changed += Sync;
        Sync();
        view.SetPionTick(Session.PionTick, true);      // halaman dibuka: hewan langsung di tempatnya
    }

    void OnDisable()
    {
        if (!wired) return;
        wired = false;

        if (numpad)
        {
            numpad.PageGone();
            numpad.Closed -= OnNumpadClosed;
            numpad.Overflowed -= OnNumpadOverflow;
        }
        focusedPanel = null;

        inputA.Changed -= OnOperandChanged;
        inputB.Changed -= OnOperandChanged;
        inputAnswer.Changed -= OnAnswerChanged;

        operandAPanel.CellTapped -= OnCellTapped;
        operandBPanel.CellTapped -= OnCellTapped;
        answerPanel.CellTapped -= OnCellTapped;

        view.PionDragged -= OnPionDragged;
        if (pionGrabber)
        {
            pionGrabber.Dropped -= OnPionDropped;
            pionGrabber.CallBackPressed -= OnCallBackPressed;
        }

        lineSettings.LengthChosen -= OnLengthChosen;
        lineSettings.PartisiChanged -= OnPartisiChanged;

        if (backwardButton) backwardButton.Step -= OnBackwardStep;
        Hook(retryButton, OnRetryPressed, false);
        Hook(checkButton, OnCheckPressed, false);
        Hook(generateButton, OnGeneratePressed, false);
        Hook(clearButton, OnClearPressed, false);
        Hook(enterButton, OnEnterPressed, false);
        Hook(resetButton, OnResetPressed, false);

        if (session != null) session.Changed -= Sync;
    }

    static void Hook(Button button, UnityAction action, bool add)
    {
        if (!button) return;
        if (add) button.onClick.AddListener(action);
        else button.onClick.RemoveListener(action);
    }

    // ---------------- tampilan mengikuti session ----------------
    // Isian A, B, dan Jawaban TIDAK disentuh di sini (itu milik siswa); hanya Generate, Enter, Clear, Reset, dan ganti garis
    // yang mengosongkan atau mengisinya.
    void Sync()
    {
        view.SetConfig(Session.Config);
        view.SetPionTick(Session.PionTick);
        view.SetPionVisible(Session.HasPion);
        view.DragEnabled = Session.HasPion && !Session.IsLocked;      // hewan hanya bisa digeser saat Explore

        lineSettings.Show(Session.Config);
        if (pionGrabber) pionGrabber.SetState(Session.HasPion, Session.IsLocked);

        if (operatorText)
        {
            bool add = Session.HasProblem && Session.Problem.Value.operation == NumberLineOperation.Add;
            operatorText.text = add ? "+" : "-";
        }

        ResetVerdict();
        RefreshButtons();
    }

    // Hasil dan indikator Jawaban hanya berlaku untuk keadaan saat Cek Hasil ditekan; perubahan apa pun membatalkannya
    void ResetVerdict()
    {
        answerPanel.SetIndicator(NumberLineIndicator.None);
        if (resultPanel) resultPanel.ShowEmptyResult();
    }

    void RefreshButtons()
    {
        bool locked = Session.IsLocked;
        bool anySoalText = !inputA.IsEmpty || !inputB.IsEmpty;

        if (backwardButton) backwardButton.Interactable = locked && Session.PionTick > 0;
        if (enterButton) enterButton.interactable = !locked && anySoalText;
        if (clearButton) clearButton.interactable = locked || anySoalText;
    }

    void OnOperandChanged() => RefreshButtons();

    void OnAnswerChanged()
    {
        ResetVerdict();
        RefreshButtons();
    }

    void ShowTick(NumberLineSoalPanel panel, int tick)
    {
        if (!panel) return;
        Session.Config.TickToMixed(tick, out int whole, out int numerator);
        panel.ShowValue(whole, numerator, Session.Config.partisi);
    }

    // Mengisi isian dari tick: bulat = hanya whole; selain itu whole (kosong bila 0), pembilang, penyebut = partisi
    void FillFromTick(NumberLineSoalInput input, int tick)
    {
        Session.Config.TickToMixed(tick, out int whole, out int numerator);
        if (numerator == 0) input.SetValue(whole, null, null);
        else input.SetValue(whole > 0 ? whole : (int?)null, numerator, Session.Config.partisi);
    }

    void ClearInputs()
    {
        inputA.Clear();
        inputB.Clear();
        inputAnswer.Clear();
    }

    // ---------------- numpad ----------------
    void OnCellTapped(NumberLineSoalPanel panel, NumberLineCell cell)
    {
        bool isAnswer = panel == answerPanel;

        if (isAnswer && !Session.IsLocked)
        {
            Feedback.Show(Media5Messages.NoProblem, FeedbackKind.Info);
            return;
        }
        if (!isAnswer && Session.IsLocked)
        {
            Feedback.Show(Media5Messages.ProblemLocked, FeedbackKind.Info);
            return;
        }
        if (numpad == null)
        {
            Debug.LogError($"[{name}] Media5Controller: Numpad belum diisi.", this);
            return;
        }

        if (focusedPanel != null && focusedPanel != panel) focusedPanel.SetFocusedCell(null);
        focusedPanel = panel;
        panel.SetFocusedCell(cell);
        numpad.Open(panel.Input, cell);
    }

    void OnNumpadClosed()
    {
        if (focusedPanel != null) focusedPanel.SetFocusedCell(null);
        focusedPanel = null;
    }

    void OnNumpadOverflow(int max) => Feedback.Show(Media5Messages.Overflow(max), FeedbackKind.Warning);

    void CloseNumpad()
    {
        if (numpad != null && numpad.IsOpen) numpad.Close();
    }

    // ---------------- hewan (Explore) ----------------
    // Dilepas di garis dari PionGrabber
    void OnPionDropped(int tick)
    {
        CloseNumpad();
        if (!Session.PlacePion(tick))
        {
            Sync();
            return;
        }
        view.SetPionTick(Session.PionTick, true);      // hewan baru muncul langsung di tick, tanpa lompatan dari posisi lama
    }

    // Hewan di garis digeser kiri-kanan (NumberLinePionDrag)
    void OnPionDragged(int tick)
    {
        if (!Session.PlacePion(tick))
        {
            Sync();      // Locked: kembalikan tampilan ke posisi session
            return;
        }
        view.SetPionTick(Session.PionTick, true);      // mengikuti drag tanpa animasi lompat
    }

    void OnCallBackPressed()
    {
        if (!Session.RemovePion())
        {
            if (Session.IsLocked) Feedback.Show(Media5Messages.ProblemLocked, FeedbackKind.Info);
            return;
        }
        view.ClearTrail();
    }

    // ---------------- atur garis ----------------
    void OnLengthChosen(int length) => TryReconfigure(length, Session.Config.partisi);
    void OnPartisiChanged(int partisi) => TryReconfigure(Session.Config.length, partisi);

    void TryReconfigure(int length, int partisi)
    {
        NumberLineConfigError err = Session.Validate(length, partisi);
        if (err != NumberLineConfigError.None)
        {
            Feedback.Show(NumberLineMessages.For(err, limits.Line), FeedbackKind.Warning);
            Sync();
            return;
        }

        if (length == Session.Config.length && partisi == Session.Config.partisi)
        {
            Sync();
            return;
        }

        // garis tidak dikunci; kalau ada soal yang belum terjawab benar, tanya dulu. YA = soal ikut dihapus.
        if (Session.NeedsConfirmBeforeReconfigure && confirmDialog != null)
        {
            confirmDialog.Ask(Media5Messages.ReconfigureWarning, () => ApplyConfig(length, partisi), Sync);
            return;
        }
        ApplyConfig(length, partisi);
    }

    void ApplyConfig(int length, int partisi)
    {
        CloseNumpad();
        bool hadProblem = Session.HasProblem;
        Session.ApplyConfig(length, partisi);
        if (hadProblem) ClearInputs();                 // soal ikut clear; isian A/B yang belum jadi soal dibiarkan
        view.ClearTrail();
        view.SetPionTick(Session.PionTick, true);
    }

    // ---------------- gerak ----------------
    void OnBackwardStep()
    {
        int before = Session.PionTick;
        Media5JumpResult r = Session.JumpBackward();

        if (r == Media5JumpResult.Moved) view.AddTrailArc(before, Session.PionTick);
        else if (r == Media5JumpResult.NoProblem) Feedback.Show(Media5Messages.NoProblem, FeedbackKind.Info);
        // AtStart: tombolnya sudah nonaktif di titik 0
    }

    void OnRetryPressed()
    {
        switch (Session.RetryPosition())
        {
            case Media5RetryResult.Moved:
                view.ClearTrail();                      // percobaan baru: jejak lama dibuang
                break;
            case Media5RetryResult.NoProblem:
                Feedback.Show(Media5Messages.NoProblem, FeedbackKind.Info);
                break;
            case Media5RetryResult.AlreadyAtStart:
                Feedback.Show(Media5Messages.AlreadyAtStart, FeedbackKind.Info);
                break;
        }
    }

    // ---------------- soal ----------------
    void OnGeneratePressed()
    {
        // isian siswa (walau belum lengkap) akan diganti: tanya dulu. Soal yang sudah jadi diganti langsung.
        bool studentText = !Session.IsLocked && (!inputA.IsEmpty || !inputB.IsEmpty);
        if (studentText && confirmDialog != null)
        {
            confirmDialog.Ask(Media5Messages.OverwriteWarning, GenerateNow);
            return;
        }
        GenerateNow();
    }

    void GenerateNow()
    {
        CloseNumpad();
        Media5ProblemError err = Session.TryNewProblem();
        if (err != Media5ProblemError.None)
        {
            Feedback.Show(Media5Messages.For(err, limits), FeedbackKind.Warning);
            return;
        }

        NumberLineProblem p = Session.Problem.Value;
        FillFromTick(inputA, p.aTick);
        FillFromTick(inputB, p.bTick);
        inputAnswer.Clear();

        view.ClearTrail();
        view.SetPionTick(Session.PionTick, true);      // soal baru: hewan langsung di A, tanpa lompatan
    }

    void OnEnterPressed()
    {
        CloseNumpad();
        Media5EnterError err = Session.TryEnterProblem(inputA, inputB);
        if (err != Media5EnterError.None)
        {
            Feedback.Show(Media5Messages.For(err, Session.Config), FeedbackKind.Warning);
            return;
        }

        inputAnswer.Clear();
        view.ClearTrail();
        view.SetPionTick(Session.PionTick, true);
    }

    void OnClearPressed()
    {
        CloseNumpad();
        Session.ClearProblem();       // Locked -> Explore, hewan ke titik 0 (event Changed -> Sync)
        ClearInputs();                // juga membersihkan isian A/B yang belum jadi soal
        view.ClearTrail();
        view.SetPionTick(Session.PionTick, true);
        RefreshButtons();
    }

    // ---------------- Cek Hasil ----------------
    void OnCheckPressed()
    {
        CloseNumpad();
        Media5CheckResult r = Session.Check(inputAnswer);

        if (r.Outcome == Media5CheckOutcome.NoProblem)
        {
            Feedback.Show(Media5Messages.NoProblem, FeedbackKind.Info);
            return;
        }

        // Check yang pertama kali benar memicu Sync (mengosongkan hasil dan indikator), jadi tampilkan sesudahnya
        ResetVerdict();
        answerPanel.SetIndicator(r.AnswerState == Media5AnswerState.Correct ? NumberLineIndicator.Correct
                               : r.AnswerState == Media5AnswerState.Wrong ? NumberLineIndicator.Wrong
                               : NumberLineIndicator.None);

        if (r.Outcome == Media5CheckOutcome.Correct) ShowTick(resultPanel, Session.ResultTick);
        Feedback.Show(Media5Messages.For(r.Outcome),
            r.Outcome == Media5CheckOutcome.Correct ? FeedbackKind.Success : FeedbackKind.Warning);
    }

    // ---------------- Reset ----------------
    void OnResetPressed()
    {
        bool pristine = Session.IsPristine && inputA.IsEmpty && inputB.IsEmpty && inputAnswer.IsEmpty;
        if (pristine)
        {
            Feedback.Show(Media5Messages.NothingToReset, FeedbackKind.Info);
            return;
        }

        if (confirmDialog != null) confirmDialog.Ask(Media5Messages.ResetWarning, ResetAll);
        else ResetAll();
    }

    void ResetAll()
    {
        CloseNumpad();
        Session.ResetAll();
        ClearInputs();
        view.ClearTrail();
        view.SetPionTick(Session.PionTick, true);
        Feedback.Show(Media5Messages.ResetDone, FeedbackKind.Success);
    }
}