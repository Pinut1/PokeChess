using UnityEngine;

/// <summary>
/// 프로토타입 HUD (IMGUI). 내 골드 / 파트너 골드 / 팀 라이프 / 라운드·페이즈 / 상점(구매·리롤)을 한 곳에 표시.
/// 정식 uGUI HUD는 UIManager(황해인) 담당 — 이 컴포넌트는 그 전까지 프로토 플레이/검증용 통합 HUD다.
/// 값은 GameManager.Instance.X에서 pull, 파트너 골드만 이벤트(OnPartnerGoldChanged)로 캐시.
/// (씬에 흩어진 디버그 OnGUI(ShopDebugTest 상점, RoundPhaseManager Ready)는 이걸 쓰면 꺼도 됨.)
/// </summary>
public class PrototypeHud : MonoBehaviour
{
    private int _partnerGold = -1; // -1 = 아직 수신 전

    private void OnEnable() => GameEvents.OnPartnerGoldChanged += OnPartnerGold;
    private void OnDisable() => GameEvents.OnPartnerGoldChanged -= OnPartnerGold;
    private void OnPartnerGold(int gold) => _partnerGold = gold;

    private void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        DrawStatusPanel(gm);
        DrawItemShopBar(gm);
        DrawShopBar(gm);
        DrawReady(gm);
        DrawVictory(gm);
    }

    // ── 우측 하단: 준비 완료(쇼핑 중에만) ──
    private void DrawReady(GameManager gm)
    {
        if (gm.Phase == null || gm.Phase.CurrentPhase != GamePhase.Shopping) return;

        var style = new GUIStyle(GUI.skin.button) { fontSize = 24 };
        var rect = new Rect(Screen.width - 230f, Screen.height - 110f, 210f, 80f);
        if (GUI.Button(rect, "준비 완료", style))
            gm.Phase.PlayerReady();
    }

    // ── 중앙: 완주 표시 ──
    private void DrawVictory(GameManager gm)
    {
        if (gm.Phase == null || gm.Phase.CurrentPhase != GamePhase.Victory) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 64, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0, Screen.height / 2f - 60f, Screen.width, 120f), "STAGE CLEAR!", style);
    }

    // ── 상단 좌측: 라운드·페이즈 / 팀 라이프 / 골드 / 레벨·XP / 파트너 골드 / 아이템 정보 ──
    private void DrawStatusPanel(GameManager gm)
    {
        // 레벨 / XP 표시가 추가되어 높이를 180 → 230으로 확장
        GUILayout.BeginArea(new Rect(10, 10, 280, 230), GUI.skin.box);

        if (gm.Phase != null)
            GUILayout.Label($"라운드 {gm.Phase.CurrentRound}   |   {PhaseKr(gm.Phase.CurrentPhase)}");

        if (gm.PlayerHealth != null)
            GUILayout.Label($"팀 라이프: {Mathf.Max(0, gm.PlayerHealth.Health)} / {gm.PlayerHealth.MaxHealth}");

        if (gm.Shop != null)
        {
            GUILayout.Label($"내 골드: {gm.Shop.Gold} G");

            // 레벨 / XP 시스템 검증용 표시
            GUILayout.Space(4);
            GUILayout.Label($"레벨: {gm.Shop.CurrentLevel}");
            GUILayout.Label($"XP: {gm.Shop.CurrentXp} / {gm.Shop.RequiredXp}");
            GUILayout.Label($"배치 가능 기물 수: {gm.Shop.UnitCap}");
        }

        GUILayout.Space(4);
        GUILayout.Label(_partnerGold >= 0 ? $"파트너 골드: {_partnerGold} G" : "파트너 골드: -");

        if (gm.Item != null)
        {
            GUILayout.Space(4);
            GUILayout.Label($"아이템 쿠폰: {gm.Item.ItemCoupon}");
            GUILayout.Label($"아이템 인벤토리: {gm.Item.InventoryCount}/20");
        }

        GUILayout.EndArea();
    }

    // ── 하단 중앙: 유닛 상점 슬롯(구매) + 리롤 + XP 구매 + 판매 ──
    private void DrawShopBar(GameManager gm)
    {
        var shop = gm.Shop;
        if (shop == null) return;

        var slots = shop.CurrentSlots;
        int n = slots?.Count ?? 0;
        const float w = 110f, h = 46f, gap = 6f;

        float totalW = n * (w + gap);
        float startX = (Screen.width - totalW) / 2f;
        float y = Screen.height - 120f;

        for (int i = 0; i < n; i++)
        {
            var data = slots[i];
            GUI.enabled = data != null;

            string label = data != null ? $"{data.pokemonName}\n{data.cost} G" : "(빈 슬롯)";

            if (GUI.Button(new Rect(startX + i * (w + gap), y, w, h), label))
                shop.Buy(i);

            GUI.enabled = true;
        }

        float by = y + h + gap;

        // 유닛 상점 리롤
        if (GUI.Button(new Rect(startX, by, w, 30), "리롤 (2G)"))
            shop.Reroll();

        // XP 구매 버튼
        // 버튼이 직접 레벨업하는 것이 아니라, 골드를 사용해 XP를 구매하고 필요 XP 도달 시 자동 레벨업된다.
        bool canBuyXp = shop.RequiredXp > 0 && shop.Gold >= shop.BuyXpCostGold;

        GUI.enabled = canBuyXp;
        if (GUI.Button(new Rect(startX + (w + gap), by, w, 30), $"XP 구매 ({shop.BuyXpCostGold}G)"))
            shop.BuyXp();
        GUI.enabled = true;

        // 첫 벤치 유닛 판매
        if (GUI.Button(new Rect(startX + (w + gap) * 2, by, w, 30), "첫 벤치 판매"))
        {
            var bench = gm.Board.GetBenchSnapshot();

            for (int i = 0; i < bench.Count; i++)
            {
                if (bench[i] != null)
                {
                    gm.Board.SellUnit(bench[i]);
                    break;
                }
            }
        }

        // XP 구매 버튼 보조 표시
        GUI.Label(
            new Rect(startX + (w + gap), by + 32f, w + 80f, 24f),
            $"Lv.{shop.CurrentLevel}  XP {shop.CurrentXp}/{shop.RequiredXp}  +{shop.BuyXpAmount}XP"
        );

        // 준비 완료는 우측 하단(DrawReady)으로 분리.
    }

    // ── 유닛 상점 위쪽: 아이템 상점 슬롯(쿠폰 구매). 0번 슬롯=진화의 돌, 1~3번 슬롯=일반 아이템 ──
    private void DrawItemShopBar(GameManager gm)
    {
        var shop = gm.Shop;
        if (shop == null) return;

        var slots = shop.CurrentItemSlots;
        int n = slots?.Count ?? 0;
        const float w = 130f, h = 46f, gap = 6f;

        float totalW = n * (w + gap);
        float startX = (Screen.width - totalW) / 2f;
        float y = Screen.height - 190f;

        for (int i = 0; i < n; i++)
        {
            var data = slots[i];

            GUI.enabled = data != null;

            string label = data switch
            {
                EvolutionStoneData stone => $"[돌] {stone.stoneName}\n쿠폰 1",
                ItemData item => $"{item.itemName}\n쿠폰 1",
                _ => "(빈 슬롯)"
            };

            if (GUI.Button(new Rect(startX + i * (w + gap), y, w, h), label))
                shop.BuyItem(i);

            GUI.enabled = true;
        }
    }
    

    private static string PhaseKr(GamePhase p) => p switch
    {
        GamePhase.Lobby => "대기",
        GamePhase.Shopping => "쇼핑",
        GamePhase.Battle => "전투",
        GamePhase.Result => "결과",
        GamePhase.Victory => "완주!",
        GamePhase.GameOver => "게임오버",
        _ => p.ToString()
    };
}