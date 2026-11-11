using UnityEngine;

public static class Audio
{
    static bool warned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => warned = false;

    static AudioService Service
    {
        get
        {
            AudioService s = AudioService.Instance;
            if (s == null && !warned)
            {
                warned = true;
                Debug.LogWarning("[Audio] AudioService belum ada di scene; semua suara dilewati.");
            }
            return s;
        }
    }

    public static bool Ready => AudioService.Instance != null;

    // owner: objek pemilik (biasanya 'this'); loop milik objek yang dinonaktifkan berhenti sendiri
    public static AudioHandle Play(AudioId id, object owner = null, float volumeMul = 1f, float pitchMul = 1f)
    {
        AudioService s = Service;
        return s != null ? s.Play(id, owner, null, volumeMul, pitchMul) : default;
    }

    // Suara di posisi dunia (panning ringan menurut spatialBlend cue)
    public static AudioHandle PlayAt(AudioId id, Vector3 position, object owner = null, float volumeMul = 1f, float pitchMul = 1f)
    {
        AudioService s = Service;
        return s != null ? s.Play(id, owner, position, volumeMul, pitchMul) : default;
    }

    public static void Stop(AudioHandle handle, float fade = -1f) => handle.Stop(fade);

    public static void StopAll(object owner, float fade = 0.1f)
    {
        AudioService s = AudioService.Instance;
        if (s != null) s.StopAll(owner, fade);
    }

    public static void PlayMusic(AudioId id)
    {
        AudioService s = Service;
        if (s != null) s.PlayMusic(id);
    }

    public static void StopMusic(float fade = -1f)
    {
        AudioService s = AudioService.Instance;
        if (s != null) s.StopMusic(fade);
    }

    public static void Duck(float amount, float seconds)
    {
        AudioService s = AudioService.Instance;
        if (s != null) s.Duck(amount, seconds);
    }

    public static void SetWorldMuffled(bool muffled)
    {
        AudioService s = AudioService.Instance;
        if (s != null) s.SetWorldMuffled(muffled);
    }
}