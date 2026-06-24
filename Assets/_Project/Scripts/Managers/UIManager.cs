using UnityEngine;

/// <summary>
/// UI 업데이트 담당. 김태욱 파트.
/// 현재는 플레이 테스트용 HUD를 OnGUI로 표시한다.
/// </summary>
public class UIManager : MonoBehaviour
{
    private int _currentGold = -1;
    private int _partnerGold = -1;
    private int _teamHealth = -1;
    private int _currentRound = 0;

    private GUIStyle _labelStyle;
    private GUIStyle _boxStyle;

    private void OnEnable()
    {
        GameEvents.OnGoldChanged += HandleGoldChanged;
        GameEvents.OnPartnerGoldChanged += HandlePartnerGoldChanged;
        GameEvents.OnHealthChanged += HandleHealthChanged;
        GameEvents.OnRoundChanged += HandleRoundChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnGoldChanged -= HandleGoldChanged;
        GameEvents.OnPartnerGoldChanged -= HandlePartnerGoldChanged;
        GameEvents.OnHealthChanged -= HandleHealthChanged;
        GameEvents.OnRoundChanged -= HandleRoundChanged;
    }

    private void Start()
    {
        SyncFallbackValues();
    }

    private void OnGUI()
    {
        EnsureStyles();
        SyncFallbackValues();

        var gm = GameManager.Instance;

        string teamHpText = _teamHealth >= 0 && gm != null && gm.PlayerHealth != null
            ? $"{_teamHealth}/{gm.PlayerHealth.MaxHealth}"
            : "-";

        string roundText = _currentRound > 0 ? _currentRound.ToString() : "-";
        string goldText = _currentGold >= 0 ? $"{_currentGold}G" : "-";
        string partnerGoldText = _partnerGold >= 0 ? $"{_partnerGold}G" : "-";

        float boxWidth = 280f;
        float boxHeight = 150f;
        float margin = 20f;

        float x = Screen.width - boxWidth - margin;
        float y = margin;

        GUI.Box(new Rect(x, y, boxWidth, boxHeight), "", _boxStyle);

        GUI.Label(new Rect(x + 14f, y + 12f, 250f, 28f), $"팀 HP: {teamHpText}", _labelStyle);
        GUI.Label(new Rect(x + 14f, y + 42f, 250f, 28f), $"라운드: {roundText}", _labelStyle);
        GUI.Label(new Rect(x + 14f, y + 72f, 250f, 28f), $"골드: {goldText}", _labelStyle);
        GUI.Label(new Rect(x + 14f, y + 102f, 250f, 28f), $"파트너 골드: {partnerGoldText}", _labelStyle);
    }

    private void EnsureStyles()
    {
        if (_labelStyle != null && _boxStyle != null)
            return;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            normal = { textColor = Color.white }
        };

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 20,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(12, 12, 10, 10)
        };
    }

    /// <summary>
    /// 이벤트 수신 전에도 현재 매니저 상태를 읽어 HUD가 비어 보이지 않게 한다.
    /// </summary>
    private void SyncFallbackValues()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;

        if (gm.PlayerHealth != null)
            _teamHealth = gm.PlayerHealth.Health;

        if (gm.Phase != null)
            _currentRound = gm.Phase.CurrentRound;

        if (gm.Shop != null)
            _currentGold = gm.Shop.Gold;
    }

    private void HandleGoldChanged(int gold)
    {
        _currentGold = gold;
    }

    private void HandlePartnerGoldChanged(int gold)
    {
        _partnerGold = gold;
    }

    private void HandleHealthChanged(int health)
    {
        _teamHealth = health;
    }

    private void HandleRoundChanged(int round)
    {
        _currentRound = round;
    }
}