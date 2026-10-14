using UnityEngine;

public class FractionSelfTest : MonoBehaviour
{
    int failed;

    void Check(bool ok, string label)
    {
        if (ok) Debug.Log("ok: " + label);
        else { failed++; Debug.LogError("FAIL: " + label); }
    }

    [ContextMenu("Run Fraction Tests")]
    void Run()
    {
        failed = 0;

        Check(new Fraction(4, 2).ValueEquals(new Fraction(8, 4)) && new Fraction(8, 4).ValueEquals(Fraction.Whole(2)), "4/2 == 8/4 == 2/1");

        Fraction third = Fraction.Unit(3);
        Check((third + third + third).ValueEquals(Fraction.Whole(1)), "1/3 x3 == 1");
        Check((new Fraction(7, 8) - third).ValueEquals(new Fraction(13, 24)), "7/8 - 1/3 == 13/24");

        // Kotak acak dari diskusi
        var ka = new KotakContents();
        foreach (var p in new[] { new Fraction(5, 17), new Fraction(2, 5), Fraction.Whole(1), new Fraction(16, 25),
                                  Fraction.Whole(1), Fraction.Whole(1), new Fraction(22, 25), new Fraction(5, 17) }) ka.Add(p);
        var kb = new KotakContents();
        foreach (var p in new[] { new Fraction(6, 13), Fraction.Whole(1), Fraction.Whole(1), new Fraction(7, 7), new Fraction(11, 16) }) kb.Add(p);

        Check(ka.Total().ToMixedString() == "5 216/425", "A = 5 216/425, dapat " + ka.Total().ToMixedString());
        Check(kb.Total().ToMixedString() == "4 31/208", "B = 4 31/208, dapat " + kb.Total().ToMixedString());
        Check((ka.Total() + kb.Total()).ToMixedString() == "9 58103/88400", "A+B = 9 58103/88400");

        // Contoh 4 2/5 + 3 6/4
        var kc = new KotakContents(); for (int i = 0; i < 4; i++) kc.Add(Fraction.Whole(1)); kc.Add(new Fraction(2, 5));
        var kd = new KotakContents(); for (int i = 0; i < 3; i++) kd.Add(Fraction.Whole(1)); kd.Add(new Fraction(6, 4));
        Check((kc.Total() + kd.Total()).ToMixedString() == "8 9/10", "4 2/5 + 3 6/4 = 8 9/10");

        // Merge
        var km = new KotakContents(); km.Add(new Fraction(3, 4));
        Check(km.CanMerge(0, Fraction.Unit(4)), "3/4 bisa merge 1/4");
        km.MergeAt(0, Fraction.Unit(4));
        Check(km.Pieces[0].numerator == 4 && km.Pieces[0].denominator == 4, "hasil merge 4/4, tidak di-reduce");
        Check(!km.CanMerge(0, Fraction.Unit(7)), "4/4 tidak bisa merge 1/7");
        Check(!km.CanMerge(0, Fraction.Unit(4)), "4/4 sudah penuh, tidak bisa merge lagi");
        Check(!km.CanMerge(5, Fraction.Unit(4)), "index di luar jangkauan -> false");

        // DecrementAt: -1 satuan, habis = blok hilang
        var kd2 = new KotakContents();
        kd2.Add(new Fraction(3, 4)); kd2.Add(Fraction.Unit(5));
        kd2.DecrementAt(0);
        Check(kd2.Count == 2 && kd2.Pieces[0].numerator == 2 && kd2.Pieces[0].denominator == 4, "3/4 -1 satuan = 2/4 (tidak di-reduce)");
        kd2.DecrementAt(1);
        Check(kd2.Count == 1 && kd2.Pieces[0].denominator == 4, "1/5 -1 satuan = blok hilang");

        // TransferUnits: hanya yang muat; sumber habis = blok hilang
        var ktr = new KotakContents();
        ktr.Add(new Fraction(3, 5)); ktr.Add(new Fraction(4, 5));
        Check(ktr.CountTransferable(0, 1) == 1, "3/5 -> 4/5: hanya 1 satuan yang muat");
        Check(ktr.TransferUnits(0, 1) == 1 && ktr.Count == 2 && ktr.Pieces[0].numerator == 2 && ktr.Pieces[1].numerator == 5,
              "[3/5, 4/5] -> [2/5, 5/5]");
        Check(ktr.TransferUnits(0, 1) == 0, "target penuh: tidak ada yang pindah");

        var ktr2 = new KotakContents();
        ktr2.Add(new Fraction(2, 5)); ktr2.Add(new Fraction(1, 3)); ktr2.Add(new Fraction(1, 5));
        Check(ktr2.TransferUnits(0, 2) == 2 && ktr2.Count == 2 && ktr2.Pieces[0].denominator == 3
              && ktr2.Pieces[1].numerator == 3 && ktr2.Pieces[1].denominator == 5,
              "[2/5, 1/3, 1/5] -> [1/3, 3/5]");
        Check(ktr2.CountTransferable(0, 1) == 0 && ktr2.CountTransferable(1, 1) == 0, "penyebut beda atau blok sama: 0");

        // Stress overflow: LCM(1..25) > int.MaxValue
        Fraction h = Fraction.Zero;
        for (int den = 2; den <= 25; den++) h += Fraction.Unit(den);
        Check(26771144400L % h.denominator == 0 && h.WholePart == 2, "jumlah 1/2..1/25 gak overflow");

        // Pecah jadi bagian utuh + sisa (dulu lewat ProblemTarget, sekarang langsung dari Fraction)
        var t = new Fraction(9, 4);
        Check(t.WholePart == 2 && t.RemainderNumerator == 1, "9/4 -> 2 dan sisa 1/4");

        Debug.Log(failed == 0 ? "SEMUA LOLOS" : failed + " tes GAGAL");
    }
}