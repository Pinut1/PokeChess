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
/// ⚠️ 이 컴포넌트는 EventSystem을 거치지 않고 키보드를 직접 본다. 모달의 Dim은 마우스 클릭만
/// 막을 뿐 키 입력은 그대로 통과시키므로, 막아야 할 상황은 <see cref="GameplayInputBlock"/>에
/// 물어본다 — UnitDragController가 3D 조작을 막을 때와 <b>같은 판단</b>을 쓴다.
/// 새로 막을 상황이 생기면 그쪽에만 추가하면 이 컴포넌트도 자동으로 따라간다.
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

    private void Awake()
    {
        if (_button == null) _button = GetComponent<Button>();

        if (_button == null)
        {
            Debug.LogWarning(
                "[ButtonHotkey] 누를 버튼이 없습니다. Button이 있는 오브젝트에 붙이거나 " +
                "Button 칸에 직접 물려 주세요.", this);
        }
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
    private static bool IsBlocked()
    {
        // 글자를 치는 중이면 D·F는 입력 문자다. 닉네임 칸 등에서 상점이 굴러가면 안 된다.
        // (이건 이 컴포넌트만의 사정이라 공용 판정에 넣지 않는다 — 3D 드래그는 무관하다.)
        EventSystem es = EventSystem.current;
        GameObject selected = es != null ? es.currentSelectedGameObject : null;
        if (selected != null && selected.GetComponent<TMP_InputField>() != null) return true;

        // 모달·증강 선택·폼 선택·파트너 관전·재접속 대기·매치 종료는 공용 판정 한 곳에서 본다.
        return GameplayInputBlock.IsBlocked();
    }
}
