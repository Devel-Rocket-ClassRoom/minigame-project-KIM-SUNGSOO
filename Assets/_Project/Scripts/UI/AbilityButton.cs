using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KRTD.Abilities;

namespace KRTD.UI
{
    /// <summary>
    /// 특수능력 1개를 시각화하는 UI 버튼.
    /// 클릭 시 SpecialAbilityController 와 통신해 조준 모드 진입/취소를 토글한다.
    ///   - 처음 누름: 조준 모드 진입
    ///   - 같은 버튼 다시 누름: 조준 취소 (모바일에서 ESC/우클릭 대용)
    ///   - 다른 능력 버튼 누름: 그쪽으로 swap
    ///
    /// 인스펙터 구조 권장:
    ///   Button (이 컴포넌트)
    ///   ├─ Icon              (Image - ability.Icon 자동 주입)
    ///   ├─ SelectionHighlight (GameObject - 테두리 이미지, 조준 중에만 활성)
    ///   ├─ CooldownMask      (Image, Type=Filled, FillMethod=Radial360, FillOrigin=Top)
    ///   └─ CooldownText      (TMP_Text - 남은 초)
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

        [Tooltip("이 능력이 조준 중일 때만 활성될 강조 표시 (보통 테두리 이미지 GameObject). 비워두면 무시.")]
        [SerializeField] private GameObject selectionHighlight;

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
            // 처음엔 강조 끄고 시작 (이전 씬 상태에서 켜진 채로 저장돼 있어도 안전하게).
            if (selectionHighlight != null) selectionHighlight.SetActive(false);
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

            // 이 능력이 컨트롤러에서 조준 중이면 강조 표시 ON, 아니면 OFF.
            // 컨트롤러의 CancelTargeting 이 외부에서 호출되거나(능력 시전 완료/취소) 다른 버튼으로 swap 되어도
            // 매 프레임 동기화되므로 별도 이벤트 구독 없이 일관된 상태를 유지한다.
            if (selectionHighlight != null)
            {
                var controller = SpecialAbilityController.Instance;
                bool isPending = controller != null && controller.PendingAbility == ability;
                if (selectionHighlight.activeSelf != isPending) selectionHighlight.SetActive(isPending);
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

            // 같은 능력을 이미 조준 중이면 취소(토글). 모바일에서 ESC/우클릭 대용.
            if (controller.PendingAbility == ability)
                controller.CancelTargeting();
            else
                controller.BeginTargeting(ability);
        }
    }
}
