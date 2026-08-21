using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨업 순간에 화살표 스프라이트가 위로 떠오르며 사라지는 연출.
/// 유닛을 따라다니는 <see cref="StarUpPopupHud"/>와 달리 화면상 고정 위치(레벨 표시 옆 등)에서만 뜨므로,
/// 월드 좌표 변환 없이 인스펙터에 꽂아둔 원본 Image의 anchoredPosition에서 그대로 시작한다.
///
/// <b>한 번에 2레벨이 오르는 경우</b>(1라운드에서 XP 구매 → Lv1→Lv3)에는 화살표가
/// <see cref="_stagger"/> 간격으로 2개 연달아 올라간다. ShopManager.TryLevelUp()이 while 루프로
/// <b>레벨 1칸당 OnLevelChanged를 한 번씩</b> 쏘기 때문에 이벤트 자체가 이미 2번 오고,
/// 다만 둘 다 같은 프레임에 도착하므로 여기서 큐에 쌓아 시간차를 준다.
///
/// ⚠️ 재접속 복원(ShopManager.RestoreProgressionState)도 같은 OnLevelChanged를 쓴다 — 플레이어가
/// 목격하지 않은 레벨이라 연출하면 안 된다. 그래서 복원은 <b>증가폭으로 추측하지 않고</b>
/// <see cref="GameEvents.OnProgressionRestored"/>로 직접 구분한다. 복원은 항상 여러 칸을 뛴다고 가정하면
/// 자리를 비운 사이 <b>정확히 한 칸만</b> 오른 뒤 재접속했을 때 +1로 보여 연출이 잘못 재생된다.
/// ShopManager가 복원 신호를 LevelChanged보다 <b>먼저</b> 쏘므로, 여기서 기준값을 미리 당겨두면
/// 뒤따르는 LevelChanged는 증가폭 0이 되어 자연히 걸러진다.
/// </summary>
public class LevelUpArrowHud : MonoBehaviour
{
    [Header("아트")]
    [Tooltip("올라갈 화살표 Image의 RectTransform. 이 오브젝트가 곧 '시작 위치'이자 복제 원본이다. " +
             "씬에 원하는 위치로 배치해두면 되고, 실행 중에는 자동으로 비활성화된다(복제본만 보인다). " +
             "부모에 Layout Group이 붙어 있으면 위치를 뺏기므로 Layout 없는 오브젝트 아래에 둘 것.")]
    [SerializeField] private RectTransform _arrowTemplate;

    [Header("연출")]
    [Tooltip("떠오르는 거리(UI 픽셀).")]
    [SerializeField] private float _riseDistance = 60f;

    [Tooltip("화살표 하나가 뜨고 사라지기까지의 시간(초).")]
    [SerializeField] private float _duration = 0.8f;

    [Tooltip("이 비율까지는 완전히 불투명하게 유지하고, 남은 구간에서 서서히 사라진다(0~1).")]
    [Range(0f, 1f)]
    [SerializeField] private float _holdRatio = 0.35f;

