using System.Collections.Generic;
using UnityEngine;
using KRTD.Combat;

namespace KRTD.UI
{
    /// <summary>
    /// 갭 시작 시 다음 웨이브의 pathId 들을 읽어, 해당 스포너 위치 위에
    /// WaveCallButton 을 N개 띄워준다. 아무 버튼이나 클릭되면 WaveDirector 에 스킵 요청 + 전체 정리.
    /// 갭이 자연 종료되어도 자동 정리.
    /// </summary>
    public class WaveCallButtonSpawner : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("이벤트를 구독할 WaveDirector. 비워두면 씬에서 자동 탐색.")]
        [SerializeField] private WaveDirector director;

        [Tooltip("버튼들이 자식으로 붙을 부모 RectTransform. 보통 Screen Space Overlay Canvas 또는 그 자식.")]
        [SerializeField] private RectTransform buttonParent;

        [Tooltip("월드 좌표 변환에 사용할 카메라. 비워두면 Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("인스턴스화할 WaveCallButton prefab.")]
        [SerializeField] private WaveCallButton buttonPrefab;

        [Header("위치 보정")]
        [Tooltip("스폰 위치 기준 추가 월드 오프셋 (스폰 지점 위에 살짝 띄우기 등).")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.6f, 0f);

        private readonly List<WaveCallButton> active = new List<WaveCallButton>();

        private void Awake()
        {
            if (director == null) director = FindFirstObjectByType<WaveDirector>();
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (director != null)
            {
                director.OnGapStarted += HandleGapStarted;
                director.OnGapEnded += HandleGapEnded;
            }
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.OnGapStarted -= HandleGapStarted;
                director.OnGapEnded -= HandleGapEnded;
            }
            ClearButtons();
        }

        private void HandleGapStarted(WaveData nextWave)
        {
            ClearButtons();

            if (nextWave == null || buttonPrefab == null || buttonParent == null) return;

            // 다음 웨이브의 distinct pathId 수집 (entries 중 빈 값은 fallback 스포너로 간주).
            var seen = new HashSet<string>();
            foreach (var entry in nextWave.entries)
            {
                if (entry == null || entry.enemy == null || entry.count <= 0) continue;
                var id = entry.pathId ?? "";
                if (!seen.Add(id)) continue;

                if (!director.TryGetSpawner(id, out var spawner) || spawner == null || spawner.Path == null)
                {
                    // pathId 가 빈 문자열일 때 등 매칭 실패 — 매니저는 조용히 건너뛴다.
                    // (WaveDirector 가 진행 시 동일하게 fallback 처리하므로 시각화는 누락)
                    continue;
                }

                var worldPos = spawner.Path.SpawnPoint + worldOffset;
                var btn = Instantiate(buttonPrefab, buttonParent);
                btn.Bind(worldPos, worldCamera, OnAnyButtonClicked);
                active.Add(btn);
            }
        }

        private void HandleGapEnded()
        {
            ClearButtons();
        }

        private void OnAnyButtonClicked()
        {
            // 아무 버튼이나 누르면 즉시 시작 — 정리는 OnGapEnded 가 도착해 자동 처리.
            // 단, 안전하게 즉시 비활성화해 같은 프레임의 추가 클릭을 막는다.
            foreach (var b in active)
                if (b != null) b.gameObject.SetActive(false);

            director?.RequestSkipToNextWave();
        }

        private void ClearButtons()
        {
            foreach (var b in active)
                if (b != null) Destroy(b.gameObject);
            active.Clear();
        }
    }
}
