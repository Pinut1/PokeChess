using System;
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
public class ItemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("아이템 일러스트. 스프라이트는 Bind에서 데이터로 갈아끼운다.")]
    [SerializeField] private Image _icon;

    [Tooltip("테두리처럼 일러스트와 함께 켜지고 꺼져야 하는 묶음의 루트. " +
             "비워두면 _icon 오브젝트만 토글한다. 드래그 시 커서를 따라가는 것도 이 오브젝트다.")]
    [SerializeField] private GameObject _itemView;

    /// <summary>이 칸이 들고 있는 항목. 비어 있으면 null.</summary>
    public ScriptableObject CurrentData { get; private set; }

    public bool IsEmpty => CurrentData == null;

    /// <summary>드래그를 놓았을 때 발행. 인자는 드롭 지점(스크린 좌표)을 담은 이벤트 데이터.</summary>
    public event Action<ItemSlotUI, PointerEventData> Dropped;

    private RectTransform _dragLayer;
    private Transform _viewHome;      // 아이템 표시부의 원래 부모(이 칸)
    private int _viewHomeSiblingIndex;
    private bool _iconRaycastDefault; // 프리팹에서 지정한 값 — 드래그가 끝나면 이대로 되돌린다
    private bool _dragging;

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

        if (_icon != null) _iconRaycastDefault = _icon.raycastTarget;
    }

    public void Bind(ScriptableObject data)
    {
        CurrentData = data;

        if (_icon != null)
        {
            // 아이템과 진화의 돌은 표시 방식이 같고 아이콘 필드만 각자 타입에 있다.
            _icon.sprite = data switch
            {
                ItemData item            => item.icon,
                EvolutionStoneData stone => stone.icon,
                _                        => null
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        var view = ViewObject;
        if (IsEmpty || view == null) return;

        _dragging = true;

        // 표시부(테두리 포함)를 칸 밖으로 꺼내 최상위에 올린다 — 원래 칸은 빈 상태로 보인다.
        if (_dragLayer != null) view.transform.SetParent(_dragLayer, true);
        view.transform.SetAsLastSibling();

        // 끌고 있는 아이템이 커서 아래를 가려 드롭 판정을 방해하지 않도록 한다.
        if (_icon != null) _icon.raycastTarget = false;

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

        if (_icon != null) _icon.raycastTarget = _iconRaycastDefault;

        var rect = view.transform as RectTransform;
        if (rect != null) rect.anchoredPosition = Vector2.zero;
    }
}
