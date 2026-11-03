public static class Media1Texts
{
    public const string AddResetWarning = "Wadah atau gelas akhir sudah terisi. Menambah larutan akan mengatur ulang semuanya. Ingin melanjutkan?";
    public const string PartitionResetWarning = "Mengubah partisi akan mengosongkan semua gelas dan wadah. Ingin melanjutkan?";
    public const string WaterOnlyA = "Air hanya bisa dituang ke gelas A.";
    public const string SyrupOnlyB = "Sirup hanya bisa dituang ke gelas B.";
    public const string DropOnCup = "Lepaskan di gelas A (air) atau gelas B (sirup) ya.";
    public const string NothingToMerge = "Belum ada air atau sirup di gelas A atau B.";
    public const string AddFirst = "Tambahkan air atau sirup dulu ya.";
    public const string PourBusy = "Tunggu sampai tuangan selesai ya.";
    public const string NothingToReset = "Belum ada yang perlu diatur ulang.";

    public static string CupFull(LiquidKind kind, int p) =>
        $"{(kind == LiquidKind.Water ? "Gelas A" : "Gelas B")} sudah penuh ({p}/{p}).";
}