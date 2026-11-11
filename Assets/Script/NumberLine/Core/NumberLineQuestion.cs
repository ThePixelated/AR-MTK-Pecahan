using System;
using System.Collections.Generic;
using UnityEngine;

public enum QuestionKind
{
    LineOnly,        // siswa mengatur panjang dan partisi garis sesuai gambar; pion tersembunyi
    PionOnly,        // garis sudah ditetapkan; siswa menaruh pion di nilai target
    LineAndPion,     // siswa mengatur garis lalu menaruh pion di nilai target
    MultiPosition,   // garis sudah ditetapkan; beberapa titik bersurat (A, B, C) dibaca nilainya
    Comparison,      // soal teks dengan satu jawaban pecahan; tanpa garis
    MultiPion        // garis tetap; tiap tokoh satu pion yang digeser siswa (partisi garis mengikuti pion yang dipilih). Paling akhir supaya nilai enum aset lama tidak bergeser.
}

public enum ComparisonRule
{
    Exact,           // jawaban harus bernilai sama dengan 'exact'
    StrictlyBetween  // jawaban harus tepat di antara lowerBound dan upperBound (batas sendiri tidak dihitung)
}

public enum PionCheckMode
{
    ByValue,   // pion harus bernilai sama dengan jawaban (5/10 sama dengan 1/2, apa pun partisinya)
    ByTick     // pion harus di tick yang sama pada partisi yang sama dengan jawaban (5/10 hanya benar di tick 5 pada partisi 10)
}

// Satu tokoh pada soal MultiPion
[Serializable]
public class PionAnswer
{
    public string label = "";            // teks tombol tokoh di sisi kiri (mis. "Rahmi"); kosong = A, B, C ...
    public FractionValue value;          // nilai yang harus ditempati pion
    [Min(0)] public int partisi = 0;     // partisi garis saat pion ini aktif; 0 = penyebut 'value' apa adanya (5/10 jadi 10)

    public int ResolvedPartisi => partisi > 0 ? partisi : (value.denominator > 0 ? value.denominator : 0);
}

[Serializable]
public struct LineAnswer
{
    public int length;
    public int partisi;

    public LineAnswer(int length, int partisi) { this.length = length; this.partisi = partisi; }
}

// Pecahan campuran untuk diisi di Inspector (tidak disederhanakan)
[Serializable]
public struct FractionValue
{
    public int whole;
    public int numerator;
    public int denominator;

    public FractionValue(int whole, int numerator, int denominator)
    {
        this.whole = whole;
        this.numerator = numerator;
        this.denominator = denominator;
    }

    public bool IsValid => denominator > 0 && whole >= 0 && numerator >= 0;

    public Fraction ToFraction() =>
        IsValid ? new Fraction((long)whole * denominator + numerator, denominator) : Fraction.Zero;

    public string ToText()
    {
        if (!IsValid) return "?";
        if (numerator == 0) return whole.ToString();
        return whole == 0 ? $"{numerator}/{denominator}" : $"{whole} {numerator}/{denominator}";
    }
}

// Satu soal mode Latihan. Hanya field milik jenisnya yang dipakai.
[Serializable]
public class NumberLineQuestion
{
    public QuestionKind kind;
    [TextArea(2, 4)] public string text = "";        // boleh memuat {target}
    public Sprite image;                             // gambar soal (opsional)

    [Header("Garis tetap (PionOnly, MultiPosition)")]
    public int presetLength = 1;
    public int presetPartisi = 5;

    [Header("LineOnly: kunci jawaban")]
    public LineAnswer lineAnswer = new LineAnswer(1, 4);
    public List<LineAnswer> alternativeLineAnswers = new List<LineAnswer>();

    [Header("PionOnly, LineAndPion: nilai yang harus ditempati pion")]
    public FractionValue target;

    [Header("MultiPosition: nilai tiap titik bersurat (A, B, C ...)")]
    public List<FractionValue> markers = new List<FractionValue>();

    [Header("Comparison")]
    public ComparisonRule rule = ComparisonRule.Exact;
    public FractionValue exact;
    public FractionValue lowerBound;
    public FractionValue upperBound;

    [Header("MultiPion: tiap tokoh satu pion yang digeser (panjang garis = presetLength)")]
    public List<PionAnswer> pions = new List<PionAnswer> { new PionAnswer(), new PionAnswer(), new PionAnswer() };   // default 3 pion
    public PionCheckMode checkMode = PionCheckMode.ByValue;
    [Tooltip("Kunci pemilih hewan dan pengaturan panjang garis. Matikan kalau siswa boleh mengubahnya.")]
    public bool lockConfig = true;

    // Teks soal dengan {target} diganti nilai target
    public string FormattedText() => (text ?? "").Replace("{target}", target.ToText());

