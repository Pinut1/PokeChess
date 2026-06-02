using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory
{
    Ingredient,  // 재료 열매
    Result       // 조합 결과
}

[CreateAssetMenu(menuName = "PokeChess/Item", fileName = "NewItem_Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;
    public string itemName;
    public string itemNameEn;
    public ItemCategory category;

    [TextArea]
    public string description;

    [Header("조합")]
    /// <summary>조합 재료 이름 목록. 재료 열매는 빈 리스트.</summary>
    public List<string> recipe = new();

    [Header("스탯 효과")]
    // TODO: 아이템 효과 확정 후 필드 추가
    // 예시 필드들 (필요한 것만 남기고 나머지 삭제)
    public float hpBonus;
    public float attackBonus;
    public float defenseBonus;
    public float attackSpeedBonus;
    public float hpRegenPercent;    // 매 초 최대 HP의 N% 회복

    [Header("에셋 참조")]
    public Sprite icon;
}
