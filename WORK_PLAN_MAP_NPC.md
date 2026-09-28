# 맵빌더 개편 + NPC 이동 통일 — 작업 기록과 계획

> 최종 갱신: 2026-09-29 / 브랜치 `JunYeoung_Branch`
> 이 문서는 인수인계용입니다. 계획이 바뀌면 이 문서도 같이 고치고, 낡으면 지웁니다.
> 이전 NPC 계획서 원문은 커밋 `842b58c5b`의 `NPC_SYSTEM.md`에 있습니다 (`git show 842b58c5b:NPC_SYSTEM.md`).

---

## 0. 지금 상태 한눈에

| 항목 | 상태 |
|---|---|
| 맵빌더 버그 원인 분석 | ✅ 완료 (§1) |
| **M0** 맵빌더 긴급 복구 | ✅ 커밋·push `a2b17923f` |
| 오버월드 바닥 `Floor` 태그 | ✅ 커밋·push `fd16ae9b8` |
| 마을 바닥 태그 | ⬜ 마을을 새 맵빌더로 만들 때 함께 (§4 N4-B) |
| **이동 방식 결정** | ✅ **어디서든 A\*** (던전 밖 NavMesh 안 쓰기로 함) |
| **작업 순서 결정** (2026-09-28) | ✅ 오버월드로 NPC 이동 먼저 → 맵빌더 개편 → 새 빌더로 마을 (§4) |
| **N4-A** 씬 장소 그리드 | ✅ `5bf53c6c9` + 오버월드 배치 `e33c091aa` |
| **N4-C** 이동 브레인 | ✅ `3696810f3` 몸 / `d92e93421` GridSearch / `08a8f9114` 배회·순찰 |
| N4-D 던전 재확인 | ⬜ 에디터 작업만 (§4) |
| **맵빌더 방향 변경** (2026-09-29) | ✅ 새 도구를 만들지 않고 **옛 `MapBuilder`를 단계별로 진화** (§4 🅜) |
| ~~B1 팔레트 SO~~ | ❌ 취소 (2026-09-29) — 기존 프리팹 연결 방식 유지 |
| ~~B2~B6~~ | ❌ 재정리 (2026-09-29) — 굽기(덮어쓰기) 방식 유지로 결정. 아래 C1~C4로 대체 |
| **C1~C4** 빌더 개선 | ✅ `93e7f5a6a` + 던전 맵 5개 변환 `ab7f5355a` |
| **P1** 팔레트 파일 + 슬롯 자동 이전 | ✅ (팔레트 UI 정렬, 맵 컨테이너를 MapBuilder 밖으로 — 선택 시 맵 기즈모가 안 보이게) |
| **P2** 맵 종류(던전/필드/마을) + 필드·마을 굽기 자동 처리 | 🔨 다음 |
| N4-B 마을 | ⬜ B4 이후, 개선된 빌더로 |

**결정 대기 1건 → §6**

### 이전 세션에서 잃은 것
다른 PC와 브랜치가 갈라져 로컬을 `fd16ae9b8`로 리셋했다. 그때 로컬에만 있던 두 커밋이 빠졌다.
- `8b558841b` `ResetToSpawn`의 체력 리셋 분리 → **N5.9로 다시 한다** (§4)
- `8bba0d3ad` 마을·오버월드 NavMesh 베이크 → A\* 통일로 **불필요**

---

## 1. 맵빌더 문제의 원인 (기록용)

팀원(hjune87) 보고: "맵빌더에서 전체 프리팹 지우기 안 되고, 수정해도 저장 안 되고 날아간다"
→ **개인 PC 문제가 아니라 구조 문제**였음.

### 발생 시점
**2026-08-23 `f3a7c0d74` "오버월드 맵생성"** (hjune87).
`OverworldMapSetup.cs`의 메뉴가 MapBuildScene을 열어 MapBuilder의 타일 컨테이너를 지우고,
`OverworldMap.prefab`을 **연결된 프리팹 인스턴스 그대로** `tileContainer`에 넣고 씬을 저장했다.
(던전용으로 만든 맵빌더를 오버월드 제작에 쓰려다 전제가 깨진 것)

| 시점 | 컨테이너 | 저장되는 맵 |
|---|---|---|
| ~7/22 | 일반 GameObject | 독립 프리팹 (Firstmap1~5, TestMap) |
| 8/23 이후 | OverworldMap 인스턴스 | Demomap1~5 **전부 OverworldMap Variant** (타일 289개 "제거" + 던전 타일 ~590개 "추가") |

