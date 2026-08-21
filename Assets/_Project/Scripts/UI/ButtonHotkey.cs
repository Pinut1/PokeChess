using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 버튼 하나에 단축키를 붙인다. XP 구매(F)·상점 새로고침(D)처럼 툴팁에 "[F]"라고 적어둔 키를
/// 실제로 먹게 하는 용도다. 버튼에 붙이고 <see cref="_key"/>만 고르면 끝 — onClick을 다시 엮을
/// 필요가 없다(이미 물려 있는 onClick을 그대로 쏜다).
///
/// 입력은 새 Input System을 쓰되, 다른 곳(UnitDragController·StatInfoController)처럼 임베드
/// InputAction을 쓰지 않고 <b>Key 드롭다운</b> 하나만 둔다 — 버튼마다 키가 달라서 인스펙터에서
/// 바로 골라야 하는데, InputAction은 바인딩 목록을 열고 "Listen"으로 잡아야 해서 손이 더 간다.
/// 리바인딩 UI가 생기면 그때 InputAction으로 바꾸면 된다.
///
/// <b>실제로 누른 것과 똑같이 동작한다</b> — onClick만 부르지 않고 <see cref="Button.OnSubmit"/>를
/// 태워서 눌린 색 변화(Pressed 트랜지션)까지 재현한다. 키로 눌렀는데 버튼이 가만히 있으면
/// 먹었는지 알 수 없기 때문이다. 살 수 없는 상태(interactable=false)면 Button 쪽에서 알아서 무시한다.
///
/// ⚠️ 이 컴포넌트는 EventSystem을 거치지 않고 키보드를 직접 본다. 그래서 모달이 조작을 막는
/// 방식(EventSystem.enabled 끄기, AugmentManager.IsChoiceBlocking 등)에 자동으로 걸리지 않아
/// <see cref="IsBlocked"/>에서 같은 조건을 직접 확인한다 — UnitDragController가 3D 조작을 막을 때
/// 쓰는 목록과 같다. 새 모달을 추가하면 여기도 같이 봐야 한다.
/// </summary>
public class ButtonHotkey : MonoBehaviour
{
    [Tooltip("이 키를 누르면 버튼이 눌린다. 툴팁 문구의 [F]·[D]와 같은 키로 맞출 것.")]
    [SerializeField] private Key _key = Key.F;

    [Tooltip("누를 버튼. 비우면 같은 오브젝트의 Button을 쓴다 — 보통은 비워두면 된다.")]
    [SerializeField] private Button _button;

    /// <summary>
    /// 툴팁 문구에 넣을 키 이름("F", "D", "1" …). 단축키가 없으면 빈 문자열.
    /// Key 열거형 이름을 그대로 쓰면 숫자키가 "Digit1"으로 나와서 그 두 갈래만 다듬는다.
    /// </summary>
    public string KeyLabel
    {
        get
        {
            if (_key == Key.None) return string.Empty;

            string name = _key.ToString();

            if (name.StartsWith("Digit"))  return name.Substring("Digit".Length);
            if (name.StartsWith("Numpad")) return "Num" + name.Substring("Numpad".Length);

            return name;
        }
    }

    private AugmentManager _augment;
    private UIManager      _ui;
    private NetworkManager _network;

    // 매니저를 한 번이라도 찾아봤는지. GameManager가 늦게 준비될 수 있어 찾을 때까지 다시 시도한다.
    private bool _resolved;

    private void Awake()
    {
        if (_button == null) _button = GetComponent<Button>();

        if (_button == null)
        {
            Debug.LogWarning(
                "[ButtonHotkey] 누를 버튼이 없습니다. Button이 있는 오브젝트에 붙이거나 " +
                "Button 칸에 직접 물려 주세요.", this);
        }

        ResolveManagers();
    }

    private void Update()
    {
        if (_button == null || _key == Key.None) return;

        // 키보드가 없는 환경(빌드 타깃/에디터 포커스 밖)에서는 Keyboard.current가 null이다.
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (!keyboard[_key].wasPressedThisFrame) return;

        if (IsBlocked()) return;

        // Button.OnSubmit이 활성/interactable을 스스로 확인하고, 눌린 표시를 낸 뒤 onClick을 부른다.
        _button.OnSubmit(new BaseEventData(EventSystem.current));
    }

    /// <summary>
    /// 지금 키 입력을 무시해야 하는 상황인지. 마우스로는 못 누르는 상태인데 키로는 눌리는
    /// 구멍을 막는 것이 목적이다.
    /// </summary>
    private bool IsBlocked()
    {
        EventSystem es = EventSystem.current;

        // 옵션창의 대기 모달·패배 모달은 EventSystem을 꺼서 UI 조작을 통째로 막는다.
        if (es == null || !es.enabled) return true;

        // 글자를 치는 중이면 D·F는 입력 문자다. 닉네임 칸 등에서 상점이 굴러가면 안 된다.
        GameObject selected = es.currentSelectedGameObject;
        if (selected != null && selected.GetComponent<TMP_InputField>() != null) return true;

        ResolveManagers();

        if (_augment != null && _augment.IsChoiceBlocking) return true;              // 증강 3택1
        if (_ui != null && _ui.IsPlusleMinunChoiceBlocking) return true;             // 플러시/마이농 폼 선택
        if (_network != null && _network.IsAwaitingPartnerReconnect) return true;    // 파트너 재접속 대기

        return false;
    }

    /// <summary>
    /// 블로킹 판정에 쓰는 매니저를 찾아둔다. GameManager는 프로젝트 규칙대로 TryGet으로만 조회한다
    /// (Singleton.Instance 널 검사 금지). AugmentManager는 GameManager와 같은 오브젝트에 있으므로
    /// UnitDragController와 같은 방식으로 가져온다.
    /// </summary>
    private void ResolveManagers()
    {
        if (_resolved) return;
        if (!GameManager.TryGet(out var gm)) return;

        _augment = gm.Augment != null ? gm.Augment : gm.GetComponent<AugmentManager>();
        _ui      = gm.UI;
        _network = gm.Network;

        _resolved = true;
    }
}
