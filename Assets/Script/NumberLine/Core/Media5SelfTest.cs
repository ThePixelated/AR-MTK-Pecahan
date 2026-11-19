using System.Collections.Generic;
using UnityEngine;

// Tes logika inti Media 5 (tanpa scene): soal, generator, dan Media5Session (Explore, Locked, Enter Soal, Cek Hasil). Klik kanan komponen > "Run Media5 Tests"
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
        TestExploreState();
        TestLockedFlow();
        TestEnterProblem();
        TestCheck();
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

    static NumberLineSoalInput Cells(int? whole, int? numerator, int? denominator)
    {
        var input = new NumberLineSoalInput();
        input.SetValue(whole, numerator, denominator);
        return input;
    }

    void TestExploreState()
    {
        var s = new Media5Session(null, 1);
        Check(s.Config.length == 1 && s.Config.partisi == 5, "default panjang 1 partisi 5");
        Check(!s.HasPion && !s.IsLocked && !s.HasProblem && s.IsPristine && !s.NeedsConfirmBeforeReconfigure, "awal: Explore, belum ada hewan");
        Check(s.JumpBackward() == Media5JumpResult.NoProblem, "mundur tanpa soal ditolak");
        Check(s.RetryPosition() == Media5RetryResult.NoProblem, "Ulangi tanpa soal ditolak");
        Check(s.Check(null).Outcome == Media5CheckOutcome.NoProblem, "Cek Hasil tanpa soal ditolak");
        Check(!s.RemovePion(), "Tarik Kembali tanpa hewan ditolak");

        Check(s.PlacePion(3) && s.HasPion && s.PionTick == 3 && !s.IsPristine, "hewan ditaruh di tick 3");
        Check(s.PlacePion(99) && s.PionTick == s.Config.MaxTick, "tick terlalu besar dibatasi ke ujung kanan");
        Check(s.PlacePion(-4) && s.PionTick == 0 && s.HasPion, "tick negatif dibatasi ke 0");

        int changes = 0;
        s.Changed += () => changes++;
        s.PlacePion(0);
        Check(changes == 0, "tick sama: tanpa event");
        s.PlacePion(2);
        Check(changes == 1 && s.PionTick == 2, "digeser: satu event");
        Check(s.RemovePion() && !s.HasPion && s.PionTick == 0 && changes == 2, "Tarik Kembali: hewan keluar dari garis");
        Check(!s.RemovePion(), "Tarik Kembali dua kali ditolak");
    }

    void TestLockedFlow()
    {
        var s = new Media5Session(null, 1);
        s.ApplyConfig(1, 2);
        Check(!s.HasPion, "garis diubah: hewan tetap belum ada");

        Check(s.TryNewProblem() == Media5ProblemError.None && s.HasProblem && s.IsLocked, "Generate Soal mengunci");
        Check(s.HasPion, "hewan muncul otomatis walau tadi belum ada");

        NumberLineProblem p = s.Problem.Value;
        Check(p.operation == NumberLineOperation.Subtract && p.aTick == 2 && p.bTick == 1 && s.ResultTick == 1, "satu-satunya soal: 2 - 1 = 1");
        Check(s.PionTick == 2 && s.StartTick == 2, "hewan pindah ke A");
        Check(!s.IsSolved && s.NeedsConfirmBeforeReconfigure && !s.IsPristine, "soal belum terjawab: perlu konfirmasi");

        Check(!s.PlacePion(1) && s.PionTick == 2, "Locked: hewan tidak bisa digeser");
        Check(!s.RemovePion() && s.HasPion, "Locked: hewan tidak bisa ditarik kembali");

        Check(s.JumpBackward() == Media5JumpResult.Moved && s.PionTick == 1, "mundur 1 tick");
        Check(s.JumpBackward() == Media5JumpResult.Moved && s.PionTick == 0, "mundur ke 0");
        Check(s.JumpBackward() == Media5JumpResult.AtStart && s.PionTick == 0, "di titik 0: tidak bisa mundur");

        Check(s.RetryPosition() == Media5RetryResult.Moved && s.PionTick == 2, "Ulangi: hewan kembali ke A");
        Check(s.RetryPosition() == Media5RetryResult.AlreadyAtStart, "Ulangi saat sudah di A: tidak ada perubahan");

        Check(s.ClearProblem() && !s.IsLocked && !s.HasProblem && !s.IsSolved, "Clear Soal: kembali ke Explore");
        Check(s.HasPion && s.PionTick == 0, "Clear Soal: hewan tetap ada, di titik 0");
        Check(!s.ClearProblem(), "Clear Soal tanpa soal: tidak ada perubahan");
        Check(s.PlacePion(1) && s.PionTick == 1, "setelah Clear Soal hewan bisa digeser lagi");

        // hewan sudah ada di tempat lain saat Generate: pindah ke A
        Check(s.TryNewProblem() == Media5ProblemError.None && s.PionTick == 2, "Generate dengan hewan sudah di garis: pindah ke A");
    }

    void TestEnterProblem()
    {
        // garis 1 x 5: MaxTick 5
        var s = new Media5Session(null, 1);
        int changes = 0;
        s.Changed += () => changes++;

        Check(s.TryEnterProblem(Cells(null, null, null), Cells(null, 1, 5)) == Media5EnterError.AMissing, "A kosong");
        Check(s.TryEnterProblem(Cells(null, 4, 5), Cells(null, null, null)) == Media5EnterError.BMissing, "B kosong");
        Check(s.TryEnterProblem(null, Cells(null, 1, 5)) == Media5EnterError.AMissing, "A null dianggap kosong");
        Check(s.TryEnterProblem(Cells(null, 1, null), Cells(null, 1, 5)) == Media5EnterError.AIncomplete, "A: pembilang saja");
        Check(s.TryEnterProblem(Cells(null, 4, 5), Cells(null, null, 5)) == Media5EnterError.BIncomplete, "B: penyebut saja");
        Check(s.TryEnterProblem(Cells(null, 1, 0), Cells(null, 1, 5)) == Media5EnterError.AImpossible, "A: penyebut 0");
        Check(s.TryEnterProblem(Cells(null, 4, 5), Cells(null, 1, 0)) == Media5EnterError.BImpossible, "B: penyebut 0");
        Check(s.TryEnterProblem(Cells(null, 1, 2), Cells(null, 1, 5)) == Media5EnterError.ANotOnTick, "A = 1/2 tidak tepat di tick partisi 5");
        Check(s.TryEnterProblem(Cells(null, 4, 5), Cells(null, 1, 2)) == Media5EnterError.BNotOnTick, "B = 1/2 tidak tepat di tick partisi 5");
        Check(s.TryEnterProblem(Cells(2, null, null), Cells(null, 1, 5)) == Media5EnterError.AExceedsLine, "A = 2 melewati garis panjang 1");
        Check(s.TryEnterProblem(Cells(null, 4, 5), Cells(2, null, null)) == Media5EnterError.BExceedsLine, "B = 2 melewati garis panjang 1");
        Check(s.TryEnterProblem(Cells(null, 4, 5), Cells(0, null, null)) == Media5EnterError.BZero, "B = 0");
        Check(s.TryEnterProblem(Cells(null, 1, 5), Cells(null, 4, 5)) == Media5EnterError.AMustBeGreater, "A lebih kecil dari B");
        Check(s.TryEnterProblem(Cells(null, 3, 5), Cells(null, 3, 5)) == Media5EnterError.AMustBeGreater, "A sama dengan B");

        Check(!s.HasProblem && !s.HasPion && changes == 0, "gagal: tidak ada yang berubah dan tanpa event");

        // berhasil, dengan hewan sudah ada di tempat lain
        s.PlacePion(1);
        changes = 0;
        Check(s.TryEnterProblem(Cells(null, 4, 5), Cells(null, 1, 5)) == Media5EnterError.None, "4/5 - 1/5 sah");
        Check(s.IsLocked && s.ResultTick == 3 && s.PionTick == 4 && s.StartTick == 4, "terkunci, hewan pindah ke A, hasil tick 3");
        Check(changes == 1, "berhasil: satu event");

        // pecahan ekuivalen diterima: 8/10 pada partisi 5 = tick 4
        var e = new Media5Session(null, 1);
        Check(e.TryEnterProblem(Cells(null, 8, 10), Cells(null, 2, 10)) == Media5EnterError.None && e.Problem.Value.aTick == 4 && e.Problem.Value.bTick == 1, "8/10 - 2/10 = tick 4 dan 1");

        // bilangan campuran pada garis 2 x 5: 1 3/5 - 1 = 3/5
        var m = new Media5Session(null, 1);
        m.ApplyConfig(2, 5);
        Check(m.TryEnterProblem(Cells(1, 3, 5), Cells(1, null, null)) == Media5EnterError.None, "1 3/5 - 1 sah");
        Check(m.Problem.Value.aTick == 8 && m.Problem.Value.bTick == 5 && m.ResultTick == 3, "bilangan campuran: tick 8 - 5 = 3");

        // A tepat di ujung garis
        var edge = new Media5Session(null, 1);
        Check(edge.TryEnterProblem(Cells(1, null, null), Cells(null, 2, 5)) == Media5EnterError.None && edge.Problem.Value.aTick == 5, "A tepat di ujung garis sah");
    }

    void TestCheck()
    {
        // garis 1 x 5, soal 4/5 - 1/5, hasil tick 3, hewan mulai di tick 4
        var s = new Media5Session(null, 1);
        s.TryEnterProblem(Cells(null, 4, 5), Cells(null, 1, 5));
        NumberLineSoalInput answerOk = Cells(null, 3, 5);

        Media5CheckResult r = s.Check(answerOk);
        Check(r.Outcome == Media5CheckOutcome.WrongPosition && r.AnswerState == Media5AnswerState.Correct && !s.IsSolved, "jawaban benar, posisi salah");

        s.JumpBackward();
        Check(s.PionTick == 3, "hewan di tick hasil");

        r = s.Check(Cells(null, 2, 5));
        Check(r.Outcome == Media5CheckOutcome.WrongAnswer && r.AnswerState == Media5AnswerState.Wrong, "posisi benar, jawaban salah");
        r = s.Check(Cells(null, null, null));
        Check(r.Outcome == Media5CheckOutcome.AnswerEmpty && r.AnswerState == Media5AnswerState.Empty, "posisi benar, jawaban kosong");
        r = s.Check(null);
        Check(r.Outcome == Media5CheckOutcome.AnswerEmpty, "posisi benar, jawaban null dianggap kosong");
        r = s.Check(Cells(null, 3, null));
        Check(r.Outcome == Media5CheckOutcome.AnswerIncomplete && r.AnswerState == Media5AnswerState.Incomplete, "posisi benar, jawaban belum lengkap");
        r = s.Check(Cells(null, 3, 0));
        Check(r.Outcome == Media5CheckOutcome.AnswerIncomplete, "posisi benar, penyebut 0 dianggap belum lengkap");
        r = s.Check(Cells(7, null, null));
        Check(r.Outcome == Media5CheckOutcome.WrongAnswer, "jawaban melewati garis: salah");
        Check(!s.IsSolved, "belum ada yang benar semua");

        s.JumpBackward();
        r = s.Check(Cells(null, 2, 5));
        Check(r.Outcome == Media5CheckOutcome.WrongBoth && r.AnswerState == Media5AnswerState.Wrong, "posisi salah dan jawaban salah");
        r = s.Check(null);
        Check(r.Outcome == Media5CheckOutcome.WrongBoth, "posisi salah dan jawaban kosong");

        Check(s.RetryPosition() == Media5RetryResult.Moved && s.PionTick == 4, "Ulangi ke A");
        s.JumpBackward();

        int changes = 0;
        s.Changed += () => changes++;
        r = s.Check(Cells(null, 6, 10));
        Check(r.Outcome == Media5CheckOutcome.Correct && r.AnswerState == Media5AnswerState.Correct, "6/10 setara 3/5: benar");
        Check(s.IsSolved && !s.NeedsConfirmBeforeReconfigure && changes == 1, "terjawab: satu event, tidak perlu konfirmasi");
        Check(s.Check(answerOk).Outcome == Media5CheckOutcome.Correct && changes == 1, "Cek ulang tetap benar tanpa event baru");
        Check(s.Check(Cells(0, 3, 5)).Outcome == Media5CheckOutcome.Correct, "0 3/5 juga benar");

        s.JumpBackward();
        Check(s.Check(answerOk).Outcome == Media5CheckOutcome.WrongPosition && s.IsSolved, "posisi bergeser: salah, tapi status terjawab tetap");
    }

    void TestSessionOptions()
    {
        var minTwo = new Media5Session(new Media5Limits { MinPartisiForProblem = 2 }, 1);
        Check(minTwo.ApplyConfig(2, 1) == NumberLineConfigError.None, "garis 2 x 1");
        Check(minTwo.TryNewProblem() == Media5ProblemError.PartisiTooSmall && !minTwo.HasProblem && !minTwo.HasPion, "partisi 1 ditolak saat minimal 2");
        minTwo.ApplyConfig(1, 2);
        Check(minTwo.TryNewProblem() == Media5ProblemError.None, "partisi 2 diterima");

        var normal = new Media5Session(null, 1);
        normal.ApplyConfig(1, 1);
        Check(normal.TryNewProblem() == Media5ProblemError.NoProblemPossible && !normal.HasPion, "garis 1 x 1 tidak punya soal dan tidak memunculkan hewan");
        normal.ApplyConfig(2, 1);
        Check(normal.TryNewProblem() == Media5ProblemError.None, "garis 2 x 1 (partisi 1 dibolehkan) punya soal");
        NumberLineProblem np = normal.Problem.Value;
        Check(np.aTick == 2 && np.bTick == 1, "garis 2 x 1: soal 2 - 1");

        var addOnly = new Media5Session(new Media5Limits { Operations = Media5OperationMode.AddOnly }, 1);
        addOnly.ApplyConfig(1, 2);
        Check(addOnly.TryNewProblem() == Media5ProblemError.None, "buat soal penjumlahan");
        NumberLineProblem ap = addOnly.Problem.Value;
        Check(ap.operation == NumberLineOperation.Add && ap.aTick == 1 && ap.bTick == 1 && addOnly.ResultTick == 2, "satu-satunya soal: 1 + 1 = 2");
        Check(addOnly.PionTick == 1 && addOnly.HasPion, "penjumlahan: hewan mulai di A");
    }

    void TestSessionReconfigAndReset()
    {
        var s = new Media5Session(null, 2);
        Check(s.Validate(0, 5) == NumberLineConfigError.LengthOutOfRange, "panjang 0 ditolak");
        Check(s.Validate(6, 5) == NumberLineConfigError.LengthOutOfRange, "panjang 6 ditolak");
        Check(s.Validate(1, 0) == NumberLineConfigError.PartisiOutOfRange, "partisi 0 ditolak");
        Check(s.Validate(1, 16) == NumberLineConfigError.PartisiOutOfRange, "partisi 16 ditolak");

        // Explore: ganti garis memindahkan hewan ke 0 tanpa menghapusnya
        s.PlacePion(3);
        Check(s.ApplyConfig(2, 3) == NumberLineConfigError.None && s.HasPion && s.PionTick == 0 && !s.HasProblem, "ganti garis saat Explore: hewan ke 0");

        s.ApplyConfig(1, 3);
        s.TryNewProblem();
        int changes = 0;
        s.Changed += () => changes++;

        Check(s.NeedsConfirmBeforeReconfigure, "soal belum terjawab: perlu konfirmasi");
        Check(s.ApplyConfig(1, 3) == NumberLineConfigError.None && changes == 0 && s.HasProblem, "garis sama: soal dan hewan tetap, tanpa event");
        Check(s.ApplyConfig(6, 3) == NumberLineConfigError.LengthOutOfRange && s.Config.length == 1, "konfigurasi tidak valid tidak diterapkan");

        Check(s.ApplyConfig(2, 3) == NumberLineConfigError.None, "ubah garis");
        Check(!s.HasProblem && !s.IsLocked && s.HasPion && s.PionTick == 0 && s.Config.length == 2 && changes == 1, "garis baru: soal dibuang, hewan tetap ada di 0");

        s.TryNewProblem();
        Check(changes == 2, "buat soal memicu Changed");
        s.JumpBackward();
        Check(changes == 3, "mundur memicu Changed");

        while (s.JumpBackward() == Media5JumpResult.Moved) { }
        int before = changes;
        Check(s.JumpBackward() == Media5JumpResult.AtStart && changes == before, "mundur yang ditolak tidak memicu Changed");

        s.ResetAll();
        Check(s.Config.length == 1 && s.Config.partisi == 5, "reset: garis default");
        Check(!s.HasProblem && !s.IsSolved && !s.HasPion && s.PionTick == 0, "reset: tanpa soal, tanpa hewan");
        Check(s.IsPristine && changes > before, "reset: keadaan awal dan memicu Changed");
    }
}