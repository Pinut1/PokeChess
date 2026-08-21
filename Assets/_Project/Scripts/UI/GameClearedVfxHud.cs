using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 챕터 완주(최종 라운드 클리어) 순간 카메라 앞에 폭죽 VFX를 터뜨리는 연출.
///
/// <b>왜 월드 공간에 띄우는가</b> — 씬의 Canvas가 Screen Space Overlay 하나뿐이라 UI는 항상
/// 최상단에 그려진다. 그래서 파티클을 월드에 두기만 하면 <b>자동으로 모달 뒤</b>로 들어간다.
/// 렌더 순서를 맞추려고 전용 카메라·RenderTexture를 팔 필요가 없다(관전 화면이 쓰는 그 구조).
///
/// 위치는 카메라 기준이 아니라 <b>고정 월드 좌표</b>다. 이 게임은 카메라가 보드를 내려다보는
/// 고정 시점이라, 보드 기준으로 한 번 맞춰두면 그대로 유지되는 편이 조절하기 쉽다.
///
/// <see cref="GameEvents.OnGameCleared"/>만 듣는다. 패배(SessionEnded)에는 반응하지 않는다 —
/// 완주 방송과 게임오버가 경쟁하다 순서가 뒤집혀도 폭죽이 패배 화면에 겹치지 않도록,
/// 게임오버로 이미 넘어간 뒤에 도착한 완주 신호는 무시한다(RoundPhaseManager.HandleGameCleared와 같은 규칙).
///
/// ⚠️ <b>항상 활성인 오브젝트</b>(Canvas나 그 아래 상시 켜진 오브젝트)에 붙일 것 — 꺼져 있으면
/// 이벤트를 못 받는다. 프리팹이 안 물려 있으면 아무 일도 하지 않고 조용히 넘어간다(경고 1회).
/// </summary>
public class GameClearedVfxHud : MonoBehaviour
{
    [Header("VFX")]
    [Tooltip("터뜨릴 파티클 프리팹. ⚠️ 반드시 Project 창의 프리팹 에셋을 물릴 것 — " +
             "씬에 올려둔 오브젝트를 물리면 그 오브젝트 자체가 게임 내내 재생되거나(켜둔 경우), " +
             "복제본이 꺼진 채 생긴다(꺼둔 경우). 비워두면 연출을 건너뛴다.")]
    [SerializeField] private GameObject _vfxPrefab;

    [Tooltip("여러 개를 물리면 매번 무작위로 골라 터뜨린다. 비어 있으면 위 프리팹만 쓴다.")]
    [SerializeField] private GameObject[] _vfxVariants;

    [Header("터지는 위치")]
    [Tooltip("폭죽이 터질 월드 좌표. 보드 아래에서 위로 솟구치도록 y를 내려 잡는다.")]
    [SerializeField] private Vector3 _spawnPosition = new(0f, -9f, 0f);

    [Tooltip("프리팹 회전(오일러). SF_Rainbow처럼 위로 쏘는 프리팹은 x=-90으로 세워야 한다.")]
    [SerializeField] private Vector3 _spawnEuler = new(-90f, 0f, 0f);

    [Tooltip("터질 때마다 위 좌표에서 이만큼 무작위로 흩뿌린다(월드 단위). " +
             "(0,0,0)이면 매번 같은 자리에서 터진다.")]
    [SerializeField] private Vector3 _positionSpread = new(4f, 0f, 4f);

    [Header("성공 사운드")]
    [Tooltip("완주 순간 한 번 재생할 성공 팡파레(SoundId). None이면 소리 없이 연출만 나온다. " +
             "SoundCatalog에 해당 id의 클립이 등록돼 있어야 한다. 길이는 상관없다 — " +
             "타이틀로 나가면 그 시점에 끊긴다.")]
    [SerializeField] private SoundId _clearSound = SoundId.Victory;

    [Tooltip("팡파레를 울리기 전에 배경음을 멈출지. 켜면 팡파레만 깔끔하게 들리고, " +
             "끄면 돌고 있던 BGM 위에 겹쳐 울린다. 완주 모달에서 나가는 길이 [타이틀로 이동]뿐이라 " +
             "타이틀 BGM이 그때 다시 시작되므로, 멈춰도 되돌릴 필요가 없다.")]
    [SerializeField] private bool _stopBgm = true;

