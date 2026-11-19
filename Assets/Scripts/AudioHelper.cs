using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public enum SoundEffect
{
    Shoot,
    SMGShoot,
    SMGGrenade1,
    SMGGrenade2,
    ExplosionSubBass,
    SniperShoot,
    SniperShoot1,
    SniperShoot2,
    SniperShoot3,
    SniperShoot4,
    TankShoot1,
    TankShoot2,
    TankExplosionDebris,
    ICBMExplosion,
    Hit,
    ZombieDeath,
    BuildingPlace,
    BuildingDemolish,
    ResourceGain,
    WaveStart,
    WaveVictory,
    GameOver,
    ButtonClick,
    EventAlert,
    ZombieHit,
    GuardianStomp,
    GuardianPunch,
    B2FlyoverWhoosh,
    B2BombDropping
}

public static class AudioHelper
{
    private struct CustomClipSpec
    {
        public SoundEffect Effect;
        public string[] FileNames;
        public float TrimThreshold;

        public CustomClipSpec(SoundEffect effect, float trimThreshold, params string[] fileNames)
        {
            Effect = effect;
            TrimThreshold = trimThreshold;
            FileNames = fileNames;
        }
    }

    private sealed class AudioPreloadRunner : MonoBehaviour { }

    private static readonly CustomClipSpec[] _customClipSpecs = new CustomClipSpec[]
    {
        new CustomClipSpec(SoundEffect.SMGShoot, 0f, "SMGShoot.mp3", "SMGShoot.wav"),
        new CustomClipSpec(SoundEffect.SMGGrenade1, 0f, "SMGGrenade1.mp3", "SMGGrenade1.wav"),
        new CustomClipSpec(SoundEffect.SMGGrenade2, 0f, "SMGGrenade2.mp3", "SMGGrenade2.wav"),
        new CustomClipSpec(SoundEffect.SniperShoot1, 0f, "SniperShoot1.mp3", "SniperShoot1.wav"),
        new CustomClipSpec(SoundEffect.SniperShoot2, 0f, "SniperShoot2.mp3", "SniperShoot2.wav"),
        new CustomClipSpec(SoundEffect.SniperShoot3, 0f, "SniperShoot3.mp3", "SniperShoot3.wav"),
        new CustomClipSpec(SoundEffect.SniperShoot4, 0f, "SniperShoot4.mp3", "SniperShoot4.wav"),
        new CustomClipSpec(SoundEffect.TankShoot1, 0f, "TankShoot1.mp3", "TankShoot1.wav"),
        new CustomClipSpec(SoundEffect.TankShoot2, 0f, "TankShoot2.mp3", "TankShoot2.wav"),
        new CustomClipSpec(SoundEffect.TankExplosionDebris, 0f, "TankExplosionDebris.mp3", "TankExplosionDebris.wav"),
        new CustomClipSpec(SoundEffect.ICBMExplosion, 0f, "ICBMExplosion.mp3", "ICBMExplosion.wav"),
        new CustomClipSpec(SoundEffect.GuardianStomp, 0.035f, "freesound_community-stomp2-93279.mp3", "GuardianStomp.mp3", "GuardianStomp.wav"),
        new CustomClipSpec(SoundEffect.GuardianPunch, 0.035f, "soraatwod-punch-416719.wav", "GuardianPunch.wav", "soraatwod-punch-416719.mp3", "GuardianPunch.mp3"),
        new CustomClipSpec(SoundEffect.B2FlyoverWhoosh, 0.020f, "floraphonic-flying-drone-whoosh-1-185324.mp3"),
        new CustomClipSpec(SoundEffect.B2BombDropping, 0.020f, "freesound_community-bomb-dropping-101151.mp3")
    };

    private static AudioSource _audioSource;
    private static readonly Dictionary<SoundEffect, AudioClip> _clipCache = new();
    private static readonly Dictionary<SoundEffect, CustomClipSpec> _specLookup = BuildSpecLookup();
    private static AudioPreloadRunner _preloadRunner;
    private static bool _preloadStarted = false;

    // Master Volume & Audio Control System:
    public static float MasterVolume { get; private set; } = 1.0f;
    public static bool IsMuted { get; private set; } = false;
    private static float _preMuteVolume = 1.0f;
    private static bool _volumeInitialized = false;

