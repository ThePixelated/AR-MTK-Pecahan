using System;

// Operasi pecahan tambahan untuk sistem cairan. Fraction bersama (Media 4/6) sengaja tidak diubah.
public static class LiquidMath
{
    public static Fraction Mul(Fraction a, Fraction b)
    {
        checked
        {
            long g1 = Gcd(Math.Abs(a.numerator), b.denominator);   // silang-sederhanakan dulu supaya tidak overflow
            long g2 = Gcd(Math.Abs(b.numerator), a.denominator);
            long num = (a.numerator / g1) * (b.numerator / g2);
            long den = (a.denominator / g2) * (b.denominator / g1);
            return new Fraction(num, den).Reduce();
        }
    }

    public static Fraction Div(Fraction a, Fraction b)
    {
        if (b.numerator == 0) throw new DivideByZeroException("Pembagi pecahan bernilai 0");
        return Mul(a, new Fraction(b.denominator, b.numerator));   // konstruktor menormalkan tanda penyebut
    }

    static long Gcd(long a, long b)
    {
        while (b != 0) { long t = a % b; a = b; b = t; }
        return a;
    }
}