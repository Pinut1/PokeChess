using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SoundManager는 DontDestroyOnLoad 싱글턴이라 "최초 진입 씬"에 오브젝트가 없으면
/// 다른 씬으로 넘어가도 인스턴스가 아예 생기지 않는다. GameSceneTest에는 배치돼 있지만
/// NetworkTest(타이틀 씬)에는 없어서, 거기서 바로 Play하면 TitleScreenUI의
/// SoundManager.TryGet이 계속 조용히 실패해 BGM/SFX가 안 들린다(TryGet은 로그를 안 남긴다).
/// 이 툴은 GameSceneTest에 있는 SoundManager 구성(AudioSource 2개 + SoundCatalog 연결)을
/// 현재 열려있는 씬에 그대로 재현한다.
/// </summary>
public static class SoundManagerSetupTool
{
    private const string CatalogPath = "Assets/_Project/ScriptableObjects/SFXData/SoundCatalog.asset";

    [MenuItem("PokeChess/Audio/Create SoundManager In Current Scene")]
    private static void CreateSoundManager()
    {
        if (Object.FindFirstObjectByType<SoundManager>() != null)
        {
            Debug.LogWarning("[SoundManagerSetupTool] 현재 씬에 이미 SoundManager가 있습니다 — 건너뜁니다.");
            return;
        }

        var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[SoundManagerSetupTool] SoundCatalog을 찾지 못했습니다: {CatalogPath}");
            return;
        }

        var go = new GameObject("SoundManager");
        Undo.RegisterCreatedObjectUndo(go, "Create SoundManager");

        var sfxSource = go.AddComponent<AudioSource>();
        var bgmSource = go.AddComponent<AudioSource>();
        var manager = go.AddComponent<SoundManager>();

        var so = new SerializedObject(manager);
        so.FindProperty("_sfxSource").objectReferenceValue = sfxSource;
        so.FindProperty("_bgmSource").objectReferenceValue = bgmSource;
        so.FindProperty("_catalog").objectReferenceValue = catalog;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        Debug.Log("[SoundManagerSetupTool] SoundManager 생성 완료. Ctrl+S로 씬을 저장해 주세요.");
    }
}
