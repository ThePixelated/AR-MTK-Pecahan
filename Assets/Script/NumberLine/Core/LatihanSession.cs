using System;
using System.Collections.Generic;

[Serializable]
public class LatihanOptions
{
    public NumberLineLimits Line = new NumberLineLimits();       // batas garis dan sel Soal (sama dengan Eksplorasi)
    public int MaxMarkers = 3;                                   // MaxPion: titik bersurat maksimal per soal Level 2
    public int MaxPions = 5;                                     // MaxPion: pion maksimal per soal MultiPion (soal baru berisi 3 pion)
    public bool UnlockAllLevels = false;                         // uji: semua level terbuka

    [UnityEngine.Header("Scaffold (mati secara default)")]
    public bool EnableWrongStrike = false;
    public int MaxWrongStrikes = 3;                              // salah sebanyak ini: level gagal
    public bool EnableTimer = false;
    public float TimerSeconds = 60f;                             // per soal
    public bool EnableSoalBank = false;                          // soal diacak
    public int BankPickCount = 0;                                // 0 = semua soal; selain itu ambil sebanyak ini
}

public sealed class LatihanLevel
{
    public string title = "";
    public bool stayUntilCorrect = true;        // true: baru bisa lanjut kalau benar; false: setelah CEK boleh lanjut
    public List<NumberLineQuestion> questions = new List<NumberLineQuestion>();
}

// Apa yang tampil dan terbuka untuk satu soal; UI cukup mengikuti struct ini
public readonly struct QuestionLayout
{
    public readonly bool showLine, lineUnlocked, showPion, pionUnlocked, showMarkers;
    public readonly bool multiPion, chooserLocked;      // MultiPion: banyak pion (lihat LatihanSession.Board); pemilih hewan terkunci
    public readonly int panelCount;

    public QuestionLayout(bool showLine, bool lineUnlocked, bool showPion, bool pionUnlocked, bool showMarkers, int panelCount,
                          bool multiPion = false, bool chooserLocked = false)
    {
        this.showLine = showLine;
        this.lineUnlocked = lineUnlocked;
        this.showPion = showPion;
        this.pionUnlocked = pionUnlocked;
        this.showMarkers = showMarkers;
        this.panelCount = panelCount;
        this.multiPion = multiPion;
        this.chooserLocked = chooserLocked;
    }

    public bool NeedsNumpad => panelCount > 0;

    public static QuestionLayout For(NumberLineQuestion q)
    {
        switch (q.kind)
        {
            case QuestionKind.LineOnly: return new QuestionLayout(true, true, false, false, false, 0);
            case QuestionKind.PionOnly: return new QuestionLayout(true, false, true, true, false, 0);
            case QuestionKind.LineAndPion: return new QuestionLayout(true, true, true, true, false, 0);
            case QuestionKind.MultiPosition: return new QuestionLayout(true, false, false, false, true, q.markers != null ? q.markers.Count : 0);
            case QuestionKind.MultiPion: return new QuestionLayout(true, !q.lockConfig, false, false, false, 0, true, q.lockConfig);
            default: return new QuestionLayout(false, false, false, false, false, 1);
        }
    }
}

public enum LatihanStart { Started, Locked, NoQuestions, InvalidLevel }
public enum LatihanConfigResult { Applied, Unchanged, Locked, LengthOutOfRange, PartisiOutOfRange }
public enum LatihanOutcome { Correct, Wrong, Incomplete, NoQuestion, Blocked }
public enum LatihanAdvance { NextQuestion, LevelCompleted, NotAllowed }
public enum LatihanFailReason { None, StrikeLimit, TimeUp }

public sealed class LatihanCheckResult
{
    public LatihanOutcome outcome;
    public NumberLineIndicator[] indicators = new NumberLineIndicator[0];   // satu per panel; semua None kalau belum lengkap
}

// State mode Latihan Media 2: level, soal aktif, jawaban siswa (garis, pion, panel Soal), penilaian, kemajuan
public sealed class LatihanSession
{
    public LatihanOptions Options { get; }

    readonly List<LatihanLevel> levels;
    readonly bool[] completed;
    readonly Random rng;
    List<NumberLineQuestion> active = new List<NumberLineQuestion>();

