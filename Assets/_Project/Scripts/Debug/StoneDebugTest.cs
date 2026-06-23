#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진화의 돌 장착/해제 디버그. 인벤토리/드래그 UI 없이 벤치 유닛에 돌을 장착/해제 테스트.
/// 인스펙터에 'Import EvolutionStone JSON'으로 생성된 스톤 SO들을 할당해두고 버튼으로 장착.
/// 장착=data를 진화체로 스왑(PokemonUnit.TryEquipStone), 해제=베이스로 원복(RemoveStone).
/// </summary>
public class StoneDebugTest : MonoBehaviour
{
    [Tooltip("ScriptableObjects/EvolutionStones/*_Stone.asset 들을 할당")]
    [SerializeField] private List<EvolutionStoneData> _stones = new();

    private void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Board == null) return;

        var bench = gm.Board.GetBenchSnapshot();
        float baseX = 10f, y = 360f;   // 좌측(통신교환 버튼 아래)
        GUI.Label(new Rect(baseX, y, 360, 22), "진화의 돌(장착/해제):");
        y += 24f;

        for (int i = 0; i < bench.Count; i++)
        {
            var u = bench[i];
            if (u == null || u.data == null) continue;

            GUI.Label(new Rect(baseX, y, 360, 22),
                $"슬롯{i}: {u.data.pokemonName}{(u.IsStoneEvolved ? " (돌진화중)" : "")}");
            y += 22f;

            if (u.IsStoneEvolved)
            {
                if (GUI.Button(new Rect(baseX + 10, y, 200, 24), "돌 해제(원복)"))
                    u.RemoveStone();
                y += 26f;
            }
            else
            {
                float x = baseX + 10;
                foreach (var stone in _stones)
                {
                    if (stone == null) continue;
                    if (GUI.Button(new Rect(x, y, 90, 24), stone.stoneName))
                        u.TryEquipStone(stone); // 대상 아니면 내부에서 실패 로그
                    x += 94f;
                }
                y += 26f;
            }
        }
    }
}
#endif
