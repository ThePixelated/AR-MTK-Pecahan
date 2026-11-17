public static class Media3Messages
{
    public const string ResetAllWarning = "Semua percobaan akan dihapus, dan isi wadah dikosongkan. Ingin melanjutkan?";
    public const string AllReset = "Semua percobaan sudah dihapus.";
    public const string NothingToReset = "Belum ada yang perlu diatur ulang.";
    public const string PourBusy = "Tunggu sampai tuangan selesai ya.";

    public const string EnterCompare = "Masuk mode perbandingan.";
    public const string EnterCompareEmpty = "Masuk mode perbandingan. Belum ada percobaan yang bisa dibandingkan.";
    public const string EnterEntry = "Masuk mode entri.";
    public const string InputInCompareMode = "Kembali ke mode entri dulu untuk menambah percobaan.";

    public const string MergeNeedsFull = "Isi kedua gelas dulu ya, baru digabungkan.";

    static string Cup(LiquidKind kind) => kind == LiquidKind.Water ? "air" : "sirup";

    public static string CupEmpty(LiquidKind kind) => $"Gelas {Cup(kind)} sudah kosong. Tekan Isi dulu ya.";
    public static string CupStillFull(LiquidKind kind) => $"Gelas {Cup(kind)} masih penuh.";
    public static string WadahFull(Media3Limits limits) => $"Wadah sudah penuh ({limits.MaxTotalPours} tuangan). Catat dengan Input atau atur ulang.";
    // Dipertahankan supaya Media3Controller lama tetap terkompilasi sampai tahap berikutnya menggantinya dengan ForMerge
    public static string MergeBlocked(Media3Limits limits) => MergeWadahFull(limits);

    public static string MergeWadahFull(Media3Limits limits) => $"Tidak muat dua tuangan lagi (maksimal {limits.MaxTotalPours}). Tuang satu per satu atau catat dulu.";

    public static string EntryAdded(int number, Media3Entry e, bool nowFull)
    {
        string s = $"Percobaan {number} tercatat: air {e.waterSections} : sirup {e.syrupSections}.";
        return nowFull ? s + " Percobaan sudah penuh, coba lihat perbandingan." : s;
    }

    public static string Duplicate(int number) =>
        $"Campuran ini sama persis dengan percobaan {number}. Coba jumlah yang lain.";

    public static string EntryRemoved(int number) => $"Percobaan {number} dihapus.";

    // Pesan untuk tuangan yang ditolak (swipe gelas ke wadah)
    public static string ForPour(PourError err, LiquidKind kind, Media3Limits limits)
    {
        switch (err)
        {
            case PourError.SourceEmpty: return CupEmpty(kind);
            case PourError.TargetFull: return WadahFull(limits);
            default: return "";
        }
    }

    public static string ForMerge(PourError err, Media3Limits limits)
    {
        switch (err)
        {
            case PourError.SourceEmpty: return MergeNeedsFull;
            case PourError.TargetFull: return MergeWadahFull(limits);
            default: return "";
        }
    }

    public static string For(Media3InputResult r, Media3Limits limits)
    {
        switch (r)
        {
            case Media3InputResult.TrialsFull: return $"Percobaan sudah penuh (maksimal {limits.MaxTrials}). Coba lihat perbandingan.";
            case Media3InputResult.NothingPoured: return "Tuang air atau sirup ke wadah dulu ya.";
            case Media3InputResult.DuplicateEntry: return "Campuran ini sudah ada. Coba jumlah yang lain.";
            default: return "";
        }
    }
}