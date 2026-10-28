public static class NumberLineMessages
{
    public const string ReconfigureWarning = "Pion sudah digeser. Mengubah garis bilangan akan mengembalikan pion ke posisi awal. Ingin melanjutkan?";
    public const string ResetWarning = "Garis bilangan, soal, dan posisi pion akan diatur ulang. Ingin melanjutkan?";
    public const string ResetDone = "Sudah diatur ulang.";
    public const string NothingToReset = "Belum ada yang perlu diatur ulang.";

    public const string CheckCorrect = "Nilai yang Anda masukkan benar.";
    public const string CheckWrong = "Nilai yang Anda masukkan salah.";

    public const string SoalEmpty = "Isi soalnya dulu dengan pecahan.";
    public const string SoalIncomplete = "Lengkapi pembilang dan penyebut dulu.";
    public const string SoalImpossible = "Pecahan tidak mungkin: penyebut tidak boleh 0. Isian dikosongkan.";
    public const string SoalBeyondLine = "Nilai soal lebih besar dari panjang garis bilangan.";

    public static string CellOverflow(int max) => $"Angka maksimal {max}. Isian dikosongkan.";

    public static string For(NumberLineConfigError e, NumberLineLimits limits) => e switch
    {
        NumberLineConfigError.LengthOutOfRange => $"Panjang garis harus antara {limits.MinLength} dan {limits.MaxLength}.",
        NumberLineConfigError.PartisiOutOfRange => $"Partisi harus antara {limits.MinPartisi} dan {limits.MaxPartisi}.",
        _ => ""
    };
}