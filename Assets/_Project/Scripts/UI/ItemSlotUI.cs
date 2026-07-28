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
    [Tooltip("아이템 아이콘. 드래그 중에는 이 오브젝트가 드래그 레이어로 옮겨져 커서를 따라간다.")]
    [SerializeField] private Image _icon;

    /// <summary>이 칸이 들고 있는 항목. 비어 있으면 null.</summary>
    public ScriptableObject CurrentData { get; private set; }

    public bool IsEmpty => CurrentData == null;

    /// <summary>드래그를 놓았을 때 발행. 인자는 드롭 지점(스크린 좌표)을 담은 이벤트 데이터.</summary>
    public event Action<ItemSlotUI, PointerEventData> Dropped;

    private RectTransform _dragLayer;
    private Transform _iconHome;      // 아이콘의 원래 부모(이 칸)
    private int _iconHomeSiblingIndex;
    private bool _iconRaycastDefault; // 프리팹에서 지정한 값 — 드래그가 끝나면 이대로 되돌린다
    private bool _dragging;

    /// <summary>컨트롤러가 드래그 레이어를 주입한다. 없으면 드래그 시 아이콘이 다른 UI에 가려질 수 있다.</summary>
    public void Initialize(RectTransform dragLayer)
    {
        _dragLayer = dragLayer;

        if (_icon != null)
        {
            _iconHome = _icon.transform.parent;
            _iconHomeSiblingIndex = _icon.transform.GetSiblingIndex();
            _iconRaycastDefault = _icon.raycastTarget;
        }
    }

    public void Bind(ScriptableObject data)
    {
        CurrentData = data;

        if (_icon == null) return;

        // 아이템과 진화의 돌은 표시 방식이 같고 아이콘 필드만 각자 타입에 있다.
        _icon.sprite = data switch
        {
            ItemData item            => item.icon,
            EvolutionStoneData stone => stone.icon,
            _                        => null
        };

        _icon.enabled = _icon.sprite != null;
        _icon.gameObject.SetActive(true);
    }

    /// <summary>빈 칸으로 되돌린다. 칸 배경은 그대로 두고 아이콘만 숨긴다.</summary>
    public void Clear()
    {
        CurrentData = null;

        if (_icon == null) return;

        ReturnIconHome();
        _icon.sprite = null;
        _icon.enabled = false;
        _icon.gameObject.SetActive(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty || _icon == null) return;

        _dragging = true;

        // 아이콘을 칸 밖으로 꺼내 최상위에 올린다 — 원래 칸은 빈 상태로 보인다.
        if (_dragLayer != null) _icon.transform.SetParent(_dragLayer, true);
        _icon.transform.SetAsLastSibling();

        // 드래그 중인 아이콘이 자기 자신을 가려 드롭 판정을 방해하지 않도록 한다.
        _icon.raycastTarget = false;

        _icon.transform.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || _icon == null) return;
        _icon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        _dragging = false;

        // 성공하든 실패하든 아이콘은 일단 제자리로 돌려놓는다.
        // 장착에 성공하면 GameEvents.OnInventoryChanged로 목록이 다시 그려지면서 이 칸이 비워진다.
        ReturnIconHome();

        Dropped?.Invoke(this, eventData);
    }

    private void ReturnIconHome()
    {
        if (_icon == null || _iconHome == null) return;

        _icon.transform.SetParent(_iconHome, false);
        _icon.transform.SetSiblingIndex(_iconHomeSiblingIndex);
        _icon.raycastTarget = _iconRaycastDefault;

        var rect = _icon.transform as RectTransform;
        if (rect != null) rect.anchoredPosition = Vector2.zero;
    }
}
