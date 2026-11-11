using UnityEngine;

// Pengaturan audio yang kamu atur di Inspector (nilai awal volume per bus dan parameter mesin).
[CreateAssetMenu(menuName = "AR Pecahan/Audio/Settings")]
public class AudioSettings : ScriptableObject
{
    [Header("Volume awal (0..1): atur di sini")]
    [Range(0f, 1f)] public float master = 0.8f;       // batas atas bawaan 0,8 supaya tidak terlalu keras untuk anak-anak
    [Range(0f, 1f)] public float music = 0.35f;       // BGM jauh lebih pelan dari SFX
    [Range(0f, 1f)] public float ui = 0.8f;
    [Range(0f, 1f)] public float feedback = 0.9f;
    [Range(0f, 1f)] public float world = 0.8f;
    [Range(0f, 1f)] public float voice = 1f;          // cadangan (narasi), belum dipakai
    public bool muteAll;

    [Header("Simpan volume ke perangkat")]
    [Tooltip("Volume yang diubah lewat AudioVolumes.Set (UI pengaturan nanti) disimpan dan dipakai lagi saat aplikasi dibuka. Tanpa UI tidak ada yang tersimpan, jadi nilai Inspector tetap menang.")]
    public bool persistVolumes = true;
    public string prefsPrefix = "ARP.Audio.";

    [Header("Mesin")]
    [Min(4)] public int voicePool = 24;               // suara SFX bersamaan (di luar dua sumber musik)
    [Min(0f)] public float musicCrossfadeSeconds = 1.2f;
    [Range(0f, 1f)] public float worldMuffleGain = 0.35f;   // volume bus World saat marker hilang
    [Min(0.05f)] public float worldMuffleSeconds = 0.4f;
    public bool pauseWhenAppPaused = true;            // aplikasi ke background atau ada telepon: semua suara berhenti sementara

    [Header("Debug")]
    public bool logMissingCues = true;
    public bool logPlays = false;

    public float GetDefault(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.Music: return music;
            case AudioBus.Ui: return ui;
            case AudioBus.Feedback: return feedback;
            case AudioBus.World: return world;
            default: return voice;
        }
    }
}