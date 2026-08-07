using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Art/SFX/pokemon_voice의 울음소리 파일을 PokemonData.voiceClip에 일괄 연결하는 1회성 툴.
/// ShopIllustrationLinker(카드 일러스트/유닛 아이콘 연결)와 같은 구조 — PokeChessImporter의 JSON
/// 재임포트와는 별개다. voiceClip은 임포터가 보호하는(덮어쓰지 않는) 수동 필드라 이 메뉴로 따로 실행한다.
/// 이미 채워진 항목은 건너뛰므로 손으로 물린 예외는 살아남고, Import를 다시 돌려도 값이 유지된다.
///
/// 파일명 규칙(실제 조사 결과, 추측 아님): "{도감번호}_{영문명(공백/기호 제거)}_voice.ogg"
/// 예: "1_Bulbasaur_voice.ogg", "122_Mrmime_voice.ogg"(Mr. Mime), "439_Mimejr_voice.ogg"(Mime Jr.).
/// 도감번호는 3자리 고정폭이 아니다(ShopIllustrationLinker의 "\d{3}" 규칙과 다름 — 예: "6_Charizard").
///
/// 매칭 키로 도감번호 대신 정규화된 영문명을 쓰는 이유: 실제 폴더에 "311_Plusle_voice.ogg"와
/// "311_Minun_voice.ogg"가 도감번호가 동일(311)한 채 공존한다(음원 파일명 자체의 오기로 보임 —
/// 실제 Minun의 도감번호는 312). 도감번호만으로 매칭하면 Dictionary 키 충돌로 둘 중 하나가
/// 조용히 다른 포켓몬에 잘못 연결된다. 파일명의 영문명 부분은 PokemonData.pokemonNameEn과
/// 공백/구두점 제거 기준으로 100% 1:1 대응함을 실제 파일 140개 전수 대조로 확인했으므로,
/// 이름 정규화 매칭이 이 데이터셋에서는 더 안전하다.
/// </summary>
public static class PokemonVoiceLinker
{
    private const string VoiceFolder = "Assets/Art/SFX/pokemon_voice";
    private const string PokemonDbPath = "Assets/Resources/PokemonDatabase.asset";

    private static readonly Regex FileNamePattern =
        new(@"^(\d+)_(.+)_voice$", RegexOptions.IgnoreCase);

    [MenuItem("PokeChess/Link Pokemon Voices")]
    public static void LinkVoices()
    {
        var db = AssetDatabase.LoadAssetAtPath<PokemonDatabase>(PokemonDbPath);
        if (db == null || db.all == null)
        {
            Debug.LogError($"[PokemonVoiceLinker] {PokemonDbPath} 없음 — Import Pokemon JSON 먼저 실행하세요.");
            return;
        }

        if (!Directory.Exists(VoiceFolder))
        {
            Debug.LogError($"[PokemonVoiceLinker] {VoiceFolder} 폴더가 없습니다.");
            return;
        }

        // 정규화된 영문명 → 파일 경로. 같은 정규화명이 중복되면 마지막 파일로 덮어쓰되 경고를 남긴다
        // (도감번호 오기 등으로 실제 있었던 311_Plusle/311_Minun 같은 경우는 이름이 서로 달라
        // 여기 충돌에 해당하지 않는다 — 이건 정말 동일한 이름이 두 번 나온 이상 상황 전용 경고).
        var byName = new Dictionary<string, string>();

        foreach (var path in Directory.GetFiles(VoiceFolder, "*.ogg"))
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            var m = FileNamePattern.Match(fileName);
            if (!m.Success)
            {
                Debug.LogWarning($"[PokemonVoiceLinker] 파일명 규칙과 불일치 — 건너뜀: {fileName}");
                continue;
            }

            string normalizedName = Normalize(m.Groups[2].Value);
            if (normalizedName.Length == 0) continue;

            if (byName.ContainsKey(normalizedName))
                Debug.LogWarning($"[PokemonVoiceLinker] 정규화명 '{normalizedName}' 파일 중복 — 나중 파일로 덮어씀 " +
                                  $"({byName[normalizedName]} → {path})");

            byName[normalizedName] = path.Replace('\\', '/');
        }

        if (byName.Count == 0)
        {
            Debug.LogWarning($"[PokemonVoiceLinker] {VoiceFolder}에서 규칙에 맞는 .ogg를 못 찾았습니다.");
            return;
        }

        int linked = 0, skippedAlreadySet = 0, missing = 0;
        var missingList = new List<string>();

        foreach (var data in db.all)
        {
            if (data == null) continue;

            if (data.voiceClip != null)
            {
                skippedAlreadySet++;
                continue;
            }

            string key = Normalize(data.pokemonNameEn);

            if (!byName.TryGetValue(key, out string path))
            {
                Debug.LogWarning($"[PokemonVoiceLinker] id {data.id} ({data.pokemonNameEn}) — 일치하는 음원 없음");
                missingList.Add($"{data.id} {data.pokemonNameEn}");
                missing++;
                continue;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"[PokemonVoiceLinker] {path} — AudioClip으로 로드 실패");
                missingList.Add($"{data.id} {data.pokemonNameEn} (파일은 있으나 로드 실패: {path})");
                missing++;
                continue;
            }

            data.voiceClip = clip;
            EditorUtility.SetDirty(data);
            linked++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[PokemonVoiceLinker] 연결 {linked} / 이미 설정됨(건너뜀) {skippedAlreadySet} / 매칭 실패 {missing}" +
                  (missingList.Count > 0 ? $"\n매칭 실패 목록: {string.Join(", ", missingList)}" : ""));
    }

    /// <summary>영문 알파벳/숫자만 남기고 소문자화. "Mr. Mime"→"mrmime", "Plusle&Minun"→"plusleminun".</summary>
    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));

        return sb.ToString();
    }
}
