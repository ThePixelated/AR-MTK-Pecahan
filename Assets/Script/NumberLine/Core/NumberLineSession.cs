using System;

public enum NumberLineConfigError { None, LengthOutOfRange, PartisiOutOfRange }

public enum NumberLineCheckOutcome
{
    Correct,
    Wrong,
    SoalEmpty,
    SoalIncomplete,
    SoalImpossible
}

// Hasil menekan Enter di numpad: status Soal dan, kalau valid, di mana nilainya jatuh pada garis aktif
public readonly struct NumberLineCommit
{
    public readonly NumberLineSoalStatus status;
    public readonly NumberLineReach reach;    // hanya bermakna kalau status == Valid
    public readonly int tick;                 // hanya bermakna kalau reach == OnLine

    public NumberLineCommit(NumberLineSoalStatus status, NumberLineReach reach, int tick)
    {
        this.status = status;
        this.reach = reach;
        this.tick = tick;
    }
}

public readonly struct NumberLineCheckResult
{
    public readonly NumberLineCheckOutcome outcome;
    public readonly NumberLineReach reach;    // hanya bermakna kalau outcome Correct atau Wrong

    public NumberLineCheckResult(NumberLineCheckOutcome outcome, NumberLineReach reach)
    {
        this.outcome = outcome;
        this.reach = reach;
    }
}

// State mode Eksplorasi Media 2: ukuran garis, posisi pion, dan Soal yang diketik siswa
public sealed class NumberLineSession
{
    public NumberLineLimits Limits { get; }
    public NumberLineConfig Config { get; private set; }
    public int PionTick { get; private set; }
    public NumberLineSoalInput Soal { get; }

    public event Action Changed;

    public NumberLineSession(NumberLineLimits limits = null)
    {
        Limits = limits ?? new NumberLineLimits();
        Config = DefaultConfig();
        Soal = new NumberLineSoalInput(Limits.MaxCell);
        Soal.Changed += () => Changed?.Invoke();
    }

    // true = keadaan awal: garis default, pion di 0, Soal kosong (dipakai menolak Reset yang tidak perlu)
    public bool IsPristine
    {
        get
        {
            NumberLineConfig d = DefaultConfig();
            return PionTick == 0 && Soal.IsEmpty && Config.length == d.length && Config.partisi == d.partisi;
        }
    }

    NumberLineConfig DefaultConfig() => new NumberLineConfig(
        Math.Clamp(Limits.DefaultLength, Limits.MinLength, Limits.MaxLength),
        Math.Clamp(Limits.DefaultPartisi, Limits.MinPartisi, Limits.MaxPartisi));

    public Fraction PionValue => Config.TickToFraction(PionTick);

    // true = mengubah garis akan memindahkan pion ke 0, jadi UI menampilkan popup dulu
    public bool NeedsConfirmBeforeReconfigure => PionTick != 0;

    // ---- ukuran garis ----
    public NumberLineConfigError Validate(int length, int partisi)
    {
        if (length < Limits.MinLength || length > Limits.MaxLength) return NumberLineConfigError.LengthOutOfRange;
        if (partisi < Limits.MinPartisi || partisi > Limits.MaxPartisi) return NumberLineConfigError.PartisiOutOfRange;
        return NumberLineConfigError.None;
    }

    // Nilai sama dengan yang berlaku: tidak ada yang berubah. Nilai berbeda: pion kembali ke 0 karena tick lama tidak berlaku lagi.
    public NumberLineConfigError ApplyConfig(int length, int partisi)
    {
        NumberLineConfigError err = Validate(length, partisi);
        if (err != NumberLineConfigError.None) return err;
        if (length == Config.length && partisi == Config.partisi) return NumberLineConfigError.None;

        Config = new NumberLineConfig(length, partisi);
        PionTick = 0;
        Changed?.Invoke();
        return NumberLineConfigError.None;
    }

    // ---- pion ----
    // Dipakai drag (tick hasil pembulatan); mengembalikan tick yang berlaku setelah dibatasi ke 0..MaxTick
    public int SetPionTick(int tick)
    {
        int clamped = tick < 0 ? 0 : tick > Config.MaxTick ? Config.MaxTick : tick;
        if (clamped == PionTick) return clamped;
        PionTick = clamped;
        Changed?.Invoke();
        return clamped;
    }

    // ---- Soal dan penilaian ----
    // Enter di numpad. Penyebut 0: Soal dikosongkan dan UI menampilkan notifikasi.
    public NumberLineCommit CommitSoal()
    {
        NumberLineSoalStatus status = Soal.Evaluate(out Fraction value);

        if (status == NumberLineSoalStatus.Impossible)
        {
            Soal.Clear();
            return new NumberLineCommit(status, NumberLineReach.OnLine, 0);
        }
        if (status != NumberLineSoalStatus.Valid)
            return new NumberLineCommit(status, NumberLineReach.OnLine, 0);

        NumberLineReach reach = Config.Classify(value, out int tick);
        return new NumberLineCommit(status, reach, tick);
    }

    // Tombol CEK: benar kalau pion ada di tick yang sama dengan nilai Soal (nilai, bukan pasangan angka: 2/4 = 1/2)
    public NumberLineCheckResult Check()
    {
        NumberLineCommit c = CommitSoal();

        switch (c.status)
        {
            case NumberLineSoalStatus.Empty:
                return new NumberLineCheckResult(NumberLineCheckOutcome.SoalEmpty, NumberLineReach.OnLine);
            case NumberLineSoalStatus.Incomplete:
                return new NumberLineCheckResult(NumberLineCheckOutcome.SoalIncomplete, NumberLineReach.OnLine);
            case NumberLineSoalStatus.Impossible:
                return new NumberLineCheckResult(NumberLineCheckOutcome.SoalImpossible, NumberLineReach.OnLine);
        }

        bool correct = c.reach == NumberLineReach.OnLine && c.tick == PionTick;
        return new NumberLineCheckResult(correct ? NumberLineCheckOutcome.Correct : NumberLineCheckOutcome.Wrong, c.reach);
    }

    // Tombol Reset: panjang dan partisi default, Soal kosong, pion ke 0
    public void ResetAll()
    {
        Config = DefaultConfig();
        PionTick = 0;
        Soal.Clear();
        Changed?.Invoke();
    }
}