### 증상과 원인
1. **전체 지우기 실패** — 프리팹 인스턴스 소속 자식은 스크립트 `DestroyImmediate`로 못 지움
2. **저장 결과가 Variant** — 인스턴스 루트를 `SaveAsPrefabAsset`하면 Variant가 됨 → OverworldMap을 다시 구우면 모든 던전맵이 깨질 수 있었음
3. **작업이 날아감** — Undo/`MarkSceneDirty` 없이 `EditorUtility.SetDirty`만 호출 → 씬 변경 표시가 안 떠서 저장 없이 닫힘
4. **덮어쓰기** — 맵이 씬(9/1 시점 사본)과 프리팹에 두 벌 존재. "불러오기" 없이 굽기 → 다른 사람이 프리팹에 넣은 NPC 등을 덮어씀

추가로 발견: `OverworldMapSetup`이 Plane·OverworldMap 프리팹을 **삭제 후 재생성** → GUID가 바뀌어 참조 전부 끊김.

---

## 2. M0 — 긴급 복구 (완료, `a2b17923f`)

| 파일 | 변경 |
|---|---|
| `Editor/MapPrefabMigration.cs` (신규) | `Tools > Map Builder > Variant 맵을 독립 프리팹으로 변환` / `MapBuildScene 컨테이너 초기화` |
| `Editor/MapBuilderEditor.cs` | 프리팹 인스턴스 컨테이너 자동 해제, Variant 불러오기 차단, 모든 편집 Undo 등록(붓질 1회 = Undo 1단계), 지우기/불러오기/덮어쓰기 확인창, 높이에 `TileHeight` 사용 |
| `Editor/OverworldMapSetup.cs` | MapBuildScene 조작 제거, 프리팹을 삭제 대신 덮어써서 GUID 유지, 실행 전 확인창 |
| `Prefabs/Maps/Demomap1~5` | Variant → 독립 프리팹 |
| `Scenes/MapBuildScene.unity` | 39k줄 → 493줄. 빈 일반 컨테이너 |

### 검증 결과
- Demomap1~5: OverworldMap 참조 0건
- `Dungeon.unity`가 참조하는 **루트 fileID 5개 모두 유지** (변환 도구가 원래 ID로 되돌림) → `DungeonGenerator.mapPrefabs` 연결 유지
- 루트 자식 수 변환 전후 동일 (586 / 593 / 591 / 597 / 583), 프리팹별 개수 동일
- 루트 `MapDecoration` 유지, Demomap1·2의 `decorationRoot` 참조 유지 (3~5는 원래 null)

### 팀원 전달 사항
- pull 후 기존 맵빌더 사용 가능: MapBuildScene → "불러올 맵 프리팹" 지정 → **프리팹 불러오기** → 수정 → **프리팹으로 굽기**
- **작업 전 반드시 "불러오기"로 최신 프리팹을 가져올 것** (안 그러면 남의 수정을 덮어씀. 덮어쓰기 전 확인창이 뜸)
- Ctrl+Z 동작함
- `Tools/Overworld/Setup Overworld Map`은 오버월드 프리팹을 **처음부터 다시 만드는** 메뉴라 함부로 누르지 말 것

---

## 3. 핵심 결정: NPC 이동을 어디서든 A\*로 통일

이전 NPC 계획서(§6)는 **B안(던전 밖은 NavMesh + `ILocomotion` 추상화)** 을 추천했고 결정 대기였다.
→ **C안(어디서든 `GridMover`/A\*)으로 확정.**

C안을 망설였던 비용이 **맵빌더 개편 방향과 합치면 거의 사라진다**:

