using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시너지 패널 전체. GameEvents.OnSynergyUpdated 하나만 구독해
/// SynergyManager.GetAllSynergyStatuses()(카운트 1 이상, 비활성 포함)를 pull → 정렬 → 행에 바인딩한다.
///
/// 행은 씬에 9칸 + 페이저 1칸을 고정 배치하고 인스펙터로 물린다(상점 카드 5장과 같은 방식).
/// 런타임 생성/파괴를 하지 않는 이유는 VerticalLayoutGroup이 자식 수에 따라 위치를 다시 잡기 때문이다 —
/// 시너지가 3개일 때 페이저가 4번째 자리로 올라와 "10번째 고정"이 깨진다.
/// 빈 슬롯은 행 루트를 켜 둔 채 SetEmpty()로 안쪽만 비운다.
///
/// 정렬: 등급 내림차순(고유 → 프리즘 → 골드 → 실버 → 브론즈 → 비활성), 같은 등급끼리는 유닛 수 내림차순.
/// 페이징: 시너지가 9개를 넘으면 페이저 버튼이 켜지고 "+n"(n = 전체 - 9)을 표시한다.
///         누르면 10번째 이후가 위에서부터 나오고, 다시 누르면 1페이지로 돌아온다.
/// </summary>
public class SynergyPanelUI : MonoBehaviour
{
    [Tooltip("씬에 고정 배치한 행 9칸. 순서가 곧 표시 순서다(위 → 아래).")]
    [SerializeField] private SynergyRowUI[] _rows;

    [Header("페이저 (SynergyChange_Pf)")]
    [Tooltip("10칸째에서 껐다 켜는 버튼. 자리를 잡는 루트가 아니라 버튼 오브젝트를 물린다 — " +
             "루트를 끄면 레이아웃이 밀린다.")]
    [SerializeField] private Button _pagerButton;
    [Tooltip("\"+n\" 표시. n은 1페이지에 못 담긴 개수로, 보고 있는 페이지와 무관하게 고정.")]
    [SerializeField] private TextMeshProUGUI _pagerCountText;

    [Header("설명창 (호버 툴팁)")]
    [Tooltip("행에 마우스를 올렸을 때 띄울 설명창 컨트롤러. 행마다 배선할 필요 없이 여기 하나만 물리면 " +
             "Awake에서 모든 행에 전달된다. 비워두면 호버해도 아무 일도 일어나지 않는다.")]
    [SerializeField] private SynergyTooltipController _tooltip;

    private readonly List<SynergyStatus> _sorted = new List<SynergyStatus>();
    private int _page;

    /// <summary>지금 이 패널이 내 시너지가 아니라 파트너 시너지를 보여주는 중인지.</summary>
    private bool _showingPartnerSynergy;

    // OpponentBoardView.LastSnapshot을 읽기 위한 캐시(지연 탐색) — ItemInventoryUI와 동일 패턴.
    private OpponentBoardView _partnerBoardView;

    private int RowsPerPage => _rows != null ? _rows.Length : 0;

    private void Awake()
    {
        if (_pagerButton != null) _pagerButton.onClick.AddListener(TogglePage);

        if (_rows != null)
            foreach (var row in _rows)
                if (row != null) row.AttachTooltip(_tooltip);
    }

    private void OnEnable()
    {
        GameEvents.OnSynergyUpdated += HandleLocalSynergyUpdated;
        GameEvents.OnOpponentBoardChanged += HandlePartnerBoardChanged;
        GameEvents.OnPartnerSpectateExpandedChanged += HandlePartnerSpectateExpandedChanged;
        Refresh();
    }

    private void OnDisable()
    {
        GameEvents.OnSynergyUpdated -= HandleLocalSynergyUpdated;
        GameEvents.OnOpponentBoardChanged -= HandlePartnerBoardChanged;
        GameEvents.OnPartnerSpectateExpandedChanged -= HandlePartnerSpectateExpandedChanged;

        // 파트너 시너지를 보여주던 도중 꺼지더라도 다음 활성화 때 그 상태로 남지 않게 한다
        // (정상 경로는 HandlePartnerSpectateExpandedChanged(false)가 되돌리지만, 이 컴포넌트 자신이
        // 먼저 비활성화되는 경우를 대비한 방어 처리 — ItemInventoryUI와 동일 원칙).
        _showingPartnerSynergy = false;

        // 패널이 꺼지면 설명창도 같이 닫는다 — 안 그러면 Exit 이벤트를 못 받고 떠 있게 된다.
        if (_tooltip != null) _tooltip.HideAll();
    }

    /// <summary>
    /// 파트너 전체화면 관전 진입/종료(GameEvents.OnPartnerSpectateExpandedChanged, PIP는 포함 안 함).
    /// 진입 시 파트너의 마지막 보드 스냅샷으로 즉시 1회 계산해 표시하고(다음 파트너 보드 변경까지
    /// 기다리지 않음), 종료 시 내 시너지(Refresh)로 정확히 되돌린다. 실제 SynergyManager 상태
    /// (_statuses/GetActiveSynergies)는 전혀 건드리지 않는다 — 이 패널이 무엇을 보여줄지만 바꾼다.
    /// </summary>
    private void HandlePartnerSpectateExpandedChanged(bool expanded)
    {
        _showingPartnerSynergy = expanded;

        if (_tooltip != null) _tooltip.HideAll(); // 전환 중 낡은 설명창이 남지 않게

        if (expanded)
            RefreshFromPartner(EnsurePartnerBoardView()?.LastSnapshot);
        else
            Refresh();
    }

