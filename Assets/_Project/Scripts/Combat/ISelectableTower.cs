namespace KRTD.Combat
{
    /// <summary>
    /// 플레이어가 클릭/선택할 수 있고, 사거리 원을 표시할 수 있는 타워.
    /// 관리 메뉴(BuildMenuController.ShowManageMenu)가 열릴 때 SetRangeVisible(true),
    /// 메뉴가 닫힐 때 SetRangeVisible(false) 로 호출된다.
    /// </summary>
    public interface ISelectableTower
    {
        void SetRangeVisible(bool visible);
    }
}