| 이전 계획서가 걱정한 비용 | 개편안 |
|---|---|
| `LocationGrid`에 Origin 추가 (A\* 포함 5파일 수정, 던전 회귀 위험) | **안 함.** 규칙: "모든 장소 맵은 월드 원점에 놓고, 바닥은 (0,0) 칸부터 양수 방향". OverworldMap은 이미 x,z = 0~32. 음수 좌표의 언덕·나무는 바닥 밖 장식이라 `InBounds`에서 무시됨 |
| `PhysicsGridBaker` 직접 구현 (~250줄) | **안 만듦.** 오버월드·마을도 던전처럼 **태그 타일 맵**으로 → 기존 `LocationGridBuilder` 그대로 사용 |
| 자유 배치 소품(언덕·나무·울타리)을 A\*가 모름 | **`GridBlocker` 컴포넌트** → 콜라이더 바운즈가 덮는 칸을 `StaticBlockers`로. `LocationGridBuilder`에 코드만 추가하는 변경이라 던전 영향 0 |
| 타일 2m라 1.5m 골목이 막힘 | 마을을 처음부터 2m 그리드 위에서 설계 → 통로는 항상 1칸 이상 |
| `ILocomotion` + NavMesh 어댑터 | **불필요.** 브레인이 `GridMover`를 직접 사용 |

### 새로 풀어야 할 문제
NPC·적은 `NotifyLocationReady()`에서 `GetComponentInParent<LocationRoot>()`로 그리드를 얻는다.
- 던전: `DungeonGenerator.CreateLocation`이 `LocationRoot`를 붙이고 `DungeonManager.NotifyActorsLocationReady`가 깨워줌 ✅
- 오버월드·마을: **씬에 놓인 맵에 `LocationRoot`를 만들어주는 주체가 없음**
- `Overworld.unity`의 `NPC_Test`는 맵의 **자식이 아님** → 부모 탐색으로 못 찾음
- 오버월드의 언덕·나무·울타리도 맵의 자식이 아니라 씬 최상단에 있음 → `GridBlocker`는 맵이 아니라 **씬 전체**에서 모아야 함
- 던전은 `DungeonGenerator.NormalizeMapOrigin`이 맵을 원점으로 **옮겨주지만**, 씬 맵은 옮기면 안 됨 (맵 밖 포탈·스폰 앵커·NPC와 어긋남) → **검사만** 하고 어기면 에러 로그
- 씬 맵 폴백 조회는 `LocationRoot`가 아니라 `SceneLocation`에 둔다. 던전에는 `SceneLocation`이 없으므로 던전 무변화가 구조적으로 보장됨

### 조사해 둔 현재 씬 상태
- **Overworld**: `OverworldMap` 인스턴스(원점, 오버라이드 없음) + Hill/Tree/Grass/fence/arch 등 자유 배치 + `Wall_Top/Bottom/Left/Right/Back` + `rigionMarker` + `NPC_Test` + `Test_Potal`
- **Town**: Unity 기본 **Plane 1개(scale 10 = 100×100m, Untagged)** + `SpawnAnchor_Start` + `Test_Potal`. 타일 바닥 없음
- `Floor` 태그를 읽는 코드는 전부 던전 전용 (`DungeonGenerator`, `LocationGridBuilder`, `LocationGrid`, `MapDecoration`, `PushableObject`, `TrapTile`) → 오버월드 Plane 태그 변경은 런타임 무영향
- `MinimapManager.SwitchLocation`은 `DungeonManager`에서만 호출됨

---

## 4. 로드맵

### 진행 순서 (2026-09-28 결정)
```
N4-A 씬 그리드 → N4-C 이동 브레인(오버월드) → N4-D 던전 재확인
  → B1~B5 맵빌더 개선 → N4-B 마을을 개선된 빌더로 → N5 이후
```
마을은 새 맵빌더로 만들 것이므로, 옛 도구로 N4-B를 먼저 하면 버려지는 작업이 된다.
오버월드는 이미 타일 맵이라 NPC 이동을 지금 바로 검증할 수 있다.

### 🅝 NPC 트랙

