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

    /// <summary>
    /// true면 진화 계열을 묶지 않고 <b>종(도감번호)별로</b> 카운트한다.
    /// 돌연변이(Mutant) 전용 — 이브이 진화체를 모으는 것 자체가 시너지 설계라
    /// 계열로 묶으면 최대 1카운트가 되어 시너지가 성립하지 않는다.
    /// 기본값 false = 진화 계열(3합체·진화의돌·통신교환) 단위 카운트.
    /// </summary>
    public bool countPerSpecies;

    [Header("발동 조건별 효과")]
    public List<SynergyTier> tiers = new();

    [Header("에셋 참조")]
    public Sprite icon;
}
