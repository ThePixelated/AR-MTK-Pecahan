using System;
using UnityEngine;
using UnityEngine.Audio;

[Serializable]
public class AudioClipEntry
{
    public AudioClip clip;
    [Min(0f)] public float weight = 1f;
}

// Satu jenis suara: klip (dengan bobot untuk variasi), volume dan nada, batas pemutaran, fade, dan ducking.
[CreateAssetMenu(menuName = "AR Pecahan/Audio/Cue")]
public class AudioCue : ScriptableObject
{
    public AudioId id;
    public AudioBus bus = AudioBus.Ui;
    public AudioClipEntry[] clips = new AudioClipEntry[0];

    [Header("Volume dan nada")]
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0f, 0.5f)] public float volumeJitter = 0f;       // variasi acak volume (+/- fraksi)
    [Range(0.25f, 3f)] public float pitch = 1f;
    [Range(0f, 0.5f)] public float pitchJitter = 0.05f;     // variasi acak nada (+/- fraksi)

    [Header("Perilaku")]
    public bool loop;
    [Range(0f, 1f)] public float spatialBlend = 0f;         // 0 = 2D; dipakai hanya kalau suara dimainkan di sebuah posisi dunia
    [Min(0f)] public float cooldown = 0.05f;                // jeda minimal antar pemicu cue ini (anti-spam)
    [Min(1)] public int maxVoices = 3;                      // suara bersamaan dari cue ini
    [Range(0, 255)] public int priority = 128;              // 0 = terpenting (AudioSource.priority); menentukan siapa yang dicuri saat kolam penuh
    public bool stealOldest = true;                         // batas cue tercapai: true = ganti yang tertua; false = abaikan yang baru
    public bool stopWhenMuffled;                            // loop dunia berhenti saat bus World dibisukan (marker hilang)

    [Header("Fade")]
    [Min(0f)] public float fadeIn = 0f;
    [Min(0f)] public float fadeOut = 0.1f;

    [Header("Ducking: musik mengecil saat cue ini bunyi (0 = tidak)")]
    [Range(0f, 1f)] public float duckAmount = 0f;
    [Min(0f)] public float duckSeconds = 0f;

    public AudioMixerGroup mixerGroupOverride;              // opsional

    public bool HasClips
    {
        get
        {
            if (clips == null) return false;
            foreach (AudioClipEntry e in clips) if (e != null && e.clip) return true;
            return false;
        }
    }
}