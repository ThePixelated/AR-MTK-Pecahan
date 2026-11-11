using System;
using UnityEngine;

// Volume per bus, master, dan mute. Nilai awal dari AudioSettings (Inspector).
// UI pengaturan nanti: slider memanggil Set/SetMaster/SetMuted, tampilannya mendengarkan Changed.
public static class AudioVolumes
{
    static readonly int BusCount = Enum.GetValues(typeof(AudioBus)).Length;
    static readonly float[] values = new float[BusCount];
    static float master = 1f;
    static bool muted;
    static AudioSettings settings;
    static bool initialized;

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Changed = null;
        settings = null;
        initialized = false;
    }

    public static bool Initialized => initialized;
    public static float Master => master;
    public static bool Muted => muted;

    public static void Initialize(AudioSettings s)
    {
        settings = s;
        ApplyDefaults();
        if (s != null && s.persistVolumes) LoadSaved();
        initialized = true;
        Changed?.Invoke();
    }

    static string Key(string name) => (settings != null ? settings.prefsPrefix : "ARP.Audio.") + name;

    static void ApplyDefaults()
    {
        master = settings != null ? settings.master : 1f;
        muted = settings != null && settings.muteAll;
        for (int i = 0; i < BusCount; i++) values[i] = settings != null ? settings.GetDefault((AudioBus)i) : 1f;
    }

    static void LoadSaved()
    {
        if (PlayerPrefs.HasKey(Key("Master"))) master = Mathf.Clamp01(PlayerPrefs.GetFloat(Key("Master")));
        if (PlayerPrefs.HasKey(Key("Muted"))) muted = PlayerPrefs.GetInt(Key("Muted")) == 1;
        for (int i = 0; i < BusCount; i++)
        {
            string k = Key(((AudioBus)i).ToString());
            if (PlayerPrefs.HasKey(k)) values[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(k));
        }
    }

    public static float Get(AudioBus bus) => values[(int)bus];

    // Volume akhir satu bus: master dikali volume bus; 0 kalau dibisukan
    public static float Effective(AudioBus bus) => muted ? 0f : master * values[(int)bus];

    public static void Set(AudioBus bus, float value)
    {
        values[(int)bus] = Mathf.Clamp01(value);
        SaveFloat(Key(bus.ToString()), values[(int)bus]);
        Changed?.Invoke();
    }

    public static void SetMaster(float value)
    {
        master = Mathf.Clamp01(value);
        SaveFloat(Key("Master"), master);
        Changed?.Invoke();
    }

    public static void SetMuted(bool value)
    {
        muted = value;
        if (settings != null && settings.persistVolumes)
        {
            PlayerPrefs.SetInt(Key("Muted"), value ? 1 : 0);
            PlayerPrefs.Save();
        }
        Changed?.Invoke();
    }

    // Kembali ke nilai Inspector (tidak menghapus simpanan di perangkat)
    public static void ResetToDefaults()
    {
        ApplyDefaults();
        Changed?.Invoke();
    }

    // Hapus volume yang tersimpan di perangkat
    public static void ClearSaved()
    {
        PlayerPrefs.DeleteKey(Key("Master"));
        PlayerPrefs.DeleteKey(Key("Muted"));
        for (int i = 0; i < BusCount; i++) PlayerPrefs.DeleteKey(Key(((AudioBus)i).ToString()));
        PlayerPrefs.Save();
    }

    static void SaveFloat(string key, float value)
    {
        if (settings == null || !settings.persistVolumes) return;
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }
}