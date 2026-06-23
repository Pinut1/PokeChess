using UnityEngine;

/// <summary>
/// 아이템/진화의 돌 장착·조합 담당. 김태욱 파트.
///
/// 장착 단일 입구 = EquipToUnit (드래그&드롭 UI가 호출). 저수준 장착 로직은 PokemonUnit에 이미 있음
/// (TryEquipItem/TryEquipStone). 인벤토리(보관)·아이템샵·스탯 조합은 태욱이 채운다.
/// </summary>
public class ItemManager : MonoBehaviour
{
    // TODO(태욱): 인벤토리 보관 — 획득(reward/아이템샵)시 추가, 장착 성공 시 제거.
    //   private readonly List<ItemData> _items = new();
    //   private readonly List<EvolutionStoneData> _stones = new();
    //   public void AddItem(ItemData item) / AddStone(EvolutionStoneData stone)

    /// <summary>
    /// 장착 단일 입구. 드래그&드롭 UI(황해인)가 인벤토리 항목을 유닛에 드롭할 때 호출.
    /// 돌/일반 아이템을 타입으로 분기해 PokemonUnit 저수준 장착으로 라우팅하고 성공 여부를 반환한다.
    /// 슬롯 규칙(2칸, 돌+템/템+템 가능·돌+돌 불가)은 PokemonUnit이 이미 강제한다.
    ///
    /// 성공 시 호출측(UI/ItemManager 인벤)이 인벤토리에서 제거, 실패 시 원위치시킨다.
    /// (인벤토리 연결은 태욱 — 지금은 장착 라우팅 계약만 확정.)
    /// </summary>
    public bool EquipToUnit(ScriptableObject equippable, PokemonUnit unit)
    {
        if (unit == null || equippable == null) return false;

        switch (equippable)
        {
            case EvolutionStoneData stone:
                return unit.TryEquipStone(stone);   // 장착 시 data를 진화체로 스왑(되돌릴 수 있음)
            case ItemData item:
                return unit.TryEquipItem(item);      // 일반 장착템(스탯 효과 적용은 BattleManager TODO)
            default:
                Debug.LogWarning($"[Item] 장착 불가 타입: {equippable.GetType().Name}");
                return false;
        }
    }

    /// <summary>
    /// 장착 해제 단일 입구(되돌리기). 돌은 베이스로 원복, 일반템은 제거. 반환물의 인벤 복귀는 호출측(태욱) 몫.
    /// </summary>
    public ScriptableObject UnequipStone(PokemonUnit unit)
        => unit != null ? unit.RemoveStone() : null;
}
