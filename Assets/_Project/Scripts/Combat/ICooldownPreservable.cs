namespace KRTD.Combat
{
    /// <summary>
    /// 업그레이드(인스턴스 재생성) 시점에 공격 쿨다운 잔여 시간을 새 인스턴스로 이식하기 위한 인터페이스.
    /// BuildSpot.ReplaceBuilding 이 옛 인스턴스를 Destroy 하기 전에 RemainingCooldown 을 캡쳐하고,
    /// 새 인스턴스가 같은 인터페이스를 구현하면 SetRemainingCooldown 으로 복원한다.
    ///
    /// 미구현 시: 새 인스턴스는 쿨다운 0 으로 시작 → 즉시 한 발 더 발사되어 부자연스러운 연사가 발생.
    /// </summary>
    public interface ICooldownPreservable
    {
        /// <summary>다음 발사까지 남은 시간(초). 이미 발사 가능 상태면 0.</summary>
        float RemainingCooldown { get; }

        /// <summary>잔여 쿨다운(초)을 강제로 설정한다. 0 이하면 즉시 발사 가능.</summary>
        void SetRemainingCooldown(float remaining);
    }
}
