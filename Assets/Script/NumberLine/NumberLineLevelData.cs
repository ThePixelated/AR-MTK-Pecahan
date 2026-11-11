using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "AR Pecahan/Level Garis Bilangan")]
public class NumberLineLevelData : ScriptableObject
{
    public string levelTitle = "Level 1";
    [Tooltip("true: baru bisa lanjut kalau benar. false: setelah CEK (benar atau salah) boleh lanjut.")]
    public bool stayUntilCorrect = true;
    public List<NumberLineQuestion> questions = new List<NumberLineQuestion>();

    public LatihanLevel ToLevel() => new LatihanLevel
    {
        title = levelTitle,
        stayUntilCorrect = stayUntilCorrect,
        questions = new List<NumberLineQuestion>(questions)
    };

    // Klik kanan aset > "Periksa soal": melaporkan isian yang tidak sah (target tidak jatuh di tick, titik di luar garis, dan sebagainya)
    [ContextMenu("Periksa soal")]
    void Inspect()
    {
        var limits = new NumberLineLimits();
        var options = new LatihanOptions();
        int total = 0;
        for (int i = 0; i < questions.Count; i++)
        {
            foreach (string issue in questions[i].Validate(limits, options.MaxMarkers, options.MaxPions))
            {
                Debug.LogWarning($"[{name}] soal {i + 1} ({questions[i].kind}): {issue}", this);
                total++;
            }
        }
        Debug.Log(total == 0 ? $"[{name}] {questions.Count} soal sah." : $"[{name}] {total} masalah ditemukan.", this);
    }
}