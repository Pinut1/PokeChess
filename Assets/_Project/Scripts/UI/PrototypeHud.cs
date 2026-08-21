using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 프로토타입 HUD (IMGUI). 내 골드 / 파트너 골드 / 팀 라이프 / 라운드·페이즈 / 남은 상점 기능을 한 곳에 표시.
/// 진행 정보와 XP 구매 UI는 UIManager가 담당하며,
/// 이 컴포넌트는 남은 프로토타입 기능의 플레이 검증용 HUD다.
/// 값은 GameManager.Instance.X에서 pull, 파트너 골드만 이벤트(OnPartnerGoldChanged)로 캐시.
/// (씬에 흩어진 디버그 OnGUI(ShopDebugTest 상점, RoundPhaseManager Ready)는 이걸 쓰면 꺼도 됨.)
/// </summary>
public class PrototypeHud : MonoBehaviour
{
    [Tooltip("꺼짐: 유닛/아이템 상점 디버그 텍스트 버튼 바를 숨김(실 UI 카드만 사용). " +
             "켜짐(기본): 기존처럼 화면 하단에 디버그 바도 같이 표시.")]
    [SerializeField] private bool _showShopDebugBar = true;

    private int _partnerGold = -1; // -1 = 아직 수신 전

    private void OnEnable() => GameEvents.OnPartnerGoldChanged += OnPartnerGold;
    private void OnDisable() => GameEvents.OnPartnerGoldChanged -= OnPartnerGold;
    private void OnPartnerGold(int gold) => _partnerGold = gold;

    private Vector2 _qaScroll;

