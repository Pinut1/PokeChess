using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 파일명 앞 3자리(도감번호)를 PokemonData.id와 매칭해 스프라이트 필드를 일괄 연결하는 1회성 툴.
/// 예: "001bulbasaur_card.png" → id 1의 포켓몬.
///
/// PokeChessImporter의 JSON 재임포트와는 별개다 — 스프라이트 필드는 임포터가 보호하는(덮어쓰지 않는)
/// 수동 필드라 이 메뉴로 따로 실행해야 한다. 이미 채워진 항목은 건너뛰므로 손으로 물린 예외는 살아남는다.
///
/// 두 종류를 연결한다.
///   · Link Card Illustrations → icon      (상점 카드·스탯창용 큰 일러스트, Art/UI/Illust)
///   · Link Unit Icons         → unitIcon  (별도 png로 만든 전용 아이콘, Art/UI/UnitIcon)
///
/// 일러스트 한 장을 _0(전체)/_1(250×250 아이콘)로 쪼개 쓰는 경로는 이쪽이 아니라
/// IllustSpriteSplitter가 담당한다 — 그쪽은 같은 파일 안의 서브 스프라이트를 물리므로
/// 파일명 매칭이 아니라 이름 접미사(_0/_1)로 고른다.
/// </summary>
public static class ShopIllustrationLinker
{
    private const string IllustFolder   = "Assets/Art/UI/Illust";
    private const string UnitIconFolder = "Assets/Art/UI/UnitIcon";
    private const string PokemonDbPath  = "Assets/Resources/PokemonDatabase.asset";

    [MenuItem("PokeChess/Link Card Illustrations")]
    public static void LinkIllustrations()
    {
        Link("ShopIllustrationLinker", IllustFolder,
             data => data.icon,
             (data, sprite) => data.icon = sprite);
    }

    [MenuItem("PokeChess/Link Unit Icons")]
    public static void LinkUnitIcons()
    {
        Link("UnitIconLinker", UnitIconFolder,
             data => data.unitIcon,
             (data, sprite) => data.unitIcon = sprite);
    }

    /// <summary>
    /// folder 안의 png를 도감번호로 매칭해 setter가 가리키는 필드를 채운다.
    /// getter가 이미 값을 반환하는(= 수동으로 물려둔) 항목은 건드리지 않는다.
    /// </summary>
    private static void Link(string tag, string folder,
                             Func<PokemonData, Sprite> getter,
                             Action<PokemonData, Sprite> setter)
    {
        var db = AssetDatabase.LoadAssetAtPath<PokemonDatabase>(PokemonDbPath);
        if (db == null || db.all == null)
        {
            Debug.LogError($"[{tag}] {PokemonDbPath} 없음 — Import Pokemon JSON 먼저 실행하세요.");
            return;
        }

        if (!Directory.Exists(folder))
        {
            Debug.LogError($"[{tag}] {folder} 폴더가 없습니다 — 아이콘 png를 그 경로에 넣고 다시 실행하세요. " +
                           "파일명 앞 3자리가 도감번호여야 합니다(예: 001bulbasaur_icon.png).");
            return;
        }

        // id(3자리, 예: 001) → 파일 경로
        var byId = new Dictionary<int, string>();
        var idPrefix = new Regex(@"^(\d{3})");

        foreach (var path in Directory.GetFiles(folder, "*.png"))
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            var m = idPrefix.Match(fileName);
            if (!m.Success) continue;

            byId[int.Parse(m.Groups[1].Value)] = path.Replace('\\', '/');
        }

        if (byId.Count == 0)
        {
            Debug.LogWarning($"[{tag}] {folder}에서 도감번호로 시작하는 png를 못 찾았습니다 — 파일명을 확인하세요.");
            return;
        }

        int linked = 0, skippedAlreadySet = 0, missing = 0;

        foreach (var data in db.all)
        {
            if (data == null) continue;

            if (getter(data) != null)
            {
                skippedAlreadySet++;
                continue;
            }

            if (!byId.TryGetValue(data.id, out string path))
            {
                Debug.LogWarning($"[{tag}] id {data.id} ({data.pokemonNameEn}) — 일치하는 파일 없음");
                missing++;
                continue;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[{tag}] {path} — Sprite로 로드 실패(Texture Type을 Sprite로 바꿨는지 확인)");
                missing++;
                continue;
            }

            setter(data, sprite);
            EditorUtility.SetDirty(data);
            linked++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[{tag}] 연결 {linked} / 이미 설정됨(건너뜀) {skippedAlreadySet} / 매칭 실패 {missing}");
    }
}
