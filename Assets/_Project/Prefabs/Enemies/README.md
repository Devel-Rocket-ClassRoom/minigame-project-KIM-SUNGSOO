# Enemy Prefabs

- `Enemy_Base.prefab` : EnemyController + EnemyHealth + EnemyMovement + Collider + (kinematic) Rigidbody
- `Enemy_Goblin.prefab` / `Enemy_Orc.prefab` / `Enemy_Wolf.prefab` : Base 변형, 모델/스프라이트만 교체

각 적의 능력치는 `ScriptableObjects/Enemies/*.asset`(EnemyData)에서 정의합니다.
