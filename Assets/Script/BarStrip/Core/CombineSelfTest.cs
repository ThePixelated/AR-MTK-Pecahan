using UnityEngine;

public class CombineSelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run Combine Tests")]
    public void Run()
    {
        pass = fail = 0;
        TestSeparate();
        TestCombineFlow();
        TestUncombineAndLock();
        TestCheckFullAdd();
        TestCheckFullWholeResult();
        TestCheckFullSubtract();
        TestLegacyCheck();
        string summary = $"Combine tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static Fraction F(long n, long d) => new Fraction(n, d);

    // Soal terbentuk: A dan B terisi, tombol +/- ditekan, kedua row ditarik ke area atas (belum dijumlahkan)
    static FractionSession Prepared(Operation op, Fraction a, Fraction b, SessionOptions options = null, bool moveBoth = true)
    {
        var s = new FractionSession(op, options);
        s.TryDropToKotak(true, a);
        s.TryDropToKotak(false, b);
        s.CommitProblem();
        if (moveBoth)
        {
            s.TryMoveToArea(true, 0);
            s.TryMoveToArea(false, 0);
        }
        return s;
    }

    void TestSeparate()
    {
        var blocks = new System.Collections.Generic.List<AreaBlock>
        {
            new AreaBlock(F(1, 2), true, 0),
            new AreaBlock(F(2, 2), true, 1),
            new AreaBlock(F(3, 4), false, 0)
        };
        var sep = AreaComposer.Separate(blocks);
        Check(sep.Count == 3, "Separate: satu balok per row");
        Check(sep[0].kind == ComposedKind.Fraction && sep[1].kind == ComposedKind.Whole && sep[2].kind == ComposedKind.Fraction, "Separate: row penuh jadi bilangan bulat, lainnya pecahan");
        Check(sep[0].sources.Length == 1 && sep[0].sources[0] == 0 && sep[2].sources[0] == 2, "Separate: tiap balok hanya berasal dari row-nya");
        Check(sep[2].cellSource.Length == 3 && sep[2].piece.numerator == 3 && sep[2].piece.denominator == 4, "Separate: balok 3/4 punya 3 sel");
        Check(AreaComposer.Separate(new System.Collections.Generic.List<AreaBlock>()).Count == 0, "Separate: area kosong");
    }

    void TestCombineFlow()
    {
        Check(!new SessionOptions().LockAfterCombine, "Lock After Combine mati secara bawaan");

        var s = new FractionSession(Operation.Add);
        s.TryDropToKotak(true, F(1, 2));
        s.TryDropToKotak(false, F(3, 4));
        Check(s.PreviewCombine() == CombineResult.SoalBelumDibentuk && s.CombineUpper() == CombineResult.SoalBelumDibentuk && !s.Combined, "sebelum + ditekan: tidak bisa dijumlahkan");

        s.CommitProblem();
        Check(s.CurrentPhase == Phase.Merakit && s.Composed.Count == 0, "+ hanya membentuk soal: area atas kosong");
        Check(s.PreviewCombine() == CombineResult.PerluKeduaKotak, "area atas kosong: perlu balok dari kedua Kotak");

        int views = 0, states = 0;
        s.UpperViewChanged += () => views++;
        s.StateChanged += () => states++;

        s.TryMoveToArea(true, 0);
        Check(views == 1 && s.Composed.Count == 1 && s.PreviewCombine() == CombineResult.PerluKeduaKotak, "baru Kotak A di area: belum bisa dijumlahkan");
        s.TryMoveToArea(false, 0);
        Check(views == 2 && s.Composed.Count == 2, "dua balok masih terpisah (tidak langsung dijumlahkan)");
        Check(s.Composed[0].kind == ComposedKind.Fraction && s.Composed[1].kind == ComposedKind.Fraction, "balok terpisah berupa 1/2 dan 3/4");
        Check(!s.Combined && s.PreviewCombine() == CombineResult.Berhasil, "kedua Kotak terwakili: siap dijumlahkan");

        int statesBefore = states;
        Check(s.CombineUpper() == CombineResult.Berhasil && s.Combined, "Jumlahkan berhasil");
        Check(views == 3 && states == statesBefore + 1, "Jumlahkan memicu tampilan area atas dan perubahan state");
        Check(s.Composed.Count == 2 && s.Composed[0].kind == ComposedKind.Carry && s.Composed[1].kind == ComposedKind.Fraction, "setelah dijumlahkan: satu carry dan satu sisa");
        Check(s.Composed[1].piece.numerator == 1 && s.Composed[1].piece.denominator == 4, "sisa 1/4 (1/2 + 3/4 = 1 1/4)");
        Check(s.CombineUpper() == CombineResult.SudahDijumlahkan && views == 3, "Jumlahkan lagi: sudah dijumlahkan, tanpa perubahan");
        Check(s.CurrentPhase == Phase.Merakit, "Jumlahkan tidak mengubah fase");

        // semua row dikembalikan lewat ketuk balok gabungan (sumbernya semua)
        Check(s.ReturnComposed(0) && s.Upper.Count == 0 && !s.Combined && s.A.Count == 1 && s.B.Count == 1, "ketuk balok gabungan mengembalikan semua row ke Kotak");
    }

    void TestUncombineAndLock()
    {
        // Lock mati: ubah isi area = gabungan batal
        var s = Prepared(Operation.Add, F(1, 2), F(3, 4));
        s.CombineUpper();
        int views = 0;
        s.UpperViewChanged += () => views++;
        Check(!s.IsCombineLocked && !s.IsKotakLocked(false) == false, "Lock mati: Kotak terkunci hanya karena balok masih di area (mode pindah)");
        Check(s.ReturnToKotak(1) && !s.Combined && views >= 1, "Lock mati: balok bisa dikembalikan, gabungan batal");
        Check(s.Composed.Count == 1 && s.Composed[0].kind == ComposedKind.Fraction, "setelah batal: balok sisa tampil terpisah");
        s.TryMoveToArea(false, 0);
        Check(!s.Combined && s.PreviewCombine() == CombineResult.Berhasil, "menarik balok lagi: perlu Jumlahkan ulang");

        // Lock menyala
        var opt = new SessionOptions { LockAfterCombine = true };
        var t = Prepared(Operation.Add, F(1, 2), F(3, 4), opt);
        Check(!t.IsCombineLocked, "Lock menyala tapi belum dijumlahkan: belum terkunci");
        Check(t.ReturnToKotak(0) && t.Upper.Count == 1, "sebelum Jumlahkan: balok masih bisa dikembalikan");
        t.TryMoveToArea(true, 0);
        t.CombineUpper();

        Check(t.IsCombineLocked && t.IsKotakLocked(true) && t.IsKotakLocked(false), "Lock menyala: setelah dijumlahkan, kedua Kotak terkunci");
        Check(t.KotakLockReason(true) == RejectReason.AreaTerkunci, "alasan: area terkunci, bukan 'kembalikan dulu'");
        Check(t.PreviewMoveToArea(true, 0) == RejectReason.AreaTerkunci, "balok baru tidak boleh masuk area");
        Check(!t.ReturnComposed(0) && !t.ReturnToKotak(0) && t.Upper.Count == 2 && t.Combined, "balok tidak bisa dikembalikan");
        t.ReturnAllToKotak();
        Check(t.Upper.Count == 2 && t.Combined, "kembalikan semua: diabaikan saat terkunci");
        Check(t.PreviewDropToKotak(true, F(1, 3)) == RejectReason.AreaTerkunci, "menjatuhkan pecahan ke Kotak ditolak (terkunci)");
        Check(t.PreviewDropToDpz(F(1, 4)) == RejectReason.None, "area bawah tetap bisa diisi (mencari sisa)");

        int resetViews = 0;
        t.UpperViewChanged += () => resetViews++;
        t.ResetAll();
        Check(!t.Combined && !t.IsCombineLocked && t.CurrentPhase == Phase.Menyusun && t.Upper.Count == 0, "atur ulang membuka kunci dan mengosongkan semuanya");
        Check(t.A.IsEmpty && t.B.IsEmpty && resetViews >= 1, "atur ulang: Kotak kosong dan area digambar ulang");
        Check(t.PreviewDropToKotak(true, F(1, 3)) == RejectReason.None, "setelah atur ulang: Kotak bisa diisi lagi");

        // ubah Kotak setelah soal dibentuk membatalkan gabungan juga
        var u = Prepared(Operation.Add, F(1, 2), F(3, 4), null, false);
        u.TryMoveToArea(true, 0);
        u.TryMoveToArea(false, 0);
        u.CombineUpper();
        u.ReturnAllToKotak();
        Check(u.TryDropToKotak(true, F(1, 2)) == RejectReason.None, "menambah 1/2 ke Kotak A (satu jenis pecahan, menyatu jadi 2/2)");
        Check(!u.Combined && u.CurrentPhase == Phase.Menyusun && u.Upper.Count == 0, "mengubah Kotak: soal dan gabungan dibatalkan");
    }

    void TestCheckFullAdd()
    {
        var s = Prepared(Operation.Add, F(1, 2), F(3, 4));       // hasil 1 1/4, sisa 1/4
        Check(s.Current.result.ValueEquals(F(5, 4)), "soal: 1/2 + 3/4 = 5/4");

        Check(s.CheckFull(null).outcome == FullCheckOutcome.BelumDijumlahkan, "sebelum Jumlahkan: ditolak");
        Check(s.CheckFull(F(5, 4)).outcome == FullCheckOutcome.BelumDijumlahkan && s.CurrentPhase == Phase.Merakit, "sebelum Jumlahkan: jawaban pun belum dinilai, fase tidak berubah");

        s.CombineUpper();
        Check(s.CheckFull(null).outcome == FullCheckOutcome.KeduanyaKosong, "balok bawah dan jawaban kosong");
        Check(s.CheckFull(F(5, 4)).outcome == FullCheckOutcome.BalokKosong, "balok bawah kosong, jawaban terisi");
        Check(s.CurrentPhase == Phase.Merakit, "penolakan karena kosong tidak mengubah fase");

        s.TryDropToDpz(F(1, 4));
        Check(s.CheckFull(null).outcome == FullCheckOutcome.JawabanKosong, "balok ada, jawaban kosong");

        FullCheckResult ok = s.CheckFull(F(5, 4));
        Check(ok.outcome == FullCheckOutcome.Benar && ok.blockVerdict == CheckVerdict.Benar && ok.Evaluated, "balok 1/4 dan jawaban 5/4: benar");
        Check(s.CurrentPhase == Phase.Dicek && s.LastVerdict == CheckVerdict.Benar, "fase Dicek setelah dinilai");
        Check(s.CheckFull(F(10, 8)).outcome == FullCheckOutcome.Benar, "jawaban senilai (10/8) benar");
        Check(s.CheckFull(F(1 * 4 + 1, 4)).outcome == FullCheckOutcome.Benar, "jawaban 1 1/4 (bilangan campuran) benar");

        FullCheckResult wrongAnswer = s.CheckFull(F(3, 2));
        Check(wrongAnswer.outcome == FullCheckOutcome.JawabanSalah && wrongAnswer.blockVerdict == CheckVerdict.Benar, "balok benar, jawaban 3/2 salah");

        s.RemoveFromDpz(0);
        Check(s.CurrentPhase == Phase.Merakit, "area diubah setelah dicek: fase kembali Merakit");
        s.TryDropToDpz(F(1, 2));
        FullCheckResult wrongBlocks = s.CheckFull(F(5, 4));
        Check(wrongBlocks.outcome == FullCheckOutcome.BalokSalah && wrongBlocks.blockVerdict == CheckVerdict.Kelebihan, "jawaban benar, balok 1/2 kelebihan");
        Check(s.CheckFull(F(3, 2)).outcome == FullCheckOutcome.KeduanyaSalah, "balok dan jawaban salah");

        s.RemoveFromDpz(0);
        s.TryDropToDpz(F(1, 8));
        FullCheckResult kurang = s.CheckFull(F(5, 4));
        Check(kurang.outcome == FullCheckOutcome.BalokSalah && kurang.blockVerdict == CheckVerdict.Kurang, "balok 1/8 kurang");

        // urutan boleh bebas: jawaban terisi duluan lalu balok, atau sebaliknya, hasilnya sama
        var f = Prepared(Operation.Add, F(1, 2), F(3, 4));
        f.CombineUpper();
        Check(f.CheckFull(F(5, 4)).outcome == FullCheckOutcome.BalokKosong, "jawaban lebih dulu: balok masih kosong");
        f.TryDropToDpz(F(1, 4));
        Check(f.CheckFull(F(5, 4)).outcome == FullCheckOutcome.Benar, "lalu balok disusun: benar");
    }

    void TestCheckFullWholeResult()
    {
        // 1/2 + 1/2 = 1: sisa pecahan 0, jadi area bawah kosong adalah susunan yang benar
        var s = Prepared(Operation.Add, F(1, 2), F(1, 2));
        Check(s.Current.result.ValueEquals(F(1, 1)), "soal: 1/2 + 1/2 = 1");
        s.CombineUpper();
        Check(s.Composed.Count == 1 && s.Composed[0].kind == ComposedKind.Carry, "dua setengah menjadi satu carry");

        Check(s.CheckFull(null).outcome == FullCheckOutcome.JawabanKosong, "hasil bulat: area bawah kosong sudah benar, tinggal jawaban");
        FullCheckResult ok = s.CheckFull(F(1, 1));
        Check(ok.outcome == FullCheckOutcome.Benar, "area bawah kosong dan jawaban 1: benar");
        Check(s.CheckFull(F(2, 2)).outcome == FullCheckOutcome.Benar, "jawaban 2/2 senilai 1: benar");
        Check(s.CheckFull(F(3, 2)).outcome == FullCheckOutcome.JawabanSalah, "jawaban 3/2 salah");

        s.TryDropToDpz(F(1, 4));
        FullCheckResult extra = s.CheckFull(F(1, 1));
        Check(extra.outcome == FullCheckOutcome.BalokSalah && extra.blockVerdict == CheckVerdict.Kelebihan, "ada balok sisa padahal hasilnya bulat: balok kelebihan");
    }

    void TestCheckFullSubtract()
    {
        var s = Prepared(Operation.Subtract, F(3, 4), F(1, 3));     // 3/4 - 1/3 = 5/12
        Check(s.Current.result.ValueEquals(F(5, 12)), "soal: 3/4 - 1/3 = 5/12");
        Check(s.Composed.Count == 2 && s.Composed[1].sources[0] == 1, "sebelum Jumlahkan: dua balok terpisah (A dan B)");
        Check(s.CheckFull(null).outcome == FullCheckOutcome.BelumDijumlahkan, "pengurangan: sebelum Jumlahkan ditolak");

        s.CombineUpper();
        Check(s.Composed.Count == 1 && s.Composed[0].kind == ComposedKind.Fraction && s.Composed[0].piece.numerator == 5 && s.Composed[0].piece.denominator == 12, "setelah Jumlahkan: satu balok bersih 5/12");

        s.TryDropToDpz(F(7, 12));      // yang dicari di area bawah adalah pelengkap (1 - 5/12)
        Check(s.CheckFull(F(5, 12)).outcome == FullCheckOutcome.Benar, "balok pelengkap 7/12 dan jawaban 5/12: benar");
        Check(s.CheckFull(F(7, 12)).outcome == FullCheckOutcome.JawabanSalah, "jawaban 7/12 (pelengkap tertukar) salah");
        s.RemoveFromDpz(0);
        s.TryDropToDpz(F(5, 12));
        Check(s.CheckFull(F(5, 12)).outcome == FullCheckOutcome.BalokSalah, "balok 5/12 (bukan pelengkap) salah walau jawaban benar");
    }

    void TestLegacyCheck()
    {
        var s = new FractionSession(Operation.Add);
        Check(s.Check() == null, "Check lama: soal belum dibentuk = null");
        s.TryDropToKotak(true, F(1, 2));
        s.TryDropToKotak(false, F(3, 4));
        s.CommitProblem();
        s.TryDropToDpz(F(1, 4));
        Check(s.Check() == CheckVerdict.Benar && s.CurrentPhase == Phase.Dicek, "Check lama tetap menilai balok saja");
    }
}
