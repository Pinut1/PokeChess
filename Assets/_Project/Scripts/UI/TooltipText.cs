using System.Text;
using UnityEngine;

/// <summary>
/// 설명창 문구를 만들 때 쓰는 공용 조각. 안내 툴팁(<see cref="TextTooltipTrigger"/>)과
/// XP 구매 툴팁(<see cref="XpPurchaseTooltipTrigger"/>)이 제목/본문/꼬리를 잇고 단축키를
/// 끼워 넣는 방식이 같아서, 양쪽에 복사돼 있던 것을 여기로 모았다
/// (2026-08 코드리뷰 지적 — 대괄호 처리 같은 것을 두 곳에서 따로 고쳐야 했다).
/// </summary>
public static class TooltipText
{
    /// <summary>
    /// 단축키 표기를 만든다. 예: key="F", format="[{0}]" → "[F]".
    ///
    /// 단축키가 없으면 <b>빈 문자열</b>이다 — 대괄호까지 통째로 사라진다. 없는 키를 "[]"로
    /// 남겨두면 오히려 눈에 걸린다. 그래서 문구에는 대괄호 없이 {key}만 쓰고, 감싸는 모양은
    /// format이 전담한다. 문구에 "[{key}]"라고 쓰면 "[[F]]"가 되니 주의.
    /// </summary>
    public static string KeyLabel(string key, string format)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        return string.IsNullOrEmpty(format) ? key : string.Format(format, key);
    }

    /// <summary>제목·본문·꼬리 사이에 넣을 구분자(줄바꿈 n개). 1이면 다음 줄, 2면 한 줄 띄운다.</summary>
    public static string Separator(int blankLines) => new('\n', Mathf.Clamp(blankLines, 1, 3));

    /// <summary>
    /// 빈 칸은 통째로 건너뛰며 이어 붙인다. 제목이나 꼬리를 비워두면 그 줄이 아예 없어진다.
    /// 꼬리 공백도 정리한다 — 단축키가 빠지면 "XP 구매 " 처럼 남는다.
    /// </summary>
    public static void Append(StringBuilder sb, string part, string separator)
    {
        if (string.IsNullOrWhiteSpace(part)) return;

        if (sb.Length > 0) sb.Append(separator);
        sb.Append(part.TrimEnd());
    }
}
