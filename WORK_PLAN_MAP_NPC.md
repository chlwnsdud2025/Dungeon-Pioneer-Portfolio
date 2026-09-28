# 맵빌더 개편 + NPC 이동 통일 — 작업 기록과 계획

> 최종 갱신: 2026-09-28 / 브랜치 `JunYeoung_Branch`
> 이 문서는 인수인계용입니다. 계획이 바뀌면 이 문서도 같이 고치고, 낡으면 지웁니다.
> 이전 NPC 계획서 원문은 커밋 `842b58c5b`의 `NPC_SYSTEM.md`에 있습니다 (`git show 842b58c5b:NPC_SYSTEM.md`).

---

## 0. 지금 상태 한눈에

| 항목 | 상태 |
|---|---|
| 맵빌더 버그 원인 분석 | ✅ 완료 (§1) |
| **M0** 맵빌더 긴급 복구 | ✅ 커밋·push `a2b17923f` |
| 오버월드 바닥 `Floor` 태그 | ✅ 작업함 · **미커밋** (`Prefabs/Tile/Overworld/Plane.prefab` 1줄) |
| 마을 바닥 태그 | ⬜ 대상 없음 — 마을 바닥이 타일이 아님 (§4 N4-B) |
| **이동 방식 결정** | ✅ **어디서든 A\*** (던전 밖 NavMesh 안 쓰기로 함) |
| N4-A ~ N4-D (NPC 이동 통일) | ⬜ 다음 작업 |
| M1 ~ M4 (맵빌더 본 개편) | ⏸ 보류 — NPC 작업 뒤 |

**결정 대기 3건 → §6**

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

### 조사해 둔 현재 씬 상태
- **Overworld**: `OverworldMap` 인스턴스(원점, 오버라이드 없음) + Hill/Tree/Grass/fence/arch 등 자유 배치 + `Wall_Top/Bottom/Left/Right/Back` + `rigionMarker` + `NPC_Test` + `Test_Potal`
- **Town**: Unity 기본 **Plane 1개(scale 10 = 100×100m, Untagged)** + `SpawnAnchor_Start` + `Test_Potal`. 타일 바닥 없음
- `Floor` 태그를 읽는 코드는 전부 던전 전용 (`DungeonGenerator`, `LocationGridBuilder`, `LocationGrid`, `MapDecoration`, `PushableObject`, `TrapTile`) → 오버월드 Plane 태그 변경은 런타임 무영향
- `MinimapManager.SwitchLocation`은 `DungeonManager`에서만 호출됨

---

## 4. 로드맵

### 🅝 NPC 트랙 — 지금 진행

| 단계 | 내용 | 완료 조건 |
|---|---|---|
| **N4-A** 씬 장소 그리드 | ① `SceneLocation` 컴포넌트: 씬에 놓인 맵 루트에 붙어 Awake/Start에 `LocationRoot` 추가 + `LocationGridBuilder.Build` + 씬 안 액터들 `NotifyLocationReady` ② 맵 자식이 아닌 액터용 폴백: `LocationRoot`를 씬 단위로 조회 (예: `LocationRoot.FindInScene(scene)`) — `NpcActor`·`EnemyController`의 `NotifyLocationReady` 1줄씩 ③ `GridBlocker` 컴포넌트 + `LocationGridBuilder`에 추가 패스 ④ 그리드/막힌 칸 디버그 기즈모 | **던전 동작 무변화** + 오버월드에서 그리드·막힌 칸이 보임 |
| **N4-B** 마을 바닥 | 기본 Plane 제거 → 타일 바닥(태그 `Floor`)으로 교체, 원점 (0,0) 규칙 준수. 건물·소품에 `GridBlocker` | 마을에서 그리드 생성 |
| **N4-C** 이동 브레인 | `GridSearch` 추출 (`EnemyPatrolState`의 BFS를 정적 헬퍼로, 적·NPC 공용), `WanderBrain`, `RouteBrain`, `NpcWaypointRoute` | 오버월드·마을 NPC가 A\*로 돌아다님 + **적 순찰 회귀 없음** |
| **N4-D** 던전 NPC 검증 | 이전 계획서 §8 미검증 항목: Demomap1의 NPC가 `Grid` 획득 → 층 전환 `ResetToSpawn` | 던전에서도 같은 브레인 동작 |
| N5 | 어포던스 (`IAffordance` + Chest/Stair/Torch 어댑터) | 이전 계획서 그대로 |
| N5.5 | 함정 `IDamageable` 일반화 (전제: 백스탭은 공격자가 `IFactionMember`일 때만) | 이전 계획서 그대로 |
| N6 ~ N8 | `DelverBrain` / 파티 (Utility + FSM) / 추상 시뮬 & AI LOD | 이전 계획서 그대로 |