    [Tooltip("시간에 따른 상승 곡선. 기본값은 처음 빠르게 올라가다 끝에서 느려지는 형태.")]
    [SerializeField] private AnimationCurve _riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("연속 레벨업")]
    [Tooltip("한 번에 여러 레벨이 오를 때 화살표 사이의 간격(초). 0이면 겹쳐서 한 개처럼 보인다.")]
    [SerializeField] private float _stagger = 0.18f;

    // 재생 중인 화살표 하나.
    private sealed class Active
    {
        public RectTransform rect;
        public CanvasGroup   group;
        public float         elapsed;
    }

    private readonly List<Active> _active = new();
    private readonly Stack<Active> _pool = new();

    private Transform _spawnParent;
    private Vector2 _startAnchoredPos;

    // 아직 띄우지 않고 대기 중인 화살표 수. 같은 프레임에 도착한 레벨업들을 시간차로 풀어주는 큐다.
    private int _pending;

    // 마지막으로 화살표를 띄운 뒤 지난 시간. 큰 값으로 시작해 첫 화살표는 지연 없이 바로 뜬다.
    private float _sinceLastSpawn = float.MaxValue;

    // 직전에 표시한 레벨. -1은 "아직 기준값 없음" — 최초 통지는 연출 없이 기준만 잡는다.
    private int _lastLevel = -1;

    private void Awake()
    {
        if (_arrowTemplate == null)
        {
            Debug.LogWarning("[LevelUpArrowHud] 화살표 원본(_arrowTemplate)이 비어 있어 연출이 동작하지 않는다.", this);
            return;
        }

        _spawnParent = _arrowTemplate.parent;
        _startAnchoredPos = _arrowTemplate.anchoredPosition;

        // 원본은 위치·크기 기준일 뿐이라 화면에는 보이지 않아야 한다.
        _arrowTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.OnLevelChanged        += HandleLevelChanged;
        GameEvents.OnProgressionRestored += HandleProgressionRestored;
        SyncBaseline();
    }

    private void OnDisable()
    {
        GameEvents.OnLevelChanged        -= HandleLevelChanged;
        GameEvents.OnProgressionRestored -= HandleProgressionRestored;

        // 비활성화되는 동안 재생 중이던 화살표는 정리한다 — 다시 켰을 때 멈춰 있던 잔상이 남지 않도록.
        for (int i = _active.Count - 1; i >= 0; i--) Release(i);
        _pending = 0;

        // 꺼져 있는 동안의 레벨 변화는 못 받으므로 기준값을 버린다. 다시 켤 때 현재 레벨로 새로 잡지
        // 않으면 그 사이 오른 만큼이 다음 레벨업에 더해져 "+2 점프"로 오인돼 연출이 통째로 걸러진다.
        _lastLevel = -1;
    }

    // ShopManager.Start()가 이 컴포넌트의 OnEnable보다 늦게 돌 수 있어 한 번 더 기준을 맞춘다.
    private void Start() => SyncBaseline();

    /// <summary>
    /// 현재 레벨을 기준값으로만 받아둔다(연출 없음). 이벤트를 놓친 채 시작해도
    /// 첫 레벨업이 "여러 칸 점프"로 오해되지 않게 하려는 것이다.
    /// GameManager는 프로젝트 규칙대로 TryGet으로만 조회한다(Singleton.Instance 널 검사 금지).
    /// </summary>
    private void SyncBaseline()
    {
        if (_lastLevel >= 0) return;
        if (!GameManager.TryGet(out var gm) || gm.Shop == null) return;

        _lastLevel = gm.Shop.CurrentLevel;
    }

    /// <summary>
    /// 재접속 복원 — 플레이어가 보지 못한 레벨 변화이므로 연출 없이 기준값만 새로 잡는다.
    /// 이 신호는 뒤따를 OnLevelChanged보다 먼저 오므로(ShopManager.RestoreProgressionState),
    /// 여기서 기준을 맞춰두면 그 LevelChanged는 증가폭 0이 되어 걸러진다.
    /// </summary>
    private void HandleProgressionRestored(int level, int currentXp) => _lastLevel = level;

    private void HandleLevelChanged(int level)
    {
        if (_lastLevel < 0)
        {
            _lastLevel = level; // 최초 통지 = 기준값
            return;
        }

        int delta = level - _lastLevel;
        _lastLevel = level;

        // 진짜 레벨업은 ShopManager가 한 칸씩 쏘므로 항상 +1이다. 복원은 위에서 이미 기준을 맞춰
        // 증가폭 0으로 들어오고, 그 밖의 점프(+2 이상)나 하락도 목격한 레벨업이 아니라 연출하지 않는다.
        if (delta != 1) return;

        _pending++;
    }

    private void Update()
    {
        // 일시정지(Time.timeScale=0)에서도 연출이 끝까지 재생되도록 unscaled를 쓴다.
        float dt = Time.unscaledDeltaTime;

        _sinceLastSpawn += dt;

        if (_pending > 0 && _sinceLastSpawn >= _stagger)
        {
            _pending--;
            _sinceLastSpawn = 0f;
            Spawn();
        }

        if (_active.Count == 0) return;

        float duration = Mathf.Max(0.01f, _duration);

        // 뒤에서부터 순회 — 도중에 끝난 항목을 바로 제거해도 인덱스가 밀리지 않는다.
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var a = _active[i];
            a.elapsed += dt;

            float t = a.elapsed / duration;
            if (t >= 1f)
            {
                Release(i);
                continue;
            }

            float rise = _riseDistance * _riseCurve.Evaluate(t);

            // HP바와 같은 이유로 정수 픽셀 스냅 — 소수점 좌표면 가장자리가 프레임마다 흔들린다.
            a.rect.anchoredPosition = new Vector2(
                _startAnchoredPos.x,
                Mathf.Round(_startAnchoredPos.y + rise));

            a.group.alpha = FadeAlpha(t);
        }
    }

    private void Spawn()
    {
        if (_arrowTemplate == null) return;

        Active a;
        if (_pool.Count > 0)
        {
            a = _pool.Pop();
        }
        else
        {
            var clone = Instantiate(_arrowTemplate, _spawnParent);
            clone.name = $"{_arrowTemplate.name}_Play";

            // 페이드에 CanvasGroup이 필요한데 아트가 안 붙여둘 수 있어 여기서 보강한다.
            var group = clone.GetComponent<CanvasGroup>();
            if (group == null) group = clone.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; // 연출용이라 클릭을 가로채면 안 된다
            group.interactable = false;

            a = new Active { rect = clone, group = group };
        }

        a.elapsed = 0f;
        a.rect.anchoredPosition = _startAnchoredPos;
        a.rect.localScale = _arrowTemplate.localScale;
        a.group.alpha = 1f;
        a.rect.gameObject.SetActive(true);

        // 나중에 뜬 화살표가 위에 그려지도록 — 겹칠 때 앞선 화살표에 가려지지 않는다.
        a.rect.SetAsLastSibling();

        _active.Add(a);
    }

    /// <summary>_holdRatio 구간까지는 1, 이후 선형으로 0까지 내려간다.</summary>
    private float FadeAlpha(float t)
    {
        if (t <= _holdRatio) return 1f;

        float remain = 1f - _holdRatio;
        return remain <= 0f ? 0f : 1f - (t - _holdRatio) / remain;
    }

    private void Release(int index)
    {
        var a = _active[index];
        a.rect.gameObject.SetActive(false);
        _pool.Push(a);
        _active.RemoveAt(index);
    }
}
