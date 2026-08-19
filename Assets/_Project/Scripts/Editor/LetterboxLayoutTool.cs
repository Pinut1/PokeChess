using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 열려 있는 씬의 Canvas를 <b>16:9 레터박스 구조</b>로 바꾸는 에디터 전용 도구.
///
/// 만드는 것:
/// <code>
/// Canvas
///  └ SafeArea_16x9      AspectRatioFitter(FitInParent, 16/9) — 기존 UI가 전부 이 밑으로 들어간다
///     ├ Letterbox_Top / Bottom / Left / Right   검은 Image (여백 가림)
///     └ (기존 직속 자식들)
/// </code>
///
/// <b>왜 필요한가</b> — CanvasScaler Reference가 1920x1080(16:9)이라 화면 비율이 다르면 논리 캔버스
/// 크기가 달라져 UI 배치가 어긋난다. SafeArea_16x9는 부모 안에 들어가는 가장 큰 16:9 박스를 만들어
/// 가운데 정렬하므로, <b>어떤 비율에서도 UI가 보는 캔버스는 항상 1920x1080 하나</b>가 된다.
/// (CanvasScaler는 Screen Match Mode = Expand여야 한다 — 그래야 부모가 Reference 이상으로 보장된다.)
///
/// <b>검은 띠에 런타임 코드가 없는 이유</b> — 띠를 SafeArea의 <b>자식</b>으로 두고 각 변에 앵커한 뒤
/// 바깥쪽으로 충분히 크게(BAR_THICKNESS) 뻗어 놓는다. SafeArea 바깥은 정의상 전부 여백이므로,
/// 화면 밖으로 넘치는 부분은 그냥 안 보일 뿐이라 매 프레임 크기를 계산할 필요가 없다.
///
/// <b>여러 번 실행해도 안전하다</b> — 이미 SafeArea_16x9가 있으면 새로 만들지 않고 값만 맞추며,
/// 그 뒤에 Canvas 밑으로 새로 추가된 UI가 있으면 마저 안으로 옮긴다.
/// </summary>
public static class LetterboxLayoutTool
{
    private const string UNDO_NAME   = "Apply 16:9 Letterbox";
    private const string SAFE_AREA   = "SafeArea_16x9";
    private const float  ASPECT_16_9 = 16f / 9f;

    /// <summary>검은 띠가 SafeArea 바깥으로 뻗는 길이(px). 어떤 비율에서도 여백보다 크면 된다.</summary>
    private const float BAR_THICKNESS = 2000f;

    private static readonly string[] BarNames =
    {
        "Letterbox_Top", "Letterbox_Bottom", "Letterbox_Left", "Letterbox_Right"
    };

    [MenuItem("PokeChess/UI/Apply 16:9 Letterbox")]
    private static void Apply()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        if (canvases.Length == 0)
        {
            Debug.LogError("[LetterboxLayoutTool] 열려 있는 씬에서 Canvas를 찾지 못했습니다.");
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);

        int applied = 0;
        foreach (Canvas canvas in canvases)
        {
            // 자식 Canvas(중첩 캔버스)는 건드리지 않는다 — 루트 Canvas 하나만 가두면 충분하고,
            // 중첩 캔버스까지 감싸면 렌더 순서가 꼬인다.
            if (canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>() != null)
                continue;

            if (ApplyToCanvas(canvas)) applied++;
        }

