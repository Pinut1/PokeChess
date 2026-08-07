using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

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

    [Header("SFX 믹서 그룹 (선택 — 컴프레서/리미터로 겹침 시 음량 폭주 방지용)")]
    [Tooltip("Audio Mixer에서 SFX용 그룹을 만들고 여기 연결하면 _sfxSource.outputAudioMixerGroup에 반영된다. " +
             "비워두면 기존처럼 믹서 없이 바로 재생.")]
    [SerializeField] private AudioMixerGroup _sfxMixerGroup;

    private float _sfxVolume = DEFAULT_VOLUME;
    private float _bgmVolume = DEFAULT_VOLUME;
    private float _bgmEntryVolume = 1f; // 현재 재생 중인 BGM의 SoundCatalog 배율 — 슬라이더 조작 시에도 유지해야 함
    private Coroutine _bgmIntroCoroutine;

    public SoundCatalog Catalog => _catalog;

    private void OnEnable()  => GameEvents.OnBattleStart += HandleBattleStart;
    private void OnDisable() => GameEvents.OnBattleStart -= HandleBattleStart;

    private void HandleBattleStart() => PlayBgm(SoundId.BattleStart);

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

        if (_sfxMixerGroup != null && _sfxSource != null)
            _sfxSource.outputAudioMixerGroup = _sfxMixerGroup;
    }

    private void LoadAndApplySavedVolumes()
    {
        float masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, AudioListener.volume));
        _bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_BGM_VOLUME, DEFAULT_VOLUME));
        _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_SFX_VOLUME, DEFAULT_VOLUME));

        AudioListener.volume = masterVolume;

        ApplyBgmVolume();
    }

    /// <summary>옵션 슬라이더(_bgmVolume)와 카탈로그 항목별 배율(_bgmEntryVolume)을 곱해 실제 AudioSource에 반영한다.</summary>
    private void ApplyBgmVolume()
    {
        if (_bgmSource != null)
            _bgmSource.volume = _bgmVolume * _bgmEntryVolume;
    }

    // ─────────────────────────────────────────
    // BGM
    // ─────────────────────────────────────────

    /// <summary>
    /// 같은 클립이 이미 재생 중이면 이어서 재생(처음부터 다시 시작하지 않음), 다른 클립이면 교체.
    /// volumeMultiplier는 SoundCatalog의 항목별 볼륨 배율(0~1) — 옵션 슬라이더 값 위에 곱해진다.
    /// </summary>
    public void PlayBgm(AudioClip clip, bool loop = true, float volumeMultiplier = 1f)
    {
        if (clip == null) return;

        if (_bgmSource == null)
        {
            Debug.LogWarning("[SoundManager] BGM AudioSource 미연결 — 재생 스킵");
            return;
        }

        _bgmEntryVolume = Mathf.Clamp01(volumeMultiplier);
        ApplyBgmVolume();

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
        if (_bgmIntroCoroutine != null)
        {
            StopCoroutine(_bgmIntroCoroutine);
            _bgmIntroCoroutine = null;
        }

        if (_bgmSource == null) return;
        _bgmSource.Stop();
    }

    /// <summary>SoundId로 BGM 재생. 카탈로그에 등록된 항목별 볼륨 배율을 함께 적용한다.</summary>
    public void PlayBgm(SoundId id, bool loop = true)
    {
        if (id == SoundId.None) return;

        if (_catalog == null)
        {
            Debug.LogWarning("[SoundManager] SoundCatalog 미연결 — BGM 재생 스킵");
            return;
        }

        if (!_catalog.TryGetClip(id, out var clip, out var entryVolume))
        {
            Debug.LogWarning($"[SoundManager] SoundCatalog에 '{id}' 클립 없음 — BGM 재생 스킵");
            return;
        }

        PlayBgm(clip, loop, entryVolume);
    }

    /// <summary>
    /// 인트로 클립을 한 번(non-loop) 재생한 뒤, 끝나는 시점에 자동으로 루프 클립으로 넘어간다.
    /// (예: 타이틀 BGM — 오프닝이 있는 인트로 + 루프 파트가 분리된 소스일 때)
    /// </summary>
    public void PlayBgmWithIntro(SoundId introId, SoundId loopId)
    {
        if (_catalog == null)
        {
            Debug.LogWarning("[SoundManager] SoundCatalog 미연결 — BGM 재생 스킵");
            return;
        }

        if (!_catalog.TryGetClip(introId, out var introClip, out var introVolume) ||
            !_catalog.TryGetClip(loopId, out var loopClip, out var loopVolume))
        {
            Debug.LogWarning($"[SoundManager] '{introId}' 또는 '{loopId}' 클립 없음 — BGM 재생 스킵");
            return;
        }

        if (_bgmIntroCoroutine != null)
        {
            StopCoroutine(_bgmIntroCoroutine);
            _bgmIntroCoroutine = null;
        }

        PlayBgm(introClip, loop: false, volumeMultiplier: introVolume);
        _bgmIntroCoroutine = StartCoroutine(SwitchToLoopAfter(introClip.length, loopClip, loopVolume));
    }

    private IEnumerator SwitchToLoopAfter(float delay, AudioClip loopClip, float volumeMultiplier)
    {
        yield return new WaitForSecondsRealtime(delay);
        _bgmIntroCoroutine = null;
        PlayBgm(loopClip, loop: true, volumeMultiplier: volumeMultiplier);
    }

    // ─────────────────────────────────────────
    // SFX
    // ─────────────────────────────────────────

    public void PlaySfx(AudioClip clip) => PlaySfx(clip, 1f);

    /// <summary>volumeMultiplier는 SoundCatalog의 항목별 볼륨 배율(0~1) — 원본 클립마다 다른 녹음 크기를 맞추는 용도.</summary>
    public void PlaySfx(AudioClip clip, float volumeMultiplier)
    {
        if (clip == null) return;

        if (_sfxSource == null)
        {
            Debug.LogWarning("[SoundManager] SFX AudioSource 미연결 — 재생 스킵");
            return;
        }

        _sfxSource.PlayOneShot(clip, _sfxVolume * Mathf.Clamp01(volumeMultiplier));
    }

    /// <summary>SoundId로 SFX 재생. 카탈로그에 등록된 항목별 볼륨 배율을 함께 적용한다.</summary>
    public void PlaySfx(SoundId id)
    {
        if (id == SoundId.None) return;

        if (_catalog == null)
        {
            Debug.LogWarning("[SoundManager] SoundCatalog 미연결 — SFX 재생 스킵");
            return;
        }

        if (!_catalog.TryGetClip(id, out var clip, out var entryVolume))
        {
            Debug.LogWarning($"[SoundManager] SoundCatalog에 '{id}' 클립 없음 — SFX 재생 스킵");
            return;
        }

        PlaySfx(clip, entryVolume);
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
        _bgmVolume = Mathf.Clamp01(value);
        ApplyBgmVolume();
        PlayerPrefs.SetFloat(PREF_BGM_VOLUME, _bgmVolume);
    }

    public void SetSfxVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, _sfxVolume);
    }
}