### 🅜 맵빌더 트랙 — 보류 (재개 시점 §6-2)

| 단계 | 내용 |
|---|---|
| M0 | ✅ 완료 |
| **M1** 데이터 계층 (SO) | 아래 설계 참고 |
| **M2** 새 에디터 | `Tools/Map Builder` EditorWindow + SceneView 오버레이. **Prefab Mode에서 맵 프리팹을 직접 편집** (씬 작업공간·"굽기" 폐지 → Undo/저장은 Unity 기본 동작, Variant·이중보관 원천 차단). 도구: 칠하기/지우기/사각 채우기/스포이드/회전(Shift+R)/브러시 크기/층/레이어 필터. "새 맵 만들기(프로필 선택)" |
| **M3** 검증·연결 | 검증 패널(태그-카테고리 불일치, Entrance/Exit 개수, 격자 이탈, 중복 칸, Missing prefab, 원점 ≠ (0,0), 연결 안 된 발판) + 함정 연결 도구(발판 선택 → 화살 함정 클릭, `ArrowTrapAutoWire` 로직 재사용) |
| **M4** 정리 | 구 `MapBuilder`/`MapBuilderEditor` 삭제, MapBuildScene 폐기(또는 조명 미리보기용 빈 씬), 사용법 문서 |

#### M1 설계
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

MapRoot (MonoBehaviour)  맵 프리팹 루트. profile, size. 원점 (0,0) 규칙 강제
```
- **함정 해결**: 함정도 `category = Trap`인 정의 하나. 새 함정 = SO 만들어 팔레트에 추가, 코드 수정 0
- 함정 수치(데미지·쿨다운)는 지금처럼 **프리팹 컴포넌트에** 둔다. 수치만 다른 버전은 Prefab Variant + 정의 하나 더. SO에 수치를 옮기는 `TrapDefinition`은 밸런싱 요구가 생기면
- `Blocker` 카테고리 배치 시 `GridBlocker` 자동 부착 (N4-A와 연결)
- 현재 팔레트 밖에서 손으로 배치 중인 것도 등록: Xfloor, Bonus, torch_mounted, wall_doorway, NPC

#### 맵 프리팹 표준 구조
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

#### 구 맵빌더의 남은 잔버그 (M4에서 사라짐)
월드 좌표 기준 `SyncPlacedTiles`, 가이드 Plane 머티리얼이 씬에 누수, 고스트가 `SkinnedMeshRenderer` 미지원, 팔레트 하드코딩.

### 정리 항목
- `Dungeon.unity`의 `NavMeshSurface`·`NavMeshModifier` 잔재 → A\* 통일했으니 N4 이후 제거
- SO 애셋 위치 통일 (`Assets/Scripts/SO/NPC/` vs `Assets/SO/`)
- `NPC_Test`의 모델 자식(`Barbarian`) 레이어 0 → NPC(11)
- Firstmap1~5, TestMap: 현재 미사용. **그대로 둔다** (사용자 결정)

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

1. **마을 바닥 타일** — 오버월드 `Plane`(풀밭) / 던전 `Road` / 마을 전용 타일 새로 만들기
2. **맵빌더(M1~) 재개 시점** — 추천: **N4 이후, N5 전.** N4-B에서 마을을 타일로 깔 때 임시 수단(구 빌더·스크립트)의 한계가 드러나고, N5 이후 NPC 작업은 맵 콘텐츠 없이 검증이 어려움. 대안: NPC 전체(N8) 완료 후
3. **`Plane.prefab` 태그 변경 커밋** — 바로 커밋 / N4-A와 함께. Unity에서 OverworldMap 타일들이 `Floor`로 보이는지 확인 필요

---

## 7. 작업 재개 체크리스트

1. `git pull` → `git log --oneline -3`에 `a2b17923f 맵빌더 0단계 복구`가 있는지 확인
2. 이 문서가 커밋돼 있는지, `Plane.prefab` 태그 변경이 들어왔는지 확인 (`git show HEAD --stat`)
3. Unity 열고 컴파일 에러 0 확인
4. `Overworld.unity`에서 OverworldMap 타일 하나 선택 → Tag가 `Floor`인지 확인
5. §6 결정 → **N4-A 착수**

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
