using UnityEngine;

/// <summary>
/// 보드/벤치 위의 포켓몬 유닛 런타임 상태.
/// 스탯 원본은 PokemonData(ScriptableObject)에 있고,
/// 이 클래스는 실제 전투 중 변하는 값만 들고 있음.
/// </summary>
public class PokemonUnit : MonoBehaviour
{
    [Header("데이터")]
    public PokemonData data;

    [Header("런타임 스탯")]
    public float currentHp;
    public float currentMana;
    public int starLevel = 1;       // 1~3성

    [Header("상태")]
    public bool isOnBoard;          // false = 벤치

    private void Start()
    {
        if (data != null)
            currentHp = data.hp;
    }

    // TODO: starLevel에 따른 스탯 스케일링 — 기획 확정 후 구현
}
