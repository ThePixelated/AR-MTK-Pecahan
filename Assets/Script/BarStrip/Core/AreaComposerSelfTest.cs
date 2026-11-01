using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AreaComposerSelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run AreaComposer Tests")]
    public void Run()
    {
        pass = fail = 0;
        TestAdd();
        TestSubtract();
        TestSession();
        string summary = $"AreaComposer tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static Fraction U(long d) => Fraction.Unit(d);
    static AreaBlock A(Fraction p) => new AreaBlock(p, true, 0);
    static AreaBlock B(Fraction p, bool sub = false) => new AreaBlock(p, false, 0, sub);
    static string Dens(ComposedBlock cb, IReadOnlyList<AreaBlock> blocks) =>
        string.Join(",", cb.cellSource.Select(i => blocks[i].piece.denominator));
    static string Src(ComposedBlock cb) => string.Join(",", cb.sources);

    void TestAdd()
    {
        // contoh klien: A = [1, 1/2], B = [3/4] -> [carry 4/4, 1, 1/4]
        var l1 = new List<AreaBlock> { A(Fraction.Whole(1)), A(U(2)), B(new Fraction(3, 4)) };
        var c = AreaComposer.Compose(Operation.Add, l1);
        Check(c.Count == 3 && c[0].kind == ComposedKind.Carry && c[1].kind == ComposedKind.Whole && c[2].kind == ComposedKind.Fraction,
              "urutan carry, bilangan bulat, pecahan");
        Check(c[0].piece.numerator == 4 && c[0].piece.denominator == 4 && Dens(c[0], l1) == "2,2,4,4", "carry 4/4: 2 sel warna 1/2 dan 2 sel warna 3/4");
        Check(Src(c[0]) == "1,2", "carry berasal dari 1/2 dan 3/4");
        Check(c[1].piece.numerator == 1 && c[1].piece.denominator == 1 && Src(c[1]) == "0", "balok bulat berdiri sendiri");
        Check(c[2].piece.numerator == 1 && c[2].piece.denominator == 4 && Dens(c[2], l1) == "4" && Src(c[2]) == "2", "sisa 1/4 berwarna 3/4");

        // urutan tarik tidak berpengaruh
        var l2 = new List<AreaBlock> { B(new Fraction(3, 4)), A(U(2)), A(Fraction.Whole(1)) };
        var c2 = AreaComposer.Compose(Operation.Add, l2);
        Check(c2.Count == 3 && c2[0].kind == ComposedKind.Carry && Dens(c2[0], l2) == "2,2,4,4" && Dens(c2[2], l2) == "4", "urutan tarik tidak berpengaruh");

        // penyebut tidak berkelipatan: KPK 12 -> 13/12
        var l3 = new List<AreaBlock> { A(U(3)), B(new Fraction(3, 4)) };
        var c3 = AreaComposer.Compose(Operation.Add, l3);
        Check(c3.Count == 2 && c3[0].piece.numerator == 12 && c3[0].piece.denominator == 12, "1/3 + 3/4: carry 12/12");
        Check(Dens(c3[0], l3) == "3,3,3,3,4,4,4,4,4,4,4,4", "carry: 4 sel warna 1/3 dan 8 sel warna 3/4");
        Check(c3[1].piece.numerator == 1 && c3[1].piece.denominator == 12 && Dens(c3[1], l3) == "4", "sisa 1/12 berwarna 3/4");

        var l4 = new List<AreaBlock> { A(U(2)) };
        var c4 = AreaComposer.Compose(Operation.Add, l4);
        Check(c4.Count == 1 && c4[0].kind == ComposedKind.Fraction && c4[0].piece.denominator == 2, "satu pecahan sebagian: apa adanya");

        var l5 = new List<AreaBlock> { A(U(4)), B(U(2)) };
        var c5 = AreaComposer.Compose(Operation.Add, l5);
        Check(c5.Count == 1 && c5[0].piece.numerator == 3 && c5[0].piece.denominator == 4 && Dens(c5[0], l5) == "2,2,4", "1/2 + 1/4 = 3/4 (tanpa carry)");

        var l6 = new List<AreaBlock> { A(new Fraction(3, 4)), B(new Fraction(3, 4)) };
        var c6 = AreaComposer.Compose(Operation.Add, l6);
        Check(c6.Count == 2 && c6[0].kind == ComposedKind.Carry && c6[1].piece.numerator == 2 && c6[1].piece.denominator == 4, "3/4 + 3/4 = carry 4/4 dan 2/4");

        var l7 = new List<AreaBlock> { A(new Fraction(2, 2)), A(U(2)) };
        var c7 = AreaComposer.Compose(Operation.Add, l7);
        Check(c7.Count == 2 && c7[0].kind == ComposedKind.Whole && c7[0].piece.denominator == 2 && c7[1].kind == ComposedKind.Fraction, "row penuh 2/2 berperilaku sebagai bilangan bulat");

        var l8 = new List<AreaBlock> { A(U(25)), B(U(24)) };
        var c8 = AreaComposer.Compose(Operation.Add, l8);
        Check(c8.Count == 2 && c8[0].kind == ComposedKind.Fraction && c8[1].kind == ComposedKind.Fraction, "KPK terlalu besar: tidak digabung");

        Check(AreaComposer.Compose(Operation.Add, new List<AreaBlock>()).Count == 0, "kosong tetap kosong");
    }

    void TestSubtract()
    {
        // 7/8 - 1/3 = 13/24 satu balok, sel dari A
        var l1 = new List<AreaBlock> { A(new Fraction(7, 8)), B(U(3), true) };
        var c1 = AreaComposer.Compose(Operation.Subtract, l1);
        Check(c1.Count == 1 && c1[0].kind == ComposedKind.Fraction && c1[0].piece.numerator == 13 && c1[0].piece.denominator == 24, "7/8 - 1/3 = 13/24");
        Check(Src(c1[0]) == "0,1" && c1[0].cellSource.All(i => i == 0), "semua row ikut kembali saat ditap; sel berwarna A");

        // baru B yang ditarik: tampil apa adanya
        var l2 = new List<AreaBlock> { B(U(3), true) };
        var c2 = AreaComposer.Compose(Operation.Subtract, l2);
        Check(c2.Count == 1 && Src(c2[0]) == "0", "baru B: tampil sendiri supaya bisa dikembalikan");

        // 1 7/8 - 1/3 = 1 13/24
        var l3 = new List<AreaBlock> { A(Fraction.Whole(1)), A(new Fraction(7, 8)), B(U(3), true) };
        var c3 = AreaComposer.Compose(Operation.Subtract, l3);
        Check(c3.Count == 2 && c3[0].kind == ComposedKind.Whole && c3[1].piece.numerator == 13 && c3[1].piece.denominator == 24, "1 7/8 - 1/3: satu bulat dan 13/24");

        // 1 1/3 - 7/8 = 11/24 (pinjam 1)
        var l4 = new List<AreaBlock> { A(Fraction.Whole(1)), A(U(3)), B(new Fraction(7, 8), true) };
        var c4 = AreaComposer.Compose(Operation.Subtract, l4);
        Check(c4.Count == 1 && c4[0].piece.numerator == 11 && c4[0].piece.denominator == 24, "1 1/3 - 7/8 = 11/24");

        // 1 1/2 - 1/2 = 1
        var l5 = new List<AreaBlock> { A(Fraction.Whole(1)), A(U(2)), B(U(2), true) };
        var c5 = AreaComposer.Compose(Operation.Subtract, l5);
        Check(c5.Count == 1 && c5[0].kind == ComposedKind.Whole, "hasil bulat: hanya balok bulat");
    }

    void TestSession()
    {
        var s = new FractionSession(Operation.Add);
        s.TryDropToKotak(true, Fraction.Whole(1)); s.TryDropToKotak(true, U(2));
        for (int i = 0; i < 3; i++) s.TryDropToKotak(false, U(4));
        s.CommitProblem();
        s.TryMoveToArea(true, 0); s.TryMoveToArea(true, 0); s.TryMoveToArea(false, 0);

        Check(s.Composed.Count == 3 && s.Composed[0].kind == ComposedKind.Carry, "sesi: carry, bilangan bulat, pecahan");
        Check(s.ReturnComposed(0) && s.A.Count == 1 && s.A.Pieces[0].denominator == 2 && s.B.Count == 1 && s.B.Pieces[0].numerator == 3,
              "tap carry: 1/2 dan 3/4 kembali ke Kotaknya");
        Check(s.Upper.Count == 1 && s.Composed.Count == 1 && s.Composed[0].kind == ComposedKind.Whole, "area tersisa [1]");
        Check(s.ReturnComposed(0) && s.A.Count == 2 && s.A.Pieces[0].denominator == 1 && s.A.Pieces[1].denominator == 2, "tap bilangan bulat: urutan asal Kotak A pulih");
        Check(!s.ReturnComposed(3), "indeks di luar jangkauan gagal");

        // tap balok pecahan sisa mengembalikan juga sumber carry
        s.TryMoveToArea(true, 0); s.TryMoveToArea(true, 0); s.TryMoveToArea(false, 0);
        Check(s.ReturnComposed(2) && s.Upper.Count == 1, "tap sisa 1/4: carry ikut kembali");

        // Media 6: yang dicari komplemen 11/24
        var m6 = new FractionSession(Operation.Subtract);
        for (int i = 0; i < 7; i++) m6.TryDropToKotak(true, U(8));
        m6.TryDropToKotak(false, U(3));
        Check(m6.CommitProblem() == ProblemError.None && m6.Current.result.ValueEquals(new Fraction(13, 24)), "7/8 - 1/3 = 13/24");
        m6.TryMoveToArea(true, 0); m6.TryMoveToArea(false, 0);
        Check(m6.Composed.Count == 1 && m6.Composed[0].piece.numerator == 13 && m6.Composed[0].piece.denominator == 24, "area atas: satu balok 13/24");
        for (int i = 0; i < 10; i++) m6.TryDropToDpz(U(24));
        Check(m6.Check() == CheckVerdict.Kurang, "10/24 dari 11/24 -> Kurang");
        m6.TryDropToDpz(U(24));
        Check(m6.Check() == CheckVerdict.Benar, "11/24 -> Benar");
        m6.TryDropToDpz(U(24));
        Check(m6.Check() == CheckVerdict.Kelebihan, "12/24 -> Kelebihan");
        Check(m6.ReturnComposed(0) && m6.A.Count == 1 && m6.B.Count == 1 && m6.Upper.IsEmpty, "tap balok gabungan: A dan B kembali");
    }
}