# 절차적 식물 생성 모듈 v2 · Refined 메시

기준: `codex/overnight-local-loop`의 `01d72572c2f5377404768681701ffd14ed221236`.
구현 브랜치: `codex/procedural-plant-generation`.

공통 씨앗에서 시작하여 활동 이력으로 12종 중 하나를 선택하고, 같은 종에서도
줄기·잎·꽃·열매를 다르게 만드는 독립 모듈이다. 기존 `LocalGardenState`,
`GardenHomePresenter`, 씬, 미션 카탈로그는 수정하지 않았다. 이 브랜치를 열어도
기존 정원 화면이 자동으로 새 생성기로 교체되지는 않는다.

## 바로 확인하기

1. Unity Hub에서 프로젝트를 **6000.3.24f1**로 연다. 원래 프로젝트의 URP를 사용한다.
2. `Tools > Mindful Garden > Procedural Plant Preview`를 연다.
3. `종별 활동 예시`에서 식물을 선택하고 `적용 + 새 씨앗`을 누른다.
4. `성장 기준`과 `프로그램 목표`를 정하고 `목표 적용 · 새 씨앗`을 누른다.
5. `성장 1회 미리보기` 또는 `성숙까지`를 누른다. 생성 이력 슬라이더로 이전 단계를 확인한다.
6. 마우스로 모델을 드래그하여 회전한다. 실제 저장에는 영향을 주지 않는다.

새 씨앗은 Chamomile Refined 샘플과 같은 **두께 있는 다각형 잎·꽃잎**을 사용한다.
미리보기의 `형태: Refined` 표시로 확인한다. 성장 규칙 버전은 2를 유지하고,
별도 `MeshVersion=2`를 저장한다. 메시 버전 필드가 없는 과거 저장본은 1로 읽어
기존 모양을 유지한다.

## Refined 메시 구조

`Assets/Editor/Prototype/PrototypeGardenArt.cs`의 `Batch.Blade`와
`Art/Prototype/RefinedMeshes/Chamomile_Refined_*`가 아트 기준이다.
샘플 역시 최종 렌더링에는 삼각형을 사용한다. 차이는 삼각형을 없애는 것이 아니라,
잎·꽃잎의 외곽과 앞뒷면을 구성해 입체적인 다면체로 만드는 것이다.

- 잎과 꽃잎: 8점 외곽, 볼록한 앞면과 닫힌 뒷면, 기관당 16개 삼각형.
  꽃잎은 끝쪽이 넓은 외곽과 살짝 내려간 가장자리, 잎은 솟은 중심을 사용한다.
  길이·너비·가장자리 변이는 기존 Recipe 값을 반영하며, 두께도 크기에 비례한다.
- 캐모마일: 10각 단면의 노란 꽃 중심과 7개의 작은 꽃가루 돌출부를 추가한다.
- 종꽃·나팔꽃: 바깥벽·안쪽벽·입구 테두리로 두께를 표현한다. 아직 덜 열린
  꽃에서도 안쪽 벽이 뒤집히지 않도록 열림 정도에 비례해 두께를 줄인다.
- 면마다 독립 법선과 밝기 차이를 주며, 줄기·구체의 면 방향도 바깥쪽으로 정렬한다.
  한 식물당 Mesh 하나·Material 하나, 성장 사건 때만 생성하는 구조는 유지한다.
- 새 꽃 배치의 경계 검사에도 같은 Refined 템플릿을 사용한다. 종별 크기 범위,
  8,000개 삼각형 상한, 활동 분화 공식과 성장 목표·줄기 종료 규칙을 유지한다.

`GeneratorVersion`은 성장 구조, `MeshVersion`은 렌더링 형태를 구분한다.
v1/v2 기존 XML은 메시 버전 1로 복원하며, 새 메시를 적용하려고 저장된 개체의
시드·형질·꽃자리를 다시 정하지 않는다. 알 수 없는 메시 버전은 오류로 처리한다.
원본 RefinedMeshes 에셋을 덮어쓰거나 고정 캐모마일 모델을 복제하는 방식이 아니므로
12종의 개성과 같은 종 안의 변이가 계속 유지된다.

