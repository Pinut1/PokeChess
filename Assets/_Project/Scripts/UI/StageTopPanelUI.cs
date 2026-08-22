using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단 스테이지 패널(Canvas/Top_PanelGroup) 바인딩.
///   - NowStage_Text  : 현재 스테이지 표기("1-1" ~ "1-5")
///   - StageTime_Text : 남은 시간 표기
///   - StageIRow_Icon : 현재 스테이지 아이콘 하이라이팅 + 화살표 이동
///
/// 시간은 전투 로직에서 값을 읽어오지 않고 <b>표시 전용으로 자체 카운트다운</b>한다.
/// 유저가 "몇 초 남았는지" 감을 잡는 용도라 전투 시뮬레이션과 정밀 동기화는 목표가 아니다.
/// 대신 전투가 조기 종료되면(전멸) OnBattleEnd로 즉시 멈춘다.
///
/// ⚠️ _battleDuration / _resultDuration은 실제 로직값의 <b>복제</b>다. 아래를 바꾸면 같이 맞출 것.
///   - 전투: BattleManager의 MAX_TICKS(300) × TICK_INTERVAL(0.1) = 30초
///   - 결과: RoundPhaseManager._resultDuration = 3초
/// </summary>
public class StageTopPanelUI : MonoBehaviour
{
    [Header("현재 스테이지")]
    [SerializeField] private TextMeshProUGUI _nowStageText;

    [Header("시간 표기")]
    [SerializeField] private TextMeshProUGUI _stageTimeText;

    [Tooltip("전투 제한시간(초). BattleManager의 MAX_TICKS × TICK_INTERVAL과 맞출 것.")]
    [SerializeField] private float _battleDuration = 40f;

    [Tooltip("결과 페이즈 노출시간(초). RoundPhaseManager._resultDuration과 맞출 것.")]
    [SerializeField] private float _resultDuration = 3f;

    [Tooltip("쇼핑 자동 시작이 꺼져 있어(= Ready로만 전투 시작) 제한시간이 없을 때 표기할 문자열.")]
    [SerializeField] private string _noTimerLabel = "Auto";

    [Tooltip("켜면 소수점 1자리(29.4), 끄면 정수(29)로 표기.")]
    [SerializeField] private bool _showDecimal;

    [Tooltip("오버타임 중 타이머 텍스트가 깜박이는 간격(초). 작을수록 빨리 깜박인다. 0 이하면 깜박이지 않는다(텍스트는 항상 표시).")]
    [SerializeField] private float _overtimeBlinkInterval = 0.4f;

    [Header("준비 상태 텍스트")]
    [Tooltip("\"전투 준비중...(n/총)\" → 전원 준비 시 \"전투 시작\"으로 바뀌는 상태 텍스트. 비워두면 표시하지 않는다.")]
    [SerializeField] private TextMeshProUGUI _readyStatusText;

    [Tooltip("준비 인원 표기 포맷. {0}=준비 인원, {1}=총 인원.")]
    [SerializeField] private string _readyStatusFormat = "전투 준비중...({0}/{1})";

    [Tooltip("전원 준비 완료 시 표시할 문구.")]
    [SerializeField] private string _allReadyLabel = "전투 시작";

    [Tooltip("전원 준비 완료/전투 결과 문구가 뜬 뒤 텍스트를 비우기까지 대기시간(초). " +
             "Result 페이즈 길이(RoundPhaseManager._resultDuration, 기본 3초)를 넘기면 다음 라운드의 " +
             "\"전투 준비중\" 리셋이 먼저 텍스트를 덮어써버리니 그 값 이하로 맞출 것.")]
    [SerializeField] private float _readyStatusClearDelay = 3f;

    [Tooltip("전투 종료 시 같은 텍스트에 잠깐 표시할 결과 문구. {0}=라운드 번호. " +
             "증강 선택창 등에 가려서 놓치기 쉬워 라운드 번호를 같이 표기한다.")]
    [SerializeField] private string _victoryLabelFormat = "{0}라운드 승리";
    [SerializeField] private string _defeatLabelFormat = "{0}라운드 패배";

    [Header("스테이지 아이콘")]
    [Tooltip("좌→우 순서대로. 씬의 StageIRow_Icon (1)~(5)를 순서대로 넣을 것 — 이름이 복제형이라 자동 탐색은 순서를 보장하지 못한다.")]
    [SerializeField] private StageIconSlot[] _stageIcons;