    private OpponentBoardView EnsurePartnerBoardView()
    {
        if (_partnerBoardView == null) _partnerBoardView = FindFirstObjectByType<OpponentBoardView>();
        return _partnerBoardView;
    }

    /// <summary>내 시너지 갱신(GameEvents.OnSynergyUpdated). 파트너 시너지를 보여주는 중에는 보류한다 —
    /// 관전을 마치고 Refresh()로 돌아갈 때 SynergyManager를 다시 pull하므로 놓치는 변경은 없다.</summary>
    private void HandleLocalSynergyUpdated()
    {
        if (_showingPartnerSynergy) return;
        Refresh();
    }

    /// <summary>파트너 보드 갱신(GameEvents.OnOpponentBoardChanged). 파트너 시너지를 보여주는 중일 때만
    /// 반영한다 — 평소(내 시너지 표시 중)에는 이 이벤트를 무시한다.</summary>
    private void HandlePartnerBoardChanged(BoardSnapshot snap)
    {
        if (!_showingPartnerSynergy) return;
        RefreshFromPartner(snap);
    }

    private void TogglePage()
    {
        _page = _page == 0 ? 1 : 0;
        if (_showingPartnerSynergy) RefreshFromPartner(EnsurePartnerBoardView()?.LastSnapshot);
        else Refresh();
    }

    private void Refresh()
    {
        var synergy = GameManager.TryGet(out var gm) ? gm.Synergy : null;
        BindStatuses(synergy != null ? synergy.GetAllSynergyStatuses() : null);
    }

    /// <summary>
    /// 파트너 BoardSnapshot의 보드(벤치 제외) 유닛만 골라 SynergyManager.ComputeSynergyStatuses로
    /// 계산한 뒤 같은 방식으로 바인딩한다. SynergyManager._statuses(실제 게임에 적용되는 상태)는
    /// 전혀 건드리지 않는다 — 이 결과는 계산만 하고 화면에 그리는 용도로만 쓴다.
    /// </summary>
    private void RefreshFromPartner(BoardSnapshot snap)
    {
        var synergy = GameManager.TryGet(out var gm) ? gm.Synergy : null;
        if (synergy == null) { BindStatuses(null); return; }

        BindStatuses(synergy.ComputeSynergyStatuses(ResolveBoardSpecies(snap)).Values);
    }

    /// <summary>BoardSnapshot에서 보드 위(벤치 제외) 유닛의 종만 추린다 — 로컬 계산(board.GetUnitsOnBoard())과
    /// 동일 규칙. speciesId만으로 충분한 이유는 SynergyManager.ComputeSynergyStatuses의 doc 참고.</summary>
    private static List<PokemonData> ResolveBoardSpecies(BoardSnapshot snap)
    {
        var list = new List<PokemonData>();
        if (snap == null) return list;

        PokemonDatabase db = PokemonDatabase.Instance;
        if (db == null) return list;

        foreach (var e in snap.entries)
        {
            if (!e.onBoard) continue; // 벤치는 시너지에 포함되지 않음
            PokemonData data = db.GetById(e.speciesId);
            if (data != null) list.Add(data);
        }
        return list;
    }

    private void BindStatuses(IEnumerable<SynergyStatus> all)
    {
        if (RowsPerPage == 0) return;

        _sorted.Clear();
        if (all != null)
            foreach (var s in all)
                if (s != null && s.data != null) _sorted.Add(s);

        _sorted.Sort(CompareForDisplay);

        int total = _sorted.Count;
        int overflow = Mathf.Max(0, total - RowsPerPage);

        // 시너지가 줄어 2페이지가 비게 되면 1페이지로 되돌린다.
        if (_page == 1 && overflow == 0) _page = 0;

        int start = _page * RowsPerPage;

        for (int i = 0; i < _rows.Length; i++)
        {
            if (_rows[i] == null) continue;

            int index = start + i;
            if (index < total) _rows[i].Bind(_sorted[index]);
            else               _rows[i].SetEmpty();
        }

        UpdatePager(overflow);
    }

    /// <summary>등급 내림차순 → 유닛 수 내림차순 → 이름(동률일 때 순서가 매번 흔들리지 않게).</summary>
    private static int CompareForDisplay(SynergyStatus a, SynergyStatus b)
    {
        int ga = (int)SynergyGradeUtil.Of(a);
        int gb = (int)SynergyGradeUtil.Of(b);
        if (ga != gb) return gb.CompareTo(ga);

        if (a.uniqueCount != b.uniqueCount) return b.uniqueCount.CompareTo(a.uniqueCount);

        return string.Compare(a.data.synergyName, b.data.synergyName, System.StringComparison.Ordinal);
    }

    /// <summary>페이저 버튼만 껐다 켠다. 10칸째 자리(루트)는 계속 켜져 있어야 아래 정렬이 유지된다.</summary>
    private void UpdatePager(int overflow)
    {
        if (_pagerButton == null) return;

        bool need = overflow > 0;
        _pagerButton.gameObject.SetActive(need);
        if (!need) return;

        if (_pagerCountText != null) _pagerCountText.text = $"+{overflow}";
    }
}
