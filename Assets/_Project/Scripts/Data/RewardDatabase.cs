using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전체 보상 테이블을 List 하나로 인라인 보유하는 중앙 레지스트리(SO 1개).
/// Reward_{id}.asset을 쪼개던 방식을 대체 — 파일/머지 노이즈 제거 + 런타임 단일 로드.
///
/// 채우기: PokeChessImporter "Import Reward JSON"이 tables를 자동 갱신 → 손으로 편집 비권장.
/// 읽기:  런타임은 RewardDatabase.Instance.GetByTableId(id) 로 접근.
/// 일관성: 중앙 [[StageDatabase]] / [[PokemonDatabase]]와 동일 패턴.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/RewardDatabase", fileName = "RewardDatabase")]
public class RewardDatabase : ScriptableObject
{
    [Tooltip("임포터(Import Reward JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<RewardData> tables = new();

    private static RewardDatabase _instance;
    public static RewardDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<RewardDatabase>("RewardDatabase");
            if (_instance == null)
                Debug.LogError("[RewardDatabase] Resources/RewardDatabase.asset 없음 — 'Import Reward JSON' 먼저 실행");
            return _instance;
        }
    }

    /// <summary>rewardTableId("RW001" 등)로 보상 테이블 조회. 대소문자 무시. 없으면 null.</summary>
    public RewardData GetByTableId(string rewardTableId)
    {
        if (tables == null || string.IsNullOrEmpty(rewardTableId)) return null;
        foreach (var t in tables)
            if (t != null && string.Equals(t.rewardTableId, rewardTableId, StringComparison.OrdinalIgnoreCase))
                return t;
        return null;
    }
}
