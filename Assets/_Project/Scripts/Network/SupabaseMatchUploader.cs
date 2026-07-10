using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 전적 Phase 2 — Supabase 업로드 (스키마: Docs/SCHEMA_2026-07-10_supabase-matches.sql).
///
/// 흐름:
///  1. Start에서 세션 확보 — 저장된 refresh_token이 있으면 갱신, 없으면 익명 가입.
///     (기기당 익명 계정 1개. PlayerPrefs에 refresh_token 보관 → 재실행해도 같은 유저)
///  2. GameEvents.OnMatchRecorded 구독 → 로컬 jsonl 기록 직후 REST로 업로드.
///  3. 닉네임은 세션 확보 시 profiles에 upsert (현재 NetworkManager.LocalNickname).
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

    private string _accessToken = "";
    private string _userId      = "";

    public bool HasSession => !string.IsNullOrEmpty(_accessToken);

    private void OnEnable()  => GameEvents.OnMatchRecorded += HandleMatchRecorded;
    private void OnDisable() => GameEvents.OnMatchRecorded -= HandleMatchRecorded;

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
        string nickname = net != null ? net.LocalNickname : "";

        string body = "[{\"id\":\"" + _userId + "\",\"nickname\":" + JsonString(nickname) + "}]";
        using var req = BuildJsonPost($"{_projectUrl}/rest/v1/profiles?on_conflict=id", body, authed: true);
        req.SetRequestHeader("Prefer", "resolution=merge-duplicates");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
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
}
