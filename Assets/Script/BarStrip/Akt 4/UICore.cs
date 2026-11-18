using UnityEngine;

public static class FractionFormat
{
    public static string Label(Fraction f) =>
        f.denominator == 1 ? f.numerator.ToString() : $"{f.numerator}/{f.denominator}";
}

public enum FeedbackKind { Info, Success, Warning, Error }

// NotificationBar di scene belum punya TMP/icon, jadi sementara cuma Debug.Log. Step 4 menyambungkannya.
public static class Feedback
{
    // di dalam class Feedback (supaya subscriber tidak nyangkut antar sesi Play)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Raised = null;
    public static event System.Action<string, FeedbackKind> Raised;

    public static void Show(string message, FeedbackKind kind = FeedbackKind.Info)
    {
        if (Raised == null) Debug.Log($"[Feedback/{kind}] {message}");
        Raised?.Invoke(message, kind);
    }
}

public static class Messages
{

    // di dalam class Messages
    public const string SoalSudahDibentuk = "Soal sudah dibentuk. Ubah kotak kalau mau soal baru.";
    public const string KurangPeringatanJudul = "Perhatian";
    public const string KurangPeringatanIsi = "Kotak A harus lebih besar dari Kotak B supaya hasil pengurangannya lebih dari 0. Ubah isi salah satu kotak dulu ya.";

    public static string For(ProblemError e) => e switch
    {
        ProblemError.KotakKosong => "Isi Kotak A dan Kotak B dulu ya.",
        ProblemError.HasilTidakPositif => "Pecahan A harus lebih besar dari B.",
        //ProblemError.HasilTidakPositif => "Kotak A harus lebih besar dari Kotak B.",
        ProblemError.HasilTerlaluBesar => "Hasilnya terlalu besar, kurangi pecahannya.",
        ProblemError.PenyebutTerlaluRumit => "Kombinasi ini terlalu rumit, coba penyebut yang lebih sederhana.",
        _ => ""
    };

    public static string For(RejectReason r) => r switch
    {
        RejectReason.SoalBelumDibentuk => "Tekan tombol + atau − dulu ya.",
        RejectReason.KotakTerlaluBesar => "Kotak ini sudah penuh, kurangi dulu.",
        RejectReason.HasilTerlaluBesar => "Hasilnya terlalu besar, coba pecahan yang lebih kecil.",
        RejectReason.DpzPenuh => "Area susun sudah penuh.",
        RejectReason.KotakBulatPenuh => "Bilangan bulat maksimal 4.",
        RejectReason.KotakPecahanBeda => "Kotak ini hanya boleh satu jenis pecahan.",
        RejectReason.KotakPecahanPenuh => "Baris pecahan di kotak ini sudah penuh.",
        RejectReason.KotakTerkunci => "Kembalikan dulu balok dari area ke kotak ini.",
        RejectReason.LowerHanyaPecahan => "Balok sisa harus pecahan, bukan bilangan bulat.",
        RejectReason.LowerSatuanBeda => "Balok sisa harus satu jenis pecahan.",
        RejectReason.BlokSudahDiArea => "Balok ini sudah ada di area.",
        RejectReason.AreaTerkunci => "Sudah dijumlahkan. Atur ulang dulu kalau mau mengubah.",
        _ => ""
    };

    public static string For(CombineResult r) => r switch
    {
        CombineResult.SoalBelumDibentuk => "Tekan tombol + atau − dulu ya.",
        CombineResult.PerluKeduaKotak => "Tarik pecahan dari Kotak A dan Kotak B ke area atas dulu.",
        CombineResult.SudahDijumlahkan => "Balok sudah dijumlahkan.",
        _ => ""
    };

    public static string Word(Operation op) => op == Operation.Add ? "penjumlahan" : "pengurangan";

    // Hasil Cek Hasil dua tahap. answerText = jawaban siswa di kotak "?" (mis. "1 3/4"); dipakai di pesan jawaban salah.
    public static string For(FullCheckResult r, Operation op, string answerText)
    {
        switch (r.outcome)
        {
            case FullCheckOutcome.SoalBelumDibentuk: return "Tekan tombol + atau − dulu ya.";
            case FullCheckOutcome.BelumDijumlahkan: return "Tekan Jumlahkan dulu untuk menggabungkan balok di area atas.";
            case FullCheckOutcome.KeduanyaKosong: return "Susun balok di area bawah dan jawab soal terlebih dahulu.";
            case FullCheckOutcome.BalokKosong: return "Susun balok pecahan di area bawah terlebih dahulu.";
            case FullCheckOutcome.JawabanKosong: return "Jawab soal di kotak jawaban terlebih dahulu.";
            case FullCheckOutcome.Benar: return "Benar! Balok dan jawabanmu tepat.";
            case FullCheckOutcome.BalokSalah:
                string detail = r.blockVerdict == CheckVerdict.Kurang ? " Masih kurang, coba tambah balok."
                              : r.blockVerdict == CheckVerdict.Kelebihan ? " Kelebihan, coba kurangi balok." : "";
                return "Jawabanmu benar, tapi balok pecahan yang kamu susun belum tepat." + detail;
            case FullCheckOutcome.JawabanSalah:
                return $"Balok pecahanmu sudah tepat, tapi hasil {Word(op)} yang kamu masukkan ({answerText}) salah.";
            case FullCheckOutcome.KeduanyaSalah:
                return $"Balok pecahan dan hasil {Word(op)} yang kamu masukkan ({answerText}) belum tepat. Coba cek lagi susunan balok dan hitungannya.";
            default: return "";
        }
    }

    public static FeedbackKind KindOf(FullCheckOutcome o) =>
        o == FullCheckOutcome.Benar ? FeedbackKind.Success
        : o == FullCheckOutcome.BalokSalah || o == FullCheckOutcome.JawabanSalah || o == FullCheckOutcome.KeduanyaSalah ? FeedbackKind.Warning
        : FeedbackKind.Info;

    public const string AnswerIncomplete = "Lengkapi pecahan jawabanmu dulu: pembilang dan penyebut harus terisi.";

    public static string For(CheckVerdict v) => v switch
    {
        CheckVerdict.Benar => "Jawaban benar!",
        CheckVerdict.Kurang => "Masih kurang, coba tambah balok.",
        CheckVerdict.Kelebihan => "Kelebihan, coba kurangi balok.",
        CheckVerdict.NilaiPasSatuanBeda => "Nilainya sudah pas! Samakan dulu satuan pecahannya ya.",
        CheckVerdict.UtuhBelumSesuai => "Sisanya sudah pas! Cek lagi bilangan bulatnya.",
        _ => ""
    };
}

// Satu row Kotak yang lagi "dipilih" (tombol hapus kelihatan)
public static class RowSelection
{
    public static FractionBarRow Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;

    public static void Select(FractionBarRow row)
    {
        if (Current == row) return;   // spek: tombol hapus hanya ditutup oleh tap di luar row atau oleh aksi hapus
        Clear();
        Current = row;
        row.ShowDelete(true);
    }

    public static void Clear()
    {
        if (Current) Current.ShowDelete(false);
        Current = null;
    }
}