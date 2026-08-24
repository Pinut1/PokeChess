using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// "지금 커서 아래에 UI가 있는지"를 묻는 곳이 여러 군데(SellZone·AugmentInfoTrigger·StatInfoController
/// 등)라 하나로 모았다 — OnMouseDown/OnMouseEnter/OnMouseOver 같은 Main Camera 기준 물리 레이캐스트,
/// 그리고 관전 화면의 좌표-거리 클릭 판정은 전부 화면상 UI에 가려도 그대로 통과하므로, 그 위에 뜬
/// UI를 뚫고 상호작용/안내창이 나타나는 걸 막을 때 쓴다(2026-08 QA 리포트: "통신기안내창이
/// 견본덱창 뚫음"). 각 호출부가 개별적으로 EventSystem.current null 체크까지 반복하지 않도록
/// 여기 한 곳에서만 관리한다.
/// </summary>
public static class PointerUtil
{
    public static bool IsOverUI() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    /// <summary>
    /// 화면 좌표 클릭 반경 값을 튜닝할 때 기준으로 삼은 렌더 높이(px). 반경을 이 값에 대해 비례시키면
    /// 해상도가 달라져도 같은 월드 거리가 같은 비율로 잡힌다(<see cref="ScaledRadius"/> 참고).
    /// </summary>
    public const float REFERENCE_VIEW_HEIGHT_PX = 1080f;

    /// <summary>
    /// <see cref="REFERENCE_VIEW_HEIGHT_PX"/> 기준으로 튜닝된 반경을 실제 렌더 높이에 비례시킨다.
    /// <b>Screen.height를 쓰면 안 된다</b> — CameraLetterbox가 뷰포트를 16:9로 줄여 놓으면 화면 높이와
    /// 실제로 그려지는 높이가 다르고, 관전 화면(RawImage)이 따르는 것은 후자다
    /// (PartnerSpectateView.RefreshTextureOnScreenChange도 같은 이유로 Screen이 아니라 Main Camera의
    /// 렌더 크기를 감시한다). Main Camera가 없는 프레임(씬 전환 중)에만 Screen.height로 폴백한다.
    /// 화면 좌표 거리로 클릭을 판정하는 곳(AugmentInfoTrigger·StatInfoController)이 공통으로 쓴다.
    /// </summary>
    public static float ScaledRadius(float referenceRadiusAt1080p)
    {
        Camera mainCamera = Camera.main;
        // 창 리사이즈/최소화 복귀 등 과도기 프레임에 렌더 높이가 비정상적으로 작게(또는 0으로) 보고될
        // 수 있다 — 그대로 나누면 반경이 0에 수렴해 정상 클릭까지 막아버린다(PartnerSpectateView.
        // ComputeDesiredRenderTextureSize와 동일하게 최소 1px로 바닥을 둔다).
        float viewHeight = Mathf.Max(mainCamera != null ? mainCamera.pixelHeight : Screen.height, 1f);
        return referenceRadiusAt1080p * (viewHeight / REFERENCE_VIEW_HEIGHT_PX);
    }

    // RaycastAll 호출마다 새로 할당하지 않도록 재사용 — 프레임당 호출자가 많지 않은 폴링/클릭
    // 경로에서만 쓰여 경쟁이 없다.
    private static readonly List<RaycastResult> s_uiRaycastBuffer = new();

    /// <summary>
    /// screenPos 지점의 맨 위 UI가 allowedRoot(예: 관전 배경 RawImage) 소속이 아니면 true — 즉,
    /// 그 자리를 다른 UI(설정창 버튼 등)가 실제로 덮고 있다는 뜻이다. "소속"은 allowedRoot 자신뿐
    /// 아니라 그 자식도 포함한다(IsChildOf) — 관전 화면(PartnerSpectateView)은 파트너 HP/마나 바
    /// 같은 자체 HUD를 배경 RawImage의 자식으로 붙여 PIP/전체화면 전환에 자동으로 따라가게 하므로,
    /// "배경 자신"만 비교하면 그 HUD 자체를 "다른 UI가 막고 있다"고 오판한다(2026-08 재재재리뷰
    /// 지적). 아무 UI도 없거나 allowedRoot 소속만 잡히면 false(클릭을 그대로 인정).
    /// </summary>
    public static bool IsBlockedByOtherUI(Vector2 screenPos, GameObject allowedRoot)
        => IsBlockedByOtherUI(screenPos, allowedRoot, out _);

    /// <summary>
    /// 위와 같은 판정에 <b>실제로 맨 위에 잡힌 UI</b>를 함께 돌려주는 버전. 막힌 이유를 로그로 남겨야
    /// 하는 진단 경로에서 쓴다 — 판정 자체를 두 번 계산(RaycastAll 중복 호출)하지 않도록 여기 한
    /// 곳에서만 레이캐스트한다. 아무것도 안 잡히면 topMost는 null이다.
    /// </summary>
    public static bool IsBlockedByOtherUI(Vector2 screenPos, GameObject allowedRoot, out GameObject topMost)
    {
        topMost = null;
        if (EventSystem.current == null) return false;

        var pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
        s_uiRaycastBuffer.Clear();
        EventSystem.current.RaycastAll(pointerData, s_uiRaycastBuffer);

        if (s_uiRaycastBuffer.Count == 0) return false;

        topMost = s_uiRaycastBuffer[0].gameObject;
        return !topMost.transform.IsChildOf(allowedRoot.transform);
    }
}
