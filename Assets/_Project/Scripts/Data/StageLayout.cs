/// <summary>
/// 스키마 v2의 적 배치 slot(1~6)을 헥스 좌표(q/r)로 변환하는 단일 진실 출처.
///
/// 기획 시트(Trainer Entry)는 slot만 채우고, 개발이 고정 배치도로 좌표를 부여한다.
/// 3시트 join 임포터(태욱)가 EnemyPlacement.q/r을 채울 때 이 헬퍼를 호출한다.
/// 보스처럼 q/r을 직접 지정하는 경우엔 이 헬퍼를 쓰지 않고 입력값을 그대로 override.
///
/// 좌표 규약(적 로컬 프레임 — BattleManager가 플레이어 앞으로 미러링):
///   q = 열(0~2), r = 행(0=앞열, 1=뒷열)
///
///   배치도(SCHEMA_2026-06-19_stage-data-v2.md):
///     [4] [5] [6]  ← 뒷열 (slot 4~6, r=1)
///     [1] [2] [3]  ← 앞열 (slot 1~3, r=0)
/// </summary>
public static class StageLayout
{
    public const int MinSlot = 1;
    public const int MaxSlot = 6;

    /// <summary>slot(1~6) → 헥스 좌표. 범위를 벗어나면 (0,0)으로 보정하고 경고.</summary>
    public static HexCoords SlotToHex(int slot)
    {
        if (slot < MinSlot || slot > MaxSlot)
        {
            UnityEngine.Debug.LogWarning($"[StageLayout] slot 범위 초과({slot}) — 1~6만 허용. (0,0)으로 처리.");
            return new HexCoords(0, 0);
        }

        int index = slot - 1;   // 0~5
        int q = index % 3;      // 열: 0,1,2
        int r = index / 3;      // 행: 0(앞열), 1(뒷열)
        return new HexCoords(q, r);
    }
}
