using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 중 열 수 있는 옵션창(IMGUI).
/// 마스터/배경음/효과음 볼륨, 항복 안내, 타이틀 이동, 게임 종료 기능을 제공한다.
/// </summary>
public class OptionsPanelUI : MonoBehaviour
{
    private enum ConfirmMode
    {
        None,
        ReturnToTitle,
        QuitGame
    }

    [Header("씬 전환")]
    [Tooltip("타이틀로 버튼 확인 시 로드할 씬 이름입니다. " +
             "비어 있거나 Build Settings에 등록되지 않은 경우 이동하지 않습니다.")]
    [SerializeField] private string _titleSceneName = "";

    private const string PREF_MASTER_VOLUME = "MasterVolume";
    private const string PREF_BGM_VOLUME = "BgmVolume";
    private const string PREF_SFX_VOLUME = "SfxVolume";

    private const float DEFAULT_SUB_VOLUME = 1f;

    private const float MIN_WIDTH = 380f;
    private const float MIN_HEIGHT = 300f;
    private const float RESIZE_HANDLE_SIZE = 16f;

    private bool _optionsOpen;
    private bool _optionsRectInitialized;

    private Rect _optionsRect = new Rect(0f, 0f, 420f, 360f);

    private float _masterVolume;
    private float _bgmVolume;
    private float _sfxVolume;

    private ConfirmMode _confirmMode = ConfirmMode.None;
    private bool _surrenderNoticeShown;

