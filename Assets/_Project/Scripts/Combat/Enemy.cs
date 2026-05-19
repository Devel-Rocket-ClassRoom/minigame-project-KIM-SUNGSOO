using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 적 유닛. 현재는 체력만 있고 외부에서 데미지를 받을 수 있다.
    /// 경로 이동, 스탯 세분화 등은 추후 추가한다.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        [Header("체력")]
        [SerializeField] private float maxHp = 10f;
        [SerializeField] private float currentHp;

        public bool IsDead => currentHp <= 0f;
        public Vector3 Position => transform.position;

        private void Awake()
        {
            currentHp = maxHp;
        }

        public void TakeDamage(float damage)
        {
            if (IsDead) return;

            currentHp -= damage;
            if (currentHp <= 0f)
            {
                currentHp = 0f;
                Die();
            }
        }

        private void Die()
        {
            // TODO: 사망 연출 / 보상 처리
            Destroy(gameObject);
        }
    }
}
