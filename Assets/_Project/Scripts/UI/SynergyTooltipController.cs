using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시너지 설명창(SynergyTooltipUI)을 여닫고 자리를 잡는 쪽.
/// 시너지 행(SynergyRowUI)에 마우스를 올리면 열리고, 벗어나면 닫힌다.
///
/// ⚠️ <b>항상 활성인 오브젝트</b>에 붙일 것(시너지 패널 루트나 Canvas). 툴팁 자신에 붙이면
/// 닫힌 동안 호출을 받지 못한다 — AugmentInfoPanel·StatInfoController와 같은 규약이다.
///
/// <b>위치 규칙</b> — 요구사항이 "상단은 기준선에 고정, 내용이 길어지면 아래로만 자란다"라서
/// 가로 위치는 프리팹에 잡아둔 값을 그대로 두고 세로만 계산한다.
///   ① 툴팁 위쪽 모서리를 Top Anchor의 위쪽 모서리에 맞춘다(내용 길이와 무관하게 항상 같은 높이).
///   ② 그러고도 아래가 화면(또는 Bottom Limit) 밖으로 넘치면 넘친 만큼만 위로 밀어 올린다.
/// ②가 발동하면 상단 고정이 깨지지만, 창이 잘려 나가는 것보단 낫다는 판단이다.
/// 자주 발동하면 툴팁 폭을 넓히거나 단계 줄 폰트를 줄여 높이를 낮출 것.
///
/// 툴팁 루트의 <b>부모에는 레이아웃 그룹을 두지 말 것</b> — 여기서 잡은 위치를 매 프레임 되돌린다.
/// </summary>
public class SynergyTooltipController : MonoBehaviour
{
    [Header("툴팁")]
    [Tooltip("씬에 배치해둔 설명창. 씬에는 꺼둔 상태로 저장할 것(Awake에서 한 번 더 꺼준다).")]
    [SerializeField] private SynergyTooltipUI _panel;

    [Header("위치")]
    [Tooltip("툴팁 위쪽 모서리를 맞출 기준. 보통 시너지 패널 맨 윗칸이나 상단 기준선용 빈 RectTransform을 넣는다. " +
             "비우면 프리팹에 잡아둔 자리를 그대로 쓴다.")]
    [SerializeField] private RectTransform _topAnchor;

    [Tooltip("아래로 넘치는지 판정할 영역. 비우면 루트 Canvas(=화면 전체)를 쓴다.")]
    [SerializeField] private RectTransform _bottomLimit;

    [Tooltip("아래쪽으로 최소한 남길 여백(픽셀).")]
    [SerializeField] private float _bottomMargin = 8f;

    // GetWorldCorners 재사용 버퍼(호버마다 배열을 새로 만들지 않도록).
    private static readonly Vector3[] Corners = new Vector3[4];

    // 지금 툴팁을 띄운 행. 다른 행으로 커서가 옮겨간 뒤 도착하는 Exit이 새 툴팁을 꺼버리는 걸 막는다.
    private SynergyRowUI _owner;

    private void Awake()
    {
        if (_panel == null)
        {
            Debug.LogError("[SynergyTooltipController] 툴팁 미배선 — 아이콘에 올려도 설명창이 뜨지 않는다", this);
            return;
        }

        // 씬에 켜둔 채 저장했더라도 시작은 닫힌 상태로 맞춘다.
        _panel.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────
    // 행이 부르는 API
    // ─────────────────────────────────────────

    /// <summary>
    /// 툴팁 유닛 아이콘의 채색 기준이 될 보드를 지정한다(SynergyTooltipUI.SetBoardSpeciesOverride로
    /// 그대로 전달). null이면 내 보드를 읽는 기본 동작. 시너지 행을 채우는 SynergyPanelUI가
    /// <b>행에 넘긴 것과 같은 목록</b>을 여기에도 넣어, 행과 툴팁이 서로 다른 보드를 읽지 않게 한다.
    /// </summary>
    public void SetBoardSpeciesOverride(IReadOnlyList<PokemonData> boardSpecies)
    {
        if (_panel != null) _panel.SetBoardSpeciesOverride(boardSpecies);
    }

    /// <summary>행에 커서가 들어왔을 때. 표시할 시너지가 없으면(빈 슬롯) 아무것도 열지 않는다.</summary>
    public void Show(SynergyRowUI owner, SynergyStatus status)
    {
        if (_panel == null) return;

        if (status == null || status.data == null)
        {
            Hide(owner);
            return;
        }

        _owner = owner;
        _panel.gameObject.SetActive(true);
        _panel.Bind(status);
        Place();
    }

    /// <summary>행에서 커서가 나갔을 때. 이미 다른 행이 띄운 툴팁은 건드리지 않는다.</summary>
    public void Hide(SynergyRowUI owner)
    {
        if (owner != null && _owner != owner) return;

        _owner = null;
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    /// <summary>패널이 꺼질 때 등, 소유자와 무관하게 무조건 닫는다.</summary>
    public void HideAll() => Hide(null);

    /// <summary>
    /// 행 내용이 다시 바인딩됐을 때(유닛 배치/판매로 정렬이 바뀌면 커서 아래 행의 시너지가 바뀐다).
    /// 커서가 가리키는 행을 그대로 따라가므로 롤체와 같은 동작이 된다.
    /// </summary>
    public void RowRebound(SynergyRowUI row, SynergyStatus status)
    {
        if (_owner != row || _panel == null) return;

        if (status == null || status.data == null)
        {
            Hide(row);
            return;
        }

        _panel.Bind(status);
        Place();
    }

    // ─────────────────────────────────────────
    // 배치
    // ─────────────────────────────────────────

    private void Place()
    {
        RectTransform rect = _panel.Rect;
        if (rect == null) return;

        // ContentSizeFitter 결과를 이번 프레임에 바로 반영한다.
        // 안 하면 방금 켠 프레임에는 이전 내용의 높이로 자리를 잡아 창이 한 번 튄다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        if (_topAnchor != null)
            rect.position += new Vector3(0f, WorldTop(_topAnchor) - WorldTop(rect), 0f);

        RectTransform limit = _bottomLimit != null ? _bottomLimit : RootCanvasRect(rect);
        if (limit == null) return;

        // 여백은 픽셀 단위 입력이라 캔버스 스케일을 곱해 월드 길이로 바꾼다(해상도 대응 캔버스 대비).
        float floor = WorldBottom(limit) + _bottomMargin * Mathf.Abs(limit.lossyScale.y);
        float overflow = floor - WorldBottom(rect);
        if (overflow > 0f) rect.position += new Vector3(0f, overflow, 0f);
    }

    private static RectTransform RootCanvasRect(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
    }

    private static float WorldTop(RectTransform rect)
    {
        rect.GetWorldCorners(Corners);
        return Corners[1].y; // 0=좌하 1=좌상 2=우상 3=우하
    }

    private static float WorldBottom(RectTransform rect)
    {
        rect.GetWorldCorners(Corners);
        return Corners[0].y;
    }
}
