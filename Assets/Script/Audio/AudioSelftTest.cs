using System;
using System.Collections.Generic;
using UnityEngine;

public class AudioSelfTest : MonoBehaviour
{
    int pass, fail;

    [ContextMenu("Run Audio Tests")]
    public void Run()
    {
        pass = fail = 0;
        TestMath();
        TestPicker();
        TestGate();
        TestVolumes();
        TestLibrary();
        string summary = $"Audio tests: {pass} lulus, {fail} gagal";
        if (fail == 0) Debug.Log(summary); else Debug.LogError(summary);
    }

    void Check(bool ok, string name)
    {
        if (ok) pass++;
        else { fail++; Debug.LogError("GAGAL: " + name); }
    }

    static bool Near(float a, float b, float eps = 0.001f) => Mathf.Abs(a - b) <= eps;

    void TestMath()
    {
        Check(Near(AudioMath.LinearToDb(1f), 0f), "1.0 = 0 dB");
        Check(Near(AudioMath.LinearToDb(0.5f), -6.0206f, 0.01f), "0.5 = -6 dB");
        Check(AudioMath.LinearToDb(0f) == AudioMath.SilenceDb, "0 = -80 dB (hening)");
        Check(Near(AudioMath.DbToLinear(0f), 1f) && AudioMath.DbToLinear(-80f) == 0f, "dB ke linear: 0 dB = 1, -80 dB = 0");
        Check(Near(AudioMath.DbToLinear(AudioMath.LinearToDb(0.3f)), 0.3f), "konversi dB bolak-balik");

        var rng = new System.Random(1);
        Check(AudioMath.Jitter(1f, 0f, rng) == 1f, "tanpa variasi: nilai tetap");
        bool inRange = true;
        for (int i = 0; i < 200; i++)
        {
            float v = AudioMath.Jitter(1f, 0.1f, rng);
            if (v < 0.9f || v > 1.1f) inRange = false;
        }
        Check(inRange, "variasi 10 persen tetap di 0.9 sampai 1.1");
    }

    void TestPicker()
    {
        var rng = new System.Random(2);

        Check(new ClipPicker().Pick(new List<float>(), rng) == -1, "tanpa klip: -1");
        Check(new ClipPicker().Pick(new List<float> { 1f }, rng) == 0, "satu klip: selalu 0");

        var two = new ClipPicker();
        var w2 = new List<float> { 1f, 1f };
        int prev = two.Pick(w2, rng);
        bool noRepeat = true;
        for (int i = 0; i < 40; i++)
        {
            int p = two.Pick(w2, rng);
            if (p == prev) noRepeat = false;
            prev = p;
        }
        Check(noRepeat, "dua klip: tidak pernah sama dua kali berturutan");

        var three = new ClipPicker();
        var w3 = new List<float> { 1f, 1f, 1f };
        var counts = new int[3];
        prev = -1;
        noRepeat = true;
        for (int i = 0; i < 300; i++)
        {
            int p = three.Pick(w3, rng);
            counts[p]++;
            if (p == prev) noRepeat = false;
            prev = p;
        }
        Check(noRepeat, "tiga klip: tidak ada pengulangan langsung");
        Check(counts[0] > 60 && counts[1] > 60 && counts[2] > 60, "tiga klip: semuanya terpakai");

        var heavy = new ClipPicker();
        var wh = new List<float> { 20f, 1f };
        int zeros = 0;
        var heavyRng = new System.Random(3);
        for (int i = 0; i < 40; i++) if (heavy.Pick(wh, heavyRng) == 0) zeros++;
        Check(zeros >= 15, "dua klip: pengulangan langsung tetap dilarang walau bobot timpang");

        var zero = new ClipPicker();
        var wz = new List<float> { 0f, 0f, 0f };
        Check(zero.Pick(wz, rng) >= 0, "semua bobot nol: tetap memilih sesuatu");
    }

    void TestGate()
    {
        var gate = new CueGate();
        Check(gate.Ready(0f, 0.5f), "awal: siap");
        gate.Mark(1f);
        Check(!gate.Ready(1.2f, 0.5f), "dalam cooldown: belum siap");
        Check(gate.Ready(1.5f, 0.5f) && gate.Ready(2f, 0.5f), "setelah cooldown: siap");
        Check(gate.Ready(1.0f, 0f), "cooldown 0: selalu siap");
    }

