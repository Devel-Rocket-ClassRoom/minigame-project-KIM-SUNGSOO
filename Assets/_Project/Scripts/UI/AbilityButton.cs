using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KRTD.Abilities;

namespace KRTD.UI
{
    /// <summary>
    /// 특수능력 1개를 시각화하는 UI 버튼.
    /// 클릭 시 SpecialAbilityController.BeginTargeting 을 호출해 조준 모드로 진입한다.
    ///
    /// 인스펙터 구조 권장:
    ///   Button (이 컴포넌트)
    ///   ├─ Icon         (Image - ability.Icon 자동 주입)
    ///   ├─ CooldownMask (Image, Type=Filled, FillMethod=Radial360, FillOrigin=Top)
    ///   └─ CooldownText (TMP_Text - 남은 초)
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AbilityButton : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("이 버튼이 표현/시전할 특수능력.")]
        [SerializeField] private SpecialAbility ability;

        [Header("표시 요소")]
        [Tooltip("ability.Icon 으로 자동 세팅된다. 비워두면 무시.")]
        [SerializeField] private Image iconImage;

        [Tooltip("쿨다운 시 채워지는 마스크. Type=Filled, FillMethod=Radial360 권장.")]
        [SerializeField] private Image cooldownMask;

        [Tooltip("남은 쿨다운 초를 표시할 라벨. 비워두면 숨김.")]
        [SerializeField] private TMP_Text cooldownLabel;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClicked);
        }

        private void OnEnable()
        {
            if (ability != null)
            {
                ability.OnStateChanged += RefreshState;
                if (iconImage != null && ability.Icon != null) iconImage.sprite = ability.Icon;
            }
            RefreshState();
        }

        private void OnDisable()
        {
            if (ability != null) ability.OnStateChanged -= RefreshState;
        }

        private void Update()
        {
            // 쿨다운 진행도/라벨은 매 프레임 갱신 (이벤트는 상태 전이만 알려주므로 부드러운 표시용으로 폴링).
            if (ability == null) return;

            if (cooldownMask != null)
            {
                // 마스크는 쿨다운 동안 가득 채워졌다가 시간이 흐르면 줄어든다.
                cooldownMask.fillAmount = 1f - ability.CooldownProgress01;
            }

            if (cooldownLabel != null)
            {
                float remain = ability.CooldownRemaining;
                if (remain > 0.05f)
                {
                    cooldownLabel.enabled = true;
                    cooldownLabel.text = remain >= 10f
                        ? Mathf.CeilToInt(remain).ToString()
                        : remain.ToString("F1");
                }
                else
                {
                    cooldownLabel.enabled = false;
                }
            }
        }

        private void RefreshState()
        {
            if (button != null && ability != null)
                button.interactable = ability.IsReady;
        }

        private void OnClicked()
        {
            if (ability == null) return;
            var controller = SpecialAbilityController.Instance;
            if (controller == null)
            {
                Debug.LogWarning("[AbilityButton] 씬에 SpecialAbilityController 가 없다.");
                return;
            }
            controller.BeginTargeting(ability);
        }
    }
}
