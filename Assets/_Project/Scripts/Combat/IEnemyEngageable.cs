namespace KRTD.Combat
{
    /// <summary>
    /// 적이 \"정지하고 교전 / 1:1 페어 락 / 공격 대상\" 으로 인식할 수 있는 아군 유닛.
    /// 보병(Soldier) 과 영웅(Hero) 이 공통으로 구현해 Enemy 측 타겟 검색·교전 로직을 단일화한다.
    ///
    /// IDamageable 을 확장 — Position/IsDead/TakeDamage 는 거기서 상속.
    ///
    /// 페어 락 정책 (Enemy.SetCurrentEngageTarget 가 자동 갱신):
    ///   - 적이 currentEngageTarget 으로 잡으면 그 대상의 TargetedBy 가 그 적으로 세팅됨.
    ///   - 다른 적은 이 대상을 후보에서 제외해 1:1 교전 유지.
    ///   - 대상이 죽거나 적이 풀면 자동 해제.
    /// </summary>
    public interface IEnemyEngageable : IDamageable
    {
        /// <summary>
        /// 배치 중 (보병이 랠리까지 아직 못 간 상태) — 적 검색에서 제외된다.
        /// 영웅 같이 \"배치\" 개념이 없는 유닛은 항상 false 반환.
        /// </summary>
        bool IsDeploying { get; }

        /// <summary>이 유닛을 노리고 있는 적. 없으면 null.</summary>
        Enemy TargetedBy { get; }

        /// <summary>Enemy 측에서 페어 lock 설정/해제 시 호출 — 외부 직접 호출 비권장.</summary>
        void SetTargetedBy(Enemy e);

        /// <summary>
        /// true 면 여러 적이 동시에 이 유닛을 currentEngageTarget 으로 잡을 수 있다.
        /// 보병(Soldier)은 false — 1:1 페어 lock 유지 (한 보병에 한 적).
        /// 영웅(Hero)은 true — 탱커 컨셉으로 여러 적이 동시에 달려들 수 있음.
        /// false 일 때는 TargetedBy 가 다른 적이면 후보에서 제외, true 면 항상 후보로 잡힘.
        /// </summary>
        bool AcceptsMultipleAttackers { get; }

        /// <summary>
        /// true 면 이 유닛이 직접 적에게 다가간다(보병의 sideEngage 슬라이드 등).
        /// 적은 detection 안에 잡으면 멈춰서 이 유닛이 올 때까지 기다린다.
        ///
        /// false 면 이 유닛은 적에게 다가가지 않는다(영웅의 고정 랠리).
        /// 적이 detection 안에 잡았는데 attack range 밖이면 적이 직접 이 유닛 쪽으로 이동해야 한다.
        /// </summary>
        bool ApproachesEnemies { get; }
    }
}
