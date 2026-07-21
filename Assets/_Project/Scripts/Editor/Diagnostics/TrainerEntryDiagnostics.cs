using System;
using System.Collections.Generic;

namespace PokeChess.EditorTools
{
    public enum TrainerEntryIssueSeverity
    {
        Info,
        Warning,
        Error,
    }

    public readonly struct TrainerEntryDiagnosticMessage
    {
        public readonly TrainerEntryIssueSeverity Severity;
        public readonly string Text;

        public TrainerEntryDiagnosticMessage(TrainerEntryIssueSeverity severity, string text)
        {
            Severity = severity;
            Text = text;
        }
    }

    /// <summary>
    /// stage_data ↔ trainer_entry_data join 과정에서 발견한 데이터 문제를 모았다가 마지막에 한 번에 보고한다.
    /// Unity 타입에 의존하지 않아 EditMode 테스트에서 그대로 검증할 수 있다.
    /// </summary>
    public sealed class TrainerEntryDiagnostics
    {
        private static readonly StringComparer IdComparer = StringComparer.OrdinalIgnoreCase;

        private readonly List<string> _duplicateTrainerIds = new List<string>();
        private readonly HashSet<string> _referencedTrainerIds = new HashSet<string>(IdComparer);
        private readonly Dictionary<string, List<string>> _missingTrainerStages =
            new Dictionary<string, List<string>>(IdComparer);
        private readonly Dictionary<string, List<string>> _emptyEnemyTrainerStages =
            new Dictionary<string, List<string>>(IdComparer);
        private readonly List<string> _emptyStageIds = new List<string>();

        private int _legacyInlineEnemyCount;
        private int _trainerIdInlineFallbackCount;

        /// <summary>trainerId 자체가 없는 정상 레거시 스테이지 수.</summary>
        public int LegacyInlineEnemyCount => _legacyInlineEnemyCount;

        /// <summary>trainerId는 있는데 인라인 enemies로 폴백한 스테이지 수(= 데이터 문제).</summary>
        public int TrainerIdInlineFallbackCount => _trainerIdInlineFallbackCount;

        public IReadOnlyList<string> DuplicateTrainerIds => _duplicateTrainerIds;

        /// <summary>적이 한 마리도 배치되지 않은 스테이지 id. StageType이 전부 전투이므로 항상 결함이다.</summary>
        public IReadOnlyList<string> EmptyStageIds => _emptyStageIds;

        /// <summary>
        /// 임포트를 중단해야 하는 결함이 있는지. 산출물이 깨지거나(적 0마리)
        /// 어느 데이터가 맞는지 모호한(중복·누락 trainerId) 경우가 해당된다.
        /// 미참조 trainerId는 산출물에 영향이 없으므로 경고로만 남긴다.
        /// </summary>
        public bool HasBlockingIssues =>
            _duplicateTrainerIds.Count > 0 ||
            _missingTrainerStages.Count > 0 ||
            _emptyStageIds.Count > 0;

        /// <summary>적이 한 마리도 없는 스테이지를 기록한다.</summary>
        public void RecordEmptyStage(string stageId)
        {
            _emptyStageIds.Add(stageId ?? "");
        }

        /// <summary>trainer_entry_data에 같은 trainerId가 두 번 이상 나온 경우.</summary>
        public void RecordDuplicateTrainerId(string trainerId)
        {
            if (string.IsNullOrEmpty(trainerId))
                return;

            _duplicateTrainerIds.Add(trainerId);
        }

        /// <summary>스테이지가 trainer_entry 엔트리를 실제로 찾아 참조했음.</summary>
        public void RecordTrainerEntryReferenced(string trainerId)
        {
            if (string.IsNullOrEmpty(trainerId))
                return;

            _referencedTrainerIds.Add(trainerId);
        }

        /// <summary>
        /// 스테이지가 trainer_entry 대신 stage_data 인라인 enemies로 폴백했음.
        /// trainerId가 비어 있으면 정상 레거시, 있으면 데이터 문제로 분류한다.
        /// </summary>
        public void RecordInlineEnemyFallback(string stageId, string trainerId, bool hasTrainerEntry)
        {
            if (string.IsNullOrEmpty(trainerId))
            {
                _legacyInlineEnemyCount++;
                return;
            }

            _trainerIdInlineFallbackCount++;

            // 엔트리를 아예 못 찾음 / 엔트리는 있는데 enemies가 비어있음 — 원인이 다르므로 따로 집계
            var bucket = hasTrainerEntry ? _emptyEnemyTrainerStages : _missingTrainerStages;
            if (!bucket.TryGetValue(trainerId, out var stageIds))
            {
                stageIds = new List<string>();
                bucket.Add(trainerId, stageIds);
            }

            stageIds.Add(stageId ?? "");
        }

