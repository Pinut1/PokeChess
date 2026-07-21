using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파트너(상대 클라이언트)의 보드를 읽기 전용 미러로 렌더하는 표현 레이어.
/// NetworkManager가 RPC로 받은 보드 스냅샷을 GameEvents.OnOpponentBoardChanged로 흘려주면,
/// 이를 받아 내 보드 옆(오프셋)에 그린다. 게임 로직 없음 — 순수 시각화.
///
/// speciesId를 PokemonDatabase로 해석해 modelPrefab이 있으면 실제 종 모델을,
/// 없거나 ID가 잘못됐으면 기존 캡슐 폴백(성급 색상)을 표시한다.
/// 미러는 표시 전용이므로 모델의 콜라이더는 전부 제거해 드래그 레이캐스트에 개입하지 않는다.
///
/// 위치는 내 BoardManager의 좌표 변환을 그대로 빌려 쓴다(같은 그리드 형상이므로).
/// 더블업처럼 쇼핑 중에도 파트너 판이 옆에 보이게 한다.
/// 규모가 작아(보드 ≤28 + 벤치 ≤9) 스냅샷마다 전체 재배치로 충분하며,
/// 비주얼 오브젝트는 종별 풀로 재사용해 스냅샷마다 Instantiate가 반복되지 않게 한다.
/// </summary>
public class OpponentBoardView : MonoBehaviour
{
    [Header("파트너 보드 표시 위치(월드 오프셋)")]
    [Tooltip("내 보드 기준 파트너 보드를 띄울 오프셋. BattleManager의 적 미러(Z+10)와 겹치지 않게.")]
    [SerializeField] private Vector3 _boardOffset = new Vector3(0f, 0f, 14f);
    [SerializeField] private float _unitHeight = 0.5f;

    [Header("성급 색상 (1/2/3성) — 캡슐 폴백 전용")]
    [SerializeField] private Color[] _starColors =
    {
        new Color(0.6f, 0.8f, 1f),  // 1성
        new Color(0.4f, 0.6f, 1f),  // 2성
        new Color(0.2f, 0.3f, 1f),  // 3성
    };

    /// <summary>캡슐 폴백을 종별 풀에 넣을 때 쓰는 키(실제 speciesId와 충돌하지 않는 음수).</summary>
    private const int FALLBACK_POOL_KEY = -1;

    /// <summary>종별 비활성 비주얼 풀. 키 = speciesId(모델) 또는 FALLBACK_POOL_KEY(캡슐).</summary>
    private readonly Dictionary<int, Stack<GameObject>> _poolBySpecies = new();

    /// <summary>현재 스냅샷으로 활성화된 비주얼과 풀 키.</summary>
    private readonly List<(int poolKey, GameObject go)> _active = new();

    /// <summary>해석 실패를 이미 경고한 speciesId — 스냅샷마다 반복 경고(스팸) 방지.</summary>
    private readonly HashSet<int> _warnedSpecies = new();

    private void OnEnable()  => GameEvents.OnOpponentBoardChanged += Render;
    private void OnDisable() => GameEvents.OnOpponentBoardChanged -= Render;

    private void Render(BoardSnapshot snap)
    {
        var board = GameManager.Instance != null ? GameManager.Instance.Board : null;

        // 활성분 전부 풀로 반환 후 스냅샷 기준으로 다시 배치.
        ReleaseActive();
        if (board == null || snap == null) return;

        foreach (BoardSnapshot.Entry e in snap.entries)
        {
            Vector3 basePos = e.onBoard
                ? board.CoordsToWorldPosition(new HexCoords(e.a, e.b))
                : board.BenchSlotWorldPosition(e.a);

            PokemonData data = ResolveSpecies(e.speciesId);
            int poolKey = data != null && data.modelPrefab != null ? e.speciesId : FALLBACK_POOL_KEY;

            GameObject go = RentVisual(poolKey, data);
            go.SetActive(true);
            go.transform.position = basePos + _boardOffset + Vector3.up * _unitHeight;

            float scale = 0.6f + (e.starLevel - 1) * 0.15f; // 성급 높을수록 약간 크게
            go.transform.localScale = Vector3.one * scale;

            if (poolKey == FALLBACK_POOL_KEY)
            {
                // 캡슐 폴백만 성급 색상 표시(모델 머티리얼은 건드리지 않음).
                go.transform.localScale = new Vector3(scale, 0.5f, scale);
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.material.color = StarColor(e.starLevel);
            }

            string speciesName = data != null ? data.pokemonNameEn : e.speciesId.ToString();
            go.name = $"PartnerUnit_{speciesName}_{e.starLevel}star";

            _active.Add((poolKey, go));
        }
    }

    /// <summary>speciesId → PokemonData 해석. 실패 시 null(캡슐 폴백) + 종당 1회만 경고.</summary>
    private PokemonData ResolveSpecies(int speciesId)
    {
        var db = PokemonDatabase.Instance;
        PokemonData data = db != null ? db.GetById(speciesId) : null;

        if (data == null && _warnedSpecies.Add(speciesId))
            Debug.LogWarning($"[OpponentBoardView] speciesId {speciesId} 해석 실패 — 캡슐 폴백 (PokemonDatabase 미임포트?)");

        return data;
    }

    /// <summary>종별 풀에서 비주얼을 꺼내거나 새로 만든다. 미러 전용이라 콜라이더는 전부 제거.</summary>
    private GameObject RentVisual(int poolKey, PokemonData data)
    {
        if (_poolBySpecies.TryGetValue(poolKey, out var stack) && stack.Count > 0)
            return stack.Pop();

        GameObject go;
        if (poolKey != FALLBACK_POOL_KEY)
        {
            go = Instantiate(data.modelPrefab, transform);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(transform);
        }

        // 미러는 표시 전용 — 드래그 레이캐스트에 안 걸리도록 콜라이더 제거.
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            Destroy(col);

        return go;
    }

    /// <summary>활성 비주얼을 전부 비활성화하고 각자의 종별 풀로 반환.</summary>
    private void ReleaseActive()
    {
        foreach ((int poolKey, GameObject go) in _active)
        {
            if (go == null) continue;
            go.SetActive(false);

            if (!_poolBySpecies.TryGetValue(poolKey, out var stack))
            {
                stack = new Stack<GameObject>();
                _poolBySpecies[poolKey] = stack;
            }
            stack.Push(go);
        }
        _active.Clear();
    }

    private Color StarColor(int star)
    {
        if (_starColors == null || _starColors.Length == 0) return Color.cyan;
        int idx = Mathf.Clamp(star - 1, 0, _starColors.Length - 1);
        return _starColors[idx];
    }
}
