using UnityEngine;
using UnityEngine.UI;
using KRTD.Combat;

namespace KRTD.UI
{
    /// <summary>
    /// "다음 웨이브 즉시 시작" 버튼.
    /// WaveDirector 가 갭 대기 중일 때만 활성화되며, 클릭하면 다음 웨이브를 즉시 시작시킨다.
    /// 스킵 보상(쿨다운 단축 등)은 AbilityCooldownReward 가 별도로 처리한다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SendNextWaveButton : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("호출할 WaveDirector. 비워두면 씬에서 자동 탐색.")]
        [SerializeField] private WaveDirector director;

        [Header("표시 정책")]
        [Tooltip("갭 대기 중이 아닐 때 버튼 자체를 숨길지 여부. 끄면 비활성(회색) 상태로 남는다.")]
        [SerializeField] private bool hideWhenUnavailable = true;

        [Tooltip("버튼이 사용 불가일 때 켜둘 자식 그래픽 (있다면). 비워두면 무시.")]
        [SerializeField] private GameObject disabledOverlay;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClicked);

            if (director == null) director = FindObjectOfType<WaveDirector>();
        }

        private void Update()
        {
            if (director == null) return;

            bool available = director.IsWaitingBetweenWaves;

            if (hideWhenUnavailable)
            {
                if (gameObject.activeSelf != available)
                    gameObject.SetActive(available);
            }
            else
            {
                if (button.interactable != available)
                    button.interactable = available;
                if (disabledOverlay != null && disabledOverlay.activeSelf == available)
                    disabledOverlay.SetActive(!available);
            }
        }

        private void OnClicked()
        {
            if (director == null) return;
            director.RequestSkipToNextWave();
        }
    }
}