| 단계 | 내용 | 완료 조건 |
|---|---|---|
| **N4-A** 씬 장소 그리드 | ① `SceneLocation` (`[DefaultExecutionOrder(-100)]`): 맵 루트에 붙어 **Awake**에 `LocationRoot` + 그리드 생성, **Start**에 씬 안 액터 `NotifyLocationReady`. Awake에서 그리드를 만들어야 `NpcActor.Start`의 자가 준비보다 앞선다 ② 원점 검사 (옮기지 않음) ③ 폴백 `SceneLocation.FindInScene(scene)` — `NpcActor`·`EnemyController` 1줄씩 ④ `GridBlocker` + `LocationGridBuilder.ApplyBlockers` (씬 전체에서 수집) ⑤ 선택 시 그리드 기즈모 (에디터에서는 미리보기) | **던전 동작 무변화** + 오버월드에서 그리드·막힌 칸이 보임 |
| **N4-C** 이동 브레인 | ① 중력 적용을 브레인 → `NpcActor.Update`로 (목적지 없을 때만 `Move(zero)`, 프레임당 1회 유지) ② `Grid`가 있으면 스폰 위치를 셀 중앙에 스냅 ③ `GridSearch` 추출 (`EnemyPatrolState`의 BFS, **따로 커밋**) ④ `WanderBrain`, `RouteBrain`, `NpcWaypointRoute` | 오버월드 NPC가 A\*로 돌아다님 + **적 순찰 회귀 없음** |
| **N4-D** 던전 재확인 | Step 1(`3c490823f`)에서 이미 확인함. M0 변환 뒤에도 `Demomap1`에 NPC가 남아 있음. 브레인만 바꿔 층 왕복 | 던전에서도 같은 브레인 동작 |
| *(B1~B5)* | 맵빌더 개선 — 아래 🅜 | |
| **N4-B** 마을 | 개선된 맵빌더로 타일 바닥. 원점 (0,0). 건물·소품은 `Blocker` 카테고리 → `GridBlocker` 자동 부착 | 마을에서 그리드 생성 |
| N5 | 어포던스 (`IAffordance` + Chest/Stair/Torch 어댑터) | 이전 계획서 그대로 |
| N5.5 | 함정 `IDamageable` 일반화 (전제: 백스탭은 공격자가 `IFactionMember`일 때만) | 이전 계획서 그대로 |
| **N5.9** | `ResetToSpawn`의 체력 리셋 분리 재적용 (`NotifyLocationReady(bool restoreStats = true)`). 파티 스폰 때 주입한 체력이 만피로 덮이는 문제. **N6 착수 전 필수** | 동작 변화 0 |
| N6 ~ N8 | `DelverBrain` / 파티 (Utility + FSM) / 추상 시뮬 & AI LOD | 이전 계획서 그대로 |

N5·N5.5는 검증할 NPC가 있어야 하므로 **N6 최소판(혼자 계단 내려가기) 뒤로** 두는 것을 추천.

### 🅜 맵빌더 트랙 — 최종 방향 (2026-09-29 재결정)

**굽기(덮어쓰기)는 유지한다.** 맵은 기획자가 빌더로만 만들고 작업 전에 불러오기를 하므로 문제없다.
덮어쓰기 위험의 원인은 "빌더로 못 놓는 것이 있어 프리팹을 직접 열게 되는 것"이라, 빌더에 자유 배치를 넣어 원인을 줄인다.
**높이·층 구조는 도구가 정하지 않는다** (슬롯별 높이 기억, 벽 N단 자동 쌓기 등 제안 거절됨 — 층고는 제작자 몫).

| 단계 | 내용 |
|---|---|
| **C1** 종류별 컨테이너 | 굽는 맵 구조: `Floor`/`Wall`/`Entrance`/`Exit`/`Chest`/`Trap`/`Enemy`/`Pushable`(슬롯과 1:1) + `FreeObjects` + `DecorationRoot`. 칸 등록은 타일 묶음만. 옛 평평한 맵은 불러올 때 자동 정리(슬롯 원본 → 태그 → 나머지는 FreeObjects) |
| **C2** MapDecoration 자동 | 굽기 직전 DecorationRoot에 자식이 있으면 루트에 `MapDecoration` 부착·연결, 없으면 제거. **예전 굽기는 원본 루트의 MapDecoration을 잃어 장식이 처음부터 보이는 버그가 있었음** |
| **C3** 자유 배치 2종 | [자유 배치] → FreeObjects(항상 보임) / [개척 장식] → DecorationRoot. Shift+클릭 하나 배치(드래그 연속 배치 없음), Shift+우클릭 마우스 아래 오브젝트 삭제(현재 모드 묶음만), **Ctrl+휠 = 높이**(간격 기본 0.25m), 자유 배치용 프리팹 목록은 인스펙터에 끌어다 놓아 추가 |
| **C4** 인스펙터·씬 뷰 개편 | 헤더(작업 중 맵·크기) / 모드 탭 / 썸네일 팔레트 / 배치 / 단축키 요약 / 맵 파일(불러오기·굽기) / 접힘(맵 설정·슬롯 설정) / 가이드·전체 지우기. 씬 뷰 왼쪽 위 안내판, 자유 배치 시 바닥까지 세로선 |
| **P1** 팔레트 파일 | 고정 슬롯 8칸 대신 **팔레트 = 이름 + 타입 + 프리팹 목록**, `+`로 추가. 타입이 배치 방식(격자/자유)과 묶음을 정하므로 모드 탭 제거. 저장은 **`Assets/SO/MapBuilder/MapPaletteLibrary.asset` 파일 하나**(던전·필드·마을 목록). 씬에 두지 않는 이유: MapBuildScene은 작업본 때문에 커밋하지 않는데 팔레트는 팀 공유가 필요. 연결이 비면 프로젝트에서 찾고, 없을 때만 옛 슬롯 값으로 새로 만듦. 옛 슬롯 필드는 숨기고 남김(원칙 ①). **`MapTileType`은 번호로 저장되므로 새 타입은 맨 뒤에만 추가** |
| **P2** 맵 종류 | [던전][필드][마을] 탭. 필드·마을은 Entrance~Pushable·개척 장식 타입 숨김, 자유 배치 → **`Obstacles`** 묶음 + `GridBlocker` 1개 자동, 굽기 시 루트에 `SceneLocation` 자동 부착 → 그다음 N4-B |

