using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemStatModifier
{
    public string key;
    public float value;
    public bool isPercent;
    public bool hasValue = true;
}

[CreateAssetMenu(menuName = "PokeChess/Item", fileName = "NewItem_Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;
    public string itemName;
    public string itemNameEn;

    [TextArea]
    public string description;

    [Header("원본 스탯 (GeneralItem 시트)")]
    public List<ItemStatModifier> sourceStats = new();

    [Header("스탯 효과")]
    public float hpBonus;               // hp
    public float maxHpPct;              // maxHpPct
    public float hpRegenPercent;        // hpRegenPercent — 매 초 최대 HP의 N% 회복
    public float healTakenDmgPct;       // healTakenDmgPct — 피격 피해의 N% 흡수
    public float shieldPctOnFatalHit;   // shieldPctOnFatalHit — 치명적 피해 시 최대HP N% 보호막

    public float attackBonus;           // atk
    public float spAtkPct;              // spAtkPct
    public float attackSpeedBonus;      // atkSpdPct
    public float moveSpdPctOnKill;      // moveSpdPctOnKill — 적 처치 시 이동속도 N%

    public float defenseBonus;          // def
    public float spDefBonus;            // spDef
    public float reflectPhysPct;        // reflectPhysPct — 물리 피해 N% 반사
    public float reflectSpPct;          // reflectSpPct — 특수 피해 N% 반사
    public float defSpDefPerAttacker;   // defSpDefPerAttacker — 공격 중인 적 1명당 방/특방 +N

    public float criPct;                // criPct — 치명타율 N%
    public float criDmgPct;             // criDmgPct — 치명타 피해 N%

    public bool  burnNearOnPhysHit;     // burnNearOnPhysHit — 물리 피격 시 주변 적 화상
    public bool  ccImmune;              // ccImmune — 첫 CC 무효화

    [Header("에셋 참조")]
    public Sprite icon;
}
