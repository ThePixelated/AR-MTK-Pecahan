using System;

// Batas-batas Media 3, bisa diatur dari Inspector lewat controller.
[Serializable]
public class Media3Limits
{
    public int MaxPerCup = 10;     // air dan sirup masing-masing maksimal sekian cangkir per percobaan
    public int MaxTrials = 4;      // jumlah entri maksimum
}

// Satu percobaan: isi wadah saat Input ditekan, dalam cangkir
public readonly struct Media3Entry
{
    public readonly int syrupSections;
    public readonly int waterSections;

    public Media3Entry(int syrupSections, int waterSections)
    {
        this.syrupSections = syrupSections;
        this.waterSections = waterSections;
    }

    public int TotalSections => syrupSections + waterSections;

    // Bagian sirup dari total (0 = air bening, 1 = sirup murni); rasio senilai menghasilkan nilai yang sama persis
    public Fraction Concentration =>
        TotalSections == 0 ? Fraction.Zero : new Fraction(syrupSections, TotalSections).Reduce();

    // Bagian sirup dari total sebagai float; dipakai untuk warna
    public float SyrupRatio => TotalSections == 0 ? 0f : (float)syrupSections / TotalSections;

    // Angka sama persis (dipakai untuk menolak entri kembar)
    public bool SamePair(Media3Entry other) =>
        syrupSections == other.syrupSections && waterSections == other.waterSections;

    // Rasio senilai (2:1 dan 8:4); warnanya sama
    public bool SameRatio(Media3Entry other) =>
        (long)syrupSections * other.waterSections == (long)waterSections * other.syrupSections;
}

public enum Media3InputResult
{
    Added,
    TrialsFull,       // sudah MaxTrials entri
    NothingPoured,    // wadah masih kosong
    DuplicateEntry    // angka sama persis dengan entri yang sudah ada
}