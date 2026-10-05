using UnityEngine;

public class SessionSelfTest : MonoBehaviour
{
    static readonly Fraction One = Fraction.Whole(1);
    static Fraction U(long den) => Fraction.Unit(den);

    int failed;

    void Check(bool ok, string label)
    {
        if (ok) Debug.Log("ok: " + label);
        else { failed++; Debug.LogError("FAIL: " + label); }
    }

    bool DropN(FractionSession s, bool intoA, Fraction piece, int n)
    {
        for (int i = 0; i < n; i++)
            if (s.TryDropToKotak(intoA, piece) != RejectReason.None) return false;
        return true;
    }

    // bangun satu row n/den lewat merge (Kotak harus masih kosong)
    void BuildRow(FractionSession s, bool intoA, long den, int n)
    {
        s.TryDropToKotak(intoA, U(den));
        for (int i = 1; i < n; i++) s.TryDropToKotak(intoA, U(den), 0);
    }

    [ContextMenu("Run Session Tests")]
    void Run()
    {
        failed = 0;

        // 1. Add: jalur normal
        var s = new FractionSession(Operation.Add);
        Check(s.TryDropToKotak(true, One) == RejectReason.None, "A: drop 1");
        Check(s.TryDropToKotak(true, U(2)) == RejectReason.None, "A: drop 1/2 (row baru)");
        BuildRow(s, false, 4, 3);
        Check(s.B.Count == 1 && s.B.Pieces[0].numerator == 3 && s.B.Pieces[0].denominator == 4, "B: tiga kali 1/4 jadi satu row 3/4");
        Check(s.TryDropToDpz(U(2)) == RejectReason.SoalBelumDibentuk, "dpz ditolak sebelum + ditekan");
        Check(s.Check() == null, "Cek Hasil sebelum + -> null");
        Check(s.CommitProblem() == ProblemError.None && s.CurrentPhase == Phase.Merakit, "+ lolos -> Merakit");
        Check(s.Current.result.ValueEquals(new Fraction(9, 4)), "1 1/2 + 3/4 = 9/4");
        bool dpzOk = s.TryDropToDpz(One) == RejectReason.None && s.TryDropToDpz(One) == RejectReason.None
                  && s.TryDropToDpz(U(4)) == RejectReason.None;
        Check(dpzOk, "dpz: 2 utuh + 1/4 diterima");
        Check(s.Check() == CheckVerdict.Benar && s.CurrentPhase == Phase.Dicek, "dpz 2 1/4 -> Benar");
        s.TryDropToDpz(U(4));
        Check(s.CurrentPhase == Phase.Merakit, "ubah dpz setelah dicek -> balik Merakit");
        Check(s.Check() == CheckVerdict.Kelebihan, "2 2/4 vs 9/4 -> Kelebihan");
        s.ClearDpz();
        s.TryDropToDpz(U(4));
        Check(s.Check() == CheckVerdict.Kurang, "1/4 vs 9/4 -> Kurang");

        // 2. Kunci penyebut di dpz
        var s2 = new FractionSession(Operation.Add);
        s2.TryDropToKotak(true, U(2)); s2.TryDropToKotak(false, U(4)); s2.CommitProblem();
        Check(s2.TryDropToDpz(U(4)) == RejectReason.None, "dpz: 1/4 diterima (kunci penyebut 4)");
        Check(s2.TryDropToDpz(U(7)) == RejectReason.SatuanBeda, "dpz: 1/7 ditolak, satuan beda");
        Check(s2.TryDropToDpz(One) == RejectReason.None, "dpz: balok utuh tetap boleh");
        s2.ClearDpz();
        Check(s2.TryDropToDpz(U(7)) == RejectReason.None, "dpz kosong -> kunci lepas");

        // 3. Gate penyebut di tombol +
        var s3 = new FractionSession(Operation.Add);
        s3.TryDropToKotak(true, U(7)); s3.TryDropToKotak(false, U(4));
        Check(s3.CommitProblem() == ProblemError.PenyebutTerlaluRumit && s3.CurrentPhase == Phase.Menyusun,
              "1/7 + 1/4 (penyebut 28) ditolak di +");
        Check(new FractionSession(Operation.Add).CommitProblem() == ProblemError.KotakKosong, "+ dengan Kotak kosong ditolak");

        // 4. Subtract: hasil harus > 0
        var s4 = new FractionSession(Operation.Subtract);
        s4.TryDropToKotak(true, U(2)); BuildRow(s4, false, 4, 3);
        Check(s4.CommitProblem() == ProblemError.HasilTidakPositif, "1/2 - 3/4 ditolak (negatif)");
        var s4b = new FractionSession(Operation.Subtract);
        s4b.TryDropToKotak(true, U(2)); s4b.TryDropToKotak(false, U(2));
        Check(s4b.CommitProblem() == ProblemError.HasilTidakPositif, "1/2 - 1/2 = 0 ditolak");

        // 5. Subtract: jalur normal + nilai sama (3/6 == 1/2)
        var s5 = new FractionSession(Operation.Subtract);
        BuildRow(s5, true, 6, 4); s5.TryDropToKotak(false, U(6));
        Check(s5.CommitProblem() == ProblemError.None && s5.Current.result.ValueEquals(new Fraction(1, 2)), "4/6 - 1/6 = 1/2");
        s5.TryDropToDpz(U(2));
        Check(s5.Check() == CheckVerdict.Benar, "dpz satu balok 1/2 -> Benar");
        s5.ClearDpz();
        bool tiga = true;
        for (int i = 0; i < 3; i++) tiga &= s5.TryDropToDpz(U(6)) == RejectReason.None;
        Check(tiga && s5.Check() == CheckVerdict.Benar, "dpz tiga balok 1/6 -> Benar juga (nilai sama)");

        // 6. Add: cap hasil saat drop (7 ditolak, 6 1/2 diterima)
        var s6 = new FractionSession(Operation.Add);
        Check(DropN(s6, true, One, 6), "A: 6 utuh diterima");
        Check(s6.TryDropToKotak(false, One) == RejectReason.HasilTerlaluBesar, "B: +1 utuh -> hasil 7, ditolak");
        Check(s6.TryDropToKotak(false, U(2)) == RejectReason.None, "B: +1/2 -> 6 1/2, diterima");
        Check(s6.TryDropToKotak(false, U(2)) == RejectReason.HasilTerlaluBesar, "B: +1/2 lagi -> 7, ditolak");

        // 7. Subtract: cap per Kotak saat drop, cap hasil saat tombol -
        var s7 = new FractionSession(Operation.Subtract);
        Check(DropN(s7, true, One, 12), "Subtract: A boleh 12 utuh (belum kena cap hasil)");
        Check(s7.TryDropToKotak(true, One) == RejectReason.KotakTerlaluBesar, "A ke-13 ditolak (cap per Kotak)");
        Check(s7.CommitProblem() == ProblemError.KotakKosong, "B masih kosong");
        Check(DropN(s7, false, One, 5), "B: 5 utuh");
        Check(s7.CommitProblem() == ProblemError.HasilTerlaluBesar, "12 - 5 = 7 ditolak di -");
        var s7b = new FractionSession(Operation.Subtract);
        DropN(s7b, true, One, 10); DropN(s7b, false, One, 5);
        Check(s7b.CommitProblem() == ProblemError.None && s7b.Current.result.ValueEquals(Fraction.Whole(5)), "10 - 5 = 5 lolos");

        // 8. Edit Kotak setelah + -> dpz dihapus, balik Menyusun
        var s8 = new FractionSession(Operation.Add);
        s8.TryDropToKotak(true, U(2)); s8.TryDropToKotak(false, U(4)); s8.CommitProblem(); s8.TryDropToDpz(U(4));
        Check(s8.KotakEditClearsDpz, "setelah +, edit Kotak bakal hapus dpz");
        s8.TryDropToKotak(true, U(2));
        Check(s8.CurrentPhase == Phase.Menyusun && s8.Dpz.IsEmpty && s8.LastVerdict == null, "edit Kotak -> Menyusun, dpz kosong");

        // 9. Kapasitas dpz = 6 + 1
        var s9 = new FractionSession(Operation.Add);
        DropN(s9, true, One, 6); s9.TryDropToKotak(false, U(2)); s9.CommitProblem();
        bool tujuh = true;
        for (int i = 0; i < 7; i++) tujuh &= s9.TryDropToDpz(One) == RejectReason.None;
        Check(tujuh, "dpz: 7 utuh masih muat");
        Check(s9.TryDropToDpz(One) == RejectReason.DpzPenuh, "dpz: utuh ke-8 ditolak");

        // 10. Preview tidak mengubah state
        var s10 = new FractionSession(Operation.Add);
        Check(s10.PreviewDropToKotak(true, One) == RejectReason.None && s10.A.IsEmpty, "Preview: diterima, tapi A tetap kosong");
        DropN(s10, true, One, 6);
        Check(s10.PreviewDropToKotak(false, One) == RejectReason.HasilTerlaluBesar && s10.B.IsEmpty, "Preview: ditolak, B tetap kosong");
        Check(s10.PreviewDropToDpz(U(2)) == RejectReason.SoalBelumDibentuk, "Preview dpz sebelum +");
        var s11 = new FractionSession(Operation.Add);
        s11.TryDropToKotak(true, U(2)); s11.TryDropToKotak(false, U(4)); s11.CommitProblem();
        Check(s11.PreviewDropToDpz(U(4)) == RejectReason.None && s11.Dpz.IsEmpty, "Preview dpz: diterima, dpz tetap kosong");

        // 12. Row penuh: potongan berikutnya jadi row baru
        var s12 = new FractionSession(Operation.Add);
        BuildRow(s12, true, 4, 4);
        Check(s12.A.Count == 1 && s12.A.Pieces[0].numerator == 4, "A: satu row 4/4");
        Check(s12.TryDropToKotak(true, U(4), 0) == RejectReason.None, "drop 1/4 di atas row penuh diterima");
        Check(s12.A.Count == 2 && s12.A.Pieces[0].numerator == 4 && s12.A.Pieces[1].numerator == 1, "jadi row baru [4/4, 1/4]");
        Check(!s12.A.CanMerge(0, U(4)) && s12.A.CanMerge(1, U(4)), "row 4/4 penuh, row 1/4 masih bisa merge");

        // 13. Utuh selalu satu row sendiri
        var s13 = new FractionSession(Operation.Add);
        s13.TryDropToKotak(true, One); s13.TryDropToKotak(true, One, 0);
        Check(s13.A.Count == 2, "dua utuh jadi dua row");

        Debug.Log(failed == 0 ? "SEMUA LOLOS (session)" : failed + " tes GAGAL (session)");
    }
}