    [Tooltip("음원 앞부분을 몇 초만 재생할지. 원본이 이보다 짧으면 클립이 끝나는 대로 끝난다.")]
    [SerializeField] private float _playSeconds = 7f;

    [Tooltip("끝에서 몇 초 동안 볼륨을 줄이며 끝낼지. 0이면 뚝 끊긴다 — " +
             "중간을 잘라 쓰는 음원이라 약간이라도 주는 편이 자연스럽다.")]
    [SerializeField] private float _fadeOutSeconds = 1f;

    [Header("터지는 방식")]
    [Tooltip("총 몇 번 터뜨릴지.")]
    [Range(1, 30)]
    [SerializeField] private int _burstCount = 8;

    [Tooltip("터짐 사이 간격(초). 0이면 한 번에 전부 터진다.")]
    [SerializeField] private float _interval = 0.22f;

    [Tooltip("각 파티클을 몇 초 뒤에 파괴할지. 프리팹의 재생 길이보다 넉넉하게 준다.")]
    [SerializeField] private float _lifetime = 4f;

    [Tooltip("파티클 크기 배율 하한/상한. 매번 이 사이에서 무작위로 뽑아 크기를 다르게 한다.")]
    [SerializeField] private Vector2 _scaleRange = new(0.8f, 1.4f);

    // 재생 중 만든 인스턴스. 씬을 벗어나거나 컴포넌트가 꺼질 때 남지 않게 직접 정리한다.
    private readonly List<GameObject> _spawned = new();

    private Coroutine _routine;

    // 한 판에 한 번만. 완주 신호가 중복 도착해도(RPC 재도달 등) 두 번 터지지 않는다.
    private bool _played;

    private void OnEnable()
    {
        GameEvents.OnGameCleared  += HandleGameCleared;
        GameEvents.OnRoundChanged += HandleRoundChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnGameCleared  -= HandleGameCleared;
        GameEvents.OnRoundChanged -= HandleRoundChanged;

        StopAndClear();
    }

    /// <summary>새 판이 시작되면 다시 터질 수 있게 1회 가드를 푼다(QA 재시작 포함).</summary>
    private void HandleRoundChanged(int round)
    {
        if (round <= 1) _played = false;
    }

    private void HandleGameCleared()
    {
        if (_played) return;

        // 이미 게임오버로 넘어간 뒤에 도착한 완주 신호는 무시한다 — 패배 화면 위에 폭죽이
        // 겹치지 않도록(RoundPhaseManager.HandleGameCleared와 같은 판단 기준).
        if (GameManager.TryGet(out var gm) && gm.Phase != null &&
            gm.Phase.CurrentPhase == GamePhase.GameOver)
            return;

        WarnIfSceneObject(_vfxPrefab);
        if (_vfxVariants != null)
            foreach (GameObject variant in _vfxVariants) WarnIfSceneObject(variant);

        if (_vfxPrefab == null && (_vfxVariants == null || _vfxVariants.Length == 0))
        {
            Debug.LogWarning("[GameClearedVfx] VFX 프리팹이 안 물려 있어 완주 연출을 건너뜁니다.", this);
            return;
        }

        _played = true;

        PlayClearSound();

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(PlayBursts());
    }

    /// <summary>
    /// 완주 성공 팡파레를 한 번 재생한다(PlayOneShot).
    ///
    /// <b>BGM 채널을 쓰지 않는 이유</b> — PlayBgm은 BGM 소스의 클립 자체를 이 팡파레로 바꿔버린다.
    /// 그러면 곡이 끝난 뒤 BGM 상태가 "팡파레가 걸린 채 멈춤"으로 남아, 이후 BGM을 다루는 쪽이
    /// 타이틀 BGM이 돌고 있다고 가정하면 어긋난다. 여긴 그 순간 한 번 울리는 소리면 충분하므로
    /// SFX 채널의 PlayOneShot으로 얹어 BGM 상태를 그대로 둔다.
    ///
    /// 원본이 필요한 길이보다 길어도 된다 — <see cref="_playSeconds"/>만큼만 틀고 끝에서
    /// 페이드아웃한다(SoundManager.PlaySfxForSeconds). 뚝 자르면 어색해서 약간 줄이며 끝낸다.
    ///
    /// 배경음은 <see cref="_stopBgm"/>이 켜져 있으면 울리기 직전에 멈춘다(겹쳐 들리지 않게).
    /// 멈춘 뒤 되돌리지 않는 이유는 완주 모달에서 나가는 길이 [타이틀로 이동]뿐이고,
    /// 그 시점에 타이틀 BGM이 다시 시작되기 때문이다.
    ///
    /// SoundManager는 자체 싱글톤이라 SoundManager.TryGet으로 조회한다
    /// (UnitDragController가 효과음을 낼 때 쓰는 것과 같은 방식). Singleton.Instance 널 검사 금지.
    /// </summary>
    private void PlayClearSound()
    {
        if (!SoundManager.TryGet(out var sound)) return;

        // 팡파레가 없어도(_clearSound == None) BGM 정지는 그대로 적용한다 — 소리를 끄고 싶어서
        // 비워둔 경우에 배경음만 계속 도는 게 더 어색하다.
        if (_stopBgm) sound.StopBgm();

        if (_clearSound == SoundId.None) return;

        sound.PlaySfxForSeconds(_clearSound, _playSeconds, _fadeOutSeconds);
    }

