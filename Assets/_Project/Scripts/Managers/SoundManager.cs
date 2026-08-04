using UnityEngine;

/// <summary>
/// 프로젝트 전역 BGM/SFX 재생 및 볼륨 관리.
/// GameManager는 씬 로컬(KeepAcrossScenes=false)이라 얹으면 씬 전환마다 재생성돼 BGM이 끊기므로,
/// Singleton 기본값(DontDestroyOnLoad)을 그대로 쓰는 독립 싱글턴으로 둔다.
/// 볼륨 PlayerPrefs 키는 OptionsPanelUI가 쓰던 것을 그대로 재사용한다 — 저장(PlayerPrefs.Save
/// 호출 타이밍)은 여전히 OptionsPanelUI(OnDisable/OnApplicationQuit)가 전담하고, 여기서는
/// SetFloat(다음 Save 때 함께 기록됨)과 실제 AudioSource/AudioListener 반영만 담당한다.
/// </summary>
public class SoundManager : Singleton<SoundManager>
{
    private const string PREF_MASTER_VOLUME = "MasterVolume";
    private const string PREF_BGM_VOLUME = "BgmVolume";
    private const string PREF_SFX_VOLUME = "SfxVolume";

    private const float DEFAULT_VOLUME = 1f;

    [Header("AudioSource (Inspector에서 연결)")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;

    [Header("사운드 카탈로그 (SoundId → AudioClip 매핑)")]
    [SerializeField] private SoundCatalog _catalog;

    private float _sfxVolume = DEFAULT_VOLUME;

    public SoundCatalog Catalog => _catalog;

    protected override void Awake()
    {
        // Singleton<T>.Awake()가 실행되기 전 시점에 이미 인스턴스가 있었는지를 먼저 캡처해 둔다.
        // true였다면 이 Awake 호출은 두 번째(이후) 인스턴스의 것이라는 뜻이고, base.Awake()가
        // 곧바로 이 gameObject를 Destroy(지연 파괴)하므로 아래 초기화를 건너뛰어야 한다.
        // Instance 게터(없으면 LogError를 남기는 조회용 프로퍼티)는 다시 조회하지 않는다.
        bool alreadyRegistered = HasInstance;

        base.Awake();
        if (alreadyRegistered) return; // 중복 인스턴스 — base.Awake()에서 이미 Destroy 처리됨

        LoadAndApplySavedVolumes();
    }

    private void LoadAndApplySavedVolumes()
    {
        float masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, AudioListener.volume));
        float bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_BGM_VOLUME, DEFAULT_VOLUME));
        _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_SFX_VOLUME, DEFAULT_VOLUME));

        AudioListener.volume = masterVolume;

        if (_bgmSource != null)
            _bgmSource.volume = bgmVolume;
    }

    // ─────────────────────────────────────────
    // BGM
    // ─────────────────────────────────────────

    /// <summary>같은 클립이 이미 재생 중이면 이어서 재생(처음부터 다시 시작하지 않음), 다른 클립이면 교체.</summary>
    public void PlayBgm(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;

        if (_bgmSource == null)
        {
            Debug.LogWarning("[SoundManager] BGM AudioSource 미연결 — 재생 스킵");
            return;
        }

        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
        {
            _bgmSource.loop = loop;
            return;
        }

        _bgmSource.clip = clip;
        _bgmSource.loop = loop;
        _bgmSource.Play();
    }

    public void StopBgm()
    {
        if (_bgmSource == null) return;
        _bgmSource.Stop();
    }

    /// <summary>SoundId로 BGM 재생. 실제 재생은 PlayBgm(AudioClip, bool)를 그대로 재사용 —
    /// 중복 재생 방지 등 기존 규칙이 그대로 적용된다.</summary>
    public void PlayBgm(SoundId id, bool loop = true)
    {
        if (id == SoundId.None) return;

        if (_catalog == null)
        {
            Debug.LogWarning("[SoundManager] SoundCatalog 미연결 — BGM 재생 스킵");
            return;
        }

        if (!_catalog.TryGetClip(id, out var clip))
        {
            Debug.LogWarning($"[SoundManager] SoundCatalog에 '{id}' 클립 없음 — BGM 재생 스킵");
            return;
        }

        PlayBgm(clip, loop);
    }

    // ─────────────────────────────────────────
    // SFX
    // ─────────────────────────────────────────

    public void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;

        if (_sfxSource == null)
        {
            Debug.LogWarning("[SoundManager] SFX AudioSource 미연결 — 재생 스킵");
            return;
        }

        _sfxSource.PlayOneShot(clip, _sfxVolume);
    }

    /// <summary>SoundId로 SFX 재생. 실제 재생은 PlaySfx(AudioClip)를 그대로 재사용한다.</summary>
    public void PlaySfx(SoundId id)
    {
        if (id == SoundId.None) return;

        if (_catalog == null)
        {
            Debug.LogWarning("[SoundManager] SoundCatalog 미연결 — SFX 재생 스킵");
            return;
        }

        if (!_catalog.TryGetClip(id, out var clip))
        {
            Debug.LogWarning($"[SoundManager] SoundCatalog에 '{id}' 클립 없음 — SFX 재생 스킵");
            return;
        }

        PlaySfx(clip);
    }

    // ─────────────────────────────────────────
    // 볼륨
    // ─────────────────────────────────────────

    public void SetMasterVolume(float value)
    {
        float clamped = Mathf.Clamp01(value);
        AudioListener.volume = clamped;
        PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, clamped);
    }

    public void SetBgmVolume(float value)
    {
        float clamped = Mathf.Clamp01(value);

        if (_bgmSource != null)
            _bgmSource.volume = clamped;

        PlayerPrefs.SetFloat(PREF_BGM_VOLUME, clamped);
    }

    public void SetSfxVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, _sfxVolume);
    }
}