예시 선택은 그 종에 가까운 **활동 카운트**를 채운다. 종류를 강제로 지정하는
우회 로직이 아니다. ID 변경은 `새 씨앗 / ID 반영`을 누를 때 적용된다.
종류 확정 후 활동 수를 바꾸면 다음 성장에서 새로 만들어지는 기관만 영향을 받는다.
이전 이력으로 돌아간 후 성장하면 미리보기의 미래 이력은 새 경로로 대체된다.

## 목표 횟수와 미션의 관계

이전 v1의 12회는 영양제 **성장 구매** 횟수였다. 미션 보상 5개, 성장 구매 비용
10개이므로 새 보상만 사용하면 완료 미션 24회분이다. 미션과 성장 구매는 별도였다.

v2는 개체 생성 시 `MaxGrowthSteps`와 `GrowthMode`를 저장한다.

| 모드 | 증가 조건 | 영양제 |
|---|---|---|
| `MissionCompletions` | 외형 인정 미션 1회당 성장 진행 1회 | 성장 비용 없음, 기존 보상은 별도로 유지 |
| `NutrientPurchases` | `Grow` 구매 성공 1회당 진행 1회 | 구매마다 10개 차감 |

미리보기 프리셋은 12 / 28 / 48 / 56회, 직접 입력은 6~1000회다.
4주 매일 1회라면 28, 8주 주 6회라면 48, 8주 매일 1회라면 56을 선택한다.
**주 수는 카운트 설정 예시이며 실제 달력 기간을 강제하지 않는다.** 외형 인정은
기존대로 하루 최대 3회이며 같은 `(CycleId, MissionId)`를 중복 인정하지 않는다.
복습도 외형 자격이 있으면 미션 연동 성장에 반영되지만 영양제 보상은 별도다.
프로그램의 필수 미션만 성장에 반영하려면 호출 측에서 해당 자격을 구분해야 한다.

목표와 모드는 새 씨앗에 고정된다. 기존 개체를 설정 변경으로 갑자기 성숙시키지 않는다.
종류 확정은 `ceil(MaxGrowthSteps/3)`번째다: 28회 목표는 10회, 48회는 16회,
56회는 19회. 인정 이력 6회 조건을 유지한다. 짧은 목표에서 이 조건을 먼저 만나면
분화 직전에 머물렀다가 여섯 번째 인정 완료에서 누적 진행을 한 트랜잭션으로 따라잡는다.
따라서 6·12회 목표도 정확히 해당 인정 완료 횟수에서 성숙한다.

## 추가 파일

| 위치 | 역할 |
|---|---|
| `Assets/Scripts/ProceduralPlants/PlantCatalog.cs` | 12종, 꽃말, 형태·색상·수량 한계 |
| `PlantMath.cs` | 시드, 독립 해시 난수, 성장 곡선, Linear RGB |
| `PlantRecipe.cs` | 영구 저장할 구조·기관 DTO, XML 직렬화·깊은 복사 |
| `PlantGenerator.cs` | 종 결정, 18개 제어 요소, 기관 추가, 열매 평가, 검증 |
| `PlantGeometry.cs` | Unity에 의존하지 않는 실제 삼각형·법선·색 생성 |
| `PlantGrowthService.cs` | 독립 시연용 상태 서비스, 일일 집계, 보상·성장 원자적 저장 |
| `ProceduralPlantView.cs` | Unity Mesh 적용, 생성한 Mesh/Material 수명 관리 |
| `Assets/Resources/ProceduralPlants/PlantVertexColor.shader` | URP용 양면·면별 음영·정점 색 셰이더 |
| `Assets/Editor/ProceduralPlants/ProceduralPlantPreview.cs` | 기존 씬을 건드리지 않는 미리보기 창 |
| `Assets/Tests/ProceduralPlants/Editor/` | 공통 계약 검사와 Unity 렌더러 검사 |
| `scripts/test-procedural-plants.py` | Unity 없이 실제 생성기·기하 계산 검사 |

새 assembly, linker 보존 설정 및 `.meta`를 포함한다. `UnityEditor` 의존성은
Editor assembly에만 있다. 런타임에서는 잎마다 GameObject를 만들지 않고 한 식물에
Mesh 하나, Material 하나를 사용한다. 유휴 `Update()` 및 전역 랜덤 호출은 없다.

## 확정된 성장 규칙

