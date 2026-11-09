using System.Collections.Generic;
using UnityEngine;

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
    EventAlert
}

public static class AudioHelper
{
    private static AudioSource _audioSource;
    private static readonly Dictionary<SoundEffect, AudioClip> _clipCache = new();

    // Master Volume & Audio Control System:
    public static float MasterVolume { get; private set; } = 1.0f;
    public static bool IsMuted { get; private set; } = false;
    private static float _preMuteVolume = 1.0f;
    private static bool _volumeInitialized = false;

    public static void EnsureVolumeInitialized()
    {
        if (_volumeInitialized) return;
        _volumeInitialized = true;
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("LastBastion_MasterVolume", 1.0f));
        IsMuted = PlayerPrefs.GetInt("LastBastion_IsMuted", 0) == 1;
        _preMuteVolume = (MasterVolume > 0.05f) ? MasterVolume : 1.0f;
        AudioListener.volume = IsMuted ? 0f : MasterVolume;
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

            if (!_clipCache.TryGetValue(effect, out var clip) || clip == null)
            {
                if (effect == SoundEffect.SMGShoot)
                {
                    clip = Resources.Load<AudioClip>("Audio/SMGShoot");
                }
                else if (effect == SoundEffect.SMGGrenade1)
                {
                    clip = Resources.Load<AudioClip>("Audio/SMGGrenade1");
                }
                else if (effect == SoundEffect.SMGGrenade2)
                {
                    clip = Resources.Load<AudioClip>("Audio/SMGGrenade2");
                }
                else if (effect == SoundEffect.SniperShoot1)
                {
                    clip = Resources.Load<AudioClip>("Audio/SniperShoot1");
                }
                else if (effect == SoundEffect.SniperShoot2)
                {
                    clip = Resources.Load<AudioClip>("Audio/SniperShoot2");
                }
                else if (effect == SoundEffect.SniperShoot3)
                {
                    clip = Resources.Load<AudioClip>("Audio/SniperShoot3");
                }
                else if (effect == SoundEffect.SniperShoot4)
                {
                    clip = Resources.Load<AudioClip>("Audio/SniperShoot4");
                }
                else if (effect == SoundEffect.TankShoot1)
                {
                    clip = Resources.Load<AudioClip>("Audio/TankShoot1");
                }
                else if (effect == SoundEffect.TankShoot2)
                {
                    clip = Resources.Load<AudioClip>("Audio/TankShoot2");
                }
                else if (effect == SoundEffect.TankExplosionDebris)
                {
                    clip = Resources.Load<AudioClip>("Audio/TankExplosionDebris");
                }
                else if (effect == SoundEffect.ICBMExplosion)
                {
                    clip = Resources.Load<AudioClip>("Audio/ICBMExplosion");
                }

                if (clip == null)
                {
                    clip = GenerateProceduralClip(effect);
                    if (effect != SoundEffect.SMGShoot &&
                        effect != SoundEffect.SMGGrenade1 &&
                        effect != SoundEffect.SMGGrenade2 &&
                        effect != SoundEffect.SniperShoot1 &&
                        effect != SoundEffect.SniperShoot2 &&
                        effect != SoundEffect.SniperShoot3 &&
                        effect != SoundEffect.SniperShoot4 &&
                        effect != SoundEffect.TankShoot1 &&
                        effect != SoundEffect.TankShoot2 &&
                        effect != SoundEffect.TankExplosionDebris &&
                        effect != SoundEffect.ICBMExplosion)
                    {
                        _clipCache[effect] = clip;
                    }
                }
                else
                {
                    _clipCache[effect] = clip;
                }
            }

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
