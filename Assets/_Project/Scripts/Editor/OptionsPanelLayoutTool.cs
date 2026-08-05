using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 씬에 이미 만들어져 있는 OptionsPanel uGUI 계층에 RectTransform/TMP 폰트/텍스트/정렬 값을
/// 일괄 적용하는 에디터 전용 배치 도구.
///
/// 오브젝트를 새로 만들거나 지우거나 순서를 바꾸지 않는다 — 이름/경로로 기존 오브젝트를 찾아
/// 값만 덮어쓴다. 찾지 못한 오브젝트는 경고만 남기고 나머지 작업을 계속한다.
/// </summary>
public static class OptionsPanelLayoutTool
{
    private const string UNDO_NAME = "Apply Options Panel Layout";

    [MenuItem("PokeChess/UI/Apply Options Panel Layout")]
    private static void ApplyLayout()
    {
        GameObject optionsPanelGO = FindOptionsPanel();
        if (optionsPanelGO == null)
        {
            Debug.LogError("[OptionsPanelLayoutTool] 씬에서 'OptionsPanel' 오브젝트를 찾지 못했습니다.");
            return;
        }

        Transform root = optionsPanelGO.transform;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);

        TMP_FontAsset koreanFont = FindKoreanFontAsset();
        if (koreanFont == null)
        {
            Debug.LogWarning(
                "[OptionsPanelLayoutTool] 이름에 'NEXON'을 포함하는 TMP_FontAsset을 프로젝트에서 찾지 못해 " +
                "폰트 적용 단계를 건너뜁니다(레이아웃/텍스트 적용은 계속 진행).");
        }

        ApplyOptionsPanelRoot(optionsPanelGO);
        ApplyBackground(root);
        ApplyTitleText(root);
        ApplyCloseButton(root);
        // 볼륨 3행은 VolumeRow_Pf 인스턴스(Sound_Page/MasterVolume 등)로 바뀌었다 — 배치·문구·슬라이더
        // 범위를 전부 프리팹과 VolumeRowUI가 들고 있으므로 이 도구가 좌표를 덮어쓰지 않는다.
        ApplyWindowedToggle(root);
        ApplyFullscreenToggle(root);
        ApplyToggleGroup(root);
        ApplyResolutionDropdown(root);
        ApplyBottomButton(root, "ApplyButton", new Vector2(-75f, 190f), new Vector2(130f, 40f), "적용");
        ApplyOptionsCancelButton(root);
        ApplyBottomEdgeButton(root, "ReturnTitleButton", 45f, 45f, 105f, 42f, "타이틀로");
        ApplyBottomEdgeButton(root, "QuitButton", 45f, 45f, 50f, 42f, "게임 종료");

        ApplyConfirmModal(root);

        // 폰트는 계층 전체(OptionsPanel 아래 모든 TMP_Text, ConfirmModal 포함)를 한 번에 순회해 적용한다.
        ApplyFontToAllTMP(root, koreanFont);

        AutoWireOptionsPanelUI(optionsPanelGO);

        EditorSceneManager.MarkSceneDirty(optionsPanelGO.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log("[OptionsPanelLayoutTool] OptionsPanel 레이아웃 적용 완료.");
    }

    // ─────────────────────────────────────────
    // 탐색
    // ─────────────────────────────────────────

