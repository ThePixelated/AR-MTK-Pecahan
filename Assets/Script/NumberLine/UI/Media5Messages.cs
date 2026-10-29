public static class Media5Messages
{
    public const string NoProblem = "Tekan Buat soal dulu.";
    public const string ReconfigureWarning = "Soal yang sedang dikerjakan akan dibuang dan pion kembali ke posisi awal. Ingin melanjutkan?";
    public const string ResetWarning = "Garis bilangan dan soal akan diatur ulang. Ingin melanjutkan?";
    public const string ResetDone = "Sudah diatur ulang.";
    public const string NothingToReset = "Belum ada yang perlu diatur ulang.";
    public const string AlreadyAtStart = "Pion sudah di posisi awal.";
    public const string CheckCorrect = "Posisi pion sudah benar.";
    public const string CheckWrong = "Posisi pion belum tepat. Coba lagi.";
    public const string NoProblemPossible = "Garis bilangan terlalu pendek untuk membuat soal. Perpanjang garis atau tambah partisi.";

    public static string For(Media5ProblemError e, Media5Limits limits) => e switch
    {
        Media5ProblemError.PartisiTooSmall => $"Partisi minimal {limits.MinPartisiForProblem} untuk membuat soal.",
        Media5ProblemError.NoProblemPossible => NoProblemPossible,
        _ => ""
    };
}