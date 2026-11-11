using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Tanda untuk satu suara yang sedang berjalan (loop aliran air, goyangan cairan, dan sebagainya)
public readonly struct AudioHandle
{
    internal readonly int slot;
    internal readonly int generation;

    internal AudioHandle(int slot, int generation)
    {
        this.slot = slot;
        this.generation = generation;
    }

    public bool IsValid
    {
        get
        {
            AudioService s = AudioService.Instance;
            return generation > 0 && s != null && s.IsAlive(this);
        }
    }

    public void Stop(float fade = -1f)
    {
        AudioService s = AudioService.Instance;
        if (s != null) s.Stop(this, fade);
    }

    // volumeMul dan pitchMul mengubah suara yang sedang berjalan (mis. volume goyangan mengikuti kecepatan)
    public void SetParams(float volumeMul, float pitchMul = 1f)
    {
        AudioService s = AudioService.Instance;
        if (s != null) s.SetParams(this, volumeMul, pitchMul);
    }
}

// Mesin audio: kolam sumber suara, musik dengan crossfade, loop dengan handle, ducking, bisu bus World, dan penghentian per pemilik.
// Letakkan SATU di objek root scene (bukan anak objek lain).
[DefaultExecutionOrder(-200)]
public class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    [SerializeField] AudioLibrary library;
    [SerializeField] AudioSettings settings;
    [SerializeField] bool dontDestroyOnLoad = true;

    [Header("Mixer group (opsional; kosongkan kalau tidak memakai AudioMixer)")]
    [SerializeField] AudioMixerGroup musicGroup;
    [SerializeField] AudioMixerGroup uiGroup;
    [SerializeField] AudioMixerGroup feedbackGroup;
    [SerializeField] AudioMixerGroup worldGroup;
    [SerializeField] AudioMixerGroup voiceGroup;

    sealed class Voice
    {
        public AudioSource source;
        public AudioCue cue;
        public object owner;
        public AudioBus bus;
        public bool isMusic;
        public bool active;
        public int generation;
        public float startTime;
        public float baseVolume = 1f, basePitch = 1f;
        public float userVolume = 1f, userPitch = 1f;
        public float fadeGain = 1f, fadeTarget = 1f, fadeRate;
        public bool stopAfterFade;
    }

    readonly List<Voice> voices = new List<Voice>();       // kolam SFX (indeks 0..poolCount-1), lalu dua sumber musik
    readonly Voice[] musicVoices = new Voice[2];
    int poolCount;
    int musicCurrent = -1;

    readonly Dictionary<AudioCue, CueGate> gates = new Dictionary<AudioCue, CueGate>();
    readonly Dictionary<AudioCue, ClipPicker> pickers = new Dictionary<AudioCue, ClipPicker>();
    readonly HashSet<AudioId> warned = new HashSet<AudioId>();
    readonly HashSet<AudioCue> warnedNoClips = new HashSet<AudioCue>();
    readonly System.Random rng = new System.Random();
    readonly List<float> weightBuffer = new List<float>();
    readonly List<AudioClip> clipBuffer = new List<AudioClip>();

    float duckGain = 1f, duckTarget = 1f, duckUntil;
    bool worldMuffled;
    float muffleGain = 1f;

    public bool WorldMuffled => worldMuffled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    // ---------------- siklus ----------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (settings == null)
        {
            Debug.LogWarning($"[{name}] AudioService: 'Settings' kosong, memakai nilai bawaan.", this);
            settings = ScriptableObject.CreateInstance<AudioSettings>();
        }
        if (library == null) Debug.LogWarning($"[{name}] AudioService: 'Library' kosong, semua cue tidak akan bunyi.", this);

        if (dontDestroyOnLoad)
        {
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            else Debug.LogWarning($"[{name}] AudioService: harus objek root supaya 'Dont Destroy On Load' berlaku.", this);
        }

        AudioVolumes.Initialize(settings);
        BuildPool();
    }

    void Start()
    {
        int listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        if (listeners > 1) Debug.LogWarning($"[{name}] AudioService: ada {listeners} AudioListener aktif; sisakan satu saja.", this);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnApplicationPause(bool paused)
    {
        if (settings != null && settings.pauseWhenAppPaused) AudioListener.pause = paused;
    }

    void BuildPool()
    {
        poolCount = Mathf.Max(4, settings.voicePool);
        for (int i = 0; i < poolCount; i++) voices.Add(NewVoice($"Voice {i}", false));
        for (int i = 0; i < 2; i++)
        {
            Voice v = NewVoice($"Music {i}", true);
            musicVoices[i] = v;
            voices.Add(v);
        }
    }

    Voice NewVoice(string objName, bool music)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = 500f;       // marker kecil: jarak tidak boleh meredam suara
        return new Voice { source = src, isMusic = music };
    }

    AudioMixerGroup GroupFor(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.Music: return musicGroup;
            case AudioBus.Ui: return uiGroup;
            case AudioBus.Feedback: return feedbackGroup;
            case AudioBus.World: return worldGroup;
            default: return voiceGroup;
        }
    }

    // ---------------- memutar ----------------
    bool TryCue(AudioId id, out AudioCue cue)
    {
        cue = null;
        if (library == null) return false;
        if (library.TryGet(id, out cue)) return true;

        if (settings.logMissingCues && warned.Add(id)) Debug.LogWarning($"[Audio] cue {id} tidak ada di library.", this);
        return false;
    }

    public AudioHandle Play(AudioId id, object owner = null, Vector3? position = null, float volumeMul = 1f, float pitchMul = 1f)
    {
        if (id == AudioId.None || !TryCue(id, out AudioCue cue)) return default;
        return Play(cue, owner, position, volumeMul, pitchMul);
    }

    public AudioHandle Play(AudioCue cue, object owner, Vector3? position, float volumeMul, float pitchMul)
    {
        if (cue == null) return default;
        if (cue.bus == AudioBus.Music)
        {
            PlayMusic(cue);
            return default;
        }
        if (!PickClip(cue, out AudioClip clip))
        {
            if (settings.logMissingCues && warnedNoClips.Add(cue)) Debug.LogWarning($"[Audio] cue {cue.name} belum punya klip.", cue);
            return default;
        }
        if (worldMuffled && cue.stopWhenMuffled) return default;

        float now = Time.unscaledTime;
        if (!gates.TryGetValue(cue, out CueGate gate)) { gate = new CueGate(); gates[cue] = gate; }
        if (!gate.Ready(now, cue.cooldown)) return default;

        Voice v = AcquireVoice(cue);
        if (v == null) return default;
        gate.Mark(now);

        v.cue = cue;
        v.owner = owner;
        v.bus = cue.bus;
        v.active = true;
        v.generation++;
        v.startTime = now;
        v.baseVolume = AudioMath.Jitter(cue.volume, cue.volumeJitter, rng) * volumeMul;
        v.basePitch = AudioMath.Jitter(cue.pitch, cue.pitchJitter, rng);
        v.userVolume = 1f;
        v.userPitch = pitchMul;
        v.stopAfterFade = false;
        v.fadeGain = cue.fadeIn > 0f ? 0f : 1f;
        v.fadeTarget = 1f;
        v.fadeRate = cue.fadeIn > 0f ? 1f / cue.fadeIn : 0f;

        AudioSource s = v.source;
        s.Stop();
        s.clip = clip;
        s.loop = cue.loop;
        s.priority = cue.priority;
        s.outputAudioMixerGroup = cue.mixerGroupOverride != null ? cue.mixerGroupOverride : GroupFor(cue.bus);
        if (position.HasValue)
        {
            s.transform.position = position.Value;
            s.spatialBlend = cue.spatialBlend;
        }
        else
        {
            s.spatialBlend = 0f;
        }

        ApplyVolume(v);
        s.pitch = v.basePitch * v.userPitch;
        s.Play();

        if (cue.duckAmount > 0f) Duck(cue.duckAmount, cue.duckSeconds);
        if (settings.logPlays) Debug.Log($"[Audio] {cue.name} ({clip.name})", this);

        return new AudioHandle(voices.IndexOf(v), v.generation);
    }

    bool PickClip(AudioCue cue, out AudioClip clip)
    {
        clip = null;
        weightBuffer.Clear();
        clipBuffer.Clear();
        if (cue.clips != null)
        {
            foreach (AudioClipEntry e in cue.clips)
            {
                if (e == null || !e.clip) continue;
                clipBuffer.Add(e.clip);
                weightBuffer.Add(e.weight);
            }
        }
        if (clipBuffer.Count == 0) return false;

        if (!pickers.TryGetValue(cue, out ClipPicker picker)) { picker = new ClipPicker(); pickers[cue] = picker; }
        int index = picker.Pick(weightBuffer, rng);
        if (index < 0) return false;

        clip = clipBuffer[index];
        return true;
    }

    // Suara bebas di kolam; batas suara per cue dihormati; kolam penuh: curi suara berprioritas lebih rendah
    Voice AcquireVoice(AudioCue cue)
    {
        int count = 0;
        Voice oldest = null;
        for (int i = 0; i < poolCount; i++)
        {
            Voice v = voices[i];
            if (!v.active || v.cue != cue) continue;
            count++;
            if (oldest == null || v.startTime < oldest.startTime) oldest = v;
        }
        if (count >= cue.maxVoices)
        {
            if (!cue.stealOldest || oldest == null) return null;
            Release(oldest);
        }

        for (int i = 0; i < poolCount; i++)
            if (!voices[i].active) return voices[i];

        Voice victim = null;
        for (int i = 0; i < poolCount; i++)
        {
            Voice v = voices[i];
            if (v.cue.priority <= cue.priority) continue;     // hanya yang kurang penting yang boleh dicuri
            if (victim == null || v.cue.priority > victim.cue.priority
                || (v.cue.priority == victim.cue.priority && v.startTime < victim.startTime)) victim = v;
        }
        if (victim == null) return null;
        Release(victim);
        return victim;
    }

    // ---------------- handle ----------------
    public bool IsAlive(AudioHandle h) =>
        h.generation > 0 && h.slot >= 0 && h.slot < voices.Count && voices[h.slot].active && voices[h.slot].generation == h.generation;

    public void Stop(AudioHandle h, float fade = -1f)
    {
        if (!IsAlive(h)) return;
        Voice v = voices[h.slot];
        BeginStop(v, fade < 0f ? v.cue.fadeOut : fade);
    }

    public void SetParams(AudioHandle h, float volumeMul, float pitchMul)
    {
        if (!IsAlive(h)) return;
        Voice v = voices[h.slot];
        v.userVolume = Mathf.Max(0f, volumeMul);
        v.userPitch = Mathf.Max(0.05f, pitchMul);
    }

    // Hentikan semua suara milik objek ini (mis. saat halaman atau objek dunia dinonaktifkan)
    public void StopAll(object owner, float fade = 0.1f)
    {
        if (owner == null) return;
        for (int i = 0; i < poolCount; i++)
        {
            Voice v = voices[i];
            if (v.active && Equals(v.owner, owner)) BeginStop(v, fade);
        }
    }

    public void StopBus(AudioBus bus, float fade = 0.1f)
    {
        foreach (Voice v in voices)
            if (v.active && v.bus == bus) BeginStop(v, fade);
    }

    public void StopEverything(float fade = 0.1f)
    {
        foreach (Voice v in voices) BeginStop(v, fade);
    }

    void BeginStop(Voice v, float fade)
    {
        if (!v.active) return;
        if (fade <= 0f || v.fadeGain <= 0.0001f) { Release(v); return; }

        v.fadeTarget = 0f;
        v.fadeRate = Mathf.Max(v.fadeGain, 0.05f) / fade;
        v.stopAfterFade = true;
    }

    void Release(Voice v)
    {
        v.source.Stop();
        v.source.clip = null;
        v.active = false;
        v.owner = null;
        v.cue = null;
        v.generation++;      // handle lama tidak berlaku lagi
    }

    // ---------------- musik ----------------
    public void PlayMusic(AudioId id)
    {
        if (id == AudioId.None || !TryCue(id, out AudioCue cue)) return;
        PlayMusic(cue);
    }

    // Crossfade dari musik yang sedang berjalan; cue yang sama dibiarkan
    public void PlayMusic(AudioCue cue)
    {
        if (cue == null) return;
        if (!PickClip(cue, out AudioClip clip))
        {
            if (settings.logMissingCues && warnedNoClips.Add(cue)) Debug.LogWarning($"[Audio] musik {cue.name} belum punya klip.", cue);
            return;
        }

        Voice current = musicCurrent >= 0 ? musicVoices[musicCurrent] : null;
        if (current != null && current.active && current.cue == cue && !current.stopAfterFade) return;

        float fade = settings.musicCrossfadeSeconds;
        if (current != null && current.active) BeginStop(current, fade);

        int next = musicCurrent == 0 ? 1 : 0;
        Voice v = musicVoices[next];
        if (v.active) Release(v);      // crossfade sebelumnya belum selesai: sumber ini dipakai ulang

        v.cue = cue;
        v.owner = null;
        v.bus = AudioBus.Music;
        v.active = true;
        v.generation++;
        v.startTime = Time.unscaledTime;
        v.baseVolume = cue.volume;
        v.basePitch = cue.pitch;
        v.userVolume = 1f;
        v.userPitch = 1f;
        v.stopAfterFade = false;
        v.fadeGain = fade > 0f ? 0f : 1f;
        v.fadeTarget = 1f;
        v.fadeRate = fade > 0f ? 1f / fade : 0f;

        AudioSource s = v.source;
        s.Stop();
        s.clip = clip;
        s.loop = true;
        s.priority = 0;
        s.spatialBlend = 0f;
        s.outputAudioMixerGroup = cue.mixerGroupOverride != null ? cue.mixerGroupOverride : musicGroup;
        ApplyVolume(v);
        s.pitch = v.basePitch;
        s.Play();

        musicCurrent = next;
    }

    public void StopMusic(float fade = -1f)
    {
        if (musicCurrent < 0) return;
        Voice v = musicVoices[musicCurrent];
        if (v.active) BeginStop(v, fade < 0f ? settings.musicCrossfadeSeconds : fade);
        musicCurrent = -1;
    }

    // ---------------- ducking dan bisu dunia ----------------
    // Musik mengecil sebesar amount (0..1) selama seconds, lalu naik pelan
    public void Duck(float amount, float seconds)
    {
        float target = Mathf.Clamp01(1f - amount);
        float now = Time.unscaledTime;
        duckTarget = now >= duckUntil ? target : Mathf.Min(duckTarget, target);
        duckUntil = Mathf.Max(duckUntil, now + Mathf.Max(0f, seconds));
    }

    // Marker hilang: bus World mengecil, dan loop dunia yang menandai stopWhenMuffled berhenti
    public void SetWorldMuffled(bool muffled)
    {
        if (worldMuffled == muffled) return;
        worldMuffled = muffled;
        if (!muffled) return;

        foreach (Voice v in voices)
            if (v.active && v.bus == AudioBus.World && v.cue.loop && v.cue.stopWhenMuffled) BeginStop(v, 0.15f);
    }

    // ---------------- per frame ----------------
    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (Time.unscaledTime >= duckUntil) duckTarget = 1f;
        duckGain = Mathf.MoveTowards(duckGain, duckTarget, (duckTarget < duckGain ? 6f : 1.5f) * dt);

        float muffleTarget = worldMuffled ? settings.worldMuffleGain : 1f;
        float muffleRate = Mathf.Abs(1f - settings.worldMuffleGain) / Mathf.Max(0.05f, settings.worldMuffleSeconds);
        muffleGain = Mathf.MoveTowards(muffleGain, muffleTarget, muffleRate * dt);

        for (int i = 0; i < voices.Count; i++)
        {
            Voice v = voices[i];
            if (!v.active) continue;

            if (!v.stopAfterFade && OwnerGone(v)) BeginStop(v, v.cue.fadeOut);

            v.fadeGain = v.fadeRate > 0f ? Mathf.MoveTowards(v.fadeGain, v.fadeTarget, v.fadeRate * dt) : v.fadeTarget;
            if (v.stopAfterFade && v.fadeGain <= 0.0001f) { Release(v); continue; }
            if (!v.cue.loop && !v.isMusic && !v.source.isPlaying && !AudioListener.pause) { Release(v); continue; }

            ApplyVolume(v);
            v.source.pitch = v.basePitch * v.userPitch;
        }
    }

    // Loop tanpa pemilik yang masih hidup dihentikan (halaman dinonaktifkan atau objek dihancurkan)
    static bool OwnerGone(Voice v)
    {
        if (!v.cue.loop || v.owner == null) return false;
        if (v.owner is UnityEngine.Object uo && uo == null) return true;
        if (v.owner is Component c) return !c.gameObject.activeInHierarchy;
        if (v.owner is GameObject g) return !g.activeInHierarchy;
        return false;
    }

    void ApplyVolume(Voice v)
    {
        float gain = v.baseVolume * v.userVolume * v.fadeGain * AudioVolumes.Effective(v.bus);
        if (v.bus == AudioBus.World) gain *= muffleGain;
        if (v.isMusic) gain *= duckGain;
        v.source.volume = Mathf.Clamp01(gain);
    }

    // ---------------- debug (klik kanan komponen saat Play) ----------------
    // Nada sintetis, jadi sistem bisa diuji tanpa aset audio apa pun
    AudioCue debugUi, debugLoop, debugMusicA, debugMusicB, debugDuck;

    static AudioClip Tone(string clipName, float freq, float seconds, bool wholeCycles)
    {
        const int rate = 44100;
        int cycles = Mathf.Max(1, Mathf.RoundToInt(freq * seconds));
        int samples = wholeCycles ? Mathf.RoundToInt(cycles * rate / freq) : Mathf.RoundToInt(seconds * rate);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float env = wholeCycles ? 1f : Mathf.Clamp01(Mathf.Min(i / (rate * 0.005f), (samples - i) / (rate * 0.03f)));
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / rate) * 0.35f * env;
        }
        var clip = AudioClip.Create(clipName, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioCue MakeCue(AudioClip clip, AudioBus bus, bool loop)
    {
        var cue = ScriptableObject.CreateInstance<AudioCue>();
        cue.name = "Debug " + clip.name;
        cue.bus = bus;
        cue.loop = loop;
        cue.cooldown = 0.02f;
        cue.maxVoices = 2;
        cue.fadeIn = loop ? 0.2f : 0f;
        cue.fadeOut = loop ? 0.3f : 0.05f;
        cue.clips = new[] { new AudioClipEntry { clip = clip } };
        return cue;
    }

    [ContextMenu("Debug: nada UI")]
    void DebugUi()
    {
        if (debugUi == null) debugUi = MakeCue(Tone("ui", 880f, 0.12f, false), AudioBus.Ui, false);
        Play(debugUi, this, null, 1f, 1f);
    }

    [ContextMenu("Debug: mulai loop dunia")]
    void DebugLoopStart()
    {
        if (debugLoop == null)
        {
            debugLoop = MakeCue(Tone("loopDunia", 220f, 0.5f, true), AudioBus.World, true);
            debugLoop.stopWhenMuffled = true;
        }
        Play(debugLoop, this, null, 1f, 1f);
    }

    [ContextMenu("Debug: hentikan loop dunia")]
    void DebugLoopStop() => StopAll(this, 0.3f);

    [ContextMenu("Debug: musik A")]
    void DebugMusicA()
    {
        if (debugMusicA == null) debugMusicA = MakeCue(Tone("musikA", 261.6f, 1f, true), AudioBus.Music, true);
        PlayMusic(debugMusicA);
    }

    [ContextMenu("Debug: musik B (crossfade dari A)")]
    void DebugMusicB()
    {
        if (debugMusicB == null) debugMusicB = MakeCue(Tone("musikB", 392f, 1f, true), AudioBus.Music, true);
        PlayMusic(debugMusicB);
    }

    [ContextMenu("Debug: musik berhenti")]
    void DebugMusicStop() => StopMusic();

    [ContextMenu("Debug: umpan balik (musik mengecil)")]
    void DebugDuck()
    {
        if (debugDuck == null)
        {
            debugDuck = MakeCue(Tone("umpanBalik", 660f, 0.6f, false), AudioBus.Feedback, false);
            debugDuck.duckAmount = 0.7f;
            debugDuck.duckSeconds = 1.5f;
        }
        Play(debugDuck, this, null, 1f, 1f);
    }

    [ContextMenu("Debug: bisukan dunia (hidup atau mati)")]
    void DebugMuffle() => SetWorldMuffled(!worldMuffled);

    [ContextMenu("Debug: master 30 persen")]
    void DebugMaster30() => AudioVolumes.SetMaster(0.3f);

    [ContextMenu("Debug: kembalikan volume ke Inspector")]
    void DebugVolumesReset()
    {
        AudioVolumes.ClearSaved();
        AudioVolumes.ResetToDefaults();
    }

    [ContextMenu("Debug: cetak status")]
    void DebugStatus()
    {
        int active = 0, loops = 0;
        foreach (Voice v in voices)
        {
            if (!v.active) continue;
            active++;
            if (v.cue.loop) loops++;
        }
        Debug.Log($"[Audio] suara aktif {active}/{voices.Count} (loop {loops}) | master {AudioVolumes.Master:0.00} mute {AudioVolumes.Muted} | "
                + $"musik {AudioVolumes.Get(AudioBus.Music):0.00}, ui {AudioVolumes.Get(AudioBus.Ui):0.00}, umpan balik {AudioVolumes.Get(AudioBus.Feedback):0.00}, "
                + $"dunia {AudioVolumes.Get(AudioBus.World):0.00} | duck {duckGain:0.00} muffle {muffleGain:0.00} (bisu={worldMuffled})", this);
    }
}