namespace KRTD.Data
{
    /// <summary>
    /// 데미지 타입(저항/약점 계산용). 새 타입을 추가하려면 여기에 항목만 늘리면 됨.
    /// </summary>
    public enum DamageType
    {
        Physical,   // 화살, 검
        Magical,    // 마법
        Explosive,  // 폭발(범위)
        True        // 저항 무시
    }

    /// <summary>
    /// 적 분류(보병/비행/거대/방어 등). 타워의 공격 가능 여부에 사용.
    /// </summary>
    public enum EnemyArmorType
    {
        Unarmored,
        Light,
        Heavy,
        Magic,
        Flying
    }
}
