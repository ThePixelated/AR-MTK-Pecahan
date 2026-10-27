public static class Media3Messages
{
    public const string ReconfigureWarning = "Wadah sudah berisi cairan. Mengubah campuran akan mengosongkan wadah dan mengisi ulang gelas. Ingin melanjutkan?";
    public const string ResetAllWarning = "Semua percobaan akan dihapus, lalu gelas dan wadah diatur ulang. Ingin melanjutkan?";
    public const string AllReset = "Semua percobaan sudah dihapus.";
    public const string NothingToReset = "Belum ada yang perlu diatur ulang.";
    public const string PourBusy = "Tunggu sampai tuangan selesai ya.";
    public const string EnterCompare = "Masuk mode perbandingan.";
    public const string EnterCompareEmpty = "Masuk mode perbandingan. Belum ada percobaan yang bisa dibandingkan.";
    public const string EnterEntry = "Masuk mode entri.";
    public const string InputInCompareMode = "Kembali ke mode entri dulu untuk menambah percobaan.";

    public static string EntryRemoved(int number) => $"Percobaan {number} dihapus.";

    public static string EntryAdded(int number, Media3Entry e, bool nowFull)
    {
        string s = $"Percobaan {number} tercatat: air {e.waterSections} : sirup {e.syrupSections}.";
        return nowFull ? s + " Percobaan sudah penuh, coba lihat perbandingan." : s;
    }

    public static string Duplicate(int number) =>
        $"Campuran ini sama persis dengan percobaan {number}. Coba jumlah yang lain.";

    public static string For(Media3InputResult r, Media3Limits limits) => r switch
    {
        Media3InputResult.NotConfigured => "Atur jumlah sirup dan air dulu ya.",
        Media3InputResult.TrialsFull => $"Percobaan sudah penuh (maksimal {limits.MaxTrials}). Coba lihat perbandingan.",
        Media3InputResult.NothingPoured => "Tuang cairan ke wadah dulu ya.",
        Media3InputResult.NotAllPoured => "Tuang semua cairan ke wadah dulu ya.",
        Media3InputResult.DuplicateEntry => "Campuran ini sudah ada. Coba jumlah yang lain.",
        _ => ""
    };

    public static string For(Media3ConfigError e, Media3Limits limits) => e switch
    {
        Media3ConfigError.SyrupOutOfRange => $"Jumlah sirup harus antara {limits.MinPerCup} dan {limits.MaxPerCup}.",
        Media3ConfigError.WaterOutOfRange => $"Jumlah air harus antara {limits.MinPerCup} dan {limits.MaxPerCup}.",
        _ => ""
    };
}