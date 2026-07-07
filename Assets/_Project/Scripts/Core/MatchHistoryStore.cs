using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 전적 파일 입출력 (persistentDataPath/matches.jsonl).
/// 한 판 = 한 줄(JSON Lines) — 통짜 JSON 재작성보다 크래시에 강하고,
/// 추후 서버 업로드도 줄 단위로 밀면 된다.
///
/// 쓰기: MatchRecorder만 호출(Append).
/// 읽기: 인게임 전적 창(UI)이 LoadRecent()로 pull. 실시간 갱신은 GameEvents.OnMatchRecorded 구독.
/// </summary>
public static class MatchHistoryStore
{
    private const string FILE_NAME = "matches.jsonl";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    /// <summary>전적 1건 추가 저장. 실패해도 게임 진행에는 영향 없음(로그만).</summary>
    public static bool Append(MatchRecord record)
    {
        if (record == null) return false;

        try
        {
            File.AppendAllText(FilePath, JsonUtility.ToJson(record) + "\n");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchHistory] 전적 저장 실패: {e.Message} ({FilePath})");
            return false;
        }
    }

    /// <summary>최근 전적을 최신순으로 최대 count건 반환. 파일 없으면 빈 리스트.</summary>
    public static List<MatchRecord> LoadRecent(int count)
    {
        var all = LoadAll();
        int take = Mathf.Clamp(count, 0, all.Count);

        var result = new List<MatchRecord>(take);
        for (int i = all.Count - 1; i >= all.Count - take; i--)
            result.Add(all[i]);
        return result;
    }

    /// <summary>전체 전적(파일 순서 = 오래된 것부터). 깨진 줄은 건너뛴다.</summary>
    public static List<MatchRecord> LoadAll()
    {
        var result = new List<MatchRecord>();
        if (!File.Exists(FilePath)) return result;

        try
        {
            foreach (string line in File.ReadLines(FilePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                MatchRecord record = null;
                try { record = JsonUtility.FromJson<MatchRecord>(line); }
                catch { /* 깨진 줄(부분 기록 등)은 스킵 — 나머지 전적은 살린다 */ }

                if (record != null) result.Add(record);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatchHistory] 전적 로드 실패: {e.Message} ({FilePath})");
        }

        return result;
    }
}
