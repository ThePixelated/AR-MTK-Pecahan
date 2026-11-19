using System;

// State mode Media 5. Dua keadaan:
//   Explore: belum ada soal. Hewan boleh belum ada, ditaruh, digeser, atau ditarik kembali.
//   Locked : ada soal (dari Generate Soal atau Enter Soal). Hewan terkunci, hanya bisa mundur (atau Ulangi ke A).
// Murni logika (tanpa UI), jadi bisa dites tanpa scene.
public sealed class Media5Session
{
    public Media5Limits Limits { get; }
    public NumberLineConfig Config { get; private set; }
    public bool HasPion { get; private set; }
    public int PionTick { get; private set; }
    public NumberLineProblem? Problem { get; private set; }
    public bool IsSolved { get; private set; }    // posisi pion dan jawaban pernah benar bersamaan untuk soal ini
    public int StartTick { get; private set; }    // A; tujuan tombol Ulangi

    public event Action Changed;

    readonly NumberLineProblemGenerator generator;

    public Media5Session(Media5Limits limits = null, int? seed = null)
    {
        Limits = limits ?? new Media5Limits();
        Config = DefaultConfig();
        generator = new NumberLineProblemGenerator(seed, Limits.AntiRepeatWindow);
    }

    NumberLineConfig DefaultConfig() => new NumberLineConfig(
        Math.Clamp(Limits.Line.DefaultLength, Limits.Line.MinLength, Limits.Line.MaxLength),
        Math.Clamp(Limits.Line.DefaultPartisi, Limits.Line.MinPartisi, Limits.Line.MaxPartisi));

    public bool HasProblem => Problem.HasValue;
    public bool IsLocked => Problem.HasValue;
    public int ResultTick => Problem.HasValue ? Problem.Value.ResultTick : 0;

    // true = mengubah garis membuang soal yang belum terjawab benar, jadi UI menampilkan popup dulu
    public bool NeedsConfirmBeforeReconfigure => HasProblem && !IsSolved;

    // true = keadaan awal: garis default, belum ada soal, belum ada hewan (isian A/B ditambahkan oleh controller)
    public bool IsPristine
    {
        get
        {
            NumberLineConfig d = DefaultConfig();
            return !HasProblem && !HasPion && PionTick == 0 && Config.length == d.length && Config.partisi == d.partisi;
        }
    }

    // ---- hewan (hanya saat Explore) ----
    // Menaruh atau menggeser hewan ke tick (dibatasi ke ujung garis). false = ditolak karena Locked.
    public bool PlacePion(int tick)
    {
        if (IsLocked) return false;

        tick = Math.Clamp(tick, 0, Config.MaxTick);
        if (HasPion && PionTick == tick) return true;

        HasPion = true;
        PionTick = tick;
        Changed?.Invoke();
        return true;
    }

    // Tombol Tarik Kembali: hewan keluar dari garis. false = tidak ada hewan atau sedang Locked.
    public bool RemovePion()
    {
        if (IsLocked || !HasPion) return false;

        HasPion = false;
        PionTick = 0;
        Changed?.Invoke();
        return true;
    }

    // ---- ukuran garis ----
    public NumberLineConfigError Validate(int length, int partisi)
    {
        if (length < Limits.Line.MinLength || length > Limits.Line.MaxLength) return NumberLineConfigError.LengthOutOfRange;
        if (partisi < Limits.Line.MinPartisi || partisi > Limits.Line.MaxPartisi) return NumberLineConfigError.PartisiOutOfRange;
        return NumberLineConfigError.None;
    }

    // Nilai sama dengan yang berlaku: tidak ada yang berubah.
    // Nilai berbeda: soal dibuang (tidak dibuat ulang), hewan (kalau ada) ke titik 0, memori anti-ulang dikosongkan.
    public NumberLineConfigError ApplyConfig(int length, int partisi)
    {
        NumberLineConfigError err = Validate(length, partisi);
        if (err != NumberLineConfigError.None) return err;
        if (length == Config.length && partisi == Config.partisi) return NumberLineConfigError.None;

        Config = new NumberLineConfig(length, partisi);
        Problem = null;
        IsSolved = false;
        PionTick = 0;
        StartTick = 0;
        generator.ClearMemory();
        Changed?.Invoke();
        return NumberLineConfigError.None;
    }

    // ---- soal ----
    // Generate Soal: soal baru untuk garis aktif. Mengunci; hewan pindah ke A (muncul kalau belum ada).
    public Media5ProblemError TryNewProblem()
    {
        if (Config.partisi < Limits.MinPartisiForProblem) return Media5ProblemError.PartisiTooSmall;

        generator.Window = Limits.AntiRepeatWindow;
        if (!generator.TryGenerate(Config, Limits.Operations, out NumberLineProblem p))
            return Media5ProblemError.NoProblemPossible;

        Lock(p);
        return Media5ProblemError.None;
    }

    // Enter Soal: A dan B dari isian siswa (pengurangan saja). Gagal = tidak ada yang berubah.
    // Aturan: A > B >= 1, A tidak melewati panjang garis, A, B, dan hasilnya tepat di tick.
    public Media5EnterError TryEnterProblem(NumberLineSoalInput a, NumberLineSoalInput b)
    {
        Media5EnterError err = ReadStatus(a, true, out Fraction va);
        if (err != Media5EnterError.None) return err;
        err = ReadStatus(b, false, out Fraction vb);
        if (err != Media5EnterError.None) return err;

        err = ReadTick(va, true, out int aTick);
        if (err != Media5EnterError.None) return err;
        err = ReadTick(vb, false, out int bTick);
        if (err != Media5EnterError.None) return err;

        if (bTick < 1) return Media5EnterError.BZero;
        if (aTick <= bTick) return Media5EnterError.AMustBeGreater;

        var p = new NumberLineProblem(NumberLineOperation.Subtract, aTick, bTick);
        if (!p.IsValidFor(Config)) return Media5EnterError.AMustBeGreater;     // pengaman; seharusnya tidak pernah terjadi

        Lock(p);
        return Media5EnterError.None;
    }

