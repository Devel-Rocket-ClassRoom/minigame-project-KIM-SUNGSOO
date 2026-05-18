# Kingdom Rush 스타일 Tower Defense — 스켈레톤 구조

Unity 2.5D 기반 타워디펜스의 **확장 가능한 뼈대**입니다.
폴더 그대로 Unity 프로젝트의 `Assets/` 아래에 복사하면 됩니다.

> 의존 패키지: **TextMeshPro** (HUD에서 사용). 그 외 외부 패키지는 없습니다.

---

## 1. 폴더 구조

```
Assets/_Project/
├── Scripts/
│   ├── Core/            # GameManager, EventBus
│   ├── Pooling/         # ObjectPool
│   ├── Data/            # ScriptableObject 정의 (TowerData, EnemyData, WaveData ...)
│   ├── Towers/          # 타워 컴포넌트(컨트롤러/타겟탐지/업그레이더/슬롯)
│   │   └── Types/       # 구체 타워(ProjectileShooter, BarracksShooter ...)
│   ├── Enemies/         # EnemyController/Health/Movement
│   ├── Projectiles/     # 투사체 베이스
│   ├── Waves/           # WaveManager, EnemySpawner
│   ├── Map/             # Path, LevelLoader
│   ├── Economy/         # GoldManager, LivesManager
│   └── UI/              # HUD, BuildMenu, UpgradeMenu
├── Prefabs/
│   ├── Towers/
│   ├── Enemies/
│   ├── Projectiles/
│   ├── Effects/
│   ├── UI/
│   └── Map/
├── ScriptableObjects/
│   ├── Towers/
│   ├── Enemies/
│   ├── Waves/
│   └── Levels/
└── Scenes/
```

---

## 2. 프리펩 분류 (어떤 프리펩을 만들어야 하는가)

각 프리펩은 **빈 GameObject + 아래 컴포넌트 조합**으로 만들면 됩니다.

### A. Tower (타워) — 가장 중요한 확장 단위

| 프리펩 | 컴포넌트 구성 | 비고 |
|---|---|---|
| `Tower_Base.prefab` | `TowerController`, `TargetFinder`, `TowerUpgrader`, `ProjectileShooter`(또는 `BarracksShooter`), Collider, AudioSource | 모든 타워 프리펩의 공통 뼈대 |
| `Tower_Archer.prefab` | `Tower_Base` 변형 + `_Visual/` 자식에 아처 모델 | `ProjectileShooter` 사용 |
| `Tower_Mage.prefab` | `Tower_Base` 변형 + 마법사 모델 | `ProjectileShooter`, DamageType=Magical |
| `Tower_Artillery.prefab` | `Tower_Base` 변형 + 포대 모델 | `ProjectileShooter`, splashRadius>0 |
| `Tower_Barracks.prefab` | `Tower_Base` 변형, Shooter만 `BarracksShooter`로 교체 | 병사 소환 |
| `TowerSlot.prefab` | `TowerSlot` + Collider(클릭용) + 비주얼 링 | 맵에 미리 배치 |

> **핵심**: 비주얼은 프리펩 안의 `_Visual` 자식 Transform에 보관.
> 티어 업그레이드 시 `TowerUpgrader`가 이 자식 트랜스폼 내용을
> `TowerUpgradeData.visualPrefab`으로 교체합니다 (2.5D 외형 swap 용이).

### B. Enemy (적)

| 프리펩 | 컴포넌트 |
|---|---|
| `Enemy_Base.prefab` | `EnemyController`, `EnemyHealth`, `EnemyMovement`, Collider, Rigidbody(IsKinematic), Animator |
| `Enemy_Goblin.prefab` | `Enemy_Base` 변형 + 고블린 모델/스프라이트 |
| `Enemy_Orc.prefab` | `Enemy_Base` 변형 + 오크 모델 |
| `Enemy_Wolf.prefab` | `Enemy_Base` 변형 + 늑대 모델 |

### C. Projectile (투사체)

| 프리펩 | 컴포넌트 |
|---|---|
| `Projectile_Base.prefab` | `Projectile`, TrailRenderer/스프라이트 |
| `Projectile_Arrow.prefab` | `Projectile`(homing=false 가능) |
| `Projectile_MagicBolt.prefab` | `Projectile`(homing=true) |
| `Projectile_Cannon.prefab` | `Projectile`(splash 사용) |

### D. Effect (이펙트)

| 프리펩 | 용도 |
|---|---|
| `FX_Hit.prefab` | 피격 파티클(풀로 재사용) |
| `FX_Explosion.prefab` | 폭발 |
| `FX_Death.prefab` | 적 사망 |

### E. Map (맵)

