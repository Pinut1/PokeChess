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
        if (gm == null || gm.Item == null)
            return;

        bool isShopping =
            gm.Phase == null ||
            gm.Phase.CurrentPhase == GamePhase.Shopping;

        // 쇼핑이 아니면 진행 중이던 아이템 드래그만 취소한다.
        // 선택된 유닛은 유지해 전투 중에도 장착 정보를 확인할 수 있게 한다.
        if (!isShopping)
        {
            _dragging = null;
            _dragLabel = null;
        }

        // 하단 좌측 앵커
        int total = gm.Item.Items.Count + gm.Item.Stones.Count;
        int rowCount = Mathf.Max(
            1,
            Mathf.CeilToInt(total / (float)COLS)
        );

        float panelH =
            24f +
            rowCount * (SLOT_H + SLOT_GAP) +
            6f;

        _panelY = Screen.height - 150f - panelH;

        // 클릭에 의한 유닛 선택은 모든 페이즈에서 허용한다.
        // 아이템 드래그 및 장착은 HandleInput 내부에서 쇼핑일 때만 처리한다.
        HandleInput(gm, isShopping);

        // 인벤토리는 쇼핑 중에만 표시
        if (isShopping)
        {
            DrawInventory(gm);
            DrawDragGhost();
        }

        // 장착 정보 패널은 전투 중에도 표시
        DrawInspectPanel(gm, isShopping);
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

    private void HandleInput(GameManager gm, bool isShopping)
    {
        Event e = Event.current;
        var item = gm.Item;

        if (e.type == EventType.MouseDown &&
            _dragging == null)
        {
            // 쇼핑 중에만 인벤토리 아이템을 집을 수 있다.
            if (isShopping)
            {
                int i = 0;

                foreach (var it in item.Items)
                {
                    if (SlotRect(i).Contains(e.mousePosition))
                    {
                        _dragging = it;
                        _dragLabel = it.itemName;
                        e.Use();
                        return;
                    }

                    i++;
                }

                foreach (var st in item.Stones)
                {
                    if (SlotRect(i).Contains(e.mousePosition))
                    {
                        _dragging = st;
                        _dragLabel = st.stoneName;
                        e.Use();
                        return;
                    }

                    i++;
                }
            }

            // 쇼핑·전투 모두 보드 위 유닛 선택 가능
            PokemonUnit clicked =
                RaycastUnit(e.mousePosition);

            if (clicked != null)
            {
                _inspectUnit = clicked;
                e.Use();
            }
        }
        else if (
            isShopping &&
            e.type == EventType.MouseUp &&
            _dragging != null)
        {
            PokemonUnit target =
                RaycastUnit(e.mousePosition);

            if (target != null)
            {
                bool success =
                    item.EquipToUnit(_dragging, target);

                Debug.Log(
                    $"[ItemHud] 장착 " +
                    $"{(success ? "성공" : "실패")}: " +
                    $"{_dragLabel} → " +
                    $"{target.data?.pokemonName}"
                );
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

    private void DrawInspectPanel(GameManager gm, bool isShopping)
    {
        if (_inspectUnit == null)
            return;

        // 전투 도중 유닛이 제거되었을 경우 안전 처리
        if (_inspectUnit.data == null)
        {
            _inspectUnit = null;
            return;
        }

        GUILayout.BeginArea(
            new Rect(
                Screen.width - 260f,
                10f,
                250f,
                220f
            ),
            GUI.skin.box
        );

        GUILayout.Label(
            $"[{_inspectUnit.data.pokemonName}] 장착 아이템"
        );

        if (_inspectUnit.items == null ||
            _inspectUnit.items.Count == 0)
        {
            GUILayout.Label("일반 아이템 없음");
        }
        else
        {
            foreach (
                ItemData it
                in new List<ItemData>(_inspectUnit.items))
            {
                if (it == null)
                    continue;

                if (isShopping)
                {
                    if (GUILayout.Button(
                            $"해제: {it.itemName}"))
                    {
                        gm.Item.UnequipFromUnit(
                            it,
                            _inspectUnit
                        );
                    }
                }
                else
                {
                    GUILayout.Label($"• {it.itemName}");
                }
            }
        }

        if (_inspectUnit.equippedStone != null)
        {
            string stoneName =
                _inspectUnit.equippedStone.stoneName;

            if (isShopping)
            {
                if (GUILayout.Button(
                        $"해제: [돌]{stoneName}"))
                {
                    gm.Item.UnequipFromUnit(
                        _inspectUnit.equippedStone,
                        _inspectUnit
                    );
                }
            }
            else
            {
                GUILayout.Label($"• [돌] {stoneName}");
            }
        }

        if (!isShopping)
        {
            GUILayout.Space(4f);
            GUILayout.Label("전투 중에는 장비를 해제할 수 없습니다.");
        }

        if (GUILayout.Button("닫기"))
            _inspectUnit = null;

        GUILayout.EndArea();
    }

    /// <summary>
    /// 포인터 아래 모든 Collider를 검사하여 가장 가까운 PokemonUnit을 찾는다.
    /// 필드 타일 Collider가 먼저 맞아도 유닛 선택이 가능하다.
    /// </summary>
    private PokemonUnit RaycastUnit(Vector2 guiMousePos)
    {
        if (_camera == null)
            return null;

        Vector3 screenPos = new Vector3(
            guiMousePos.x,
            Screen.height - guiMousePos.y,
            0f
        );

        Ray ray = _camera.ScreenPointToRay(screenPos);

        RaycastHit[] hits =
            Physics.RaycastAll(
                ray,
                1000f,
                _raycastMask
            );

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit hit in hits)
        {
            PokemonUnit unit =
                hit.collider.GetComponentInParent<PokemonUnit>();

            if (unit != null)
                return unit;

            BattleManager battleManager =
                GameManager.Instance != null
                    ? GameManager.Instance.GetComponent<BattleManager>()
                    : null;

            if (battleManager != null)
            {
                PokemonUnit sourceUnit =
                    battleManager.GetSourceUnitFromVisual(
                        hit.collider.gameObject
                    );

                if (sourceUnit != null)
                    return sourceUnit;
            }
        }

        return null;
    }
}
