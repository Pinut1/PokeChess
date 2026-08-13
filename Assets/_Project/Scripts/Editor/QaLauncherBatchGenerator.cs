#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Windows Standalone 빌드 완료 후 exe 옆에 QA_Client_A.bat / QA_Client_B.bat를 생성한다.
/// 동일 PC에서 같은 빌드를 두 개 띄워 "-qaClient=A"/"-qaClient=B"로 구분 실행할 때 쓰는
/// 실행 편의 파일일 뿐 — QA 슬롯 판별 로직(NetworkManager/SupabaseMatchUploader의
/// ParseQaSlot/PrefKey)은 건드리지 않는다. exe 이름은 report.summary.outputPath에서
/// 실제 빌드 산출물 파일명을 그대로 읽어온다(하드코딩 금지 — 다른 이름으로 빌드해도 맞게 생성).
/// </summary>
public class QaLauncherBatchGenerator : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows &&
            report.summary.platform != BuildTarget.StandaloneWindows64)
        {
            return;
        }

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogWarning("[QaLauncherBatchGenerator] 빌드가 실패/취소되어 QA BAT를 생성하지 않습니다.");
            return;
        }

        if (!IsQaBuildDefined(report.summary.platform)) return;

        string outputPath = report.summary.outputPath;
        if (string.IsNullOrEmpty(outputPath))
        {
            Debug.LogError("[QaLauncherBatchGenerator] BuildReport.summary.outputPath가 비어 있어 QA BAT를 생성하지 못했습니다.");
            return;
        }

        string exeDirectory = Path.GetDirectoryName(outputPath);
        string exeFileName = Path.GetFileName(outputPath);
        if (string.IsNullOrEmpty(exeDirectory) || string.IsNullOrEmpty(exeFileName))
        {
            Debug.LogError($"[QaLauncherBatchGenerator] outputPath에서 exe 디렉터리/파일명을 얻지 못했습니다. outputPath={outputPath}");
            return;
        }

        WriteLauncherBat(exeDirectory, "QA_Client_A.bat", exeFileName, "A");
        WriteLauncherBat(exeDirectory, "QA_Client_B.bat", exeFileName, "B");
    }

    /// <summary>빌드 대상 그룹의 Scripting Define Symbols에 "QA_BUILD"가 있는지 확인한다.</summary>
    private static bool IsQaBuildDefined(BuildTarget platform)
    {
        var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(platform));
        string defineSymbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
        foreach (string symbol in defineSymbols.Split(';'))
        {
            if (symbol == "QA_BUILD") return true;
        }
        return false;
    }

    private static void WriteLauncherBat(string directory, string batFileName, string exeFileName, string qaSlot)
    {
        string batPath = Path.Combine(directory, batFileName);
        string content = $"@echo off\r\nstart \"\" \"%~dp0{exeFileName}\" -qaClient={qaSlot}\r\n";

        try
        {
            File.WriteAllText(batPath, content);
            Debug.Log($"[QaLauncherBatchGenerator] {batFileName} 생성 완료: {batPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[QaLauncherBatchGenerator] {batFileName} 생성 실패: {batPath}\n{e}");
        }
    }
}
#endif