**주의:** 묶음 오브젝트는 Untagged여야 하고(태그가 있으면 그 아래 타일이 그리드에서 빠짐), 이름이 "Bonus"로 시작하면 안 된다(미니맵이 이름으로 보너스를 찾음).

### (이전 안) 옛 빌더를 단계별로 진화 — 기록용

처음 계획(M1~M4)은 EditorWindow를 새로 만드는 것이었다. 코드를 다시 보니 문제 대부분이 기존
`MapBuilder`/`MapBuilderEditor` 안에서 고쳐지고, 특히 사고의 근본 원인인 "씬에서 만들고 굽기"는
**`MapBuilder`를 맵 프리팹 루트에 붙여 Prefab Mode에서 그대로 칠하게** 하면 사라진다.
팀원이 익숙한 UI·단축키와 M0의 Undo·안전장치도 유지된다.
새 창이 꼭 필요한 이유는 "빌더 오브젝트를 선택하고 있을 때만 칠해진다" 하나뿐이라, 써보고 불편하면 그때 만든다.

**모든 단계의 완료 조건: 기존 MapBuildScene 방식으로도 여전히 맵을 만들 수 있을 것.**

| 단계 | 내용 |
|---|---|
| M0 | ✅ 완료 |
| ~~B1 팔레트 SO~~ | **취소.** 구현까지 했다가 되돌림. 팔레트는 지금처럼 `MapBuilder`의 프리팹 슬롯(`floorPrefab` 등)으로 간다. 슬롯이 부족하면 SO 대신 **프리팹 목록 필드를 추가**하는 쪽으로 (필드 추가는 안전, 원칙 ①) |
| **B2** 로컬 좌표 + 레이어 점유 | `SyncPlacedTiles`를 루트 기준 좌표로, 점유 키를 `(칸, Ground/Object)`로. 같은 칸에 바닥과 상자 공존. 레이어는 SO 없이 **프리팹 태그로 판정** (Floor·Entrance·Exit·바닥 함정 → Ground, 나머지 → Object) |
| **B3** Prefab Mode 지원 | `MapBuilder`가 맵 프리팹 루트에 있으면 컨테이너 = 루트. Prefab Mode에서는 굽기·불러오기 숨김, 저장은 Unity 기본. MapBuildScene 방식도 그대로 동작 |
| **B4** 맵 종류별 프로필 | Dungeon/Overworld/Town 팔레트·검증 규칙. Town/Overworld 루트에 `SceneLocation` 자동 부착, `Blocker` 카테고리는 `GridBlocker` 자동 부착 |
| **B5** 검증 + 함정 연결 | "맵 검사" 버튼 (아래 검사 목록). 함정 연결은 `ArrowTrapAutoWire` 로직 재사용 |
| B6 정리 | 팀이 Prefab Mode 방식으로 옮겨오면 MapBuildScene 굽기 경로 제거 |

**팀 규칙:** 한 맵 프리팹은 한 사람이 작업한다. Prefab Mode로 덮어쓰기 문제는 사라져도, 같은 프리팹을 동시에 고치면 YAML 병합 충돌은 여전히 난다.

