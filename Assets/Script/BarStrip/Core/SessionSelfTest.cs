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

        // 2. dpz: satuan bebas saat menyusun (tidak ada kunci satuan)
        var s2 = new FractionSession(Operation.Add);
        s2.TryDropToKotak(true, U(2)); s2.TryDropToKotak(false, U(4)); s2.CommitProblem();
        Check(s2.TryDropToDpz(U(4)) == RejectReason.None, "dpz: 1/4 diterima");
        Check(s2.TryDropToDpz(U(7)) == RejectReason.None, "dpz: 1/7 juga diterima (tidak ada kunci satuan)");
        Check(s2.TryDropToDpz(One) == RejectReason.None, "dpz: balok utuh diterima");
        Check(s2.Dpz.Count == 3, "tiga blok terpisah (penyebut beda tidak merge)");

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

        // 14. dpz: merge di zona luar, blok baru kalau penuh
        var s14 = new FractionSession(Operation.Add);
        s14.TryDropToKotak(true, One); s14.TryDropToKotak(false, U(4)); s14.CommitProblem();   // 1 + 1/4
        s14.TryDropToDpz(U(4)); s14.TryDropToDpz(U(4));
        Check(s14.Dpz.Count == 1 && s14.Dpz.Pieces[0].numerator == 2, "zona luar: 1/4 + 1/4 merge jadi 2/4");
        s14.TryDropToDpz(U(4)); s14.TryDropToDpz(U(4));
        Check(s14.Dpz.Count == 1 && s14.Dpz.Pieces[0].numerator == 4, "merge sampai 4/4");
        s14.TryDropToDpz(U(4));
        Check(s14.Dpz.Count == 2 && s14.Dpz.Pieces[1].numerator == 1, "4/4 penuh: 1/4 berikutnya jadi blok baru");

        // 15. dpz: drop persis di atas blok
        var s15 = new FractionSession(Operation.Add);
        s15.TryDropToKotak(true, U(2)); s15.TryDropToKotak(false, U(4)); s15.CommitProblem();
        s15.TryDropToDpz(U(4));        // blok 0: 1/4
        s15.TryDropToDpz(U(3));        // blok 1: 1/3
        s15.TryDropToDpz(U(3), 0);     // persis di atas 1/4 tapi penyebut beda -> blok baru
        Check(s15.Dpz.Count == 3 && s15.Dpz.Pieces[2].denominator == 3, "di atas blok beda satuan: jadi blok baru");
        s15.TryDropToDpz(U(4), 0);     // persis di atas 1/4 -> merge
        Check(s15.Dpz.Count == 3 && s15.Dpz.Pieces[0].numerator == 2, "di atas blok sejenis: merge");
        Check(s15.FindDpzMergeTarget(U(3)) == 1, "zona luar: blok 1/3 pertama yang belum penuh menerima 1/3");

        // 16. Nilai pas tapi satuan campur
        var s16 = new FractionSession(Operation.Add);
        s16.TryDropToKotak(true, U(2)); s16.TryDropToKotak(false, U(4)); s16.CommitProblem();   // 3/4
        s16.TryDropToDpz(U(2)); s16.TryDropToDpz(U(4));
        Check(s16.Check() == CheckVerdict.NilaiPasSatuanBeda, "1/2 + 1/4 = 3/4 tapi satuan campur");
        Check(s16.CurrentPhase == Phase.Dicek && s16.LastVerdict != CheckVerdict.Benar, "belum Benar, hasil di Soal tetap ?");
        s16.ClearDpz();
        for (int i = 0; i < 3; i++) s16.TryDropToDpz(U(4));
        Check(s16.Dpz.Count == 1 && s16.Check() == CheckVerdict.Benar, "3 x 1/4 (satu blok 3/4) -> Benar");

        // 17. Batas jumlah blok: blok baru ditolak, merge tetap boleh
        var s17 = new FractionSession(Operation.Add);
        s17.TryDropToKotak(true, U(2)); s17.TryDropToKotak(false, U(3)); s17.CommitProblem();
        bool penuh = true;
        for (int den = 2; den < 2 + GameRules.MaxDpzBlocks; den++) penuh &= s17.TryDropToDpz(U(den)) == RejectReason.None;
        Check(penuh && s17.Dpz.Count == GameRules.MaxDpzBlocks, "blok sebanyak batas diterima");
        Check(s17.TryDropToDpz(U(2 + GameRules.MaxDpzBlocks)) == RejectReason.DpzPenuh, "blok melebihi batas ditolak");
        Check(s17.TryDropToDpz(U(2)) == RejectReason.None && s17.Dpz.Count == GameRules.MaxDpzBlocks, "merge ke blok yang ada tetap boleh");

        // 18. -1 satuan dan hapus blok
        var s18 = new FractionSession(Operation.Add);
        s18.TryDropToKotak(true, U(2)); s18.TryDropToKotak(false, U(4)); s18.CommitProblem();
        s18.TryDropToDpz(U(4)); s18.TryDropToDpz(U(4)); s18.TryDropToDpz(U(4));   // satu blok 3/4
        s18.RemoveUnitFromDpz(0);
        Check(s18.Dpz.Count == 1 && s18.Dpz.Pieces[0].numerator == 2, "3/4 -1 satuan = 2/4");
        s18.RemoveFromDpz(0);
        Check(s18.Dpz.IsEmpty, "hapus blok utuh");

        // 19. Gabung antar-blok dpz lewat sesi
        var s19 = new FractionSession(Operation.Add);
        s19.TryDropToKotak(true, One); s19.TryDropToKotak(false, U(5)); s19.CommitProblem();
        for (int i = 0; i < 5; i++) s19.TryDropToDpz(U(5));          // satu blok 5/5
        s19.TryDropToDpz(U(5), 0);                                   // persis di atas blok penuh -> blok baru 1/5
        s19.RemoveUnitFromDpz(0); s19.RemoveUnitFromDpz(0);          // blok 0 jadi 3/5
        Check(s19.Dpz.Count == 2 && s19.Dpz.Pieces[0].numerator == 3 && s19.Dpz.Pieces[1].numerator == 1, "susunan [3/5, 1/5]");
        Check(s19.PreviewMergeDpzBlocks(1, 0) == 1, "prediksi: 1 satuan pindah");
        Check(s19.MergeDpzBlocks(1, 0) == 1 && s19.Dpz.Count == 1 && s19.Dpz.Pieces[0].numerator == 4, "gabung: [3/5, 1/5] -> [4/5]");

        Debug.Log(failed == 0 ? "SEMUA LOLOS (session)" : failed + " tes GAGAL (session)");
    }
}