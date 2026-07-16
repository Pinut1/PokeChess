using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 UI 표시와 입력 연결을 담당한다. 김태욱 파트.
///
/// 현재 구현 범위:
/// 1. 플레이어 진행 HUD
///    - 골드, 레벨, XP, 배치 가능 기물 수 표시
///    - GameEvents 변경 이벤트를 구독해 표시값 캐시
///    - XP 구매 입력을 ShopManager.BuyXp()에 연결
/// 2. 전적창 MVP
///    - MatchHistoryStore.LoadRecent()를 이용한 과거 전적 조회
///    - GameEvents.OnMatchRecorded 구독을 통한 신규 전적 실시간 반영
///    - 최근 전적 목록과 선택 전적 상세 표시
///
/// 구조 원칙:
/// - 진행 상태는 OnGUI에서 ShopManager를 매번 조회하지 않는다.
/// - 상태 변경 시 GameEvents를 통해 전달받아 로컬 캐시에 저장하고, OnGUI는 캐시만 그린다.
/// - Start의 SyncProgressState()는 이벤트 구독 이전에 발생한 초기값을 보완하기 위한 1회 동기화다.
/// - 현재 표시는 OnGUI 기반 MVP이며, 추후 Canvas/TMP UI로 교체해도 이벤트 구독 구조는 그대로 재사용한다.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Match History")]
    [SerializeField] private int _recentMatchCount = 10;

    // ──────────────────────────────────────────
    // 전적 데이터 및 전적창 상태
    // ──────────────────────────────────────────

    private readonly List<MatchRecord> _recentMatches = new List<MatchRecord>();

    private bool _showMatchHistory;
    private int _selectedMatchIndex;
    private Vector2 _listScroll;
    private Vector2 _detailScroll;

    private Rect _matchHistoryWindowRect = new Rect(260f, 80f, 980f, 650f);

    // ──────────────────────────────────────────
    // 플레이어 진행 상태 캐시
    // ──────────────────────────────────────────
    // 아래 값은 GameEvents를 통해 갱신한다.
    // OnGUI에서 ShopManager.CurrentXp 등을 직접 조회하지 않아 매 프레임 폴링을 피한다.
    // XP 구매 비용/획득량은 런타임 중 변하지 않는 설정값이므로 시작 시 한 번만 가져온다.

    private int _gold;
    private int _currentLevel = 1;
    private int _currentXp;
    private int _requiredXp;
    private int _unitCap = 1;

    private int _buyXpCostGold;
    private int _buyXpAmount;

    // ──────────────────────────────────────────
    // OnGUI 스타일 캐시
    // ──────────────────────────────────────────

    private GUIStyle _titleStyle;
    private GUIStyle _sectionTitleStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _smallStyle;
    private GUIStyle _mutedStyle;
    private GUIStyle _winStyle;
    private GUIStyle _loseStyle;
    private GUIStyle _badgeStyle;
    private GUIStyle _unitCardStyle;
    private GUIStyle _selectedListCardStyle;
    private GUIStyle _normalListCardStyle;

    // ──────────────────────────────────────────
    // Unity 생명주기 / 이벤트 구독
    // ──────────────────────────────────────────
    private void OnEnable()
    {
        GameEvents.OnMatchRecorded += HandleMatchRecorded;

        GameEvents.OnGoldChanged += HandleGoldChanged;
        GameEvents.OnLevelChanged += HandleLevelChanged;
        GameEvents.OnXpChanged += HandleXpChanged;
        GameEvents.OnUnitCapChanged += HandleUnitCapChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchRecorded -= HandleMatchRecorded;

        GameEvents.OnGoldChanged -= HandleGoldChanged;
        GameEvents.OnLevelChanged -= HandleLevelChanged;
        GameEvents.OnXpChanged -= HandleXpChanged;
        GameEvents.OnUnitCapChanged -= HandleUnitCapChanged;
    }

    private void Start()
    {
        RefreshMatchHistory();
        SyncProgressState();
    }


    /// <summary>
    /// UI가 이벤트 구독을 시작하기 전에 ShopManager가 초기 이벤트를 발행했을 가능성에 대비해
    /// 현재 진행 상태를 한 번만 직접 가져온다.
    ///
    /// 이 메서드는 Start에서 1회 실행되며, OnGUI에서 반복 호출하는 폴링이 아니다.
    /// 이후 골드/레벨/XP/유닛 캡 변화는 각 GameEvents 핸들러가 캐시를 갱신한다.
    /// </summary>
    private void SyncProgressState()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Shop == null)
            return;

        var shop = gm.Shop;

        _gold = shop.Gold;
        _currentLevel = shop.CurrentLevel;
        _currentXp = shop.CurrentXp;
        _requiredXp = shop.RequiredXp;
        _unitCap = shop.UnitCap;

        // 구매 비용과 획득량은 현재 별도 변경 이벤트가 없는 고정 설정값이므로 초기화 시 캐시한다.
        _buyXpCostGold = shop.BuyXpCostGold;
        _buyXpAmount = shop.BuyXpAmount;
    }

    /// <summary>골드 변경 이벤트를 받아 HUD 표시용 캐시를 갱신한다.</summary>
    private void HandleGoldChanged(int gold)
    {
        _gold = gold;
    }

    /// <summary>레벨 변경 이벤트를 받아 HUD 표시용 캐시를 갱신한다.</summary>
    private void HandleLevelChanged(int level)
    {
        _currentLevel = level;
    }

    /// <summary>XP 변경 이벤트를 받아 현재 XP와 다음 레벨 필요 XP를 함께 갱신한다.</summary>
    private void HandleXpChanged(int currentXp, int requiredXp)
    {
        _currentXp = currentXp;
        _requiredXp = requiredXp;
    }

    /// <summary>레벨에 따른 보드 배치 가능 기물 수 변경을 HUD 캐시에 반영한다.</summary>
    private void HandleUnitCapChanged(int unitCap)
    {
        _unitCap = unitCap;
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawOpenButton();
        DrawProgressPanel();

        if (_showMatchHistory)
            DrawMatchHistoryWindow();
    }


    // ──────────────────────────────────────────
    // 플레이어 진행 HUD
    // ──────────────────────────────────────────

    /// <summary>
    /// 이벤트로 갱신된 진행 상태 캐시를 화면에 표시한다.
    /// 이 메서드는 표시 과정에서 ShopManager의 현재 상태를 직접 조회하지 않는다.
    /// 단, 사용자가 XP 구매 버튼을 눌렀을 때만 ShopManager.BuyXp()를 호출한다.
    /// </summary>
    private void DrawProgressPanel()
    {
        // 인벤토리와 유닛 상점 사이의 좁은 공간에 맞춘 축소 크기.
        const float width = 150f;
        const float height = 145f;

        // PrototypeHud의 현재 유닛 상점 배치값과 동일하게 맞춘다.
        const int shopSlotCount = 5;
        const float shopSlotWidth = 145f;
        const float shopSlotGap = 6f;

        float shopTotalWidth =
            shopSlotCount * shopSlotWidth +
            (shopSlotCount - 1) * shopSlotGap;

        float shopStartX =
            (Screen.width - shopTotalWidth) / 2f;

        // 유닛 상점 바로 왼쪽에 4px 간격을 두고 배치한다.
        // 창 폭을 줄여 왼쪽 인벤토리와도 겹치지 않게 한다.
        float x = Mathf.Max(
            10f,
            shopStartX - width - 4f
        );

        float y = Screen.height - 190f;

        GUI.Box(
            new Rect(x, y, width, height),
            "플레이어 정보"
        );

        GUI.Label(
            new Rect(x + 10f, y + 28f, width - 20f, 22f),
            $"골드: {_gold} G"
        );

        GUI.Label(
            new Rect(x + 10f, y + 50f, width - 20f, 22f),
            $"레벨: {_currentLevel} · 배치: {_unitCap}"
        );

        string xpText = _requiredXp > 0
            ? $"XP: {_currentXp} / {_requiredXp}"
            : "XP: MAX";

        GUI.Label(
            new Rect(x + 10f, y + 72f, width - 20f, 22f),
            xpText
        );

        bool hasPurchaseConfig =
            _buyXpCostGold > 0 &&
            _buyXpAmount > 0;

        bool canBuyXp =
            hasPurchaseConfig &&
            _requiredXp > 0 &&
            _gold >= _buyXpCostGold;

        GUI.enabled = canBuyXp;

        string buttonLabel = hasPurchaseConfig
            ? $"XP 구매\n{_buyXpCostGold}G → +{_buyXpAmount}XP"
            : "XP 구매 준비 중";

        var buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter
        };

        if (GUI.Button(
            new Rect(
                x + 10f,
                y + 98f,
                width - 20f,
                38f
            ),
            buttonLabel,
            buttonStyle
        ))
        {
            var gm = GameManager.Instance;

            if (gm != null && gm.Shop != null)
                gm.Shop.BuyXp();
        }

        GUI.enabled = true;
    }

    // ──────────────────────────────────────────
    // 전적창 UI
    // ──────────────────────────────────────────

    /// <summary>
    /// 화면 상단 중앙에 전적창 열기/닫기 버튼을 표시한다.
    /// 전적창을 열 때는 로컬 저장소의 최신 전적을 다시 불러온다.
    /// </summary>
    private void DrawOpenButton()
    {
        const float width = 160f;
        const float height = 40f;

        float x = (Screen.width - width) * 0.5f;
        float y = 20f;

        string label = _showMatchHistory ? "전적창 닫기" : "전적창 열기";

        if (GUI.Button(new Rect(x, y, width, height), label))
        {
            _showMatchHistory = !_showMatchHistory;

            if (_showMatchHistory)
                RefreshMatchHistory();
        }
    }

    /// <summary>
    /// 이동 가능한 전적창 윈도우를 생성한다.
    /// 창이 화면 밖으로 완전히 벗어나지 않도록 현재 위치를 보정한다.
    /// </summary>
    private void DrawMatchHistoryWindow()
    {
        _matchHistoryWindowRect.x = Mathf.Clamp(_matchHistoryWindowRect.x, 0f, Screen.width - 160f);
        _matchHistoryWindowRect.y = Mathf.Clamp(_matchHistoryWindowRect.y, 0f, Screen.height - 120f);

        _matchHistoryWindowRect = GUI.Window(
            1001,
            _matchHistoryWindowRect,
            DrawMatchHistoryWindowContent,
            "전적"
        );
    }

    /// <summary>
    /// 전적창 내부 전체 구성을 그린다.
    /// 상단 요약, 왼쪽 전적 목록, 오른쪽 선택 전적 상세 영역으로 구성된다.
    /// 저장된 전적이 없으면 빈 상태 안내만 표시한다.
    /// </summary>
    private void DrawMatchHistoryWindowContent(int windowId)
    {
        float width = _matchHistoryWindowRect.width;
        float height = _matchHistoryWindowRect.height;

        DrawHeader(width);

        if (_recentMatches.Count == 0)
        {
            GUI.Label(new Rect(28f, 95f, 500f, 30f), "저장된 전적이 없습니다.", _labelStyle);
            GUI.DragWindow(new Rect(0f, 0f, width, 28f));
            return;
        }

        _selectedMatchIndex = Mathf.Clamp(_selectedMatchIndex, 0, _recentMatches.Count - 1);

        // 왼쪽: 최근 전적 목록
        DrawMatchList(new Rect(24f, 120f, 300f, height - 145f));
        // 오른쪽: 현재 선택한 전적의 상세 정보
        DrawMatchDetail(new Rect(340f, 120f, width - 365f, height - 145f), _recentMatches[_selectedMatchIndex]);

        GUI.DragWindow(new Rect(0f, 0f, width, 28f));
    }

    /// <summary>
    /// 전적창 헤더를 표시한다.
    /// 최근 전적 수와 해당 목록 기준 승률을 계산해 함께 보여준다.
    /// </summary>
    private void DrawHeader(float width)
    {
        GUI.Label(new Rect(24f, 32f, 220f, 36f), "전적", _titleStyle);
        GUI.Label(new Rect(24f, 67f, 420f, 24f), "최근 플레이 기록 · 카드를 누르면 상세가 열립니다", _mutedStyle);

        int total = _recentMatches.Count;
        int winCount = CountWins();
        int winRate = total > 0 ? Mathf.RoundToInt((float)winCount / total * 100f) : 0;

        GUI.Label(new Rect(width - 210f, 34f, 60f, 24f), total.ToString(), _sectionTitleStyle);
        GUI.Label(new Rect(width - 205f, 60f, 80f, 20f), "표시 전적", _mutedStyle);

        GUI.Label(new Rect(width - 120f, 34f, 80f, 24f), $"{winRate}%", _sectionTitleStyle);
        GUI.Label(new Rect(width - 110f, 60f, 80f, 20f), "승률", _mutedStyle);

        if (GUI.Button(new Rect(width - 100f, 84f, 70f, 28f), "닫기"))
            _showMatchHistory = false;

        if (GUI.Button(new Rect(width - 180f, 84f, 70f, 28f), "새로고침"))
            RefreshMatchHistory();
    }

    /// <summary>
    /// 최근 전적 목록을 스크롤 영역으로 표시한다.
    /// 각 전적은 선택 가능한 카드 형태로 그려진다.
    /// </summary>
    private void DrawMatchList(Rect rect)
    {
        GUI.Box(rect, "");

        float contentHeight = Mathf.Max(rect.height - 20f, _recentMatches.Count * 92f + 10f);

        _listScroll = GUI.BeginScrollView(
            rect,
            _listScroll,
            new Rect(0f, 0f, rect.width - 20f, contentHeight)
        );

        for (int i = 0; i < _recentMatches.Count; i++)
        {
            DrawMatchListCard(_recentMatches[i], i, new Rect(8f, 8f + i * 92f, rect.width - 36f, 82f));
        }

        GUI.EndScrollView();
    }

    /// <summary>
    /// 전적 목록의 카드 한 장을 표시한다.
    /// 승패, 최종 스테이지, 라운드, 플레이 모드, 플레이 시간과 경과 시간을 요약한다.
    /// 카드를 누르면 해당 전적이 상세 영역의 선택 대상으로 지정된다.
    /// </summary>
    private void DrawMatchListCard(MatchRecord record, int index, Rect rect)
    {
        if (record == null)
            return;

        bool selected = index == _selectedMatchIndex;
        GUI.Box(rect, "", selected ? _selectedListCardStyle : _normalListCardStyle);

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            _selectedMatchIndex = index;

        bool isWin = IsVictory(record);
        string resultText = isWin ? "승리" : "패배";
        string stageText = string.IsNullOrWhiteSpace(record.finalStageId) ? "-" : record.finalStageId;
        string modeText = GetPlayModeText(record);
        string durationText = FormatDurationShort(record.durationSeconds);
        string relativeText = FormatRelativeTime(record.endedAtUtc);

        GUI.Label(
            new Rect(rect.x + 12f, rect.y + 10f, 80f, 22f),
            resultText,
            isWin ? _winStyle : _loseStyle
        );

        GUI.Label(
            new Rect(rect.x + rect.width - 95f, rect.y + 10f, 80f, 22f),
            $"{stageText}  R{record.finalRound}",
            _smallStyle
        );

        GUI.Label(
            new Rect(rect.x + 12f, rect.y + 38f, 190f, 20f),
            $"{modeText} · {durationText}",
            _smallStyle
        );

        GUI.Label(
            new Rect(rect.x + rect.width - 85f, rect.y + 38f, 75f, 20f),
            relativeText,
            _mutedStyle
        );

        if (IsDisconnectedMatch(record))
        {
            GUI.Label(
                new Rect(rect.x + 12f, rect.y + 58f, 120f, 20f),
                "⚠ 연결 끊김",
                _loseStyle
            );
        }
    }

    /// <summary>
    /// 현재 선택한 전적의 상세 정보를 표시한다.
    /// 경기 결과와 본인·파트너 기록을 출력하고,
    /// 하단에는 개발 확인용 매치 메타 정보를 표시한다.
    /// </summary>
    private void DrawMatchDetail(Rect rect, MatchRecord record)
    {
        GUI.Box(rect, "");

        bool isWin = IsVictory(record);
        string resultText = isWin ? "승리" : "패배";
        string stageText = string.IsNullOrWhiteSpace(record.finalStageId) ? "-" : record.finalStageId;
        string durationText = FormatDurationShort(record.durationSeconds);
        string modeText = GetPlayModeText(record);

        GUI.Label(
            new Rect(rect.x + 22f, rect.y + 18f, 130f, 36f),
            resultText,
            isWin ? _winStyle : _loseStyle
        );
        if (IsDisconnectedMatch(record))
        {
            GUI.Label(
                new Rect(rect.x + 22f, rect.y + 58f, 160f, 22f),
                "⚠ 연결 끊김",
                _loseStyle
            );
        }

        GUI.Label(new Rect(rect.x + rect.width - 220f, rect.y + 20f, 60f, 24f), stageText, _sectionTitleStyle);
        GUI.Label(new Rect(rect.x + rect.width - 160f, rect.y + 20f, 60f, 24f), durationText, _sectionTitleStyle);
        GUI.Label(new Rect(rect.x + rect.width - 85f, rect.y + 20f, 60f, 24f), modeText, _sectionTitleStyle);

        GUI.Label(new Rect(rect.x + rect.width - 220f, rect.y + 47f, 80f, 20f), $"스테이지 R{record.finalRound}", _mutedStyle);
        GUI.Label(new Rect(rect.x + rect.width - 160f, rect.y + 47f, 60f, 20f), "플레이", _mutedStyle);
        GUI.Label(new Rect(rect.x + rect.width - 85f, rect.y + 47f, 60f, 20f), "모드", _mutedStyle);

        GUI.Box(new Rect(rect.x, rect.y + 82f, rect.width, 1f), "");

        Rect scrollRect = new Rect(rect.x + 18f, rect.y + 98f, rect.width - 36f, rect.height - 120f);
        Rect contentRect = new Rect(0f, 0f, scrollRect.width - 20f, GetDetailContentHeight(record));

        _detailScroll = GUI.BeginScrollView(scrollRect, _detailScroll, contentRect);

        float y = 0f;

        y = DrawPlayerSection(record.self, "나", y);

        if (HasPartner(record))
        {
            y += 18f;
            y = DrawPlayerSection(record.partner, "파트너", y);
        }
        else
        {
            y += 18f;
            GUI.Box(new Rect(0f, y, contentRect.width - 5f, 42f), "");
            GUI.Label(new Rect(12f, y + 11f, 520f, 20f), "파트너 기록 없음 — 솔로 플레이로 표시됩니다.", _mutedStyle);
            y += 52f;
        }

        // 개발/디버그용 전적 메타 정보.
        // - matchId: 전적 1건의 고유 ID
        // - gameVersion: 저장 당시 게임 버전
        // - endedAtUtc: 전적 종료 날짜
        // 실제 유저용 전적창에서는 없어도 되는 정보이므로,
        // 화면을 더 깔끔하게 만들고 싶으면 아래 GUI.Label 블록은 삭제해도 된다.
        y += 10f;
        GUI.Label(
            new Rect(0f, y, contentRect.width - 5f, 22f),
            $"match {SafeText(record.matchId)} · v{SafeText(record.gameVersion)} · {FormatDate(record.endedAtUtc)}",
            _mutedStyle
        );

        GUI.EndScrollView();
    }

    /// <summary>
    /// 본인 또는 파트너 한 명의 전적 정보를 표시한다.
    /// 닉네임, 레벨, 보드 유닛, 활성 시너지와 증강 정보를 순서대로 그린다.
    /// 반환값은 다음 UI 요소를 배치할 Y 좌표다.
    /// </summary>
    private float DrawPlayerSection(PlayerRecord player, string roleLabel, float y)
    {
        if (player == null)
        {
            GUI.Label(new Rect(0f, y, 400f, 24f), $"{roleLabel}: 기록 없음", _mutedStyle);
            return y + 34f;
        }

        string nickname = string.IsNullOrWhiteSpace(player.nickname) ? "-" : player.nickname;
        int level = player.level;

        GUI.Label(new Rect(0f, y, 58f, 24f), roleLabel, _badgeStyle);
        GUI.Label(new Rect(68f, y, 260f, 24f), nickname, _sectionTitleStyle);
        GUI.Label(new Rect(500f, y, 70f, 24f), $"Lv {level}", _badgeStyle);

        y += 36f;

        y = DrawUnitCards(player.board, y);

        y += 12f;

        GUI.Label(new Rect(0f, y, 120f, 20f), "활성 시너지", _mutedStyle);
        y += 24f;
        y = DrawTextBadges(player.activeSynergies, "시너지 없음", y);

        y += 12f;

        GUI.Label(new Rect(0f, y, 120f, 20f), "증강", _mutedStyle);
        y += 24f;
        y = DrawTextBadges(player.augments, "증강 없음", y);

        return y + 8f;
    }

    /// <summary>
    /// 전적에 저장된 보드 유닛들을 카드 형태로 배치한다.
    /// 한 줄에 최대 6개를 표시하며, 반환값은 다음 요소를 배치할 Y 좌표다.
    /// </summary>
    private float DrawUnitCards(UnitRecord[] units, float y)
    {
        if (units == null || units.Length == 0)
        {
            GUI.Box(new Rect(0f, y, 520f, 42f), "");
            GUI.Label(new Rect(12f, y + 11f, 420f, 20f), "보드 유닛 없음", _mutedStyle);
            return y + 52f;
        }

        const float cardWidth = 86f;
        const float cardHeight = 68f;
        const float gap = 8f;
        const int cardsPerRow = 6;

        for (int i = 0; i < units.Length; i++)
        {
            int row = i / cardsPerRow;
            int col = i % cardsPerRow;

            float x = col * (cardWidth + gap);
            float cardY = y + row * (cardHeight + gap);

            DrawUnitCard(units[i], new Rect(x, cardY, cardWidth, cardHeight));
        }

        int rowCount = Mathf.CeilToInt(units.Length / (float)cardsPerRow);
        return y + rowCount * (cardHeight + gap);
    }

    /// <summary>
    /// 전적 유닛 한 마리의 이름, 성급과 장착 아이템 슬롯을 표시한다.
    /// </summary>
    private void DrawUnitCard(UnitRecord unit, Rect rect)
    {
        if (unit == null)
            return;

        GUI.Box(rect, "", _unitCardStyle);

        string unitName = string.IsNullOrWhiteSpace(unit.nameEn) ? "Unknown" : unit.nameEn;
        GUI.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, 20f), unitName, _smallStyle);

        string stars = FormatStars(unit.star);
        GUI.Label(new Rect(rect.x + 6f, rect.y + 27f, rect.width - 12f, 18f), stars, _winStyle);

        DrawItemSlots(unit, new Rect(rect.x + 6f, rect.y + 48f, rect.width - 12f, 14f));
    }

    /// <summary>
    /// 유닛의 일반 아이템 슬롯 3개와 진화의 돌 정보를 표시한다.
    /// 현재 OnGUI MVP에서는 아이템 이름을 간단한 텍스트로 함께 보여준다.
    /// </summary>
    private void DrawItemSlots(UnitRecord unit, Rect rect)
    {
        const int slotCount = 3;
        const float slotSize = 11f;
        const float gap = 4f;

        for (int i = 0; i < slotCount; i++)
        {
            Rect slotRect = new Rect(rect.x + i * (slotSize + gap), rect.y, slotSize, slotSize);

            string itemName = GetItemNameAt(unit, i);
            bool hasItem = !string.IsNullOrWhiteSpace(itemName);

            GUI.Box(slotRect, hasItem ? "●" : "");

            if (hasItem)
            {
                Rect tooltipRect = new Rect(slotRect.x - 2f, slotRect.y - 18f, 120f, 18f);
                GUI.Label(tooltipRect, itemName, _mutedStyle);
            }
        }

        if (!string.IsNullOrWhiteSpace(unit.stoneEn))
        {
            GUI.Label(new Rect(rect.x + 56f, rect.y - 2f, 90f, 18f), $"돌:{unit.stoneEn}", _mutedStyle);
        }
    }

    /// <summary>
    /// 시너지나 증강 문자열 배열을 여러 개의 배지 형태로 줄바꿈해 표시한다.
    /// 값이 없으면 전달받은 빈 상태 문구를 대신 표시한다.
    /// 반환값은 다음 요소를 배치할 Y 좌표다.
    /// </summary>
    private float DrawTextBadges(string[] values, string emptyText, float y)
    {
        if (values == null || values.Length == 0)
        {
            GUI.Label(new Rect(0f, y, 300f, 20f), emptyText, _mutedStyle);
            return y + 24f;
        }

        float x = 0f;
        float maxWidth = 540f;
        float lineHeight = 28f;

        for (int i = 0; i < values.Length; i++)
        {
            string text = string.IsNullOrWhiteSpace(values[i]) ? "-" : values[i];
            float badgeWidth = Mathf.Clamp(text.Length * 12f + 28f, 64f, 160f);

            if (x + badgeWidth > maxWidth)
            {
                x = 0f;
                y += lineHeight;
            }

            GUI.Label(new Rect(x, y, badgeWidth, 22f), text, _badgeStyle);
            x += badgeWidth + 8f;
        }

        return y + lineHeight;
    }

    // ──────────────────────────────────────────
    // 전적 데이터 갱신 / 실시간 반영
    // ──────────────────────────────────────────

    /// <summary>
    /// 로컬 전적 저장소에서 최근 전적을 다시 불러온다.
    /// 기존 목록을 교체하고 현재 선택 인덱스가 유효한 범위에 있도록 보정한다.
    /// </summary>
    private void RefreshMatchHistory()
    {
        _recentMatches.Clear();

        List<MatchRecord> records = MatchHistoryStore.LoadRecent(_recentMatchCount);
        if (records == null)
            return;

        _recentMatches.AddRange(records);

        if (_recentMatches.Count == 0)
            _selectedMatchIndex = 0;
        else
            _selectedMatchIndex = Mathf.Clamp(_selectedMatchIndex, 0, _recentMatches.Count - 1);
    }

    /// <summary>
    /// 새 전적 기록 이벤트를 받아 목록 가장 앞에 즉시 추가한다.
    /// 설정된 최대 표시 개수를 초과한 오래된 전적은 목록 끝에서 제거한다.
    /// </summary>
    private void HandleMatchRecorded(MatchRecord record)
    {
        if (record == null)
            return;

        _recentMatches.Insert(0, record);

        while (_recentMatches.Count > _recentMatchCount)
            _recentMatches.RemoveAt(_recentMatches.Count - 1);

        _selectedMatchIndex = 0;
    }

    // ──────────────────────────────────────────
    // OnGUI 스타일 생성
    // ──────────────────────────────────────────

    /// <summary>
    /// 전적창에서 사용하는 OnGUI 스타일을 최초 1회 생성해 캐시한다.
    /// OnGUI가 반복 호출될 때마다 GUIStyle 객체를 다시 만들지 않도록 한다.
    /// </summary>
    private void EnsureStyles()
    {
        if (_titleStyle != null)
            return;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        _sectionTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = Color.white }
        };

        _smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = Color.white }
        };

        _mutedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.75f, 0.78f, 0.88f) }
        };

        _winStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.72f, 0.18f) }
        };

        _loseStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.78f, 0.82f, 0.92f) }
        };

        _badgeStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        _unitCardStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = Color.white }
        };

        _selectedListCardStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { textColor = Color.white }
        };

        _normalListCardStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { textColor = Color.white }
        };
    }

    // ──────────────────────────────────────────
    // 전적 표시용 계산 / 포맷 유틸리티
    // ──────────────────────────────────────────

    private int CountWins()
    {
        int count = 0;

        for (int i = 0; i < _recentMatches.Count; i++)
        {
            if (IsVictory(_recentMatches[i]))
                count++;
        }

        return count;
    }

    private static bool IsVictory(MatchRecord record)
    {
        return record != null &&
               !string.IsNullOrWhiteSpace(record.result) &&
               record.result.Equals("Victory", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 파트너 데이터에 닉네임, 보드, 시너지 또는 증강 중 하나라도 있으면
    /// 팀플레이 전적으로 판단한다.
    /// </summary>
    private static bool HasPartner(MatchRecord record)
    {
        if (record == null || record.partner == null)
            return false;

        bool hasNickname = !string.IsNullOrWhiteSpace(record.partner.nickname);
        bool hasBoard = record.partner.board != null && record.partner.board.Length > 0;
        bool hasSynergy = record.partner.activeSynergies != null && record.partner.activeSynergies.Length > 0;
        bool hasAugments = record.partner.augments != null && record.partner.augments.Length > 0;

        return hasNickname || hasBoard || hasSynergy || hasAugments;
    }

    private static string GetPlayModeText(MatchRecord record)
    {
        return HasPartner(record) ? "팀플" : "솔로";
    }

    private static string GetItemNameAt(UnitRecord unit, int index)
    {
        if (unit == null || unit.itemsEn == null)
            return "";

        if (index < 0 || index >= unit.itemsEn.Length)
            return "";

        return unit.itemsEn[index];
    }

    private static string FormatStars(int star)
    {
        if (star <= 0)
            return "-";

        if (star > 5)
            star = 5;

        return new string('★', star);
    }

    private static string FormatDurationShort(int seconds)
    {
        if (seconds <= 0)
            return "-";

        int minutes = seconds / 60;

        if (minutes <= 0)
            return $"{seconds}초";

        return $"{minutes}분";
    }

    private static string FormatRelativeTime(string utcText)
    {
        if (string.IsNullOrWhiteSpace(utcText))
            return "-";

        if (!DateTime.TryParse(utcText, out DateTime utcTime))
            return "-";

        TimeSpan diff = DateTime.UtcNow - utcTime.ToUniversalTime();

        if (diff.TotalMinutes < 1)
            return "방금 전";

        if (diff.TotalHours < 1)
            return $"{Mathf.FloorToInt((float)diff.TotalMinutes)}분 전";

        if (diff.TotalDays < 1)
            return $"{Mathf.FloorToInt((float)diff.TotalHours)}시간 전";

        if (diff.TotalDays < 7)
            return $"{Mathf.FloorToInt((float)diff.TotalDays)}일 전";

        return utcTime.ToLocalTime().ToString("MM-dd");
    }

    private static string FormatDate(string utcText)
    {
        if (string.IsNullOrWhiteSpace(utcText))
            return "-";

        if (!DateTime.TryParse(utcText, out DateTime utcTime))
            return utcText;

        return utcTime.ToLocalTime().ToString("yyyy-MM-dd");
    }

    private static string SafeText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    /// <summary>
    /// 본인과 파트너의 보드 유닛 수를 기준으로 상세 스크롤 콘텐츠 높이를 계산한다.
    /// </summary>
    private static float GetDetailContentHeight(MatchRecord record)
    {
        float height = 360f;

        if (record != null && record.self != null && record.self.board != null)
        {
            int rows = Mathf.CeilToInt(record.self.board.Length / 6f);
            height += rows * 80f;
        }

        if (HasPartner(record) && record.partner != null && record.partner.board != null)
        {
            int rows = Mathf.CeilToInt(record.partner.board.Length / 6f);
            height += rows * 80f;
        }

        return Mathf.Max(520f, height);
    }

    /// <summary>
    /// 저장된 종료 사유 문자열에 연결 종료 관련 키워드가 포함됐는지 확인한다.
    /// 과거 기록의 서로 다른 종료 사유 표현을 호환하기 위해 여러 키워드를 검사한다.
    /// </summary>
    private static bool IsDisconnectedMatch(MatchRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.endReason))
            return false;

        string reason = record.endReason.ToLower();

        return reason.Contains("disconnect") ||
               reason.Contains("connection") ||
               reason.Contains("network") ||
               reason.Contains("leave") ||
               reason.Contains("left") ||
               reason.Contains("quit") ||
               reason.Contains("timeout") ||
               reason.Contains("lost") ||
               reason.Contains("연결") ||
               reason.Contains("끊김");
    }
}