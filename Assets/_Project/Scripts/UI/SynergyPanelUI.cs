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
        GameEvents.OnSynergyUpdated += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        GameEvents.OnSynergyUpdated -= Refresh;

        // 패널이 꺼지면 설명창도 같이 닫는다 — 안 그러면 Exit 이벤트를 못 받고 떠 있게 된다.
        if (_tooltip != null) _tooltip.HideAll();
    }

    private void TogglePage()
    {
        _page = _page == 0 ? 1 : 0;
        Refresh();
    }

    private void Refresh()
    {
        if (RowsPerPage == 0) return;

        var synergy = GameManager.TryGet(out var gm) ? gm.Synergy : null;
        var all = synergy != null ? synergy.GetAllSynergyStatuses() : null;

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