| 항목 | 동작 |
|---|---|
| 입력 | BR 호흡, BD 몸 감각, MV 움직임, SN 감각, RF 생각·감정, SC 자기돌봄의 인정 횟수 + 성장 횟수 |
| 비율 | `p[k] = (count[k] + 1) / (sum(count) + 6)` |
| 종별 목표 | 주 분류 0.60, 보조 분류 0.25, 나머지 각각 0.0375 |
| 종 결정 | 목표 벡터와의 제곱 거리 최소; 1e-12 이내 동률은 종별 해시, 마지막은 ordinal ID |
| 시드 | 길이 접두어 UTF-8 필드의 SHA-256: `GardenPlant`, `1`, `ownerId`, `instanceId` |
| 난수 | `SHA256(BaseSeedHex, nodeId, tag)`의 첫 4바이트 big-endian / 2^32 |
| 형질값 | `clip(0.5 + 0.6*(p[A]-p[B]) + 0.2*(2*U-1))` |
| 정수 범위 | `min(max, min + floor((max-min+1)*z))` |
| 발달 곡선 | `t=clip((g-a)/(b-a))`, `S=t*t*(3-2*t)`, `g=j/MaxGrowthSteps` |
| j=0 | 동일한 씨앗 |
| 분화 전 | 동일한 두 떡잎; 줄기 높이 0.08~0.16 |
| `j=ceil(MaxGrowthSteps/3)` | 인정 활동 6회 이상 필요; 종·목표 구조 확정, 첫 봉오리 |
| `g≤0.50` | 줄기·가지 길이 성장, 끝에 붙은 봉오리가 함께 이동 |
| `g>0.50` | 줄기 길이는 고정, 잎·꽃의 추가 및 개화 |
| `g≥5/6` | 성숙; 장미·은방울꽃의 일부 꽃자리를 열매로 전환 |
| 보존 | 첫 꽃자리 보존. 열매 성공·실패 모두 저장. 이미 생성한 잎·꽃의 목표값은 고정 |
| 성숙 이후 | 성장 구매 거부. 새 활동으로 완성 식물의 외형을 변경하지 않음 |

제어 요소 18개는 **종, 높이, 줄기 굵기, 굽힘, 축 수, 가지 각도, 잎 수, 잎 길이,
잎 비율, 가장자리, 잎 배열, 꽃 단위 수, 단위당 작은 꽃 수, 꽃잎/갈래 수, 꽃 크기,
꽃 색, 열매 유무·개수, 열매 크기**다. 종별 표와 형질 식은 v1을 유지하며,
v2에서는 시간 정규화와 줄기·꽃 연결 구조를 다음과 같이 수정한다.
`BaseSeed`와 이미 생성한 기관의 목표값을 저장하므로, 계정 연결 후 재시딩하지 않는다.
ID가 종을 미리 정하지 않으며, 동률인 경우를 제외하면 종 선택에 ID 난수가 관여하지 않는다.

| 종 | 주 / 보조 활동 | 꽃말 | 템플릿 |
|---|---|---|---|
| 캐모마일 | BR / SC | 역경 속의 힘 | 가는 방사형 꽃잎, 갈라진 잎 |
| 데이지 | SN / BD | 순수 | 낮은 잎, 방사형 꽃 |
| 해바라기 | MV / SC | 기쁨·희망 | 큰 중심, 심장형 잎 |
| 코스모스 | MV / SN | 조화 | 8장 꽃잎, 갈라진 잎 |
| 흰 캄파눌라 | SC / RF | 감사 | 흰 종꽃 |
| 물망초 | RF / SN | 진실한 사랑 | 푸른 5갈래 군집 |
| 프리지아 | SC / MV | 우정 | 검 모양 잎, 한쪽으로 이어진 나팔꽃 형태 |
| 라벤더 | BR / BD | 헌신 | 가는 잎, 층을 이루는 보랏빛 꽃이삭 |
| 수국 | RF / SC | 진심·감사 | 넓은 잎, 반구형 4갈래 꽃 군집 |
| 분홍 장미 | SC / BD | 감사·존중 | 5장 홑꽃, 복엽, 로즈힙 최대 3개 |
| 스노드롭 | BD / BR | 희망 | 띠 모양 잎, 아래를 향한 3+3 구조 |
| 은방울꽃 | BD / SN | 다시 찾은 행복 | 넓은 밑동 잎, 종꽃, 열매 최대 5개 |

