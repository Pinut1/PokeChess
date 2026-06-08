using UnityEngine;

// MOCKUP: consumableType은 기획 확정 후 enum으로 교체 예정
[CreateAssetMenu(menuName = "PokeChess/Consumable", fileName = "NewConsumable_Data")]
public class ConsumableData : ScriptableObject
{
    [Header("기본 정보")]
    public int id;
    public string consumableName;
    public string consumableNameEn;
    public string consumableType;   // ex) "duplicate_unit", "reforge_item"

    [TextArea]
    public string description;

    [Header("에셋 참조")]
    public Sprite icon;
}
