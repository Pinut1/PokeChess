using TMPro;
using UnityEngine;

/// <summary>
/// UserInfo_Panel(내 프로필 / 파트너 프로필 두 줄)의 표시 바인딩.
/// 롤체 좌상단 플레이어 목록과 같은 모양 — [프로필 아이콘 + 닉네임] 한 줄이 곧 "그 사람 맵 보기" 버튼이다.
///
/// <b>줄 순서는 고정이다</b> — 첫 줄은 항상 내 프로필(MyProfile_Button), 둘째 줄은 항상 파트너
/// (PartnerProfile_Button). 2인 협동이라 순위가 바뀔 일이 없어 재정렬 로직을 아예 두지 않는다.
/// 롤체처럼 순위대로 줄이 움직이는 동작이 나중에 필요해지면 그때 별도로 설계할 것.
///
/// 이 컴포넌트가 하는 일은 <b>표시뿐</b>이다.
///   - 닉네임 두 줄
///   - 팀 공통 HP 텍스트(GDD: 2인 공동 HP — 사람마다 다른 값이 아니라 하나뿐이다)
///
/// 화면 전환(버튼 클릭 → 내 맵/파트너 맵)과 "지금 어느 쪽을 보고 있는지" 표시는 전부
/// <see cref="PartnerSpectateView"/>가 담당한다 — 프로필 색으로 누구인지(방장=_hostColor 보라,
/// 참가자=_guestColor 파랑, 방 밖=_offlineColor 회색)를, 밝기로 지금 어느 쪽을 보고 있는지
/// (안 보는 쪽만 _inactiveDim만큼 어둡게)를 나타내는 방식이라 여기서 따로 표시할 것이 없다.
/// 버튼 onClick도 PartnerSpectateView 쪽 인스펙터 칸에 그대로 물려 둘 것.
///
/// ⚠️ 닉네임은 변경 이벤트가 없다(GameEvents에 파트너 입장/닉네임 변경 이벤트 자체가 없음).
/// 파트너는 나보다 늦게 들어오거나 중간에 나갈 수 있어서 저주기 폴링으로 따라간다
/// (<see cref="_refreshInterval"/>, 기본 0.5초). 값이 실제로 바뀐 경우에만 TMP에 대입한다 —
/// 같은 문자열이라도 대입하면 TMP가 메시를 다시 만들기 때문이다.
/// </summary>
public class UserInfoPanelUI : MonoBehaviour
{
    [Header("닉네임")]
    [Tooltip("첫째 줄(MyProfile_Button) 닉네임 텍스트. 내 닉네임이 들어간다.")]
    [SerializeField] private TMP_Text _myNicknameText;

    [Tooltip("둘째 줄(PartnerProfile_Button) 닉네임 텍스트. 파트너 닉네임이 들어간다.")]
    [SerializeField] private TMP_Text _partnerNicknameText;

    [Tooltip("파트너가 아직 안 들어왔거나 나갔을 때 둘째 줄에 표시할 문구.")]
    [SerializeField] private string _partnerAbsentLabel = "파트너 대기 중";

    [Header("팀 공통 HP")]
    [Tooltip("공통 HP를 표시할 텍스트들. 값이 하나뿐이라 여러 칸에 꽂아도 전부 같은 값이 들어간다 — " +
             "패널 옆에 하나만 둘 거면 한 칸만 채우고, 롤체처럼 두 줄 모두에 숫자를 띄우고 싶으면 두 칸을 채운다.")]
    [SerializeField] private TMP_Text[] _teamHpTexts;

    [Tooltip("{0}=현재 공통 HP. 예: \"{0}\", \"HP {0}\"")]
    [SerializeField] private string _teamHpFormat = "{0}";

    [Tooltip("아직 HP가 초기화되지 않았을 때(방 입장 직후 등) 표시할 문구.")]
    [SerializeField] private string _teamHpUnknownLabel = "-";

    [Header("갱신")]
    [Tooltip("닉네임·HP를 다시 확인하는 간격(초). 이벤트가 없는 닉네임을 따라잡기 위한 폴링이라 " +
             "짧게 둘 이유가 없다.")]
    [SerializeField] private float _refreshInterval = 0.5f;

    // 마지막으로 실제 화면에 대입한 값. 같은 값을 다시 넣어 TMP 메시를 재생성하지 않기 위한 캐시다.
    private string _shownMyNickname;
    private string _shownPartnerNickname;
    private int _shownTeamHp = int.MinValue;

    private float _refreshTimer;

    private void OnEnable()
    {
        GameEvents.OnHealthChanged += HandleHealthChanged;

        RefreshFromNetwork();
        _refreshTimer = 0f;
    }

    private void OnDisable()
    {
        GameEvents.OnHealthChanged -= HandleHealthChanged;
    }

    // 매니저 초기화가 OnEnable보다 늦을 수 있어(PlayerHealthManager.Start에서 HP 최초 통지) 한 번 더.
    private void Start() => RefreshFromNetwork();

    private void Update()
    {
        _refreshTimer += Time.unscaledDeltaTime;
        if (_refreshTimer < _refreshInterval) return;

        _refreshTimer = 0f;
        RefreshFromNetwork();
    }

    /// <summary>
    /// 팀 공통 HP 변경(GameEvents.OnHealthChanged). 이 이벤트가 정규 경로이고,
    /// <see cref="RefreshFromNetwork"/>의 HP 확인은 구독 이전에 이미 값이 정해진 경우를 위한 보완이다.
    /// </summary>
    private void HandleHealthChanged(int health) => ApplyTeamHp(health);

    /// <summary>
    /// 네트워크에서 닉네임과 HP를 읽어 표시를 맞춘다. 값이 그대로면 아무 것도 대입하지 않는다.
    /// GameManager는 프로젝트 규칙대로 TryGet으로만 조회한다(Singleton.Instance 널 검사 금지).
    /// </summary>
    private void RefreshFromNetwork()
    {
        if (!GameManager.TryGet(out var gm) || gm.Network == null) return;

        ApplyNickname(_myNicknameText, gm.Network.LocalNickname, ref _shownMyNickname, string.Empty);

        // 파트너가 없으면 PartnerNickname은 빈 문자열이다(방에 나 혼자거나 상대가 나간 상태).
        ApplyNickname(_partnerNicknameText, gm.Network.PartnerNickname, ref _shownPartnerNickname, _partnerAbsentLabel);

        ApplyTeamHp(gm.Network.TeamHealth);
    }

    private static void ApplyNickname(TMP_Text target, string nickname, ref string cache, string fallback)
    {
        if (target == null) return;

        string display = string.IsNullOrWhiteSpace(nickname) ? fallback : nickname;
        if (display == cache) return;

        cache = display;
        target.text = display;
    }

    /// <summary>공통 HP를 모든 표시 칸에 반영한다. -1(미초기화)은 숫자 대신 안내 문구로 보여준다.</summary>
    private void ApplyTeamHp(int health)
    {
        if (health == _shownTeamHp) return;
        _shownTeamHp = health;

        if (_teamHpTexts == null) return;

        string display = health < 0 ? _teamHpUnknownLabel : string.Format(_teamHpFormat, health);

        foreach (var text in _teamHpTexts)
            if (text != null) text.text = display;
    }
}
