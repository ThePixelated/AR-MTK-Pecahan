using System;

// Batas-batas Media 2 dan 5, bisa diatur dari Inspector lewat controller.
[Serializable]
public class NumberLineLimits
{
    public int MinLength = 1;
    public int MaxLength = 5;         // tombol panjang garis 1-5
    public int MinPartisi = 1;
    public int MaxPartisi = 15;
    public int DefaultLength = 1;
    public int DefaultPartisi = 5;    // juga nilai setelah Reset
    public int MaxCell = 25;          // batas tiap kotak Soal (whole, pembilang, penyebut)
}

// Posisi sebuah nilai terhadap garis bilangan aktif
public enum NumberLineReach
{
    OnLine,            // tepat di salah satu tick
    NotOnPartition,    // di dalam garis tapi jatuh di antara dua tick
    BeyondLine         // lebih besar dari panjang garis
}

// Ukuran garis: panjang (jumlah satuan penuh) dan partisi (bagian per satuan). Tick 0..MaxTick, tiap tick = tick/partisi.
public readonly struct NumberLineConfig
{
    public readonly int length;
    public readonly int partisi;

    public NumberLineConfig(int length, int partisi)
    {
        this.length = length;
        this.partisi = partisi;
    }

    public int MaxTick => length * partisi;
    public int TickCount => MaxTick + 1;

    public bool IsWholeTick(int tick) => tick % partisi == 0;

    // Nilai tick sebagai pecahan dengan penyebut = partisi (tidak disederhanakan)
    public Fraction TickToFraction(int tick) => new Fraction(tick, partisi);

    // 0 = ujung kiri, 1 = ujung kanan
    public float TickToNormalized(int tick) => MaxTick == 0 ? 0f : (float)tick / MaxTick;

    // Tick sebagai bilangan campuran dengan penyebut = partisi: tick 8 pada partisi 5 = 1 3/5
    public void TickToMixed(int tick, out int whole, out int numerator)
    {
        whole = tick / partisi;
        numerator = tick % partisi;
    }

    // Posisi 0..1 sepanjang garis menjadi tick terdekat (di luar rentang dibatasi ke ujung)
    public int NearestTick(float normalized)
    {
        float n = normalized < 0f ? 0f : normalized > 1f ? 1f : normalized;
        int tick = (int)Math.Round(n * MaxTick, MidpointRounding.AwayFromZero);
        return tick < 0 ? 0 : tick > MaxTick ? MaxTick : tick;
    }

    // Apakah nilai bisa ditempati pion: tepat di tick, di antara tick, atau melewati garis. Nilai 2/4 dan 1/2 diperlakukan sama.
    public NumberLineReach Classify(Fraction value, out int tick)
    {
        tick = 0;
        long n = value.numerator, d = value.denominator;
        if (d <= 0 || n < 0) return NumberLineReach.NotOnPartition;    // tidak muncul dari input yang valid

        if (n > (long)length * d) return NumberLineReach.BeyondLine;

        long scaled = checked(n * partisi);
        if (scaled % d != 0) return NumberLineReach.NotOnPartition;

        tick = (int)(scaled / d);
        return NumberLineReach.OnLine;
    }
}

public static class NumberLineLabels
{
    // 0 -> A, 1 -> B, ..., 25 -> Z, 26 -> AA, 27 -> AB
    public static string Letter(int index)
    {
        if (index < 0) return "?";
        string s = "";
        index++;
        while (index > 0)
        {
            index--;
            s = (char)('A' + index % 26) + s;
            index /= 26;
        }
        return s;
    }
}