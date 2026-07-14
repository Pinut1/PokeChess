using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 견본덱 유닛 1기 — Pokemon DB id 참조 + 목표 성급.
/// </summary>
[Serializable]
public class DeckUnitEntry
{
    public int pokemonId;   // PokemonDatabase id 참조
    public int starLevel;   // 목표 성급
    public int slot;        // 시트 순서 (표시 정렬용)
}

/// <summary>
/// 견본덱 1개 — 플레이어 가이드(목표 조합 표시)용 데이터.
/// AI 상대 덱이 아님 — 라운드 합류/배치 정보는 싣지 않는다 (시트 원본에만 보존).
/// </summary>
[Serializable]
public class DeckData
{
    public int deckId;
    public string deckName;
    public int unitCount;
    public List<string> activeSynergies = new();  // "Bug(4/4)" 형식 표시 문자열
    public int totalGoldToBuild;
    public List<DeckUnitEntry> units = new();
}

/// <summary>
/// 전체 견본덱을 List 하나로 인라인 보유하는 중앙 레지스트리(SO 1개).
///
/// 채우기: PokeChessImporter "Import Deck JSON"이 decks를 자동 갱신 → 손으로 편집 비권장.
/// 읽기:  런타임은 DeckDatabase.Instance.GetByDeckId(id) 로 접근.
/// 일관성: 중앙 [[RewardDatabase]] / [[StageDatabase]]와 동일 패턴.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/DeckDatabase", fileName = "DeckDatabase")]
public class DeckDatabase : ScriptableDatabase<DeckDatabase>
{
    [Tooltip("임포터(Import Deck JSON)가 자동으로 채움. 수동 편집 비권장.")]
    public List<DeckData> decks = new();

    /// <summary>deckId로 견본덱 조회. 없으면 null.</summary>
    public DeckData GetByDeckId(int deckId)
    {
        if (decks == null) return null;
        foreach (var d in decks)
            if (d != null && d.deckId == deckId)
                return d;
        return null;
    }
}
