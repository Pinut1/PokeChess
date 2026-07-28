using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Art/UI/Illust의 "001bulbasaur_card.png" 식 파일명(앞 3자리 = 도감번호)을
/// PokemonDatabase의 PokemonData.id와 매칭해 icon(Sprite) 필드를 일괄 연결하는 1회성 툴.
/// PokeChessImporter의 JSON 재임포트와는 별개 — icon은 임포터가 보호하는 필드라
/// 이 메뉴로 별도 실행해야 한다. 이미 icon이 채워진 항목은 덮어쓰지 않는다.
/// </summary>
public static class ShopIllustrationLinker
{
    private const string IllustFolder = "Assets/Art/UI/Illust";
    private const string PokemonDbPath = "Assets/Resources/PokemonDatabase.asset";

    [MenuItem("PokeChess/Link Card Illustrations")]
    public static void LinkIllustrations()
    {
        var db = AssetDatabase.LoadAssetAtPath<PokemonDatabase>(PokemonDbPath);
        if (db == null || db.all == null)
        {
            Debug.LogError($"[ShopIllustrationLinker] {PokemonDbPath} 없음 — Import Pokemon JSON 먼저 실행하세요.");
            return;
        }

        // id(3자리, 예: 001) -> 파일 경로
        var byId = new System.Collections.Generic.Dictionary<int, string>();
        string[] files = Directory.GetFiles(IllustFolder, "*.png");
        var idPrefix = new Regex(@"^(\d{3})");

        foreach (var path in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            var m = idPrefix.Match(fileName);
            if (!m.Success) continue;

            int id = int.Parse(m.Groups[1].Value);
            string unityPath = path.Replace('\\', '/');
            byId[id] = unityPath;
        }

        int linked = 0, skippedAlreadySet = 0, missing = 0;

        foreach (var data in db.all)
        {
            if (data == null) continue;

            if (data.icon != null)
            {
                skippedAlreadySet++;
                continue;
            }

            if (!byId.TryGetValue(data.id, out string path))
            {
                Debug.LogWarning($"[ShopIllustrationLinker] id {data.id} ({data.pokemonNameEn}) — 일치하는 일러스트 파일 없음");
                missing++;
                continue;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[ShopIllustrationLinker] {path} — Sprite로 로드 실패(임포트 설정 확인 필요)");
                missing++;
                continue;
            }

            data.icon = sprite;
            EditorUtility.SetDirty(data);
            linked++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ShopIllustrationLinker] 연결 {linked} / 이미 설정됨(건너뜀) {skippedAlreadySet} / 매칭 실패 {missing}");
    }
}
