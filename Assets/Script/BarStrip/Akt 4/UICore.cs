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
        _ => ""
    };

    public static string For(CheckVerdict v) => v switch
    {
        CheckVerdict.Benar => "Jawaban benar!",
        CheckVerdict.Kurang => "Masih kurang, coba tambah balok.",
        CheckVerdict.Kelebihan => "Kelebihan, coba kurangi balok.",
        CheckVerdict.NilaiPasSatuanBeda => "Nilainya sudah pas! Samakan dulu satuan pecahannya ya.",
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