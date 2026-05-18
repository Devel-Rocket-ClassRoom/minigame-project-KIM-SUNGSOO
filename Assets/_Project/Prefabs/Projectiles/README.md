# Projectile Prefabs

- `Projectile_Base.prefab` : Projectile 컴포넌트 + TrailRenderer/Sprite
- `Projectile_Arrow.prefab` / `Projectile_MagicBolt.prefab` / `Projectile_Cannon.prefab`

투사체는 ObjectPool로 재사용됩니다. 자체적으로 Despawn을 호출하므로 따로 Destroy하지 않습니다.
