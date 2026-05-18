# Tower Prefabs

이 폴더에 타워 프리펩들을 둡니다.

- `Tower_Base.prefab` : 모든 타워의 공통 베이스 (TowerController + TargetFinder + TowerUpgrader + Shooter + `_Visual` 빈 자식)
- `Tower_Archer.prefab` : Tower_Base 변형, ProjectileShooter
- `Tower_Mage.prefab` : Tower_Base 변형, ProjectileShooter (DamageType=Magical)
- `Tower_Artillery.prefab` : Tower_Base 변형, ProjectileShooter (splashRadius>0)
- `Tower_Barracks.prefab` : Tower_Base 변형, Shooter만 BarracksShooter로 교체
- `TowerSlot.prefab` : TowerSlot 컴포넌트 + Collider (맵에 미리 배치)

비주얼 모델/스프라이트는 각 프리펩의 `_Visual` 자식에 둡니다 — 티어 업그레이드 시 이 자식 내용이 swap됩니다.
