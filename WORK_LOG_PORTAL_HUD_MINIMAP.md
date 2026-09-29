# 포탈·HUD·미니맵 작업 기록

> 최종 갱신: 2026-09-29 / 브랜치 `JunYeoung_Branch`
> 이 문서는 인수인계용입니다. 작업이 이어지면 같이 고치고, 낡으면 지웁니다.
> 맵빌더·NPC 이동 작업은 `WORK_PLAN_MAP_NPC.md`에 따로 있습니다.

---

## 0. 지금 상태 한눈에

| 항목 | 상태 | 커밋 |
|---|---|---|
| 포탈 목적지를 씬 이름으로 | ✅ | `82a8b2d97` |
| 필드·마을·오버월드 포탈 연결 | ✅ | `8a9e3ccfe` |
| 인게임 HUD 새 디자인 + MP 바 | ✅ | `f0e28fc76` |
| 씬에 남은 UI 샘플 HUD·옛 미니맵 제거 | ✅ | `5bbd57bb5`, `6c5b8e5a3` |
| 던전 미니맵: 시야 기반 + 아이콘 | ✅ | `3845542fd`, `40329cefa` |
| EnemyType_1 시야각 120도 | ✅ | `b32a77c03` |
| 점프 중 상호작용 시 공중 정지 수정 | ✅ | `4d2c7b678` |
| **던전 밖 미니맵** | 🟨 코드 완료, **Unity에서 아직 검증 전** | `4ffa18ba6` |
| 던전 밖 미니맵 가공 느낌 조정 | ⬜ 결과 보고 결정 (§5) | |
| 던전 밖 NPC 아이콘 | ⬜ 나중에 | |
| 마을 자홍색(머티리얼 깨진) 집 | ⏸ 그대로 두기로 함 | |

---

## 1. 포탈과 씬 이동

### 목적지를 씬 이름으로 (`82a8b2d97`)
- **문제:** 포탈이 `LocationType`으로 목적지를 골랐는데 Overworld와 Field가 둘 다 Overworld 종류라 Field로 갈 방법이 없었다.
- **결정:** 번호(index)보다 씬 이름이 더 명시적이라 포탈이 **도착 씬 이름**을 직접 가진다.
- 인스펙터에서는 `[SceneName]` 속성으로 Build Settings에 등록된 씬만 드롭다운으로 고른다 (`SceneNameDrawer`). 오타를 막기 위해서다.
- `SceneTransition.Go`는 Build Settings에 없는 씬이면 로드 전에 실패를 돌려준다. 전에는 화면이 까맣게 멈췄는데, 이제는 플레이어가 포탈 밖으로 도로 걸어 나온다.
- Field 씬을 Build Settings와 `LocationTable`(Overworld 1번)에 등록했다.

### 연결 구조 (`8a9e3ccfe`)
```
Town ↔ Field ↔ Overworld(던전 입구) ↔ Dungeon
```
- 포탈 하나 = 도착 씬 이름 + 그 씬에서 나올 포탈 id.
- Field·Town 씬에 흩어져 있던 오브젝트(포탈, 나무 등)를 `FieldMap`·`TownMap` 프리팹으로 옮기고 맵 루트에 `SceneLocation`을 붙였다.
- **Town·Field 씬에서 바로 Play하면 플레이어가 안 나오던 문제:** 두 씬에 `PlayerManager`가 없었다. 다른 씬과 같은 설정으로 추가했다.

---

## 2. 인게임 HUD

### 새 디자인 (`f0e28fc76`)
- `InGameHud` 프리팹을 새 UI로 교체: 상태 게이지(HP·MP), 핫바, 미니맵, 골드, 날짜.
- **MP 바:** `PlayerStat`에 `maxMana`/`currentMana` 추가. 시작·부활 때 가득 채운다. 아직 MP를 쓰는 곳이 없어 항상 가득 차 있다.
- **지운 것:** 예전 체력바, 층·지역 표시, 횃불 개수 UI, 예전 퀵슬롯 바. 횃불 재고(`TorchStock`)는 설치에 쓰이므로 코드는 남겼다.
- **퀵슬롯 빈 칸:** 기본 이미지 대신 아이콘을 끈다. 끌어다 놓기는 칸 배경 Image가 받는다.

### 정리
- `5bbd57bb5`: RUI 에셋의 예시 HUD(`GUIContainer`)가 Dungeon·Overworld 씬에 남아 실제 HUD와 겹쳤다. 던전에서 "0% Explored" 문구가 두 개 보이던 원인.
- `6c5b8e5a3`: Dungeon 씬에 꺼진 채 남아 있던 옛 미니맵 Canvas 제거. **MinimapManager는 HUD 프리팹에 하나만 있다.**

---

## 3. 던전 미니맵 (`3845542fd`, `40329cefa`)

### 밝히는 방식
밟은 칸을 밝히던 방식 → **플레이어·횃불 주변을 원 범위로 밝히는 방식**.

