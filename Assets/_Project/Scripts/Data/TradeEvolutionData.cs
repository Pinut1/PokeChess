using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 통신교환 진화 매핑 한 줄. (예: 강철톤 → 강철톤 진화체)
/// 파트너에게 전송되는 즉시 진화하며 취소 불가. JSON은 "어떤 종이 통신진화 대상인가"의
/// 분류만 담고, 실제 진화 트리거/스폰은 코드(NetworkManager 전송 트랜잭션)에서 제어한다.
/// </summary>
[Serializable]
public class TradeEvolutionMapping
{
    public string targetPokemonEn;   // 전송 전 포켓몬 영문명
    public string evolvedPokemonEn;  // 전송 즉시 진화 후 영문명
    public bool affectsField;         // 수신자 보드의 동일 원본도 즉시 교체
    public bool affectsBench;         // 수신자 벤치의 동일 원본도 즉시 교체
    public bool affectsShop;          // 현재 슬롯 + 이후 롤을 진화체로 투영
    public bool isReversible;         // 현재 확정 사양은 false(매치 종료까지 영구)
    [TextArea]
    public string note;              // 가독용 메모 (게임 로직 미사용)
}

/// <summary>
/// 통신교환(전송) 진화 매핑 모음. 진화의 돌(EvolutionStoneData)·일반 진화
/// (PokemonData.evolvesIntoEn)와 분리된 세 번째 진화 루트.
/// 수신 즉시 받은 유닛과 수신자의 보드·벤치·현재/이후 상점 동일 원본 종을
/// 같은 성급의 진화체로 영구 교체한다. 풀/가격 계산은 원본 종 기준을 유지한다.
/// 런타임은 A 클라가 자기 권위로 GetEvolved(base) 조회 후 진화체를 인스턴스화한다.
/// </summary>
[CreateAssetMenu(menuName = "PokeChess/TradeEvolution", fileName = "TradeEvolution_Data")]
public class TradeEvolutionData : ScriptableObject
{
    [Header("통신진화 매핑")]
    public List<TradeEvolutionMapping> mappings = new();

    // 런타임 단일 접근(중앙 DB 패턴). 임포터가 Resources/TradeEvolution_Data.asset에 기록.
    private static TradeEvolutionData _instance;
    public static TradeEvolutionData Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<TradeEvolutionData>("TradeEvolution_Data");
            // 매핑이 비어도(데이터 미입력) 정상 — 통신교환은 평범한 핸드오버로 폴백.
            return _instance;
        }
    }

    // 잦은 조회를 위한 캐시 (targetEn → 전체 규칙). 코드에서 딕셔너리로 제어.
    private Dictionary<string, TradeEvolutionMapping> _lookup;

    private void OnEnable()  => _lookup = null;   // 핫리로드/임포트 후 재빌드 강제
    private void OnDisable() => _lookup = null;

    private Dictionary<string, TradeEvolutionMapping> Lookup
    {
        get
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, TradeEvolutionMapping>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in mappings)
                    if (!string.IsNullOrEmpty(m.targetPokemonEn))
                        _lookup[m.targetPokemonEn] = m;
            }
            return _lookup;
        }
    }

    /// <summary>해당 종이 통신교환 시 진화하는지 여부.</summary>
    public bool IsTradeEvolver(string targetNameEn) => Lookup.ContainsKey(targetNameEn);

    /// <summary>원본 종에 대응하는 전체 통신 진화 규칙. 대상이 아니면 null.</summary>
    public TradeEvolutionMapping GetMapping(string targetNameEn)
        => !string.IsNullOrEmpty(targetNameEn) && Lookup.TryGetValue(targetNameEn, out var mapping)
            ? mapping
            : null;

    /// <summary>targetPokemonEn의 통신진화 후 영문명. 대상이 아니면 null.</summary>
    public string GetEvolved(string targetNameEn)
        => GetMapping(targetNameEn)?.evolvedPokemonEn;
}