        if (applied > 0)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"[LetterboxLayoutTool] Canvas {applied}개에 16:9 레터박스 적용 완료. " +
                  "CanvasScaler의 Screen Match Mode가 Expand인지 확인하세요.");
    }

    private static bool ApplyToCanvas(Canvas canvas)
    {
        var canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return false;

        WarnIfScalerNotExpand(canvas);

        RectTransform safeArea = FindOrCreateSafeArea(canvasRect);

        // 기존 자식을 SafeArea 안으로 이동. 뒤에서부터 도는 이유는 SetParent가 컬렉션을 흔들기 때문이다.
        // worldPositionStays=false로 옮겨야 앵커 기준 로컬 배치가 그대로 유지된다.
        int moved = 0;
        for (int i = canvasRect.childCount - 1; i >= 0; i--)
        {
            Transform child = canvasRect.GetChild(i);
            if (child == safeArea) continue;

            Undo.SetTransformParent(child, safeArea, UNDO_NAME);
            child.SetAsFirstSibling(); // 검은 띠보다 아래로 — 띠가 항상 맨 위에 그려지게 한다
            moved++;
        }

        foreach (string barName in BarNames)
            CreateOrUpdateBar(safeArea, barName);

        Debug.Log($"[LetterboxLayoutTool] '{canvas.name}' — 자식 {moved}개를 {SAFE_AREA} 안으로 이동.");
        return true;
    }

    /// <summary>
    /// CanvasScaler가 Expand가 아니면 경고만 남긴다 — 값을 마음대로 바꾸면 다른 화면 배치까지
    /// 흔들리므로 판단은 사람에게 맡긴다.
    /// </summary>
    private static void WarnIfScalerNotExpand(Canvas canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;

        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
            scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
        {
            Debug.LogWarning(
                $"[LetterboxLayoutTool] '{canvas.name}'의 CanvasScaler가 " +
                "Scale With Screen Size + Expand가 아닙니다. Expand가 아니면 부모 캔버스가 Reference보다 " +
                "작아질 수 있어 SafeArea까지 같이 줄어듭니다.", canvas);
        }
    }

    private static RectTransform FindOrCreateSafeArea(RectTransform canvasRect)
    {
        Transform existing = canvasRect.Find(SAFE_AREA);
        RectTransform rect;

        if (existing != null)
        {
            rect = existing as RectTransform;
        }
        else
        {
            var go = new GameObject(SAFE_AREA, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
            rect = go.GetComponent<RectTransform>();
            Undo.SetTransformParent(rect, canvasRect, UNDO_NAME);
        }

        // 부모를 꽉 채우게 늘려 둔다 — 실제 16:9 축소는 AspectRatioFitter가 한다.
        Undo.RecordObject(rect, UNDO_NAME);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        EditorUtility.SetDirty(rect);

        var fitter = rect.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = Undo.AddComponent<AspectRatioFitter>(rect.gameObject);

        Undo.RecordObject(fitter, UNDO_NAME);
        fitter.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = ASPECT_16_9;
        EditorUtility.SetDirty(fitter);

        return rect;
    }

    /// <summary>
    /// 검은 띠 1장. SafeArea의 한 변에 붙여 바깥쪽으로 <see cref="BAR_THICKNESS"/>만큼 뻗는다.
    /// 여백보다 길어 화면 밖으로 넘치지만, 넘친 부분은 보이지 않으므로 문제되지 않는다.
    /// </summary>
    private static void CreateOrUpdateBar(RectTransform safeArea, string barName)
    {
        Transform existing = safeArea.Find(barName);
        RectTransform rect;

        if (existing != null)
        {
            rect = existing as RectTransform;
        }
        else
        {
            var go = new GameObject(barName, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
            rect = go.GetComponent<RectTransform>();
            Undo.SetTransformParent(rect, safeArea, UNDO_NAME);
        }

        Undo.RecordObject(rect, UNDO_NAME);
        rect.localScale = Vector3.one;

        switch (barName)
        {
            case "Letterbox_Top":
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot     = new Vector2(0.5f, 0f);   // 위쪽 변에서 위로
                rect.sizeDelta = new Vector2(BAR_THICKNESS * 2f, BAR_THICKNESS);
                break;

            case "Letterbox_Bottom":
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot     = new Vector2(0.5f, 1f);   // 아래쪽 변에서 아래로
                rect.sizeDelta = new Vector2(BAR_THICKNESS * 2f, BAR_THICKNESS);
                break;

            case "Letterbox_Left":
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot     = new Vector2(1f, 0.5f);   // 왼쪽 변에서 왼쪽으로
                rect.sizeDelta = new Vector2(BAR_THICKNESS, BAR_THICKNESS * 2f);
                break;

            case "Letterbox_Right":
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot     = new Vector2(0f, 0.5f);   // 오른쪽 변에서 오른쪽으로
                rect.sizeDelta = new Vector2(BAR_THICKNESS, BAR_THICKNESS * 2f);
                break;
        }

        rect.anchoredPosition = Vector2.zero;
        rect.SetAsLastSibling(); // 항상 맨 위에 그려 여백에 비치는 3D 화면을 가린다
        EditorUtility.SetDirty(rect);

        var image = rect.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(rect.gameObject);

        Undo.RecordObject(image, UNDO_NAME);
        image.color        = Color.black;
        image.raycastTarget = false; // 띠가 클릭을 먹으면 안 된다
        EditorUtility.SetDirty(image);
    }
}