        /// <summary>trainer_entry에 정의됐지만 어떤 스테이지도 참조하지 않는 trainerId.</summary>
        public List<string> GetUnreferencedTrainerIds(IEnumerable<string> allTrainerIds)
        {
            var unreferenced = new List<string>();
            if (allTrainerIds == null)
                return unreferenced;

            foreach (var trainerId in allTrainerIds)
            {
                if (string.IsNullOrEmpty(trainerId))
                    continue;

                if (!_referencedTrainerIds.Contains(trainerId))
                    unreferenced.Add(trainerId);
            }

            unreferenced.Sort(IdComparer);
            return unreferenced;
        }

        /// <summary>모아둔 진단을 사람이 읽을 메시지 목록으로 만든다. 마지막 항목은 항상 요약(Info).</summary>
        public List<TrainerEntryDiagnosticMessage> BuildReport(IEnumerable<string> allTrainerIds)
        {
            var messages = new List<TrainerEntryDiagnosticMessage>();

            foreach (var duplicateId in _duplicateTrainerIds)
            {
                messages.Add(new TrainerEntryDiagnosticMessage(
                    TrainerEntryIssueSeverity.Error,
                    $"trainer_entry_data에 trainerId '{duplicateId}'가 중복 — 먼저 나온 엔트리를 유지하고 이후 엔트리는 무시했습니다."));
            }

            foreach (var missing in _missingTrainerStages)
            {
                messages.Add(new TrainerEntryDiagnosticMessage(
                    TrainerEntryIssueSeverity.Warning,
                    $"trainer_entry_data에 trainerId '{missing.Key}' 없음 — 참조 스테이지: " +
                    $"{string.Join(", ", missing.Value)}. stage_data 인라인 enemies로 폴백했습니다."));
            }

            foreach (var empty in _emptyEnemyTrainerStages)
            {
                messages.Add(new TrainerEntryDiagnosticMessage(
                    TrainerEntryIssueSeverity.Warning,
                    $"trainer_entry_data에 trainerId '{empty.Key}' 엔트리는 있으나 enemies가 비어있음 — 참조 스테이지: " +
                    $"{string.Join(", ", empty.Value)}. stage_data 인라인 enemies로 폴백했습니다. 데이터 확인 필요."));
            }

            if (_emptyStageIds.Count > 0)
            {
                messages.Add(new TrainerEntryDiagnosticMessage(
                    TrainerEntryIssueSeverity.Error,
                    $"적이 한 마리도 배치되지 않은 스테이지 {_emptyStageIds.Count}개: " +
                    $"{string.Join(", ", _emptyStageIds)}. 모든 StageType이 전투이므로 데이터 오류입니다."));
            }

            var unreferenced = GetUnreferencedTrainerIds(allTrainerIds);
            if (unreferenced.Count > 0)
            {
                messages.Add(new TrainerEntryDiagnosticMessage(
                    TrainerEntryIssueSeverity.Warning,
                    $"어떤 스테이지에서도 참조하지 않는 trainer_entry_data trainerId {unreferenced.Count}개: " +
                    string.Join(", ", unreferenced)));
            }

            messages.Add(new TrainerEntryDiagnosticMessage(
                TrainerEntryIssueSeverity.Info,
                $"trainer_entry 진단 요약: 중복 trainerId {_duplicateTrainerIds.Count}개, " +
                $"누락 trainerId {_missingTrainerStages.Count}개, " +
                $"enemies 비어있는 trainerId {_emptyEnemyTrainerStages.Count}개, " +
                $"미참조 trainerId {unreferenced.Count}개, " +
                $"적 0마리 스테이지 {_emptyStageIds.Count}개, " +
                $"trainerId 있는데 인라인 폴백한 스테이지 {_trainerIdInlineFallbackCount}개, " +
                $"trainerId 없는 레거시 인라인 스테이지 {_legacyInlineEnemyCount}개."));

            return messages;
        }
    }
}
