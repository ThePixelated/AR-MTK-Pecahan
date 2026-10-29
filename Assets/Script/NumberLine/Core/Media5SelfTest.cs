using System.Collections.Generic;
using UnityEngine;

// Tes logika inti Media 5 (tanpa scene). Klik kanan komponen > "Run Media5 Tests"
public class Media5SelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run Media5 Tests")]
    public void Run()
    {
        pass = fail = 0;

        TestProblem();
        TestGeneratorBasics();
        TestGeneratorAntiRepeat();
        TestSessionFlow();
        TestSessionOptions();
        TestSessionReconfigAndReset();

        string summary = $"Media5 tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static NumberLineProblem Sub(int a, int b) => new NumberLineProblem(NumberLineOperation.Subtract, a, b);
    static NumberLineProblem Add(int a, int b) => new NumberLineProblem(NumberLineOperation.Add, a, b);

    void TestProblem()
    {
        var cfg = new NumberLineConfig(1, 5);    // MaxTick 5

        Check(Sub(5, 2).ResultTick == 3, "5 - 2 = 3");
        Check(Add(2, 3).ResultTick == 5, "2 + 3 = 5");
        Check(Sub(5, 2).SamePair(Sub(5, 2)), "pasangan sama");
        Check(!Sub(5, 2).SamePair(Sub(5, 3)), "angka beda bukan pasangan sama");
        Check(!Sub(5, 2).SamePair(Add(5, 2)), "operasi beda bukan pasangan sama");

        Check(Sub(5, 2).IsValidFor(cfg), "pengurangan 5-2 sah pada MaxTick 5");
        Check(!Sub(6, 2).IsValidFor(cfg), "A melewati garis tidak sah");
        Check(!Sub(2, 2).IsValidFor(cfg), "A harus lebih besar dari B");
        Check(!Sub(1, 2).IsValidFor(cfg), "A lebih kecil dari B tidak sah");
        Check(!Sub(2, 0).IsValidFor(cfg), "B minimal 1");

        Check(Add(3, 2).IsValidFor(cfg), "penjumlahan 3+2 sah (hasil tepat di ujung)");
        Check(!Add(3, 3).IsValidFor(cfg), "penjumlahan melewati garis tidak sah");
        Check(!Add(0, 2).IsValidFor(cfg), "operan penjumlahan minimal 1");
    }

    void TestGeneratorBasics()
    {
        NumberLineProblem p;

        var g = new NumberLineProblemGenerator(42, 5);
        Check(!g.TryGenerate(new NumberLineConfig(1, 1), Media5OperationMode.SubtractOnly, out p), "MaxTick 1: tidak ada soal");

        Check(NumberLineProblemGenerator.PossibleCount(new NumberLineConfig(1, 1), Media5OperationMode.SubtractOnly) == 0, "jumlah soal MaxTick 1 = 0");
        Check(NumberLineProblemGenerator.PossibleCount(new NumberLineConfig(1, 2), Media5OperationMode.SubtractOnly) == 1, "jumlah soal MaxTick 2 = 1");
        Check(NumberLineProblemGenerator.PossibleCount(new NumberLineConfig(1, 5), Media5OperationMode.SubtractOnly) == 10, "jumlah soal MaxTick 5 = 10");
        Check(NumberLineProblemGenerator.PossibleCount(new NumberLineConfig(1, 5), Media5OperationMode.Both) == 20, "jumlah soal dua operasi = 20");

        // satu-satunya soal pada garis 1 x 2 adalah 2 - 1, dan boleh berulang karena tidak ada pilihan lain
        var cfg2 = new NumberLineConfig(1, 2);
        bool onlyOne = true;
        for (int i = 0; i < 3; i++)
        {
            bool ok = g.TryGenerate(cfg2, Media5OperationMode.SubtractOnly, out p);
            if (!ok || p.aTick != 2 || p.bTick != 1) onlyOne = false;
        }
        Check(onlyOne, "garis 1 x 2: selalu 2 - 1");

        var cfg5 = new NumberLineConfig(1, 5);
        bool subValid = true, addValid = true;
        var gs = new NumberLineProblemGenerator(1, 5);
        var ga = new NumberLineProblemGenerator(2, 5);
        for (int i = 0; i < 100; i++)
        {
            gs.TryGenerate(cfg5, Media5OperationMode.SubtractOnly, out p);
            if (p.operation != NumberLineOperation.Subtract || !p.IsValidFor(cfg5)) subValid = false;
            ga.TryGenerate(cfg5, Media5OperationMode.AddOnly, out p);
            if (p.operation != NumberLineOperation.Add || !p.IsValidFor(cfg5)) addValid = false;
        }
        Check(subValid, "100 soal pengurangan semuanya sah");
        Check(addValid, "100 soal penjumlahan semuanya sah");

        var gb = new NumberLineProblemGenerator(3, 0);
        bool sawSub = false, sawAdd = false, bothValid = true;
        for (int i = 0; i < 200; i++)
        {
            gb.TryGenerate(cfg5, Media5OperationMode.Both, out p);
            if (!p.IsValidFor(cfg5)) bothValid = false;
            if (p.operation == NumberLineOperation.Subtract) sawSub = true; else sawAdd = true;
        }
        Check(bothValid && sawSub && sawAdd, "mode Both menghasilkan kedua operasi, semuanya sah");

        var cfgBig = new NumberLineConfig(5, 15);    // MaxTick 75
        var gbig = new NumberLineProblemGenerator(4, 5);
        bool bigValid = true;
        for (int i = 0; i < 50; i++)
        {
            gbig.TryGenerate(cfgBig, Media5OperationMode.SubtractOnly, out p);
            if (!p.IsValidFor(cfgBig) || p.aTick > 75) bigValid = false;
        }
        Check(bigValid, "garis 5 x 15: soal sah dan A <= 75");

        var g1 = new NumberLineProblemGenerator(7, 5);
        var g2 = new NumberLineProblemGenerator(7, 5);
        bool same = true;
        for (int i = 0; i < 10; i++)
        {
            g1.TryGenerate(cfg5, Media5OperationMode.SubtractOnly, out NumberLineProblem p1);
            g2.TryGenerate(cfg5, Media5OperationMode.SubtractOnly, out NumberLineProblem p2);
            if (!p1.SamePair(p2)) same = false;
        }
        Check(same, "seed sama menghasilkan urutan soal yang sama");
    }

    void TestGeneratorAntiRepeat()
    {
        NumberLineProblem p;

        var cfg = new NumberLineConfig(2, 5);    // MaxTick 10, 45 soal
        var g = new NumberLineProblemGenerator(3, 5);
        var history = new List<NumberLineProblem>();
        bool noRepeat = true;
        for (int i = 0; i < 60; i++)
        {
            g.TryGenerate(cfg, Media5OperationMode.SubtractOnly, out p);
            for (int j = Mathf.Max(0, i - 5); j < i; j++)
                if (history[j].SamePair(p)) noRepeat = false;
            history.Add(p);
        }
        Check(noRepeat, "tidak ada soal yang sama dalam 5 soal terakhir");

        // jendela lebih besar dari jumlah soal: tidak pernah sama dua kali berturutan, dan ketiga soal muncul
        var small = new NumberLineConfig(1, 3);    // MaxTick 3, 3 soal
        var gs = new NumberLineProblemGenerator(5, 10);
        var seen = new HashSet<int>();
        bool noConsecutive = true, havePrev = false;
        NumberLineProblem prev = default;
        for (int i = 0; i < 30; i++)
        {
            gs.TryGenerate(small, Media5OperationMode.SubtractOnly, out p);
            if (havePrev && prev.SamePair(p)) noConsecutive = false;
            prev = p;
            havePrev = true;
            seen.Add(p.aTick * 10 + p.bTick);
        }
        Check(noConsecutive, "jendela besar pada garis kecil: tidak ada soal kembar berturutan");
        Check(seen.Count == 3, "ketiga soal garis 1 x 3 muncul");

        // sama lewat session
        var s = new Media5Session(null, 9);
        s.ApplyConfig(2, 5);
        var list = new List<NumberLineProblem>();
        bool ok = true;
        for (int i = 0; i < 20; i++)
        {
            s.TryNewProblem();
            NumberLineProblem sp = s.Problem.Value;
            for (int j = Mathf.Max(0, i - 5); j < i; j++)
                if (list[j].SamePair(sp)) ok = false;
            list.Add(sp);
        }
        Check(ok, "session: soal tidak berulang dalam jendela anti-ulang");
    }

    void TestSessionFlow()
    {
        var s = new Media5Session(null, 1);
        Check(s.Config.length == 1 && s.Config.partisi == 5, "default panjang 1 partisi 5");
        Check(!s.HasProblem && s.PionTick == 0 && s.IsPristine && !s.NeedsConfirmBeforeReconfigure, "awal: tanpa soal, pion di 0");
        Check(s.JumpForward() == Media5JumpResult.NoProblem, "lompat maju tanpa soal ditolak");
        Check(s.JumpBackward() == Media5JumpResult.NoProblem, "lompat mundur tanpa soal ditolak");
        Check(s.Check() == Media5CheckOutcome.NoProblem, "CEK tanpa soal ditolak");
        Check(s.RetryPosition() == Media5RetryResult.NoProblem, "Coba lagi tanpa soal ditolak");

        Check(s.ApplyConfig(1, 2) == NumberLineConfigError.None && s.Config.partisi == 2, "garis 1 x 2");
        Check(s.TryNewProblem() == Media5ProblemError.None && s.HasProblem, "buat soal");

        NumberLineProblem p = s.Problem.Value;
        Check(p.operation == NumberLineOperation.Subtract && p.aTick == 2 && p.bTick == 1 && s.ResultTick == 1, "satu-satunya soal: 2 - 1 = 1");
        Check(s.PionTick == 2, "pion mulai di A");
        Check(!s.IsSolved && s.NeedsConfirmBeforeReconfigure && !s.IsPristine, "soal belum terjawab: perlu konfirmasi");

        Check(s.JumpForward() == Media5JumpResult.AtEnd && s.PionTick == 2, "di ujung kanan: tidak bisa maju");
        Check(s.Check() == Media5CheckOutcome.Wrong && !s.IsSolved, "pion di A: salah");
        Check(s.JumpBackward() == Media5JumpResult.Moved && s.PionTick == 1, "mundur 1 tick");
        Check(s.Check() == Media5CheckOutcome.Correct && s.IsSolved, "pion di hasil: benar");
        Check(!s.NeedsConfirmBeforeReconfigure, "sudah benar: ubah garis tidak perlu konfirmasi");
        Check(s.Check() == Media5CheckOutcome.Correct, "CEK ulang tetap benar");

        Check(s.JumpBackward() == Media5JumpResult.Moved && s.PionTick == 0, "mundur ke 0");
        Check(s.JumpBackward() == Media5JumpResult.AtStart && s.PionTick == 0, "di ujung kiri: tidak bisa mundur");
        Check(s.Check() == Media5CheckOutcome.Wrong && s.IsSolved, "pion di luar hasil: salah, status terjawab tetap");

        Check(s.StartTick == 2, "StartTick = A");
        Check(s.RetryPosition() == Media5RetryResult.Moved && s.PionTick == 2, "Coba lagi: pion kembali ke A");
        Check(s.IsSolved, "Coba lagi tidak menghapus status terjawab");
        Check(s.RetryPosition() == Media5RetryResult.AlreadyAtStart, "Coba lagi saat sudah di A: tidak ada perubahan");
    }

    void TestSessionOptions()
    {
        var startAtZero = new Media5Session(new Media5Limits { StartAtOperand = false }, 1);
        startAtZero.ApplyConfig(1, 2);
        startAtZero.TryNewProblem();
        Check(startAtZero.PionTick == 0, "StartAtOperand mati: pion mulai di 0");
        startAtZero.JumpForward();
        Check(startAtZero.PionTick == 1 && startAtZero.StartTick == 0, "StartAtOperand mati: StartTick = 0");
        Check(startAtZero.RetryPosition() == Media5RetryResult.Moved && startAtZero.PionTick == 0, "Coba lagi kembali ke 0");

        var addOnly = new Media5Session(new Media5Limits { Operations = Media5OperationMode.AddOnly }, 1);
        addOnly.ApplyConfig(1, 2);
        Check(addOnly.TryNewProblem() == Media5ProblemError.None, "buat soal penjumlahan");
        NumberLineProblem ap = addOnly.Problem.Value;
        Check(ap.operation == NumberLineOperation.Add && ap.aTick == 1 && ap.bTick == 1 && addOnly.ResultTick == 2, "satu-satunya soal: 1 + 1 = 2");
        Check(addOnly.PionTick == 1, "penjumlahan: pion mulai di A");
        Check(addOnly.JumpForward() == Media5JumpResult.Moved && addOnly.PionTick == 2, "maju 1 tick");
        Check(addOnly.Check() == Media5CheckOutcome.Correct, "pion di hasil: benar");

        var minTwo = new Media5Session(new Media5Limits { MinPartisiForProblem = 2 }, 1);
        Check(minTwo.ApplyConfig(2, 1) == NumberLineConfigError.None, "garis 2 x 1");
        Check(minTwo.TryNewProblem() == Media5ProblemError.PartisiTooSmall && !minTwo.HasProblem, "partisi 1 ditolak saat minimal 2");
        minTwo.ApplyConfig(1, 2);
        Check(minTwo.TryNewProblem() == Media5ProblemError.None, "partisi 2 diterima");

        var normal = new Media5Session(null, 1);
        normal.ApplyConfig(1, 1);
        Check(normal.TryNewProblem() == Media5ProblemError.NoProblemPossible, "garis 1 x 1 tidak punya soal");
        normal.ApplyConfig(2, 1);
        Check(normal.TryNewProblem() == Media5ProblemError.None, "garis 2 x 1 (partisi 1 dibolehkan) punya soal");
        NumberLineProblem np = normal.Problem.Value;
        Check(np.aTick == 2 && np.bTick == 1, "garis 2 x 1: soal 2 - 1");
    }

    void TestSessionReconfigAndReset()
    {
        var s = new Media5Session(null, 2);
        Check(s.Validate(0, 5) == NumberLineConfigError.LengthOutOfRange, "panjang 0 ditolak");
        Check(s.Validate(6, 5) == NumberLineConfigError.LengthOutOfRange, "panjang 6 ditolak");
        Check(s.Validate(1, 0) == NumberLineConfigError.PartisiOutOfRange, "partisi 0 ditolak");
        Check(s.Validate(1, 16) == NumberLineConfigError.PartisiOutOfRange, "partisi 16 ditolak");

        s.ApplyConfig(1, 3);
        s.TryNewProblem();
        int changes = 0;
        s.Changed += () => changes++;

        Check(s.NeedsConfirmBeforeReconfigure, "soal belum terjawab: perlu konfirmasi");
        Check(s.ApplyConfig(1, 3) == NumberLineConfigError.None && changes == 0 && s.HasProblem, "garis sama: soal dan pion tetap, tanpa event");
        Check(s.ApplyConfig(6, 3) == NumberLineConfigError.LengthOutOfRange && s.Config.length == 1, "konfigurasi tidak valid tidak diterapkan");

        Check(s.ApplyConfig(2, 3) == NumberLineConfigError.None, "ubah garis");
        Check(!s.HasProblem && s.PionTick == 0 && s.Config.length == 2 && changes == 1, "garis baru: soal dibuang, pion ke 0");

        s.TryNewProblem();
        Check(changes == 2, "buat soal memicu Changed");
        s.JumpBackward();
        Check(changes == 3, "lompat memicu Changed");

        while (s.JumpBackward() == Media5JumpResult.Moved) { }
        int before = changes;
        Check(s.JumpBackward() == Media5JumpResult.AtStart && changes == before, "lompat yang ditolak tidak memicu Changed");

        s.ResetAll();
        Check(s.Config.length == 1 && s.Config.partisi == 5, "reset: garis default");
        Check(!s.HasProblem && !s.IsSolved && s.PionTick == 0, "reset: tanpa soal, pion di 0");
        Check(s.IsPristine && changes > before, "reset: keadaan awal dan memicu Changed");
    }
}