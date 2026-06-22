#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 샵/경제/합체 루프 플레이모드 검증용. 드래그 미연동 환경에서도 구매·리롤·합체를 버튼으로 확인.
/// 샵 풀이 비어있으면(에셋 미할당) 런타임 테스트 포켓몬을 시드한다.
/// 같은 종을 3번 구매하면 벤치에서 자동 별업되는지 확인할 수 있다.
/// </summary>
public class ShopDebugTest : MonoBehaviour
{
    [SerializeField] private bool _seedTestPoolIfEmpty = true;

    // OnGUI 도중 판매하면 컨트롤 수가 바뀌어 IMGUI 경고가 나므로, 다음 프레임으로 미룬다.
    private PokemonUnit _pendingSell;

    private void Update()
    {
        if (_pendingSell != null)
        {
            GameManager.Instance.Board.SellUnit(_pendingSell);
            _pendingSell = null;
        }
    }

    private void Start()
    {
        if (!_seedTestPoolIfEmpty) return;

        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        if (shop == null || shop.CurrentSlots == null) return;

        bool empty = true;
        foreach (var s in shop.CurrentSlots) if (s != null) { empty = false; break; }
        if (empty)
        {
            shop.SetPool(BuildTestPool());
            shop.Roll();
        }
    }

    private static List<PokemonData> BuildTestPool()
    {
        var pool = new List<PokemonData>();
        string[] names = { "피카", "꼬부", "파이리" };
        for (int i = 0; i < names.Length; i++)
        {
            var d = ScriptableObject.CreateInstance<PokemonData>();
            d.id = 8001 + i;
            d.pokemonName = names[i];
            d.pokemonNameEn = $"Test{i}";
            d.cost = 1;
            d.hp = 100; d.attack = 12; d.attackSpeed = 0.7f; d.range = 1;
            d.synergies = new List<string>();
            pool.Add(d);
        }
        return pool;
    }

    private void OnGUI()
    {
        var shop = GameManager.Instance != null ? GameManager.Instance.Shop : null;
        if (shop == null) return;

        GUI.Label(new Rect(220, 10, 200, 24), $"골드: {shop.Gold}");

        var slots = shop.CurrentSlots;
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                PokemonData data = slots[i];
                string label = data != null ? $"{data.pokemonName} ({data.cost}G)" : "(빈 슬롯)";
                if (GUI.Button(new Rect(220 + i * 110, 40, 105, 40), label) && data != null)
                    shop.Buy(i);
            }
        }

        if (GUI.Button(new Rect(220, 90, 105, 30), "리롤 (2G)"))
            shop.Reroll();

        // 벤치 유닛 판매 (드래그 SellZone 없이도 테스트) — 클릭하면 해당 유닛 판매 + 환급
        var board = GameManager.Instance.Board;
        if (board != null)
        {
            var bench = board.GetBenchSnapshot();
            float y = 130f;
            GUI.Label(new Rect(220, y, 360, 22), "벤치 (버튼 클릭 = 판매):");
            y += 24f;
            for (int i = 0; i < bench.Count; i++)
            {
                PokemonUnit u = bench[i];
                if (u == null || u.data == null) continue;
                string lbl = $"슬롯{i}: {u.data.pokemonName} ★{u.starLevel} (판매 {shop.SellValue(u)}G)";
                if (GUI.Button(new Rect(220, y, 340, 26), lbl))
                    _pendingSell = u; // 실제 판매는 Update에서(OnGUI 컨트롤 수 변동 방지)
                y += 28f;
            }
        }
    }
}
#endif
