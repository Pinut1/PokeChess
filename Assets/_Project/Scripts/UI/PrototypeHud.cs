using UnityEngine;

/// <summary>
/// 프로토타입 HUD (IMGUI).
/// 내 골드 / 파트너 골드 / 팀 라이프 / 라운드·페이즈 /
/// 상점(구매·리롤) / 공용 풀 잔여 수량 / 상점 등장 확률을 표시한다.
///
/// 공용 풀 표시 계산:
/// 현재 레벨 코스트 확률
/// × 해당 포켓몬 남은 수량
/// ÷ 같은 코스트 전체 남은 수량
/// </summary>
public class PrototypeHud : MonoBehaviour
{
    private int _partnerGold = -1; // -1 = 아직 수신 전

    private void OnEnable()
    {
        GameEvents.OnPartnerGoldChanged += OnPartnerGold;
    }

    private void OnDisable()
    {
        GameEvents.OnPartnerGoldChanged -= OnPartnerGold;
    }

    private void OnPartnerGold(int gold)
    {
        _partnerGold = gold;
    }

    private void OnGUI()
    {
        var gm = GameManager.Instance;

        if (gm == null)
            return;

        DrawStatusPanel(gm);

        // 하단 UI 배치 순서:
        // 상점 확률표 → 아이템 상점 → 유닛 상점
        DrawShopProbabilityPanel(gm);
        DrawItemShopBar(gm);
        DrawShopBar(gm);

        DrawReady(gm);
        DrawVictory(gm);
    }

    // ──────────────────────────────────────────
    // 우측 하단: 준비 완료
    // ──────────────────────────────────────────

    private void DrawReady(GameManager gm)
    {
        if (gm.Phase == null ||
            gm.Phase.CurrentPhase != GamePhase.Shopping)
        {
            return;
        }

        var style = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24
        };

        var rect = new Rect(
            Screen.width - 230f,
            Screen.height - 110f,
            210f,
            80f
        );

