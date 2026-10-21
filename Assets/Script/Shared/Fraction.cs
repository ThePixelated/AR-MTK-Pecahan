using System;

[Serializable]
public struct Fraction : IComparable<Fraction>
{
    public long numerator;
    public long denominator;   // invarian: > 0

    public Fraction(long numerator, long denominator)
    {
        if (denominator == 0) throw new ArgumentException("Penyebut tidak boleh 0");
        if (denominator < 0) { numerator = -numerator; denominator = -denominator; }
        this.numerator = numerator;
        this.denominator = denominator;
    }

    public static readonly Fraction Zero = new Fraction(0, 1);
    public static Fraction Whole(long n) => new Fraction(n, 1);
    public static Fraction Unit(long den) => new Fraction(1, den);   // 1/den, dipakai bank pecahan

    public int Sign => numerator > 0 ? 1 : numerator < 0 ? -1 : 0;
    public long WholePart => numerator / denominator;   // khusus nilai >= 0
    public long RemainderNumerator => numerator % denominator;   // khusus nilai >= 0
    public float ToFloat() => (float)numerator / denominator;   // visual saja, bukan buat logika benar/salah

    public static Fraction operator +(Fraction a, Fraction b)
    {
        checked   // overflow jadi exception, bukan angka salah diam-diam
        {
            long den = Lcm(a.denominator, b.denominator);
            long num = a.numerator * (den / a.denominator) + b.numerator * (den / b.denominator);
            return new Fraction(num, den).Reduce();
        }
    }
    public static Fraction operator -(Fraction a, Fraction b) => a + new Fraction(-b.numerator, b.denominator);

    public Fraction Reduce()
    {
        long g = Gcd(Math.Abs(numerator), denominator);
        return g <= 1 ? this : new Fraction(numerator / g, denominator / g);
    }

    // Nilai sama, representasi boleh beda: 4/2, 8/4, 2/1 -> true
    public bool ValueEquals(Fraction other)
    {
        Fraction x = Reduce(), y = other.Reduce();
        return x.numerator == y.numerator && x.denominator == y.denominator;
    }

    // Banding lewat pengurangan, bukan cross-multiply, supaya gak overflow
    public int CompareTo(Fraction other) => (this - other).Sign;

    static long Gcd(long a, long b) { while (b != 0) { long t = a % b; a = b; b = t; } return a; }
    static long Lcm(long a, long b) => a / Gcd(a, b) * b;

    public override string ToString() => $"{numerator}/{denominator}";

    public string ToMixedString()   // khusus nilai >= 0, buat panel Soal
    {
        long whole = WholePart, rem = RemainderNumerator;
        if (rem == 0) return whole.ToString();
        if (whole == 0) return $"{rem}/{denominator}";
        return $"{whole} {rem}/{denominator}";
    }
}