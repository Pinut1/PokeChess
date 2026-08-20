using UnityEngine.EventSystems;

/// <summary>
/// "지금 커서 아래에 UI가 있는지"를 묻는 곳이 여러 군데(SellZone·AugmentInfoTrigger 등)라 하나로
/// 모았다 — OnMouseDown/OnMouseEnter/OnMouseOver 같은 Main Camera 기준 물리 레이캐스트는 화면상
/// UI에 가려도 그대로 통과하므로, 그 위에 뜬 UI를 뚫고 상호작용/안내창이 나타나는 걸 막을 때 쓴다
/// (2026-08 QA 리포트: "통신기안내창이 견본덱창 뚫음"). 각 호출부가 개별적으로 EventSystem.current
/// null 체크까지 반복하지 않도록 여기 한 곳에서만 관리한다.
/// </summary>
public static class PointerUtil
{
    public static bool IsOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