#### 데이터 설계 (참고용 — B1 취소로 SO 계층은 보류. B4의 맵 종류별 설정은 `MapBuilder` 필드로 대신한다)
```
MapTileDefinition (SO)   팔레트 항목 하나
 ├ id, 표시 이름, 아이콘
 ├ category : Ground / Wall / Trap / Object / Actor / Decoration / Marker / Blocker
 ├ prefab (+ 랜덤 변형 목록: Road/Xfloor 같은 바닥 변형)
 ├ layer    : Ground / Object  (같은 칸 공존 여부)
 ├ footprint: 1×1 기본, N×M (마을 건물)
 ├ 회전 허용, 높이 오프셋, 필요 태그(검증용)
 └ 허용 맵 종류 (Dungeon / Overworld / Town)

MapPalette (SO)          정의 묶음 (DungeonPalette / OverworldPalette / TownPalette)

MapProfile (SO)          맵 종류별 규칙
 ├ kind (LocationType 재사용), 기본 크기, 팔레트, 저장 폴더/이름 규칙
 ├ 루트에 붙일 컴포넌트 (RegionMarker, SceneLocation 등)
 └ 검증 규칙 (던전: Entrance/Exit 각 1개 필수)

MapRoot                  → 따로 만들지 않고 MapBuilder가 맵 루트 역할을 겸한다 (B3)
```
- 정의 필드는 필요한 단계에서 추가한다. SO에 필드를 *추가*하는 건 기존 애셋에 안전하다
- `MapProfile`(오버월드·마을)은 루트에 **`SceneLocation` 자동 부착**
- B5 맵 검사의 "원점 ≠ (0,0)" 검사는 N4-A의 `LocationGridBuilder.TryGetFloorMinCell`을 **그대로 재사용** — 에디터와 런타임 판정을 하나로
- **함정 해결**: 함정도 `category = Trap`인 정의 하나. 새 함정 = SO 만들어 팔레트에 추가, 코드 수정 0
- 함정 수치(데미지·쿨다운)는 지금처럼 **프리팹 컴포넌트에** 둔다. 수치만 다른 버전은 Prefab Variant + 정의 하나 더. SO에 수치를 옮기는 `TrapDefinition`은 밸런싱 요구가 생기면
- `Blocker` 카테고리 배치 시 `GridBlocker` 자동 부착 (N4-A와 연결)
- 현재 팔레트 밖에서 손으로 배치 중인 것도 등록: Xfloor, Bonus, torch_mounted, wall_doorway, NPC

#### 맵 프리팹 표준 구조 (새로 놓는 오브젝트만. 기존 Demomap 자식은 옮기지 않음)
```
Demomap1 (MapRoot)       일반 프리팹. Variant 금지
 ├ Ground      바닥/입구/출구/함정 바닥
 ├ Walls
 ├ Objects     상자, Pushable, 문, 장치
 ├ Actors      적, NPC
 └ Decoration  횃불, 무덤 등 그리드 무관
```
그룹 오브젝트는 Untagged → `LocationGrid.IsTileRoot` 규칙 그대로 만족, 런타임 수정 불필요.
점유 인덱스 `(cell, layer)`는 직렬화하지 않고 계층에서 재계산 (지금은 한 칸에 하나만 허용돼서 바닥 위 상자를 놓으면 바닥이 지워짐 → `currentHeight`로 우회 중).

#### 옛 맵빌더의 남은 잔버그
월드 좌표 기준 `SyncPlacedTiles`(B2), 팔레트 하드코딩(B1), 가이드 Plane 머티리얼이 씬에 누수, 고스트가 `SkinnedMeshRenderer` 미지원.

### 정리 항목
- `Dungeon.unity`의 `NavMeshSurface`·`NavMeshModifier` 잔재 → A\* 통일했으니 N4 이후 제거
- SO 애셋 위치 통일 (`Assets/Scripts/SO/NPC/` vs `Assets/SO/`)
- `NPC_Test`의 모델 자식(`Barbarian`) 레이어 0 → NPC(11)
- Firstmap1~5, TestMap: 현재 미사용. **그대로 둔다** (사용자 결정)
- 옛 병합 충돌 표시(`<<<<<<<<`)가 남아 guid가 깨진 `.meta` 2개: `Prefabs/Tile/Test/BoxTile.prefab.meta`, `Prefabs/Tile/Test/New Tile Palette.prefab.meta` (오래전 `84c12fe9e` 병합 흔적) → M4 정리 때

