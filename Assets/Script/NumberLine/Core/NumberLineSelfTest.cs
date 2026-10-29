using UnityEngine;

// Tes logika inti Media 2 (tanpa scene). Klik kanan komponen > "Run NumberLine Tests"
public class NumberLineSelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run NumberLine Tests")]
    public void Run()
    {
        pass = fail = 0;

        TestConfigMath();
        TestClassify();
        TestSoalInput();
        TestSessionConfigAndPion();
        TestCheckAndCommit();
        TestReset();
        TestLabelsAndMixed();

        string summary = $"NumberLine tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static void Type(NumberLineSoalInput s, NumberLineCell cell, string digits)
    {
        foreach (char ch in digits) s.TypeDigit(cell, ch - '0');
    }

    // Isi Soal dari teks; string kosong = kotak dibiarkan kosong
    static void Fill(NumberLineSession s, string whole, string num, string den)
    {
        s.Soal.Clear();
        Type(s.Soal, NumberLineCell.Whole, whole);
        Type(s.Soal, NumberLineCell.Numerator, num);
        Type(s.Soal, NumberLineCell.Denominator, den);
    }

    void TestConfigMath()
    {
        var c = new NumberLineConfig(1, 5);
        Check(c.MaxTick == 5 && c.TickCount == 6, "panjang 1 partisi 5: tick 0..5");
        Check(c.IsWholeTick(0) && c.IsWholeTick(5) && !c.IsWholeTick(3), "tick bulat pada 0 dan 5");

        var c2 = new NumberLineConfig(2, 3);
        Check(c2.MaxTick == 6, "panjang 2 partisi 3: MaxTick 6");
        Check(c2.IsWholeTick(3) && c2.IsWholeTick(6) && !c2.IsWholeTick(4), "tick bulat pada 0, 3, 6");

        var c4 = new NumberLineConfig(1, 4);
        Fraction f = c4.TickToFraction(2);
        Check(f.numerator == 2 && f.denominator == 4, "tick 2 dari partisi 4 = 2/4 (tidak disederhanakan)");
        Check(f.ValueEquals(new Fraction(1, 2)), "2/4 bernilai sama dengan 1/2");

        Check(Mathf.Approximately(c4.TickToNormalized(0), 0f), "tick 0 di kiri");
        Check(Mathf.Approximately(c4.TickToNormalized(2), 0.5f), "tick 2 di tengah");
        Check(Mathf.Approximately(c4.TickToNormalized(4), 1f), "tick terakhir di kanan");

        Check(c4.NearestTick(0.5f) == 2, "0.5 ke tick 2");
        Check(c4.NearestTick(0.37f) == 1, "0.37 ke tick 1");
        Check(c4.NearestTick(0.38f) == 2, "0.38 ke tick 2");
        Check(c4.NearestTick(-0.2f) == 0, "di kiri garis dibatasi ke 0");
        Check(c4.NearestTick(1.7f) == 4, "di kanan garis dibatasi ke ujung");

        var c10 = new NumberLineConfig(2, 5);
        Check(c10.NearestTick(0.26f) == 3, "panjang 2 partisi 5: 0.26 ke tick 3");
        Check(c10.NearestTick(0.24f) == 2, "panjang 2 partisi 5: 0.24 ke tick 2");
    }

    void TestClassify()
    {
        var c = new NumberLineConfig(1, 5);
        NumberLineReach r;
        int t;

        r = c.Classify(new Fraction(3, 5), out t);
        Check(r == NumberLineReach.OnLine && t == 3, "3/5 di tick 3");
        r = c.Classify(new Fraction(6, 10), out t);
        Check(r == NumberLineReach.OnLine && t == 3, "6/10 senilai 3/5: tick 3");
        r = c.Classify(new Fraction(1, 3), out t);
        Check(r == NumberLineReach.NotOnPartition, "1/3 tidak ada di partisi 5");
        r = c.Classify(new Fraction(1, 2), out t);
        Check(r == NumberLineReach.NotOnPartition, "1/2 tidak ada di partisi 5");
        r = c.Classify(new Fraction(6, 5), out t);
        Check(r == NumberLineReach.BeyondLine, "6/5 melewati garis panjang 1");
        r = c.Classify(Fraction.Whole(1), out t);
        Check(r == NumberLineReach.OnLine && t == 5, "1 di tick terakhir");
        r = c.Classify(Fraction.Zero, out t);
        Check(r == NumberLineReach.OnLine && t == 0, "0 di tick 0");

        var c2 = new NumberLineConfig(2, 5);
        r = c2.Classify(new Fraction(8, 5), out t);
        Check(r == NumberLineReach.OnLine && t == 8, "panjang 2: 1 3/5 di tick 8");
        r = c2.Classify(Fraction.Whole(2), out t);
        Check(r == NumberLineReach.OnLine && t == 10, "panjang 2: 2 di tick 10");
        r = c2.Classify(new Fraction(11, 5), out t);
        Check(r == NumberLineReach.BeyondLine, "panjang 2: 2 1/5 melewati garis");

        var c4 = new NumberLineConfig(1, 4);
        r = c4.Classify(new Fraction(1, 2), out t);
        Check(r == NumberLineReach.OnLine && t == 2, "partisi 4: 1/2 di tick 2");
    }

    void TestSoalInput()
    {
        var s = new NumberLineSoalInput(25);
        int changes = 0;
        s.Changed += () => changes++;

        Check(s.IsEmpty, "awal kosong");
        Check(s.Evaluate(out Fraction v) == NumberLineSoalStatus.Empty, "kosong = Empty");
        Check(s.TextOf(NumberLineCell.Whole) == "" && s.TextOf(NumberLineCell.Numerator) == "", "teks awal kosong");

        Check(s.TypeDigit(NumberLineCell.Numerator, 1) == NumberLineEditResult.Ok, "ketik 1");
        Check(s.TypeDigit(NumberLineCell.Numerator, 2) == NumberLineEditResult.Ok, "ketik 2 menjadi 12");
        Check(s.Get(NumberLineCell.Numerator) == 12, "pembilang 12");
        Check(s.TypeDigit(NumberLineCell.Numerator, 5) == NumberLineEditResult.Overflow, "125 melebihi 25");
        Check(!s.Get(NumberLineCell.Numerator).HasValue, "kotak dikosongkan setelah overflow");

        Type(s, NumberLineCell.Numerator, "25");
        Check(s.Get(NumberLineCell.Numerator) == 25, "25 diterima");
        Check(s.TypeDigit(NumberLineCell.Numerator, 0) == NumberLineEditResult.Overflow, "250 melebihi 25");

        s.Clear();
        Type(s, NumberLineCell.Numerator, "25");
        s.Backspace(NumberLineCell.Numerator);
        Check(s.Get(NumberLineCell.Numerator) == 2, "backspace 25 menjadi 2");
        s.Backspace(NumberLineCell.Numerator);
        Check(!s.Get(NumberLineCell.Numerator).HasValue, "backspace 2 mengosongkan");
        s.Backspace(NumberLineCell.Numerator);
        Check(s.IsEmpty, "backspace pada kotak kosong tidak melakukan apa-apa");

        s.TypeDigit(NumberLineCell.Whole, 0);
        Check(s.Get(NumberLineCell.Whole) == 0 && s.TextOf(NumberLineCell.Whole) == "", "whole 0 tampil kosong");
        Check(s.Evaluate(out v) == NumberLineSoalStatus.Valid && v.ValueEquals(Fraction.Zero), "hanya whole 0 = nilai 0");
        s.Clear();

        Type(s, NumberLineCell.Whole, "1");
        Type(s, NumberLineCell.Numerator, "3");
        Type(s, NumberLineCell.Denominator, "4");
        Check(s.Evaluate(out v) == NumberLineSoalStatus.Valid && v.numerator == 7 && v.denominator == 4, "1 3/4 = 7/4");

        s.Clear();
        Type(s, NumberLineCell.Numerator, "3");
        Type(s, NumberLineCell.Denominator, "4");
        Check(s.Evaluate(out v) == NumberLineSoalStatus.Valid && v.numerator == 3 && v.denominator == 4, "tanpa whole: 3/4");

        s.Clear();
        Type(s, NumberLineCell.Whole, "2");
        Check(s.Evaluate(out v) == NumberLineSoalStatus.Valid && v.ValueEquals(Fraction.Whole(2)), "hanya whole 2 = 2");

        s.Clear();
        Type(s, NumberLineCell.Numerator, "3");
        Check(s.Evaluate(out v) == NumberLineSoalStatus.Incomplete, "pembilang saja = Incomplete");
        s.Clear();
        Type(s, NumberLineCell.Denominator, "4");
        Check(s.Evaluate(out v) == NumberLineSoalStatus.Incomplete, "penyebut saja = Incomplete");

        s.Clear();
        Type(s, NumberLineCell.Numerator, "3");
        Type(s, NumberLineCell.Denominator, "0");
        Check(s.Evaluate(out v) == NumberLineSoalStatus.Impossible, "penyebut 0 = Impossible");

        s.Clear();
        Type(s, NumberLineCell.Numerator, "0");
        Type(s, NumberLineCell.Denominator, "5");
        Check(s.Evaluate(out v) == NumberLineSoalStatus.Valid && v.ValueEquals(Fraction.Zero), "0/5 valid bernilai 0");

        s.Clear();
        Type(s, NumberLineCell.Whole, "25");
        Type(s, NumberLineCell.Numerator, "25");
        Type(s, NumberLineCell.Denominator, "25");
        Check(s.Evaluate(out v) == NumberLineSoalStatus.Valid && v.numerator == 650 && v.denominator == 25, "nilai terbesar 25 25/25 = 650/25");

        s.Clear();
        Check(s.IsEmpty, "Clear mengosongkan semua");
        Check(changes > 0, "event Changed terpicu");
        int before = changes;
        s.Clear();
        Check(changes == before, "Clear pada Soal kosong tidak memicu event");
    }

    void TestSessionConfigAndPion()
    {
        var s = new NumberLineSession();
        Check(s.Config.length == 1 && s.Config.partisi == 5, "default panjang 1 partisi 5");
        Check(s.PionTick == 0 && !s.NeedsConfirmBeforeReconfigure, "pion di 0, tidak perlu konfirmasi");

        Check(s.Validate(0, 5) == NumberLineConfigError.LengthOutOfRange, "panjang 0 ditolak");
        Check(s.Validate(6, 5) == NumberLineConfigError.LengthOutOfRange, "panjang 6 ditolak");
        Check(s.Validate(1, 0) == NumberLineConfigError.PartisiOutOfRange, "partisi 0 ditolak");
        Check(s.Validate(1, 16) == NumberLineConfigError.PartisiOutOfRange, "partisi 16 ditolak");
        Check(s.Validate(5, 15) == NumberLineConfigError.None, "panjang 5 partisi 15 diterima");
        Check(s.ApplyConfig(9, 5) == NumberLineConfigError.LengthOutOfRange && s.Config.length == 1, "konfigurasi tidak valid tidak diterapkan");

        Check(s.SetPionTick(3) == 3 && s.PionTick == 3, "pion ke tick 3");
        Check(s.NeedsConfirmBeforeReconfigure, "pion sudah digeser: perlu konfirmasi");
        Check(s.SetPionTick(99) == 5, "tick di atas MaxTick dibatasi");
        Check(s.SetPionTick(-2) == 0, "tick negatif dibatasi ke 0");

        int changes = 0;
        s.Changed += () => changes++;

        s.SetPionTick(3);
        Check(changes == 1, "menggeser pion memicu Changed");
        s.SetPionTick(3);
        Check(changes == 1, "menggeser ke tick yang sama tidak memicu Changed");

        Check(s.ApplyConfig(1, 5) == NumberLineConfigError.None && s.PionTick == 3 && changes == 1, "konfigurasi sama: pion tetap, tidak ada event");
        Check(s.ApplyConfig(2, 5) == NumberLineConfigError.None, "ubah panjang ke 2");
        Check(s.Config.length == 2 && s.PionTick == 0 && changes == 2, "konfigurasi baru: pion kembali ke 0");
        Check(s.ApplyConfig(2, 4) == NumberLineConfigError.None && s.Config.partisi == 4, "ubah partisi ke 4");

        s.SetPionTick(3);
        Check(s.PionValue.numerator == 3 && s.PionValue.denominator == 4, "nilai pion = 3/4");

        var custom = new NumberLineSession(new NumberLineLimits { MaxPartisi = 8 });
        Check(custom.Validate(1, 9) == NumberLineConfigError.PartisiOutOfRange, "MaxPartisi kustom 8: partisi 9 ditolak");
        Check(custom.Validate(1, 8) == NumberLineConfigError.None, "MaxPartisi kustom 8: partisi 8 diterima");
    }

    void TestCheckAndCommit()
    {
        var s = new NumberLineSession();    // panjang 1, partisi 5

        Check(s.Check().outcome == NumberLineCheckOutcome.SoalEmpty, "Soal kosong");
        Fill(s, "", "3", "");
        Check(s.Check().outcome == NumberLineCheckOutcome.SoalIncomplete, "Soal belum lengkap");

        Fill(s, "", "3", "5");
        s.SetPionTick(3);
        Check(s.Check().outcome == NumberLineCheckOutcome.Correct, "3/5 dan pion di tick 3: benar");
        s.SetPionTick(2);
        NumberLineCheckResult r = s.Check();
        Check(r.outcome == NumberLineCheckOutcome.Wrong && r.reach == NumberLineReach.OnLine, "pion di tick 2: salah, nilai ada di garis");

        Fill(s, "", "6", "10");
        s.SetPionTick(3);
        Check(s.Check().outcome == NumberLineCheckOutcome.Correct, "6/10 senilai 3/5: benar");

        Fill(s, "", "1", "3");
        s.SetPionTick(2);
        r = s.Check();
        Check(r.outcome == NumberLineCheckOutcome.Wrong && r.reach == NumberLineReach.NotOnPartition, "1/3 tidak ada di partisi 5: salah");

        Fill(s, "1", "1", "5");
        r = s.Check();
        Check(r.outcome == NumberLineCheckOutcome.Wrong && r.reach == NumberLineReach.BeyondLine, "1 1/5 melewati garis: salah");

        Fill(s, "", "3", "0");
        Check(s.Check().outcome == NumberLineCheckOutcome.SoalImpossible && s.Soal.IsEmpty, "penyebut 0: Soal tidak mungkin dan dikosongkan");

        Fill(s, "", "7", "5");
        NumberLineCommit c = s.CommitSoal();
        Check(c.status == NumberLineSoalStatus.Valid && c.reach == NumberLineReach.BeyondLine, "7/5 pada garis panjang 1: diterima tapi melewati garis");
        Check(!s.Soal.IsEmpty, "Soal yang melewati garis tetap tersimpan");

        s.ApplyConfig(3, 5);
        Fill(s, "2", "3", "5");
        c = s.CommitSoal();
        Check(c.status == NumberLineSoalStatus.Valid && c.reach == NumberLineReach.OnLine && c.tick == 13, "panjang 3: 2 3/5 di tick 13");
        s.SetPionTick(13);
        Check(s.Check().outcome == NumberLineCheckOutcome.Correct, "pion di tick 13: benar");

        Fill(s, "", "3", "");
        c = s.CommitSoal();
        Check(c.status == NumberLineSoalStatus.Incomplete, "Commit: belum lengkap");
        Fill(s, "", "3", "0");
        c = s.CommitSoal();
        Check(c.status == NumberLineSoalStatus.Impossible && s.Soal.IsEmpty, "Commit: penyebut 0 dikosongkan");
    }

    void TestReset()
    {
        var p = new NumberLineSession();
        Check(p.IsPristine, "sesi baru dalam keadaan awal");
        p.SetPionTick(1);
        Check(!p.IsPristine, "pion digeser: bukan keadaan awal");
        p.ResetAll();
        Check(p.IsPristine, "setelah reset: keadaan awal");
        Fill(p, "", "1", "2");
        Check(!p.IsPristine, "Soal terisi: bukan keadaan awal");
        p.ResetAll();
        p.ApplyConfig(2, 5);
        Check(!p.IsPristine, "garis diubah: bukan keadaan awal");

        var s = new NumberLineSession();
        s.ApplyConfig(4, 9);
        s.SetPionTick(12);
        Fill(s, "1", "2", "3");

        int changes = 0;
        s.Changed += () => changes++;
        s.ResetAll();

        Check(s.Config.length == 1 && s.Config.partisi == 5, "reset: garis kembali ke default");
        Check(s.PionTick == 0, "reset: pion ke 0");
        Check(s.Soal.IsEmpty, "reset: Soal kosong");
        Check(changes >= 1, "reset memicu Changed");
        Check(!s.NeedsConfirmBeforeReconfigure, "setelah reset tidak perlu konfirmasi");
        Check(s.IsPristine, "sesi lama setelah reset: keadaan awal");
    }

    void TestLabelsAndMixed()
    {
        var c = new NumberLineConfig(2, 5);
        int w, n;

        c.TickToMixed(8, out w, out n);
        Check(w == 1 && n == 3, "tick 8 (partisi 5) = 1 3/5");
        c.TickToMixed(3, out w, out n);
        Check(w == 0 && n == 3, "tick 3 = 0 3/5");
        c.TickToMixed(5, out w, out n);
        Check(w == 1 && n == 0, "tick 5 = 1 tepat");
        c.TickToMixed(10, out w, out n);
        Check(w == 2 && n == 0, "tick 10 = 2 tepat");
        c.TickToMixed(0, out w, out n);
        Check(w == 0 && n == 0, "tick 0 = 0");

        Check(NumberLineLabels.Letter(0) == "A", "indeks 0 = A");
        Check(NumberLineLabels.Letter(1) == "B", "indeks 1 = B");
        Check(NumberLineLabels.Letter(25) == "Z", "indeks 25 = Z");
        Check(NumberLineLabels.Letter(26) == "AA", "indeks 26 = AA");
        Check(NumberLineLabels.Letter(27) == "AB", "indeks 27 = AB");
        Check(NumberLineLabels.Letter(-1) == "?", "indeks negatif = ?");
    }
}