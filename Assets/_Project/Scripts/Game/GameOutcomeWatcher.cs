using UnityEngine;
using KRTD.Combat;

namespace KRTD.Game
{
    /// <summary>
    /// 승리 조건 감시자. "모든 웨이브 종료 + 필드 적 0" 이 충족되면 GameState.TriggerWin 을 한 번만 호출.
    ///
    /// 책임 분리:
    ///   - 패배(life ≤ 0) 는 GameState.LoseLife 가 직접 트리거 → 여기서 다루지 않음
    ///   - 이 컴포넌트는 승리 조건만 본다
    ///
    /// 동작:
    ///   1) WaveDirector.OnAllWavesDone 구독 → wavesDone = true
    ///   2) Update 에서 wavesDone && Enemy.AliveCount == 0 → GameState.TriggerWin
    ///   3) IsGameEnded 이면 즉시 종료
    /// </summary>
    public class GameOutcomeWatcher : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("감시할 WaveDirector. 비워두면 씬에서 자동 탐색.")]
        [SerializeField] private WaveDirector director;

        [Header("판정")]
        [Tooltip("웨이브 종료 후, 적 0 상태가 이 시간(초) 유지되면 승리. 마지막 적이 정확히 같은 프레임에 죽기 직전 hp가 0이 되는 케이스 등의 떨림 방어.")]
        [Min(0f)]
        [SerializeField] private float winConfirmDelay = 0.3f;

        private bool wavesDone;
        private float wavesDoneSinceTime;
        private bool triggered;

        private void Awake()
        {
            if (director == null) director = FindFirstObjectByType<WaveDirector>();
        }

        private void OnEnable()
        {
            if (director != null) director.OnAllWavesDone += HandleAllWavesDone;
        }

        private void OnDisable()
        {
            if (director != null) director.OnAllWavesDone -= HandleAllWavesDone;
        }

        private void HandleAllWavesDone()
        {
            wavesDone = true;
            wavesDoneSinceTime = Time.time;
        }

        private void Update()
        {
            if (triggered) return;
            if (!wavesDone) return;

            var state = GameState.Instance;
            if (state == null || state.IsGameEnded) { triggered = true; return; }

            // 마지막 적이 남아있으면 대기
            if (Enemy.AliveCount > 0) return;

            // 적 0 상태가 winConfirmDelay 만큼 유지됐는지 확인 (떨림 방지)
            if (Time.time - wavesDoneSinceTime < winConfirmDelay) return;

            triggered = true;
            state.TriggerWin();
        }
    }
}
