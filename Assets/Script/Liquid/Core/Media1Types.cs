using System;

// Batas Media 1. Bisa diatur dari editor nanti (kelas ini [Serializable]).
[Serializable]
public class Media1Limits
{
    public int MinDenominator = 2;
    public int MaxDenominator = 10;     // usulan awal
    public int MinNumerator = 1;        // pembilang tiap gelas: MinNumerator sampai penyebut
    public int MaxPredictCups = 5;      // rentang stepper prediksi (1..N); jawaban benar hanya 1 atau 2 karena tiap gelas akhir maksimal 1 utuh
    public int DilutionLevels = 4;      // tingkat warna encer
}

public readonly struct Media1Config
{
    public readonly int denominator;      // "Partial per Satuan" (p)
    public readonly int waterSections;    // pembilang gelas A (air bening)
    public readonly int syrupSections;    // pembilang gelas B (sirup kuning)

    public Media1Config(int denominator, int waterSections, int syrupSections)
    {
        this.denominator = denominator;
        this.waterSections = waterSections;
        this.syrupSections = syrupSections;
    }

    public int TotalSections => waterSections + syrupSections;
}

public enum ConfigError { None, DenominatorOutOfRange, NumeratorOutOfRange }

public enum PourError
{
    None,
    NotConfigured,
    SameContainer,
    SourceCannotGive,
    TargetCannotReceive,
    FinalCupOnlyFromWadah,   // gelas akhir hanya boleh diisi dari wadah W
    SourceEmpty,
    TargetFull,
    FinalCupOutOfOrder       // gelas akhir harus berurutan: gelas berikutnya baru boleh kalau sebelumnya penuh
}

public enum PredictError { None, NotConfigured, OutOfRange, AlreadySpawned }

public readonly struct PredictResult
{
    public readonly PredictError error;
    public readonly bool correct;

    public PredictResult(PredictError error, bool correct) { this.error = error; this.correct = correct; }
}