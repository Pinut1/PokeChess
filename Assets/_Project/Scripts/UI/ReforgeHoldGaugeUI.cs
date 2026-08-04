using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재조합기를 인벤토리의 다른 진화의 돌 슬롯 위로 끌 때 뜨는 홀드 진행 게이지.
///
/// 표시 위치 이동 / 진행도(fillAmount) 갱신 / 숨김만 담당한다.
/// 실제 대상 판정이나 재조합 로직은 갖지 않는다 — 그건 ItemInventoryUI의 몫이다
/// (ItemTooltipController가 아이템 판정 없이 표시/배치만 맡는 것과 같은 분리).
///
/// 슬롯마다 새로 만들지 않고 씬에 하나만 둔 공용 인스턴스를 재사용한다.
/// 씬에는 꺼둔 상태로 저장할 것(Awake에서도 다시 꺼서 보정한다).
/// </summary>
public class ReforgeHoldGaugeUI : MonoBehaviour
{
    [Tooltip("진행도를 표시할 Filled 타입 Image. fillAmount 0~1로 채워진다.")]
    [SerializeField] private Image _fill;

    [Tooltip("대상 슬롯 기준 오프셋(픽셀). 기본은 슬롯 바로 아래에 뜬다.")]
    [SerializeField] private Vector2 _slotOffset = new(0f, -40f);

    private RectTransform _rect;
    private Canvas _canvas;

    private void Awake()
    {
        _rect = transform as RectTransform;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) _canvas = _canvas.rootCanvas;

        // 게이지가 대상 슬롯 위에 겹쳐 뜨므로, 배경/필 그래픽이 레이캐스트를 먹으면
        // 드래그 중인 슬롯의 드롭 판정(포인터 아래 대상 감지)이 깨진다.
        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        gameObject.SetActive(false);
    }

    /// <summary>대상 슬롯 위치로 옮기고 진행도 0으로 띄운다.</summary>
    public void Show(RectTransform target)
    {
        if (target == null) return;

        gameObject.SetActive(true);
        PlaceAt(target);
        SetProgress(0f);
    }

    /// <summary>진행도를 0~1 범위로 반영한다.</summary>
    public void SetProgress(float t)
    {
        if (_fill != null) _fill.fillAmount = Mathf.Clamp01(t);
    }

    /// <summary>진행도를 0으로 되돌리고 숨긴다. 취소/성공/실패 모든 종료 경로에서 호출한다.</summary>
    public void Hide()
    {
        SetProgress(0f);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 대상 슬롯의 월드 위치에 오프셋을 더해 자리를 잡는다.
    /// 이 프로젝트의 다른 커서 추종 UI(ItemTooltipController)와 같은 가정 —
    /// Screen Space - Overlay 캔버스를 전제로 rect.position을 화면 좌표로 다룬다.
    /// 인벤토리 Canvas가 Overlay가 아니라면(Screen Space - Camera/World) 이 배치 방식을 조정해야 한다.
    /// </summary>
    private void PlaceAt(RectTransform target)
    {
        if (_rect == null) return;

        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        Vector3 offset = new(_slotOffset.x * scale, _slotOffset.y * scale, 0f);

        _rect.position = target.position + offset;
    }
}
