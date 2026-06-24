#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 통신교환 전송 디버그. 드래그/통신기 UI 없이 벤치 유닛을 파트너에게 전송 테스트.
/// 버튼 클릭 → NetworkManager.SendTradeUnit. 받는 쪽은 매핑되면 진화체로 벤치 직행.
/// </summary>
public class TradeDebugTest : MonoBehaviour
{
    private void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Board == null || gm.Network == null) return;

        var bench = gm.Board.GetBenchSnapshot();
        float x = 10f, y = 460f;   // 좌측(ItemInventoryHud 인벤토리 패널 아래로 내림 — 최대 5행까지 자라도 안 겹치게)
        GUI.Label(new Rect(x, y, 300, 22), "통신교환(클릭 = 파트너 전송):");
        y += 24f;
        for (int i = 0; i < bench.Count; i++)
        {
            var u = bench[i];
            if (u == null || u.data == null) continue;
            if (GUI.Button(new Rect(x, y, 280, 24), $"전송: 슬롯{i} {u.data.pokemonName} ★{u.starLevel}"))
                gm.Network.SendTradeUnit(u);
            y += 26f;
        }
    }
}
#endif
