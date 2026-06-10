using UnityEngine;

/// <summary>
/// 마우스 드래그 앤 드롭을 받아낼 수 있는 모든 '과녁판(타일, 상점 등)'의 공통 규약.
/// PlayerController가 레이캐스트를 쏠 때 이 인터페이스를 찾아 상호작용합니다.
/// </summary>
public interface IDropTarget
{
    /// <summary>
    /// 들고 있던 유닛을 이 타겟 위에 놓았을 때 호출됩니다.
    /// </summary>
    /// <param name="unit">떨어뜨린 포켓몬 유닛</param>
    void OnDropUnit(PokemonUnit unit);

    /// <summary>
    /// 피카츄를 들고 타겟 위로 마우스가 올라왔을 때 호출됩니다. (색상 변경 등 시각 효과용)
    /// </summary>
    void OnHoverEnter();

    /// <summary>
    /// 마우스가 타겟 밖으로 빠져나갔을 때 호출됩니다. (시각 효과 복구용)
    /// </summary>
    void OnHoverExit();
}