    /// <summary>열려 있는 모든 씬의 루트 오브젝트를 뒤져 이름이 정확히 "OptionsPanel"인 오브젝트를 찾는다.</summary>
    private static GameObject FindOptionsPanel()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject found = FindByNameRecursive(root.transform, "OptionsPanel");
                if (found != null) return found;
            }
        }

        return null;
    }

    private static GameObject FindByNameRecursive(Transform current, string name)
    {
        if (current.name == name) return current.gameObject;

        for (int i = 0; i < current.childCount; i++)
        {
            GameObject found = FindByNameRecursive(current.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>root 기준 상대 경로("A/B/C")로 자식을 찾는다. 못 찾으면 경고만 남기고 null.</summary>
    private static Transform FindChild(Transform root, string relativePath)
    {
        Transform found = root.Find(relativePath);
        if (found == null)
        {
            Debug.LogWarning($"[OptionsPanelLayoutTool] 오브젝트를 찾지 못했습니다: {root.name}/{relativePath}");
        }
        return found;
    }

    private static TMP_FontAsset FindKoreanFontAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null && font.name.IndexOf("NEXON", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return font;
        }
        return null;
    }

    // ─────────────────────────────────────────
    // RectTransform 적용 — 공용 헬퍼
    // ─────────────────────────────────────────

    /// <summary>
    /// 앵커/피벗을 설정한 뒤, X/Y 축을 각각 "스트레치(Left/Right, Top/Bottom)" 또는
    /// "고정 위치(Pos/Size)"로 적용한다. left+right가 둘 다 있으면 X는 스트레치,
    /// top만 있고 bottom이 없으면(TitleText류) pivot이 위쪽에 붙은 "Top 오프셋" 관례로
    /// anchoredPosition.y = -top 로 처리한다.
    /// </summary>
    private static void ApplyRect(
        RectTransform rt,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        float? left = null, float? right = null,
        float? top = null, float? bottom = null,
        float? posX = null, float? posY = null,
        float? width = null, float? height = null)
    {
        if (rt == null) return;

        Undo.RecordObject(rt, UNDO_NAME);

        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;

        if (left.HasValue && right.HasValue)
        {
            SetLeft(rt, left.Value);
            SetRight(rt, right.Value);
        }
        else
        {
            Vector2 pos = rt.anchoredPosition;
            Vector2 size = rt.sizeDelta;
            if (posX.HasValue) pos.x = posX.Value;
            if (width.HasValue) size.x = width.Value;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        if (top.HasValue && bottom.HasValue)
        {
            SetTop(rt, top.Value);
            SetBottom(rt, bottom.Value);
        }
        else if (top.HasValue)
        {
            Vector2 pos = rt.anchoredPosition;
            Vector2 size = rt.sizeDelta;
            pos.y = -top.Value;
            if (height.HasValue) size.y = height.Value;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
        else
        {
            Vector2 pos = rt.anchoredPosition;
            Vector2 size = rt.sizeDelta;
            if (posY.HasValue) pos.y = posY.Value;
            if (height.HasValue) size.y = height.Value;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        EditorUtility.SetDirty(rt);
    }

    private static void SetLeft(RectTransform rt, float left)   => rt.offsetMin = new Vector2(left, rt.offsetMin.y);
    private static void SetRight(RectTransform rt, float right) => rt.offsetMax = new Vector2(-right, rt.offsetMax.y);
    private static void SetTop(RectTransform rt, float top)     => rt.offsetMax = new Vector2(rt.offsetMax.x, -top);
    private static void SetBottom(RectTransform rt, float bottom) => rt.offsetMin = new Vector2(rt.offsetMin.x, bottom);

    private static void ApplyFullStretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        ApplyRect(rt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            left: left, right: right, top: top, bottom: bottom);
    }

    // ─────────────────────────────────────────
    // 텍스트/이미지/컴포넌트 적용 — 공용 헬퍼
    // ─────────────────────────────────────────

    private static void ApplyText(Transform t, string content, TextAlignmentOptions? alignment = null, float? fontSize = null, Color? color = null)
    {
        if (t == null) return;

        var tmp = t.GetComponent<TMP_Text>();
        if (tmp == null)
        {
            Debug.LogWarning($"[OptionsPanelLayoutTool] TMP_Text 컴포넌트를 찾지 못했습니다: {t.name}");
            return;
        }

        Undo.RecordObject(tmp, UNDO_NAME);
        if (content != null) tmp.text = content;
        if (alignment.HasValue) tmp.alignment = alignment.Value;
        if (fontSize.HasValue) tmp.fontSize = fontSize.Value;
        if (color.HasValue) tmp.color = color.Value;
        EditorUtility.SetDirty(tmp);
    }

    private static void ApplyImage(Transform t, Color color, bool raycastTarget)
    {
        if (t == null) return;

        var image = t.GetComponent<Image>();
        if (image == null)
        {
            Debug.LogWarning($"[OptionsPanelLayoutTool] Image 컴포넌트를 찾지 못했습니다: {t.name}");
            return;
        }

        Undo.RecordObject(image, UNDO_NAME);
        image.color = color;
        image.raycastTarget = raycastTarget;
        EditorUtility.SetDirty(image);
    }

    /// <summary>버튼의 "Text (TMP)" 자식을 Full Stretch + 지정 문구/정렬/크기로 맞춘다.</summary>
    private static void ApplyButtonText(Transform buttonRoot, string content, float fontSize)
    {
        if (buttonRoot == null) return;

        Transform textChild = FindChild(buttonRoot, "Text (TMP)");
        if (textChild == null) return;

        var rt = textChild.GetComponent<RectTransform>();
        ApplyFullStretch(rt, 0f, 0f, 0f, 0f);
        ApplyText(textChild, content, TextAlignmentOptions.Center, fontSize);
    }

    private static void SetActiveWithUndo(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf == active) return;

        Undo.RecordObject(go, UNDO_NAME);
        go.SetActive(active);
        EditorUtility.SetDirty(go);
    }

    // ─────────────────────────────────────────
    // OptionsPanel 루트 / Background / TitleText / CloseButton
    // ─────────────────────────────────────────

    private static void ApplyOptionsPanelRoot(GameObject optionsPanelGO)
    {
        var rt = optionsPanelGO.GetComponent<RectTransform>();
        if (rt == null)
        {
            Debug.LogWarning("[OptionsPanelLayoutTool] OptionsPanel에 RectTransform이 없습니다.");
            return;
        }

        ApplyRect(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            posX: 0f, posY: 0f, width: 520f, height: 620f);

        Undo.RecordObject(rt, UNDO_NAME);
        rt.localScale = Vector3.one;
        EditorUtility.SetDirty(rt);
    }

    private static void ApplyBackground(Transform root)
    {
        Transform t = FindChild(root, "Background");
        if (t == null) return;

        ApplyFullStretch(t.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        // 어두운 반투명 배경(정확한 색상 미지정 — 팀 톤에 맞춰 조정 가능).
        ApplyImage(t, new Color32(18, 18, 22, 235), true);
    }

    private static void ApplyTitleText(Transform root)
    {
        Transform t = FindChild(root, "TitleText");
        if (t == null) return;

        ApplyRect(t.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            left: 80f, right: 80f, top: 15f, height: 42f);
        ApplyText(t, "옵션", TextAlignmentOptions.Center, 24f);
    }

    private static void ApplyCloseButton(Transform root)
    {
        Transform t = FindChild(root, "CloseButton");
        if (t == null) return;

        ApplyRect(t.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            posX: -15f, posY: -15f, width: 70f, height: 34f);
        ApplyButtonText(t, "닫기", 18f);
    }

    // ─────────────────────────────────────────
    // 화면 모드 토글 / 해상도 드롭다운
    // ─────────────────────────────────────────

    /// <summary>라벨/체크마크가 배경과 잘 구분되도록 쓰는 고정 색상(정확한 팀 톤 미지정 — 조정 가능).</summary>
    private static readonly Color TOGGLE_LABEL_COLOR = new Color32(235, 235, 240, 255);
    private static readonly Color TOGGLE_CHECK_COLOR = new Color32(90, 170, 255, 255);

    private static void ApplyWindowedToggle(Transform root)
    {
        Transform t = FindChild(root, "WindowedToggle");
        if (t == null) return;

        ApplyRect(t.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            posX: 140f, posY: -280f, width: 130f, height: 30f);

        Transform label = FindChild(t, "Label");
        if (label != null) ApplyText(label, "창모드", color: TOGGLE_LABEL_COLOR);

        BrightenToggleCheckmark(t);
    }

    private static void ApplyFullscreenToggle(Transform root)
    {
        Transform t = FindChild(root, "FullscreenToggle");
        if (t == null) return;

        ApplyRect(t.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            posX: -140f, posY: -280f, width: 130f, height: 30f);

        Transform label = FindChild(t, "Label");
        if (label != null) ApplyText(label, "전체화면", color: TOGGLE_LABEL_COLOR);

        BrightenToggleCheckmark(t);
    }

    /// <summary>표준 Toggle 프리팹 구조(Background/Checkmark 또는 Checkmark)를 시도해 체크마크 색상을 보정한다.</summary>
    private static void BrightenToggleCheckmark(Transform toggleRoot)
    {
        Transform checkmark = toggleRoot.Find("Background/Checkmark");
        if (checkmark == null) checkmark = toggleRoot.Find("Checkmark");

        if (checkmark == null)
        {
            Debug.LogWarning(
                $"[OptionsPanelLayoutTool] {toggleRoot.name}에서 체크마크 오브젝트(Background/Checkmark)를 " +
                "찾지 못해 선택 표시 색상 보정을 건너뜁니다 — 구조를 직접 확인해 주세요.");
            return;
        }

        ApplyImage(checkmark, TOGGLE_CHECK_COLOR, false);
    }

    /// <summary>
    /// WindowedToggle/FullscreenToggle이 동시에 켜지지 않고 최소 1개는 항상 선택되도록
    /// ToggleGroup(allowSwitchOff=false)으로 묶는다. 없으면 OptionsPanel에 컴포넌트를 추가한다
    /// (새 GameObject는 만들지 않음).
    /// </summary>
    private static void ApplyToggleGroup(Transform root)
    {
        Transform windowed = FindChild(root, "WindowedToggle");
        Transform fullscreen = FindChild(root, "FullscreenToggle");
        if (windowed == null || fullscreen == null) return;

        var windowedToggle = windowed.GetComponent<Toggle>();
        var fullscreenToggle = fullscreen.GetComponent<Toggle>();
        if (windowedToggle == null || fullscreenToggle == null)
        {
            Debug.LogWarning(
                "[OptionsPanelLayoutTool] WindowedToggle/FullscreenToggle에서 Toggle 컴포넌트를 찾지 못해 " +
                "ToggleGroup 배선을 건너뜁니다.");
            return;
        }

        GameObject panelGO = root.gameObject;
        var group = panelGO.GetComponent<ToggleGroup>();
        if (group == null) group = Undo.AddComponent<ToggleGroup>(panelGO);

        Undo.RecordObject(group, UNDO_NAME);
        group.allowSwitchOff = false; // 최소 1개는 항상 선택 — 그룹이 보장.
        EditorUtility.SetDirty(group);

        Undo.RecordObject(windowedToggle, UNDO_NAME);
        windowedToggle.group = group;
        EditorUtility.SetDirty(windowedToggle);

        Undo.RecordObject(fullscreenToggle, UNDO_NAME);
        fullscreenToggle.group = group;
        EditorUtility.SetDirty(fullscreenToggle);
    }

    /// <summary>Caption Text 후보 이름(우선순위 순). TMP_Dropdown 기본 프리팹은 "Label".</summary>
    private static readonly string[] CAPTION_TEXT_CANDIDATE_NAMES = { "Label", "Caption Text" };

    private static void ApplyResolutionDropdown(Transform root)
    {
        Transform t = FindChild(root, "ResolutionDropdown");
        if (t == null) return;

        ApplyRect(t.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            left: 80f, right: 80f, posY: -330f, height: 38f);

        var dropdown = t.GetComponent<TMP_Dropdown>();
        if (dropdown == null)
        {
            Debug.LogWarning("[OptionsPanelLayoutTool] ResolutionDropdown에서 TMP_Dropdown 컴포넌트를 찾지 못했습니다.");
            return;
        }

        Transform captionText = null;
        foreach (string candidate in CAPTION_TEXT_CANDIDATE_NAMES)
        {
            captionText = t.Find(candidate);
            if (captionText != null) break;
        }

        Transform template = t.Find("Template");
        Transform itemText = t.Find("Template/Viewport/Content/Item/Item Label");

        var so = new SerializedObject(dropdown);
        Undo.RecordObject(dropdown, UNDO_NAME);

        WireDropdownSubComponent(so, "m_CaptionText", captionText != null ? captionText.GetComponent<TMP_Text>() : null);
        WireDropdownSubComponent(so, "m_ItemText", itemText != null ? itemText.GetComponent<TMP_Text>() : null);
        WireDropdownSubComponent(so, "m_Template", template != null ? template.GetComponent<RectTransform>() : null);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(dropdown);

        // "New Text" 같은 디자인 시점 기본 문구가 남지 않게 정리한다.
        // 실제 표시값은 런타임에 OptionsPanelUI가 ClearOptions/AddOptions로 다시 채운다.
        if (captionText != null)
        {
            var captionTMP = captionText.GetComponent<TMP_Text>();
            if (captionTMP != null && captionTMP.text == "New Text")
                ApplyText(captionText, "해상도");
        }

        if (captionText == null || itemText == null || template == null)
        {
            Debug.LogWarning(
                "[OptionsPanelLayoutTool] ResolutionDropdown의 Caption Text/Item Text/Template 중 일부를 " +
                "찾지 못했습니다 — TMP_Dropdown 내부 구조가 불완전할 수 있습니다(Unity 'Dropdown - TextMeshPro' " +
                "기본 구조와 비교해 수동으로 확인해 주세요). 이 도구는 새 오브젝트를 만들지 않습니다.");
        }
    }

    /// <summary>target이 있고 현재 값과 다를 때만 SerializedProperty를 갱신한다(불필요한 초기화 방지).</summary>
    private static void WireDropdownSubComponent(SerializedObject so, string propertyName, Object target)
    {
        if (target == null) return;

        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null) return;
        if (prop.objectReferenceValue == target) return;

        prop.objectReferenceValue = target;
    }

    // ─────────────────────────────────────────
    // 하단 버튼들
    // ─────────────────────────────────────────

    private static void ApplyBottomButton(Transform root, string name, Vector2 anchoredPosition, Vector2 sizeDelta, string text)
    {
        Transform t = FindChild(root, name);
        if (t == null) return;

        ApplyRect(t.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            posX: anchoredPosition.x, posY: anchoredPosition.y, width: sizeDelta.x, height: sizeDelta.y);
        ApplyButtonText(t, text, 18f);
    }

    /// <summary>
    /// 옵션 본문의 CancelButton(OptionsPanel 직계 자식)만 대상으로 한다.
    /// ConfirmModal/Popup/CancelButton과 이름이 같아 반드시 root.Find("CancelButton")로 경로를 못박아 구분한다.
    /// </summary>
    private static void ApplyOptionsCancelButton(Transform root)
    {
        ApplyBottomButton(root, "CancelButton", new Vector2(75f, 190f), new Vector2(130f, 40f), "취소");
    }

    private static void ApplyBottomEdgeButton(Transform root, string name, float left, float right, float posY, float height, string text)
    {
        Transform t = FindChild(root, name);
        if (t == null) return;

        ApplyRect(t.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            left: left, right: right, posY: posY, height: height);
        ApplyButtonText(t, text, 18f);
    }

    // ─────────────────────────────────────────
    // ConfirmModal
    // ─────────────────────────────────────────

    private static void ApplyConfirmModal(Transform root)
    {
        Transform confirmModal = FindChild(root, "ConfirmModal");
        if (confirmModal == null) return;

        ApplyFullStretch(confirmModal.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        SetActiveWithUndo(confirmModal.gameObject, false);

        Transform dim = FindChild(confirmModal, "Dim");
        if (dim != null)
        {
            ApplyFullStretch(dim.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            ApplyImage(dim, new Color32(0, 0, 0, 140), true);
        }

        Transform popup = FindChild(confirmModal, "Popup");
        if (popup == null) return;

        ApplyRect(popup.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            posX: 0f, posY: 0f, width: 340f, height: 150f);
        // 옵션창(Background)보다 약간 밝은 어두운 색.
        ApplyImage(popup, new Color32(35, 35, 42, 255), true);

        Transform messageText = FindChild(popup, "MessageText");
        if (messageText != null)
        {
            ApplyRect(messageText.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                left: 20f, right: 20f, top: 20f, height: 55f);
            ApplyText(messageText, "타이틀 화면으로 이동하시겠습니까?", TextAlignmentOptions.Center, 20f);
        }

        Transform confirmButton = FindChild(popup, "ConfirmButton");
        if (confirmButton != null)
        {
            ApplyRect(confirmButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                posX: -70f, posY: 18f, width: 120f, height: 38f);
            ApplyButtonText(confirmButton, "확인", 18f);
        }

        // Popup 직계 자식 CancelButton — ApplyOptionsCancelButton(OptionsPanel 직계)과는 root.Find 경로가 달라 혼동되지 않는다.
        Transform modalCancelButton = FindChild(popup, "CancelButton");
        if (modalCancelButton != null)
        {
            ApplyRect(modalCancelButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                posX: 70f, posY: 18f, width: 120f, height: 38f);
            ApplyButtonText(modalCancelButton, "취소", 18f);
        }
    }

    // ─────────────────────────────────────────
    // TMP 폰트 일괄 적용
    // ─────────────────────────────────────────

    /// <summary>
    /// root 아래 모든 TMP_Text(비활성 포함 — Dropdown Template은 기본 비활성)를 순회해 폰트만 교체한다.
    /// 머티리얼(m_fontMaterial/m_fontSharedMaterial)은 건드리지 않는다.
    /// </summary>
    private static void ApplyFontToAllTMP(Transform root, TMP_FontAsset font)
    {
        if (font == null) return;

        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text.font == font) continue;

            var so = new SerializedObject(text);
            SerializedProperty fontProp = so.FindProperty("m_fontAsset");
            if (fontProp == null) continue;

            Undo.RecordObject(text, UNDO_NAME);
            fontProp.objectReferenceValue = font;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(text);
        }
    }

    // ─────────────────────────────────────────
    // OptionsPanelUI 필드 자동 연결
    // ─────────────────────────────────────────

    private static void AutoWireOptionsPanelUI(GameObject optionsPanelGO)
    {
        var ui = optionsPanelGO.GetComponent<OptionsPanelUI>();
        if (ui == null) ui = Object.FindFirstObjectByType<OptionsPanelUI>();

        if (ui == null)
        {
            Debug.LogWarning("[OptionsPanelLayoutTool] OptionsPanelUI 컴포넌트를 찾지 못해 필드 자동 연결을 건너뜁니다.");
            return;
        }

        Transform root = optionsPanelGO.transform;
        Transform confirmModal = root.Find("ConfirmModal");
        Transform popup = confirmModal != null ? confirmModal.Find("Popup") : null;
        Transform messageText = popup != null ? popup.Find("MessageText") : null;
        Transform confirmButton = popup != null ? popup.Find("ConfirmButton") : null;
        Transform modalCancelButton = popup != null ? popup.Find("CancelButton") : null;

        // 볼륨은 행 프리팹(VolumeRow_Pf) 단위로 바뀌었다 — 슬라이더/텍스트를 따로 찾지 않는다.
        Transform masterRow = root.Find("Sound_Page/MasterVolume");
        Transform bgmRow = root.Find("Sound_Page/BgmVolume");
        Transform sfxRow = root.Find("Sound_Page/SfxVolume");

        Transform windowedToggle = root.Find("WindowedToggle");
        Transform fullscreenToggle = root.Find("FullscreenToggle");
        Transform resolutionDropdown = root.Find("ResolutionDropdown");
        Transform applyButton = root.Find("ApplyButton");
        Transform cancelOptionsButton = root.Find("CancelButton"); // OptionsPanel 직계 — ConfirmModal 쪽과 경로로 구분됨
        Transform closeButton = root.Find("CloseButton");
        Transform returnTitleButton = root.Find("ReturnTitleButton");
        Transform quitButton = root.Find("QuitButton");

        Undo.RecordObject(ui, UNDO_NAME);

        var so = new SerializedObject(ui);

        // 옵션 패널 루트 자신을 그대로 연결한다(OptionsPanelUI는 이 오브젝트에 붙어 있으며,
        // 표시/숨김은 SetActive가 아니라 CanvasGroup으로 처리하므로 자기 자신 참조가 안전하다).
        WireField(so, "_optionsPanelRoot", optionsPanelGO);

        WireField(so, "_masterRow", masterRow != null ? masterRow.GetComponent<VolumeRowUI>() : null);
        WireField(so, "_bgmRow", bgmRow != null ? bgmRow.GetComponent<VolumeRowUI>() : null);
        WireField(so, "_sfxRow", sfxRow != null ? sfxRow.GetComponent<VolumeRowUI>() : null);

        WireField(so, "_windowedToggle", windowedToggle != null ? windowedToggle.GetComponent<Toggle>() : null);
        WireField(so, "_fullscreenToggle", fullscreenToggle != null ? fullscreenToggle.GetComponent<Toggle>() : null);
        WireField(so, "_resolutionDropdown", resolutionDropdown != null ? resolutionDropdown.GetComponent<TMP_Dropdown>() : null);

        WireField(so, "_applyButton", applyButton != null ? applyButton.GetComponent<Button>() : null);
        WireField(so, "_cancelOptionsButton", cancelOptionsButton != null ? cancelOptionsButton.GetComponent<Button>() : null);
        WireField(so, "_closeButton", closeButton != null ? closeButton.GetComponent<Button>() : null);
        WireField(so, "_returnTitleButton", returnTitleButton != null ? returnTitleButton.GetComponent<Button>() : null);
        WireField(so, "_quitButton", quitButton != null ? quitButton.GetComponent<Button>() : null);

        WireField(so, "_confirmModal", confirmModal != null ? confirmModal.gameObject : null);
        WireField(so, "_confirmMessageText", messageText != null ? messageText.GetComponent<TMP_Text>() : null);
        WireField(so, "_confirmButton", confirmButton != null ? confirmButton.GetComponent<Button>() : null);
        WireField(so, "_cancelButton", modalCancelButton != null ? modalCancelButton.GetComponent<Button>() : null);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);

        WireOpenOptionsButton(optionsPanelGO, ui);
    }

    /// <summary>
    /// 화면 왼쪽 아래 "설정" 버튼을 씬에서 찾아 OptionsPanelUI._openOptionsButton에 연결한다.
    /// OptionsPanel 계층 밖에서, 이름 또는 자식 텍스트에 "설정"/"Settings"/"Option"이 포함되고
    /// Button 컴포넌트가 있는 오브젝트를 찾는다. 찾은 오브젝트에 Button이 없으면 컴포넌트만
    /// 추가한다(시각 디자인은 건드리지 않음). 새 GameObject는 만들지 않으며, 후보를 전혀 찾지
    /// 못하면 경고만 남기고 기존 값을 유지한다.
    /// </summary>
    private static void WireOpenOptionsButton(GameObject optionsPanelGO, OptionsPanelUI ui)
    {
        GameObject candidate = FindSettingsButtonCandidate(optionsPanelGO);

        if (candidate == null)
        {
            Debug.LogWarning(
                "[OptionsPanelLayoutTool] 화면 왼쪽 아래 '설정' 버튼 오브젝트를 씬에서 찾지 못했습니다 — " +
                "_openOptionsButton은 수동으로 연결해 주세요(이 도구는 새 오브젝트를 만들지 않습니다). " +
                "그때까지는 ESC 키로 옵션 패널을 열 수 있습니다.");
            return;
        }

        var button = candidate.GetComponent<Button>();
        if (button == null)
        {
            // 오브젝트는 있으나 Button 컴포넌트가 없는 경우만 추가한다 — 시각 요소는 건드리지 않는다.
            button = Undo.AddComponent<Button>(candidate);
            Debug.Log($"[OptionsPanelLayoutTool] '{candidate.name}'에 Button 컴포넌트를 추가했습니다(시각 디자인 변경 없음).");
        }

        Undo.RecordObject(ui, UNDO_NAME);
        var so = new SerializedObject(ui);
        WireField(so, "_openOptionsButton", button);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);
    }

    private static readonly string[] SETTINGS_BUTTON_NAME_HINTS = { "설정", "Settings", "Option" };

    private static GameObject FindSettingsButtonCandidate(GameObject optionsPanelGO)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject found = FindSettingsButtonRecursive(root.transform, optionsPanelGO.transform);
                if (found != null) return found;
            }
        }

        return null;
    }

    private static GameObject FindSettingsButtonRecursive(Transform current, Transform excludeSubtree)
    {
        if (current == excludeSubtree) return null; // OptionsPanel 계층 자체는 후보에서 제외

        if (NameLooksLikeSettingsButton(current.name) ||
            (current.GetComponent<Button>() != null && HasChildTextMatchingHint(current)))
        {
            return current.gameObject;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (child == excludeSubtree) continue;

            GameObject found = FindSettingsButtonRecursive(child, excludeSubtree);
            if (found != null) return found;
        }

        return null;
    }

    private static bool NameLooksLikeSettingsButton(string name)
    {
        foreach (string hint in SETTINGS_BUTTON_NAME_HINTS)
            if (name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                name.IndexOf("Button", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    private static bool HasChildTextMatchingHint(Transform t)
    {
        foreach (TMP_Text text in t.GetComponentsInChildren<TMP_Text>(true))
        {
            if (string.IsNullOrEmpty(text.text)) continue;
            foreach (string hint in SETTINGS_BUTTON_NAME_HINTS)
                if (text.text.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
        }
        return false;
    }

    /// <summary>
    /// 이미 올바른 값이 연결돼 있으면 건드리지 않는다(불필요한 초기화 금지).
    /// target이 null이면(오브젝트/컴포넌트를 못 찾음) 경고만 남기고 기존 값은 그대로 둔다.
    /// </summary>
    private static void WireField(SerializedObject so, string fieldName, Object target)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[OptionsPanelLayoutTool] OptionsPanelUI에 필드가 없습니다: {fieldName}");
            return;
        }

        if (target == null)
        {
            Debug.LogWarning($"[OptionsPanelLayoutTool] {fieldName}에 연결할 오브젝트를 찾지 못했습니다 — 기존 값 유지.");
            return;
        }

        if (prop.objectReferenceValue == target)
            return;

        prop.objectReferenceValue = target;
    }
}
