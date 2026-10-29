using System;
using System.Collections.Generic;

// Pembuat soal Media 5 dari garis aktif, dengan memori anti-ulang. Seed opsional supaya bisa dites dengan hasil tetap.
public sealed class NumberLineProblemGenerator
{
    readonly Random rng;
    readonly List<NumberLineProblem> recent = new List<NumberLineProblem>();

    public int Window { get; set; }

    public NumberLineProblemGenerator(int? seed = null, int window = 5)
    {
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
        Window = window;
    }

    public void ClearMemory() => recent.Clear();

    // Jumlah soal berbeda yang mungkin pada garis ini
    public static int PossibleCount(NumberLineConfig config, Media5OperationMode mode)
    {
        int m = config.MaxTick;
        int perOperation = m < 2 ? 0 : m * (m - 1) / 2;
        return mode == Media5OperationMode.Both ? 2 * perOperation : perOperation;
    }

    public bool TryGenerate(NumberLineConfig config, Media5OperationMode mode, out NumberLineProblem problem)
    {
        problem = default;
        int max = config.MaxTick;
        var pool = new List<NumberLineProblem>();

        if (mode != Media5OperationMode.AddOnly)
            for (int a = 2; a <= max; a++)
                for (int b = 1; b < a; b++)
                    pool.Add(new NumberLineProblem(NumberLineOperation.Subtract, a, b));

        if (mode != Media5OperationMode.SubtractOnly)
            for (int a = 1; a < max; a++)
                for (int b = 1; a + b <= max; b++)
                    pool.Add(new NumberLineProblem(NumberLineOperation.Add, a, b));

        if (pool.Count == 0) return false;

        List<NumberLineProblem> fresh = pool.FindAll(p => !IsRecent(p));
        if (fresh.Count == 0)
        {
            // semua soal sudah terpakai (garis kecil atau jendela besar): mulai lagi, tapi jangan langsung mengulang soal terakhir
            bool hadLast = recent.Count > 0;
            NumberLineProblem last = hadLast ? recent[recent.Count - 1] : default;
            recent.Clear();
            fresh = pool;
            if (hadLast && pool.Count > 1) fresh = pool.FindAll(p => !p.SamePair(last));
        }

        problem = fresh[rng.Next(fresh.Count)];

        if (Window > 0)
        {
            recent.Add(problem);
            while (recent.Count > Window) recent.RemoveAt(0);
        }
        return true;
    }

    bool IsRecent(NumberLineProblem p) => recent.Exists(r => r.SamePair(p));
}