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
    public static event System.Action<string, FeedbackKind> Raised;

    public static void Show(string message, FeedbackKind kind = FeedbackKind.Info)
    {
        if (Raised == null) Debug.Log($"[Feedback/{kind}] {message}");
        Raised?.Invoke(message, kind);
    }
}

public static class Messages
{
    public static string For(RejectReason r) => r switch
    {
        RejectReason.SoalBelumDibentuk => "Tekan tombol + atau − dulu ya.",
        RejectReason.KotakTerlaluBesar => "Kotak ini sudah penuh, kurangi dulu.",
        RejectReason.HasilTerlaluBesar => "Hasilnya terlalu besar, coba pecahan yang lebih kecil.",
        RejectReason.SatuanBeda => "Pakai pecahan satuan yang sama ya.",
        RejectReason.DpzPenuh => "Area susun sudah penuh.",
        _ => ""
    };

    public static string For(ProblemError e) => e switch
    {
        ProblemError.KotakKosong => "Isi Kotak A dan Kotak B dulu ya.",
        ProblemError.HasilTidakPositif => "Pecahan A harus lebih besar dari B.",
        ProblemError.HasilTerlaluBesar => "Hasilnya terlalu besar, kurangi pecahannya.",
        ProblemError.PenyebutTerlaluRumit => "Kombinasi ini terlalu rumit, coba penyebut yang lebih sederhana.",
        _ => ""
    };

    public static string For(CheckVerdict v) => v switch
    {
        CheckVerdict.Benar => "Jawaban benar!",
        CheckVerdict.Kurang => "Masih kurang, coba tambah balok.",
        CheckVerdict.Kelebihan => "Kelebihan, coba kurangi balok.",
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