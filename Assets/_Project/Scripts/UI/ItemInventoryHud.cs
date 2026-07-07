using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 아이템/진화의 돌을 마우스로 집어 보드 위 유닛에 드래그해서 장착하는 프로토타입 UI(IMGUI).
/// 정식 uGUI 드래그앤드롭은 황해인 담당 — 이건 그 전까지 쓰는 임시 버전.
/// 장착 해제는 유닛을 클릭해 목록을 펼치고 버튼으로 처리(드래그-아웃은 아님, 스코프 단순화).
/// 쇼핑 페이즈에서만 동작(UnitDragController와 동일 제약 — 전투 중엔 유닛이 비활성/미러 좌표라 의미 없음).
/// </summary>
public class ItemInventoryHud : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _raycastMask = ~0;

    private const float SLOT_W = 90f, SLOT_H = 40f, SLOT_GAP = 4f;
    private const int   COLS = 4;
    private const float PANEL_X = 10f;
    // 좌측 상단은 시너지 특성 패널(SynergyHud)이 사용 → 인벤토리는 하단 좌측(상점 바 위)으로.
    // OnGUI에서 행 수에 맞춰 Screen.height 기준으로 매 프레임 설정(위로 성장).
    private float _panelY;

    private ScriptableObject _dragging;
    private string _dragLabel;
    private PokemonUnit _inspectUnit;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Item == null) return;

        if (gm.Phase != null && gm.Phase.CurrentPhase != GamePhase.Shopping)
        {
            _dragging = null;
            _inspectUnit = null;
            return;
        }

        // 하단 좌측 앵커: 현재 행 수만큼 높이를 잡아 상점 바(하단 중앙, ~Screen.height-120) 위에 배치.
        int total = gm.Item.Items.Count + gm.Item.Stones.Count;
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(total / (float)COLS));
        float panelH = 24f + rowCount * (SLOT_H + SLOT_GAP) + 6f;
        _panelY = Screen.height - 150f - panelH;

        HandleInput(gm);
        DrawInventory(gm);
        DrawInspectPanel(gm);
        DrawDragGhost();
    }

    private Rect SlotRect(int index)
    {
        float x = PANEL_X + (index % COLS) * (SLOT_W + SLOT_GAP);
        float y = _panelY + 24f + (index / COLS) * (SLOT_H + SLOT_GAP);
        return new Rect(x, y, SLOT_W, SLOT_H);
    }

    private void DrawInventory(GameManager gm)
    {
        var item = gm.Item;
        int total = item.Items.Count + item.Stones.Count;
        int rows = Mathf.Max(1, Mathf.CeilToInt(total / (float)COLS));

        GUI.Box(new Rect(PANEL_X - 5f, _panelY, COLS * (SLOT_W + SLOT_GAP) + 6f, 24f + rows * (SLOT_H + SLOT_GAP) + 6f),
            "인벤토리 (드래그해서 유닛에 장착)");

        int i = 0;
        foreach (var it in item.Items)
        {
            GUI.Box(SlotRect(i), it != null ? it.itemName : "?");
            i++;
        }
        foreach (var st in item.Stones)
        {
            GUI.Box(SlotRect(i), st != null ? $"[돌]{st.stoneName}" : "?");
            i++;
        }
    }

    private void HandleInput(GameManager gm)
    {
        var e = Event.current;
        var item = gm.Item;

        if (e.type == EventType.MouseDown && _dragging == null)
        {
            int i = 0;
            foreach (var it in item.Items)
            {
                if (SlotRect(i).Contains(e.mousePosition)) { _dragging = it; _dragLabel = it.itemName; e.Use(); return; }
                i++;
            }
            foreach (var st in item.Stones)
            {
                if (SlotRect(i).Contains(e.mousePosition)) { _dragging = st; _dragLabel = st.stoneName; e.Use(); return; }
                i++;
            }

            // 인벤토리 칸이 아니면 보드 위 유닛 클릭으로 간주 — 장착 목록 조회용 선택.
            var clicked = RaycastUnit(e.mousePosition);
            if (clicked != null) _inspectUnit = clicked;
        }
        else if (e.type == EventType.MouseUp && _dragging != null)
        {
            var target = RaycastUnit(e.mousePosition);
            if (target != null)
            {
                bool success = item.EquipToUnit(_dragging, target);
                Debug.Log($"[ItemHud] 장착 {(success ? "성공" : "실패")}: {_dragLabel} → {target.data?.pokemonName}");
            }
            _dragging = null;
            _dragLabel = null;
            e.Use();
        }
    }

    private void DrawDragGhost()
    {
        if (_dragging == null) return;
        Vector2 pos = Event.current.mousePosition;
        GUI.Box(new Rect(pos.x - 40f, pos.y - 15f, 80f, 30f), _dragLabel);
    }

    private void DrawInspectPanel(GameManager gm)
    {
        if (_inspectUnit == null) return;

        GUILayout.BeginArea(new Rect(Screen.width - 260f, 10f, 250f, 220f), GUI.skin.box);
        GUILayout.Label($"[{_inspectUnit.data?.pokemonName ?? "?"}] 장착 아이템");

        foreach (var it in new List<ItemData>(_inspectUnit.items))
        {
            if (GUILayout.Button($"해제: {it.itemName}"))
                gm.Item.UnequipFromUnit(it, _inspectUnit);
        }

        if (_inspectUnit.equippedStone != null)
        {
            string stoneName = _inspectUnit.equippedStone.stoneName;
            if (GUILayout.Button($"해제: [돌]{stoneName}"))
                gm.Item.UnequipFromUnit(_inspectUnit.equippedStone, _inspectUnit);
        }

        if (GUILayout.Button("닫기"))
            _inspectUnit = null;

        GUILayout.EndArea();
    }

    /// <summary>UnitDragController와 동일한 레이캐스트 패턴(유닛 Collider 필요).</summary>
    private PokemonUnit RaycastUnit(Vector2 guiMousePos)
    {
        if (_camera == null) return null;

        Vector3 screenPos = new Vector3(guiMousePos.x, Screen.height - guiMousePos.y, 0f);
        Ray ray = _camera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _raycastMask))
            return hit.collider.GetComponentInParent<PokemonUnit>();
        return null;
    }
}