    [Tooltip("현재 스테이지를 가리킬 화살표. 아이콘 피벗의 x를 따라간다. 비워두면 이동 처리를 건너뛴다.\n" +
             "StageList 아래에 두면 HorizontalLayoutGroup이 자식으로 배치해버리니 그 바깥에 둘 것.")]
    [SerializeField] private RectTransform _currentArrow;

    [Tooltip("켜면 y도 아이콘 기준으로 맞춘다(아이콘 피벗 y + 아래 오프셋). 끄면 화살표에 잡아둔 y를 그대로 유지.")]
    [SerializeField] private bool _followIconY;

    [Tooltip("_followIconY가 켜져 있을 때 아이콘 피벗에서 띄울 거리. 아래에 두려면 음수.")]
    [SerializeField] private float _arrowYOffset = -50f;

    [Tooltip("현재 스테이지를 강조할 배경 패널. 화살표와 같은 방식으로 아이콘 피벗 x를 따라간다.\n" +
             "아이콘 뒤에 깔려야 하므로 하이라키에서 StageList보다 위에 둘 것(형제 순서가 곧 그리기 순서).")]
    [SerializeField] private RectTransform _highlightPanel;

    [Tooltip("켜면 패널 y도 아이콘 기준으로 맞춘다. 아이콘과 같은 높이에 깔려면 켜고 오프셋 0.")]
    [SerializeField] private bool _panelFollowIconY = true;

    [Tooltip("_panelFollowIconY가 켜져 있을 때 아이콘 피벗에서 띄울 거리.")]
    [SerializeField] private float _panelYOffset;

    /// <summary>
    /// 스테이지 아이콘 한 칸. 스테이지마다 심볼이 달라서 슬롯별로 스프라이트를 따로 받는다.
    /// normal을 비워두면 시작 시 씬에 이미 세팅된 스프라이트를 기본형으로 기억하므로,
    /// 인스펙터에서는 highlighted만 채우면 된다.
    /// </summary>
    [System.Serializable]
    private class StageIconSlot
    {
        public Image icon;

        [Tooltip("이 스테이지가 현재일 때 쓸 스프라이트.")]
        public Sprite highlighted;

        [Tooltip("평소 스프라이트. 비우면 씬에 세팅된 현재 값을 자동으로 사용한다.")]
        public Sprite normal;
    }

    // 표시 전용 잔여 시간. 페이즈 진입 시 세팅되고 Update에서 감소한다.
    private float _fightingTime;

    // 감소 중인지. 쇼핑 페이즈에서는 전투 시간을 '대기' 상태로 띄우기만 하므로 false.
    private bool _counting;

    // 오버타임 안내용 깜박임 코루틴. null이면 실행 중이 아님(=오버타임 아님).
    private Coroutine _overtimeBlinkCoroutine;

    // 전원 준비 완료 문구를 잠시 보여준 뒤 비우는 코루틴. null이면 실행 중이 아님.
    private Coroutine _readyStatusClearCoroutine;

    // 전투 결과("n라운드 승리/패배")가 떠 있는 동안 true. 파트너를 기다리느라 Result 페이즈가
    // _readyStatusClearDelay보다 짧게 끝나는 플레이어의 경우, 다음 라운드의 Ready 리셋(0/총)이
    // 결과 문구를 다 보여주기도 전에 덮어쓰는 걸 막는 가드.
    private bool _showingBattleResult;

    // _showingBattleResult 동안 들어온 준비 인원 갱신을 버리지 않고 기억해둔다 — 가드가 풀리면
    // 그 사이 놓친 최신 값(예: 파트너가 그새 먼저 준비를 누른 것)으로 바로 갱신한다.
    private bool _hasPendingReadyCount;
    private int _pendingReadyCount;
    private int _pendingReadyTotal;

