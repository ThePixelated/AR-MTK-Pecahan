public static class LatihanMessages
{
    public const string Correct = "Jawaban benar!";
    public const string WrongStay = "Belum tepat. Coba lagi ya.";
    public const string WrongContinue = "Belum tepat. Lanjut ke soal berikutnya.";
    public const string IncompleteMulti = "Isi semua jawaban dulu ya.";
    public const string IncompleteSingle = "Isi jawabannya dulu ya.";
    public const string NotAvailable = "Level ini belum tersedia.";
    public const string NoQuestions = "Level ini belum punya soal.";
    public const string ReconfigureWarning = "Pion sudah digeser. Mengubah garis akan mengembalikan pion ke posisi awal. Ingin melanjutkan?";
    public const string ExitWarning = "Keluar dari level ini akan menghapus semua yang sudah dikerjakan. Yakin keluar?";
    public const string LevelDoneTitle = "Level selesai";
    public const string LevelDoneButton = "OKE";
    public const string FailTitle = "Level gagal";
    public const string FailButton = "ULANGI";
    public const string StrikeFailed = "Kesempatan salah sudah habis. Ayo ulangi level ini.";
    public const string TimeFailed = "Waktu habis. Ayo ulangi level ini.";

    // level: indeks 0-based yang ditekan; yang harus diselesaikan dulu = level sebelumnya (nomor 1-based = indeks level yang ditekan)
    public static string LevelLocked(int level) => $"Selesaikan Level {level} dulu ya.";
    public static string LevelDone(string title) => $"Hebat! {title} sudah selesai.";

    public static string LineOutOfRange(NumberLineLimits l) =>
        $"Panjang garis {l.MinLength}-{l.MaxLength} dan partisi {l.MinPartisi}-{l.MaxPartisi}.";
}