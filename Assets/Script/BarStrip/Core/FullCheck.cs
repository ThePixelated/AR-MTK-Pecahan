// Hasil tombol Jumlahkan
public enum CombineResult
{
    Berhasil,
    SoalBelumDibentuk,    // tombol + atau - belum ditekan
    PerluKeduaKotak,      // area atas belum berisi balok dari Kotak A dan dari Kotak B
    SudahDijumlahkan
}

// Hasil Cek Hasil dua tahap: balok area bawah dan jawaban di kotak "?" dinilai terpisah
public enum FullCheckOutcome
{
    SoalBelumDibentuk,
    BelumDijumlahkan,    // balok area atas belum digabung (tombol Jumlahkan)
    KeduanyaKosong,      // balok area bawah dan jawaban sama-sama belum diisi
    BalokKosong,         // jawaban terisi, balok area bawah belum
    JawabanKosong,       // balok area bawah ada, jawaban belum diisi
    Benar,               // balok dan jawaban benar
    BalokSalah,          // jawaban benar, balok salah
    JawabanSalah,        // balok benar, jawaban salah
    KeduanyaSalah
}

public readonly struct FullCheckResult
{
    public readonly FullCheckOutcome outcome;
    public readonly CheckVerdict? blockVerdict;     // rincian balok (Kurang, Kelebihan, ...) kalau balok sudah dinilai

    public FullCheckResult(FullCheckOutcome outcome, CheckVerdict? blockVerdict = null)
    {
        this.outcome = outcome;
        this.blockVerdict = blockVerdict;
    }

    // Balok dan jawaban sudah dinilai (hasil bukan penolakan karena belum lengkap)
    public bool Evaluated =>
        outcome == FullCheckOutcome.Benar || outcome == FullCheckOutcome.BalokSalah
        || outcome == FullCheckOutcome.JawabanSalah || outcome == FullCheckOutcome.KeduanyaSalah;
}
