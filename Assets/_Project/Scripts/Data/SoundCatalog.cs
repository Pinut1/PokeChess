using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SoundId → AudioClip 매핑 전용 데이터. 재생 로직은 갖지 않는다(SoundManager가 담당).
/// Assets/Art/SFX 아래 수백 개 파일 중 실제로 쓸 대표 사운드만 골라 한 줄씩 등록하는 용도라,
/// 임포터가 전량을 채우는 Pokemon/Item류 DB와 달리 아트/기획이 인스펙터에서 직접 편집한다.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/Sound Catalog", fileName = "SoundCatalog")]
public class SoundCatalog : ScriptableObject
{
    [System.Serializable]
    public struct SoundEntry
    {
        public SoundId id;
        public AudioClip clip;
    }

    [SerializeField] private List<SoundEntry> _entries = new();

    private Dictionary<SoundId, AudioClip> _lookup;

    /// <summary>
    /// id로 클립 조회. None이거나 미등록이거나 clip이 비어있으면 실패(false)로 처리한다.
    /// 최초 조회 시점에 1회 Dictionary를 빌드해 캐싱한다 — ScriptableObject는 OnEnable 타이밍이
    /// 에디터/빌드에 따라 들쭉날쭉해 그쪽에 기대지 않고, 실제로 필요한 첫 호출에서 지연 생성한다.
    /// </summary>
    public bool TryGetClip(SoundId id, out AudioClip clip)
    {
        clip = null;
        if (id == SoundId.None) return false;

        EnsureLookup();

        return _lookup.TryGetValue(id, out clip) && clip != null;
    }

    private void EnsureLookup()
    {
        if (_lookup != null) return;

        _lookup = new Dictionary<SoundId, AudioClip>();

        if (_entries == null) return;

        foreach (var entry in _entries)
        {
            if (entry.id == SoundId.None) continue;

            if (_lookup.ContainsKey(entry.id))
                Debug.LogWarning($"[SoundCatalog] SoundId '{entry.id}' 중복 등록 — 나중 항목으로 덮어씀");

            _lookup[entry.id] = entry.clip; // 정책: 같은 id가 여러 번 등록되면 리스트 뒤쪽 항목이 최종값
        }
    }

    /// <summary>인스펙터에서 항목을 편집하면 다음 조회 때 다시 빌드하도록 캐시를 비운다.</summary>
    private void OnValidate() => _lookup = null;
}
