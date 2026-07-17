using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전적 기록 담당. 판 진행 중 이벤트로 상태를 추적하다가
/// 종료(OnGameCleared=승리 / OnSessionEnded=패배) 시점에 MatchRecord를 만들어
/// MatchHistoryStore(jsonl)에 저장하고 GameEvents.MatchRecorded로 알린다.
///
/// 각 클라이언트가 자기 관점 기록을 쓴다(마스터 단독 기록 아님 — 재접속 실패처럼
/// 마스터가 이미 죽은 채 끝나는 판이 있어, 각자 기록이 유실에 강하다).
///
/// 보드/시너지는 종료 시점에 GameManager 허브를 통해 pull(Board↔Synergy와 동일한
/// 이벤트+API 하이브리드 패턴). 파트너 보드는 마지막 미러 스냅샷(OnOpponentBoardChanged) 기반.
/// </summary>
public class MatchRecorder : MonoBehaviour
{
    private bool _matchActive;   // 라운드 1 시작 ~ 종료 사이
    private bool _finalized;     // 이번 판 기록 완료(중복 저장 방지)

    private DateTime _startedUtc;
    private int      _finalRound;
    private string   _finalStageId = "";
    private int      _selfLevel = 1;
    private BoardSnapshot _lastPartnerBoard;
    private readonly List<string> _selfAugments = new();
    private string[] _partnerAugments = Array.Empty<string>();

    private void OnEnable()
    {
        GameEvents.OnRoundChanged           += HandleRoundChanged;
        GameEvents.OnStageEntered           += HandleStageEntered;
        GameEvents.OnLevelChanged           += HandleLevelChanged;
        GameEvents.OnOpponentBoardChanged   += HandleOpponentBoardChanged;
        GameEvents.OnAugmentSelected        += HandleAugmentSelected;
        GameEvents.OnPartnerAugmentsChanged += HandlePartnerAugmentsChanged;
        GameEvents.OnGameCleared            += HandleGameCleared;
        GameEvents.OnSessionEnded           += HandleSessionEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundChanged           -= HandleRoundChanged;
        GameEvents.OnStageEntered           -= HandleStageEntered;
        GameEvents.OnLevelChanged           -= HandleLevelChanged;
        GameEvents.OnOpponentBoardChanged   -= HandleOpponentBoardChanged;
        GameEvents.OnAugmentSelected        -= HandleAugmentSelected;
        GameEvents.OnPartnerAugmentsChanged -= HandlePartnerAugmentsChanged;
        GameEvents.OnGameCleared            -= HandleGameCleared;
        GameEvents.OnSessionEnded           -= HandleSessionEnded;
    }

    // ─────────────────────────────────────────
    // 진행 중 상태 추적
    // ─────────────────────────────────────────

    private void HandleRoundChanged(int round)
    {
        if (!_matchActive)
        {
            _matchActive = true;
            _finalized   = false;
            _startedUtc  = DateTime.UtcNow;
        }
        _finalRound = round;
    }

    private void HandleStageEntered(StageData stage)
    {
        if (stage != null && !string.IsNullOrEmpty(stage.stageId))
            _finalStageId = stage.stageId;
    }

    private void HandleLevelChanged(int level) => _selfLevel = level;

    private void HandleOpponentBoardChanged(BoardSnapshot snap) => _lastPartnerBoard = snap;

    private void HandleAugmentSelected(AugmentData data)
    {
        if (data == null) return;
        string name = !string.IsNullOrEmpty(data.augmentNameEn) ? data.augmentNameEn : data.augmentName;
        if (!string.IsNullOrEmpty(name)) _selfAugments.Add(name);
    }

    /// <summary>파트너 증강은 CustomProperties 미러(누적 전체 배열)라 마지막 수신값으로 덮어쓴다.</summary>
    private void HandlePartnerAugmentsChanged(string[] augments)
        => _partnerAugments = augments ?? Array.Empty<string>();

    // ─────────────────────────────────────────
    // 종료 → 기록
    // ─────────────────────────────────────────

    private void HandleGameCleared() => Finalize("Victory", "GameCleared");

    private void HandleSessionEnded(SessionEndReason reason) => Finalize("Defeat", reason.ToString());

    private void Finalize(string result, string endReason)
    {
        // Victory 직후 마지막 전투 결과로 SessionEnded가 겹치는 등 종료 이벤트가
        // 중복 도착할 수 있으므로 한 판당 1건만 기록한다.
        if (!_matchActive || _finalized) return;
        _finalized = true;

        var record = BuildRecord(result, endReason);

        if (MatchHistoryStore.Append(record))
        {
            Debug.Log($"[MatchHistory] 전적 기록: {record.result}/{record.endReason} " +
                      $"(라운드 {record.finalRound}, 스테이지 {record.finalStageId}) → {MatchHistoryStore.FilePath}");
            GameEvents.MatchRecorded(record);
        }
        else
        {
            Debug.LogWarning($"[MatchHistory] 전적 저장 실패 — MatchRecorded 미발행 ({record.result}/{record.endReason})");
        }

        ResetForNextMatch();
    }

