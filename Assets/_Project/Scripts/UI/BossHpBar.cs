using UnityEngine;
using UnityEngine.UI;
using KRTD.Combat;

namespace KRTD.UI
{
    /// <summary>
    /// 화면 상단 보스 HP 바. Enemy.ActiveBoss 가 있으면 자동으로 보이고, 없거나 죽으면 숨긴다.
    ///
    /// 권장 구성 (Canvas 아래):
    ///   BossHpBarRoot                 ← root 에 할당 (또는 이 컴포넌트가 붙은 GO 자체)
    ///     ├─ Background (Image, dark)
    ///     └─ Fill (Image, red)         ← fillImage 에 할당. Image Type=Filled, Fill Method=Horizontal, Fill Origin=Left
    ///
    /// 매 프레임 Enemy.ActiveBoss.HpRatio 로 fillAmount 갱신.
    /// 보스 등장/사망 시점에만 root 활성/비활성을 토글하므로 idle 비용은 매 프레임 정적 참조 1회 읽기뿐.
    /// </summary>
    public class BossHpBar : MonoBehaviour
    {
        [Tooltip("HP 비율에 따라 fillAmount 가 0~1 로 변하는 Image. " +
            "Image Type=Filled, Fill Method=Horizontal, Fill Origin=Left 로 설정.")]
        [SerializeField] private Image fillImage;

        [Tooltip("표시/숨김을 토글할 root GameObject. 비워두면 이 컴포넌트가 붙은 GameObject 자체를 토글.")]
        [SerializeField] private GameObject root;

        private void Awake()
        {
            if (root == null) root = gameObject;
            // 시작 시점엔 보스 없음 — 무조건 숨김.
            root.SetActive(false);
        }

        private void Update()
        {
            var boss = Enemy.ActiveBoss;
            if (boss == null || boss.IsDead)
            {
                if (root.activeSelf) root.SetActive(false);
                return;
            }

            if (!root.activeSelf) root.SetActive(true);

            if (fillImage != null)
                fillImage.fillAmount = Mathf.Clamp01(boss.HpRatio);
        }
    }
}
