using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 전적 Phase 2+3 — Supabase 업로드/조회 (스키마: Docs/SCHEMA_2026-07-10_supabase-matches.sql).
///
/// 흐름:
///  1. Start에서 세션 확보 — 저장된 refresh_token이 있으면 갱신, 없으면 익명 가입.
///     (기기당 익명 계정 1개. PlayerPrefs에 refresh_token 보관 → 재실행해도 같은 유저)
///  2. GameEvents.OnMatchRecorded 구독 → 로컬 jsonl 기록 직후 REST로 업로드.
///  3. 닉네임은 세션 확보 시 profiles에 upsert (현재 NetworkManager.LocalNickname).
///  4. (Phase 3) GameEvents.OnServerMatchesRequested 구독 → 내 전적을 최신순 조회해
///     GameEvents.ServerMatchesLoaded로 회신. UI와는 이벤트로만 통신(직접 참조 금지 규칙).
///
/// 설계 결정:
///  - SDK 없이 UnityWebRequest만 사용 (의존성/유지보수 최소화, 인수인계 고려).
///  - 업로드 실패는 로그만 남기고 게임 진행에 영향 없음 (로컬 jsonl이 원본,
///    서버는 사본). 중복 업로드는 DB unique(user_id, match_id)가 거른다.
///  - anon key는 클라이언트 노출 전제 키 (RLS가 방어선) — 인스펙터 값이 소스.
/// </summary>
public class SupabaseMatchUploader : MonoBehaviour
{
    [Header("Supabase (Settings > API)")]
    [SerializeField] private string _projectUrl = "";   // https://xxxx.supabase.co
    [SerializeField] private string _anonKey    = "";

    private const string PREFS_REFRESH_TOKEN = "supabase_refresh_token";

    private string _accessToken       = "";
    private string _userId            = "";
    private string _upsertedNickname; // 마지막으로 서버에 반영한 닉네임 (중복 upsert 방지)

    public bool HasSession => !string.IsNullOrEmpty(_accessToken);

    private void OnEnable()
    {
        GameEvents.OnMatchRecorded          += HandleMatchRecorded;
        GameEvents.OnServerMatchesRequested += HandleServerMatchesRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchRecorded          -= HandleMatchRecorded;
        GameEvents.OnServerMatchesRequested -= HandleServerMatchesRequested;
    }

    private void Start()
    {
        // 인스펙터 붙여넣기 시 섞여 들어오는 공백/개행 방어
        _projectUrl = (_projectUrl ?? "").Trim().TrimEnd('/');
        _anonKey    = (_anonKey ?? "").Trim();

        if (string.IsNullOrEmpty(_projectUrl) || string.IsNullOrEmpty(_anonKey))
        {
            Debug.LogWarning("[Supabase] URL/anon key 미설정 — 전적 업로드 비활성 (로컬 jsonl만 기록)");
            return;
        }
        StartCoroutine(EnsureSession());
    }

    // ---------------- 세션 ----------------

    private IEnumerator EnsureSession()
    {
        string refreshToken = PlayerPrefs.GetString(PREFS_REFRESH_TOKEN, "");

        if (!string.IsNullOrEmpty(refreshToken))
        {
            yield return AuthRequest(
                $"{_projectUrl}/auth/v1/token?grant_type=refresh_token",
                $"{{\"refresh_token\":\"{refreshToken}\"}}");
            if (HasSession) { yield return UpsertProfile(); yield break; }
            Debug.Log("[Supabase] 세션 갱신 실패 — 익명 계정 새로 생성");
        }

        // 익명 가입 (대시보드에서 Anonymous sign-ins 활성화 필요)
        yield return AuthRequest($"{_projectUrl}/auth/v1/signup", "{}");
        if (HasSession) yield return UpsertProfile();
        else Debug.LogWarning("[Supabase] 익명 로그인 실패 — 이번 세션은 로컬 기록만 유지");
    }

    /// <summary>인증 요청 공통 처리 — 성공 시 토큰/유저ID 저장.</summary>
    private IEnumerator AuthRequest(string url, string jsonBody)
    {
        using var req = BuildJsonPost(url, jsonBody, authed: false);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[Supabase] 인증 실패 ({req.responseCode}): {req.downloadHandler.text}");
            yield break;
        }