    private void Awake()
    {
        _masterVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, AudioListener.volume));

        _bgmVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(PREF_BGM_VOLUME, DEFAULT_SUB_VOLUME));

        _sfxVolume = Mathf.Clamp01(
            PlayerPrefs.GetFloat(PREF_SFX_VOLUME, DEFAULT_SUB_VOLUME));

        AudioListener.volume = _masterVolume;
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }

    private void OnGUI()
    {
        HandleEscapeKey();
        DrawOpenButton();

        if (!_optionsOpen)
            return;

        if (!_optionsRectInitialized)
        {
            _optionsRect.x = (Screen.width - _optionsRect.width) * 0.5f;
            _optionsRect.y = (Screen.height - _optionsRect.height) * 0.5f;
            _optionsRectInitialized = true;
        }

        ClampRectSize(ref _optionsRect, MIN_WIDTH, MIN_HEIGHT);

        _optionsRect = GUILayout.Window(
            GetInstanceID(),
            _optionsRect,
            DrawOptionsWindow,
            "옵션");

        ClampRectToScreen(ref _optionsRect);
    }

    private void DrawOpenButton()
    {
        const float width = 70f;
        const float height = 28f;

        Rect buttonRect = new Rect(
            8f,
            Screen.height - height - 8f,
            width,
            height);

        if (GUI.Button(buttonRect, "설정"))
            ToggleOptions(!_optionsOpen);
    }

    private void HandleEscapeKey()
    {
        Event currentEvent = Event.current;

        if (currentEvent.type != EventType.KeyDown ||
            currentEvent.keyCode != KeyCode.Escape)
        {
            return;
        }

        if (_confirmMode != ConfirmMode.None)
        {
            _confirmMode = ConfirmMode.None;
        }
        else
        {
            ToggleOptions(!_optionsOpen);
        }

        currentEvent.Use();
    }

    private void ToggleOptions(bool open)
    {
        _optionsOpen = open;

        if (!_optionsOpen)
        {
            _confirmMode = ConfirmMode.None;
            _surrenderNoticeShown = false;
            PlayerPrefs.Save();
        }
    }

    private static void ClampRectSize(
        ref Rect rect,
        float minimumWidth,
        float minimumHeight)
    {
        float maximumWidth = Mathf.Max(
            minimumWidth,
            Screen.width - 10f);

        float maximumHeight = Mathf.Max(
            minimumHeight,
            Screen.height - 10f);

        rect.width = Mathf.Clamp(
            rect.width,
            minimumWidth,
            maximumWidth);

        rect.height = Mathf.Clamp(
            rect.height,
            minimumHeight,
            maximumHeight);
    }

    private static void ClampRectToScreen(ref Rect rect)
    {
        const float visibleMargin = 60f;

        rect.x = Mathf.Clamp(
            rect.x,
            -(rect.width - visibleMargin),
            Screen.width - visibleMargin);

        rect.y = Mathf.Clamp(
            rect.y,
            0f,
            Screen.height - visibleMargin);
    }

    private static void DrawResizeHandle(
        ref Rect rect,
        float minimumWidth,
        float minimumHeight)
    {
        Rect handleRect = new Rect(
            rect.width - RESIZE_HANDLE_SIZE,
            rect.height - RESIZE_HANDLE_SIZE,
            RESIZE_HANDLE_SIZE,
            RESIZE_HANDLE_SIZE);

        GUI.Box(handleRect, "◢");

        Event currentEvent = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        switch (currentEvent.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (handleRect.Contains(currentEvent.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    currentEvent.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId)
                {
                    float maximumWidth = Mathf.Max(
                        minimumWidth,
                        Screen.width - 10f);

                    float maximumHeight = Mathf.Max(
                        minimumHeight,
                        Screen.height - 10f);

                    rect.width = Mathf.Clamp(
                        rect.width + currentEvent.delta.x,
                        minimumWidth,
                        maximumWidth);

                    rect.height = Mathf.Clamp(
                        rect.height + currentEvent.delta.y,
                        minimumHeight,
                        maximumHeight);

                    currentEvent.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    currentEvent.Use();
                }
                break;
        }
    }

    private void DrawOptionsWindow(int windowId)
    {
        GUILayout.BeginVertical();

        DrawHeader();

        GUILayout.Space(6f);
        DrawVolumeSection();

        GUILayout.Space(12f);

        if (_confirmMode == ConfirmMode.None)
        {
            DrawNormalButtons();
        }
        else
        {
            DrawConfirmSection();
        }

        GUILayout.EndVertical();

        DrawResizeHandle(
            ref _optionsRect,
            MIN_WIDTH,
            MIN_HEIGHT);

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    private void DrawHeader()
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label("옵션", GUILayout.Width(200f));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("닫기", GUILayout.Width(60f)))
            ToggleOptions(false);

        GUILayout.EndHorizontal();
    }

    private void DrawVolumeSection()
    {
        GUILayout.Label("── 볼륨 ──");

        DrawVolumeRow(
            "마스터 볼륨",
            ref _masterVolume,
            PREF_MASTER_VOLUME,
            value => AudioListener.volume = value);

        DrawVolumeRow(
            "배경음",
            ref _bgmVolume,
            PREF_BGM_VOLUME,
            null);

        DrawVolumeRow(
            "효과음",
            ref _sfxVolume,
            PREF_SFX_VOLUME,
            null);

        GUILayout.Label(
            "배경음·효과음은 현재 설정값만 저장됩니다.");
    }

    private static void DrawVolumeRow(
        string label,
        ref float value,
        string preferenceKey,
        System.Action<float> applyImmediately)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(label, GUILayout.Width(100f));

        float newValue = GUILayout.HorizontalSlider(
            value,
            0f,
            1f,
            GUILayout.Width(160f));

        GUILayout.Label(
            $"{Mathf.RoundToInt(newValue * 100f)}%",
            GUILayout.Width(50f));

        GUILayout.EndHorizontal();

        if (Mathf.Approximately(newValue, value))
            return;

        value = newValue;

        PlayerPrefs.SetFloat(preferenceKey, value);
        applyImmediately?.Invoke(value);
    }

    private void DrawNormalButtons()
    {
        DrawSurrenderSection();

        GUILayout.Space(10f);

        if (GUILayout.Button("타이틀로"))
        {
            _surrenderNoticeShown = false;
            _confirmMode = ConfirmMode.ReturnToTitle;
        }

        GUILayout.Space(10f);

        if (GUILayout.Button("게임 종료"))
        {
            _surrenderNoticeShown = false;
            _confirmMode = ConfirmMode.QuitGame;
        }
    }

    private void DrawSurrenderSection()
    {
        if (GUILayout.Button("항복"))
        {
            Debug.Log(
                "[OptionsPanelUI] 항복 기능은 추후 구현 예정입니다.");

            _surrenderNoticeShown = true;
        }

        if (_surrenderNoticeShown)
        {
            GUILayout.Label(
                "항복 기능은 추후 구현 예정입니다.");
        }
    }

    private void DrawConfirmSection()
    {
        string message = _confirmMode switch
        {
            ConfirmMode.ReturnToTitle =>
                "타이틀 화면으로 이동하시겠습니까?",

            ConfirmMode.QuitGame =>
                "게임을 종료하시겠습니까?",

            _ => ""
        };

        GUILayout.Label(message);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("확인", GUILayout.Width(80f)))
        {
            ConfirmMode selectedMode = _confirmMode;
            _confirmMode = ConfirmMode.None;

            if (selectedMode == ConfirmMode.ReturnToTitle)
                ConfirmReturnToTitle();
            else if (selectedMode == ConfirmMode.QuitGame)
                QuitGame();
        }

        if (GUILayout.Button("취소", GUILayout.Width(80f)))
            _confirmMode = ConfirmMode.None;

        GUILayout.EndHorizontal();
    }

    private void ConfirmReturnToTitle()
    {
        if (string.IsNullOrWhiteSpace(_titleSceneName))
        {
            Debug.LogWarning(
                "[OptionsPanelUI] 타이틀 씬 이름이 설정되지 않았습니다.");

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(_titleSceneName))
        {
            Debug.LogWarning(
                $"[OptionsPanelUI] 빌드에 등록되지 않은 씬입니다: {_titleSceneName}");

            return;
        }

        PlayerPrefs.Save();
        Time.timeScale = 1f;

        if (GameManager.TryGet(out var gameManager) &&
            gameManager.Network != null &&
            gameManager.Network.IsInRoom)
        {
            gameManager.Network.LeaveRoom();
        }

        SceneManager.LoadScene(_titleSceneName);
    }

    private static void QuitGame()
    {
        PlayerPrefs.Save();
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}