    private static Dictionary<SoundEffect, CustomClipSpec> BuildSpecLookup()
    {
        var map = new Dictionary<SoundEffect, CustomClipSpec>(_customClipSpecs.Length);
        foreach (var spec in _customClipSpecs)
        {
            map[spec.Effect] = spec;
        }
        return map;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _audioSource = null;
        _clipCache.Clear();
        _preloadRunner = null;
        _preloadStarted = false;
        _volumeInitialized = false;
        _lastICBMExplosionTime = -999f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoPreloadOnStartup()
    {
        EnsureVolumeInitialized();
    }

    public static void EnsureVolumeInitialized()
    {
        if (!_volumeInitialized)
        {
            _volumeInitialized = true;
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("LastBastion_MasterVolume", 1.0f));
            IsMuted = PlayerPrefs.GetInt("LastBastion_IsMuted", 0) == 1;
            _preMuteVolume = (MasterVolume > 0.05f) ? MasterVolume : 1.0f;
            AudioListener.volume = IsMuted ? 0f : MasterVolume;
        }

        EnsurePreloadStarted();
    }

    private static string GetExternalAudioDirectory()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, "StreamingAudio~");
#else
        return Path.Combine(Application.streamingAssetsPath, "Audio");
#endif
    }

    private static void EnsurePreloadStarted()
    {
        if (_preloadStarted || !Application.isPlaying) return;
        _preloadStarted = true;

        if (_preloadRunner == null)
        {
            var go = GameObject.Find("AudioManager");
            if (go == null)
            {
                go = new GameObject("AudioManager");
                Object.DontDestroyOnLoad(go);
            }
            _preloadRunner = go.GetComponent<AudioPreloadRunner>();
            if (_preloadRunner == null)
            {
                _preloadRunner = go.AddComponent<AudioPreloadRunner>();
            }
        }

        foreach (var spec in _customClipSpecs)
        {
            if (_clipCache.TryGetValue(spec.Effect, out var existing) && existing != null) continue;

            // Load WAV synchronously first if present, otherwise stream/decode via UnityWebRequestMultimedia
            if (!TryLoadSyncFromDisk(spec, out AudioClip syncClip))
            {
                _preloadRunner.StartCoroutine(LoadClipCoroutine(spec));
            }
            else if (syncClip != null)
            {
                _clipCache[spec.Effect] = ApplyOptionalTrim(syncClip, spec.TrimThreshold);
            }
        }
    }

    private static bool TryLoadSyncFromDisk(CustomClipSpec spec, out AudioClip clip)
    {
        clip = null;
        string dir = GetExternalAudioDirectory();
        if (string.IsNullOrEmpty(dir)) return false;

        foreach (string fileName in spec.FileNames)
        {
            if (!fileName.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)) continue;
            string fullPath = Path.Combine(dir, fileName);
            if (!File.Exists(fullPath)) continue;

            clip = LoadWavFromFile(fullPath, Path.GetFileNameWithoutExtension(fileName));
            if (clip != null) return true;
        }

        return false;
    }

    private static IEnumerator LoadClipCoroutine(CustomClipSpec spec)
    {
        string dir = GetExternalAudioDirectory();
        if (string.IsNullOrEmpty(dir)) yield break;

        foreach (string fileName in spec.FileNames)
        {
            string fullPath = Path.Combine(dir, fileName);
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS
            if (!File.Exists(fullPath)) continue;
#endif
            AudioType audioType = GetAudioType(fileName);
            string uri = BuildFileUri(fullPath);

            using (var request = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
            {
                if (request.downloadHandler is DownloadHandlerAudioClip audioHandler)
                {
                    // Keep uncompressed PCM in memory so CreateInstantClip/GetData works cleanly
                    audioHandler.compressed = false;
                    audioHandler.streamAudio = false;
                }

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    AudioClip loaded = DownloadHandlerAudioClip.GetContent(request);
                    if (loaded != null && loaded.loadState == AudioDataLoadState.Loaded)
                    {
                        loaded.name = Path.GetFileNameWithoutExtension(fileName);
                        AudioClip processed = ApplyOptionalTrim(loaded, spec.TrimThreshold);
                        if (spec.Effect == SoundEffect.B2FlyoverWhoosh)
                        {
                            processed = AmplifyAndNormalizeClip(processed, 2.8f);
                        }
                        else if (spec.Effect == SoundEffect.B2BombDropping)
                        {
                            processed = AmplifyAndNormalizeClip(processed, 1.45f);
                        }
                        _clipCache[spec.Effect] = processed;
                        yield break;
                    }
                }
                else
                {
                    Debug.LogWarning($"[AudioHelper] Failed loading '{fileName}' via UnityWebRequest: {request.error}");
                }
            }
        }
    }

    private static AudioType GetAudioType(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        switch (ext)
        {
            case ".wav": return AudioType.WAV;
            case ".ogg": return AudioType.OGGVORBIS;
            default: return AudioType.MPEG;
        }
    }

    private static string BuildFileUri(string path)
    {
        if (path.Contains("://")) return path;
        return new System.Uri(Path.GetFullPath(path)).AbsoluteUri;
    }

    private static AudioClip ApplyOptionalTrim(AudioClip clip, float threshold)
    {
        if (clip != null && threshold > 0f)
        {
            return CreateInstantClip(clip, threshold);
        }
        return clip;
    }

    /// <summary>
    /// Pure C# PCM WAV decoder so .wav files load synchronously without Unity's external FSBTool64.exe.
    /// </summary>
    private static AudioClip LoadWavFromFile(string filePath, string clipName)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes.Length < 44) return null;

            // Verify RIFF/WAVE header
            if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F' ||
                bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
            {
                return null;
            }

            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            int audioFormat = 0;
            int dataOffset = -1;
            int dataSize = 0;

            int pos = 12;
            while (pos + 8 <= bytes.Length)
            {
                string chunkId = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                int chunkSize = System.BitConverter.ToInt32(bytes, pos + 4);
                pos += 8;

                if (chunkSize < 0 || pos + chunkSize > bytes.Length) break;

                if (chunkId == "fmt " && chunkSize >= 16)
                {
                    audioFormat = System.BitConverter.ToUInt16(bytes, pos);
                    channels = System.BitConverter.ToUInt16(bytes, pos + 2);
                    sampleRate = System.BitConverter.ToInt32(bytes, pos + 4);
                    bitsPerSample = System.BitConverter.ToUInt16(bytes, pos + 14);
                }
                else if (chunkId == "data")
                {
                    dataOffset = pos;
                    dataSize = chunkSize;
                    break;
                }

                pos += chunkSize + (chunkSize & 1); // RIFF chunks are word-aligned
            }

            if (dataOffset < 0 || channels <= 0 || sampleRate <= 0) return null;
            // Support PCM (1) and IEEE float (3)
            if (audioFormat != 1 && audioFormat != 3) return null;

            int bytesPerSample = bitsPerSample / 8;
            if (bytesPerSample <= 0) return null;

            int totalSampleValues = dataSize / bytesPerSample;
            int frameCount = totalSampleValues / channels;
            if (frameCount <= 0) return null;

            float[] samples = new float[totalSampleValues];
            if (audioFormat == 3 && bitsPerSample == 32)
            {
                for (int i = 0; i < totalSampleValues; i++)
                {
                    samples[i] = System.BitConverter.ToSingle(bytes, dataOffset + i * 4);
                }
            }
            else if (bitsPerSample == 16)
            {
                for (int i = 0; i < totalSampleValues; i++)
                {
                    short val = System.BitConverter.ToInt16(bytes, dataOffset + i * 2);
                    samples[i] = val / 32768f;
                }
            }
            else if (bitsPerSample == 24)
            {
                for (int i = 0; i < totalSampleValues; i++)
                {
                    int idx = dataOffset + i * 3;
                    int val = bytes[idx] | (bytes[idx + 1] << 8) | ((sbyte)bytes[idx + 2] << 16);
                    samples[i] = val / 8388608f;
                }
            }
            else if (bitsPerSample == 8)
            {
                for (int i = 0; i < totalSampleValues; i++)
                {
                    samples[i] = (bytes[dataOffset + i] - 128) / 128f;
                }
            }
            else
            {
                return null;
            }

            AudioClip clip = AudioClip.Create(clipName, frameCount, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AudioHelper] Failed to parse WAV '{filePath}': {ex.Message}");
            return null;
        }
    }

    public static AudioClip GetClip(SoundEffect effect)
    {
        if (_clipCache.TryGetValue(effect, out var cached) && cached != null) return cached;

        EnsurePreloadStarted();

        if (_specLookup.TryGetValue(effect, out var spec))
        {
            if (TryLoadSyncFromDisk(spec, out AudioClip syncClip) && syncClip != null)
            {
                syncClip = ApplyOptionalTrim(syncClip, spec.TrimThreshold);
                _clipCache[effect] = syncClip;
                return syncClip;
            }

            // Optional fallback if imported assets exist in Resources/Audio
            foreach (string fileName in spec.FileNames)
            {
                string resName = Path.GetFileNameWithoutExtension(fileName);
                AudioClip resClip = Resources.Load<AudioClip>("Audio/" + resName);
                if (resClip != null)
                {
                    resClip = ApplyOptionalTrim(resClip, spec.TrimThreshold);
                    _clipCache[effect] = resClip;
                    return resClip;
                }
            }

            // While async MP3 preload is still in flight, return procedural fallback without caching it
            return GenerateProceduralClip(effect);
        }

        AudioClip procClip = GenerateProceduralClip(effect);
        if (procClip != null)
        {
            _clipCache[effect] = procClip;
        }
        return procClip;
    }

    public static void SetMasterVolume(float volume)
    {
        EnsureVolumeInitialized();
        MasterVolume = Mathf.Clamp01(volume);
        if (MasterVolume > 0.001f && IsMuted)
        {
            IsMuted = false;
            PlayerPrefs.SetInt("LastBastion_IsMuted", 0);
        }
        PlayerPrefs.SetFloat("LastBastion_MasterVolume", MasterVolume);
        PlayerPrefs.Save();
        AudioListener.volume = IsMuted ? 0f : MasterVolume;
    }

    public static void AdjustMasterVolume(float delta)
    {
        EnsureVolumeInitialized();
        if (IsMuted && delta > 0f)
        {
            ToggleMute();
            return;
        }
        float target = Mathf.Clamp01(Mathf.Round((MasterVolume + delta) * 10f) / 10f);
        SetMasterVolume(target);
    }

    public static void ToggleMute()
    {
        EnsureVolumeInitialized();
        IsMuted = !IsMuted;
        if (IsMuted)
        {
            _preMuteVolume = (MasterVolume > 0.05f) ? MasterVolume : 1.0f;
            AudioListener.volume = 0f;
        }
        else
        {
            if (MasterVolume <= 0.001f)
            {
                MasterVolume = (_preMuteVolume > 0.05f) ? _preMuteVolume : 0.8f;
                PlayerPrefs.SetFloat("LastBastion_MasterVolume", MasterVolume);
            }
            AudioListener.volume = MasterVolume;
        }
        PlayerPrefs.SetInt("LastBastion_IsMuted", IsMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ICBM Nuclear Audio Ducking System:
    // When ICBM explodes, all other weapon gunfire sounds are heavily ducked for a period
    // so the devastating magnitude and rolling shockwave of the blast take complete prominence.
    private static float _lastICBMExplosionTime = -999f;
    private const float ICBM_DUCK_HOLD_DURATION = 3.0f;    // 3 seconds of maximum ducking (blast & fireball)
    private const float ICBM_DUCK_RECOVER_DURATION = 3.0f; // 3 seconds smooth fade back to normal volume
    private const float ICBM_DUCK_MIN_VOLUME = 0.18f;      // Faint background gunshots (~18% normal volume)

    public static void TriggerICBMDucking()
    {
        _lastICBMExplosionTime = Time.time;
    }

    public static float GetGunfireVolumeMultiplier()
    {
        float elapsed = Time.time - _lastICBMExplosionTime;
        if (elapsed < 0f || elapsed >= (ICBM_DUCK_HOLD_DURATION + ICBM_DUCK_RECOVER_DURATION))
        {
            return 1.0f;
        }

        if (elapsed < ICBM_DUCK_HOLD_DURATION)
        {
            return ICBM_DUCK_MIN_VOLUME;
        }

        float t = (elapsed - ICBM_DUCK_HOLD_DURATION) / ICBM_DUCK_RECOVER_DURATION;
        return Mathf.Lerp(ICBM_DUCK_MIN_VOLUME, 1.0f, t);
    }

    private static bool IsGunfireEffect(SoundEffect effect)
    {
        switch (effect)
        {
            case SoundEffect.Shoot:
            case SoundEffect.SMGShoot:
            case SoundEffect.SniperShoot:
            case SoundEffect.SniperShoot1:
            case SoundEffect.SniperShoot2:
            case SoundEffect.SniperShoot3:
            case SoundEffect.SniperShoot4:
            case SoundEffect.TankShoot1:
            case SoundEffect.TankShoot2:
            case SoundEffect.SMGGrenade1:
            case SoundEffect.SMGGrenade2:
            case SoundEffect.TankExplosionDebris:
            case SoundEffect.GuardianStomp:
            case SoundEffect.GuardianPunch:
                return true;
            default:
                return false;
        }
    }

    public static void Initialize(AudioSource source)
    {
        EnsureVolumeInitialized();
        _audioSource = source;
        if (_audioSource != null)
        {
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }
    }

    public static void PlaySound(SoundEffect effect, float volume = 0.6f)
    {
        try
        {
            EnsureVolumeInitialized();
            if (effect == SoundEffect.ICBMExplosion)
            {
                TriggerICBMDucking();
            }

            if (IsGunfireEffect(effect))
            {
                volume *= GetGunfireVolumeMultiplier();
            }

            if (effect == SoundEffect.SniperShoot)
            {
                PlayRandomSniperShoot(volume * 0.65f);
                return;
            }

            if (_audioSource == null)
            {
                var go = GameObject.Find("AudioManager");
                if (go == null)
                {
                    go = new GameObject("AudioManager");
                    Object.DontDestroyOnLoad(go);
                }
                _audioSource = go.GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = go.AddComponent<AudioSource>();
                }
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
            }

            AudioClip clip = GetClip(effect);

            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip, volume);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AudioHelper] Failed to play sound: {ex.Message}");
        }
    }

    /// <summary>
    /// Randomly plays one of the custom SMG grenade explosion sounds with heavy, powerful impact and sub-bass shockwave
    /// </summary>
    public static void PlayRandomSMGGrenade(float volume = 1.0f)
    {
        SoundEffect effect = (Random.value < 0.5f) ? SoundEffect.SMGGrenade1 : SoundEffect.SMGGrenade2;
        PlaySound(effect, volume);
        PlaySound(SoundEffect.ExplosionSubBass, Mathf.Clamp01(volume * 0.85f));
    }

    /// <summary>
    /// Randomly plays one of the 4 custom sniper rifle gunshot sounds (lowered volume for pleasant audio balance)
    /// </summary>
    public static void PlayRandomSniperShoot(float volume = 0.52f)
    {
        SoundEffect[] clips = new SoundEffect[]
        {
            SoundEffect.SniperShoot1,
            SoundEffect.SniperShoot2,
            SoundEffect.SniperShoot3,
            SoundEffect.SniperShoot4
        };
        SoundEffect effect = clips[Random.Range(0, clips.Length)];
        PlaySound(effect, volume);
    }

    /// <summary>
    /// Randomly plays one of the 2 heavy tank cannon firing sounds (50% chance between TankShoot1 and TankShoot2)
    /// </summary>
    public static void PlayRandomTankShoot(float volume = 0.95f)
    {
        SoundEffect effect = (Random.value < 0.5f) ? SoundEffect.TankShoot1 : SoundEffect.TankShoot2;
        PlaySound(effect, volume);
    }

    /// <summary>
    /// Plays Guardian normal stomp attack sound (freesound_community-stomp2-93279)
    /// </summary>
    public static void PlayGuardianStomp(float volume = 0.85f)
    {
        PlaySound(SoundEffect.GuardianStomp, volume);
    }

    /// <summary>
    /// Plays Guardian shield push ability punch sound (soraatwod-punch-416719)
    /// </summary>
    public static void PlayGuardianPunch(float volume = 0.95f)
    {
        PlaySound(SoundEffect.GuardianPunch, volume);
    }

    /// <summary>
    /// Trims leading dead-air or silence from an audio clip so it starts playing immediately with zero latency.
    /// </summary>
    private static AudioClip CreateInstantClip(AudioClip original, float threshold = 0.035f)
    {
        if (original == null) return null;

        try
        {
            int channels = original.channels;
            int freq = original.frequency;
            float[] allSamples = new float[original.samples * channels];
            original.GetData(allSamples, 0);

            int attackSample = -1;
            for (int i = 0; i < allSamples.Length; i++)
            {
                if (Mathf.Abs(allSamples[i]) > threshold)
                {
                    attackSample = i / channels;
                    break;
                }
            }

            // Only trim if there is noticeable leading silence (> 0.015s = 15ms)
            if (attackSample < Mathf.RoundToInt(freq * 0.015f))
            {
                return original;
            }

            // Leave tiny 3ms pre-roll to preserve natural attack transient
            int preRoll = Mathf.RoundToInt(freq * 0.003f);
            int startSample = Mathf.Max(0, attackSample - preRoll);
            int newFrames = original.samples - startSample;
            if (newFrames <= 0) return original;

            float[] trimmed = new float[newFrames * channels];
            System.Array.Copy(allSamples, startSample * channels, trimmed, 0, trimmed.Length);

            // 2ms micro fade-in at sample 0 to guarantee zero click/pop
            int fadeFrames = Mathf.RoundToInt(freq * 0.002f);
            for (int f = 0; f < fadeFrames && f < newFrames; f++)
            {
                float t = (float)f / fadeFrames;
                for (int c = 0; c < channels; c++)
                {
                    trimmed[f * channels + c] *= t;
                }
            }

            AudioClip trimmedClip = AudioClip.Create($"{original.name}_Instant", newFrames, channels, freq, false);
            trimmedClip.SetData(trimmed, 0);
            float removedSec = (float)startSample / freq;
            Debug.Log($"[AudioHelper] Trimmed {removedSec:F4}s leading silence from '{original.name}'! Sound now triggers INSTANTLY at 0.000s.");
            return trimmedClip;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AudioHelper] Failed to trim clip {original.name}: {ex.Message}");
            return original;
        }
    }

    /// <summary>
    /// Normalizes peak amplitude and applies soft-limited gain boost so quieter aircraft clips cut clearly through combat SFX.
    /// </summary>
    private static AudioClip AmplifyAndNormalizeClip(AudioClip original, float gainBoost)
    {
        if (original == null) return null;
        try
        {
            int channels = original.channels;
            int freq = original.frequency;
            float[] samples = new float[original.samples * channels];
            original.GetData(samples, 0);

            float peak = 0.001f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = Mathf.Abs(samples[i]);
                if (a > peak) peak = a;
            }

            float normGain = (0.95f / peak) * Mathf.Max(1f, gainBoost);
            for (int i = 0; i < samples.Length; i++)
            {
                float v = samples[i] * normGain;
                // Smooth soft saturation (tanh) prevents harsh digital clipping while maximizing perceived loudness
                samples[i] = (float)System.Math.Tanh(v) * 0.98f;
            }

            AudioClip boosted = AudioClip.Create($"{original.name}_Boosted", original.samples, channels, freq, false);
            boosted.SetData(samples, 0);
            return boosted;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AudioHelper] Failed to amplify clip {original.name}: {ex.Message}");
            return original;
        }
    }

    private static AudioClip GenerateProceduralClip(SoundEffect effect)
    {
        const int sampleRate = 44100;
        float duration;
        System.Func<float, float> waveGenerator;

        switch (effect)
        {
            case SoundEffect.Shoot:
            case SoundEffect.SMGShoot:
                duration = 0.07f;
                waveGenerator = t =>
                {
                    float f = Mathf.Lerp(880f, 240f, t / duration);
                    float noise = (Random.value * 2f - 1f) * 0.25f;
                    return (Mathf.Sin(2f * Mathf.PI * f * t) + noise) * (1f - (t / duration));
                };
                break;

            case SoundEffect.SMGGrenade1:
            case SoundEffect.SMGGrenade2:
                duration = 0.45f;
                waveGenerator = t =>
                {
                    float norm = t / duration;
                    float boomFreq = Mathf.Lerp(130f, 32f, Mathf.Pow(norm, 0.4f));
                    float boom = Mathf.Sin(2f * Mathf.PI * boomFreq * t) * Mathf.Exp(-t * 8f);
                    float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 10f) * 0.75f;
                    return Mathf.Clamp(boom + noise, -1f, 1f);
                };
                break;

            case SoundEffect.ExplosionSubBass:
                duration = 0.38f;
                waveGenerator = t =>
                {
                    float norm = t / duration;
                    // Heavy 48Hz -> 20Hz sub-bass shockwave thump
                    float f = Mathf.Lerp(48f, 20f, norm);
                    float env = Mathf.Exp(-t * 8.5f);
                    float sub = Mathf.Sin(2f * Mathf.PI * f * t) * env;
                    return Mathf.Clamp(sub * 1.2f, -1f, 1f);
                };
                break;

            case SoundEffect.SniperShoot:
            case SoundEffect.SniperShoot1:
            case SoundEffect.SniperShoot2:
            case SoundEffect.SniperShoot3:
            case SoundEffect.SniperShoot4:
                duration = 0.42f;
                waveGenerator = t =>
                {
                    float norm = t / duration;
                    // 1. Sharp supersonic rifle crack at transient (0..0.025s)
                    float crackEnv = Mathf.Exp(-t * 115f);
                    float crack = (Random.value * 2f - 1f) * crackEnv * 1.1f;

                    // 2. Heavy .50-caliber cannon thump (155Hz -> 36Hz)
                    float bodyFreq = Mathf.Lerp(155f, 36f, Mathf.Pow(norm, 0.35f));
                    float bodyEnv = Mathf.Exp(-t * 13f);
                    float bodyBoom = Mathf.Sin(2f * Mathf.PI * bodyFreq * t) * bodyEnv * 0.95f;

                    // 3. Reverberating gunfire echo tail + metallic bolt ring
                    float tailEnv = Mathf.Exp(-t * 6.5f) * (1f - norm);
                    float tailNoise = (Random.value * 2f - 1f) * tailEnv * 0.48f;
                    float boltClick = (t > 0.18f && t < 0.22f) ? Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.25f : 0f;

                    return Mathf.Clamp(crack + bodyBoom + tailNoise + boltClick, -1f, 1f);
                };
                break;

            case SoundEffect.TankShoot1:
            case SoundEffect.TankShoot2:
                duration = 0.50f;
                waveGenerator = t =>
                {
                    float norm = t / duration;
                    float boomFreq = Mathf.Lerp(130f, 26f, Mathf.Pow(norm, 0.35f));
                    float boom = Mathf.Sin(2f * Mathf.PI * boomFreq * t) * Mathf.Exp(-t * 8.5f);
                    float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 10f) * 0.85f;
                    return Mathf.Clamp(boom + noise, -1f, 1f);
                };
                break;

            case SoundEffect.TankExplosionDebris:
                duration = 0.60f;
                waveGenerator = t =>
                {
                    float norm = t / duration;
                    float boomFreq = Mathf.Lerp(115f, 24f, Mathf.Pow(norm, 0.4f));
                    float boom = Mathf.Sin(2f * Mathf.PI * boomFreq * t) * Mathf.Exp(-t * 7f);
                    float debris = (Random.value * 2f - 1f) * Mathf.Exp(-t * 3.8f) * 0.65f;
                    return Mathf.Clamp(boom + debris, -1f, 1f);
                };
                break;

            case SoundEffect.ICBMExplosion:
                duration = 3.5f;
                waveGenerator = t =>
                {
                    float env = Mathf.Exp(-t * 1.2f);
                    float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(65f, 20f, t / duration) * t);
                    float rumble = (Random.value * 2f - 1f) * Mathf.Exp(-t * 2.0f);
                    return Mathf.Clamp(sub * 0.75f + rumble * 0.5f, -1f, 1f) * env;
                };
                break;

            case SoundEffect.Hit:
                duration = 0.05f;
                waveGenerator = t =>
                {
                    float env = 1f - (t / duration);
                    return (Random.value * 2f - 1f) * env;
                };
                break;

            case SoundEffect.ZombieHit:
                duration = 0.05f;
                waveGenerator = t =>
                {
                    float norm = t / duration;
                    float env = 1f - norm;
                    float lowPunch = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 50f, norm) * t) * env;
                    float softNoise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 30f) * 0.25f;
                    return Mathf.Clamp(lowPunch * 0.6f + softNoise, -1f, 1f);
                };
                break;

            case SoundEffect.GuardianStomp:
                duration = 0.28f;
                waveGenerator = t =>
                {
                    float norm = t / duration;
                    float f = Mathf.Lerp(110f, 35f, norm);
                    float env = Mathf.Exp(-t * 11f);
                    float sub = Mathf.Sin(2f * Mathf.PI * f * t) * env;
                    float thud = (Random.value * 2f - 1f) * Mathf.Exp(-t * 16f) * 0.5f;
                    return Mathf.Clamp(sub + thud, -1f, 1f);
                };
                break;

            case SoundEffect.GuardianPunch:
                duration = 0.35f;
                waveGenerator = t =>
                {
                    float norm = t / duration;
                    float f = Mathf.Lerp(170f, 40f, Mathf.Pow(norm, 0.4f));
                    float boom = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 8.5f);
                    float impact = (Random.value * 2f - 1f) * Mathf.Exp(-t * 14f) * 0.75f;
                    return Mathf.Clamp(boom + impact, -1f, 1f);
                };
                break;

            case SoundEffect.ZombieDeath:
                duration = 0.22f;
                waveGenerator = t =>
                {
                    float f = Mathf.Lerp(260f, 50f, t / duration);
                    float env = Mathf.Pow(1f - (t / duration), 2);
                    float noise = (Random.value * 2f - 1f) * 0.4f;
                    return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f + noise) * env;
                };
                break;

            case SoundEffect.BuildingPlace:
                duration = 0.12f;
                waveGenerator = t =>
                {
                    float f = Mathf.Lerp(260f, 520f, t / duration);
                    float env = 1f - (t / duration);
                    return Mathf.Sin(2f * Mathf.PI * f * t) * env;
                };
                break;

            case SoundEffect.BuildingDemolish:
                duration = 0.16f;
                waveGenerator = t =>
                {
                    float env = 1f - (t / duration);
                    float noise = (Random.value * 2f - 1f) * 0.85f;
                    return noise * env;
                };
                break;

            case SoundEffect.ResourceGain:
                duration = 0.18f;
                waveGenerator = t =>
                {
                    float f = (t < duration * 0.5f) ? 587.33f : 880f;
                    float env = 1f - (t / duration);
                    return Mathf.Sin(2f * Mathf.PI * f * t) * env;
                };
                break;

            case SoundEffect.WaveStart:
                duration = 0.4f;
                waveGenerator = t =>
                {
                    float f = Mathf.Lerp(140f, 320f, t / duration);
                    float env = Mathf.Sin(Mathf.PI * (t / duration));
                    return Mathf.Sin(2f * Mathf.PI * f * t) * env;
                };
                break;

            case SoundEffect.WaveVictory:
                duration = 0.38f;
                waveGenerator = t =>
                {
                    float note = (t < 0.12f) ? 523.25f : (t < 0.24f ? 659.25f : 783.99f);
                    float env = 1f - ((t % 0.12f) / 0.12f);
                    return Mathf.Sin(2f * Mathf.PI * note * t) * env;
                };
                break;

            case SoundEffect.GameOver:
                duration = 0.6f;
                waveGenerator = t =>
                {
                    float f = Mathf.Lerp(280f, 60f, t / duration);
                    float env = 1f - (t / duration);
                    return Mathf.Sin(2f * Mathf.PI * f * t) * env;
                };
                break;

            case SoundEffect.ButtonClick:
                duration = 0.04f;
                waveGenerator = t =>
                {
                    float env = 1f - (t / duration);
                    return Mathf.Sin(2f * Mathf.PI * 1200f * t) * env;
                };
                break;

            case SoundEffect.EventAlert:
                duration = 0.25f;
                waveGenerator = t =>
                {
                    float f = (t < 0.12f) ? 440f : 660f;
                    float env = 1f - ((t % 0.12f) / 0.12f);
                    return Mathf.Sin(2f * Mathf.PI * f * t) * env;
                };
                break;

            case SoundEffect.B2FlyoverWhoosh:
                duration = 2.0f;
                waveGenerator = t =>
                {
                    float norm = t / duration;
                    float env = Mathf.Sin(Mathf.PI * norm);
                    float noise = (Random.value * 2f - 1f) * 0.45f;
                    float hum = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 65f, norm) * t) * 0.35f;
                    return (noise + hum) * env;
                };
                break;

            case SoundEffect.B2BombDropping:
                duration = 0.65f;
                waveGenerator = t =>
                {
                    float norm = t / duration;
                    float f = Mathf.Lerp(950f, 340f, norm);
                    float env = Mathf.Clamp01(norm * 3f);
                    return Mathf.Sin(2f * Mathf.PI * f * t) * 0.4f * env;
                };
                break;

            default:
                return null;
        }

        int totalSamples = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            samples[i] = Mathf.Clamp(waveGenerator(t), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create($"SFX_{effect}", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
