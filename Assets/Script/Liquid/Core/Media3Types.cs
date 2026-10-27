using System;

// Batas-batas Media 3, bisa diatur dari Inspector lewat controller.
[Serializable]
public class Media3Limits
{
    public int MinPerCup = 1;              // isi minimum tiap gelas (pembilang stepper)
    public int MaxPerCup = 10;             // isi maksimum tiap gelas; juga jadi kapasitas gelas
    public int MaxTrials = 4;              // jumlah entri maksimum
    public bool RequireAllPoured = false;  // true: Input hanya boleh kalau gelas sirup dan air sudah kosong
}

public readonly struct Media3Config
{
    public readonly int syrupSections;     // isi gelas sirup
    public readonly int waterSections;     // isi gelas air

    public Media3Config(int syrupSections, int waterSections)
    {
        this.syrupSections = syrupSections;
        this.waterSections = waterSections;
    }
}

// Satu percobaan: isi wadah saat Input ditekan, dalam bagian
public readonly struct Media3Entry
{
    public readonly int syrupSections;
    public readonly int waterSections;
    // Bagian sirup dari total sebagai float (0 = air bening, 1 = sirup murni); dipakai untuk warna
    public float SyrupRatio => TotalSections == 0 ? 0f : (float)syrupSections / TotalSections;

    public Media3Entry(int syrupSections, int waterSections)
    {
        this.syrupSections = syrupSections;
        this.waterSections = waterSections;
    }

    public int TotalSections => syrupSections + waterSections;

    // Bagian sirup dari total (0 = air bening, 1 = sirup murni); rasio senilai menghasilkan nilai yang sama persis
    public Fraction Concentration =>
        TotalSections == 0 ? Fraction.Zero : new Fraction(syrupSections, TotalSections).Reduce();

    // Angka sama persis (dipakai untuk menolak entri kembar)
    public bool SamePair(Media3Entry other) =>
        syrupSections == other.syrupSections && waterSections == other.waterSections;

    // Rasio senilai (2:1 dan 8:4); warnanya sama
    public bool SameRatio(Media3Entry other) =>
        (long)syrupSections * other.waterSections == (long)waterSections * other.syrupSections;
}

public enum Media3ConfigError { None, SyrupOutOfRange, WaterOutOfRange }

public enum Media3InputResult
{
    Added,
    NotConfigured,
    TrialsFull,       // sudah MaxTrials entri
    NothingPoured,    // wadah masih kosong
    NotAllPoured,     // hanya muncul kalau RequireAllPoured = true
    DuplicateEntry    // angka sama persis dengan entri yang sudah ada
}