namespace KRTD.Combat
{
    /// <summary>
    /// 데미지의 종류. 적의 저항/약점 계산, 시각 효과 분리, 통계 등에 사용된다.
    ///
    /// 정책:
    ///   - ArcherTower / Arrow / Soldier 의 근접 공격 → Physical
    ///   - MageTower / Magic 투사체 → Magic
    ///   - 향후 새로운 공격 유형 추가 시 여기에 enum 만 추가하고, EnemyData 의 저항/약점 매핑에서 처리.
    /// </summary>
    public enum AttackType
    {
        Physical,
        Magic,
    }
}