    // Memeriksa isian soal terhadap batas yang berlaku; daftar kosong = soal sah
    public List<string> Validate(NumberLineLimits limits, int maxMarkers, int maxPions = 5)
    {
        var issues = new List<string>();

        switch (kind)
        {
            case QuestionKind.LineOnly:
                CheckLine(issues, "jawaban", lineAnswer.length, lineAnswer.partisi, limits);
                if (alternativeLineAnswers != null)
                    for (int i = 0; i < alternativeLineAnswers.Count; i++)
                        CheckLine(issues, $"jawaban alternatif {i + 1}", alternativeLineAnswers[i].length, alternativeLineAnswers[i].partisi, limits);
                break;

            case QuestionKind.PionOnly:
                CheckLine(issues, "garis tetap", presetLength, presetPartisi, limits);
                if (!target.IsValid) issues.Add("nilai target belum diisi");
                else if (new NumberLineConfig(presetLength, presetPartisi).Classify(target.ToFraction(), out _) != NumberLineReach.OnLine)
                    issues.Add($"target {target.ToText()} tidak jatuh di tick pada garis {presetLength} x {presetPartisi}");
                break;

            case QuestionKind.LineAndPion:
                if (!target.IsValid) { issues.Add("nilai target belum diisi"); break; }
                Fraction r = target.ToFraction().Reduce();
                if (r.denominator > limits.MaxPartisi)
                    issues.Add($"target {target.ToText()} butuh partisi {r.denominator}, melebihi maksimal {limits.MaxPartisi}");
                if ((r.numerator + r.denominator - 1) / r.denominator > limits.MaxLength)
                    issues.Add($"target {target.ToText()} melebihi panjang garis maksimal {limits.MaxLength}");
                break;

            case QuestionKind.MultiPosition:
                CheckLine(issues, "garis tetap", presetLength, presetPartisi, limits);
                int n = markers != null ? markers.Count : 0;
                if (n < 1) issues.Add("belum ada titik bersurat");
                else if (n > maxMarkers) issues.Add($"{n} titik melebihi batas {maxMarkers}");
                for (int i = 0; i < n; i++)
                {
                    string label = NumberLineLabels.Letter(i);
                    if (!markers[i].IsValid) issues.Add($"nilai titik {label} belum diisi");
                    else if (new NumberLineConfig(presetLength, presetPartisi).Classify(markers[i].ToFraction(), out _) != NumberLineReach.OnLine)
                        issues.Add($"titik {label} ({markers[i].ToText()}) tidak jatuh di tick pada garis {presetLength} x {presetPartisi}");
                }
                break;

            case QuestionKind.MultiPion:
                {
                    if (presetLength < limits.MinLength || presetLength > limits.MaxLength)
                        issues.Add($"garis tetap: panjang {presetLength} di luar {limits.MinLength}-{limits.MaxLength}");

                    int pionCount = pions != null ? pions.Count : 0;
                    if (pionCount < 1) issues.Add("belum ada pion");
                    else if (pionCount > maxPions) issues.Add($"{pionCount} pion melebihi batas {maxPions}");

                    for (int i = 0; i < pionCount; i++)
                    {
                        PionAnswer a = pions[i];
                        string who = string.IsNullOrWhiteSpace(a.label) ? NumberLineLabels.Letter(i) : a.label.Trim();
                        if (!a.value.IsValid) { issues.Add($"nilai pion {who} belum diisi"); continue; }

                        int p = a.ResolvedPartisi;
                        if (p < limits.MinPartisi || p > limits.MaxPartisi)
                        {
                            issues.Add($"pion {who}: partisi {p} di luar {limits.MinPartisi}-{limits.MaxPartisi}");
                            continue;
                        }

                        NumberLineReach reach = new NumberLineConfig(presetLength, p).Classify(a.value.ToFraction(), out _);
                        if (reach == NumberLineReach.BeyondLine) issues.Add($"pion {who} ({a.value.ToText()}) melewati garis panjang {presetLength}");
                        else if (reach == NumberLineReach.NotOnPartition) issues.Add($"pion {who} ({a.value.ToText()}) tidak jatuh di tick pada partisi {p}");
                        else if (checkMode == PionCheckMode.ByTick && p != a.value.denominator)
                            issues.Add($"pion {who}: mode ByTick, partisi {p} harus sama dengan penyebut {a.value.denominator}");
                    }
                    break;
                }

            case QuestionKind.Comparison:
                if (rule == ComparisonRule.Exact)
                {
                    if (!exact.IsValid) issues.Add("jawaban tepat belum diisi");
                }
                else if (!lowerBound.IsValid || !upperBound.IsValid) issues.Add("batas bawah atau atas belum diisi");
                else if (lowerBound.ToFraction().CompareTo(upperBound.ToFraction()) >= 0) issues.Add("batas bawah harus lebih kecil dari batas atas");
                break;
        }
        return issues;
    }

    static void CheckLine(List<string> issues, string what, int length, int partisi, NumberLineLimits l)
    {
        if (length < l.MinLength || length > l.MaxLength) issues.Add($"{what}: panjang {length} di luar {l.MinLength}-{l.MaxLength}");
        if (partisi < l.MinPartisi || partisi > l.MaxPartisi) issues.Add($"{what}: partisi {partisi} di luar {l.MinPartisi}-{l.MaxPartisi}");
    }
}