꽃말은 프로젝트가 채택한 상징이며 문화·색에 따라 다른 해석이 있다. 종·열매는
상태 진단이나 등급이 아니다. 개수는 렌더링 단위이며 실제 식물학적 기관 수와는 다르다.

## 구현한 기하 제약과 시안과의 차이

- v2의 주 줄기 끝에는 항상 terminal 봉오리를 둔다. 지지 축의 끝을 따라 이동하고,
  그 줄기 끝 위로 새로운 주 줄기 구간을 만들지 않는다.
- 주 줄기는 `S(g;0.25,0.50)`, 추가 축은 0.48까지 생성하여 0.50에 길이 성장을 마친다.
  꽃이 열린 뒤에는 가지 끝·기존 꽃의 위치가 더 높아지지 않는다. 이후 잎·꽃·열매의
  크기 성숙은 계속된다. 새로운 곁꽃은 지지 축의 최종 끝 높이 아래에 배치한다.
- 봉오리의 최종 앵커를 Recipe에 저장하고, 표시 위치만 해당 축의 현재 끝을 사용한다.
  이는 재추첨이나 미션에 따른 기존 목표 변경이 아니다. 개화 이후에는 표시 위치도 고정된다.
- `GeneratorVersion=1` 저장본은 이전 계산 경로로 복원한다. 새 씨앗은 성장 version 2, 메시 version 2이며,
  기존 개체를 재시딩·자동 재구성하지 않는다. 수정된 외형을 보려면 새 씨앗으로 확인한다.
- 가지 길이는 종별 허용 범위 안에서, **완성 길이**가 반경 0.60·높이 1.30을 넘지 않도록 제한한다.
- 새 꽃은 같은 부착점에서 방위각 0/+15/-15/+30도, 종별 최소 크기, 예약된 배치 후보 순서로 시도한다.
  예약 배치는 최대 12개이며 실제 선택한 위치를 저장한다. 기존 꽃을 움직이지 않는다.
- 잎은 경계와 방향을 검사한다. 자연스러운 잎 겹침은 허용한다. 모든 삼각형의 충돌이나
  기관 간 교차를 없애는 정밀 충돌 시뮬레이터는 아니다. v2의 꽃 배치는 실제 템플릿의
  봉오리·성숙 꽃·최대 열매 정점으로 경계를 검사하고, 외접 중심 간 간격을 검사한다.
- 슬롯을 만들 수 없거나 실제 Mesh가 bounds/8,000 triangles를 넘으면 성장 트랜잭션을 거부한다.
  영양제를 차감하고 기관을 빠뜨리는 방식으로 처리하지 않는다.
- 종별 비율과 기관 수는 절차적 생성 카탈로그를 따른다. Refined 샘플과 같은
  다면체 표현을 사용하지만 고정 샘플의 가지·꽃 배치를 그대로 복제하지 않는다.
- 셰이더는 일정한 부드러운 음영을 사용한다. 정원 환경광/그림자 통합과 0.8초 성장 tween은 후속 연결 사항이다.
- 현재 성장 메시 재생성은 이벤트 단위다. 다수 개체의 공유 Material·Mesh 캐시 최적화는 아직 추가하지 않았다.

## 런타임 사용 예

순수 생성기만 사용하여 기존 저장 트랜잭션에 넣는 방법:

```csharp
using CapstoneDesign.Plants;

var recipe = PlantGenerator.Create("synthetic-owner", "synthetic-instance",
    maxGrowthSteps: 28, growthMode: PlantGrowthMode.MissionCompletions);
// 실제 서비스에서는 인정 완료 영수증에서 집계한 카운트만 제공한다.
recipe.Counts = new[] { 6, 1, 0, 0, 0, 3 };
var candidate = PlantGenerator.Advance(recipe); // 입력 recipe는 변경되지 않음
// 미션 모드: 기존 aggregate 안에 candidate + 외형 크레딧 + request 영수증을 함께 저장한다.
// 영양제 모드에서는 여기에 영양제 차감도 포함한다.
// 저장 성공 후에만:
plantView.Render(candidate);
```

별도 시연에서 완전한 보상·성장·저장 흐름을 확인하는 방법:

