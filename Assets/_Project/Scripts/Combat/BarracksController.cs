using System.Collections;
using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 배럭 건물의 행동 로직.
    /// 자신이 소유한 spawn point N 개에 보병 1명씩 유지하며,
    /// 보병이 죽으면 일정 시간 뒤 흙먼지 이펙트 → 새 보병 등장 순으로 부활시킨다.
    ///
    /// 구조 권장:
    ///   Barracks (이 컴포넌트, BuildSpot 의 buildingPrefab 으로 인스턴스화)
    ///   ├─ Body              (SpriteRenderer - Barracks 그림)
    ///   ├─ SpawnPoint_1      (빈 Transform)
    ///   ├─ SpawnPoint_2      (빈 Transform)
    ///   └─ SpawnPoint_3      (빈 Transform)  ← spawnPoints 에 등록
    /// </summary>
    public class BarracksController : MonoBehaviour
    {
        [Header("유닛")]
        [Tooltip("이 배럭에서 소환할 보병 프리팹 (Soldier 컴포넌트 포함).")]
        [SerializeField] private Soldier soldierPrefab;

        [Tooltip("보병이 머무를 위치들. 슬롯 개수 = 동시 운영 보병 수.")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("부활")]
        [Tooltip("보병 사망 후 새 보병이 등장하기까지의 시간(초).")]
        [SerializeField] private float respawnDelay = 8f;

        [Tooltip("등장 직전에 spawn point 위치에 잠시 띄울 흙먼지 이펙트 프리팹. null 이면 생략.")]
        [SerializeField] private GameObject spawnDustPrefab;

        [Tooltip("흙먼지 이펙트 자체의 생존 시간(초). 보통 애니메이션 길이와 동일.")]
        [SerializeField] private float dustDuration = 0.7f;

        [Tooltip("흙먼지가 먼저 살짝 보이고 그 뒤에 보병이 등장하기까지의 텀(초).")]
        [SerializeField] private float dustToSoldierLead = 0.15f;

        // 슬롯별로 현재 살아있는 보병을 추적 (사망 시 null).
        private Soldier[] activeSoldiers;

        private void Start()
        {
            if (soldierPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning($"[BarracksController] {name}: soldierPrefab 또는 spawnPoints 가 비어있다.");
                return;
            }

            activeSoldiers = new Soldier[spawnPoints.Length];
            for (int i = 0; i < spawnPoints.Length; i++)
                SpawnImmediate(i);
        }

        private void SpawnImmediate(int slot)
        {
            if (spawnPoints[slot] == null) return;

            var soldier = Instantiate(soldierPrefab, spawnPoints[slot].position, Quaternion.identity);
            int capturedSlot = slot;
            soldier.OnDeath += _ => StartCoroutine(RespawnAfterDelay(capturedSlot));
            activeSoldiers[slot] = soldier;
        }

        /// <summary>
        /// 배럭이 파괴/판매될 때, 이 배럭이 소환했던 보병들도 함께 정리한다.
        /// (보병은 spawn 시 부모 없이 씬 루트에 만들어지므로 배럭이 사라져도 살아남는다.)
        /// </summary>
        private void OnDestroy()
        {
            if (activeSoldiers == null) return;
            for (int i = 0; i < activeSoldiers.Length; i++)
            {
                if (activeSoldiers[i] != null)
                {
                    Destroy(activeSoldiers[i].gameObject);
                    activeSoldiers[i] = null;
                }
            }
        }

        private IEnumerator RespawnAfterDelay(int slot)
        {
            activeSoldiers[slot] = null;

            yield return new WaitForSeconds(respawnDelay);

            // 1) 흙먼지 펑
            if (spawnDustPrefab != null && spawnPoints[slot] != null)
            {
                var dust = Instantiate(spawnDustPrefab, spawnPoints[slot].position, Quaternion.identity);
                if (dustDuration > 0f) Destroy(dust, dustDuration);
            }

            // 2) 살짝 텀 두고
            if (dustToSoldierLead > 0f)
                yield return new WaitForSeconds(dustToSoldierLead);

            // 3) 보병 등장 — 단, 그 사이 배럭이 파괴/판매됐을 수도 있으니 살아있는지 확인
            if (this != null) SpawnImmediate(slot);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (spawnPoints == null) return;
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.85f);
            foreach (var p in spawnPoints)
            {
                if (p == null) continue;
                Gizmos.DrawWireSphere(p.position, 0.25f);
                Gizmos.DrawLine(transform.position, p.position);
            }
        }
#endif
    }
}
