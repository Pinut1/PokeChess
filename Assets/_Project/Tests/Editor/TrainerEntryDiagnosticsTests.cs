using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PokeChess.EditorTools;

namespace PokeChess.Tests
{
    public class TrainerEntryDiagnosticsTests
    {
        private static string[] TextsOf(
            IEnumerable<TrainerEntryDiagnosticMessage> messages,
            TrainerEntryIssueSeverity severity)
        {
            return messages.Where(m => m.Severity == severity).Select(m => m.Text).ToArray();
        }

        private static string SummaryOf(IEnumerable<TrainerEntryDiagnosticMessage> messages)
        {
            return messages.Single(m => m.Severity == TrainerEntryIssueSeverity.Info).Text;
        }

        [Test]
        public void DuplicateTrainerId_IsReportedAsError()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordDuplicateTrainerId("TR_BROCK");

            var report = diagnostics.BuildReport(new[] { "TR_BROCK" });
            string[] errors = TextsOf(report, TrainerEntryIssueSeverity.Error);

            Assert.That(errors, Has.Length.EqualTo(1));
            Assert.That(errors[0], Does.Contain("TR_BROCK"));
            Assert.That(errors[0], Does.Contain("중복"));
        }

        [Test]
        public void MissingTrainerId_IsReportedWithReferencingStages()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordInlineEnemyFallback("1-3", "TR_GHOST", hasTrainerEntry: false);
            diagnostics.RecordInlineEnemyFallback("2-1", "TR_GHOST", hasTrainerEntry: false);

            var report = diagnostics.BuildReport(System.Array.Empty<string>());
            string warning = TextsOf(report, TrainerEntryIssueSeverity.Warning).Single();

            Assert.That(warning, Does.Contain("TR_GHOST"));
            Assert.That(warning, Does.Contain("1-3"));
            Assert.That(warning, Does.Contain("2-1"));
            Assert.That(diagnostics.TrainerIdInlineFallbackCount, Is.EqualTo(2));
        }

        [Test]
        public void EntryWithEmptyEnemies_IsReportedSeparatelyFromMissingEntry()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordTrainerEntryReferenced("TR_EMPTY");
            diagnostics.RecordInlineEnemyFallback("3-2", "TR_EMPTY", hasTrainerEntry: true);

            var report = diagnostics.BuildReport(new[] { "TR_EMPTY" });
            string warning = TextsOf(report, TrainerEntryIssueSeverity.Warning).Single();

            // 엔트리가 존재하므로 "없음"이 아니라 "enemies가 비어있음"으로 분류돼야 한다
            Assert.That(warning, Does.Contain("enemies가 비어있음"));
            Assert.That(warning, Does.Contain("3-2"));
            Assert.That(SummaryOf(report), Does.Contain("enemies 비어있는 trainerId 1개"));
        }

        [Test]
        public void UnreferencedTrainerId_IsReported()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordTrainerEntryReferenced("TR_USED");

            var unreferenced = diagnostics.GetUnreferencedTrainerIds(new[] { "TR_USED", "TR_ORPHAN" });

            Assert.That(unreferenced, Is.EqualTo(new[] { "TR_ORPHAN" }));
        }

        [Test]
        public void TrainerIdMatching_IsCaseInsensitive()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordTrainerEntryReferenced("tr_brock");

            // 임포터가 OrdinalIgnoreCase 맵을 쓰므로 미참조 판정도 대소문자를 무시해야 한다
            var unreferenced = diagnostics.GetUnreferencedTrainerIds(new[] { "TR_BROCK" });

            Assert.That(unreferenced, Is.Empty);
        }

        [Test]
        public void StageWithoutTrainerId_CountsAsLegacyNotDataProblem()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordInlineEnemyFallback("1-1", "", hasTrainerEntry: false);
            diagnostics.RecordInlineEnemyFallback("1-2", null, hasTrainerEntry: false);

            Assert.That(diagnostics.LegacyInlineEnemyCount, Is.EqualTo(2));
            Assert.That(diagnostics.TrainerIdInlineFallbackCount, Is.Zero);

            // 레거시 스테이지만 있으면 경고는 없고 요약(Info)만 남아야 한다
            var report = diagnostics.BuildReport(System.Array.Empty<string>());
            Assert.That(TextsOf(report, TrainerEntryIssueSeverity.Warning), Is.Empty);
            Assert.That(TextsOf(report, TrainerEntryIssueSeverity.Error), Is.Empty);
        }

        [Test]
        public void CleanData_ProducesOnlySummary()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordTrainerEntryReferenced("TR_A");
            diagnostics.RecordTrainerEntryReferenced("TR_B");

            var report = diagnostics.BuildReport(new[] { "TR_A", "TR_B" });

            Assert.That(report, Has.Count.EqualTo(1));
            Assert.That(report[0].Severity, Is.EqualTo(TrainerEntryIssueSeverity.Info));
        }

        [Test]
        public void CleanData_DoesNotBlockImport()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordTrainerEntryReferenced("TR_A");

            Assert.That(diagnostics.HasBlockingIssues, Is.False);
        }

        [Test]
        public void MissingTrainerId_BlocksImport()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordInlineEnemyFallback("1-3", "TR_TYPO", hasTrainerEntry: false);

            Assert.That(diagnostics.HasBlockingIssues, Is.True);
        }

        [Test]
        public void DuplicateTrainerId_BlocksImport()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordDuplicateTrainerId("GYM_R2");

            Assert.That(diagnostics.HasBlockingIssues, Is.True);
        }

        [Test]
        public void EmptyStage_BlocksImportAndIsReportedAsError()
        {
            var diagnostics = new TrainerEntryDiagnostics();
            diagnostics.RecordEmptyStage("1-4");

            Assert.That(diagnostics.HasBlockingIssues, Is.True);

            var report = diagnostics.BuildReport(System.Array.Empty<string>());
            string error = TextsOf(report, TrainerEntryIssueSeverity.Error).Single();
            Assert.That(error, Does.Contain("1-4"));
            Assert.That(error, Does.Contain("적이 한 마리도"));
        }

        [Test]
        public void UnreferencedTrainerId_DoesNotBlockImport()
        {
            var diagnostics = new TrainerEntryDiagnostics();

            // 안 쓰는 엔트리는 산출물에 영향이 없으므로 경고까지만
            var report = diagnostics.BuildReport(new[] { "TR_ORPHAN" });

            Assert.That(TextsOf(report, TrainerEntryIssueSeverity.Warning), Has.Length.EqualTo(1));
            Assert.That(diagnostics.HasBlockingIssues, Is.False);
        }
    }
}
