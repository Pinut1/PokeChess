using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 전체 스테이지를 List 하나로 인라인 보유하는 중앙 레지스트리(SO 1개).
/// 스테이지당 .asset을 쪼개던 방식을 대체 — 파일/머지 노이즈 제거, 인스펙터 일괄 조망.
/// (적 참조가 영문명 문자열이라 개별 SO 객체참조가 필요 없어 인라인이 가능.)
///
/// 채우기: PokeChessImporter "Import Stage JSON"이 stages를 자동 갱신 → 손으로 편집 비권장.
/// 읽기:  런타임은 StageDatabase.Instance.GetForRound(round) 로 접근.
/// 일관성: 중앙 [[PokemonDatabase]]와 동일 패턴.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/StageDatabase", fileName = "StageDatabase")]
public class StageDatabase : ScriptableObject
{
    [Tooltip("임포터(Import Stage JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<StageData> stages = new();

    /// <summary>
    /// 챕터 최종 라운드 번호. 완주(Victory) 판정용 — RoundPhaseManager가 CurrentRound와 비교.
    /// GetForRound는 stages가 비지 않으면 항상 클램프해 절대 null을 안 주므로, 이 값으로 끝을 안다.
    /// 스테이지가 없으면 0.
    /// </summary>
    public int LastRound => (stages == null || stages.Count == 0)
        ? 0
        : stages.Max(s => Mathf.Max(s.order, s.round));

    // 런타임 단일 접근. Resources 루트에 단 하나의 DB 에셋만 두면 됨.
    private static StageDatabase _instance;
    public static StageDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<StageDatabase>("StageDatabase");
            if (_instance == null)
                Debug.LogError("[StageDatabase] Resources/StageDatabase.asset 없음 — 'Import Stage JSON' 먼저 실행");
            return _instance;
        }
    }

    /// <summary>
    /// 현재 라운드에 해당하는 스테이지 선택. order==round → round==round → 인덱스 클램프 순.
    /// (기존 BattleManager.SelectStage 로직을 그대로 이관.) 없으면 null.
    /// </summary>
    public StageData GetForRound(int round)
    {
        if (stages == null || stages.Count == 0) return null;

        foreach (var s in stages)
            if (s != null && s.order == round) return s;
        foreach (var s in stages)
            if (s != null && s.round == round) return s;

        int idx = Mathf.Clamp(round - 1, 0, stages.Count - 1);
        return stages[idx];
    }
}
