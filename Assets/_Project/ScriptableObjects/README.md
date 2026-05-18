# ScriptableObjects

Project 창에서 우클릭 → **Create → KRTD →** 메뉴로 생성합니다.

```
ScriptableObjects/
├── Towers/    # TowerData (타워 종류) + TowerUpgradeData (티어별 능력치)
├── Enemies/   # EnemyData
├── Waves/     # WaveData
└── Levels/    # LevelData (스테이지 = 웨이브 리스트 + 허용 타워)
```

## 권장 명명 규칙

- `TD_Archer.asset` (TowerData)
- `TT_Archer_T1.asset`, `TT_Archer_T2.asset`, `TT_Archer_T3.asset` (TowerUpgradeData)
- `ED_Goblin.asset` (EnemyData)
- `WD_Stage01_Wave1.asset` (WaveData)
- `LD_Stage01.asset` (LevelData)
