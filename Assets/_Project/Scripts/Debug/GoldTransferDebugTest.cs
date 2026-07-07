#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 통신기 골드 전송 디버그. 통신기 UI 없이 파트너 송금 테스트.
/// 버튼 클릭 → NetworkManager.SendGoldToPartner. 결과 피드백은 GameEvents 구독으로 표시.
/// </summary>
public class GoldTransferDebugTest : MonoBehaviour
{
    private static readonly int[] AMOUNTS = { 1, 5, 10 };

    private string _lastResult = "";

    private void OnEnable()
    {
        GameEvents.OnGoldTransferCompleted += HandleCompleted;
        GameEvents.OnGoldTransferRejected  += HandleRejected;
        GameEvents.OnPartnerGoldReceived   += HandleReceived;
    }

    private void OnDisable()
    {
        GameEvents.OnGoldTransferCompleted -= HandleCompleted;
        GameEvents.OnGoldTransferRejected  -= HandleRejected;
        GameEvents.OnPartnerGoldReceived   -= HandleReceived;
    }

    private void HandleCompleted(int amount)  => _lastResult = $"전송 완료: {amount}G";
    private void HandleRejected(string reason) => _lastResult = $"전송 실패: {reason}";
    private void HandleReceived(int amount)   => _lastResult = $"수신: 파트너에게서 {amount}G";

    private void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Network == null || gm.Shop == null) return;

        float x = 300f, y = 460f;   // TradeDebugTest(좌측 x=10) 오른쪽
        GUI.Label(new Rect(x, y, 280, 22), $"골드 전송(보유 {gm.Shop.Gold}G):");
        y += 24f;
        foreach (int amount in AMOUNTS)
        {
            if (GUI.Button(new Rect(x, y, 130, 24), $"파트너에게 {amount}G"))
                gm.Network.SendGoldToPartner(amount);
            y += 26f;
        }
        if (!string.IsNullOrEmpty(_lastResult))
            GUI.Label(new Rect(x, y, 280, 22), _lastResult);
    }
}
#endif