| 규칙 | 내용 |
|---|---|
| 시야 | 픽셀 단위 시야선(LOS). 벽과 밀 수 있는 오브젝트가 가린다 (`MinimapVisionMap`) |
| 반경 | 플레이어·횃불 각각 인스펙터에서 설정 (지금 프리팹 값 3칸) |
| 빛 감쇠 | 광원에서 멀수록 어두워진다 (`lightFalloff` 커브) — 실제 횃불처럼 |
| 벽 | 바닥과 맞닿은 벽 표면만 `wallFaceThickness` 두께로 그린다 |
| 기억 | 밝혔던 바닥은 회색, 봤던 벽면은 어두운 벽 색으로 남는다 |
| 횃불 꺼짐 | 그 범위는 회색(밝혀졌던 곳)이 된다 |
| 개척도 | **밝혀진 바닥 면적 비율.** 밀 수 있는 오브젝트가 보이면 그 칸 바닥도 밝힌 것으로 친다 |
| 저장 | 탐색 기록을 픽셀 단위(`MinimapExplorationRecord`)로 저장 |

### 아이콘
- 이미지: `Assets/Image/Minimap/image/` (32·64px). **Import Settings에서 Read/Write를 켜야 한다.**
- 아이콘마다 이미지·크기(px)·색을 인스펙터에서 따로 정한다. 이미지가 없으면 예전 점으로 대신 그린다.
- 아이콘 자리는 바닥으로 그리고 그 위에 아이콘만 얹는다 (타일과 아이콘은 한 세트가 아님).
- 아이콘은 벽에 가려지지 않는다.

| 대상 | 표시 조건 |
|---|---|
| 적(해골) · 상자 · 밀 수 있는 오브젝트 | 지금 비춰질 때만. 받는 빛만큼 진하게, 서서히 나타나고 사라짐 |
| 함정(경고 표지판) | 칸이 비춰질 때만. **메운 구덩이는 숨김** |
| 횃불 · 묘비 | 항상. 횃불은 벽 표면에서 바닥 쪽으로 조금 들여 그림 |
| Bonus 오브젝트 | **표시 안 함** — 맵 장식일 뿐이라 뺐다 |

### 같이 한 것
- `b32a77c03`: EnemyType_1 시야각 60° → 120° (사용자가 직접 바꾼 값을 따로 커밋).
- `4d2c7b678`: 점프 중 상자 열기·횃불 설치·밀기를 하면 공중에서 멈추던 문제. 공격 때와 같은 방식으로 상태가 이어지는 동안 중력·점프 관성을 유지하고, 끝났을 때 공중이면 점프 상태로 돌아간다 (`PlayerBaseState.TryResumeAirborne`로 모음).

---

## 4. 던전 밖 미니맵 (`4ffa18ba6`)

### 결정한 방식: B — 맵에 들어갈 때 한 번 위에서 찍기
| 요구 | 결정 |
|---|---|
| 안개 | 없음 |
| 표시 범위 | 플레이어 중심 창, 기본 12×12칸 (`outdoorViewTiles`, 지금 프리팹 값 14). 맵 끝에서는 창이 멈추고 플레이어 표시가 가운데에서 벗어남 |
| 움직이는 것 | 찍은 그림에는 **안 나오고**, 그 위치에 아이콘/점을 얹음 — 포탈·던전 입구·적(아이콘), 플레이어(점) |
| 느낌 | 단순화한 색 + 높이 외곽선. 세부 값은 결과 보고 조정 |
| NPC 아이콘 | 나중에 |

### 흐름
1. `MinimapManager.Update` → `SyncOutdoor`가 `SceneLocation.Primary`(지금 켜진 던전 밖 맵)를 감시.
2. 바뀌면 `OutdoorMinimap.Capture`:
   - 찍을 렌더러 모으기. **플레이어·NPC·적·포탈·던전 입구는 레이어가 아니라 오브젝트 소속으로 뺀다.** (플레이어 몸체는 Torch 레이어, NPC 몸체는 Default 레이어라 레이어로는 못 거른다.) 던전 입구 모델은 `DungeonEntrance`가 붙은 오브젝트의 **부모**에 있어서, 부모가 렌더러를 가지면 부모째 뺀다.
   - 직교 카메라로 위에서 두 번 찍음: **색**(조명 없는 원래 색) + **높이**(외곽선 계산용, 16비트를 R·G에 나눠 담음).
   - 찍는 동안만 머티리얼을 캡처 셰이더로 바꿔 끼우고 `finally`에서 되돌린다.
3. 색 단순화(`Stylize`) + 높이 차 외곽선(`DrawHeightOutline`) → 배경 완성.
4. 매 프레임: 플레이어 위치로 창 위치 계산(`WindowStart`) → 배경 잘라 붙이기(`CopyWindow`) → 마커 그리기. 적 목록은 1초마다 다시 찾는다.

