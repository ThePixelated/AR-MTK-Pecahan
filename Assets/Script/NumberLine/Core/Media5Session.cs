using System;

// State mode Media 5: garis, soal, dan posisi pion. Soal hanya dibuat lewat TryNewProblem (tombol "Buat soal").
public sealed class Media5Session
{
    public Media5Limits Limits { get; }
    public NumberLineConfig Config { get; private set; }
    public int PionTick { get; private set; }
    public NumberLineProblem? Problem { get; private set; }
    public bool IsSolved { get; private set; }
    public int StartTick { get; private set; }    // posisi awal pion untuk soal ini (A, atau 0); tujuan tombol Coba lagi

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
    public int ResultTick => Problem.HasValue ? Problem.Value.ResultTick : 0;

    // true = mengubah garis membuang soal yang belum terjawab benar, jadi UI menampilkan popup dulu
    public bool NeedsConfirmBeforeReconfigure => HasProblem && !IsSolved;

    // true = keadaan awal: garis default, belum ada soal, pion di 0 (dipakai menolak Reset yang tidak perlu)
    public bool IsPristine
    {
        get
        {
            NumberLineConfig d = DefaultConfig();
            return !HasProblem && PionTick == 0 && Config.length == d.length && Config.partisi == d.partisi;
        }
    }

    // ---- ukuran garis ----
    public NumberLineConfigError Validate(int length, int partisi)
    {
        if (length < Limits.Line.MinLength || length > Limits.Line.MaxLength) return NumberLineConfigError.LengthOutOfRange;
        if (partisi < Limits.Line.MinPartisi || partisi > Limits.Line.MaxPartisi) return NumberLineConfigError.PartisiOutOfRange;
        return NumberLineConfigError.None;
    }

    // Nilai sama dengan yang berlaku: tidak ada yang berubah.
    // Nilai berbeda: soal dibuang (tidak dibuat ulang otomatis), pion ke 0, memori anti-ulang dikosongkan.
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
    // Tombol "Buat soal": soal baru untuk garis aktif; pion ke A (atau ke 0 kalau StartAtOperand mati)
    public Media5ProblemError TryNewProblem()
    {
        if (Config.partisi < Limits.MinPartisiForProblem) return Media5ProblemError.PartisiTooSmall;

        generator.Window = Limits.AntiRepeatWindow;
        if (!generator.TryGenerate(Config, Limits.Operations, out NumberLineProblem p))
            return Media5ProblemError.NoProblemPossible;

        Problem = p;
        IsSolved = false;
        StartTick = Limits.StartAtOperand ? p.aTick : 0;
        PionTick = StartTick;
        Changed?.Invoke();
        return Media5ProblemError.None;
    }

    // ---- lompat (1 tick per panggilan) ----
    public Media5JumpResult JumpForward() => Jump(+1);
    public Media5JumpResult JumpBackward() => Jump(-1);

    Media5JumpResult Jump(int direction)
    {
        if (!HasProblem) return Media5JumpResult.NoProblem;

        int next = PionTick + direction;
        if (next < 0) return Media5JumpResult.AtStart;
        if (next > Config.MaxTick) return Media5JumpResult.AtEnd;

        PionTick = next;
        Changed?.Invoke();
        return Media5JumpResult.Moved;
    }

    // Tombol "Coba lagi": pion kembali ke posisi awal soal. Status terjawab tidak berubah.
    public Media5RetryResult RetryPosition()
    {
        if (!HasProblem) return Media5RetryResult.NoProblem;
        if (PionTick == StartTick) return Media5RetryResult.AlreadyAtStart;

        PionTick = StartTick;
        Changed?.Invoke();
        return Media5RetryResult.Moved;
    }

    // ---- penilaian ----
    // Hanya posisi akhir pion yang dinilai; jalur yang ditempuh bebas
    public Media5CheckOutcome Check()
    {
        if (!HasProblem) return Media5CheckOutcome.NoProblem;

        bool correct = PionTick == Problem.Value.ResultTick;
        if (correct && !IsSolved)
        {
            IsSolved = true;
            Changed?.Invoke();
        }
        return correct ? Media5CheckOutcome.Correct : Media5CheckOutcome.Wrong;
    }

    // Tombol Reset: garis default, tanpa soal, pion ke 0
    public void ResetAll()
    {
        Config = DefaultConfig();
        Problem = null;
        IsSolved = false;
        PionTick = 0;
        StartTick = 0;
        generator.ClearMemory();
        Changed?.Invoke();
    }
}