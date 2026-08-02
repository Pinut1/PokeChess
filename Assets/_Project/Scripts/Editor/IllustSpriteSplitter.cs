using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// 카드 일러스트 한 장(600×350)을 스프라이트 <b>2개</b>로 쪼개는 1회성 툴.
///   · {파일명}_0 → 원본 전체 (상점 카드·스탯창용 일러스트)
///   · {파일명}_1 → 250×250 정사각 (시너지 툴팁 같은 작은 칸용 아이콘)
/// 311플러시·312마이농에 이미 쓰던 방식을 나머지 전부에 똑같이 적용한다.
///
/// 파일을 새로 만들지 않는 게 핵심이다 — 자르는 <b>영역만</b> .meta에 기록되므로
/// LFS 용량이 늘지 않고, 얼굴 위치가 마음에 안 들면 Sprite Editor에서 사각형만 옮기면 된다.
///
/// _1의 기본 위치는 가운데(175, 50)다. 카드 아트는 구도가 제각각이라
/// (이상해씨는 얼굴이 중앙, 롱스톤은 머리가 오른쪽 끝) <b>자동으로 얼굴을 맞출 수는 없다.</b>
/// 기본값으로 깔아둔 뒤 Sprite Editor에서 종별로 옮기는 것을 전제로 한다.
///
/// ⚠️ 이미 Multiple로 잘라둔 파일은 <b>건드리지 않는다</b> — 손으로 맞춰둔 위치를 지키기 위해서다.
/// 그래서 여러 번 실행해도 안전하다.
/// </summary>
public static class IllustSpriteSplitter
{
    private const string IllustFolder  = "Assets/Art/UI/Illust";
    private const string PokemonDbPath = "Assets/Resources/PokemonDatabase.asset";

    /// <summary>작은 칸용 아이콘 한 변(px). 311/312에서 쓰던 값과 같다.</summary>
    private const int IconSize = 250;

    [MenuItem("PokeChess/Split Illustration Sprites")]
    public static void Split()
    {
        string[] files = Directory.GetFiles(IllustFolder, "*.png");
        if (files.Length == 0)
        {
            Debug.LogError($"[IllustSplitter] {IllustFolder}에 png가 없습니다.");
            return;
        }

        int split = 0, keptExisting = 0, failed = 0;

        try
        {
            AssetDatabase.StartAssetEditing(); // 202장을 한 장씩 리임포트하면 몇 분씩 걸린다

            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');

                if (EditorUtility.DisplayCancelableProgressBar(
                        "일러스트 분할", Path.GetFileName(path), (float)i / files.Length))
                    break;

                switch (SplitOne(path))
                {
                    case Result.Split:        split++;        break;
                    case Result.KeptExisting: keptExisting++; break;
                    case Result.Failed:       failed++;       break;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[IllustSplitter] 분할 {split} / 기존 유지(건너뜀) {keptExisting} / 실패 {failed}");

        // 자르고 나면 예전 단일 스프라이트 참조가 끊기므로 곧바로 다시 연결한다.
        RelinkPokemonData();
    }

    private enum Result { Split, KeptExisting, Failed }

    private static Result SplitOne(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return Result.Failed;

        // 이미 쪼개 둔 파일은 손대지 않는다(손으로 맞춘 _1 위치 보존).
        if (importer.spriteImportMode == SpriteImportMode.Multiple)
            return Result.KeptExisting;

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) return Result.Failed;

        int width = texture.width, height = texture.height;
        int icon = Mathf.Min(IconSize, width, height);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.SaveAndReimport(); // 모드를 먼저 확정해야 데이터 프로바이더가 Multiple로 열린다

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) return Result.Failed;

        provider.InitSpriteEditorDataProvider();

        string baseName = Path.GetFileNameWithoutExtension(path);
        var rects = new[]
        {
            NewRect($"{baseName}_0", new Rect(0, 0, width, height)),
            // 기본 위치는 한가운데. 얼굴에 맞추는 건 Sprite Editor에서 사람이 한다.
            NewRect($"{baseName}_1", new Rect((width - icon) / 2f, (height - icon) / 2f, icon, icon)),
        };

        provider.SetSpriteRects(rects);
        provider.Apply();
        importer.SaveAndReimport();

        return Result.Split;
    }

    private static SpriteRect NewRect(string name, Rect rect) => new()
    {
        name = name,
        rect = rect,
        alignment = SpriteAlignment.Center,
        pivot = new Vector2(0.5f, 0.5f),
        border = Vector4.zero,
        spriteID = GUID.Generate(),
    };

    // ─────────────────────────────────────────
    // 분할 후 재연결
    // ─────────────────────────────────────────

    /// <summary>
    /// PokemonData.icon → _0, unitIcon → _1로 다시 연결한다.
    /// 단일 스프라이트를 Multiple로 바꾸면 예전 참조(fileID 21300000)가 끊기므로 반드시 필요하다.
    ///
    /// unitIcon은 이미 값이 있으면 건드리지 않는다 — 손으로 만들어 넣은 전용 아이콘이 우선이다.
    /// </summary>
    [MenuItem("PokeChess/Relink Illustrations (_0 일러스트 / _1 아이콘)")]
    public static void RelinkPokemonData()
    {
        var db = AssetDatabase.LoadAssetAtPath<PokemonDatabase>(PokemonDbPath);
        if (db == null || db.all == null)
        {
            Debug.LogError($"[IllustSplitter] {PokemonDbPath} 없음 — Import Pokemon JSON 먼저 실행하세요.");
            return;
        }

        // 도감번호 → 파일 경로 (파일명 앞 3자리 규칙, ShopIllustrationLinker와 동일)
        var byId = new Dictionary<int, string>();
        var idPrefix = new Regex(@"^(\d{3})");
        foreach (var file in Directory.GetFiles(IllustFolder, "*.png"))
        {
            var m = idPrefix.Match(Path.GetFileNameWithoutExtension(file));
            if (m.Success) byId[int.Parse(m.Groups[1].Value)] = file.Replace('\\', '/');
        }

        int linkedIcon = 0, linkedUnitIcon = 0, keptUnitIcon = 0, missing = 0;

        foreach (var data in db.all)
        {
            if (data == null) continue;

            if (!byId.TryGetValue(data.id, out string path))
            {
                missing++;
                continue;
            }

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[IllustSplitter] {path} — 스프라이트를 못 읽음");
                missing++;
                continue;
            }

            // 이름이 _0/_1로 끝나는 걸 고르고, 아직 안 쪼갠 파일이면 유일한 스프라이트를 일러스트로 본다.
            Sprite full = sprites.FirstOrDefault(s => s.name.EndsWith("_0")) ?? sprites[0];
            Sprite icon = sprites.FirstOrDefault(s => s.name.EndsWith("_1"));

            if (data.icon != full)
            {
                data.icon = full;
                linkedIcon++;
            }

            if (icon != null)
            {
                // 손으로 만들어 넣은 전용 아이콘(Art/UI/UnitIcon 등)은 덮어쓰지 않는다.
                bool isFromThisIllust = data.unitIcon != null &&
                                        AssetDatabase.GetAssetPath(data.unitIcon) == path;

                if (data.unitIcon == null || isFromThisIllust)
                {
                    if (data.unitIcon != icon) linkedUnitIcon++;
                    data.unitIcon = icon;
                }
                else keptUnitIcon++;
            }

            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[IllustSplitter] 재연결 — 일러스트 {linkedIcon} / 아이콘 {linkedUnitIcon} / " +
                  $"직접 넣은 아이콘 유지 {keptUnitIcon} / 파일 없음 {missing}");
    }
}