    /// <summary>QA 단축키 — 최종 라운드로 스킵. 게임오버/완주 화면 검증용(최종 라운드 Split 등)을
    /// 매 라운드 다 플레이하지 않고 바로 재현하기 위한 개발 편의 기능. ButtonHotkey는 UGUI Button
    /// 전용이라 이 IMGUI QA 패널엔 안 맞아 직접 키 입력을 본다(ButtonHotkey.cs와 같은 판단 근거).</summary>
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[Key.S].wasPressedThisFrame) return;
        if (IsDebugHotkeyBlocked()) return;
        if (!GameManager.TryGet(out var gm)) return;

        DebugSkipToFinalRound(gm);
    }

    /// <summary>텍스트 입력 중이거나(닉네임 등 S를 문자로 쳐야 하는 상황) 모달 등으로 막혀 있으면
    /// 무시한다 — ButtonHotkey.IsBlocked()와 같은 판단.</summary>
    private static bool IsDebugHotkeyBlocked()
    {
        EventSystem es = EventSystem.current;
        GameObject selected = es != null ? es.currentSelectedGameObject : null;
        if (selected != null && selected.GetComponent<TMP_InputField>() != null) return true;

        return GameplayInputBlock.IsBlocked();
    }

    private void OnGUI()
    {
        if (!GameManager.TryGet(out var gm)) return;

        if (gm.Network != null && gm.Network.IsMasterClient)
        {
            if (GUI.Button(
                    new Rect(Screen.width - 570f, 10f, 200f, 40f),
                    "게임 재시작"))
            {
                gm.Network.RestartGame();
            }
        }

        DrawStatusPanel(gm);
        DrawQaPanel(gm);

        // Ready 버튼은 Canvas의 전투시작 버튼으로 이관되어 이 HUD에서 제거됐다(DrawReady 삭제).
        // 준비 요청 경로도 gm.Phase.PlayerReady() 직접 호출에서 GameEvents.RequestPlayerReady()로 바뀌었다.
        if (_showShopDebugBar)
        {
            DrawShopProbabilityPanel(gm);
            DrawItemShopBar(gm);
            DrawShopBar(gm);
        }

        DrawVictory(gm);
    }

    // ──────────────────────────────────────────
    // QA 강제 실행 버튼
    // ──────────────────────────────────────────
    private void DrawQaPanel(GameManager gm)
    {
        const float panelWidth = 210f;
        const float panelHeight = 360f;

        float x =
            Screen.width -
            panelWidth -
            1400f;

        float y = 530f;

        GUILayout.BeginArea(
            new Rect(
                x,
                y,
                panelWidth,
                panelHeight
            ),
            GUI.skin.box
        );

        _qaScroll = GUILayout.BeginScrollView(
            _qaScroll,
            false,
            true
        );

        GUILayout.Label("── QA 강제 실행 ──");

        GUILayout.Space(4f);

        if (GUILayout.Button(
                "골드 +10",
                GUILayout.Height(30f)))
        {
            DebugAddGold(gm, 10);
        }

        if (GUILayout.Button(
                "아이템 쿠폰 +1",
                GUILayout.Height(30f)))
        {
            DebugAddItemCoupon(gm, 1);
        }

        if (GUILayout.Button(
                "아이템 상점 강제 갱신",
                GUILayout.Height(30f)))
        {
            DebugRefreshItemShop(gm);
        }

        GUILayout.Space(8f);

        // ─────────────────────────────
        // 라운드 디버그
        // ─────────────────────────────

        GUILayout.Label("── 라운드 디버그 ──");

        int lastRound = StageDatabase.Instance != null ? StageDatabase.Instance.LastRound : 0;

        if (GUILayout.Button(
                lastRound > 0 ? $"최종 라운드로 스킵 (1-{lastRound}) [S]" : "최종 라운드로 스킵 [S]",
                GUILayout.Height(30f)))
        {
            DebugSkipToFinalRound(gm);
        }

        GUILayout.Space(8f);

        // ─────────────────────────────
        // 도구 디버그
        // ─────────────────────────────

        GUILayout.Label("── 도구 디버그 ──");

        int reforgerCount =
            gm.Item != null
                ? gm.Item.ReforgerCount
                : 0;

        if (GUILayout.Button(
                $"재조합기 +1  (보유 {reforgerCount})",
                GUILayout.Height(30f)))
        {
            DebugAddReforger(gm, 1);
        }

        if (GUILayout.Button(
                $"재조합기 -1  (보유 {reforgerCount})",
                GUILayout.Height(30f)))
        {
            DebugSpendReforger(gm, 1);
        }

        GUILayout.Space(8f);

        // ─────────────────────────────
        // 코스트별 유닛 획득
        // ─────────────────────────────

        GUILayout.Label("── 코스트별 유닛 획득 ──");

        for (int cost = 1; cost <= 5; cost++)
        {
            int selectedCost = cost;

            int remaining =
                gm.Shop != null
                    ? gm.Shop.GetRemainingPoolCountByCost(selectedCost)
                    : 0;

            if (GUILayout.Button(
                    $"{selectedCost}코 유닛 획득  (남음 {remaining})",
                    GUILayout.Height(30f)))
            {
                DebugGrantUnitByCost(
                    gm,
                    selectedCost
                );
            }
        }

        GUILayout.Space(4f);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DebugAddReforger(
    GameManager gm,
    int amount)
    {
        if (gm.Item == null)
        {
            Debug.LogWarning(
                "[PrototypeHud] ItemManager가 없습니다."
            );

            return;
        }

        bool success =
            gm.Item.AddReforger(amount);

        if (!success)
        {
            Debug.LogWarning(
                $"[PrototypeHud][QA] 재조합기 +{amount} 획득 실패"
            );

            return;
        }

        Debug.Log(
            $"[PrototypeHud][QA] 재조합기 +{amount} 지급 완료 — " +
            $"보유 {gm.Item.ReforgerCount}개"
        );
    }

    private void DebugSpendReforger(
        GameManager gm,
        int amount)
    {
        if (gm.Item == null)
        {
            Debug.LogWarning(
                "[PrototypeHud] ItemManager가 없습니다."
            );

            return;
        }

        bool success =
            gm.Item.SpendReforger(amount);

        if (!success)
        {
            Debug.LogWarning(
                $"[PrototypeHud][QA] 재조합기 -{amount} 실패 — " +
                $"보유 {gm.Item.ReforgerCount}개"
            );

            return;
        }

        Debug.Log(
            $"[PrototypeHud][QA] 재조합기 -{amount} 처리 완료 — " +
            $"보유 {gm.Item.ReforgerCount}개"
        );
    }

    /// <summary>최종 라운드로 즉시 스킵(게임오버/완주 화면 검증용). 마스터클라이언트가 아니면
    /// NetworkManager.BroadcastRoundStart 내부에서 자동으로 무시된다(다른 QA 버튼과 달리 별도
    /// 마스터 체크가 필요 없음 — BroadcastRoundStart가 이미 그렇게 만들어져 있다).
    /// 라운드 번호만 바꿀 뿐 골드/보드/레벨은 그대로 둔다 — 최종 라운드 전투/결과 UI만 빨리
    /// 보고 싶을 때 쓰는 용도라 진행 상태를 흉내 낼 필요가 없다.</summary>
    private void DebugSkipToFinalRound(GameManager gm)
    {
        if (gm.Network == null)
        {
            Debug.LogWarning("[PrototypeHud] NetworkManager가 없습니다.");
            return;
        }

        int lastRound = StageDatabase.Instance != null ? StageDatabase.Instance.LastRound : 0;
        if (lastRound <= 0)
        {
            Debug.LogWarning("[PrototypeHud] StageDatabase가 비어있어 최종 라운드를 알 수 없습니다.");
            return;
        }

        gm.Network.BroadcastRoundStart(lastRound);
        Debug.Log($"[PrototypeHud][QA] 최종 라운드(1-{lastRound})로 스킵");
    }

    private void DebugAddGold(
    GameManager gm,
    int amount)
    {
        if (gm.Shop == null)
        {
            Debug.LogWarning(
                "[PrototypeHud] ShopManager가 없습니다."
            );

            return;
        }

        gm.Shop.AddGold(amount);

        Debug.Log(
            $"[PrototypeHud][QA] 골드 +{amount} 지급 완료"
        );
    }

    private void DebugAddItemCoupon(
        GameManager gm,
        int amount)
    {
        if (gm.Item == null)
        {
            Debug.LogWarning(
                "[PrototypeHud] ItemManager가 없습니다."
            );

            return;
        }

        gm.Item.AddItemCoupon(amount);

        Debug.Log(
            $"[PrototypeHud][QA] 아이템 쿠폰 +{amount} 지급 완료"
        );
    }

    private void DebugRefreshItemShop(
        GameManager gm)
    {
        if (gm.Shop == null)
        {
            Debug.LogWarning(
                "[PrototypeHud] ShopManager가 없습니다."
            );

            return;
        }

        gm.Shop.RollItemShop();

        Debug.Log(
            "[PrototypeHud][QA] 아이템 상점 강제 갱신 완료"
        );
    }

    private void DebugGrantUnitByCost(
        GameManager gm,
        int cost)
    {
        if (gm.Shop == null ||
            gm.Board == null)
        {
            Debug.LogWarning(
                "[PrototypeHud] ShopManager 또는 BoardManager가 없습니다."
            );

            return;
        }

        bool success =
            gm.Shop.DebugGrantUnitByCost(cost);

        if (!success)
        {
            Debug.LogWarning(
                $"[PrototypeHud][QA] {cost}코 유닛 획득 실패"
            );

            return;
        }

        Debug.Log(
            $"[PrototypeHud][QA] {cost}코 유닛 획득 요청 완료"
        );
    }

    // ── 중앙: 완주 표시 ──
    private void DrawVictory(GameManager gm)
    {
        if (gm.Phase == null || gm.Phase.CurrentPhase != GamePhase.Victory) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 64, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0, Screen.height / 2f - 60f, Screen.width, 120f), "STAGE CLEAR!", style);
    }

    // ── 상단 좌측: 라운드·페이즈 / 팀 라이프 / 골드 / 파트너 골드 / 아이템 정보 ──
    private void DrawStatusPanel(GameManager gm)
    {
        const float panelX = 10f;
        const float panelY = 10f;
        const float panelWidth = 280f;
        const float panelHeight = 320f;

        GUILayout.BeginArea(
            new Rect(
                panelX,
                panelY,
                panelWidth,
                panelHeight
            ),
            GUI.skin.box
        );

        // 기존 Scene / Is Master / Players 표시 아래로 내용 내리기
        GUILayout.Space(68f);

        if (gm.Phase != null)
            GUILayout.Label(
                $"라운드 {gm.Phase.CurrentRound} | " +
                $"{PhaseKr(gm.Phase.CurrentPhase)}"
            );

        if (gm.PlayerHealth != null)
            GUILayout.Label(
                $"팀 라이프: " +
                $"{Mathf.Max(0, gm.PlayerHealth.Health)} / " +
                $"{gm.PlayerHealth.MaxHealth}"
            );

        string infHpLabel =
            NetworkManager.DebugInfiniteTeamHealth
                ? "무한 HP: ON ▣"
                : "무한 HP: OFF ☐";

        if (GUILayout.Button(infHpLabel))
            NetworkManager.DebugInfiniteTeamHealth =
                !NetworkManager.DebugInfiniteTeamHealth;

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
            GUILayout.Label($"아이템 쿠폰: {gm.Item.ItemCoupon}");
            GUILayout.Label(
                $"아이템 인벤토리: {gm.Item.InventoryCount}/20"
            );
        }

        GUILayout.EndArea();
    }

    // ──────────────────────────────────────────
    // 아이템 상점 위쪽: 공용 풀 상태 + 코스트별 등장 확률
    // ──────────────────────────────────────────
    private void DrawShopProbabilityPanel(GameManager gm)
    {
        var shop = gm.Shop;
        if (shop == null)
            return;

        const float width = 749f;
        const float height = 64f;

        float x = (Screen.width - width) / 2f;
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
                ? "2인 공용 풀 ON · 내 역할: MasterClient"
                : "2인 공용 풀 ON · 내 역할: 파트너";
        }

        GUILayout.Label(poolStatus, titleStyle);

        int[] rates = shop.GetCurrentCostRatesForDebug();

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
    // 하단 중앙: 유닛 상점 슬롯 + 풀 잔여 수량 + 등장 확률
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
        // 준비 완료 입력은 Canvas의 BattleReady_Button이 담당한다.
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
        float y = Screen.height - 225f;

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
