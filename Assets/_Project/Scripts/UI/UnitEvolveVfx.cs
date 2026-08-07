using UnityEngine;

/// <summary>
/// 유닛 진화 시 이펙트를 재생하는 뷰 전용 컴포넌트.
/// 성급 합체·진화의 돌·통신교환 모두 같은 이펙트를 쓴다(GameEvents.OnUnitEvolved 한 곳으로 모임).
///
/// 진화는 상점 단계에서 일어나 BattleUnit이 없으므로, 좌표 기반인 BattleVfxPlayer.PlayAt을 쓴다.
/// VFX는 결과에 영향을 주지 않는 순수 연출이라 네트워크 동기화 없이 각 클라이언트가 자기 화면에서만 재생한다.
/// </summary>
public class UnitEvolveVfx : MonoBehaviour
{
    [Tooltip("VfxDatabase에 등록된 진화 이펙트 id. 비우면 아무것도 재생하지 않는다.")]
    [SerializeField] private string _vfxId = "VFX_Evolve";

    [Tooltip("유닛 위치 기준 오프셋. 발밑에서 올라오는 연출이면 0, 몸통 중앙이면 살짝 올린다.")]
    [SerializeField] private Vector3 _offset = new(0f, 0.5f, 0f);

    [Tooltip("성급 합체(2·3성)일 때만 배율을 키워 더 크게 보이게 한다. 1이면 동일 크기.")]
    [SerializeField] private float _starUpScale = 1.3f;

    private void OnEnable()  => GameEvents.OnUnitEvolved += HandleEvolved;
    private void OnDisable() => GameEvents.OnUnitEvolved -= HandleEvolved;

    private void HandleEvolved(PokemonUnit unit, bool isStarUp)
    {
        if (unit == null || string.IsNullOrEmpty(_vfxId)) return;

        float scale = isStarUp ? Mathf.Max(0.01f, _starUpScale) : 1f;
        BattleVfxPlayer.PlayAt(_vfxId, unit.transform.position + _offset, scale);

        if (isStarUp && SoundManager.TryGet(out var sm))
        {
            SoundId sfx = unit.starLevel >= 3 ? SoundId.Evolution3Star : SoundId.Evolution2Star;
            sm.PlaySfx(sfx);
        }
    }
}
