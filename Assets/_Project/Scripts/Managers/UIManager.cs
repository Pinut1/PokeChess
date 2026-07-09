using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 업데이트 담당. 김태욱 파트.
///
/// 현재 구현 범위:
/// - 롤체식 전적창 MVP
/// - 왼쪽: 최근 전적 리스트
/// - 오른쪽: 선택한 전적 상세
/// - MatchHistoryStore.LoadRecent()를 이용한 과거 전적 조회
/// - GameEvents.OnMatchRecorded 구독을 통한 새 전적 실시간 반영
///
/// 주의:
/// - 새 스크립트 추가 없이 기존 UIManager 스텁에 구현.
/// - 현재는 OnGUI 기반 MVP.
/// - 추후 정식 Canvas UI가 만들어지면 표시 부분만 TMP/Image/Button 구조로 교체하면 됨.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Match History")]
    [SerializeField] private int _recentMatchCount = 10;

    private readonly List<MatchRecord> _recentMatches = new List<MatchRecord>();

    private bool _showMatchHistory;
    private int _selectedMatchIndex;
    private Vector2 _listScroll;
    private Vector2 _detailScroll;

    private Rect _matchHistoryWindowRect = new Rect(260f, 80f, 980f, 650f);

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

    private void OnEnable()
    {
        GameEvents.OnMatchRecorded += HandleMatchRecorded;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchRecorded -= HandleMatchRecorded;
    }

    private void Start()
    {
        RefreshMatchHistory();
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawOpenButton();

        if (_showMatchHistory)
            DrawMatchHistoryWindow();
    }

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

        DrawMatchList(new Rect(24f, 120f, 300f, height - 145f));
        DrawMatchDetail(new Rect(340f, 120f, width - 365f, height - 145f), _recentMatches[_selectedMatchIndex]);

        GUI.DragWindow(new Rect(0f, 0f, width, 28f));
    }

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

    private void HandleMatchRecorded(MatchRecord record)
    {
        if (record == null)
            return;

        _recentMatches.Insert(0, record);

        while (_recentMatches.Count > _recentMatchCount)
            _recentMatches.RemoveAt(_recentMatches.Count - 1);

        _selectedMatchIndex = 0;
    }

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