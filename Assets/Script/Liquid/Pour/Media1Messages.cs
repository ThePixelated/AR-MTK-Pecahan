public static class Media1Messages
{
    public const string NotMovable = "Gelas akhir tidak bisa dipindahkan.";
    public const string DataExists = "Terdeteksi data yang sudah ada. Melanjutkan akan mengatur ulang nilai yang sekarang. Ingin melanjutkan?";
    public const string RepeatWarning = "Mengulang akan menghapus semua kemajuan yang sudah dilakukan. Ingin mengulang?";
    public const string PredictCorrect = "Berhasil! Gelas akhir muncul.";
    public const string PredictWrong = "Belum tepat, coba lagi.";

    public static string For(ConfigError e, Media1Limits l) => e switch
    {
        ConfigError.DenominatorOutOfRange => $"Penyebut harus antara {l.MinDenominator} dan {l.MaxDenominator}.",
        ConfigError.NumeratorOutOfRange => $"Pembilang tiap gelas harus antara {l.MinNumerator} dan penyebut.",
        _ => ""
    };

    public static string For(PredictError e) => e switch
    {
        PredictError.NotConfigured => "Atur penyebut dan pembilang, lalu tekan ENTER dulu ya.",
        PredictError.AlreadySpawned => "Gelas akhir sudah muncul.",
        PredictError.OutOfRange => "Pilih jumlah gelas dalam rentang yang tersedia.",
        _ => ""
    };


    public static string For(PourError e) => e switch
    {
        PourError.NotConfigured => "Atur penyebut dan pembilang, lalu tekan ENTER dulu ya.",
        PourError.SourceCannotGive => "Gelas ini tidak bisa dituang.",
        PourError.TargetCannotReceive => "Gelas ini tidak bisa menerima cairan.",
        PourError.FinalCupOnlyFromWadah => "Gelas akhir hanya bisa diisi dari wadah.",
        PourError.SourceEmpty => "Gelas ini sudah kosong.",
        PourError.TargetFull => "Tempat tujuan sudah penuh.",
        PourError.FinalCupOutOfOrder => "Isi gelas akhir yang pertama sampai penuh dulu ya.",
        _ => ""
    };
}