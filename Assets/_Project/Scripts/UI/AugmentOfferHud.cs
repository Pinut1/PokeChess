using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 3택1 임시 선택 UI(OnGUI). AugmentManager가 자동 부착 — 씬 배선 불필요.
/// 정식 UI는 태욱님 UIManager 이관 대상(OnAugmentOfferReady 구독 + SelectAugment 호출만 옮기면 됨).
/// </summary>
public class AugmentOfferHud : MonoBehaviour
{
    private IReadOnlyList<AugmentData> _offer;

    private void OnEnable()
    {
        GameEvents.OnAugmentOfferReady += HandleOfferReady;
        GameEvents.OnAugmentSelected   += HandleSelected;

        // 부착 전에 오퍼가 이미 떠 있던 경우(활성화 순서 역전) 복구
        var augment = GameManager.Instance != null ? GameManager.Instance.Augment : null;
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
        if (_offer == null || _offer.Count == 0) return;

        const float cardWidth = 240f, cardHeight = 150f, gap = 16f;
        int count = _offer.Count;

        float totalWidth = count * cardWidth + (count - 1) * gap;
        float panelW = totalWidth + 40f, panelH = cardHeight + 76f;
        float panelX = (Screen.width - panelW) * 0.5f;
        float panelY = (Screen.height - panelH) * 0.5f;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), "증강을 선택하세요 (3택1)");

        for (int i = 0; i < count; i++)
        {
            var data = _offer[i];
            if (data == null) continue;

            float x = panelX + 20f + i * (cardWidth + gap);
            float y = panelY + 32f;

            GUI.Box(new Rect(x, y, cardWidth, cardHeight),
                    $"[{data.tier}] {data.augmentName}\n\n{data.description}");

            if (GUI.Button(new Rect(x, y + cardHeight + 8f, cardWidth, 28f), $"{data.augmentName} 선택"))
            {
                var augment = GameManager.Instance != null ? GameManager.Instance.Augment : null;
                if (augment != null) augment.SelectAugment(data);
                else Debug.LogWarning("[AugmentOfferHud] AugmentManager 없음 — 선택 불가");
                return; // 선택 시 _offer가 비워지므로 이번 프레임 렌더 중단(반복 중 변경 방지)
            }
        }
    }
}