        var session = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
        if (session == null || string.IsNullOrEmpty(session.access_token)) yield break;

        _accessToken = session.access_token;
        _userId      = session.user != null ? session.user.id : "";
        PlayerPrefs.SetString(PREFS_REFRESH_TOKEN, session.refresh_token);
        PlayerPrefs.Save();
        Debug.Log($"[Supabase] 세션 확보 (user {_userId[..Mathf.Min(8, _userId.Length)]}…)");
    }

    private IEnumerator UpsertProfile()
    {
        var net = GameManager.Instance != null ? GameManager.Instance.Network : null;
        yield return UpsertProfile(net != null ? net.LocalNickname : "");
    }

    /// <summary>
    /// profiles에 닉네임 반영. 세션 확보 직후 + 전적 업로드 시점에 호출된다 —
    /// 세션 시작 때는 로비 입력 전이라 임시값(Player_xxxx)일 수 있어서,
    /// 실제 판에 쓰인 닉네임(self_record 기준)으로 따라잡는 구조.
    /// </summary>
    private IEnumerator UpsertProfile(string nickname)
    {
        nickname = (nickname ?? "").Trim();
        if (nickname.Length == 0 || nickname == _upsertedNickname) yield break;

        string body = "[{\"id\":\"" + _userId + "\",\"nickname\":" + JsonString(nickname) + "}]";
        using var req = BuildJsonPost($"{_projectUrl}/rest/v1/profiles?on_conflict=id", body, authed: true);
        req.SetRequestHeader("Prefer", "resolution=merge-duplicates");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            _upsertedNickname = nickname;
        else
            Debug.LogWarning($"[Supabase] 프로필 upsert 실패 ({req.responseCode}): {req.downloadHandler.text}");
    }

    // ---------------- 업로드 ----------------

    private void HandleMatchRecorded(MatchRecord record)
    {
        if (record == null) return;
        if (!HasSession)
        {
            Debug.Log("[Supabase] 세션 없음 — 업로드 생략 (로컬 jsonl에는 기록됨)");
            return;
        }
        StartCoroutine(UploadMatch(record));
    }

    private IEnumerator UploadMatch(MatchRecord r)
    {
        // 판에 실제 쓰인 닉네임으로 프로필 최신화 (로비 입력이 세션 시작보다 늦는 경우 대비)
        if (r.self != null) yield return UpsertProfile(r.self.nickname);

        string body = BuildMatchRow(r);
        // unique(user_id, match_id) 충돌 시 409 대신 무시 — 재시도/중복 안전
        string url = $"{_projectUrl}/rest/v1/matches?on_conflict=user_id,match_id";
        using var req = BuildJsonPost(url, "[" + body + "]", authed: true);
        req.SetRequestHeader("Prefer", "resolution=ignore-duplicates");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[Supabase] 전적 업로드 완료: {r.result} ({r.matchId})");
        else
            Debug.LogWarning($"[Supabase] 전적 업로드 실패 ({req.responseCode}): {req.downloadHandler.text}");
    }

    /// <summary>MatchRecord → matches 테이블 1행(JSON). 컬럼은 snake_case, 플레이어 기록은 jsonb.</summary>
    private string BuildMatchRow(MatchRecord r)
    {
        var sb = new StringBuilder(1024);
        sb.Append('{');
        sb.Append("\"user_id\":").Append(JsonString(_userId)).Append(',');
        sb.Append("\"schema_version\":").Append(r.schemaVersion).Append(',');
        sb.Append("\"match_id\":").Append(JsonString(r.matchId)).Append(',');
        sb.Append("\"game_version\":").Append(JsonString(r.gameVersion ?? "")).Append(',');
        sb.Append("\"started_at\":").Append(JsonStringOrNull(r.startedAtUtc)).Append(',');
        sb.Append("\"ended_at\":").Append(JsonStringOrNull(r.endedAtUtc)).Append(',');
        sb.Append("\"duration_seconds\":").Append(r.durationSeconds).Append(',');
        sb.Append("\"result\":").Append(JsonString(r.result)).Append(',');
        sb.Append("\"end_reason\":").Append(JsonString(r.endReason ?? "")).Append(',');
        sb.Append("\"final_round\":").Append(r.finalRound).Append(',');
        sb.Append("\"final_stage_id\":").Append(JsonString(r.finalStageId ?? "")).Append(',');
        sb.Append("\"self_record\":").Append(r.self != null ? JsonUtility.ToJson(r.self) : "null").Append(',');
        sb.Append("\"partner_record\":").Append(r.partner != null ? JsonUtility.ToJson(r.partner) : "null");
        sb.Append('}');
        return sb.ToString();
    }

    // ---------------- 조회 (Phase 3) ----------------

    private void HandleServerMatchesRequested(int count)
    {
        if (!HasSession)
        {
            GameEvents.ServerMatchesLoaded(null, "서버 세션 없음 — 오프라인이거나 인증 실패 (로컬 전적을 확인하세요)");
            return;
        }
        StartCoroutine(FetchRecentMatches(Mathf.Clamp(count, 1, 100)));
    }

    /// <summary>내 전적을 종료 시각 최신순으로 최대 count건 조회 → ServerMatchesLoaded 발행.</summary>
    private IEnumerator FetchRecentMatches(int count)
    {
        // RLS(user_id = auth.uid())가 서버 방어선이지만, 명시 필터로 의도를 드러낸다.
        string url = $"{_projectUrl}/rest/v1/matches" +
                     $"?user_id=eq.{_userId}&order=ended_at.desc.nullslast&limit={count}";

        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("apikey", _anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + _accessToken);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[Supabase] 전적 조회 실패 ({req.responseCode}): {req.downloadHandler.text}");
            GameEvents.ServerMatchesLoaded(null, $"서버 전적 조회 실패 ({req.responseCode})");
            yield break;
        }

        List<MatchRecord> records = ParseMatchRows(req.downloadHandler.text);
        Debug.Log($"[Supabase] 전적 조회 완료: {records.Count}건");
        GameEvents.ServerMatchesLoaded(records, null);
    }

    /// <summary>PostgREST 응답(JSON 배열) → MatchRecord 목록. JsonUtility는 최상위 배열을 못 읽어 래퍼로 감싼다.</summary>
    private static List<MatchRecord> ParseMatchRows(string json)
    {
        var result = new List<MatchRecord>();
        MatchRowArray wrapper;
        try
        {
            wrapper = JsonUtility.FromJson<MatchRowArray>("{\"rows\":" + json + "}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Supabase] 전적 응답 파싱 실패: {e.Message}");
            return result;
        }
        if (wrapper?.rows == null) return result;

        foreach (var row in wrapper.rows)
        {
            if (row == null || string.IsNullOrEmpty(row.match_id)) continue;
            result.Add(new MatchRecord
            {
                schemaVersion   = row.schema_version,
                matchId         = row.match_id,
                gameVersion     = row.game_version,
                startedAtUtc    = row.started_at,
                endedAtUtc      = row.ended_at,
                durationSeconds = row.duration_seconds,
                result          = row.result,
                endReason       = row.end_reason,
                finalRound      = row.final_round,
                finalStageId    = row.final_stage_id,
                self            = row.self_record,
                partner         = row.partner_record,
            });
        }
        return result;
    }

    // ---------------- 공통 ----------------

    private UnityWebRequest BuildJsonPost(string url, string jsonBody, bool authed)
    {
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("apikey", _anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + (authed ? _accessToken : _anonKey));
        return req;
    }

    private static string JsonString(string s)
        => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string JsonStringOrNull(string s)
        => string.IsNullOrEmpty(s) ? "null" : JsonString(s);

    // ---------------- 응답 DTO (JsonUtility용) ----------------

    [Serializable] private class AuthResponse
    {
        public string access_token;
        public string refresh_token;
        public AuthUser user;
    }

    [Serializable] private class AuthUser
    {
        public string id;
    }

    /// <summary>matches 테이블 행(snake_case). self/partner jsonb는 업로드 때 JsonUtility로 쓴 그대로라 PlayerRecord로 역직렬화된다.</summary>
    [Serializable] private class MatchRow
    {
        public int schema_version;
        public string match_id;
        public string game_version;
        public string started_at;
        public string ended_at;
        public int duration_seconds;
        public string result;
        public string end_reason;
        public int final_round;
        public string final_stage_id;
        public PlayerRecord self_record;
        public PlayerRecord partner_record;
    }

    [Serializable] private class MatchRowArray
    {
        public MatchRow[] rows;
    }
}