### 파일
| 파일 | 역할 |
|---|---|
| `Scripts/Core/OutdoorMinimap.cs` | 찍기·마커 수집·창 갱신 |
| `Scripts/Core/MinimapImageProcessing.cs` | 순수 계산 (색 단순화, 높이 복원, 외곽선, 창 위치, 잘라 붙이기) |
| `Scripts/Core/MinimapCanvas.cs` | 미니맵 텍스처에 아이콘·점·사각형 그리기. 던전과 던전 밖이 같이 씀 |
| `Shader/Minimap/MinimapCaptureColor.shader` | 조명 없이 `_BaseMap × _BaseColor`. `_WorldTiling > 0`이면 월드 XZ 좌표로 텍스처를 붙임 (트라이플래너 재질용) |
| `Shader/Minimap/MinimapCaptureHeight.shader` | 월드 높이를 인코딩해서 출력 |
| `Settings/MinimapCapture_Renderer.asset` | 찍기 전용 URP 렌더러 (Mobile_Renderer 복사본, 후처리 없음) |
| `SceneLocation.Primary` | 지금 켜진 던전 밖 맵 |
| `UIInGameHUD.HasMinimapFor` | 던전 밖 미니맵이 켜져 있으면 지역과 상관없이 표시 |

### 설정할 때 주의
- **`Outdoor Capture Renderer`에는 `Assets/Settings/MinimapCapture_Renderer`를 넣는다.** RadiantGI의 `CaptureCameraRenderer`와 이름이 비슷해 잘못 고르기 쉽다. 잘못 넣으면 "Renderer List에 없다"는 에러가 나고 미니맵이 안 나온다.
- 이 렌더러는 **지금 쓰는 URP 설정 파일의 Renderer List에 들어 있어야 한다.** `UniversalHigh_RPAsset`, `Mobile_RPAsset` 두 곳에 두 번째 항목으로 넣어 두었다. URP 설정 파일을 새로 만들면 거기에도 넣어야 한다.
- 게임 렌더러(`UniversalHigh_Renderer`)에는 HBAO·SSR·GI·안개 같은 기능이 붙어 있어 캡처에 섞이므로 따로 둔 것이다.
- 인스펙터 값을 바꾸면(`OnValidate`) 다음 프레임에 다시 찍는다. Play 중에 느낌을 조정할 수 있다.

### 캡처 셰이더가 재질 색을 읽는 규칙
찍을 때 원래 머티리얼에서 색과 텍스처를 뽑아 캡처 머티리얼에 넣는다.
- 텍스처: `_BaseMap` → 없으면 `_MainTex` (셰이더에 실제로 있는 속성만)
- 색: `_BaseColor` (없으면 `_Color`) × `_Tint`
- 트라이플래너(셰이더에 `_TriplanarBlend`가 있거나 이름에 "Triplanar"): `_Tiling` 값으로 월드 좌표 매핑

오버월드 바닥(`TEST_OVERWORLD_BLOCK_MATERIAL`, 셰이더 그래프 `Testing/TEST_TRIPLANAR_LIT`)은 색이 `_Tint`(초록)에서 나와서, 이 규칙이 없으면 하얗게 찍혔다. **새 셰이더 그래프 재질이 이상한 색으로 찍히면 여기에 속성 이름을 추가하면 된다** (`OutdoorMinimap.CreateCaptureMaterial`).

---

## 5. 남은 일

1. **Unity에서 던전 밖 미니맵 첫 검증** — 아직 한 번도 실제로 찍혀 본 적이 없다.
   - Overworld / Field / Town 각각 Play
   - 바닥 색이 제대로 나오는지 (하얗지 않은지)
   - 플레이어·NPC·던전 입구 모델이 그림에 안 찍히고, 그 자리에 아이콘/점만 있는지
   - 맵 끝에서 창이 멈추고 플레이어 점이 가장자리로 가는지
   - 낮/밤에 관계없이 같은 그림인지
   - 셰이더 컴파일 에러 없는지
2. **가공 느낌 조정** — `Outdoor Style`의 색 단계·채도·밝기·외곽선 두께·외곽선 높이 기준. 결과 보고 정한다.
3. **NPC 아이콘** — 던전 밖 미니맵에 추가 (보류 중).

### 커밋하지 않고 둔 변경
아래는 작업과 상관없이 에디터가 바꾼 것이라 커밋에서 뺐다.
- `TextMesh Pro/Fonts/NotoSansKR-VariableFont_wght SDF.asset` — 실행 중 글리프가 추가된 것
- `AddressableAssetsData/AddressableAssetSettings.asset`, `ProjectSettings/Packages/com.unity.probuilder/Settings.json` — 줄바꿈만 바뀜

---

## 6. 로컬 테스트 방법 (Unity 밖)
순수 계산 부분은 Unity 없이 컴파일·실행해서 확인했다.
- 컴파일: Unity에 들어 있는 Roslyn(`DotNetSdkRoslyn\csc.dll`)으로 `Assets/Scripts`(Editor 제외) 전체 + `PlayerInputActions.cs`를 UnityEngine·URP DLL에 대고 컴파일
- 실행: Unity의 Mono(`MonoBleedingEdge\bin\mono.exe`)로 테스트 실행 파일 실행
- 테스트: 시야 계산(`MinimapVisionMap`), 아이콘 축소, 던전 밖 이미지 계산(`MinimapImageProcessing`, 16개 항목) — 모두 통과
- 테스트 파일은 저장소에 넣지 않았다 (세션 임시 폴더).