    /// <summary>
    /// 씬 리로드 없이 다음 판이 시작될 수 있도록 판 단위 상태를 초기화.
    /// _matchActive=false라 중복 종료 이벤트는 계속 무시되고,
    /// 다음 RoundChanged가 오면 새 판으로 추적을 시작한다.
    /// </summary>
    private void ResetForNextMatch()
    {
        _matchActive      = false;
        _finalRound       = 0;
        _finalStageId     = "";
        _selfLevel        = 1;
        _lastPartnerBoard = null;
        _selfAugments.Clear();
        _partnerAugments  = Array.Empty<string>();
    }

    private MatchRecord BuildRecord(string result, string endReason)
    {
        DateTime endedUtc = DateTime.UtcNow;
        var gm = GameManager.Instance;
        var net = gm != null ? gm.Network : null;

        // matchId: NetworkManager가 배포한 판 GUID(협동=Room 커스텀 속성, 솔로=라운드1 재발급)
        // 우선 — 두 클라이언트가 항상 같은 값을 가진다. GUID가 없으면(오프라인 스텁 등)
        // 구형 "방이름+분단위 시각"으로 폴백.
        string matchId = net != null ? net.MatchGuid : "";
        if (string.IsNullOrEmpty(matchId))
        {
            string roomName = net != null ? net.RoomName : "unknown";
            matchId = $"{roomName}_{_startedUtc:yyyyMMddHHmm}";
        }

        return new MatchRecord
        {
            matchId         = matchId,
            gameVersion     = Application.version,
            startedAtUtc    = _startedUtc.ToString("o"),
            endedAtUtc      = endedUtc.ToString("o"),
            durationSeconds = (int)(endedUtc - _startedUtc).TotalSeconds,
            result          = result,
            endReason       = endReason,
            finalRound      = _finalRound,
            finalStageId    = _finalStageId,
            self            = BuildSelf(gm, net),
            partner         = BuildPartner(net),
        };
    }

    private PlayerRecord BuildSelf(GameManager gm, NetworkManager net)
    {
        var record = new PlayerRecord
        {
            nickname        = net != null ? net.LocalNickname : "",
            isMaster        = net == null || net.IsMasterClient,
            level           = _selfLevel,
            board           = Array.Empty<UnitRecord>(),
            activeSynergies = Array.Empty<string>(),
            augments        = _selfAugments.ToArray(),
        };

        var board = gm != null ? gm.Board : null;
        if (board != null)
        {
            var units = board.GetUnitsOnBoard();
            var list = new List<UnitRecord>(units.Count);
            foreach (var unit in units)
            {
                if (unit == null || unit.data == null) continue;

                var itemsEn = new List<string>(unit.items.Count);
                foreach (var item in unit.items)
                    if (item != null) itemsEn.Add(item.itemNameEn);

                list.Add(new UnitRecord
                {
                    nameEn       = unit.data.pokemonNameEn,
                    star         = unit.starLevel,
                    itemsEn      = itemsEn.ToArray(),
                    stoneEn      = unit.equippedStone != null ? unit.equippedStone.stoneNameEn : "",
                    stoneEvolved = unit.IsStoneEvolved,
                });
            }
            record.board = list.ToArray();
        }

        var synergy = gm != null ? gm.Synergy : null;
        if (synergy != null)
        {
            var list = new List<string>();
            foreach (var status in synergy.GetActiveSynergies())
            {
                if (status?.data == null) continue;
                string name = !string.IsNullOrEmpty(status.data.synergyNameEn)
                    ? status.data.synergyNameEn : status.data.synergyName;
                list.Add($"{name}:{status.uniqueCount}");
            }
            record.activeSynergies = list.ToArray();
        }

        return record;
    }

    /// <summary>파트너 기록 — 보드 미러 스냅샷 기반이라 보드 위 유닛의 종/성급만 복원 가능.</summary>
    private PlayerRecord BuildPartner(NetworkManager net)
    {
        var record = new PlayerRecord
        {
            nickname        = net != null ? net.PartnerNickname : "",
            isMaster        = net != null && !net.IsMasterClient, // 2인 방 — 내가 마스터가 아니면 파트너가 마스터
            level           = 0,                                  // 미전송 — 0 = 알 수 없음
            board           = Array.Empty<UnitRecord>(),
            activeSynergies = Array.Empty<string>(),
            augments        = _partnerAugments,
        };

        var db = PokemonDatabase.Instance;
        if (_lastPartnerBoard == null || db == null) return record;

        var list = new List<UnitRecord>(_lastPartnerBoard.entries.Count);
        foreach (var entry in _lastPartnerBoard.entries)
        {
            if (!entry.onBoard) continue; // 전적은 최종 "보드"만 (self와 기준 통일)

            var data = db.GetById(entry.speciesId);
            if (data == null) continue;

            list.Add(new UnitRecord
            {
                nameEn  = data.pokemonNameEn,
                star    = entry.starLevel,
                itemsEn = Array.Empty<string>(), // 미러 스냅샷엔 장착물 정보 없음
                stoneEn = "",
            });
        }
        record.board = list.ToArray();

        return record;
    }
}
