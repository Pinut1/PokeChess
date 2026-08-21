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
/// DefaultExecutionOrder(-100): 유니티는 서로 다른 오브젝트의 Awake 순서를 보장하지 않는다.
/// TitleScreenUI.Awake()가 SoundManager.TryGet보다 먼저 돌면 인스턴스가 아직 없어 BGM 호출이
/// 조용히(로그도 없이) 무시되므로, 다른 스크립트보다 확실히 먼저 초기화되도록 강제한다.
/// </summary>
[DefaultExecutionOrder(-100)]
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

    // PlaySfxForSeconds 전용 소스·코루틴. 공용 _sfxSource를 멈추면 다른 효과음까지 끊기므로 분리한다.
    private AudioSource _timedSfxSource;
    private Coroutine _timedSfxCoroutine;
    private float _bgmVolume = DEFAULT_VOLUME;
    private float _bgmEntryVolume = 1f; // 현재 재생 중인 BGM의 SoundCatalog 배율 — 슬라이더 조작 시에도 유지해야 함
    private Coroutine _bgmIntroCoroutine;

    /// <summary>
    /// 포켓몬 Voice 공용 슬롯이 다음에 비는 시각(Time.unscaledTime 기준) — 구매/성급진화/일반진화/
    /// 진화의 돌/통신진화 등 PlayPokemonVoice를 거치는 모든 Pokemon Voice가 이 값 하나를 공유한다.
    /// "요청이 들어온 그 순간"(TryReserveVoiceSlot 호출 시점)에 즉시 이 값을 갱신해 슬롯을 선점한다 —
    /// 실제 오디오 재생이 시작되는 시점을 기다리지 않는다. 그래야 구매처럼 다른 SFX(UnitBuy)가 끝나길
    /// 기다리는 대기 구간에 들어온 다른 Voice 요청도, 그 대기 구간까지 포함해 정확히 걸러진다.
    /// 일반 SFX(PlaySfx/PlaySfx(SoundId))는 이 값을 전혀 참조하지 않으므로 영향받지 않는다.
    /// </summary>
    private float _voiceSlotFreeAt = -1f;

    public SoundCatalog Catalog => _catalog;

    private void OnEnable()
    {
        GameEvents.OnBattleStart += HandleBattleStart;

        // 포켓몬 울음소리 — 구매 완료(OnPokemonPurchased)와 진화 완료(OnUnitEvolved, 성급/돌/통신교환 공용
        // 단일 진입점) 양쪽 모두 여기 한 곳에서만 재생한다. 각 구매/진화 코드에 재생 호출을 복붙하지 않기
        // 위해 SoundManager가 직접 이 두 이벤트를 구독한다(OnBattleStart와 동일한 기존 패턴 재사용).
        GameEvents.OnPokemonPurchased += HandlePokemonPurchased;
        GameEvents.OnUnitEvolved      += HandleUnitEvolved;
    }

    private void OnDisable()
    {
        GameEvents.OnBattleStart -= HandleBattleStart;
        GameEvents.OnPokemonPurchased -= HandlePokemonPurchased;
        GameEvents.OnUnitEvolved      -= HandleUnitEvolved;
    }

    private void HandleBattleStart() => PlayBgm(SoundId.BattleStart);

    /// <summary>
    /// 구매 Voice 경로. TFT 관찰 동작대로 "UnitBuy SFX가 끝난 뒤 Voice"를 구현하되, 슬롯 자체는
    /// 구매가 발생한 이 순간 바로 선점한다(TryReserveVoiceSlot — UnitBuy 대기 구간까지 포함해서
    /// 선점해야, 그 대기 도중 들어온 다른 Voice 요청도 정확히 걸러진다). 이미 슬롯이 점유 중이면
    /// 코루틴조차 시작하지 않고 조용히 버린다 — 큐잉하지 않는다.
    /// UnitBuy SFX 자체(즉시 재생)는 ShopCardUI가 별도로 처리하므로 여기서 다시 재생하지 않는다.
    /// 구매 판정/배치(ShopManager.Buy 등)는 이미 끝난 뒤 발행되는 GameEvents.PokemonPurchased를
    /// 그대로 받으므로, 여기서 재생 시점만 늦춰도 구매 자체는 전혀 지연되지 않는다.
    /// </summary>
    private void HandlePokemonPurchased(PokemonData data)
    {
        if (data == null || data.voiceClip == null) return;

        float buyDelay = 0f;
        if (_catalog != null && _catalog.TryGetClip(SoundId.UnitBuy, out var buyClip, out _) && buyClip != null)
            buyDelay = buyClip.length;

        // UnitBuy 대기 + Voice 재생, 두 구간 전체를 지금 이 순간 한 번에 선점한다.
        if (!TryReserveVoiceSlot(buyDelay + data.voiceClip.length)) return;

        StartCoroutine(PlayReservedVoiceAfterDelay(data, buyDelay));
    }

    /// <summary>delay(UnitBuy 클립 길이)만큼 기다린 뒤 재생한다. 슬롯은 HandlePokemonPurchased가 이미
    /// 선점해 뒀으므로 여기서 다시 확인·점유하지 않고 곧바로 PlaySfx만 호출한다(PlayPokemonVoice를
    /// 다시 타면 그 안의 재점유 로직과 충돌한다 — 이미 내 몫으로 선점된 시간에 "점유 중"으로 걸려
    /// 스스로를 막아버리기 때문).</summary>
    private IEnumerator PlayReservedVoiceAfterDelay(PokemonData data, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        PlaySfx(data.voiceClip);
    }

    /// <summary>진화 완료 시점의 유닛 — unit.data는 이미 최종(진화 후) 종으로 스왑된 상태다
    /// (BoardManager.ExecuteMerge/PokemonUnit.TryEquipStone/NetworkManager 통신진화 수신부 공통).
    /// 재생 시점 자체는 기존과 동일하게 즉시(지연 없이) 시도한다 — PlayPokemonVoice 내부에서 공용
    /// 슬롯이 이미 점유 중이면 이 진화 Voice만 스킵되고, 진화 판정/이펙트/SFX 등 다른 처리는
    /// 전부 별개 구독자(BoardManager/UnitEvolveVfx 등)가 그대로 처리하므로 영향받지 않는다.</summary>
    private void HandleUnitEvolved(PokemonUnit unit, bool isStarUp) => PlayPokemonVoice(unit != null ? unit.data : null);

    /// <summary>슬롯이 비어 있으면(Time.unscaledTime이 _voiceSlotFreeAt를 지났으면) occupySeconds만큼
    /// 즉시 선점하고 true, 이미 점유 중이면 false(스킵). PlayPokemonVoice(즉시 재생 경로)와
    /// HandlePokemonPurchased(지연 재생 경로) 양쪽이 공유하는 유일한 판정 지점이다.</summary>
    private bool TryReserveVoiceSlot(float occupySeconds)
    {
        if (Time.unscaledTime < _voiceSlotFreeAt) return false;
        _voiceSlotFreeAt = Time.unscaledTime + occupySeconds;
        return true;
    }

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

    /// <summary>
    /// 포켓몬 울음소리 공통 재생 진입점 — 구매·성급진화·일반진화·진화의 돌·통신진화 등 "지금 당장"
    /// 재생을 시도하는 모든 경로가 이 메서드 하나만 부르면 공용 Voice 슬롯 정책이 자동 적용된다.
    /// PokemonData.voiceClip을 직접 재생한다(SoundId/SoundCatalog 미경유 — 140종을 SoundId enum에
    /// 늘어놓지 않기 위해 종 데이터에 직접 물린 AudioClip을 쓴다).
    /// data 또는 voiceClip이 비어 있으면(음원 미매칭 등) 조용히 무시한다.
    ///
    /// 슬롯이 이미 점유 중이면(다른 Pokemon Voice가 대기·재생 중) 이번 요청은 스킵한다 — 큐잉하지
    /// 않는다. 구매만 예외로 "UnitBuy SFX가 끝날 때까지" 선행 대기가 필요해 HandlePokemonPurchased가
    /// 직접 TryReserveVoiceSlot으로 미리 선점해두고, 대기가 끝나면 이 메서드를 거치지 않고 PlaySfx만
    /// 호출한다(그 시점엔 이미 자기 몫으로 선점된 구간이라 여기서 다시 재확인하면 스스로 막힘).
    /// </summary>
    public void PlayPokemonVoice(PokemonData data)
    {
        if (data == null || data.voiceClip == null) return;
        if (!TryReserveVoiceSlot(data.voiceClip.length)) return; // 슬롯 점유 중 — 이 Voice는 스킵

        PlaySfx(data.voiceClip);
    }

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

    /// <summary>
    /// SFX를 지정한 길이만큼만 재생하고 끝에서 페이드아웃한다. 원본이 필요한 길이보다 긴
    /// 팡파레·징글에 쓴다(예: 30초 음원에서 앞 7초만).
    ///
    /// PlayOneShot을 안 쓰는 이유: 재생 핸들이 없어 중간에 멈출 방법이 없다. 그렇다고 공용
    /// _sfxSource를 멈추면 그 위에서 울리던 다른 효과음까지 같이 끊긴다. 그래서 전용 소스를
    /// 하나 따로 만들어 쓴다(같은 SFX 볼륨·믹서 그룹을 따른다).
    ///
    /// 끝을 뚝 자르면 어색하므로 마지막 fadeSeconds 동안 볼륨을 줄여 끝낸다.
    /// seconds가 클립 길이보다 길면 클립이 끝나는 대로 자연히 끝난다.
    /// </summary>
    public void PlaySfxForSeconds(SoundId id, float seconds, float fadeSeconds = 1f)
    {
        if (id == SoundId.None || seconds <= 0f) return;

        if (_catalog == null)
        {
            Debug.LogWarning("[SoundManager] SoundCatalog 미연결 — SFX 재생 스킵");
            return;
        }

        if (!_catalog.TryGetClip(id, out var clip, out var entryVolume) || clip == null)
        {
            Debug.LogWarning($"[SoundManager] SoundCatalog에 '{id}' 클립 없음 — SFX 재생 스킵");
            return;
        }

        EnsureTimedSfxSource();

        if (_timedSfxCoroutine != null) StopCoroutine(_timedSfxCoroutine);

        float volume = _sfxVolume * Mathf.Clamp01(entryVolume);
        _timedSfxSource.clip   = clip;
        _timedSfxSource.volume = volume;
        _timedSfxSource.Play();

        _timedSfxCoroutine = StartCoroutine(FadeOutTimedSfx(seconds, fadeSeconds, volume));
    }

    /// <summary>PlaySfxForSeconds 전용 소스를 만든다(공용 _sfxSource와 분리 — 위 주석 참고).</summary>
    private void EnsureTimedSfxSource()
    {
        if (_timedSfxSource != null) return;

        _timedSfxSource = gameObject.AddComponent<AudioSource>();
        _timedSfxSource.playOnAwake = false;
        _timedSfxSource.loop = false;

        if (_sfxMixerGroup != null) _timedSfxSource.outputAudioMixerGroup = _sfxMixerGroup;
    }

    /// <summary>seconds에서 fadeSeconds를 뺀 지점부터 볼륨을 줄여 정지한다.</summary>
    private IEnumerator FadeOutTimedSfx(float seconds, float fadeSeconds, float startVolume)
    {
        fadeSeconds = Mathf.Clamp(fadeSeconds, 0f, seconds);

        float holdSeconds = seconds - fadeSeconds;
        if (holdSeconds > 0f) yield return new WaitForSecondsRealtime(holdSeconds);

        float elapsed = 0f;
        while (elapsed < fadeSeconds && _timedSfxSource != null && _timedSfxSource.isPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            _timedSfxSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeSeconds);
            yield return null;
        }

        if (_timedSfxSource != null)
        {
            _timedSfxSource.Stop();
            _timedSfxSource.volume = startVolume; // 다음 재생을 위해 되돌린다
        }

        _timedSfxCoroutine = null;
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
