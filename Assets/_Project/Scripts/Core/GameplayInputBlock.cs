using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// "지금 게임 조작 입력을 받으면 안 되는 상황인지"를 한곳에서 판단한다.
///
/// <b>EventSystem을 우회하는 입력만 이 검사가 필요하다.</b> uGUI 버튼 클릭은 모달의 Dim
/// (Raycast Target 켜진 전체화면 Image)이 알아서 막아 준다. 하지만 새 Input System을 직접
/// 폴링하거나(<see cref="UnitDragController"/>의 Physics.Raycast, <see cref="ButtonHotkey"/>의
/// 키보드) EventSystem을 건너뛰고 실행하는 코드는 Dim에 걸리지 않아 그대로 관통한다.
///
/// ⚠️ <b>EventSystem.enabled로는 판단할 수 없다.</b> 예전에는 모달이 EventSystem을 통째로 꺼서
/// 입력을 막았지만, 그러면 모달 자신의 버튼까지 죽어서 지금은 Dim 방식으로 바뀌었다
/// (<see cref="OptionsPanelUI"/> 주석 참고). 그래서 모달이 떠 있어도 EventSystem은 계속 켜져 있다 —
/// enabled를 보는 검사는 아무것도 막지 못한다.
///
/// 이 목록이 여러 곳에 손으로 복사돼 있으면 한쪽만 고쳐지고 다른 쪽이 새는 드리프트가 생긴다
/// (2026-08 코드리뷰에서 실제로 지적된 문제 — ButtonHotkey에 관전 화면 검사가 빠져 있었다).
/// <b>새로 막아야 할 상황이 생기면 반드시 여기에만 추가할 것.</b>
/// </summary>
public static class GameplayInputBlock
{
    private static UIManager      _ui;
    private static AugmentManager _augment;
    private static NetworkManager _network;
    private static ModalDialogUI  _modal;

    // 씬에 모달이 없을 수도 있어 "못 찾음"도 기억해 둔다. 매 프레임 씬을 훑으면 비싸다.
    private static bool _modalSearched;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        // 도메인 리로드를 끈 환경에서도 중복 구독되지 않도록 먼저 떼고 붙인다.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        Clear();
    }

    // 씬이 바뀌면 이전 씬의 오브젝트를 가리키던 참조는 전부 버린다. 파괴된 참조는 == null이라
    // 그냥 두면 "검사할 대상이 없다"가 되어 조용히 통과해 버린다.
    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Clear();

    private static void Clear()
    {
        _ui = null;
        _augment = null;
        _network = null;
        _modal = null;
        _modalSearched = false;
    }

    /// <summary>
    /// 조작 입력을 무시해야 하면 true. 판단 근거를 알고 싶으면 <see cref="BlockReason"/>을 쓴다.
    /// </summary>
    public static bool IsBlocked() => BlockReason() != null;

    /// <summary>
    /// 막는 이유(로그·디버그용). 막을 상황이 아니면 null.
    /// GameManager는 프로젝트 규칙대로 TryGet으로만 조회한다(Singleton.Instance 널 검사 금지).
    /// </summary>
    public static string BlockReason()
    {
        Resolve();

        // 매치가 완전히 끝난 뒤(승리/패배 결과 화면). GamePhase.Battle만 허용하는 화이트리스트가
        // 아니라 종료 상태 둘만 거르는 blacklist라, 파트너가 아직 전투 중인 정상 진행에는 영향이 없다.
        if (GameManager.TryGet(out var gm) && gm.Phase != null &&
            (gm.Phase.CurrentPhase == GamePhase.Victory || gm.Phase.CurrentPhase == GamePhase.GameOver))
            return "MatchEnded";

        // 공용 모달(항복 확인·패배·파트너 이탈 3종 등)이 떠 있는 동안.
        if (_modal != null && _modal.IsOpen) return "ModalOpen";

        if (_augment != null && _augment.IsChoiceBlocking) return "AugmentChoice";

        if (_ui != null && _ui.IsPlusleMinunChoiceBlocking) return "FormChoice";

        // 파트너 전체화면 관전 중. 관전 화면은 Canvas RawImage일 뿐이라 3D 레이캐스트도, 키보드
        // 입력도 막지 못해 화면 뒤 내 보드/버튼이 그대로 조작되는 관통 버그가 있었다(2026-08 확인).
        if (_ui != null && _ui.IsPartnerSpectateExpanded) return "PartnerSpectate";

        if (_network != null && _network.IsAwaitingPartnerReconnect) return "AwaitingReconnect";

        return null;
    }

    private static void Resolve()
    {
        if (_ui == null || _augment == null || _network == null)
        {
            if (GameManager.TryGet(out var gm))
            {
                if (_ui == null)      _ui = gm.UI;
                if (_network == null) _network = gm.Network;

                // AugmentManager는 GameManager와 같은 오브젝트에 있다(UnitDragController와 같은 방식).
                if (_augment == null) _augment = gm.Augment != null ? gm.Augment : gm.GetComponent<AugmentManager>();
            }
        }

        if (_modal == null && !_modalSearched)
        {
            // 모달은 평소 꺼져 있으므로 비활성까지 포함해 찾는다. 씬당 한 번만 훑는다.
            _modal = Object.FindFirstObjectByType<ModalDialogUI>(FindObjectsInactive.Include);
            _modalSearched = true;
        }
    }
}