    public int LevelCount => levels.Count;
    public int CurrentLevel { get; private set; } = -1;          // -1 = di pemilih level
    public int QuestionIndex { get; private set; }
    public int QuestionCount => active.Count;
    public bool IsFinished { get; private set; }                 // semua soal level ini sudah dilewati
    public NumberLineQuestion Current =>
        CurrentLevel >= 0 && !IsFinished && QuestionIndex < active.Count ? active[QuestionIndex] : null;

    public QuestionLayout Layout { get; private set; }
    public NumberLineConfig Config { get; private set; }
    public int PionTick { get; private set; }
    public NumberLineSoalInput[] Soal { get; private set; } = new NumberLineSoalInput[0];
    public int[] MarkerTicks { get; private set; } = new int[0];     // tick tiap titik bersurat pada garis tetap (-1 = soal tidak sah)
    public MultiPionBoard Board { get; private set; }                // hanya ada untuk soal MultiPion; null untuk kind lain

    public bool IsSolved { get; private set; }
    public bool HasChecked { get; private set; }                 // sudah dinilai (benar atau salah) sekali
    public int WrongStrikes { get; private set; }
    public float TimeLeft { get; private set; }
    public LatihanFailReason FailReason { get; private set; }
    public bool Failed => FailReason != LatihanFailReason.None;

    public event Action Changed;

