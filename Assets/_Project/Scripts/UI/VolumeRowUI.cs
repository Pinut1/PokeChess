using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 옵션창 음향 페이지의 볼륨 한 줄(VolumeRow_Pf). <b>뷰 전용</b>이다.
///
/// 프리팹 구조:
///   VolumeRow_Pf
///     VolumeLabel      "마스터 볼륨" 같은 항목 이름  (코드가 안 건드린다 — 연결할 필요 없음)
///     VolumeSlider     0~1 슬라이더                  ← Slider
///     VolumeValueText  "100%" 표시                   ← Value Text
///
/// 채널 구분(마스터/배경음/효과음)은 <b>이 컴포넌트가 모른다</b> — SoundManager 호출도 PlayerPrefs
/// 키도 전부 <see cref="OptionsPanelUI"/>에 남는다. 행이 채널을 알면 "마스터 전용 행"이 되어버려
/// 프리팹으로 묶은 의미가 사라지기 때문이다. 여기서 하는 일은 딱 둘이다 —
/// 슬라이더 범위를 0~1로 맞추는 것, 값이 바뀌면 % 텍스트를 따라 갱신하는 것.
///
/// 항목 이름은 코드로 넣지 않는다. 세 인스턴스(MasterVolume·BgmVolume·SfxVolume)가 각자
/// VolumeLabel에 문구를 들고 있는 편이 인스펙터에서 보고 고치기 쉽다.
/// </summary>
public class VolumeRowUI : MonoBehaviour
{
    [Tooltip("볼륨 슬라이더(VolumeSlider). Initialize에서 0~1 범위로 맞춘다.")]
    [SerializeField] private Slider _slider;

    [Tooltip("현재값 표시(VolumeValueText). 슬라이더가 움직이면 자동으로 따라간다.")]
    [SerializeField] private TMP_Text _valueText;

    // Initialize로 받은 바깥 동작(채널별 볼륨 적용). 행 자신은 이게 무슨 채널인지 모른다.
    private UnityAction<float> _onValueChanged;

    /// <summary>
    /// 슬라이더 범위를 맞추고 값 변경 콜백을 연결한다.
    /// <para>
    /// Awake에서 스스로 하지 않고 바깥이 불러주게 한 이유 — 이 행은 Sound_Page 아래에 있고,
    /// Sound_Page는 탭 전환으로 꺼질 수 있으며 OptionsPanel 자체도 닫힌 채로 저장될 수 있다.
    /// 비활성 상태로 시작하면 Awake가 아예 돌지 않아 슬라이더 범위가 기본값(0~1이 아닐 수 있음)으로
    /// 남는다. OptionsPanelUI.Awake에서 명시적으로 불러주면 활성 여부와 무관하게 초기화된다.
    /// </para>
    /// </summary>
    public void Initialize(UnityAction<float> onValueChanged)
    {
        _onValueChanged = onValueChanged;

        if (_slider == null)
        {
            Debug.LogWarning($"[VolumeRowUI] '{name}'에 VolumeSlider가 연결되지 않았습니다 — 이 행은 동작하지 않습니다.", this);
            return;
        }

        _slider.minValue = 0f;
        _slider.maxValue = 1f;
        _slider.wholeNumbers = false;

        // 두 번 불려도 리스너가 겹치지 않게 한 번 떼고 붙인다.
        _slider.onValueChanged.RemoveListener(HandleSliderValueChanged);
        _slider.onValueChanged.AddListener(HandleSliderValueChanged);

        UpdateValueText(_slider.value);
    }

    /// <summary>
    /// 슬라이더 값을 콜백 없이 밀어넣는다(패널을 열 때·취소로 되돌릴 때).
    /// SetValueWithoutNotify는 onValueChanged를 타지 않으므로 % 텍스트는 여기서 직접 갱신한다.
    /// </summary>
    public void SetValueWithoutNotify(float value)
    {
        if (_slider != null) _slider.SetValueWithoutNotify(value);
        UpdateValueText(value);
    }

    private void HandleSliderValueChanged(float value)
    {
        UpdateValueText(value);
        _onValueChanged?.Invoke(value);
    }

    private void UpdateValueText(float value)
    {
        if (_valueText == null) return;
        _valueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
