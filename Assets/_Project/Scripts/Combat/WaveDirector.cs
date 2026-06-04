using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KRTD.Game;

namespace KRTD.Combat
{
    /// <summary>
    /// 씬 전체의 웨이브 시나리오를 진행하는 중앙 컨트롤러.
    ///
    /// 책임:
    ///   - 등록된 EnemySpawner 들을 pathId 로 라우팅
    ///   - 웨이브 리스트를 순서대로 진행 (initialDelay → wave[i] → gapBetweenWaves → wave[i+1] …)
    ///   - 한 웨이브 안에서 entry 들을 startOffset 기준 "병렬" 실행
    ///   - GameState 에 (CurrentWave, TotalWave) 동기화
    ///   - 글로벌 난이도 배율(count / interval / hp)을 웨이브 인덱스에 따라 적용
    ///
    /// 한 EnemySpawner = 한 경로의 스폰 위치. WaveDirector 가 그 위로 시나리오를 얹는다.
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        [Header("스포너 등록")]
        [Tooltip("씬에 배치된 EnemySpawner 들. SpawnEntry.pathId 가 PathId 와 일치하는 스포너로 라우팅된다.")]
        [SerializeField] private List<EnemySpawner> spawners = new List<EnemySpawner>();

        [Header("웨이브")]
        [SerializeField] private List<WaveData> waves = new List<WaveData>();

        [Header("시작 동작")]
        [Tooltip("플레이 시작과 함께 자동으로 첫 웨이브를 시작할지 여부.")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("자동 시작 시 첫 웨이브까지의 대기 시간 (초)")]
        [SerializeField] private float initialDelay = 1.5f;

        [Tooltip("웨이브 사이의 추가 대기 시간 (초). WaveData.startDelay 와 합산된다.")]
        [SerializeField] private float gapBetweenWaves = 3f;

        [Header("난이도 곡선 (x = 웨이브 인덱스, 1부터)")]
        [Tooltip("entry.count 에 곱해질 배율. 결과는 ceil 한다.")]
        [SerializeField] private AnimationCurve countMultiplier = AnimationCurve.Constant(1, 99, 1f);
        [Tooltip("entry.interval 에 곱해질 배율. 1 미만이면 더 빨라진다.")]
        [SerializeField] private AnimationCurve intervalMultiplier = AnimationCurve.Constant(1, 99, 1f);
        [Tooltip("적 hp 에 곱해질 배율. EnemySpawner.SpawnEnemy 에 전달된다.")]
        [SerializeField] private AnimationCurve hpMultiplier = AnimationCurve.Constant(1, 99, 1f);

        private readonly Dictionary<string, EnemySpawner> spawnerByPathId = new Dictionary<string, EnemySpawner>();
        private bool isRunning;

        // 갭 스킵 상태
        private bool isWaitingBetweenWaves;
        private bool skipRequested;

        /// <summary>웨이브 사이 갭 대기 중인지. UI 의 "다음 웨이브" 버튼이 이 값으로 활성/비활성을 판단.</summary>
        public bool IsWaitingBetweenWaves => isWaitingBetweenWaves;

        /// <summary>현재 진행 중인 웨이브 번호(1-base). 갭 중에는 "방금 끝난 웨이브 번호".</summary>
        public int CurrentWaveNumber { get; private set; }

        /// <summary>갭 스킵이 성공했을 때 호출. 인자는 "절약된 초". 보상 부여(쿨다운 단축 등)가 이 이벤트를 구독한다.</summary>
        public event Action<float> OnWaveSkipped;

        /// <summary>웨이브 사이 갭이 시작될 때 호출. 인자는 "곧 시작될 다음 웨이브 데이터" — 스폰 지점 버튼이 이걸 받아 pathId 목록 추출.</summary>
        public event Action<WaveData> OnGapStarted;

        /// <summary>갭이 끝났을 때 (스킵이든 시간 만료든) 호출. UI 가 일괄 정리에 사용.</summary>
        public event Action OnGapEnded;

        /// <summary>모든 웨이브 진행이 끝났을 때 한 번만 호출. 승리 조건 감지가 구독.</summary>
        public event Action OnAllWavesDone;

        /// <summary>모든 웨이브가 끝났는지 (마지막 RunWave 종료 후 true).</summary>
        public bool AreAllWavesDone { get; private set; }

        /// <summary>현재 갭의 다음 웨이브. 갭이 아니면 null.</summary>
        public WaveData NextWave { get; private set; }

        /// <summary>pathId 로 등록된 스포너 조회. 다중 경로 UI 가 위치를 찾을 때 사용.</summary>
        public bool TryGetSpawner(string pathId, out EnemySpawner spawner)
        {
            return spawnerByPathId.TryGetValue(pathId ?? "", out spawner);
        }

        private void Awake()
        {
            BuildSpawnerLookup();
        }

        private void Start()
        {
            var state = GameState.Instance;
            if (state != null) state.SetWave(0, waves.Count);

            if (autoStart) StartWaves();
        }

        private void BuildSpawnerLookup()
        {
            spawnerByPathId.Clear();
            foreach (var s in spawners)
            {
                if (s == null) continue;
                var id = s.PathId ?? "";
                if (spawnerByPathId.ContainsKey(id))
                {
                    Debug.LogWarning($"[WaveDirector] pathId 중복: \"{id}\". 첫 등록만 유지된다.");
                    continue;
                }
                spawnerByPathId[id] = s;
            }
        }

