using System;

/// <summary>
/// 견본덱 화면이 시너지 문자열을 SynergyData로 바꿀 때 쓰는 공용 조회.
///
/// 시너지가 문자열로 들어오는 곳이 두 군데다 — 견본덱의 활성 시너지(<c>"Bug(4/4)"</c>)와
/// 포켓몬의 보유 시너지(<c>"Grass"</c>). 둘 다 <b>폐기 별칭 정규화</b>를 거쳐야 하는데,
/// 규칙이 흩어지면 한쪽만 고치는 사고가 난다(실제로 <c>Cheerleader</c> 때문에 고유 시너지가
/// 화면에서 통째로 사라진 적이 있다).
/// </summary>
public static class SampleDeckSynergyLookup
{
    /// <summary>
    /// 시너지 문자열로 SynergyData를 찾는다. <c>"Bug(4/4)"</c>처럼 괄호가 붙어 있어도 되고,
    /// 폐기 별칭도 알아서 정규화한다. 못 찾으면 null.
    /// </summary>
    public static SynergyData Find(string synergyText)
    {
        string key = ExtractKey(synergyText);
        if (string.IsNullOrEmpty(key)) return null;

        SynergyDatabase db = SynergyDatabase.Instance;
        return db != null ? db.GetByKey(key) : null;
    }

    /// <summary>화면에 쓸 이름. 한글명 → 영문명 → 원본 문자열 순으로 폴백한다.</summary>
    public static string DisplayName(SynergyData data, string fallback)
    {
        if (data == null) return fallback;

        if (!string.IsNullOrWhiteSpace(data.synergyName)) return data.synergyName;
        if (!string.IsNullOrWhiteSpace(data.synergyNameEn)) return data.synergyNameEn;

        return fallback;
    }

    /// <summary>"Bug(4/4)"에서 "Bug"만 뽑고 폐기 별칭을 정규화한다. 괄호가 없으면 원본을 그대로 쓴다.</summary>
    public static string ExtractKey(string synergyText)
    {
        if (string.IsNullOrWhiteSpace(synergyText)) return "";

        int open = synergyText.IndexOf('(');
        string key = open < 0 ? synergyText.Trim() : synergyText.Substring(0, open).Trim();

        return Normalize(key);
    }

    /// <summary>"Bug(4/4)"에서 현재 유닛 수 4를 뽑는다. 형식이 다르면 0.</summary>
    public static int ExtractCurrentCount(string synergyText)
    {
        if (string.IsNullOrWhiteSpace(synergyText)) return 0;

        int open = synergyText.IndexOf('(');
        int slash = synergyText.IndexOf('/');

        if (open < 0 || slash < 0 || slash <= open + 1) return 0;

        string countText = synergyText.Substring(open + 1, slash - open - 1);
        return int.TryParse(countText, out int count) ? count : 0;
    }

    /// <summary>
    /// 폐기된 시너지 이름을 현재 이름으로 바꾼다(skillId 규약 v11: CHEERLEADER→CHEER, FAIRY→ETHEREAL).
    ///
    /// ⚠️ 여기 정규화는 견본덱 화면만 구한다. 같은 문자열을 쓰는 다른 곳도 있으므로
    /// 근본 해결은 시트를 고치거나 임포터가 정규화하도록 하는 것이다.
    /// </summary>
    private static string Normalize(string key)
    {
        if (key.Equals("Cheerleader", StringComparison.OrdinalIgnoreCase)) return "Cheer";
        if (key.Equals("Fairy", StringComparison.OrdinalIgnoreCase)) return "Ethereal";

        return key;
    }
}
