using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SynergyTier
{
    public int count;           // 발동에 필요한 유닛 수
    [TextArea]
    public string effectDescription;
}

[CreateAssetMenu(menuName = "PokeChess/Synergy", fileName = "NewSynergy_Data")]
public class SynergyData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;
    public string synergyName;      // 한글 (예: "전기")
    public string synergyNameEn;    // 영문 (예: "Electric")

    [Header("발동 조건별 효과")]
    public List<SynergyTier> tiers = new();

    [Header("에셋 참조")]
    public Sprite icon;
}
