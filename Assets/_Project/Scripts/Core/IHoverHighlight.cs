using UnityEngine;

/// <summary>
/// 드롭 존·유닛 위에 마우스를 올렸을 때의 강조 표시 방식.
///
/// 표시 방법마다 구현이 다르다(외곽선, 화면 발광 등). SellZone 같은 호출측은
/// "무엇으로 보여줄지"를 모르고 Show/Hide만 부르며, 어떤 구현이 붙어 있는지는
/// 씬 배선이 정한다.
/// </summary>
public interface IHoverHighlight
{
    /// <summary>지정한 색으로 강조를 켠다(판매=빨강 / 교환=하늘 등 상황별 색).</summary>
    void Show(Color color);

    void Hide();
}
