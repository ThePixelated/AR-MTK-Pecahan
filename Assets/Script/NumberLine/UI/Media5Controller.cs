using TMPro;
using UnityEngine;

// Controller halaman Media 5: menghubungkan garis bilangan, panel atur garis, baris soal (A - B = hasil), panel posisi,
// tombol lompat, Buat soal, Coba lagi, CEK, Reset, dan popup ke satu Media5Session. Satu instance per halaman.
public class Media5Controller : MonoBehaviour
{
    [SerializeField] Media5Limits limits = new Media5Limits();
    [SerializeField] NumberLineView view;
    [SerializeField] LineSettingsPanel lineSettings;

    [Header("Baris soal (panel Soal mode Read Only)")]
    [SerializeField] NumberLineSoalPanel operandAPanel;
    [SerializeField] NumberLineSoalPanel operandBPanel;
    [SerializeField] NumberLineSoalPanel resultPanel;
    [SerializeField] NumberLineSoalPanel positionPanel;      // posisi pion, ikut bergerak
    [SerializeField] TMP_Text operatorText;                  // "-" atau "+"

    [Header("Tombol lompat")]
    [SerializeField] HoldRepeatButton forwardButton;         // LompatMajuBtn
    [SerializeField] HoldRepeatButton backwardButton;        // LompatMundurBtn

    [SerializeField] SimpleConfirmDialog confirmDialog;

    Media5Session session;
    public Media5Session Session => session ??= new Media5Session(limits);

    bool wired;

    void OnEnable()
    {
        if (view == null || lineSettings == null)
        {
            Debug.LogError($"[{name}] Media5Controller: View dan Line Settings harus diisi.", this);
            return;
        }
        wired = true;

        view.DragEnabled = false;              // Media 5: pion hanya bergerak lewat tombol lompat

        lineSettings.Configure(limits.Line);
        lineSettings.LengthChosen += OnLengthChosen;
        lineSettings.PartisiChanged += OnPartisiChanged;

        if (forwardButton) forwardButton.Step += OnForwardStep;
        if (backwardButton) backwardButton.Step += OnBackwardStep;

        Session.Changed += Sync;
        Sync();
        view.SetPionTick(Session.PionTick, true);      // halaman dibuka: pion langsung di tempatnya
    }

    void OnDisable()
    {
        if (!wired) return;
        wired = false;

        if (lineSettings)
        {
            lineSettings.LengthChosen -= OnLengthChosen;
            lineSettings.PartisiChanged -= OnPartisiChanged;
        }
        if (forwardButton) forwardButton.Step -= OnForwardStep;
        if (backwardButton) backwardButton.Step -= OnBackwardStep;
        if (session != null) session.Changed -= Sync;
    }

    // ---------------- tampilan mengikuti session ----------------
    void Sync()
    {
        view.SetConfig(Session.Config);
        view.SetPionTick(Session.PionTick);
        lineSettings.Show(Session.Config);
        RefreshPanels();
        RefreshJumpButtons();
    }

    void RefreshPanels()
    {
        if (Session.HasProblem)
        {
            NumberLineProblem p = Session.Problem.Value;
            ShowTick(operandAPanel, p.aTick);
            ShowTick(operandBPanel, p.bTick);
            ShowTick(positionPanel, Session.PionTick);
            if (operatorText) operatorText.text = p.operation == NumberLineOperation.Subtract ? "-" : "+";
        }
        else
        {
            if (operandAPanel) operandAPanel.ShowEmptyResult();
            if (operandBPanel) operandBPanel.ShowEmptyResult();
            if (positionPanel) positionPanel.ShowEmptyResult();
        }

        // hasil hanya tampil setelah CEK benar; perubahan apa pun membuatnya kembali ".."
        if (resultPanel) resultPanel.ShowEmptyResult();
    }

    void ShowTick(NumberLineSoalPanel panel, int tick)
    {
        if (!panel) return;
        Session.Config.TickToMixed(tick, out int whole, out int numerator);
        panel.ShowValue(whole, numerator, Session.Config.partisi);
    }

    void RefreshJumpButtons()
    {
        bool hasProblem = Session.HasProblem;
        if (backwardButton) backwardButton.Interactable = hasProblem && Session.PionTick > 0;
        if (forwardButton) forwardButton.Interactable = hasProblem && Session.PionTick < Session.Config.MaxTick;
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

        // soal belum terjawab benar: tanya dulu; TIDAK mengembalikan tombol dan stepper ke nilai yang berlaku
        if (Session.NeedsConfirmBeforeReconfigure && confirmDialog != null)
        {
            confirmDialog.Ask(Media5Messages.ReconfigureWarning, () => ApplyConfig(length, partisi), Sync);
            return;
        }
        ApplyConfig(length, partisi);
    }

    void ApplyConfig(int length, int partisi)
    {
        Session.ApplyConfig(length, partisi);
        view.ClearTrail();
        view.SetPionTick(Session.PionTick, true);
    }

    // ---------------- lompat ----------------
    void OnForwardStep() => DoJump(true);
    void OnBackwardStep() => DoJump(false);

    void DoJump(bool forward)
    {
        int before = Session.PionTick;
        Media5JumpResult r = forward ? Session.JumpForward() : Session.JumpBackward();

        if (r == Media5JumpResult.Moved) view.AddTrailArc(before, Session.PionTick);
        else if (r == Media5JumpResult.NoProblem) Feedback.Show(Media5Messages.NoProblem, FeedbackKind.Info);
        // AtStart/AtEnd: tombolnya sudah nonaktif di ujung garis, tidak ada yang perlu ditampilkan
    }

    // ---------------- tombol ----------------
    // onClick BuatSoalBtn
    public void OnNewProblemPressed()
    {
        Media5ProblemError err = Session.TryNewProblem();
        if (err != Media5ProblemError.None)
        {
            Feedback.Show(Media5Messages.For(err, limits), FeedbackKind.Warning);
            return;
        }

        view.ClearTrail();
        view.SetPionTick(Session.PionTick, true);      // soal baru: pion langsung di A, tanpa lompatan
    }

    // onClick CobaLagiBtn
    public void OnRetryPressed()
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

    // onClick tombol CEK
    public void OnCheckPressed()
    {
        switch (Session.Check())
        {
            case Media5CheckOutcome.Correct:
                ShowTick(resultPanel, Session.ResultTick);      // setelah Sync dari Check, supaya tidak langsung dikosongkan
                Feedback.Show(Media5Messages.CheckCorrect, FeedbackKind.Success);
                break;
            case Media5CheckOutcome.Wrong:
                Feedback.Show(Media5Messages.CheckWrong, FeedbackKind.Warning);
                break;
            case Media5CheckOutcome.NoProblem:
                Feedback.Show(Media5Messages.NoProblem, FeedbackKind.Info);
                break;
        }
    }

    // onClick tombol Reset
    public void OnResetPressed()
    {
        if (Session.IsPristine)
        {
            Feedback.Show(Media5Messages.NothingToReset, FeedbackKind.Info);
            return;
        }

        if (confirmDialog != null) confirmDialog.Ask(Media5Messages.ResetWarning, ResetAll);
        else ResetAll();
    }

    void ResetAll()
    {
        Session.ResetAll();
        view.ClearTrail();
        view.SetPionTick(Session.PionTick, true);
        Feedback.Show(Media5Messages.ResetDone, FeedbackKind.Success);
    }
}