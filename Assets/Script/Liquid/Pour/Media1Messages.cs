public static class Media1Messages
{
    public const string NotMovable = "Gelas akhir tidak bisa dipindahkan.";

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