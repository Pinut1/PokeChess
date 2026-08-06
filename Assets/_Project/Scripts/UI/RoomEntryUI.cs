using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 방 목록의 한 줄. <see cref="RoomListUI"/>가 템플릿을 복제해 만들고 <see cref="Bind"/>로 채운다.
///
/// 한 줄은 네 칸이다 — <b>방 이름 · 방장 닉네임 · 인원/정원 · 상태</b>.
/// 방 이름이 TestRoom_1234처럼 기계적인 값이라 방장 닉네임이 실제로 방을 알아보는 단서가 된다.
///
/// 줄 자체가 <see cref="Toggle"/>이다 — 옵션창 목차(Nav_Panel)의 탭과 같은 방식으로,
/// 상호 배타(한 번에 한 방만 선택)는 코드가 아니라 Content에 붙은 ToggleGroup이 보장한다.
/// 이 클래스는 표시만 하고 선택 상태는 해석하지 않는다.
/// </summary>
public class RoomEntryUI : MonoBehaviour
{
    [Tooltip("이 줄의 Toggle. 보통 같은 오브젝트에 붙어 있다.")]
    [SerializeField] private Toggle _toggle;

    [Tooltip("방 이름(TestRoom_1234).")]
    [SerializeField] private TMP_Text _nameText;

    [Tooltip("방장 닉네임. 방을 만든 사람이 방에 있는 사람이므로 \"누구 방인지\"가 여기서 드러난다.")]
    [SerializeField] private TMP_Text _hostText;

    [Tooltip("현재 인원/정원(\"1/2\").")]
    [SerializeField] private TMP_Text _countText;

    [Tooltip("\"진행 중\"·\"가득 참\" 같은 부가 상태. 없으면 꺼진다.")]
    [SerializeField] private TMP_Text _stateText;

    /// <summary>이 줄이 가리키는 방 이름(Photon RoomInfo.Name). 입장 요청에 그대로 쓴다.</summary>
    public string RoomName { get; private set; }

    /// <summary>지금 입장 가능한 방인지. 불가면 Toggle이 잠겨 선택 자체가 되지 않는다.</summary>
    public bool Joinable { get; private set; }

    public Toggle Toggle => _toggle;

    private void Reset() => _toggle = GetComponent<Toggle>();

    private void Awake()
    {
        if (_toggle == null) _toggle = GetComponent<Toggle>();
    }

    /// <summary>
    /// 한 줄을 채운다. 입장할 수 없는 방(진행 중·가득 참·닫힘)은 Toggle을 잠가
    /// 선택 → [선택 방 입장]으로 이어지는 경로를 애초에 막는다.
    /// </summary>
    public void Bind(string roomName, string host, string count, string state, bool joinable)
    {
        RoomName = roomName;
        Joinable = joinable;

        if (_nameText != null) _nameText.text = roomName;
        if (_hostText != null) _hostText.text = host;
        if (_countText != null) _countText.text = count;

        if (_stateText != null)
        {
            bool hasState = !string.IsNullOrEmpty(state);
            _stateText.gameObject.SetActive(hasState);
            if (hasState) _stateText.text = state;
        }

        if (_toggle == null) return;

        _toggle.interactable = joinable;

        // 잠긴 줄이 선택된 채로 남지 않게 한다 — 목록을 다시 그릴 때 방이 가득 찼을 수 있다.
        if (!joinable) _toggle.SetIsOnWithoutNotify(false);
    }
}
