using UnityEngine;

// Tes aturan Kotak revisi 4 (tanpa scene). Klik kanan komponen > "Run Kotak Rules Tests"
public class KotakRulesSelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run Kotak Rules Tests")]
    public void Run()
    {
        pass = fail = 0;

        TestWholes();
        TestFractionKind();
        TestAutoMergeAndCapacity();
        TestMergeTarget();
        TestFullAndPartial();
        TestResultCap();
        TestNoMutationOnReject();
        TestEmptyHelpers();

        string summary = $"Kotak rules tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    // Satu potongan 1/den seperti dari kartu bank (den = 1 berarti bilangan bulat)
    static RejectReason Drop(FractionSession s, bool intoA, int den) =>
        s.TryDropToKotak(intoA, new Fraction(1, den));

    void TestWholes()
    {
        var s = new FractionSession(Operation.Add);
        for (int i = 0; i < 4; i++)
            Check(Drop(s, true, 1) == RejectReason.None, $"bilangan bulat ke-{i + 1} diterima");

        Check(s.A.Count == 4 && s.A.WholeBlockCount == 4, "empat row bilangan bulat");
        Check(Drop(s, true, 1) == RejectReason.KotakBulatPenuh, "bilangan bulat ke-5 ditolak");
        Check(s.A.Count == 4, "isi tetap setelah ditolak");
        Check(s.PreviewDropToKotak(true, new Fraction(1, 1)) == RejectReason.KotakBulatPenuh, "preview sama dengan hasil drop");
        Check(Drop(s, false, 1) == RejectReason.None, "Kotak B punya batas sendiri");
    }

    void TestFractionKind()
    {
        var s = new FractionSession(Operation.Add);
        Check(Drop(s, true, 2) == RejectReason.None, "1/2 diterima");
        Check(Drop(s, true, 4) == RejectReason.KotakPecahanBeda, "1/4 ditolak: Kotak sudah berjenis 1/2");
        Check(s.A.Count == 1 && s.A.Pieces[0].numerator == 1 && s.A.Pieces[0].denominator == 2, "isi lama tetap 1/2");

        Check(Drop(s, true, 2) == RejectReason.None, "1/2 lagi diterima");
        Check(s.A.Count == 1 && s.A.Pieces[0].numerator == 2 && s.A.Pieces[0].denominator == 2, "dua 1/2 menyatu jadi 2/2");

        Check(Drop(s, true, 1) == RejectReason.None, "bilangan bulat boleh bersama pecahan");
        Check(s.A.Count == 2 && s.A.WholeBlockCount == 1 && s.A.FractionRowCount == 1, "satu row bulat dan satu row pecahan");
        Check(s.A.TryGetFractionKind(out long kind) && kind == 2, "jenis pecahan Kotak A = 2");

        Check(Drop(s, false, 4) == RejectReason.None, "Kotak B punya jenis pecahan sendiri");

        s.ClearKotak(true);
        Check(Drop(s, true, 4) == RejectReason.None, "setelah dikosongkan, jenis pecahan baru diterima");
    }

    void TestAutoMergeAndCapacity()
    {
        var s = new FractionSession(Operation.Add);
        Drop(s, true, 3);
        Check(Drop(s, true, 3) == RejectReason.None && s.A.Count == 1 && s.A.Pieces[0].numerator == 2, "1/3 + 1/3 = 2/3 dalam satu row");

        Check(s.TryDropToKotak(true, new Fraction(1, 3), 5) == RejectReason.None && s.A.Count == 1 && s.A.Pieces[0].numerator == 3,
              "rowIndex diabaikan: tetap merge ke row yang belum penuh");

        Check(Drop(s, true, 3) == RejectReason.None && s.A.Count == 2 && s.A.Pieces[1].numerator == 1, "row penuh: row baru");

        Check(s.TryDropToKotak(true, new Fraction(1, 3), 0) == RejectReason.None && s.A.Count == 2
              && s.A.Pieces[0].numerator == 3 && s.A.Pieces[1].numerator == 2,
              "dijatuhkan di row penuh: tetap merge ke row kedua yang belum penuh");

        Check(Drop(s, true, 3) == RejectReason.None && s.A.Pieces[1].numerator == 3, "row kedua penuh");
        Check(Drop(s, true, 3) == RejectReason.KotakPecahanPenuh && s.A.Count == 2, "row ketiga ditolak");
        Check(s.A.Total().ValueEquals(Fraction.Whole(2)), "total dua row penuh = 2");
    }

    void TestMergeTarget()
    {
        var s = new FractionSession(Operation.Add);
        Check(s.FindKotakMergeTarget(true, new Fraction(1, 2)) == -1, "Kotak kosong: tidak ada target");

        Drop(s, true, 2);
        Check(s.FindKotakMergeTarget(true, new Fraction(1, 2)) == 0, "row 1/2 yang belum penuh jadi target");
        Check(s.FindKotakMergeTarget(true, new Fraction(1, 4)) == -1, "satuan beda: tidak ada target");
        Check(s.FindKotakMergeTarget(true, new Fraction(1, 1)) == -1, "bilangan bulat selalu row sendiri");

        Drop(s, true, 2);
        Check(s.FindKotakMergeTarget(true, new Fraction(1, 2)) == -1, "row penuh: row baru");
        Check(s.FindKotakMergeTarget(false, new Fraction(1, 2)) == -1, "Kotak B kosong: tidak ada target");
    }

    // Contoh klien: A = 1 2/4 + ... Kotak A [2/2, 1/2] dan 1, Kotak B [3/4]
    void TestFullAndPartial()
    {
        var s = new FractionSession(Operation.Add);
        Drop(s, true, 2);
        Drop(s, true, 2);
        Drop(s, true, 2);
        Check(s.A.Count == 2 && s.A.Pieces[0].numerator == 2 && s.A.Pieces[1].numerator == 1, "Kotak A = [2/2, 1/2]");

        Drop(s, true, 1);
        Check(s.A.FullRowCount == 2 && s.A.WholeBlockCount == 1, "2/2 dan 1 sama-sama row penuh");
        Check(s.A.PartialTotal().ValueEquals(new Fraction(1, 2)), "bagian pecahan Kotak A = 1/2");

        Drop(s, false, 4);
        Drop(s, false, 4);
        Drop(s, false, 4);
        Check(s.B.FullRowCount == 0 && s.B.PartialTotal().ValueEquals(new Fraction(3, 4)), "Kotak B = [3/4], tanpa row penuh");

        Check(s.CommitProblem() == ProblemError.None && s.CurrentPhase == Phase.Merakit, "soal terbentuk");
        Check(s.Current.result.ValueEquals(new Fraction(13, 4)), "2 1/2 + 3/4 = 13/4");
    }

    void TestResultCap()
    {
        var s = new FractionSession(Operation.Add);
        for (int i = 0; i < 4; i++) Drop(s, true, 1);
        Check(Drop(s, false, 1) == RejectReason.None, "A = 4, B = 1");
        Check(Drop(s, false, 1) == RejectReason.None, "A = 4, B = 2: total 6 masih muat");
        Check(Drop(s, false, 1) == RejectReason.HasilTerlaluBesar && s.B.Count == 2, "A = 4, B = 3: total 7 melewati batas hasil");

        var sub = new FractionSession(Operation.Subtract);
        bool allAccepted = true;
        for (int i = 0; i < 4; i++)
        {
            if (Drop(sub, true, 1) != RejectReason.None) allAccepted = false;
            if (Drop(sub, false, 1) != RejectReason.None) allAccepted = false;
        }
        Check(allAccepted, "pengurangan: batas hasil tidak dicek per drop");
        Check(sub.CommitProblem() == ProblemError.HasilTidakPositif, "4 - 4 tidak positif");
    }

    void TestNoMutationOnReject()
    {
        var s = new FractionSession(Operation.Add);
        Drop(s, true, 2);

        int events = 0;
        s.A.Changed += () => events++;

        Check(Drop(s, true, 4) == RejectReason.KotakPecahanBeda && events == 0, "penolakan tidak memicu Changed");
        Check(s.PreviewDropToKotak(true, new Fraction(1, 4)) == RejectReason.KotakPecahanBeda && events == 0, "preview tidak memicu Changed");
        Check(Drop(s, true, 2) == RejectReason.None && events == 1, "drop yang diterima memicu satu Changed");
        s.ClearKotak(true);
        Check(events == 2, "mengosongkan memicu Changed");
    }

    void TestEmptyHelpers()
    {
        var k = new KotakContents();
        Check(k.WholeBlockCount == 0 && k.FractionRowCount == 0 && k.FullRowCount == 0, "Kotak kosong: semua hitungan 0");
        Check(!k.TryGetFractionKind(out long kind) && kind == 0, "Kotak kosong: belum ada jenis pecahan");
        Check(k.PartialTotal().ValueEquals(Fraction.Zero), "Kotak kosong: bagian sebagian = 0");
    }
}