---

## 5. 지켜야 할 원칙 (이전 NPC 계획서에서 요약)

1. **`[SerializeField]` 필드를 다른 컴포넌트로 옮기지 않는다** — 기존 프리팹 값이 전부 날아감. 필드 *추가*는 안전. 그래서 `GridMover`·`Perception`은 순수 C# 클래스이고 설정값은 소유자(`EnemyController`/`NpcActor`)에 있다
2. **레이어는 광역 필터, 진영은 정밀 판정** — 적대 NPC를 만들 땐 레이어 마스크도 넓혀야 함
3. **진영 없는 대상(횃불·상자)은 진영 검사를 건너뛴다**
4. **플레이어가 상대하는 창구는 `NpcActor` 하나** — 모듈(`DialogueModule` 등)은 `IInteractable`이 아님
5. **능력은 bool이 아니라 컴포넌트 유무 / 이동 방식은 브레인 교체로**
6. **Phase마다 "동작 변화 0"** — 리팩터링에 기능 개선 섞지 않기. N4-A도 "던전 무변화"가 완료 조건
7. **검증할 대상이 없으면 파일을 건드리지 않는다**

### 프로젝트 고유 함정
- 레이어 추가·씬 변경은 **Ctrl+S 전까지 디스크에 없음**
- 새 `.cs`의 `.meta`는 **Unity 창에 포커스를 줘야** 생성됨 → `.meta` 없이 커밋 금지
- 상호작용 사거리는 트리거가 아니라 `Player.prefab`의 `Interact Range`(1.0)
- NPC `Obstacle Layer`는 `Wall`이 아니라 `Interactable`(6) — 벽·상자·밀기 오브젝트가 전부 여기
- 파괴된 오브젝트를 인터페이스로 캐시하면 `null`이 안 됨 (Unity `==` 오버로드 안 탐)
- 도메인 리로드 끄면 static이 살아남음 → `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 초기화
- **프리팹 인스턴스를 맵 컨테이너로 쓰지 말 것** (이번 사고)

---

## 6. ⚠️ 결정 대기

1. **마을 바닥 타일** — 오버월드 `Plane`(풀밭) / 던전 `Road` / 마을 전용 타일 새로 만들기. N4-B 전까지만 정하면 됨

해결됨: 맵빌더 재개 시점 → N4-D 다음 (2026-09-28) / `Plane.prefab` 태그 → `fd16ae9b8`에 커밋됨

---

## 7. 작업 재개 체크리스트

1. **pull 전에 `git status -sb`로 로컬 미푸시 커밋 확인.** 두 PC에서 따로 커밋하면 브랜치가 갈라져 병합 충돌이 난다 (2026-09-28 사고)
2. `git pull` → 이 문서 §0 상태표와 `git log --oneline -5` 비교
3. Unity 열고 컴파일 에러 0 확인
4. §0에서 🔨 표시된 단계부터 이어서

### Unity 없이 컴파일만 검증하는 법
Unity가 열려 있으면 포커스를 줄 때까지 컴파일이 안 되므로, Roslyn을 직접 호출한다:
```bash
D="/c/Program Files/Unity/Hub/Editor/6000.3.17f1/Editor/Data"
P="/c/Github/Project-DP/Project DP"
# check.rsp 에 다음을 넣는다:
#   -target:library -nostdlib -nologo -langversion:9 -nowarn:CS0618 -out:"<임시폴더>/check.dll"
#   -r:"$D/NetStandard/ref/2.1.0/netstandard.dll"
#   -r:"$D/Managed/UnityEngine/*.dll" 각각
#   -r:"$P/Library/ScriptAssemblies/Assembly-CSharp.dll"   (에디터 스크립트 검사 시)
#   검사할 .cs 파일들
"$D/NetCoreRuntime/dotnet.exe" "$D/DotNetSdkRoslyn/csc.dll" @check.rsp
```
런타임 스크립트 전체를 검사할 때는 `Assembly-CSharp.dll` 대신 `Scripts/**/*.cs`를 소스로 넣고,
`Library/ScriptAssemblies/`의 다른 DLL(Assembly-CSharp* 제외)을 참조로 추가한다.
