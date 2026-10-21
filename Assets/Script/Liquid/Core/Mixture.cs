// Isi sebuah wadah: berapa bagian sirup dan berapa bagian air (1 bagian = 1/p dari satu gelas utuh).
// Eksak (Fraction), supaya perbandingan yang senilai selalu menghasilkan nilai dan warna yang persis sama.
public readonly struct Mixture
{
    public readonly Fraction syrup;
    public readonly Fraction water;

    public Mixture(Fraction syrup, Fraction water) { this.syrup = syrup; this.water = water; }

    public static Mixture Empty => new Mixture(Fraction.Zero, Fraction.Zero);
    public static Mixture Syrup(long sections) => new Mixture(Fraction.Whole(sections), Fraction.Zero);
    public static Mixture Water(long sections) => new Mixture(Fraction.Zero, Fraction.Whole(sections));

    public Fraction Total => syrup + water;
    public bool IsEmpty => syrup.numerator == 0 && water.numerator == 0;

    // Bagian sirup di dalam campuran: 0 = air bening, 1 = sirup murni
    public Fraction Concentration => IsEmpty ? Fraction.Zero : LiquidMath.Div(syrup, Total);

    public static Mixture operator +(Mixture a, Mixture b) => new Mixture(a.syrup + b.syrup, a.water + b.water);
    public static Mixture operator -(Mixture a, Mixture b) => new Mixture(a.syrup - b.syrup, a.water - b.water);

    // Ambil 'sections' bagian volume dari campuran ini menurut rasio isinya (campuran dianggap homogen)
    public Mixture Take(long sections)
    {
        if (IsEmpty || sections <= 0) return Empty;
        Fraction share = LiquidMath.Div(Fraction.Whole(sections), Total);
        return new Mixture(LiquidMath.Mul(syrup, share), LiquidMath.Mul(water, share));
    }

    public bool ValueEquals(Mixture other) => syrup.ValueEquals(other.syrup) && water.ValueEquals(other.water);
}