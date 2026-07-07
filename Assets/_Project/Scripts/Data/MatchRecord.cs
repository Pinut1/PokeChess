using System;

/// <summary>
/// 전적 1건(한 판)의 기록. 로컬 jsonl 저장(MatchHistoryStore)과
/// 추후 서버 업로드(Supabase 등)·인게임 전적 창이 공유하는 스키마.
///
/// 설계 결정:
///  - 각 클라이언트가 "자기 관점(self)" 기록을 쓴다. MasterClient 단독 기록이 아님 —
///    재접속 실패 등 마스터가 이미 죽은 상태로 끝나는 판이 있어 각자 기록이 유실에 강하다.
///    같은 판은 matchId로 묶는다(협동 한 판 = 레코드 2건).
///  - partner는 보드 미러 스냅샷(BoardSnapshot) 기반이라 유닛 종/성급만 있고
///    아이템/시너지는 없다(전송 안 함). 없으면 board가 빈 배열.
///  - JsonUtility 직렬화 대상이므로 프로퍼티 대신 public 필드, 컬렉션은 배열 사용.
/// </summary>
[Serializable]
public class MatchRecord
{
    /// <summary>스키마 버전. 필드 추가/변경 시 올리고 읽기 측에서 마이그레이션 분기.</summary>
    public int schemaVersion = 1;

    /// <summary>같은 판을 식별하는 ID(두 클라이언트가 동일 값). 방이름 + 시작시각(분 단위).</summary>
    public string matchId;

    public string gameVersion;

    public string startedAtUtc;    // ISO 8601 ("o" 포맷)
    public string endedAtUtc;
    public int    durationSeconds;

    public string result;          // "Victory" | "Defeat"
    public string endReason;       // "GameCleared" | SessionEndReason 이름
    public int    finalRound;
    public string finalStageId;    // 도달 스테이지 ("1-7" 등). 스테이지 미해석이면 ""

    public PlayerRecord self;
    public PlayerRecord partner;   // 파트너 정보 없으면(솔로 모드 등) nickname=""
}

[Serializable]
public class PlayerRecord
{
    public string nickname;
    public bool   isMaster;
    public int    level;

    public UnitRecord[] board;           // 최종 보드(벤치 제외)
    public string[]     activeSynergies; // "Dark:3" = 시너지영문명:고유종수
    public string[]     augments;        // 증강 시스템(해인) 연결 전까지 빈 배열
}

[Serializable]
public class UnitRecord
{
    public string   nameEn;       // PokemonData.pokemonNameEn — 검색/조인 키
    public int      star;         // 1~3성
    public string[] itemsEn;      // 장착템 영문명
    public string   stoneEn;      // 진화의 돌 영문명. 없으면 ""
    public bool     stoneEvolved; // 돌 진화 상태로 종료했는지
}
