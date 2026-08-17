using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 통신기 안내창의 표시부. 세 칸으로 나뉜다 — 전송 / 통신진화 / 수령.
///
/// 여닫기는 통신기 오브젝트(<see cref="SellZone"/>)가 커서 상태를 넘겨 결정한다.
/// 다른 툴팁과 달리 <b>안에 버튼이 있어</b> 커서가 창 위로 올라와도 닫히면 안 되므로,
/// "통신기 위" 또는 "창 위" 둘 중 하나라도 참이면 열어 둔다.
///
/// 자리는 씬에 놓아둔 그대로 쓴다(커서를 따라가지 않는다) — 버튼을 누르러 가는 동안
/// 창이 움직이면 못 누른다.
///
/// 프리팹 구조(TradeMachinePanel_Pf):
///   TradeMachinePanel_Pf   Image(배경) + VerticalLayoutGroup + ContentSizeFitter
///     Title_Text           "통신기"
///     Send_Header_Text     "포켓몬: 전송 준비 완료" ← 상태에 따라 문구·색이 바뀐다
///     Send_Body_Text       고정 안내문
///     Evolve_Header_Text   "통신진화: 통신 교환으로 진화하는 포켓몬"
///     Evolve_Body_Text     고정 안내문
///     Evolve_Slot_Panel    GridLayoutGroup  ← UnitSlot_Pf 칸이 코스트 테두리째 깔린다
///     Receive_Header_Text  "파트너가 보낸 포켓몬 대기 중 : N마리"
///     Receive_Body_Text    고정 안내문
///     Receive_Button       Button → 포켓몬 받기
///
/// 아이콘 한 칸은 시너지·아이템 툴팁과 같은 <see cref="SynergyTooltipUnitSlot"/>(UnitSlot_Pf)이다 —
/// 코스트별 테두리 규칙을 통신기만 따로 갖지 않기 위해서다.
/// </summary>
public class TradeMachinePanelUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("전송 칸")]
    [Tooltip("맨 윗줄. 보낼 수 있으면 준비 문구, 이번 라운드를 이미 썼거나 파트너가 없으면 불가 문구가 나온다.")]
    [SerializeField] private TMP_Text _sendHeaderText;

    [Tooltip("보낼 수 있을 때의 문구.")]
    [SerializeField] private string _sendReadyLabel = "포켓몬: 전송 준비 완료";

    [Tooltip("보낼 수 없을 때의 문구.")]
    [SerializeField] private string _sendBlockedLabel = "포켓몬: 전송 불가";

    [SerializeField] private Color _sendReadyColor = new(0.45f, 0.92f, 0.62f);
    [SerializeField] private Color _sendBlockedColor = new(0.92f, 0.45f, 0.45f);

    [Tooltip("(선택) 불가 사유를 덧붙일 줄. 비워두면 사유를 표시하지 않는다.")]
    [SerializeField] private TMP_Text _sendReasonText;

    [Header("통신진화 칸")]
    [Tooltip("아이콘 칸이 깔리는 부모(Evolve_Slot_Panel). GridLayoutGroup을 붙여둘 것.")]
    [SerializeField] private RectTransform _evolveSlotRoot;

    [Tooltip("칸이 모자랄 때 복제할 칸(UnitSlot_Pf). 비워두면 미리 깔아둔 칸 수만큼만 나온다.")]
    [SerializeField] private SynergyTooltipUnitSlot _unitSlotPrefab;

    [Tooltip("만들 수 있는 칸의 상한(폭주 방지). 현재 데이터는 7종이다.")]
    [SerializeField] private int _maxUnitSlots = 16;

    [Header("수령 칸")]
    [Tooltip("대기 마릿수 줄. {0}에 마릿수가 들어간다.")]
    [SerializeField] private TMP_Text _receiveHeaderText;

    [SerializeField] private string _receiveHeaderFormat = "파트너가 보낸 포켓몬 대기 중 : {0}마리";

    [Tooltip("받기 버튼. 대기 유닛이 없거나 벤치가 가득 차면 눌리지 않는다.")]
    [SerializeField] private Button _receiveButton;

    [Tooltip("(선택) 버튼 안 글자. 벤치가 가득 찼을 때 문구를 바꿔 알린다.")]
    [SerializeField] private TMP_Text _receiveButtonLabel;

    [SerializeField] private string _receiveLabel = "포켓몬 받기";
    [SerializeField] private string _benchFullLabel = "벤치가 가득 참";

    [Header("여닫기")]
    [Tooltip("통신기에서 커서가 빠진 뒤 창을 닫기까지 기다리는 시간(초).\n" +
             "창이 통신기와 떨어져 있으면 버튼을 누르러 가는 동안 빈 공간을 지나는데, " +
             "이 유예가 없으면 그 사이에 창이 닫혀 버튼을 누를 수 없다. " +
             "창을 통신기에 붙여 뒀다면 0으로 줄여도 된다.")]
    [SerializeField] private float _closeDelay = 0.25f;

    /// <summary>지금 통신기(3D 오브젝트) 위에 커서가 있는지. SellZone이 알려준다.</summary>
    private bool _ownerHovered;

    /// <summary>지금 이 창 위에 커서가 있는지. 창 안 버튼을 누르러 오는 동안 닫히지 않게 한다.</summary>
    private bool _pointerInside;

    /// <summary>닫기 예약이 없음을 뜻하는 값.</summary>
    private const float NO_CLOSE_PENDING = -1f;

    /// <summary>이 시각(unscaled)이 지나면 창을 닫는다. 커서가 돌아오면 취소된다.</summary>
    private float _closeAt = NO_CLOSE_PENDING;

    // 미리 깔아둔 칸 + 필요해서 만든 칸. 한 번 만들면 파괴하지 않고 재사용한다.
    private readonly List<SynergyTooltipUnitSlot> _slots = new();

    // 통신진화 목록은 데이터라 게임 중에 바뀌지 않는다 — 처음 열 때 한 번만 채운다.
    private bool _evolveListFilled;

    // 같은 값을 매 프레임 다시 써서 TMP 메시를 새로 만들지 않도록 직전 값을 들고 있는다.
    private bool _lastCanSend;
    private string _lastReason = "";
    private int _lastPendingCount = -1;
    private bool _lastBenchFull;
    private bool _stateDirty = true;

    /// <summary>
    /// ⚠️ <b>씬에는 꺼둔 상태로 저장할 것</b>(다른 툴팁과 같은 규약).
    ///
    /// 여기서 스스로를 끄면 안 된다 — 꺼둔 채 저장하면 Awake는 <b>처음 열릴 때</b> 실행되므로,
    /// 켜자마자 자신을 다시 꺼버려 창이 영영 안 열린다. 켠 채로 저장된 경우를 대비한 정리는
    /// <see cref="SellZone"/>이 시작할 때 대신 해준다.
    /// </summary>
    private void Awake()
    {
        if (_receiveButton != null)
            _receiveButton.onClick.AddListener(ReceiveNow);

        // 프리팹에 미리 깔아둔 칸을 먼저 회수한다(꺼둔 칸도 포함).
        if (_evolveSlotRoot != null)
            _slots.AddRange(_evolveSlotRoot.GetComponentsInChildren<SynergyTooltipUnitSlot>(true));
    }

    /// <summary>씬에 켠 채로 저장됐을 때를 위한 정리. 소유자(SellZone)가 시작할 때 부른다.</summary>
    public void CloseOnStartup()
    {
        _ownerHovered = false;
        _pointerInside = false;
        _closeAt = NO_CLOSE_PENDING;

        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_receiveButton != null)
            _receiveButton.onClick.RemoveListener(ReceiveNow);
    }

    // ─────────────────────────────────────────
    // 여닫기 — SellZone이 부른다
    // ─────────────────────────────────────────

    /// <summary>통신기 위에 커서가 들고 났음. 창 위에 커서가 있으면 열린 채로 둔다.</summary>
    public void SetOwnerHovered(bool hovered)
    {
        _ownerHovered = hovered;

        if (hovered) Open();
        else CloseIfLeft();
    }

    private void Open()
    {
        _closeAt = NO_CLOSE_PENDING;

        if (!gameObject.activeSelf)
        {
            // 껐다 켜는 사이에 라운드·대기열이 바뀌었을 수 있으니 캐시를 무효화한다.
            _stateDirty = true;
            gameObject.SetActive(true);
        }

        FillEvolveListOnce();
        Refresh();
    }

    /// <summary>
    /// 통신기에서도 창에서도 커서가 빠졌으면 닫기를 <b>예약</b>한다(즉시 닫지 않는다).
    /// 유예 안에 커서가 창으로 들어오면 <see cref="Open"/>·<see cref="OnPointerEnter"/>가 예약을 취소한다.
    /// </summary>
    private void CloseIfLeft()
    {
        if (_ownerHovered || _pointerInside)
        {
            _closeAt = NO_CLOSE_PENDING;
            return;
        }

        // 게임이 멈춰도(timeScale 0) 창은 닫혀야 하므로 unscaled을 쓴다.
        _closeAt = Time.unscaledTime + Mathf.Max(0f, _closeDelay);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
        _closeAt = NO_CLOSE_PENDING;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
        CloseIfLeft();
    }

    private void OnDisable()
    {
        // 커서를 올린 채 창이 꺼지면 Exit이 오지 않아 다음에 열 때 "안에 있다"로 남는다.
        _pointerInside = false;
    }

    // ─────────────────────────────────────────
    // 표시 갱신
    //
    // 전송 가능 여부는 유닛을 놓는 순간 바뀌는데 그 시점을 알리는 이벤트가 없다.
    // 창이 열려 있는 동안만 상태를 확인하고, 값이 실제로 달라졌을 때만 글자를 다시 쓴다.
    // ─────────────────────────────────────────

    private void LateUpdate()
    {
        if (_closeAt != NO_CLOSE_PENDING && Time.unscaledTime >= _closeAt)
        {
            _closeAt = NO_CLOSE_PENDING;
            gameObject.SetActive(false);
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        GameManager.TryGet(out GameManager gm);

        NetworkManager network = gm != null ? gm.Network : null;
        BoardManager board = gm != null ? gm.Board : null;

        bool canSend;
        string reason;

        if (network == null)
        {
            canSend = false;
            reason = "네트워크 준비 중";
        }
        else
        {
            canSend = network.CanSendTradeUnit(out reason);
            reason ??= "";
        }

        int pending = network != null ? network.PendingTradeUnitCount : 0;
        bool benchFull = board != null && !board.HasBenchSpace();

        RefreshSendSection(canSend, reason);
        RefreshReceiveSection(pending, benchFull);

        _stateDirty = false;
    }

    private void RefreshSendSection(bool canSend, string reason)
    {
        if (!_stateDirty && canSend == _lastCanSend && reason == _lastReason) return;

        _lastCanSend = canSend;
        _lastReason = reason;

        if (_sendHeaderText != null)
        {
            _sendHeaderText.text = canSend ? _sendReadyLabel : _sendBlockedLabel;
            _sendHeaderText.color = canSend ? _sendReadyColor : _sendBlockedColor;
        }

        if (_sendReasonText == null) return;

        // 보낼 수 있을 때는 줄째 접는다 — 빈 줄이 남으면 레이아웃에 구멍이 생긴다.
        _sendReasonText.gameObject.SetActive(!canSend && !string.IsNullOrWhiteSpace(reason));
        if (!canSend) _sendReasonText.text = reason;
    }

    private void RefreshReceiveSection(int pending, bool benchFull)
    {
        if (!_stateDirty && pending == _lastPendingCount && benchFull == _lastBenchFull) return;

        _lastPendingCount = pending;
        _lastBenchFull = benchFull;

        if (_receiveHeaderText != null)
            _receiveHeaderText.text = string.Format(_receiveHeaderFormat, pending);

        // 받을 게 없으면 벤치가 차 있어도 "벤치가 가득 참"이라고 하지 않는다 — 지금 막힌 이유가 아니다.
        bool blockedByBench = pending > 0 && benchFull;

        if (_receiveButton != null)
            _receiveButton.interactable = pending > 0 && !benchFull;

        if (_receiveButtonLabel != null)
            _receiveButtonLabel.text = blockedByBench ? _benchFullLabel : _receiveLabel;
    }

    /// <summary>버튼과 통신기 클릭은 같은 경로를 쓴다 — 받는 규칙이 두 벌이 되지 않게.</summary>
    private void ReceiveNow()
    {
        NetworkManager network =
            GameManager.TryGet(out var gm) ? gm.Network : null;

        if (network == null)
        {
            Debug.LogWarning("[TradeMachinePanel] NetworkManager가 없어 수령할 수 없습니다.", this);
            return;
        }

        network.TryReceiveNextTradeUnit();

        // 마릿수·버튼 상태를 즉시 반영한다(다음 LateUpdate를 기다리지 않게).
        _stateDirty = true;
        Refresh();
    }

    // ─────────────────────────────────────────
    // 통신진화 아이콘 줄
    // ─────────────────────────────────────────

    /// <summary>
    /// 통신진화 대상(진화 <b>전</b> 종)을 코스트순으로 깐다.
    /// 매핑은 임포터가 구운 데이터라 게임 중에 바뀌지 않으므로 한 번만 채운다.
    /// </summary>
    private void FillEvolveListOnce()
    {
        if (_evolveListFilled) return;
        if (_evolveSlotRoot == null) return;

        TradeEvolutionData trade = TradeEvolutionData.Instance;
        PokemonDatabase db = PokemonDatabase.Instance;

        var targets = new List<PokemonData>();
        var seenIds = new HashSet<int>();

        if (trade != null && trade.mappings != null && db != null)
        {
            foreach (TradeEvolutionMapping mapping in trade.mappings)
            {
                if (mapping == null || string.IsNullOrWhiteSpace(mapping.targetPokemonEn)) continue;

                PokemonData target = db.GetByNameEn(mapping.targetPokemonEn);

                // 시트 표기 불일치는 조용히 건너뛴다(임포터·QAManager가 따로 검증한다).
                if (target == null) continue;

                if (!seenIds.Add(target.id)) continue;

                targets.Add(target);
            }
        }

        // 칸에 넣기 전에 정렬한다 — 넣으면서 자르면 잘려나가는 쪽이 "고코스트"가 아니라 "시트 뒷줄"이 된다.
        targets.Sort(CompareByCostThenId);

        int shown = 0;

        foreach (PokemonData target in targets)
        {
            SynergyTooltipUnitSlot slot = SlotAt(shown);
            if (slot == null) break;

            // placed:true 고정 — "지금 보드에 있나"와 무관한 목록이라 흑백 처리를 하지 않는다.
            slot.Bind(target, true);
            shown++;
        }

        for (int i = shown; i < _slots.Count; i++)
            if (_slots[i] != null) _slots[i].SetEmpty();

        if (shown < targets.Count)
        {
            Debug.LogWarning(
                $"[TradeMachinePanel] 통신진화 {targets.Count}종 중 {shown}종만 표시됨 — " +
                "Unit Slot Prefab을 물리거나 Max Unit Slots를 늘릴 것", this);
        }

        if (targets.Count == 0)
        {
            Debug.LogWarning(
                "[TradeMachinePanel] 통신진화 매핑이 비어 있습니다 — " +
                "PokeChess/Import TradeEvolution을 실행했는지 확인하세요.", this);
        }

        // 한 종도 못 찾았어도 다시 시도하지 않는다 — 데이터가 없는 상태에서 열 때마다 경고가 쏟아진다.
        _evolveListFilled = true;
    }

    /// <summary>코스트 오름차순, 같으면 id순(열 때마다 자리가 바뀌지 않게).</summary>
    private static int CompareByCostThenId(PokemonData a, PokemonData b)
    {
        int byCost = a.cost.CompareTo(b.cost);
        return byCost != 0 ? byCost : a.id.CompareTo(b.id);
    }

    /// <summary>i번째 칸. 없으면 프리팹에서 만들어 붙인다. 상한을 넘거나 프리팹이 없으면 null.</summary>
    private SynergyTooltipUnitSlot SlotAt(int index)
    {
        if (index < _slots.Count) return _slots[index];
        if (_unitSlotPrefab == null || index >= _maxUnitSlots) return null;

        // worldPositionStays: false — 켜두면 프리팹에 잡아둔 RectTransform 값이 틀어진다.
        var slot = Instantiate(_unitSlotPrefab, _evolveSlotRoot, false);
        _slots.Add(slot);
        return slot;
    }
}