    public LatihanSession(IEnumerable<LatihanLevel> levels, LatihanOptions options = null, int? seed = null)
    {
        this.levels = new List<LatihanLevel>(levels);
        Options = options ?? new LatihanOptions();
        completed = new bool[this.levels.Count];
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    NumberLineConfig DefaultConfig() => new NumberLineConfig(
        Math.Clamp(Options.Line.DefaultLength, Options.Line.MinLength, Options.Line.MaxLength),
        Math.Clamp(Options.Line.DefaultPartisi, Options.Line.MinPartisi, Options.Line.MaxPartisi));

    // ---------- level ----------
    public bool IsUnlocked(int level) =>
        level >= 0 && level < levels.Count && (Options.UnlockAllLevels || level == 0 || completed[level - 1]);

    public bool IsCompleted(int level) => level >= 0 && level < levels.Count && completed[level];

    public string LevelTitle(int level) => level >= 0 && level < levels.Count ? levels[level].title : "";

    public LatihanStart StartLevel(int level)
    {
        if (level < 0 || level >= levels.Count) return LatihanStart.InvalidLevel;
        if (!IsUnlocked(level)) return LatihanStart.Locked;
        if (levels[level].questions.Count == 0) return LatihanStart.NoQuestions;

        var picked = new List<NumberLineQuestion>(levels[level].questions);
        if (Options.EnableSoalBank)
        {
            Shuffle(picked);
            int count = Options.BankPickCount > 0 ? Math.Min(Options.BankPickCount, picked.Count) : picked.Count;
            picked = picked.GetRange(0, count);
        }

        CurrentLevel = level;
        active = picked;
        QuestionIndex = 0;
        IsFinished = false;
        WrongStrikes = 0;
        FailReason = LatihanFailReason.None;
        LoadQuestion();
        return LatihanStart.Started;
    }

    // Mengulang level yang sedang berjalan (mis. setelah gagal)
    public LatihanStart RestartLevel() => CurrentLevel < 0 ? LatihanStart.InvalidLevel : StartLevel(CurrentLevel);

    public void ExitLevel()
    {
        CurrentLevel = -1;
        active = new List<NumberLineQuestion>();
        QuestionIndex = 0;
        IsFinished = false;
        FailReason = LatihanFailReason.None;
        Layout = default;
        Soal = new NumberLineSoalInput[0];
        MarkerTicks = new int[0];
        if (Board != null) { Board.Changed -= OnBoardChanged; Board = null; }
        Changed?.Invoke();
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void LoadQuestion()
    {
        NumberLineQuestion q = active[QuestionIndex];
        Layout = QuestionLayout.For(q);

        if (Board != null) Board.Changed -= OnBoardChanged;
        Board = q.kind == QuestionKind.MultiPion ? new MultiPionBoard(q, Options.Line, Options.MaxPions) : null;
        if (Board != null) Board.Changed += OnBoardChanged;

        bool presetLine = q.kind == QuestionKind.PionOnly || q.kind == QuestionKind.MultiPosition;
        Config = presetLine
            ? new NumberLineConfig(Math.Clamp(q.presetLength, Options.Line.MinLength, Options.Line.MaxLength),
                                   Math.Clamp(q.presetPartisi, Options.Line.MinPartisi, Options.Line.MaxPartisi))
            : DefaultConfig();
        if (Board != null) Config = Board.Config;      // MultiPion: panjang dari soal, partisi mengikuti pion yang dipilih
        PionTick = 0;

        Soal = new NumberLineSoalInput[Layout.panelCount];
        for (int i = 0; i < Soal.Length; i++) Soal[i] = new NumberLineSoalInput(Options.Line.MaxCell);

        int markerCount = q.kind == QuestionKind.MultiPosition && q.markers != null ? q.markers.Count : 0;
        MarkerTicks = new int[markerCount];
        for (int i = 0; i < markerCount; i++)
        {
            NumberLineReach reach = Config.Classify(q.markers[i].ToFraction(), out int tick);
            MarkerTicks[i] = reach == NumberLineReach.OnLine ? tick : -1;
        }

        IsSolved = false;
        HasChecked = false;
        TimeLeft = Options.TimerSeconds;
        Changed?.Invoke();
    }

    // ---------- jawaban siswa ----------
    // true = mengubah garis akan memindahkan pion ke 0, jadi UI menampilkan popup dulu
    public bool NeedsConfirmBeforeReconfigure => Layout.showPion && Layout.lineUnlocked && PionTick != 0;

    public LatihanConfigResult ApplyConfig(int length, int partisi)
    {
        if (Current == null || !Layout.lineUnlocked) return LatihanConfigResult.Locked;

        if (Board != null)
        {
            // MultiPion: hanya panjang yang bisa diubah siswa; partisi garis mengikuti pion yang dipilih
            if (length < Options.Line.MinLength || length > Options.Line.MaxLength) return LatihanConfigResult.LengthOutOfRange;
            return Board.ApplyLength(length) ? LatihanConfigResult.Applied : LatihanConfigResult.Unchanged;
        }

        NumberLineLimits l = Options.Line;
        if (length < l.MinLength || length > l.MaxLength) return LatihanConfigResult.LengthOutOfRange;
        if (partisi < l.MinPartisi || partisi > l.MaxPartisi) return LatihanConfigResult.PartisiOutOfRange;
        if (length == Config.length && partisi == Config.partisi) return LatihanConfigResult.Unchanged;

        Config = new NumberLineConfig(length, partisi);
        PionTick = 0;
        Changed?.Invoke();
        return LatihanConfigResult.Applied;
    }

    // Mengembalikan tick yang berlaku setelah dibatasi; pion terkunci = tidak ada yang berubah
    public int SetPionTick(int tick)
    {
        if (Current == null || !Layout.pionUnlocked) return PionTick;

        int clamped = tick < 0 ? 0 : tick > Config.MaxTick ? Config.MaxTick : tick;
        if (clamped == PionTick) return clamped;
        PionTick = clamped;
        Changed?.Invoke();
        return clamped;
    }

    // Papan MultiPion berubah (pion dipilih, ditaruh, dicabut, atau panjang garis berubah): Config mengikuti partisi pion yang dipilih
    void OnBoardChanged()
    {
        Config = Board.Config;
        Changed?.Invoke();
    }

    // ---------- penilaian ----------
    public LatihanCheckResult Check()
    {
        var result = new LatihanCheckResult { outcome = LatihanOutcome.NoQuestion };
        NumberLineQuestion q = Current;
        if (q == null) return result;
        if (Failed) { result.outcome = LatihanOutcome.Blocked; return result; }

        switch (q.kind)
        {
            case QuestionKind.LineOnly:
                result.outcome = LineMatches(q) ? LatihanOutcome.Correct : LatihanOutcome.Wrong;
                break;

            case QuestionKind.PionOnly:
            case QuestionKind.LineAndPion:
                bool onTarget = Config.Classify(q.target.ToFraction(), out int tick) == NumberLineReach.OnLine && tick == PionTick;
                result.outcome = onTarget ? LatihanOutcome.Correct : LatihanOutcome.Wrong;
                break;

            case QuestionKind.MultiPosition:
                CheckMarkers(q, result);
                break;

            case QuestionKind.MultiPion:
                result.outcome = Board.Evaluate(out result.indicators);
                break;

            default:
                CheckComparison(q, result);
                break;
        }

        if (result.outcome == LatihanOutcome.Correct) { IsSolved = true; HasChecked = true; }
        else if (result.outcome == LatihanOutcome.Wrong) { HasChecked = true; RegisterWrong(); }

        Changed?.Invoke();
        return result;
    }

    bool LineMatches(NumberLineQuestion q)
    {
        if (Config.length == q.lineAnswer.length && Config.partisi == q.lineAnswer.partisi) return true;
        if (q.alternativeLineAnswers != null)
            foreach (LineAnswer a in q.alternativeLineAnswers)
                if (Config.length == a.length && Config.partisi == a.partisi) return true;
        return false;
    }

    // Semua panel harus terisi; baru setelah itu tiap panel dinilai menurut nilai
    void CheckMarkers(NumberLineQuestion q, LatihanCheckResult result)
    {
        int n = Soal.Length;
        var indicators = new NumberLineIndicator[n];
        bool incomplete = false, allCorrect = true;

        for (int i = 0; i < n; i++)
        {
            NumberLineSoalStatus status = Soal[i].Evaluate(out Fraction value);
            if (status == NumberLineSoalStatus.Impossible) Soal[i].Clear();
            if (status != NumberLineSoalStatus.Valid) { incomplete = true; continue; }

            bool ok = value.ValueEquals(q.markers[i].ToFraction());
            indicators[i] = ok ? NumberLineIndicator.Correct : NumberLineIndicator.Wrong;
            if (!ok) allCorrect = false;
        }

        if (incomplete)
        {
            result.outcome = LatihanOutcome.Incomplete;
            result.indicators = new NumberLineIndicator[n];          // tidak membocorkan benar atau salah sebelum semuanya diisi
            return;
        }

        result.outcome = allCorrect ? LatihanOutcome.Correct : LatihanOutcome.Wrong;
        result.indicators = indicators;
    }

    void CheckComparison(NumberLineQuestion q, LatihanCheckResult result)
    {
        NumberLineSoalStatus status = Soal[0].Evaluate(out Fraction value);
        if (status == NumberLineSoalStatus.Impossible) Soal[0].Clear();
        result.indicators = new NumberLineIndicator[1];

        if (status != NumberLineSoalStatus.Valid) { result.outcome = LatihanOutcome.Incomplete; return; }

        bool ok = q.rule == ComparisonRule.Exact
            ? value.ValueEquals(q.exact.ToFraction())
            : value.CompareTo(q.lowerBound.ToFraction()) > 0 && value.CompareTo(q.upperBound.ToFraction()) < 0;

        result.outcome = ok ? LatihanOutcome.Correct : LatihanOutcome.Wrong;
        result.indicators[0] = ok ? NumberLineIndicator.Correct : NumberLineIndicator.Wrong;
    }

    void RegisterWrong()
    {
        if (!Options.EnableWrongStrike) return;
        WrongStrikes++;
        if (WrongStrikes >= Options.MaxWrongStrikes) FailReason = LatihanFailReason.StrikeLimit;
    }

    // ---------- kemajuan ----------
    public bool CanAdvance =>
        Current != null && !Failed && (IsSolved || (!levels[CurrentLevel].stayUntilCorrect && HasChecked));

    public LatihanAdvance Advance()
    {
        if (!CanAdvance) return LatihanAdvance.NotAllowed;

        if (QuestionIndex + 1 < active.Count)
        {
            QuestionIndex++;
            LoadQuestion();
            return LatihanAdvance.NextQuestion;
        }

        completed[CurrentLevel] = true;      // membuka level berikutnya
        IsFinished = true;
        Changed?.Invoke();
        return LatihanAdvance.LevelCompleted;
    }

    // ---------- timer (scaffold) ----------
    // Dipanggil UI tiap frame dengan waktu nyata; hanya berpengaruh kalau EnableTimer menyala
    public void Tick(float deltaSeconds)
    {
        if (!Options.EnableTimer || Current == null || IsSolved || Failed) return;

        TimeLeft = Math.Max(0f, TimeLeft - deltaSeconds);
        if (TimeLeft <= 0f)
        {
            FailReason = LatihanFailReason.TimeUp;
            Changed?.Invoke();
        }
    }
}