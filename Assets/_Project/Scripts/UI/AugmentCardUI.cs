using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 증강 카드 1장(AugmentCard_pf). 카드 전체가 하나의 버튼으로 동작한다.
///
/// 클릭 리스너는 OnEnable이 아니라 SetAugment에서 건다. 카드가 비활성 상태로 대기하는 구성이라
/// OnEnable 시점이 데이터 주입 순서와 엇갈릴 수 있기 때문 — 데이터와 리스너를 한 지점에서 묶는다.
/// </summary>
public class AugmentCardUI : MonoBehaviour
{
    [Tooltip("카드 루트의 Button — 카드 전체가 눌리도록 자식 텍스트/이미지는 Raycast Target을 끌 것")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _description;

    private AugmentData _data;
    private Action<AugmentData> _onSelected;

    /// <summary>카드에 증강 데이터를 채우고 클릭 콜백을 연결한다.</summary>
    public void SetAugment(AugmentData data, Action<AugmentData> onSelected)
    {
        _data = data;
        _onSelected = onSelected;

        if (_icon != null)
        {
            _icon.sprite = data.icon;
            _icon.enabled = data.icon != null; // 아이콘 미등록 증강은 흰 사각형이 남지 않게
        }

        if (_title != null)       _title.text = data.augmentName;
        if (_description != null) _description.text = data.description;

        if (_button != null)
        {
            // 오퍼마다 다시 채워지므로 중복 등록을 막고 새로 건다.
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
            _button.interactable = true;
        }

        gameObject.SetActive(true);
    }

    private void HandleClick()
    {
        if (_data == null) return;

        // 연타로 두 번 선택되는 것을 막는다(SelectAugment는 오퍼를 비우지만 같은 프레임 중복은 남는다).
        if (_button != null) _button.interactable = false;

        if (SoundManager.TryGet(out var sm)) sm.PlaySfx(SoundId.AugmentChoice);

        _onSelected?.Invoke(_data);
    }
}