    private IEnumerator PlayBursts()
    {
        for (int i = 0; i < _burstCount; i++)
        {
            SpawnOne();

            if (_interval > 0f) yield return new WaitForSecondsRealtime(_interval);
        }

        _routine = null;
    }

    /// <summary>지정한 월드 좌표 주변에 파티클 하나를 띄운다.</summary>
    private void SpawnOne()
    {
        GameObject prefab = PickPrefab();
        if (prefab == null) return;

        Vector3 position = _spawnPosition + new Vector3(
            Random.Range(-_positionSpread.x, _positionSpread.x),
            Random.Range(-_positionSpread.y, _positionSpread.y),
            Random.Range(-_positionSpread.z, _positionSpread.z));

        GameObject instance = Instantiate(prefab, position, Quaternion.Euler(_spawnEuler));

        // 원본이 꺼진 상태여도 복제본은 켠다 — Instantiate는 활성 상태까지 그대로 복사하기 때문에,
        // 씬에 꺼둔 오브젝트를 물렸을 때 폭죽이 꺼진 채로 생겨 아무것도 안 보이는 일이 있었다.
        instance.SetActive(true);

        float scale = Random.Range(_scaleRange.x, _scaleRange.y);
        instance.transform.localScale = prefab.transform.localScale * scale;

        _spawned.Add(instance);

        // 타임스케일이 0이어도 정리되도록 Realtime 기준으로 파괴한다(모달이 게임을 멈춰도 안전).
        StartCoroutine(DestroyAfter(instance, _lifetime));
    }

    /// <summary>
    /// 씬에 올려둔 오브젝트를 프리팹 칸에 물렸는지 알려준다. 흔한 실수인데 증상이 헷갈린다 —
    /// 켜둔 채 물리면 그 오브젝트가 게임 내내 재생되고, 꺼둔 채 물리면 아무것도 안 보인다.
    /// Project 창의 프리팹 에셋은 scene이 유효하지 않아 이 검사로 구분된다.
    /// </summary>
    private void WarnIfSceneObject(GameObject candidate)
    {
        if (candidate == null || !candidate.scene.IsValid()) return;

        Debug.LogWarning(
            $"[GameClearedVfx] '{candidate.name}'은 씬에 올려둔 오브젝트입니다 — Project 창의 " +
            "프리팹 에셋을 물려 주세요. 씬 오브젝트를 물리면 켜둔 경우 게임 내내 재생되고, " +
            "꺼둔 경우 복제본이 꺼진 채 생깁니다.", this);
    }

    private GameObject PickPrefab()
    {
        if (_vfxVariants != null && _vfxVariants.Length > 0)
        {
            GameObject picked = _vfxVariants[Random.Range(0, _vfxVariants.Length)];
            if (picked != null) return picked;
        }

        return _vfxPrefab;
    }

    private IEnumerator DestroyAfter(GameObject instance, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        _spawned.Remove(instance);
        if (instance != null) Destroy(instance);
    }

    /// <summary>재생을 멈추고 남아 있는 파티클을 전부 지운다.</summary>
    private void StopAndClear()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        for (int i = _spawned.Count - 1; i >= 0; i--)
            if (_spawned[i] != null) Destroy(_spawned[i]);

        _spawned.Clear();
    }
}
