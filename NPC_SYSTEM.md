# NPC 시스템 — 작업 기록과 계획

> 최종 갱신: 2026-09-23 / 브랜치 `JunYeoung_Branch`
> 목표: 던전 내부를 스스로 탐험·전투하는 NPC 파티(1~3인). 그 전에 NPC 전반의 구조를 먼저 세운다.

---

## 1. 지금 상태 한눈에

| Phase | 내용 | 상태 |
|---|---|---|
| **0** | 액터 공통 기반 (`IGridAgent`, `GridMover`, `Perception`, `ActorMath`) | ✅ 커밋됨 `5eb9c40e9` |
| **1** | 진영 (`FactionId`, `FactionTable`, `FactionOverride`) | ✅ 커밋됨 `cd90cbc5a` |
| **2** | NPC 뼈대 (`NpcActor`, `NpcBrain`, `StaticBrain`, `NpcDefinition`) | ✅ 동작 확인 · **미커밋** |
| **3** | 대화 (`NpcInteractionModule`, `DialogueModule`, `SpeechBubble`) | ✅ 동작 확인 · **미커밋** |
| **4** | 이동 브레인 + 마을 이동 수단 | ⬜ **방식 결정 대기** (아래 §6) |
| **5** | 어포던스 | ⬜ |
| **5.5** | 함정 `IDamageable` 일반화 | ⬜ |
| **6** | `DelverBrain` — 혼자 던전 탐험 | ⬜ |
| **7** | 파티 | ⬜ |
| **8** | 추상 시뮬 & AI LOD | ⬜ |

**집에서 제일 먼저 할 일: Phase 2+3 커밋.** 미커밋 작업이 쌓여 있어서 Phase 4에서 문제가 생기면 되돌릴 지점이 애매하다.

---

## 2. 전체 아키텍처

```
[Layer 0] Actor Core            — 적·NPC 공용. Scripts/Actor/
  IGridAgent          GridMover가 의존하는 최소 계약
  GridMover           A* 이동 (경로 평활화·끼임 복구·구덩이 등반)
  IPerceptionAgent / Perception   FOV·시야선
  ActorMath           수평 거리·회전

[Layer 1] Faction               — Scripts/Actor/
  FactionId / Stance  진영과 태도
  IFactionMember      진영을 가진 액터
  FactionTable        5×5 대칭 관계표 + 판정 단일 창구
  FactionOverride     개체 단위 예외 (비대칭)

[Layer 2] NPC Body              — Scripts/NPC/
  NpcActor            몸. IGridAgent + IPerceptionAgent + IFactionMember
                           + ITarget + IInteractable
  NpcDefinition (SO)  스탯·진영·지각 범위
  NpcRegistry         활성 NPC 목록

[Layer 3] Brain (교체 가능)      — Scripts/NPC/Brains/
  NpcBrain            베이스. NpcActor당 정확히 하나
  StaticBrain         정지 NPC
  (WanderBrain / RouteBrain / DelverBrain 예정)

[Layer 4] Interaction Modules   — Scripts/NPC/Interaction/
  NpcInteractionModule  베이스. IInteractable이 '아님' (이유는 §4-③)
  DialogueModule        말풍선 대화
  (ShopModule 예정)

[Layer 5] World UI              — Scripts/UI/Components/
  SpeechBubble        월드 말풍선. NPC를 모름

(예정) Party / Simulation
```

---

## 3. 만든 파일 목록 (1,298줄)

### Scripts/Actor/ — 적·NPC 공용
| 파일 | 줄 | 요약 |
|---|---|---|
| `IGridAgent.cs` | 48 | GridMover가 보는 계약 (11멤버) |
| `GridMover.cs` | 369 | `EnemyGridMover`를 일반화. A*·평활화·끼임·등반 |
| `Perception.cs` | 79 | `IPerceptionAgent` + FOV/시야선. 읽기 전용 |
| `ActorMath.cs` | 36 | 수평 거리, 즉시/점진 회전 |
| `FactionId.cs` | 29 | `FactionId`(Neutral/Player/Monster/Delver/Town), `Stance` |
| `IFactionMember.cs` | 21 | `Faction` + `Transform` + `Overrides` |
| `FactionTable.cs` | 110 | 대칭 관계표, `GetStance`(방향성), `IsHostile` |
| `FactionOverride.cs` | 59 | 개체 예외 (인스펙터 + 런타임) |