    private void OnEnable()
    {
        GameEvents.OnStageEntered    += HandleStageEntered;
        GameEvents.OnPhaseChanged    += HandlePhaseChanged;
        GameEvents.OnBattleEnd       += HandleBattleEnd;
        GameEvents.OnOvertimeStarted += HandleOvertimeStarted;
        GameEvents.OnReadyCountChanged += HandleReadyCountChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnStageEntered    -= HandleStageEntered;
        GameEvents.OnPhaseChanged    -= HandlePhaseChanged;
        GameEvents.OnBattleEnd       -= HandleBattleEnd;
        GameEvents.OnOvertimeStarted -= HandleOvertimeStarted;
        GameEvents.OnReadyCountChanged -= HandleReadyCountChanged;

        // 컴포넌트가 꺼지는 시점에 깜박임이 돌고 있었다면(오버타임 도중 비활성화 등) 텍스트가
        // 꺼진 상태로 남지 않도록 복구한다.
        StopOvertimeBlink();

        if (_readyStatusClearCoroutine != null)
        {
            StopCoroutine(_readyStatusClearCoroutine);
            _readyStatusClearCoroutine = null;
        }

        _showingBattleResult = false;
        _hasPendingReadyCount = false;
    }

    private void Start()
    {
        CacheNormalSprites();

        _fightingTime = _battleDuration;
        RefreshTimeText();

        SyncToCurrentStage();

        if (_readyStatusText != null) _readyStatusText.text = "";
    }

    /// <summary>
    /// 현재 진행 중인 스테이지로 즉시 맞춘다.
    /// OnStageEntered는 라운드가 바뀔 때만 오므로, 패널이 그 뒤에 켜지면(씬 재진입·오브젝트 토글 등)
    /// 이벤트를 놓쳐 화살표가 초기 위치에 남는다. 시작 시 한 번 현재 상태를 끌어와 그 구멍을 막는다.
    /// </summary>
    private void SyncToCurrentStage()
    {
        if (!GameManager.TryGet(out var gm)) return;

        var stage = gm.Phase != null ? gm.Phase.CurrentStage : null;
        if (stage != null) HandleStageEntered(stage);
    }

    /// <summary>
    /// normal을 비워둔 슬롯은 씬에 이미 세팅된 스프라이트를 기본형으로 기억한다.
    /// 스테이지마다 심볼이 달라 인스펙터에 기본형을 다시 넣는 건 중복 작업이라서.
    /// </summary>
    private void CacheNormalSprites()
    {
        if (_stageIcons == null) return;

        foreach (var slot in _stageIcons)
        {
            if (slot?.icon == null) continue;
            if (slot.normal == null) slot.normal = slot.icon.sprite;
        }
    }

    private void Update()
    {
        if (!_counting) return;

        _fightingTime -= Time.deltaTime;

        if (_fightingTime <= 0f)
        {
            // 0에서 멈춘 채 유지한다. 전투가 아직 안 끝났어도 음수로 내려가지 않게.
            _fightingTime = 0f;
            _counting = false;
        }

        RefreshTimeText();
    }

    // ─────────────────────────────────────────
    // 스테이지 표기
    // ─────────────────────────────────────────

    /// <summary>라운드 진입 시 스테이지 텍스트와 아이콘 하이라이팅을 갱신.</summary>
    private void HandleStageEntered(StageData stage)
    {
        if (stage == null) return;

        if (_nowStageText != null)
            _nowStageText.text = stage.stageId;

        HighlightStage(stage.round - 1); // round는 1-base, 아이콘 배열은 0-base
    }

    /// <summary>index번째 슬롯만 하이라이트 스프라이트로 바꾸고 화살표를 그 아래로 옮긴다.</summary>
    private void HighlightStage(int index)
    {
        if (_stageIcons == null || _stageIcons.Length == 0) return;

        for (int i = 0; i < _stageIcons.Length; i++)
        {
            var slot = _stageIcons[i];
            if (slot?.icon == null) continue;

            // 스프라이트를 지정하지 않았으면 교체를 건너뛴다(아트 미확정 상태에서도 화살표는 동작).
            Sprite sprite = i == index ? slot.highlighted : slot.normal;
            if (sprite != null) slot.icon.sprite = sprite;
        }

        MoveFollowersTo(index);
    }

