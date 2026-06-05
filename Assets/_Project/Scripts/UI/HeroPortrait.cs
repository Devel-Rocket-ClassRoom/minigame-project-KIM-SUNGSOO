using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KRTD.Combat;

namespace KRTD.UI
{
    /// <summary>
    /// 좌상단 영웅 위젯. Hero.Instance 를 폴링해서 HP 바와 부활 카운트다운을 갱신한다.
    /// 위젯 자체(또는 자식 Button)가 클릭되면 HeroPathRallyController 의 조준 모드 진입.
    ///
    /// 권장 구성 (HUD Canvas 좌상단):
    ///   HeroPortrait (이 컴포넌트 + 자식 Button)
    ///     ├─ Frame      (Image, 초상화 프레임/배경)
    ///     ├─ Portrait   (Image, 영웅 얼굴)
    ///     ├─ HpBar
    ///     │    ├─ Background (Image)
    ///     │    └─ Fill       (Image, Filled Horizontal Left) ← fillImage 에 할당
    ///     ├─ DeathOverlay (Image, 회색 반투명, 부활 중에만 보임) ← deathOverlay 에 할당
    ///     └─ CountdownText (TMP_Text, 부활 남은 초)              ← countdownText 에 할당
    /// </summary>
    public class HeroPortrait : MonoBehaviour
    {
        [Tooltip("HP 비율에 따라 fillAmount 가 0~1 로 변하는 Image. " +
            "Image Type=Filled, Fill Method=Horizontal, Fill Origin=Left.")]
        [SerializeField] private Image fillImage;

        [Tooltip("부활 중일 때 표시할 회색 오버레이. 살아있으면 비활성. 비워도 됨.")]
        [SerializeField] private GameObject deathOverlay;

        [Tooltip("부활 남은 시간을 표시할 TMP 텍스트. 비워도 됨.")]
        [SerializeField] private TMP_Text countdownText;

        [Tooltip("부활 시간 표시 포맷. {0} = 남은 초(정수).")]
        [SerializeField] private string countdownFormat = "{0}";

        [Tooltip("초상화 클릭 영역의 Button. OnClick 에 OnPortraitClick 을 연결해야 한다. " +
            "비워두면 자기 자신 GameObject 의 Button 을 사용.")]
        [SerializeField] private Button clickButton;

        private void Awake()
        {
            if (clickButton == null) clickButton = GetComponent<Button>();
            if (clickButton != null) clickButton.onClick.AddListener(OnPortraitClick);
        }

        private void OnDestroy()
        {
            if (clickButton != null) clickButton.onClick.RemoveListener(OnPortraitClick);
        }

        private void Update()
        {
            var hero = Hero.Instance;

            // 영웅이 아직 스폰되지 않았으면 일단 가만히 둔다 (게임 시작 직후 1프레임 등).
            if (hero == null) return;

            // HP 바
            if (fillImage != null)
                fillImage.fillAmount = hero.HpRatio;

            // 사망/부활 표시
            bool dead = hero.IsDead;
            if (deathOverlay != null && deathOverlay.activeSelf != dead)
                deathOverlay.SetActive(dead);

            if (countdownText != null)
            {
                if (dead)
                {
                    int seconds = Mathf.CeilToInt(hero.RespawnRemaining);
                    countdownText.text = string.Format(countdownFormat, seconds);
                    if (!countdownText.gameObject.activeSelf) countdownText.gameObject.SetActive(true);
                }
                else if (countdownText.gameObject.activeSelf)
                {
                    countdownText.gameObject.SetActive(false);
                }
            }

            // 죽은 동안엔 클릭 비활성화 — 조준 시작 불가.
            if (clickButton != null && clickButton.interactable == dead)
                clickButton.interactable = !dead;
        }

        /// <summary>버튼 클릭 핸들러. HeroPathRallyController 조준 모드 토글.</summary>
        public void OnPortraitClick()
        {
            var hero = Hero.Instance;
            if (hero == null || hero.IsDead) return;

            var controller = HeroPathRallyController.Instance;
            if (controller == null)
            {
                Debug.LogWarning("[HeroPortrait] HeroPathRallyController 가 씬에 없습니다.");
                return;
            }

            // 이미 조준 중이면 토글(취소). 아니면 진입.
            if (controller.IsTargeting) controller.CancelTargeting();
            else controller.BeginTargeting(hero);
        }
    }
}
