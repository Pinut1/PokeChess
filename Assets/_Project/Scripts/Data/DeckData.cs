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
