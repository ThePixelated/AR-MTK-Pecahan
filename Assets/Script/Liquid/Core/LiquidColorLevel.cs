using System;

public static class LiquidColorLevel
{
    // 0 = air bening, 1..levels = makin pekat. Konsentrasi yang sama selalu satu tingkat (2/4 = 1/2).
    // Batas tingkat dibagi rata (levels = 4: (0,1/4] = 1, (1/4,1/2] = 2, (1/2,3/4] = 3, (3/4,1] = 4).
    // Aturan batas ini sengaja terpusat di sini supaya bisa disetel ulang tanpa menyentuh bagian lain.
    public static int Level(Mixture m, int levels)
    {
        if (m.IsEmpty) return 0;

        Fraction c = m.Concentration;
        if (c.numerator <= 0) return 0;

        long level = (c.numerator * levels + c.denominator - 1) / c.denominator;   // ceil(c * levels), tanpa float
        return (int)Math.Min(Math.Max(level, 1L), (long)levels);
    }
}