### Scripts/NPC/
| 파일 | 줄 | 요약 |
|---|---|---|
| `NpcActor.cs` | 247 | NPC의 몸. 인터페이스 5개 구현 |
| `NpcBrain.cs` | 32 | 브레인 베이스 |
| `NpcRegistry.cs` | 25 | 활성 NPC 정적 목록 |
| `Brains/StaticBrain.cs` | 20 | 정지 (중력만 적용) |
| `Data/NpcDefinition.cs` | 38 | SO — 스탯·진영·지각 |
| `Data/DialogueData.cs` | 18 | SO — 대사 줄 목록 |
| `Interaction/NpcInteractionModule.cs` | 24 | 모듈 베이스 |
| `Interaction/DialogueModule.cs` | 97 | 대사 진행, 거리 기반 종료 |

### Scripts/UI/Components/
| 파일 | 줄 | 요약 |
|---|---|---|
| `SpeechBubble.cs` | 46 | 월드 말풍선. `Show(text)` / `Hide()` |

### 수정한 기존 파일
| 파일 | 변경 |
|---|---|
| `Enemy/EnemyController.cs` | `IGridAgent`·`IPerceptionAgent`·`IFactionMember` 구현, 지각 메서드를 위임으로, `eyeHeight`·`faction` 필드 추가 |
| `Enemy/State/EnemyAttackState.cs` | 진영 게이트 1줄 |
| `Player/PlayerController.cs` | `IFactionMember` 구현 |
| `Combat/WeaponHitbox.cs` | 진영 게이트 + 공격자 해석을 `IFactionMember`로 일반화 |
| `Core/DungeonManager.cs` | `NotifyEnemiesLocationReady` → `NotifyActorsLocationReady` (NPC도 깨움) |
| *(삭제)* `Enemy/EnemyGridMover.cs` | → `Actor/GridMover.cs`로 이동·일반화 |

### 에디터 산출물
- **NPC 레이어 = 11** (`ProjectSettings/TagManager.asset`)
- `Assets/Scripts/SO/NPC/Npc_Villager.asset` — `faction: Town`
- `Assets/Scripts/SO/NPC/Dialogue_Villager.asset`
- `Assets/Prefabs/ui/WorldUI/SpeechBubble.prefab` — World Space Canvas + `Billboard` + `SpeechBubble`
- `Overworld.unity`의 `NPC_Test` 오브젝트

---

## 4. 지켜온 설계 원칙 (계속 지킬 것)