    void Lock(NumberLineProblem p)
    {
        Problem = p;
        IsSolved = false;
        StartTick = p.aTick;
        HasPion = true;
        PionTick = StartTick;
        Changed?.Invoke();
    }

    static Media5EnterError ReadStatus(NumberLineSoalInput input, bool isA, out Fraction value)
    {
        value = Fraction.Zero;
        if (input == null) return isA ? Media5EnterError.AMissing : Media5EnterError.BMissing;

        switch (input.Evaluate(out value))
        {
            case NumberLineSoalStatus.Empty:
                return isA ? Media5EnterError.AMissing : Media5EnterError.BMissing;
            case NumberLineSoalStatus.Incomplete:
                return isA ? Media5EnterError.AIncomplete : Media5EnterError.BIncomplete;
            case NumberLineSoalStatus.Impossible:
                return isA ? Media5EnterError.AImpossible : Media5EnterError.BImpossible;
        }
        return Media5EnterError.None;
    }

    Media5EnterError ReadTick(Fraction value, bool isA, out int tick)
    {
        switch (Config.Classify(value, out tick))
        {
            case NumberLineReach.NotOnPartition:
                return isA ? Media5EnterError.ANotOnTick : Media5EnterError.BNotOnTick;
            case NumberLineReach.BeyondLine:
                return isA ? Media5EnterError.AExceedsLine : Media5EnterError.BExceedsLine;
        }
        return Media5EnterError.None;
    }

    // Clear Soal: kembali ke Explore. Hewan tetap ada dan pindah ke titik 0. false = memang tidak ada soal.
    public bool ClearProblem()
    {
        if (!HasProblem) return false;

        Problem = null;
        IsSolved = false;
        StartTick = 0;
        PionTick = 0;
        Changed?.Invoke();
        return true;
    }

    // ---- gerak (hanya mundur, 1 tick per panggilan) ----
    public Media5JumpResult JumpBackward()
    {
        if (!HasProblem) return Media5JumpResult.NoProblem;
        if (PionTick <= 0) return Media5JumpResult.AtStart;

        PionTick--;
        Changed?.Invoke();
        return Media5JumpResult.Moved;
    }

    // Tombol Ulangi: hewan kembali ke A, tanpa konfirmasi. Status terjawab tidak berubah.
    public Media5RetryResult RetryPosition()
    {
        if (!HasProblem) return Media5RetryResult.NoProblem;
        if (PionTick == StartTick) return Media5RetryResult.AlreadyAtStart;

        PionTick = StartTick;
        Changed?.Invoke();
        return Media5RetryResult.Moved;
    }

    // ---- penilaian ----
    // Cek Hasil menilai dua hal: posisi pion DAN isi Panel Jawaban (pecahan ekuivalen dianggap sama, mis. 2/4 = 1/2).
    // Jalur yang ditempuh pion bebas.
    public Media5CheckResult Check(NumberLineSoalInput answer)
    {
        if (!HasProblem) return new Media5CheckResult(Media5CheckOutcome.NoProblem, Media5AnswerState.Empty);

        bool positionOk = PionTick == Problem.Value.ResultTick;
        Media5AnswerState state = ReadAnswer(answer);
        bool answerOk = state == Media5AnswerState.Correct;

        Media5CheckOutcome outcome;
        if (positionOk && answerOk) outcome = Media5CheckOutcome.Correct;
        else if (answerOk) outcome = Media5CheckOutcome.WrongPosition;
        else if (!positionOk) outcome = Media5CheckOutcome.WrongBoth;
        else if (state == Media5AnswerState.Empty) outcome = Media5CheckOutcome.AnswerEmpty;
        else if (state == Media5AnswerState.Incomplete) outcome = Media5CheckOutcome.AnswerIncomplete;
        else outcome = Media5CheckOutcome.WrongAnswer;

        if (outcome == Media5CheckOutcome.Correct && !IsSolved)
        {
            IsSolved = true;
            Changed?.Invoke();
        }
        return new Media5CheckResult(outcome, state);
    }

    Media5AnswerState ReadAnswer(NumberLineSoalInput answer)
    {
        if (answer == null) return Media5AnswerState.Empty;

        NumberLineSoalStatus status = answer.Evaluate(out Fraction value);
        switch (status)
        {
            case NumberLineSoalStatus.Empty:
                return Media5AnswerState.Empty;
            case NumberLineSoalStatus.Incomplete:
            case NumberLineSoalStatus.Impossible:
                return Media5AnswerState.Incomplete;
        }

        if (Config.Classify(value, out int tick) != NumberLineReach.OnLine) return Media5AnswerState.Wrong;
        return tick == ResultTick ? Media5AnswerState.Correct : Media5AnswerState.Wrong;
    }

    // Tombol Reset (hard reset): garis default, tanpa soal, tanpa hewan
    public void ResetAll()
    {
        Config = DefaultConfig();
        Problem = null;
        IsSolved = false;
        HasPion = false;
        PionTick = 0;
        StartTick = 0;
        generator.ClearMemory();
        Changed?.Invoke();
    }
}