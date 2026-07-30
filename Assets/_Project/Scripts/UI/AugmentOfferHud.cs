using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 3택1 임시 선택 UI(OnGUI). AugmentManager가 자동 부착 — 씬 배선 불필요.
/// 블로킹 UX(기획 확정 2026-07-16): 선택 중 화면 전체 클릭 흡수(모달) + "카드 내려두기"로 해제 +
/// 남은 시간 표시(1분 초과/Ready 자동 선택은 AugmentManager가 처리).
/// ※ IMGUI 모달은 다른 OnGUI(PrototypeHud 등)만 막는다 — 3D 배치 입력은 입력 처리 측이
///   AugmentManager.IsChoiceBlocking을 참조해야 함(후속 배선).
/// 정식 UI는 태욱님 UIManager 이관 대상(OnAugmentOfferReady 구독 + SelectAugment/SetOfferMinimized 호출).
/// </summary>
public class AugmentOfferHud : MonoBehaviour
{
    [SerializeField] private bool _enableDebugUI = false; // Inspector에서 토글 가능 (정식 UI 개발 중에만 사용)

    private IReadOnlyList<AugmentData> _offer;

    // 모달 바깥 클릭 흡수용 투명 스타일. 병합 과정에서 선언이 유실돼 다시 둔다.
    private GUIStyle _blockerStyle;

    private void OnEnable()
    {
        GameEvents.OnAugmentOfferReady += HandleOfferReady;
        GameEvents.OnAugmentSelected   += HandleSelected;

        // 부착 전에 오퍼가 이미 떠 있던 경우(활성화 순서 역전) 복구
        var augment = GameManager.TryGet(out var gm) ? gm.Augment : null;
        if (augment != null && augment.PendingOffer.Count > 0)
            _offer = augment.PendingOffer;
    }

    private void OnDisable()
    {
        GameEvents.OnAugmentOfferReady -= HandleOfferReady;
        GameEvents.OnAugmentSelected   -= HandleSelected;
    }

    private void HandleOfferReady(IReadOnlyList<AugmentData> offer) => _offer = offer;
    private void HandleSelected(AugmentData _) => _offer = null;

    private void OnGUI()
    {
        // Inspector 체크박스로 디버그 UI 활성화/비활성화 가능 (정식 UI 개발 중에만 사용)
        if (!_enableDebugUI) return;

        if (_offer == null || _offer.Count == 0) return;

        var augment = GameManager.TryGet(out var gm) ? gm.Augment : null;
        if (augment == null) return;

        GUI.depth = -100; // 다른 OnGUI(PrototypeHud 등)보다 먼저 이벤트를 받아 모달 클릭 흡수

        if (augment.IsOfferMinimized)
        {
            DrawMinimizedButton(augment);
            return;
        }

        DrawModal(augment);
    }

    /// <summary>내려둔 상태: 화면 하단의 복귀 버튼만 표시(블로킹 해제 — 배치/상점 조작 가능).</summary>
    private void DrawMinimizedButton(AugmentManager augment)
    {
        float w = 260f, h = 34f;
        var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 12f, w, h);
        if (GUI.Button(rect, $"▲ 증강 선택하기 ({FormatRemaining(augment)})"))
            augment.SetOfferMinimized(false);
    }

    private void DrawModal(AugmentManager augment)
    {
        const float cardWidth = 240f, cardHeight = 150f, gap = 16f;
        int count = _offer.Count;

        float totalWidth = count * cardWidth + (count - 1) * gap;
        float panelW = totalWidth + 40f;
        float panelH = cardHeight + 112f;
        float panelX = (Screen.width - panelW) * 0.5f;
        float panelY = (Screen.height - panelH) * 0.5f;
        var panelRect = new Rect(panelX, panelY, panelW, panelH);

        // 패널 바깥만 클릭을 흡수해 카드/최소화 버튼이 입력을 받을 수 있게 한다.
        AbsorbClicksOutside(panelRect);

        GUI.Box(panelRect,
                $"증강을 선택하세요 (3택1) — 남은 시간 {FormatRemaining(augment)} (초과 시 자동 선택)");

        for (int i = 0; i < count; i++)
        {
            AugmentData data = _offer[i];
            if (data == null)
                continue;

            float x = panelX + 20f + i * (cardWidth + gap);
            float y = panelY + 32f;

            GUI.Box(
                new Rect(x, y, cardWidth, cardHeight),
                $"[{data.tier}] {data.augmentName}\n\n{data.description}"
            );

            Rect selectRect =
                new Rect(x, y + cardHeight + 8f, cardWidth, 28f);

            if (GUI.Button(selectRect, $"{data.augmentName} 선택"))
            {
                augment.SelectAugment(data);
                return;
            }
        }

        Rect minimizeRect = new Rect(
            panelX + (panelW - 200f) * 0.5f,
            panelY + panelH - 34f,
            200f,
            26f
        );

        if (GUI.Button(minimizeRect, "▼ 카드 내려두기"))
        {
            augment.SetOfferMinimized(true);
            return;
        }

        // 모달 바깥 클릭만 소비한다.
        // 전체 화면 투명 버튼을 사용하지 않아 카드 선택 버튼 클릭을 방해하지 않는다.
        Event currentEvent = Event.current;

        if (currentEvent != null
            && currentEvent.type == EventType.MouseDown
            && !panelRect.Contains(currentEvent.mousePosition))
        {
            currentEvent.Use();
        }
    }

    private void AbsorbClicksOutside(Rect panelRect)
    {
        _blockerStyle ??= new GUIStyle();

        DrawClickBlocker(new Rect(0f, 0f, Screen.width, panelRect.yMin));
        DrawClickBlocker(new Rect(0f, panelRect.yMax, Screen.width, Screen.height - panelRect.yMax));
        DrawClickBlocker(new Rect(0f, panelRect.yMin, panelRect.xMin, panelRect.height));
        DrawClickBlocker(new Rect(panelRect.xMax, panelRect.yMin, Screen.width - panelRect.xMax, panelRect.height));
    }

    private void DrawClickBlocker(Rect rect)
    {
        if (rect.width > 0f && rect.height > 0f)
            GUI.Button(rect, GUIContent.none, _blockerStyle);
    }

    private static string FormatRemaining(AugmentManager augment)
    {
        int sec = Mathf.CeilToInt(augment.OfferTimeRemaining);
        return $"{sec / 60}:{sec % 60:00}";
    }
}
