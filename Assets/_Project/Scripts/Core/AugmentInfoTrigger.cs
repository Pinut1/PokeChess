using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 3D 오브젝트를 클릭해 증강 확인창(AugmentInfoPanel)을 여닫는다.
/// 캡슐 등 아무 Collider나 붙은 오브젝트에 달면 된다 — SellZone의 수령 클릭과 같은 방식이다.
///
/// ⚠️ Collider는 <b>이 스크립트와 같은 GameObject</b>에 있어야 한다.
/// OnMouseDown은 콜라이더가 붙은 오브젝트에만 전달되며 부모로 올라가지 않는다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AugmentInfoTrigger : MonoBehaviour
{
    [Tooltip("열고 닫을 확인창. Canvas에 붙여둔 AugmentInfoPanel을 넣는다.")]
    [SerializeField] private AugmentInfoPanel _panel;

    [Tooltip("끄면 클릭할 때마다 열기만 하고 닫히지 않는다(닫기 버튼 전용으로 쓸 때).")]
    [SerializeField] private bool _toggleOnClick = true;

    private void Awake()
    {
        if (_panel == null)
            Debug.LogError("[AugmentInfoTrigger] AugmentInfoPanel 미배선 — 클릭해도 창이 열리지 않는다", this);
    }

    private void OnMouseDown()
    {
        if (_panel == null) return;

        // 창 위를 클릭했는데 뒤에 있는 오브젝트까지 눌리는 것을 막는다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (_toggleOnClick) _panel.Toggle();
        else _panel.Open();
    }
}
