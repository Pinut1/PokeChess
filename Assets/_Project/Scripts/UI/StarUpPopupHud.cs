using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 성급 진화 시 유닛 머리 위(HP바보다 더 위)에 팝업을 띄우고 잠시 뒤 사라지게 한다.
/// GameEvents.OnUnitEvolved의 isStarUp이 true일 때만 반응한다 — 돌·통신교환 진화는 별이 오르지 않으므로 제외.
///
/// 여러 유닛이 연쇄 진화(2성 3개 → 3성)할 수 있어 팝업을 동시에 여러 개 띄울 수 있어야 한다.
/// 그래서 단일 인스턴스가 아니라 풀에서 꺼내 쓰고, 끝나면 반납한다(UnitStatusBarHud와 같은 방식).
/// </summary>
public class StarUpPopupHud : MonoBehaviour
{
    [Header("프리팹 / 부모")]
    [SerializeField] private StarUpPopupUI _popupPrefab;
    [Tooltip("팝업이 담길 부모. HP바와 같은 Canvas 아래면 된다.")]
    [SerializeField] private RectTransform _popupRoot;

    [Header("배치")]
    [Tooltip("비우면 Camera.main을 쓴다.")]
    [SerializeField] private Camera _camera;
    [Tooltip("유닛 발밑 기준 월드 높이. UnitStatusBarHud의 Height Offset보다 크게 잡아야 HP바 위에 뜬다.")]
    [SerializeField] private float _heightOffset = 2.8f;
    [Tooltip("떠 있는 동안 위로 밀려 올라가는 거리(화면 픽셀).")]
    [SerializeField] private float _riseDistance = 40f;

    [Header("타이밍")]
    [Tooltip("팝업이 화면에 머무는 총 시간(초).")]
    [SerializeField] private float _duration = 1.2f;
    [Tooltip("이 비율만큼은 완전히 불투명하게 유지하고, 남은 구간에서 서서히 사라진다(0~1).")]
    [Range(0f, 1f)]
    [SerializeField] private float _holdRatio = 0.6f;

    private sealed class Active
    {
        public StarUpPopupUI popup;
        public PokemonUnit unit;
        public float elapsed;
    }

    private readonly List<Active> _active = new();
    private readonly Stack<StarUpPopupUI> _pool = new();

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private void OnEnable()  => GameEvents.OnUnitEvolved += HandleEvolved;
    private void OnDisable() => GameEvents.OnUnitEvolved -= HandleEvolved;

    private void HandleEvolved(PokemonUnit unit, bool isStarUp)
    {
        if (!isStarUp || unit == null) return;
        if (_popupPrefab == null || _popupRoot == null) return;

        var popup = _pool.Count > 0 ? _pool.Pop() : Instantiate(_popupPrefab, _popupRoot);

        popup.SetVisible(true);
        popup.SetStar(unit.starLevel);
        popup.SetAlpha(1f);

        _active.Add(new Active { popup = popup, unit = unit, elapsed = 0f });
    }

    private void LateUpdate()
    {
        if (_camera == null || _active.Count == 0) return;

        float duration = Mathf.Max(0.01f, _duration);

        // 뒤에서부터 순회 — 도중에 끝난 항목을 바로 제거해도 인덱스가 밀리지 않는다.
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var a = _active[i];
            a.elapsed += Time.deltaTime;
            float t = a.elapsed / duration;

            // 유닛이 사라졌으면(합체로 소멸 등) 연출을 이어갈 기준점이 없다.
            if (t >= 1f || a.unit == null)
            {
                Release(i);
                continue;
            }

            Vector3 screenPos = _camera.WorldToScreenPoint(a.unit.transform.position + Vector3.up * _heightOffset);
            if (screenPos.z <= 0f)
            {
                a.popup.SetVisible(false); // 카메라 뒤 — 화면 반대편에 그려지는 것을 막는다
                continue;
            }

            a.popup.SetVisible(true);
            a.popup.Rect.position = screenPos + Vector3.up * (_riseDistance * t);
            a.popup.SetAlpha(FadeAlpha(t));
        }
    }

    /// <summary>_holdRatio 구간까지는 1, 이후 선형으로 0까지 내려간다.</summary>
    private float FadeAlpha(float t)
    {
        if (t <= _holdRatio) return 1f;

        float remain = 1f - _holdRatio;
        return remain <= 0f ? 0f : 1f - (t - _holdRatio) / remain;
    }

    private void Release(int index)
    {
        var a = _active[index];
        a.popup.SetVisible(false);
        _pool.Push(a.popup);
        _active.RemoveAt(index);
    }
}
