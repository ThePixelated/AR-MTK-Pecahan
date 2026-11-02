using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Controller halaman Latihan Media 2 (Level 1, 2, 3): pemilih level, soal aktif, jawaban siswa, CEK dan LANJUT, popup.
// Semua keputusan ada di LatihanSession; kelas ini hanya mengikuti QuestionLayout dan menampilkan hasilnya.
public class LatihanController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] LatihanOptions options = new LatihanOptions();
    [SerializeField] NumberLineLevelData[] levels;               // urut: Level 1, Level 2, Level 3
    [SerializeField] SimpleConfirmDialog confirmDialog;

    [Header("Pemilih level (BackPanel Latihan)")]
    [SerializeField] Button[] levelButtons;                      // Level1Btn, Level2Btn, Level3Btn
    [SerializeField] GameObject[] levelLockedMarks;              // opsional, sejajar dengan levelButtons
    [SerializeField] GameObject[] levelDoneMarks;                // opsional, sejajar dengan levelButtons

    [Header("Transisi: sambungkan ke PanelAnimator")]
    [SerializeField] UnityEvent onLevelStarted;                  // dari pemilih level masuk ke permainan
    [SerializeField] UnityEvent onReturnToPicker;                // dari permainan kembali ke pemilih level

    [Header("Sisi kiri")]
    [SerializeField] GameObject layoutLevel1;                    // panel atur garis dan pemilih hewan
    [SerializeField] GameObject layoutLevel2;                    // numpad dan panel Soal (Level 2 dan 3)
    [SerializeField] LineSettingsPanel lineSettings;
    [SerializeField] AnimalChooserPanel animalChooser;
    [SerializeField] NumberLineNumpad numpad;
    [SerializeField] SoalPanelGrid soalGrid;

    [Header("Sisi kanan")]
    [SerializeField] GameObject lineSection;                     // GarisBilanganSection (disembunyikan di Level 3)
    [SerializeField] NumberLineView view;
    [SerializeField] TMP_Text soalText;                          // SoalTxt
    [SerializeField] GameObject imageContainer;                  // ImageContainer
    [SerializeField] Image soalImage;                            // gambar di dalam ImageContainer
    [SerializeField] Button checkButton;                         // CekJawabanBtn
    [SerializeField] TMP_Text checkLabel;                        // teks di tombol (opsional)
    [SerializeField] string checkText = "CEK JAWABAN";
    [SerializeField] string nextText = "LANJUT";

    [Header("Navbar (opsional)")]
    [SerializeField] Button backButton;                          // BackBtn di Navbar permainan
    [SerializeField] TMP_Text soalIndicatorText;                 // SoalIndicatorTxt
    [SerializeField] string soalIndicatorFormat = "Soal {0}";    // {0} = nomor soal, {1} = jumlah soal
    [SerializeField] Slider progressSlider;                      // SoalIndicatorSlider (diisi 0..1 oleh script)
    [SerializeField] Image progressFill;                         // alternatif: Image bertipe Filled
    [SerializeField] Image levelTitleImage;                      // ImageLevelTitle
    [SerializeField] Sprite[] levelTitleSprites;                 // sejajar dengan level
    [SerializeField] TMP_Text timerText;                         // hanya terisi kalau Enable Timer menyala
    [SerializeField] TMP_Text strikeText;                        // hanya terisi kalau Enable Wrong Strike menyala

    LatihanSession session;
    public LatihanSession Session => session ??= CreateSession();

    readonly List<(Button button, UnityAction action)> boundButtons = new List<(Button, UnityAction)>();
    NumberLineSoalInput focusedInput;
    bool wired, validated;

    LatihanSession CreateSession()
    {
        var list = new List<LatihanLevel>();
        if (levels != null)
            foreach (NumberLineLevelData d in levels) list.Add(d != null ? d.ToLevel() : new LatihanLevel());

        if (!validated)
        {
            validated = true;
            for (int l = 0; l < list.Count; l++)
                for (int q = 0; q < list[l].questions.Count; q++)
                    foreach (string issue in list[l].questions[q].Validate(options.Line, options.MaxMarkers))
                        Debug.LogWarning($"[{name}] Level {l + 1} soal {q + 1} ({list[l].questions[q].kind}): {issue}", this);
        }
        return new LatihanSession(list, options);
    }

    // ---------------- siklus ----------------
    void OnEnable()
    {
        if (view == null || lineSettings == null || numpad == null || soalGrid == null)
        {
            Debug.LogError($"[{name}] LatihanController: View, Line Settings, Numpad, dan Soal Grid harus diisi.", this);
            return;
        }
        wired = true;

        for (int i = 0; levelButtons != null && i < levelButtons.Length; i++)
        {
            int level = i;
            BindButton(levelButtons[i], () => OnLevelButton(level));
        }
        BindButton(checkButton, OnCheckPressed);
        BindButton(backButton, OnBackPressed);

        lineSettings.Configure(options.Line);
        lineSettings.LengthChosen += OnLengthChosen;
        lineSettings.PartisiChanged += OnPartisiChanged;
        view.PionDragged += OnPionDragged;
        soalGrid.CellTapped += OnCellTapped;
        numpad.Overflowed += OnOverflow;
        numpad.Entered += OnNumpadEntered;
        numpad.Closed += OnNumpadClosed;
        Session.Changed += OnSessionChanged;

        if (progressSlider)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.interactable = false;
        }
        RefreshLevelButtons();
    }

    void OnDisable()
    {
        if (!wired) return;
        wired = false;

        foreach (var (button, action) in boundButtons) if (button) button.onClick.RemoveListener(action);
        boundButtons.Clear();

        if (numpad) numpad.PageGone();
        if (lineSettings)
        {
            lineSettings.LengthChosen -= OnLengthChosen;
            lineSettings.PartisiChanged -= OnPartisiChanged;
        }
        if (view) view.PionDragged -= OnPionDragged;
        if (soalGrid) soalGrid.CellTapped -= OnCellTapped;
        if (numpad)
        {
            numpad.Overflowed -= OnOverflow;
            numpad.Entered -= OnNumpadEntered;
            numpad.Closed -= OnNumpadClosed;
        }
        if (session != null)
        {
            session.Changed -= OnSessionChanged;
            if (session.CurrentLevel >= 0) session.ExitLevel();      // halaman ditinggalkan: kembali ke pemilih level saat dibuka lagi
        }
    }

    void BindButton(Button button, UnityAction action)
    {
        if (!button) return;
        button.onClick.AddListener(action);
        boundButtons.Add((button, action));
    }

    void Update()
    {
        if (Session.CurrentLevel < 0 || !options.EnableTimer) return;

        bool wasFailed = Session.Failed;
        Session.Tick(Time.unscaledDeltaTime);
        UpdateTimer();
        if (!wasFailed && Session.Failed) ShowFailure();
    }

    // ---------------- pemilih level ----------------
    void RefreshLevelButtons()
    {
        for (int i = 0; levelButtons != null && i < levelButtons.Length; i++)
        {
            if (levelLockedMarks != null && i < levelLockedMarks.Length && levelLockedMarks[i])
                levelLockedMarks[i].SetActive(!Session.IsUnlocked(i));
            if (levelDoneMarks != null && i < levelDoneMarks.Length && levelDoneMarks[i])
                levelDoneMarks[i].SetActive(Session.IsCompleted(i));
        }
    }

    void OnLevelButton(int level)
    {
        switch (Session.StartLevel(level))
        {
            case LatihanStart.Locked:
                Feedback.Show(LatihanMessages.LevelLocked(level), FeedbackKind.Info);
                return;
            case LatihanStart.NoQuestions:
                Feedback.Show(LatihanMessages.NoQuestions, FeedbackKind.Warning);
                return;
            case LatihanStart.InvalidLevel:
                Feedback.Show(LatihanMessages.NotAvailable, FeedbackKind.Info);
                return;
        }

        ShowQuestion();
        onLevelStarted?.Invoke();
    }

    void ExitToPicker()
    {
        numpad.Close();
        Session.ExitLevel();
        RefreshLevelButtons();
        onReturnToPicker?.Invoke();
    }

    public void OnBackPressed()
    {
        if (Session.CurrentLevel < 0) return;
        if (confirmDialog != null) confirmDialog.Ask(LatihanMessages.ExitWarning, ExitToPicker);
        else ExitToPicker();
    }

    // ---------------- soal aktif ----------------
    void ShowQuestion()
    {
        NumberLineQuestion q = Session.Current;
        if (q == null) return;
        QuestionLayout layout = Session.Layout;

        numpad.Close();
        if (layoutLevel1) layoutLevel1.SetActive(!layout.NeedsNumpad);
        if (layoutLevel2) layoutLevel2.SetActive(layout.NeedsNumpad);
        if (lineSection) lineSection.SetActive(layout.showLine);

        // garis dan pion
        view.SetConfig(Session.Config);
        view.DragEnabled = layout.pionUnlocked;
        view.SetPionVisible(layout.showPion);
        view.SetPionTick(Session.PionTick, true);
        SetupMarkers(q);

        lineSettings.Show(Session.Config);
        lineSettings.SetLocked(!layout.lineUnlocked);
        if (animalChooser) animalChooser.SetLocked(!layout.pionUnlocked);

        // panel Soal
        if (layout.panelCount > 0) soalGrid.Build(Session.Soal);
        else soalGrid.Clear();

        // teks dan gambar soal
        if (soalText) soalText.text = q.FormattedText();
        bool hasImage = q.image != null;
        if (imageContainer) imageContainer.SetActive(hasImage);
        if (soalImage && hasImage) soalImage.sprite = q.image;

        UpdateNavbar();
        UpdateCheckButton();
        UpdateTimer();
    }

    void SetupMarkers(NumberLineQuestion q)
    {
        if (q.kind != QuestionKind.MultiPosition) { view.ClearMarkers(); return; }

        int n = Session.MarkerTicks.Length;
        var labels = new string[n];
        for (int i = 0; i < n; i++) labels[i] = NumberLineLabels.Letter(i);
        view.SetMarkers(Session.MarkerTicks, labels);
    }

    // Tampilan mengikuti session saat garis atau pion berubah
    void OnSessionChanged()
    {
        if (Session.Current == null) return;
        view.SetConfig(Session.Config);
        view.SetPionTick(Session.PionTick);
        lineSettings.Show(Session.Config);
        UpdateNavbar();
        UpdateCheckButton();
    }

    // ---------------- atur garis dan pion ----------------
    void OnLengthChosen(int length) => TryReconfigure(length, Session.Config.partisi);
    void OnPartisiChanged(int partisi) => TryReconfigure(Session.Config.length, partisi);

    void TryReconfigure(int length, int partisi)
    {
        if (Session.Current == null || !Session.Layout.lineUnlocked || (length == Session.Config.length && partisi == Session.Config.partisi))
        {
            if (Session.Current != null) lineSettings.Show(Session.Config);
            return;
        }

        if (Session.NeedsConfirmBeforeReconfigure && confirmDialog != null)
        {
            confirmDialog.Ask(LatihanMessages.ReconfigureWarning, () => ApplyConfig(length, partisi), () => lineSettings.Show(Session.Config));
            return;
        }
        ApplyConfig(length, partisi);
    }

    void ApplyConfig(int length, int partisi)
    {
        LatihanConfigResult r = Session.ApplyConfig(length, partisi);
        if (r == LatihanConfigResult.LengthOutOfRange || r == LatihanConfigResult.PartisiOutOfRange)
            Feedback.Show(LatihanMessages.LineOutOfRange(options.Line), FeedbackKind.Warning);

        lineSettings.Show(Session.Config);
        view.SetPionTick(Session.PionTick, true);     // garis berubah: pion kembali ke 0 tanpa meluncur
    }

    void OnPionDragged(int tick) => Session.SetPionTick(tick);

    // ---------------- panel Soal dan numpad ----------------
    void OnCellTapped(NumberLineSoalPanel panel, NumberLineCell cell)
    {
        focusedInput = panel.Input;
        numpad.Open(panel.Input, cell);
        soalGrid.SetFocus(panel, cell);
    }

    void OnNumpadClosed()
    {
        soalGrid.SetFocus(null, null);
        focusedInput = null;
    }

    void OnOverflow(int max) => Feedback.Show(NumberLineMessages.CellOverflow(max), FeedbackKind.Warning);

    // Enter: penyebut 0 dikosongkan dengan notifikasi
    void OnNumpadEntered()
    {
        if (focusedInput == null) return;
        if (focusedInput.Evaluate(out _) != NumberLineSoalStatus.Impossible) return;

        focusedInput.Clear();
        Feedback.Show(NumberLineMessages.SoalImpossible, FeedbackKind.Warning);
    }

    // ---------------- CEK dan LANJUT ----------------
    void OnCheckPressed()
    {
        if (Session.Current == null) return;

        if (Session.CanAdvance) { Advance(); return; }

        numpad.Close();
        HandleCheck(Session.Check());
    }

    void HandleCheck(LatihanCheckResult r)
    {
        bool stay = levels[Session.CurrentLevel] != null && levels[Session.CurrentLevel].stayUntilCorrect;

        switch (r.outcome)
        {
            case LatihanOutcome.Correct:
                soalGrid.ShowIndicators(r.indicators);
                Feedback.Show(LatihanMessages.Correct, FeedbackKind.Success);
                break;

            case LatihanOutcome.Wrong:
                soalGrid.ShowIndicators(r.indicators);
                Feedback.Show(stay ? LatihanMessages.WrongStay : LatihanMessages.WrongContinue, FeedbackKind.Warning);
                break;

            case LatihanOutcome.Incomplete:
                Feedback.Show(Session.Layout.panelCount > 1 ? LatihanMessages.IncompleteMulti : LatihanMessages.IncompleteSingle, FeedbackKind.Info);
                break;
        }

        UpdateNavbar();
        UpdateCheckButton();
        if (Session.Failed) ShowFailure();
    }

    void Advance()
    {
        switch (Session.Advance())
        {
            case LatihanAdvance.NextQuestion:
                ShowQuestion();
                break;

            case LatihanAdvance.LevelCompleted:
                ShowLevelCompleted();
                break;
        }
    }

    void ShowLevelCompleted()
    {
        numpad.Close();
        RefreshLevelButtons();
        string title = Session.LevelTitle(Session.CurrentLevel);

        if (confirmDialog != null)
            confirmDialog.Inform(LatihanMessages.LevelDoneTitle, LatihanMessages.LevelDone(title), LatihanMessages.LevelDoneButton, ExitToPicker);
        else
            ExitToPicker();
    }

    void ShowFailure()
    {
        numpad.Close();
        string reason = Session.FailReason == LatihanFailReason.StrikeLimit ? LatihanMessages.StrikeFailed : LatihanMessages.TimeFailed;

        if (confirmDialog != null) confirmDialog.Inform(LatihanMessages.FailTitle, reason, LatihanMessages.FailButton, RestartLevel);
        else RestartLevel();
    }

    void RestartLevel()
    {
        if (Session.RestartLevel() == LatihanStart.Started) ShowQuestion();
    }

    // ---------------- navbar dan tombol ----------------
    void UpdateCheckButton()
    {
        if (checkLabel) checkLabel.text = Session.CanAdvance ? nextText : checkText;
    }

    void UpdateNavbar()
    {
        if (Session.CurrentLevel < 0) return;

        if (soalIndicatorText)
            soalIndicatorText.text = string.Format(soalIndicatorFormat, Session.QuestionIndex + 1, Session.QuestionCount);

        float progress = Session.QuestionCount == 0 ? 0f
            : (Session.QuestionIndex + (Session.CanAdvance ? 1 : 0)) / (float)Session.QuestionCount;

        if (progressSlider) progressSlider.value = progress;
        if (progressFill) progressFill.fillAmount = progress;

        int level = Session.CurrentLevel;
        if (levelTitleImage && levelTitleSprites != null && level < levelTitleSprites.Length && levelTitleSprites[level])
            levelTitleImage.sprite = levelTitleSprites[level];

        if (strikeText)
        {
            strikeText.gameObject.SetActive(options.EnableWrongStrike);
            strikeText.text = $"Salah {Session.WrongStrikes}/{options.MaxWrongStrikes}";
        }
    }

    void UpdateTimer()
    {
        if (!timerText) return;
        timerText.gameObject.SetActive(options.EnableTimer);
        if (!options.EnableTimer) return;

        int seconds = Mathf.CeilToInt(Session.TimeLeft);
        timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }
}