| 프리펩 | 컴포넌트 |
|---|---|
| `Path.prefab` | `Path` + 자식 Transform들로 웨이포인트 |
| `TowerSlot.prefab` | (위 A 참고) |
| `Goal.prefab` | 골 지점 시각화 |

### F. UI

| 프리펩 | 용도 |
|---|---|
| `UI_HUD.prefab` | `HUD` 컴포넌트 + 골드/라이프/웨이브 텍스트 + Start 버튼 |
| `UI_BuildMenu.prefab` | `TowerBuildMenu` (라디얼 메뉴) |
| `UI_UpgradeMenu.prefab` | `TowerUpgradeMenu` |
| `UI_BuildButton.prefab` | BuildMenu에서 동적 생성되는 단일 버튼 |
| `UI_BranchButton.prefab` | UpgradeMenu에서 동적 생성되는 분기 버튼 |

### G. System (씬 루트에 1개씩)

| 프리펩 | 컴포넌트 |
|---|---|
| `_Systems.prefab` | `GameManager`, `ObjectPool`, `GoldManager`, `LivesManager`, `WaveManager`, `LevelLoader` |

---

## 3. ScriptableObject 만드는 법

Project 창에서 우클릭 → **Create → KRTD →**
- `Tower Data` : 타워 한 종류 (Archer 등)
- `Tower Upgrade (Tier)` : 한 티어 능력치 (T1/T2/T3 각각 1개)
- `Enemy Data` : 적 한 종류
- `Wave Data` : 한 웨이브 구성
- `Level Data` : 스테이지 1개 = 웨이브 리스트 + 허용 타워

### 업그레이드 체인 예시 (Archer 타워)

```
ArcherTowerData.upgradeChain[0] → ArcherT1 (Tier 1)
                                 ├ nextUpgrades[0] → ArcherT2
                                                     ├ nextUpgrades[0] → ArcherT3
                                                     └ (최종 티어)
```

→ 분기 트리가 필요해지면 `nextUpgrades`에 2개 이상의 SO를 넣기만 하면
   `TowerUpgradeMenu`가 자동으로 버튼을 늘려줍니다.

---

## 4. 확장 시나리오

### 새 타워 추가하기 (예: "독 사수")
1. `Tower_PoisonArcher.prefab` 만들기 (Tower_Base 복제, 비주얼만 교체)
2. SO 3개 만들기: `PoisonArcherT1/T2/T3.asset`
3. `TowerData.asset` 만들고 `upgradeChain`에 위 3개 연결
4. `LevelData.allowedTowers`에 새 TowerData 추가
→ **코드 수정 없음.**

### 새 적 추가하기
1. `Enemy_Skeleton.prefab` 만들기
2. `EnemyData.asset` 만들고 enemyPrefab 연결
3. 원하는 WaveData에 SpawnGroup으로 추가

### 새 스테이지 추가하기
1. 새 Scene 만들고 `_Systems` 프리펩 + Path들 + TowerSlot들 배치
2. `LevelData.asset` 만들고 `LevelLoader`에 연결

---

## 5. 확장성 핵심 원칙 (왜 이렇게 나눴는가)

1. **데이터 드리븐**: 능력치/티어/웨이브가 전부 ScriptableObject.
   디자이너/기획이 코드 없이 새 타워·적·웨이브를 추가 가능.
2. **컴포넌트 분리**: Tower 하나는 `Controller + TargetFinder + Shooter + Upgrader`.
   막사 타워, 버프 타워 등 새 타입은 Shooter만 새로 만들면 됨.
3. **EventBus**: UI/매니저 간 강결합 제거. 새 매니저가 들어가도 기존 코드 안 건드림.
4. **ObjectPool**: 적·투사체·이펙트의 빈번한 생성을 풀로 처리. 모바일에서도 안정적.
5. **2.5D 친화**: 비주얼이 `_Visual` 자식에 분리되어 있어 3D 모델 ↔ 2D 스프라이트 어느 쪽도 swap 용이.

---

## 6. 다음 단계 권장 작업

다음 순서로 작업하면 가장 빠르게 플레이 가능한 빌드가 나옵니다.

1. Unity에 폴더 그대로 import → 컴파일 확인
2. `_Systems.prefab` 만들어 빈 씬에 배치
3. `Path.prefab` 하나 만들고 웨이포인트 5~6개 배치
4. `Enemy_Goblin.prefab` + `GoblinData.asset` 만들고 WaveData로 호출
5. `Tower_Archer.prefab` + `ArcherT1/T2/T3.asset` + `ArcherTowerData.asset`
6. `TowerSlot.prefab` 몇 개 맵에 배치
7. `UI_HUD.prefab` 캔버스에 연결 → Start Wave 버튼 작동 확인

문제가 생기거나 추가 컴포넌트가 필요하면 말씀해 주세요.
