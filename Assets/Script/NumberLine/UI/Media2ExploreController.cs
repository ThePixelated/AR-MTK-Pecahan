using UnityEngine;

// Controller halaman Media 2 mode Eksplorasi: menghubungkan garis bilangan, panel atur garis, panel Soal, numpad,
// CEK, Reset, dan popup ke satu NumberLineSession. Satu instance per halaman.
public class Media2ExploreController : MonoBehaviour
{
    [SerializeField] NumberLineLimits limits = new NumberLineLimits();
    [SerializeField] NumberLineView view;
    [SerializeField] LineSettingsPanel lineSettings;
    [SerializeField] NumberLineSoalPanel soalPanel;
    [SerializeField] NumberLineNumpad numpad;
    [SerializeField] SimpleConfirmDialog confirmDialog;

    NumberLineSession session;
    public NumberLineSession Session => session ??= new NumberLineSession(limits);

    bool wired;

    void OnEnable()
    {
        if (view == null || lineSettings == null || soalPanel == null || numpad == null)
        {
            Debug.LogError($"[{name}] Media2ExploreController: View, Line Settings, Soal Panel, dan Numpad harus diisi.", this);
            return;
        }
        wired = true;

        lineSettings.Configure(limits);
        lineSettings.LengthChosen += OnLengthChosen;
        lineSettings.PartisiChanged += OnPartisiChanged;

        soalPanel.Bind(Session.Soal);
        soalPanel.CellTapped += OnCellTapped;

        numpad.Overflowed += OnOverflow;
        numpad.Entered += OnEntered;
        numpad.Closed += OnNumpadClosed;

        view.PionDragged += OnPionDragged;
        view.DragEnabled = true;

        Session.Changed += Sync;
        Sync();
    }

    void OnDisable()
    {
        if (!wired) return;
        wired = false;

        if (numpad) numpad.Close();        // sebelum berhenti mendengarkan, supaya fokus kotak ikut dibersihkan

        if (lineSettings)
        {
            lineSettings.LengthChosen -= OnLengthChosen;
            lineSettings.PartisiChanged -= OnPartisiChanged;
        }
        if (soalPanel)
        {
            soalPanel.CellTapped -= OnCellTapped;
            soalPanel.Unbind();
        }
        if (numpad)
        {
            numpad.Overflowed -= OnOverflow;
            numpad.Entered -= OnEntered;
            numpad.Closed -= OnNumpadClosed;
        }
        if (view) view.PionDragged -= OnPionDragged;
        if (session != null) session.Changed -= Sync;
    }

    // Tampilan mengikuti session
    void Sync()
    {
        view.SetConfig(Session.Config);
        view.SetPionTick(Session.PionTick);
        lineSettings.Show(Session.Config);
    }

    // ---------------- atur garis ----------------
    void OnLengthChosen(int length) => TryReconfigure(length, Session.Config.partisi);
    void OnPartisiChanged(int partisi) => TryReconfigure(Session.Config.length, partisi);

    void TryReconfigure(int length, int partisi)
    {
        NumberLineConfigError err = Session.Validate(length, partisi);
        if (err != NumberLineConfigError.None)
        {
            Feedback.Show(NumberLineMessages.For(err, limits), FeedbackKind.Warning);
            Sync();
            return;
        }

        if (length == Session.Config.length && partisi == Session.Config.partisi)
        {
            Sync();
            return;
        }

        // pion sudah digeser: tanya dulu; TIDAK mengembalikan tombol dan stepper ke nilai yang berlaku
        if (Session.NeedsConfirmBeforeReconfigure && confirmDialog != null)
        {
            confirmDialog.Ask(NumberLineMessages.ReconfigureWarning, () => Session.ApplyConfig(length, partisi), Sync);
            return;
        }
        Session.ApplyConfig(length, partisi);
    }

    // ---------------- pion ----------------
    void OnPionDragged(int tick) => Session.SetPionTick(tick);

    // ---------------- Soal dan numpad ----------------
    void OnCellTapped(NumberLineSoalPanel panel, NumberLineCell cell)
    {
        numpad.Open(panel.Input, cell);
        panel.SetFocusedCell(cell);
    }

    void OnNumpadClosed() => soalPanel.SetFocusedCell(null);

    void OnOverflow(int max) => Feedback.Show(NumberLineMessages.CellOverflow(max), FeedbackKind.Warning);

    // Enter: nilai Soal dikunci; pecahan tidak mungkin dikosongkan, nilai di luar garis diberi tahu
    void OnEntered()
    {
        NumberLineCommit c = Session.CommitSoal();

        if (c.status == NumberLineSoalStatus.Impossible)
            Feedback.Show(NumberLineMessages.SoalImpossible, FeedbackKind.Warning);
        else if (c.status == NumberLineSoalStatus.Valid && c.reach == NumberLineReach.BeyondLine)
            Feedback.Show(NumberLineMessages.SoalBeyondLine, FeedbackKind.Info);
    }

    // ---------------- tombol ----------------
    // onClick tombol CEK
    public void OnCheckPressed()
    {
        numpad.Close();
        NumberLineCheckResult r = Session.Check();

        switch (r.outcome)
        {
            case NumberLineCheckOutcome.Correct:
                Feedback.Show(NumberLineMessages.CheckCorrect, FeedbackKind.Success);
                break;
            case NumberLineCheckOutcome.Wrong:
                Feedback.Show(NumberLineMessages.CheckWrong, FeedbackKind.Warning);
                break;
            case NumberLineCheckOutcome.SoalEmpty:
                Feedback.Show(NumberLineMessages.SoalEmpty, FeedbackKind.Info);
                break;
            case NumberLineCheckOutcome.SoalIncomplete:
                Feedback.Show(NumberLineMessages.SoalIncomplete, FeedbackKind.Info);
                break;
            case NumberLineCheckOutcome.SoalImpossible:
                Feedback.Show(NumberLineMessages.SoalImpossible, FeedbackKind.Warning);
                break;
        }
    }

    // onClick tombol Reset
    public void OnResetPressed()
    {
        if (Session.IsPristine)
        {
            Feedback.Show(NumberLineMessages.NothingToReset, FeedbackKind.Info);
            return;
        }

        if (confirmDialog != null) confirmDialog.Ask(NumberLineMessages.ResetWarning, ResetAll);
        else ResetAll();
    }

    void ResetAll()
    {
        numpad.Close();
        Session.ResetAll();
        Feedback.Show(NumberLineMessages.ResetDone, FeedbackKind.Success);
    }
}