    void TestVolumes()
    {
        var s = ScriptableObject.CreateInstance<AudioSettings>();
        s.persistVolumes = false;
        s.master = 0.8f; s.music = 0.3f; s.ui = 0.5f; s.feedback = 0.9f; s.world = 0.7f; s.voice = 1f;
        s.muteAll = false;

        int changes = 0;
        Action handler = () => changes++;
        AudioVolumes.Changed += handler;

        AudioVolumes.Initialize(s);
        Check(Near(AudioVolumes.Master, 0.8f) && Near(AudioVolumes.Get(AudioBus.Music), 0.3f) && Near(AudioVolumes.Get(AudioBus.World), 0.7f), "nilai awal dari Settings");
        Check(Near(AudioVolumes.Effective(AudioBus.Ui), 0.4f), "volume akhir = master x bus (0.8 x 0.5)");
        Check(changes >= 1, "Initialize memicu Changed");

        int before = changes;
        AudioVolumes.Set(AudioBus.Ui, 2f);
        Check(Near(AudioVolumes.Get(AudioBus.Ui), 1f) && changes == before + 1, "nilai di atas 1 dibatasi ke 1 dan memicu Changed");
        AudioVolumes.Set(AudioBus.Ui, -1f);
        Check(Near(AudioVolumes.Get(AudioBus.Ui), 0f), "nilai di bawah 0 dibatasi ke 0");

        AudioVolumes.Set(AudioBus.Ui, 0.5f);
        AudioVolumes.SetMaster(0.5f);
        Check(Near(AudioVolumes.Effective(AudioBus.Ui), 0.25f), "master 0.5 x bus 0.5 = 0.25");

        AudioVolumes.SetMuted(true);
        Check(AudioVolumes.Muted && AudioVolumes.Effective(AudioBus.Ui) == 0f && AudioVolumes.Effective(AudioBus.Music) == 0f, "mute: semua bus 0");
        AudioVolumes.SetMuted(false);
        Check(AudioVolumes.Effective(AudioBus.Ui) > 0f, "mute dimatikan: kembali");

        AudioVolumes.ResetToDefaults();
        Check(Near(AudioVolumes.Master, 0.8f) && Near(AudioVolumes.Get(AudioBus.Ui), 0.5f), "ResetToDefaults: kembali ke Inspector");
        Check(Near(s.GetDefault(AudioBus.Voice), 1f) && Near(s.GetDefault(AudioBus.Feedback), 0.9f), "GetDefault per bus");

        AudioVolumes.Changed -= handler;
        UnityEngine.Object.DestroyImmediate(s);
    }

    void TestLibrary()
    {
        var lib = ScriptableObject.CreateInstance<AudioLibrary>();
        var a = ScriptableObject.CreateInstance<AudioCue>(); a.id = AudioId.Ui_Click;
        var b = ScriptableObject.CreateInstance<AudioCue>(); b.id = AudioId.Ui_Back;
        var none = ScriptableObject.CreateInstance<AudioCue>(); none.id = AudioId.None;
        var dup = ScriptableObject.CreateInstance<AudioCue>(); dup.id = AudioId.Ui_Click;

        lib.SetCues(new List<AudioCue> { a, b, none });
        Check(lib.TryGet(AudioId.Ui_Click, out AudioCue got) && got == a, "cue terdaftar ditemukan");
        Check(lib.TryGet(AudioId.Ui_Back, out got) && got == b, "cue kedua ditemukan");
        Check(!lib.TryGet(AudioId.Ui_Locked, out got), "cue yang tidak ada: false");
        Check(!lib.TryGet(AudioId.None, out got), "AudioId.None tidak pernah terdaftar");

        lib.SetCues(new List<AudioCue> { a, dup });
        Check(lib.TryGet(AudioId.Ui_Click, out got) && got == dup, "id ganda: yang terakhir menang");

        Check(!a.HasClips, "cue tanpa klip: HasClips false");
        AudioClip clip = AudioClip.Create("uji", 100, 1, 44100, false);
        a.clips = new[] { new AudioClipEntry { clip = clip } };
        Check(a.HasClips, "cue dengan klip: HasClips true");
        a.clips = new[] { new AudioClipEntry { clip = null } };
        Check(!a.HasClips, "entri tanpa klip: HasClips false");

        UnityEngine.Object.DestroyImmediate(clip);
        foreach (AudioCue c in new[] { a, b, none, dup }) UnityEngine.Object.DestroyImmediate(c);
        UnityEngine.Object.DestroyImmediate(lib);
    }
}