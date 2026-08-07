using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 한 칸. 보유 중인 아이템/진화의 돌 아이콘을 표시하고, 아이콘을 끌어서 유닛에 놓는 드래그를 담당한다.
/// 칸 자체는 20개 고정이라 생성/파괴 없이 Bind/Clear로만 상태가 바뀐다
/// (MAX_INVENTORY_SIZE=20과 씬의 itemBox_Panel 20개가 1:1 대응).
///
/// 실제 장착 처리(레이캐스트로 유닛 찾기 → ItemManager.EquipToUnit)는 상위 컨트롤러(ItemInventoryUI)가 맡는다.
/// 상점 카드(ShopCardUI/ItemCardUI)가 매니저를 직접 참조하지 않는 것과 같은 구조.
/// </summary>
public class ItemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
                          IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("아이템 일러스트. 스프라이트는 Bind에서 데이터로 갈아끼운다.")]
    [SerializeField] private Image _icon;

    [Tooltip("테두리처럼 일러스트와 함께 켜지고 꺼져야 하는 묶음의 루트. " +
             "비워두면 _icon 오브젝트만 토글한다. 드래그 시 커서를 따라가는 것도 이 오브젝트다.")]
    [SerializeField] private GameObject _itemView;

    /// <summary>이 칸이 들고 있는 항목. 비어 있으면 null.</summary>
    public ScriptableObject CurrentData { get; private set; }

    public bool IsEmpty => CurrentData == null;

    /// <summary>
    /// true면 드래그를 아예 시작하지 않는다(OnBeginDrag 최상단에서 차단). Hover(OnPointerEnter/Exit)는
    /// 이 값과 무관하게 항상 정상 동작한다 — 파트너 인벤토리(읽기 전용) 표시 중에도 설명창 툴팁은
    /// 보여야 하기 때문이다(ItemInventoryUI가 진입/종료 시 이 값만 토글). OnDrag/OnEndDrag는 이미
    /// _dragging 가드를 갖고 있어(둘 다 "if (!_dragging) return;") 이 값 하나로 드래그→드롭 전체
    /// 파이프라인이 막힌다 — Dropped 이벤트도 _dragging이 true였을 때만 발행되므로 별도 처리 불필요.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>드래그를 놓았을 때 발행. 인자는 드롭 지점(스크린 좌표)을 담은 이벤트 데이터.</summary>
    public event Action<ItemSlotUI, PointerEventData> Dropped;

    /// <summary>커서가 들어오고 나갈 때 발행(true=들어옴). 설명창은 컨트롤러가 띄운다.</summary>
    public event Action<ItemSlotUI, bool> Hovered;

    /// <summary>
    /// 드래그가 시작될 때 발행(무엇을 드래그 중인지 알려주는 용도).
    /// 재조합기를 다른 인벤토리 슬롯(진화의 돌) 위로 끌 때처럼, 다른 슬롯이 "지금 드래그 중인 원본이
    /// 자신인지"를 판별해야 하는 경우에만 쓴다. 드래그 종료 시점은 기존 Dropped가 그대로 겸한다
    /// (성공/취소 모두 OnEndDrag에서 무조건 발행되므로 별도 DragEnded는 두지 않는다).
    /// </summary>
    public event Action<ItemSlotUI> DragBegan;

    private RectTransform _dragLayer;
    private Transform _viewHome;      // 아이템 표시부의 원래 부모(이 칸)
    private int _viewHomeSiblingIndex;
    private bool _dragging;

    // 드래그 중인 아이콘(_itemView)이 커서를 그대로 따라다니며 항상 최상단 형제로 올라가므로,
    // raycastTarget을 켜둔 채면 GraphicRaycaster가 커서 바로 아래의 대상 슬롯 대신 이 아이콘 자신을
    // 맞혀버려 다른 슬롯의 OnPointerEnter/Exit가 아예 발생하지 않는다. 드래그 중엔 꺼두고 종료 시 복구한다.
    private readonly List<Graphic> _dragRaycastGraphics = new();
    private readonly List<bool> _dragRaycastOriginalValues = new();

    /// <summary>테두리까지 묶어서 옮기고 토글해야 하므로, 지정돼 있으면 _itemView가 기준이 된다.</summary>
    private GameObject ViewObject => _itemView != null ? _itemView : _icon?.gameObject;

    /// <summary>컨트롤러가 드래그 레이어를 주입한다. 없으면 드래그 시 아이콘이 다른 UI에 가려질 수 있다.</summary>
    public void Initialize(RectTransform dragLayer)
    {
        _dragLayer = dragLayer;

        var view = ViewObject;
        if (view != null)
        {
            _viewHome = view.transform.parent;
            _viewHomeSiblingIndex = view.transform.GetSiblingIndex();
        }
    }

    public void Bind(ScriptableObject data)
    {
        CurrentData = data;

        if (_icon != null)
        {
            // 아이템, 진화의 돌, Consumables는 표시 방식이 같고 아이콘 필드만 각자 타입에 있다.
            _icon.sprite = data switch
            {
                ItemData item             => item.icon,
                EvolutionStoneData stone  => stone.icon,
                ConsumableData consumable => consumable.icon,
                _                         => null
            };

            _icon.enabled = _icon.sprite != null;
        }

        var view = ViewObject;
        if (view != null) view.SetActive(true);
    }

    /// <summary>빈 칸으로 되돌린다. 칸 배경은 그대로 두고 아이템 표시부(테두리 포함)만 숨긴다.</summary>
    public void Clear()
    {
        CurrentData = null;

        ReturnIconHome();

        if (_icon != null)
        {
            _icon.sprite = null;
            _icon.enabled = false;
        }

        var view = ViewObject;
        if (view != null) view.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsEmpty && !_dragging) Hovered?.Invoke(this, true);
    }

    public void OnPointerExit(PointerEventData eventData) => Hovered?.Invoke(this, false);

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ReadOnly) return; // 읽기 전용 — 드래그 자체를 시작하지 않는다(Hover는 이 검사를 거치지 않음)

        var view = ViewObject;
        if (IsEmpty || view == null) return;

        _dragging = true;
        DragBegan?.Invoke(this);
        Hovered?.Invoke(this, false); // 드래그 중에는 설명창이 커서를 가린다

        // 커서를 따라다니는 아이콘 자신이 레이캐스트를 먹지 않도록 끈다 — 안 그러면 이 아이콘이
        // 항상 커서 바로 아래 최상단이라 다른 슬롯의 OnPointerEnter/Exit가 발생하지 못한다.
        _dragRaycastGraphics.Clear();
        _dragRaycastOriginalValues.Clear();
        view.GetComponentsInChildren(true, _dragRaycastGraphics);
        foreach (var graphic in _dragRaycastGraphics)
        {
            _dragRaycastOriginalValues.Add(graphic.raycastTarget);
            graphic.raycastTarget = false;
        }

        // 표시부(테두리 포함)를 칸 밖으로 꺼내 최상위에 올린다 — 원래 칸은 빈 상태로 보인다.
        if (_dragLayer != null) view.transform.SetParent(_dragLayer, true);
        view.transform.SetAsLastSibling();

        view.transform.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        var view = ViewObject;
        if (view != null) view.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        _dragging = false;

        // 드래그 중 꺼뒀던 raycastTarget을 원래 값으로 되돌린다(원래 칸 안에서 다시 호버 가능해야 한다).
        for (int i = 0; i < _dragRaycastGraphics.Count; i++)
            if (_dragRaycastGraphics[i] != null) _dragRaycastGraphics[i].raycastTarget = _dragRaycastOriginalValues[i];

        _dragRaycastGraphics.Clear();
        _dragRaycastOriginalValues.Clear();

        // 성공하든 실패하든 일단 제자리로 돌려놓는다.
        // 장착에 성공하면 GameEvents.OnInventoryChanged로 목록이 다시 그려지면서 이 칸이 비워진다.
        ReturnIconHome();

        Dropped?.Invoke(this, eventData);
    }

    private void ReturnIconHome()
    {
        var view = ViewObject;
        if (view == null || _viewHome == null) return;

        view.transform.SetParent(_viewHome, false);
        view.transform.SetSiblingIndex(_viewHomeSiblingIndex);

        var rect = view.transform as RectTransform;
        if (rect != null) rect.anchoredPosition = Vector2.zero;
    }
}
