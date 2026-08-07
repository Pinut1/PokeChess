#if UNITY_EDITOR
using System.Reflection;
using UnityEngine;

/// <summary>
/// 🗑️ <b>모달 uGUI 이관 검증용 임시 도구. 확인 끝나면 이 파일만 지우면 된다.</b>
///
/// 씬에 아무것도 붙이지 않는다 — RuntimeInitializeOnLoadMethod가 Play 시작 시 스스로 오브젝트를
/// 만든다. 그래서 제거할 때 씬을 열어 컴포넌트를 떼거나 커밋에서 골라낼 필요가 없다.
/// <c>#if UNITY_EDITOR</c>로 통째로 감싸서 빌드에는 애초에 들어가지 않는다.
///
/// <b>왜 리플렉션인가</b> — 항복/재접속 포기 상태는 NetworkManager의 private 필드라 밖에서 세울
/// 수 없다. 디버그 setter를 추가하면 NetworkManager(영욱님 파트)를 건드리게 되고 나중에 지울 것도
/// 늘어난다. 임시 도구이므로 이쪽에서 리플렉션으로 밀어넣고, 필드를 못 찾으면 버튼을 비활성화한다.
///
/// <b>확인할 것</b>
/// <list type="number">
/// <item>버튼을 누르면 해당 모달이 뜨는가</item>
/// <item><b>모달이 뜬 상태에서 뒤쪽 보드의 유닛이 드래그되지 않는가</b> — Dim의 Raycast Target이
///       BlockAllInput()을 대신하는 근거라, 여기가 이번 이관의 핵심 검증 항목이다</item>
/// <item>ESC를 눌러도 닫히지 않는가(강제 선택 모달)</item>
/// <item>[포기하기] → 종료 확인으로 넘어갈 때 화면이 깜빡이지 않는가</item>
/// </list>
/// </summary>
public class ModalQaTrigger : MonoBehaviour
{
    private const KeyCode TOGGLE_KEY = KeyCode.F9;

    private static readonly BindingFlags PRIVATE_INSTANCE =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private bool _visible = true;
    private Rect _windowRect = new Rect(12f, 90f, 250f, 330f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[QA] ModalTrigger") { hideFlags = HideFlags.DontSave };
        go.AddComponent<ModalQaTrigger>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        if (Input.GetKeyDown(TOGGLE_KEY)) _visible = !_visible;
    }

    private void OnGUI()
    {
        if (!_visible) return;

        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "[QA] 모달 트리거");
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.Label($"F9로 숨기기 / 다시 보기");

        NetworkManager network = ResolveNetwork();

        // ── 파트너 이탈 3종 중 둘은 GameEvents만으로 재현된다(NetworkManager 무관).
        GUILayout.Space(6f);
        GUILayout.Label("― 파트너 이탈 (GameEvents) ―");

        if (GUILayout.Button("① 연결 끊김 → 대기 모달"))
            GameEvents.OpponentDisconnected(30f);

        if (GUILayout.Button("② 유예 만료 → [포기하기] 등장"))
            GameEvents.GracePeriodExpired(false);

        if (GUILayout.Button("③ 재접속 → 모달 닫힘"))
            GameEvents.OpponentReconnected();

        // ── 나머지는 NetworkManager의 private 플래그를 직접 세워야 한다.
        GUILayout.Space(6f);
        GUILayout.Label("― 항복 / 포기 (리플렉션) ―");

        if (network == null)
        {
            GUILayout.Label("NetworkManager 없음 — 아래 버튼 사용 불가");
            GUI.DragWindow();
            return;
        }

        if (GUILayout.Button("④ 파트너 항복 요청 수신"))
            SetNetworkFlag(network, "_surrenderRequestReceived");

        if (GUILayout.Button("⑤ 내 요청 거절 안내"))
            SetNetworkFlag(network, "_surrenderRejectedNotice");

        if (GUILayout.Button("⑥ 교차취소 안내"))
            SetNetworkFlag(network, "_surrenderCrossCancelledNotice");

        if (GUILayout.Button("⑦ 재접속 포기 통지"))
            SetNetworkFlag(network, "_partnerGaveUpReconnectNotice");

        GUILayout.Space(4f);
        GUILayout.Label("⑦은 [확인] 시 타이틀 씬으로 넘어간다.");

        // ④의 [항복하기]/[계속하기]는 RespondToSurrender → photonView.RPC로 이어진다.
        // 접속 없이 누르면 Photon이 콘솔에 오류를 찍지만 모달은 정상적으로 닫힌다.
        GUILayout.Label("④ 응답 시 Photon 오류 로그는 정상.");

        GUI.DragWindow();
    }

    /// <summary>솔로 모드에서도 NetworkManager 인스턴스 자체는 존재하므로 플래그를 세워 UI만 확인할 수 있다.</summary>
    private static NetworkManager ResolveNetwork()
    {
        return GameManager.TryGet(out var gameManager) ? gameManager.Network : null;
    }

    private static void SetNetworkFlag(NetworkManager network, string fieldName)
    {
        FieldInfo field = typeof(NetworkManager).GetField(fieldName, PRIVATE_INSTANCE);

        if (field == null || field.FieldType != typeof(bool))
        {
            Debug.LogWarning(
                $"[QA] NetworkManager에 bool '{fieldName}' 필드가 없습니다 — " +
                "이름이 바뀌었거나 PHOTON 심볼이 꺼져 스텁 구현이 컴파일된 상태입니다.");

            return;
        }

        field.SetValue(network, true);
        Debug.Log($"[QA] {fieldName} = true");
    }
}
#endif