        /// <summary>외부에서(예: 시작 버튼) 호출하면 첫 웨이브부터 진행한다.</summary>
        public void StartWaves()
        {
            if (isRunning) return;
            if (spawners.Count == 0)
            {
                Debug.LogWarning("[WaveDirector] 등록된 EnemySpawner 가 없다.");
                return;
            }
            isRunning = true;
            StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, initialDelay));

            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                if (wave == null) continue;

                CurrentWaveNumber = i + 1;
                var state = GameState.Instance;
                if (state != null) state.SetWave(i + 1, waves.Count);

                yield return new WaitForSeconds(Mathf.Max(0f, wave.startDelay));
                yield return RunWave(wave, i + 1);

                // 다음 웨이브 사이 갭: 스킵 가능한 대기
                if (i < waves.Count - 1)
                {
                    NextWave = waves[i + 1];
                    yield return WaitGapInterruptible(Mathf.Max(0f, gapBetweenWaves));
                    NextWave = null;
                }
            }

            isRunning = false;
            AreAllWavesDone = true;
            OnAllWavesDone?.Invoke();
        }

        // 갭 동안 매 프레임 skipRequested 를 확인. 스킵되면 절약된 초를 OnWaveSkipped 로 알린다.
        private IEnumerator WaitGapInterruptible(float seconds)
        {
            if (seconds <= 0f) yield break;

            isWaitingBetweenWaves = true;
            skipRequested = false;
            OnGapStarted?.Invoke(NextWave);

            float endTime = Time.time + seconds;
            while (Time.time < endTime && !skipRequested)
                yield return null;

            float saved = skipRequested ? Mathf.Max(0f, endTime - Time.time) : 0f;

            isWaitingBetweenWaves = false;
            skipRequested = false;

            OnGapEnded?.Invoke();

            if (saved > 0f)
                OnWaveSkipped?.Invoke(saved);
        }

        /// <summary>
        /// "다음 웨이브 즉시 시작" 버튼이 호출. 갭 대기 중일 때만 유효하며,
        /// 성공 시 true 와 함께 OnWaveSkipped 이벤트가 발사된다.
        /// </summary>
        public bool RequestSkipToNextWave()
        {
            if (!isWaitingBetweenWaves) return false;
            skipRequested = true;
            return true;
        }

        // 한 웨이브의 entries 를 startOffset 기준으로 병렬 실행하고, 모두 끝날 때까지 기다린다.
        private IEnumerator RunWave(WaveData wave, int waveNumber)
        {
            float countMul = Mathf.Max(0f, countMultiplier.Evaluate(waveNumber));
            float intervalMul = Mathf.Max(0f, intervalMultiplier.Evaluate(waveNumber));
            float hpMul = Mathf.Max(0.01f, hpMultiplier.Evaluate(waveNumber));

            int running = 0;
            foreach (var entry in wave.entries)
            {
                if (entry == null || entry.enemy == null || entry.count <= 0) continue;

                running++;
                StartCoroutine(RunEntry(entry, countMul, intervalMul, hpMul, () => running--));
            }

            while (running > 0) yield return null;
        }

        private IEnumerator RunEntry(WaveData.SpawnEntry entry, float countMul, float intervalMul, float hpMul, System.Action onDone)
        {
            if (entry.startOffset > 0f)
                yield return new WaitForSeconds(entry.startOffset);

            var spawner = ResolveSpawner(entry.pathId);
            if (spawner == null)
            {
                Debug.LogWarning($"[WaveDirector] pathId \"{entry.pathId}\" 에 해당하는 스포너 없음. entry 건너뜀.");
                onDone?.Invoke();
                yield break;
            }

            // 보스: count/hp 곡선을 무시하고 정확히 1마리만, 데이터값 그대로 스폰.
            //   - 보스가 여러 마리 복제되거나 hp 가 곡선에 휘둘리지 않도록 차단.
            //   - entry.count 가 1 이 아닌 값으로 잘못 설정돼 있어도 경고만 띄우고 1로 강제.
            int adjustedCount;
            float adjustedHpMul;
            if (entry.enemy.isBoss)
            {
                adjustedCount = 1;
                adjustedHpMul = 1f;
                if (entry.count > 1)
                    Debug.LogWarning($"[WaveDirector] 보스 entry({entry.enemy.enemyName})는 count={entry.count} 이지만 1마리로 고정한다.");
            }
            else
            {
                adjustedCount = Mathf.Max(1, Mathf.CeilToInt(entry.count * countMul));
                adjustedHpMul = hpMul;
            }
            float adjustedInterval = Mathf.Max(0f, entry.interval * intervalMul);

            for (int n = 0; n < adjustedCount; n++)
            {
                spawner.SpawnEnemy(entry.enemy, adjustedHpMul);
                if (n < adjustedCount - 1)
                    yield return new WaitForSeconds(adjustedInterval);
            }

            onDone?.Invoke();
        }

        // pathId 가 비어있거나 매칭 실패면 첫 등록 스포너로 fallback (기존 단일-경로 .asset 역호환).
        private EnemySpawner ResolveSpawner(string pathId)
        {
            if (!string.IsNullOrEmpty(pathId) && spawnerByPathId.TryGetValue(pathId, out var s))
                return s;

            foreach (var sp in spawners)
                if (sp != null) return sp;

            return null;
        }
    }
}