        if (GUI.Button(rect, "준비 완료", style))
            gm.Phase.PlayerReady();
    }

    // ──────────────────────────────────────────
    // 중앙: 완주 표시
    // ──────────────────────────────────────────

    private void DrawVictory(GameManager gm)
    {
        if (gm.Phase == null ||
            gm.Phase.CurrentPhase != GamePhase.Victory)
        {
            return;
        }

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 64,
            alignment = TextAnchor.MiddleCenter
        };

        GUI.Label(
            new Rect(
                0f,
                Screen.height / 2f - 60f,
                Screen.width,
                120f
            ),
            "STAGE CLEAR!",
            style
        );
    }

    // ──────────────────────────────────────────
    // 상단 좌측: 기본 상태 정보
    // ──────────────────────────────────────────

    private void DrawStatusPanel(GameManager gm)
    {
        GUILayout.BeginArea(
            new Rect(10f, 10f, 280f, 268f),
            GUI.skin.box
        );

        if (gm.Phase != null)
        {
            GUILayout.Label(
                $"라운드 {gm.Phase.CurrentRound}   |   " +
                $"{PhaseKr(gm.Phase.CurrentPhase)}"
            );
        }

        if (gm.PlayerHealth != null)
        {
            GUILayout.Label(
                $"팀 라이프: " +
                $"{Mathf.Max(0, gm.PlayerHealth.Health)} / " +
                $"{gm.PlayerHealth.MaxHealth}"
            );
        }

        string infHpLabel =
            NetworkManager.DebugInfiniteTeamHealth
                ? "무한 HP: ON ▣"
                : "무한 HP: OFF ☐";

        if (GUILayout.Button(infHpLabel))
        {
            NetworkManager.DebugInfiniteTeamHealth =
                !NetworkManager.DebugInfiniteTeamHealth;
        }

        if (gm.Shop != null)
            GUILayout.Label($"내 골드: {gm.Shop.Gold} G");

        GUILayout.Space(4f);

        GUILayout.Label(
            _partnerGold >= 0
                ? $"파트너 골드: {_partnerGold} G"
                : "파트너 골드: -"
        );

        if (gm.Item != null)
        {
            GUILayout.Space(4f);

            GUILayout.Label(
                $"아이템 쿠폰: {gm.Item.ItemCoupon}"
            );

            GUILayout.Label(
                $"아이템 인벤토리: " +
                $"{gm.Item.InventoryCount}/20"
            );
        }

        GUILayout.EndArea();
    }

    // ──────────────────────────────────────────
    // 아이템 상점 바로 위:
    // 공용 풀 상태 + 코스트별 등장 확률
    // ──────────────────────────────────────────

    private void DrawShopProbabilityPanel(GameManager gm)
    {
        var shop = gm.Shop;

        if (shop == null)
            return;

        // 유닛 상점 전체 폭과 비슷한 크기로 맞춘다.
        const float width = 749f;
        const float height = 64f;

        float x = (Screen.width - width) / 2f;

        // 아이템 상점 위치:
        // Screen.height - 225
        //
        // 확률표 높이:
        // 64
        //
        // 두 UI 사이 간격:
        // 약 10px
        float y = Screen.height - 299f;

        GUILayout.BeginArea(
            new Rect(x, y, width, height),
            GUI.skin.box
        );

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 14
        };

        var probabilityStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14
        };

        string poolStatus = "솔로 상점 풀";

        if (gm.Network != null &&
            gm.Network.UsesSharedShopPool)
        {
            poolStatus = gm.Network.IsMasterClient
                ? "2인 공용 풀 ON · 내 역할: MasterClient (풀 권위)"
                : "2인 공용 풀 ON · 내 역할: 파트너 · 풀 권위: MasterClient";
        }

        GUILayout.Label(poolStatus, titleStyle);

        int[] rates = shop.GetCurrentCostRatesForDebug();

        // 레벨은 UIManager의 플레이어 정보창에서 표시한다.
        // 여기서는 중복 표시하지 않고 코스트 확률만 보여준다.
        string rateText =
            $"1코 {GetRate(rates, 0)}%   " +
            $"2코 {GetRate(rates, 1)}%   " +
            $"3코 {GetRate(rates, 2)}%   " +
            $"4코 {GetRate(rates, 3)}%   " +
            $"5코 {GetRate(rates, 4)}%";

        GUILayout.Label(rateText, probabilityStyle);

        GUILayout.EndArea();
    }

    // ──────────────────────────────────────────
    // 하단 중앙: 유닛 상점
    // ──────────────────────────────────────────

    private void DrawShopBar(GameManager gm)
    {
        var shop = gm.Shop;

        if (shop == null)
            return;

        var slots = shop.CurrentSlots;
        int count = slots?.Count ?? 0;

        const float width = 145f;
        const float height = 84f;
        const float gap = 6f;

        float totalWidth =
            count > 0
                ? count * width + (count - 1) * gap
                : 0f;

        float startX = (Screen.width - totalWidth) / 2f;
        float y = Screen.height - 158f;

        for (int i = 0; i < count; i++)
        {
            PokemonData data = slots[i];

            GUI.enabled = data != null;

            string label;

            if (data == null)
            {
                label = "(빈 슬롯)";
            }
            else if (shop.TryGetPoolDebugInfo(
                         data,
                         out int remaining,
                         out int initial,
                         out int sameCostRemaining,
                         out float costRatePercent,
                         out float appearancePercent))
            {
                label =
                    $"{data.pokemonName}  {data.cost}G\n" +
                    $"풀 {remaining}/{initial}\n" +
                    $"다음 등장 {appearancePercent:0.00}%\n" +
                    $"{costRatePercent:0.#}% × " +
                    $"{remaining}/{sameCostRemaining}";
            }
            else
            {
                label =
                    $"{data.pokemonName}\n" +
                    $"{data.cost}G\n" +
                    "풀 정보 없음";
            }

            var buttonRect = new Rect(
                startX + i * (width + gap),
                y,
                width,
                height
            );

            if (GUI.Button(buttonRect, label))
                shop.Buy(i);

            GUI.enabled = true;
        }

        float buttonY = y + height + gap;

        string rerollLabel =
            shop.RerollCount > 0
                ? $"리롤 (무료 {shop.RerollCount})"
                : $"리롤 ({shop.RerollCost}G)";

        if (GUI.Button(
                new Rect(
                    startX,
                    buttonY,
                    width,
                    30f
                ),
                rerollLabel))
        {
            shop.Reroll();
        }

        if (GUI.Button(
                new Rect(
                    startX + width + gap,
                    buttonY,
                    width,
                    30f
                ),
                "첫 벤치 판매"))
        {
            if (gm.Board == null)
                return;

            var bench = gm.Board.GetBenchSnapshot();

            for (int i = 0; i < bench.Count; i++)
            {
                if (bench[i] == null)
                    continue;

                gm.Board.SellUnit(bench[i]);
                break;
            }
        }
    }

    // ──────────────────────────────────────────
    // 유닛 상점 위쪽: 아이템 상점
    // ──────────────────────────────────────────

    private void DrawItemShopBar(GameManager gm)
    {
        var shop = gm.Shop;

        if (shop == null)
            return;

        var slots = shop.CurrentItemSlots;
        int count = slots?.Count ?? 0;

        const float width = 130f;
        const float height = 46f;
        const float gap = 6f;

        float totalWidth =
            count > 0
                ? count * width + (count - 1) * gap
                : 0f;

        float startX = (Screen.width - totalWidth) / 2f;

        // 상점 풀 확률표 바로 아래,
        // 유닛 상점 바로 위에 배치한다.
        float y = Screen.height - 225f;

        for (int i = 0; i < count; i++)
        {
            ScriptableObject data = slots[i];

            GUI.enabled = data != null;

            string label = data switch
            {
                EvolutionStoneData stone =>
                    $"[돌] {stone.stoneName}\n쿠폰 1",

                ItemData item =>
                    $"{item.itemName}\n쿠폰 1",

                _ =>
                    "(빈 슬롯)"
            };

            if (GUI.Button(
                    new Rect(
                        startX + i * (width + gap),
                        y,
                        width,
                        height
                    ),
                    label))
            {
                shop.BuyItem(i);
            }

            GUI.enabled = true;
        }
    }

    private static int GetRate(int[] rates, int index)
    {
        if (rates == null ||
            index < 0 ||
            index >= rates.Length)
        {
            return 0;
        }

        return rates[index];
    }

    private static string PhaseKr(GamePhase phase)
    {
        return phase switch
        {
            GamePhase.Lobby => "대기",
            GamePhase.Shopping => "쇼핑",
            GamePhase.Battle => "전투",
            GamePhase.Result => "결과",
            GamePhase.Victory => "완주!",
            GamePhase.GameOver => "게임오버",
            _ => phase.ToString()
        };
    }
}