### ① `[SerializeField]` 필드는 절대 다른 컴포넌트로 옮기지 않는다
옮기는 순간 이미 만들어 둔 프리팹의 인스펙터 값이 전부 날아간다.
그래서 `GridMover`·`Perception`을 MonoBehaviour가 아니라 **순수 C# 클래스**로 만들고,
설정값은 소유자(`EnemyController`/`NpcActor`)에 남긴 채 인터페이스로 **읽기만** 한다.
필드 *추가*는 안전하다(새 필드는 C# 초기값을 받음). 금지된 건 *이동*.

### ② 레이어는 광역 필터, 진영은 정밀 판정
`OverlapSphere`/`SphereCast`는 레이어로 후보를 줄이고(물리 엔진, 빠름),
그 후보 중 적대 진영만 고른다(게임 규칙, 정확함).
레이어를 진영으로 대체하면 물리 쿼리 비용이 폭증한다.

→ 따름: 적대 NPC를 만들려면 **레이어 마스크도 넓혀야** 한다. 진영만 바꿔선 광역 필터에서 걸린다.

### ③ 진영이 없는 대상은 진영 검사를 건너뛴다
횃불·상자는 `IDamageable`이지만 `IFactionMember`가 아니다.
```csharp
if (attacker != null && damageable is IFactionMember target && !FactionTable.IsHostile(attacker, target))
    return;   // 진영이 있는 액터인데 적대가 아니면 차단. 진영이 없으면 그대로 통과
```
이 규칙이 없으면 적이 횃불을 못 부순다.

### ④ 플레이어가 상대하는 창구는 `NpcActor` 하나
`DialogueModule`을 직접 `IInteractable`로 만들면, 상점 모듈이 같은 NPC에 붙는 순간
`GetComponentInParent<IInteractable>()`가 뭘 집을지 알 수 없어진다.
→ `NpcActor`가 `IInteractable`을 구현하고, **우선순위가 가장 높으면서 지금 사용 가능한** 모듈로 넘긴다.

### ⑤ 능력은 bool이 아니라 컴포넌트 유무로
`isInteractable` 같은 플래그를 두지 않는다. `DialogueModule`이 붙어 있으면 말을 걸 수 있다.
"정지/이동" 토글도 bool이 아니라 **브레인 교체**로 표현한다 —
옵션은 둘까지는 괜찮지만 셋째(배회·순찰·추종·스케줄)부터 `if` 지옥이 된다.

### ⑥ Phase마다 "동작 변화 0"을 목표로
Phase 0·1·2는 전부 기존 동작을 바꾸지 않는 게 완료 조건이었다.
그래서 회귀 테스트만으로 판정할 수 있었다. 리팩터링에 기능 개선을 섞지 말 것.

### ⑦ 검증할 대상이 없는 상태로 파일을 건드리지 않는다
함정 일반화를 Phase 2에서 뺀 이유. 던전을 걷는 NPC가 없으면 확인할 방법이 없다.

---

## 5. 프로젝트 고유 함정 (실제로 당한 것들)

### Unity가 디스크에 안 쓴다
- **레이어 추가 후 `Ctrl+S`(File > Save Project)** 를 안 누르면 `TagManager.asset`에 안 들어간다.
- 씬 변경도 `Ctrl+S` 전까지 파일에 없다.
- 새 `.cs`의 `.meta`는 Unity 창에 **포커스를 줘야** 생성된다. 팀 저장소라 `.meta` 없이 커밋하면
  나중에 GUID가 엇갈린다 — 특히 MonoBehaviour.

### `SphereCast`와 상호작용 거리
`PlayerController`의 상호작용은 `Physics.SphereCast(origin, 0.5, forward, 1.0, Interactable)`.
- `interactRange`는 **코드 기본값 1.5가 아니라 `Player.prefab`의 1.0**이 실제 값이다.
- 상호작용 사거리를 늘리려면 트리거 콜라이더가 아니라 `Player.prefab`의 `Interact Range`를 키워야 한다.

### `obstacleLayer`는 `Wall`이 아니라 `Interactable`
이 프로젝트는 벽·상자·밀기 오브젝트를 전부 `Interactable`(레이어 6)에 둔다.
`Perception.HasLineOfSight`가 레이캐스트 후 `Wall` 태그 / `WallObject`로 걸러낸다.
NPC의 `Obstacle Layer`도 `Interactable`이어야 한다.

### 파괴된 오브젝트를 인터페이스로 들고 있으면 `null`이 아니다
`IFactionMember owner`처럼 인터페이스로 캐시하면 Unity의 `==` 오버로드를 타지 않아
대상이 파괴돼도 C# `null`이 되지 않는다. → `WeaponHitbox`는 스윙마다 다시 찾는다.

### static 필드와 도메인 리로드
`Enter Play Mode Options`로 도메인 리로드를 끄면 static이 플레이 종료 후에도 살아남는다.
`FactionTable`은 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 매번 초기화한다.
(`DungeonRunState` 등 기존 static도 같은 위험을 안고 있음)

---

## 6. ⚠️ Phase 4 시작 전 결정할 것 — 마을 이동 수단

### 문제
`LocationGrid`는 **던전에만** 있다. `DungeonGenerator`가 맵을 Instantiate할 때 만든다.
`Town.unity`·`Overworld.unity`에는 `LocationRoot`도 Floor 타일도 없다. → `GridMover`(A*)를 못 쓴다.

실제 좌표 범위:
```
Overworld :  x [-18.9 ~ 27.1]   z [-13.8 ~ 36.7]
Town      :  x [  0.0 ~ 16.0]   z [-10.0 ~  0.0]
```
**둘 다 음수 좌표를 포함**한다. 그런데 `LocationGrid`에는 원점 오프셋이 없어
`InBounds`가 `0 ≤ x < Size.x`를 요구한다.

### 선택지

**C. 마을에도 그리드** (한 번 결정했다가 비용 재산정 중)
- `LocationGrid`에 `Origin` 도입 → `LocationGrid`, `AStarPathfinder`, `GridMover`, `PushableObject`, `LocationGridBuilder` 5개 파일 수정. **A\* 길찾기를 건드림 = 던전 회귀 위험**
- `PhysicsGridBaker` 직접 구현 (~250줄) + 레이어·높이차·샘플 수 튜닝
- `GridSystem.TileSize`가 2라 **건물 사이 1.5m 골목이 통째로 막힐 수 있음**
- 신규/수정 약 450줄

**B. 마을·필드는 NavMesh** ← **추천**
- `com.unity.ai.navigation` **2.0.12 이미 설치됨**. `Dungeon.unity`에 `NavMeshSurface`+`NavMeshModifier` 사용 흔적도 있음
- `LocationGrid`를 **전혀 안 건드림** → 던전 회귀 위험 0
- 베이킹은 Unity가 함 (`NavMeshSurface` 붙이고 Bake)
- 대신 `ILocomotion` 추상화 + `NavMeshLocomotion` 어댑터 (~120줄)
- 신규 약 150줄, 기존 파일 수정 0

### B를 고를 때의 핵심 코드
```csharp
public interface ILocomotion
{
    void SetDestination(Vector3 worldPosition);
    void Stop();
    bool IsMoving { get; }
    bool HasArrived { get; }
}
// GridLocomotion   : GridMover 래퍼 (던전)
// NavMeshLocomotion: NavMeshAgent 래퍼 (마을·필드)
// NpcActor가 Grid 유무로 고르고, 브레인은 ILocomotion만 본다.
```

NavMeshAgent와 CharacterController가 둘 다 Transform을 움직이려 하므로,
**에이전트를 경로 계산기로만** 쓴다:
```csharp
agent.updatePosition = false;
agent.updateRotation = false;
// 매 프레임
Vector3 move = agent.desiredVelocity.normalized * speed * Time.deltaTime;
actor.Move(move);                    // 기존 CharacterController 경로 (중력 포함)
agent.nextPosition = transform.position;
```
이러면 마을 NPC도 던전 NPC와 같은 `NpcActor.Move`를 타서 이동감이 일치한다.

> "이동 계통 통일"이라는 원래 의도는 `ILocomotion` 레벨에서 지켜진다.
> 브레인 입장에서는 여전히 "저기로 가라" 한 가지 명령이고,
> 그 아래가 그리드냐 NavMesh냐는 `NpcActor`가 고른다.
> 이 추상화는 C를 골라도 결국 필요해진다 (파티 멤버가 마을에서 플레이어를 따라오는 순간).

---

## 7. 앞으로의 계획

### Phase 4 — 이동 브레인 (B 선택 시)
| 단계 | 내용 | 검증 |
|---|---|---|
| 4-A | `ILocomotion` + `GridLocomotion` + `NavMeshLocomotion` | 던전 적 동작 무변화 |
| 4-B | Town/Overworld에 `NavMeshSurface` 추가 + Bake (**에디터 작업만, 코드 0**) | 씬 뷰에 파란 NavMesh |
| 4-C | `GridSearch` 추출, `WanderBrain`, `RouteBrain`, `NpcWaypointRoute` | NPC가 마을을 돌아다님 |

`GridSearch`는 `EnemyPatrolState`의 BFS를 정적 헬퍼로 뽑은 것. 적·NPC 공용. (적 순찰 회귀 검증 필요)

### Phase 5 — 어포던스
NPC가 상자·계단·횃불·문을 특수 케이스 `if` 없이 다루게 한다. The Sims의 Smart Object 패턴.
```
Scripts/NPC/Affordance/
  IAffordance.cs        Kind, ApproachCell, IsAvailable, EstimatedSeconds, Execute(NpcActor)
  AffordanceKind.cs     Loot / Descend / Ascend / LightTorch / OpenDoor / Rest
  AffordanceRegistry.cs 층 단위 등록·근접 조회
  ChestAffordance.cs / StairAffordance.cs / TorchAffordance.cs   ← 어댑터
```
**어댑터로 분리하는 이유**: `TreasureChest`가 직접 `IAffordance`를 구현하면 맵 오브젝트가 NPC 시스템을 알게 된다.
어댑터 컴포넌트를 프리팹에 추가로 붙이면 기존 코드는 무수정이다.

`TreasureChest.Open(PlayerController)`가 플레이어를 직접 받으므로,
NPC용 보상 처리는 `ChestAffordance`에서 따로 한다(플레이어 보상 경로를 안 건드린다).

### Phase 5.5 — 함정 `IDamageable` 일반화
지금 함정들이 구체 타입을 직접 받아서, **NPC가 생기면 함정이 NPC를 안 때린다.**
```
Map/Arrow.cs:82,94        player.TakeDamage(damage, transform)
Map/Arrow.cs:101          enemy.TakeDamage(damage)
Map/PushableObject.cs:293 enemy.TakeDamage(crushDamage)
Map/RollingRock.cs:123    enemy.TakeDamage(damage)     ← 플레이어는 ApplyKnockback 특수 처리라 분기 유지
Map/TrapTile.cs:353,422   enemy/player.TakeDamage(...)  ← List<EnemyController>, Dictionary<EnemyController,float>
                                                            로 적을 추적해서 제일 까다로움
Map/WallTrap.cs:90,98     player/enemy.TakeDamage(...)
```
난이도: `WallTrap`·`PushableObject`·`Arrow` 쉬움 / `RollingRock` 중간 / **`TrapTile` 어려움**.

**전제 작업 — `EnemyController` 백스탭 규칙 변경**:
지금 함정은 적에게 `attacker = null`로 때린다. `IDamageable`로 일반화하면서 `transform`을 넘기면
FOV 밖 판정에 걸려 **함정에 뒤통수 맞았다고 1.5배**를 받게 된다.
→ **백스탭은 공격자가 `IFactionMember`일 때만** 적용하도록 바꾸면 현재 동작이 그대로 보존된다.
(환경 피해는 진영과 무관하므로 `FactionTable`을 거치지 않는다)

### Phase 6 — `DelverBrain` (혼자 던전 탐험)
```
Scripts/NPC/Delver/
  DelverBrain.cs        오더 실행 FSM (EnemyStateMachine과 같은 Type-키 딕셔너리)
  DelverMemory.cs       개인 fog-of-war: HashSet<Vector2Int> visited
  States/  DelverIdle / DelverMove / DelverEngage / DelverInteract / DelverDowned
```
여기서 **적 탐지의 진영화**도 한다:
`EnemyController.TryDetectPlayer`(구체 타입)를 "가장 가까운 적대 `IFactionMember`"로 일반화.
Phase 1에서 일부러 미뤘던 작업 — 그때는 진영을 가진 대상이 플레이어뿐이라 검증할 수 없었다.

적의 `AttackTargetMask`에 NPC 레이어를 더하는 것도 여기서. (원칙 ②)

### Phase 7 — 파티 (Full 시뮬)
**2계층 Squad AI** (Halo / Killzone / L4D 패턴):
```
PartyBrain (상위: Utility 점수로 목표 선택)
  후보: Explore / Loot / Fight / LightUp / Descend / Retreat / Extract
  ↓ PartyOrder 발행
DelverBrain × 1~3 (하위: FSM으로 실행)
```
```
Scripts/NPC/Party/
  PartyState.cs       [Serializable] 순수 데이터. 멤버 체력·인벤·현재 맵·셀·목표
  PartyMemberState.cs
  PartyOrder.cs       struct. Follow / MoveTo / Engage / Interact / Retreat
  PartyGoal.cs
  FloorDigest.cs      순수 데이터. 상자 셀 목록, 적 수, 계단 셀, 위험도
  PartyBrain.cs       ★ static 순수 함수. Decide / IssueOrders / Estimate
  GoalScorers.cs      Utility 점수 커브
  PartyController.cs  Full 실행기 (MonoBehaviour)
  Formation.cs
  PartyRegistry.cs    static. 런 전체의 모든 PartyState 소유 (DungeonRunState와 같은 결)
Scripts/NPC/Data/PartyTemplate.cs   SO
```

### Phase 8 — 추상 시뮬 & AI LOD
```
   PartyState   (순수 데이터)
   FloorDigest  (순수 데이터)
          ↓
   PartyBrain.Decide(state, digest) → PartyGoal    ★ MonoBehaviour 아님. 순수 함수
          ↓                    ↓
 [Full] DelverBrain 실행   [Abstract] AbstractPartySim이 확률로 실행
          ↓                    ↓
          └──→ PartyState 갱신 ←──┘
```

**🔴 최우선 제약: `PartyBrain`은 `Transform`·`Physics`·`Time.deltaTime`을 절대 참조하지 않는다.**
이 규칙만 지키면 LOD 전환이 "실행기 교체"로 끝나고, 어기면 추상 시뮬을 통째로 다시 짜야 한다.
`PartyBrain.Estimate`(추상 시뮬용 결과 추정)를 `Decide`와 **같은 파일에** 두어,
목표를 추가할 때 양쪽을 한 번에 정의하도록 강제한다.

```
Scripts/NPC/Simulation/
  PartySimLevel.cs          enum { Frozen, Abstract, Full }
  AbstractPartySimulator.cs
  AbstractOutcome.cs
  PartyMaterializer.cs      Abstract ↔ Full 전환 (스폰/디스폰 시 역기록)
  FloorDigestBuilder.cs     맵 '프리팹'을 스캔 → 미생성 층도 커버
  PartySimDirector.cs
```

**LOD 규칙**
| 조건 | 레벨 |
|---|---|
| 파티 mapId == 플레이어 현재 mapId | **Full** (프리팹 스폰) |
| 던전 내 다른 층 | **Abstract** (1~2초 간격 틱) |
| 플레이어가 오버월드 | **Frozen** |

Frozen인 이유: 플레이어가 마을에서 30분 쇼핑하고 왔더니 NPC 파티가 던전을 클리어해버리면 게임이 망가진다.
"던전 시간은 플레이어가 던전 안에 있을 때만 흐른다"가 `DungeonRunState`의 런 단위 리셋과도 맞는다.

**추상 시뮬 결과는 기존 `MapSnapshot`/`DungeonRunState`에 그대로 기록한다.**
"NPC 파티가 1층 상자를 털었다" = `snapshot.objects[("Chest", cell)].flag = true`.
플레이어가 나중에 그 층에 가면 기존 `RestoreMapState` 경로가 열린 상자를 복원한다. 별도 동기화 코드 불필요.

**전환 시 데이터 흐름 (버그가 제일 잘 나는 지점)**
```
Abstract → Full :  PartyState → 프리팹 스폰 → 체력 주입 → NotifyLocationReady
Full → Abstract :  NpcBrain.OnDespawn() → PartyState에 체력·전리품 역기록 → Destroy
```
**`PartyState`가 항상 정답(single source of truth)이고, 씬의 GameObject는 일시적인 뷰다.**

---

## 8. 미해결 / 확인 필요

| 항목 | 메모 |
|---|---|
| **Phase 2+3 미커밋** | 제일 먼저 처리 |
| **던전 NPC 경로 미검증** | 던전 맵 프리팹에 NPC를 아직 안 넣었다. `NotifyActorsLocationReady` → `Grid` 획득 → 층 전환 `ResetToSpawn`이 한 번도 안 돌았다. Phase 4-C 끝나고 함께 검증하기로 함 |
| `EnemyType_1.prefab`, `EnemyHelthBar.prefab`, `Demomap1.prefab` 수정됨 | 내가 건드린 적 없음. Unity 재직렬화인지 확인 후 커밋 |
| SO 애셋 위치 | `Assets/Scripts/SO/NPC/`에 있는데 다른 SO는 `Assets/SO/`에 있다. 통일할지 선택 |
| 모델 자식 레이어 | `NPC_Test`의 `Barbarian`이 Layer 0. 콜라이더가 없어 무해하지만 일관성상 NPC로 |
| `NPC_Test` 트리거 반지름 3 | 동작에는 문제없음. 상호작용 사거리는 트리거가 아니라 `Player.prefab`의 `Interact Range`(1.0)가 정한다 |
| `Dungeon.unity`의 NavMesh 잔재 | `NavMeshSurface` + `NavMeshModifier` 다수. 지금 던전은 A*를 쓰므로 미사용. B를 고르면 재활용 여지 |
| `NpcStats` 분리 | 지금은 체력 하나라 `NpcActor`에 두었다. Phase 6 전투에서 분리 검토 |
| 상점 모듈 | 언제든 끼워 넣을 수 있음. 본줄기(던전 파티)와 독립 |

---

## 9. 작업 재개 체크리스트

1. `git pull` 후 `git log --oneline -3`으로 `cd90cbc5a 진영 시스템 도입`까지 있는지 확인
2. Unity 열고 컴파일 에러 0 확인 (`.meta` 생성됨)
3. `Overworld.unity` 플레이 → NPC 앞에서 F → 말풍선 뜨면 Phase 3까지 정상
4. **Phase 2+3 커밋**
5. §6의 B/C 결정 → Phase 4 착수

### Unity 없이 컴파일만 검증하는 법
Unity가 생성한 `Assembly-CSharp.csproj`는 URP 순환 참조 때문에 `dotnet build`가 실패한다.
대신 Roslyn을 직접 호출한다:

```bash
# 1) csproj에서 참조 목록과 소스 목록을 뽑아 응답 파일 생성
#    - <HintPath> → -r:"..."
#    - Library/ScriptAssemblies/*.dll (Assembly-CSharp*.dll 제외) → -r:"..."
#    - <Compile Include> → 소스
#    - Unity NetStandard ref: Editor/Data/NetStandard/ref/2.1.0/netstandard.dll
# 2) 실행
dotnet "C:/Program Files/dotnet/sdk/<버전>/Roslyn/bincore/csc.dll" -noconfig "@build.rsp"
```
기준선: **에러 0건 / 경고 74건** (전부 `[SerializeField]` 필드에 대한 CS0649와 CS0169 1건).
경고 수가 늘면 새 파일에서 난 것인지 확인할 것.

---

## 10. 참고한 업계 패턴

| 패턴 | 이 프로젝트에서 |
|---|---|
| **Brain/Body 분리** (Sense→Think→Act) | `NpcActor`(몸) / `NpcBrain`(머리) / `Perception`(감각) |
| **Smart Object / Affordance** (The Sims) | Phase 5. 월드 오브젝트가 자기 용도를 광고 |
| **2-Tier Squad AI** (Halo, Killzone, L4D) | Phase 7. `PartyBrain`(목표) / `DelverBrain`(실행) |
| **Utility AI** (Dave Mark, Guild Wars 2) | Phase 7. 목표 후보가 6~7개라 BT보다 적합 |
| **데이터 주도 아키타입** | `NpcDefinition` SO. 상속이 아니라 조합 |
| **AI LOD / 추상 시뮬** (Mount&Blade, RimWorld, CK) | Phase 8 |
| **FSM** | `EnemyStateMachine`, `DelverBrain` (Type-키 딕셔너리) |

의사결정 아키타입 비교(FSM / BT / Utility / GOAP / HTN)는 대화 초반 참고.
요약: 상위 목표는 **Utility**로 시작(후보가 적고 커브 튜닝이 직관적), 하위 실행은 기존 **FSM** 재사용.
목표가 20개를 넘으면 그때 HTN을 고민한다.