    /// <summary>화살표와 강조 패널을 index번째 아이콘 위치로 옮긴다.</summary>
    private void MoveFollowersTo(int index)
    {
        if (_currentArrow == null && _highlightPanel == null) return;
        if (_stageIcons == null || index < 0 || index >= _stageIcons.Length) return;

        var target = _stageIcons[index]?.icon;
        if (target == null) return;

        // 아이콘은 HorizontalLayoutGroup이 배치하므로, 레이아웃이 아직 안 돌았으면 위치가 0으로 잡힌다.
        // 라운드 진입이 첫 프레임과 겹치면 따라다니는 오브젝트가 엉뚱한 곳에 서므로 강제로 한 번 갱신한다.
        var layoutRoot = target.rectTransform.parent as RectTransform;
        if (layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

        // rectTransform.position이 곧 피벗의 월드 위치다.
        Vector3 iconPivot = target.rectTransform.position;

        FollowIcon(_currentArrow,   iconPivot, _followIconY,      _arrowYOffset);
        FollowIcon(_highlightPanel, iconPivot, _panelFollowIconY, _panelYOffset);
    }

    /// <summary>
    /// mover의 x를 아이콘 피벗 x에 맞춘다. followY면 y도 아이콘 기준(피벗 y + offset)으로 맞추고,
    /// 아니면 원래 y를 유지한다. 월드 좌표로 옮기므로 어느 부모 아래 두든(캔버스 스케일이 달라도) 어긋나지 않는다.
    /// </summary>
    private static void FollowIcon(RectTransform mover, Vector3 iconPivot, bool followY, float yOffset)
    {
        if (mover == null) return;

        Vector3 pos = mover.position;

        pos.x = iconPivot.x;
        if (followY) pos.y = iconPivot.y + yOffset;

        mover.position = pos;
    }

    // ─────────────────────────────────────────
    // 시간 표기
    // ─────────────────────────────────────────

    private void HandlePhaseChanged(GamePhase phase)
    {
        // 오버타임은 GamePhase에 별도 상태가 없다(Battle 페이즈 내부 상태) — 어떤 페이즈로
        // 전환되든(다음 Shopping/Battle 포함) 이전 전투의 오버타임 표시는 반드시 정리한다.
        StopOvertimeBlink();

        switch (phase)
        {
            // 전투 준비 — 전투 제한시간을 '대기' 상태로 띄워둔다(감소 없음).
            case GamePhase.Shopping:
                _fightingTime = _battleDuration;
                _counting = false;
                break;

            case GamePhase.Battle:
                _fightingTime = _battleDuration;
                _counting = true;
                break;

            case GamePhase.Result:
                _fightingTime = _resultDuration;
                _counting = true;
                break;

            // Lobby / Victory / GameOver — 흐르는 시간 없음.
            default:
                _counting = false;
                break;
        }

        RefreshTimeText();
    }

    /// <summary>
    /// 전투가 끝나면(조기 종료/오버타임 판정 모두) 즉시 정지(Result 진입이 곧 이어진다)하고,
    /// 같은 준비 상태 텍스트에 결과("승리"/"패배")를 잠깐 띄운다.
    /// </summary>
    private void HandleBattleEnd(BattleEndReason reason)
    {
        _counting = false;
        StopOvertimeBlink();

        bool isVictory = reason == BattleEndReason.Victory || reason == BattleEndReason.DecisionVictory;
        int round = GameManager.TryGet(out var gm) && gm.Phase != null ? gm.Phase.CurrentRound : 0;
        string label = string.Format(isVictory ? _victoryLabelFormat : _defeatLabelFormat, round);

        _showingBattleResult = true;
        ShowReadyStatusText(label, autoClear: true);
    }

    /// <summary>
    /// 준비 인원 집계 변경 → "전투 준비중...(n/총)" 갱신, 전원 준비되면 파란색 "전투 시작"으로
    /// 바뀐 뒤 일정 시간 후 텍스트를 비운다.
    /// </summary>
    private void HandleReadyCountChanged(int ready, int total)
    {
        // 전투 결과 문구가 아직 노출 중이면 다음 라운드의 Ready 리셋(0/총)이 그걸 잘라먹지 않게 막는다.
        // 대신 최신 값을 기억해뒀다가 결과 문구가 스스로 지워질 때(ClearReadyStatusAfterDelay) 반영한다
        // — 그냥 버리면 그 사이 파트너가 먼저 준비를 눌러도 화면이 빈 채로 남는다.
        if (_showingBattleResult)
        {
            _hasPendingReadyCount = true;
            _pendingReadyCount = ready;
            _pendingReadyTotal = total;
            return;
        }

        ApplyReadyCountDisplay(ready, total);
    }

    private void ApplyReadyCountDisplay(int ready, int total)
    {
        if (total > 0 && ready >= total)
            ShowReadyStatusText(_allReadyLabel, autoClear: true);
        else
            ShowReadyStatusText(string.Format(_readyStatusFormat, ready, total), autoClear: false);
    }

    /// <summary>
    /// 준비 상태 텍스트에 문구를 채운다. autoClear면 _readyStatusClearDelay 뒤 자동으로 비운다
    /// (전원 준비 완료/전투 결과처럼 잠깐만 보여줄 문구용).
    /// </summary>
    private void ShowReadyStatusText(string text, bool autoClear)
    {
        if (_readyStatusText == null) return;

        if (_readyStatusClearCoroutine != null)
        {
            StopCoroutine(_readyStatusClearCoroutine);
            _readyStatusClearCoroutine = null;
        }

        _readyStatusText.text = text;

        if (autoClear)
            _readyStatusClearCoroutine = StartCoroutine(ClearReadyStatusAfterDelay());
    }

    private IEnumerator ClearReadyStatusAfterDelay()
    {
        yield return new WaitForSeconds(_readyStatusClearDelay);

        _readyStatusClearCoroutine = null;
        _showingBattleResult = false;

        if (_hasPendingReadyCount)
        {
            _hasPendingReadyCount = false;
            ApplyReadyCountDisplay(_pendingReadyCount, _pendingReadyTotal);
        }
        else if (_readyStatusText != null)
        {
            _readyStatusText.text = "";
        }
    }

    /// <summary>
    /// 기본 전투 시간이 끝나고 오버타임에 진입하면 타이머를 오버타임 지속시간으로 다시 시작한다.
    /// 표시 전용 카운트다운이므로 BattleManager의 오버타임 시뮬레이션 시간과 정밀 동기화는 하지 않는다
    /// (클래스 상단 주석과 동일한 전제).
    /// </summary>
    private void HandleOvertimeStarted(float duration)
    {
        _fightingTime = duration;
        _counting = true;
        RefreshTimeText();

        if (_overtimeBlinkCoroutine != null)
        {
            StopCoroutine(_overtimeBlinkCoroutine);
            _overtimeBlinkCoroutine = null;
        }

        // 0 이하면 깜박임을 아예 비활성화 — 카운트다운은 그대로 진행하되 텍스트는 항상 보이게 둔다.
        if (_overtimeBlinkInterval > 0f)
            _overtimeBlinkCoroutine = StartCoroutine(BlinkTimerText());
        else if (_stageTimeText != null)
            _stageTimeText.enabled = true;
    }

    /// <summary>_stageTimeText.enabled를 토글해 깜박인다. Graphic.enabled 토글은 레이아웃에 영향을 주지 않아 안전하다.</summary>
    private IEnumerator BlinkTimerText()
    {
        while (true)
        {
            if (_stageTimeText != null) _stageTimeText.enabled = !_stageTimeText.enabled;
            yield return new WaitForSeconds(_overtimeBlinkInterval);
        }
    }

    /// <summary>깜박임 코루틴을 멈추고 텍스트를 항상 보이는 상태로 복구한다. 오버타임이 아니면 아무 일도 하지 않는다.</summary>
    private void StopOvertimeBlink()
    {
        if (_overtimeBlinkCoroutine != null)
        {
            StopCoroutine(_overtimeBlinkCoroutine);
            _overtimeBlinkCoroutine = null;
        }

        if (_stageTimeText != null) _stageTimeText.enabled = true;
    }

    private void RefreshTimeText()
    {
        if (_stageTimeText == null) return;

        _stageTimeText.text = IsWaitingWithoutTimer()
            ? _noTimerLabel
            : (_showDecimal ? _fightingTime.ToString("0.0") : Mathf.CeilToInt(_fightingTime).ToString());
    }

    /// <summary>
    /// 쇼핑 페이즈인데 자동 시작이 꺼져 있는 상태(= 두 플레이어 Ready로만 전투 시작).
    /// 제한시간 개념이 없으므로 숫자 대신 _noTimerLabel을 띄운다.
    /// </summary>
    private bool IsWaitingWithoutTimer()
    {
        if (!GameManager.TryGet(out var gm)) return false;

        var phase = gm.Phase;
        return phase != null
            && phase.CurrentPhase == GamePhase.Shopping
            && !phase.AutoStartBattleOnTimeout;
    }
}
