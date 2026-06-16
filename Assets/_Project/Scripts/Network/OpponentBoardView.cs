using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파트너(상대 클라이언트)의 보드를 읽기 전용 미러로 렌더하는 표현 레이어.
/// NetworkManager가 RPC로 받은 보드 스냅샷을 GameEvents.OnOpponentBoardChanged로 흘려주면,
/// 이를 받아 내 보드 옆(오프셋)에 캡슐로 그린다. 게임 로직 없음 — 순수 시각화.
///
/// 위치는 내 BoardManager의 좌표 변환을 그대로 빌려 쓴다(같은 그리드 형상이므로).
/// 더블업처럼 쇼핑 중에도 파트너 판이 옆에 보이게 한다.
/// 규모가 작아(보드 ≤28 + 벤치 ≤9) 스냅샷마다 전체 재생성으로 충분하다.
/// </summary>
public class OpponentBoardView : MonoBehaviour
{
    [Header("파트너 보드 표시 위치(월드 오프셋)")]
    [Tooltip("내 보드 기준 파트너 보드를 띄울 오프셋. BattleManager의 적 미러(Z+10)와 겹치지 않게.")]
    [SerializeField] private Vector3 _boardOffset = new Vector3(0f, 0f, 14f);
    [SerializeField] private float _unitHeight = 0.5f;

    [Header("성급 색상 (1/2/3성)")]
    [SerializeField] private Color[] _starColors =
    {
        new Color(0.6f, 0.8f, 1f),  // 1성
        new Color(0.4f, 0.6f, 1f),  // 2성
        new Color(0.2f, 0.3f, 1f),  // 3성
    };

    private readonly List<GameObject> _pool = new();

    private void OnEnable()  => GameEvents.OnOpponentBoardChanged += Render;
    private void OnDisable() => GameEvents.OnOpponentBoardChanged -= Render;

    private void Render(BoardSnapshot snap)
    {
        var board = GameManager.Instance != null ? GameManager.Instance.Board : null;
        if (board == null || snap == null) { ClearPool(); return; }

        EnsurePool(snap.entries.Count);

        for (int i = 0; i < _pool.Count; i++)
        {
            if (i >= snap.entries.Count)
            {
                _pool[i].SetActive(false);
                continue;
            }

            BoardSnapshot.Entry e = snap.entries[i];

            Vector3 basePos = e.onBoard
                ? board.CoordsToWorldPosition(new HexCoords(e.a, e.b))
                : board.BenchSlotWorldPosition(e.a);

            GameObject go = _pool[i];
            go.SetActive(true);
            go.transform.position = basePos + _boardOffset + Vector3.up * _unitHeight;

            float scale = 0.6f + (e.starLevel - 1) * 0.15f; // 성급 높을수록 약간 크게
            go.transform.localScale = new Vector3(scale, 0.5f, scale);

            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.material.color = StarColor(e.starLevel);

            go.name = $"PartnerUnit_{e.speciesId}_{e.starLevel}star";
        }
    }

    private Color StarColor(int star)
    {
        if (_starColors == null || _starColors.Length == 0) return Color.cyan;
        int idx = Mathf.Clamp(star - 1, 0, _starColors.Length - 1);
        return _starColors[idx];
    }

    private void EnsurePool(int needed)
    {
        while (_pool.Count < needed)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(transform);
            // 미러는 표시 전용 — 드래그 레이캐스트에 안 걸리도록 콜라이더 제거.
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _pool.Add(go);
        }
    }

    private void ClearPool()
    {
        foreach (var go in _pool) if (go != null) go.SetActive(false);
    }
}