```csharp
var store = new PlantXmlStore(System.IO.Path.Combine(
    UnityEngine.Application.persistentDataPath, "procedural-plant-demo.xml"));
var service = new PlantGrowthService(store);
service.StartPlant("synthetic-start-request", "synthetic-owner", "synthetic-instance",
    maxGrowthSteps: 48, growthMode: PlantGrowthMode.MissionCompletions);
service.CompleteActivity("synthetic-completion", "synthetic-instance",
    "2026-01-01", "synthetic-mission", GrowthCategory.Breath,
    rewardEligible: true, manuallyCompleted: true);
// 이 미션의 외형 크레딧이 새로 인정되면 성장도 같은 저장 트랜잭션에서 진행된다.
// 미션 연동 모드에서는 Grow를 호출하지 않는다. 실패 시 LastError를 확인한다.
```

`CompleteActivity`는 미션 선택·타이머·센서 판정을 대신하지 않는다. 호출자는 기존
미션 검증 결과, 대표 카테고리, cycle ID, 보상 자격을 명시적으로 전달해야 한다.
`MappingVersion=1`과 하루 3회 외형 인정 정책을 사용한다. `(CycleId,MissionId)`
외형 중복 제거는 전체 식물에 걸쳐 적용된다. 복습은 `rewardEligible:false`로 전달한다.
같은 RequestId 재호출은 멱등이며, 다른 payload로 재사용하면 거부한다.
미션 연동 개체에 `Grow`를 호출하면 차감 없이 거부한다. API 인자를 생략한 기존
호출은 호환성을 위해 여전히 12회 영양제 구매 모드이며, 미리보기의 새 기본값은
28회 미션 연동 모드다. 필요한 모드와 목표는 호출에서 명시하는 것을 권장한다.

**실제 게임 연결 시 독립 서비스의 두 번째 영양제 지갑을 만들지 않는다.**
`LocalGardenState` aggregate에 Recipe/외형 영수증/요청 영수증을 포함하고 기존
`CompleteSession`·`GrowPlant`의 동일한 저장 트랜잭션에서 계산을 수행해야 한다.
기존 보상과 시연 서비스 보상을 동시에 실행하면 이중 지급이므로 금지한다.
현재 작업은 분리된 기능 구현이며 이 연결·기존 저장본 마이그레이션은 수행하지 않았다.

## 검증

Unity가 없어도 생성기와 **동일한 기하 코드**를 실행한다. Python 3와 .NET SDK 8 이상,
또는 Unity에 포함된 Mono가 필요하다. 외부 NuGet 패키지는 사용하지 않는다.

```bash
python3 scripts/test-procedural-plants.py
# .NET host 경로를 별도로 지정할 때:
DOTNET_BIN=/path/to/dotnet python3 scripts/test-procedural-plants.py
```

Unity: `Window > General > Test Runner > EditMode`에서
`Capstone.ProceduralPlants.Tests`를 실행한다. 렌더러·셰이더 리소스·메시 교체 검사도 포함한다.
실제 URP 렌더링 검사는 12종 모음, 캐모마일 성장 단계, 이전/Refined/참고 에셋
비교 이미지를 `artifacts/plant-mesh/`에 생성한다. 비교 이미지는 왼쪽부터 이전
형태, 새 형태, 원본 Chamomile 샘플이다. 12종 모음은 카탈로그 순서로 4열이다.

검사 내용: 12종 도달 가능성, 초기 메시 동일성, 성장 중 기존 기관 불변,
모든 성장 단계의 bounds·삼각형 예산, 직렬화 복원 후 동일 기하, 열매 실패 기록 보존,
차감·저장 실패 원자성, 요청 재전송·동시 재전송, 일일 집계 상한, 복습 보상 분리,
성숙 이후 동작, 버전·손상 데이터 거부, 극단/혼합 활동 프로필.

이 환경의 검사 결과는 `docs/PROCEDURAL_PLANTS_VALIDATION.md`에 기록한다.
Unity Editor 실행, 셰이더 컴파일, IL2CPP 및 Android 기기 검증은 별도 게이트다.

공식 API 참고:
- [Unity Mesh](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.html)
- [URP shader structure](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/writing-shaders-urp-basic-unlit-structure.html)
