public static class Media5Messages
{
    // ---- umum ----
    public const string NoProblem = "Buat soalnya dulu: tekan Generate Soal, atau isi A dan B lalu tekan Enter.";
    public const string ProblemLocked = "Soal sedang dikerjakan. Tekan Clear Soal kalau mau mengganti soal.";
    public const string AlreadyAtStart = "Hewan sudah di posisi awal.";
    public const string NoProblemPossible = "Garis bilangan terlalu pendek untuk membuat soal. Perpanjang garis atau tambah partisi.";

    // ---- popup konfirmasi ----
    public const string OverwriteWarning = "Soal yang kamu isi akan diganti. Lanjutkan?";
    public const string ReconfigureWarning = "Kalau garis diubah, soal akan dihapus dan hewan kembali ke titik 0. Lanjutkan?";
    public const string ResetWarning = "Garis bilangan, soal, dan hewan akan diatur ulang. Lanjutkan?";

    // ---- reset ----
    public const string ResetDone = "Sudah diatur ulang.";
    public const string NothingToReset = "Belum ada yang perlu diatur ulang.";

    // ---- Cek Hasil ----
    public const string CheckCorrect = "Hebat! Posisi pion dan jawabanmu benar.";
    public const string PositionWrong = "Posisi pion belum tepat. Coba cek lagi lompatannya!";
    public const string AnswerWrong = "Jawabanmu belum tepat. Hitung lagi ya!";
    public const string BothWrong = "Posisi pion dan jawabanmu belum tepat. Ulangi lompatannya, lalu hitung lagi ya!";
    public const string AnswerEmpty = "Isi jawabanmu dulu, lalu cek hasilnya.";
    public const string AnswerIncomplete = "Jawabanmu belum lengkap. Isi pembilang dan penyebut yang benar ya.";

    public static string Overflow(int max) => $"Angkanya terlalu besar. Paling besar {max}.";

    public static string For(Media5CheckOutcome outcome)
    {
        switch (outcome)
        {
            case Media5CheckOutcome.Correct: return CheckCorrect;
            case Media5CheckOutcome.WrongPosition: return PositionWrong;
            case Media5CheckOutcome.WrongAnswer: return AnswerWrong;
            case Media5CheckOutcome.WrongBoth: return BothWrong;
            case Media5CheckOutcome.AnswerEmpty: return AnswerEmpty;
            case Media5CheckOutcome.AnswerIncomplete: return AnswerIncomplete;
            default: return NoProblem;
        }
    }

    public static string For(Media5ProblemError e, Media5Limits limits) => e switch
    {
        Media5ProblemError.PartisiTooSmall => $"Partisi minimal {limits.MinPartisiForProblem} untuk membuat soal.",
        Media5ProblemError.NoProblemPossible => NoProblemPossible,
        _ => ""
    };

    // Enter Soal: pesan menyebut persis bagian yang bermasalah
    public static string For(Media5EnterError e, NumberLineConfig line) => e switch
    {
        Media5EnterError.AMissing => "Isi soal A dulu.",
        Media5EnterError.BMissing => "Isi soal B dulu.",
        Media5EnterError.AIncomplete => "Soal A belum lengkap. Isi pembilang dan penyebutnya.",
        Media5EnterError.BIncomplete => "Soal B belum lengkap. Isi pembilang dan penyebutnya.",
        Media5EnterError.AImpossible => "Penyebut soal A tidak boleh 0.",
        Media5EnterError.BImpossible => "Penyebut soal B tidak boleh 0.",
        Media5EnterError.ANotOnTick => "Soal A tidak jatuh tepat di salah satu tanda garis. Pilih pecahan yang sesuai dengan partisi.",
        Media5EnterError.BNotOnTick => "Soal B tidak jatuh tepat di salah satu tanda garis. Pilih pecahan yang sesuai dengan partisi.",
        Media5EnterError.AExceedsLine => $"Soal A melewati panjang garis. A paling besar {line.length}.",
        Media5EnterError.BExceedsLine => $"Soal B melewati panjang garis. B paling besar {line.length}.",
        Media5EnterError.BZero => "Soal B harus lebih dari 0.",
        Media5EnterError.AMustBeGreater => "Soal A harus lebih besar dari B.",
        _ => ""
    };
}