# 미션 실습 성장과 코인 · 2026-11-05

> 아래는 11월 5일 구현 기록이다. 현재 정책은 [전체 미션 문서의 11월 7일 미션 코인과 식물 메모](ALL_MISSIONS_AND_COURSES_2026-10-29.md#2026년-11월-7일-미션-코인과-식물-메모)를 따른다. 기본·추가 완료는 성장과 50코인, 감정 기록 완료는 성장 없이 메모와 100코인이며, 빈 화분에서는 먼저 새 식물을 시작해야 한다.

## 확정한 사용자 동작

이번 변경은 영양제를 써서 식물을 키우던 흐름을 대체한다. 기존 보유 재화는 같은 수량의
코인으로 표시하며, 미션 실습을 마치면 재화를 주거나 차감하지 않고 식물을 정확히 한 절차
성장 단계 전진시킨다. 새 코인 획득 방식과 다른 소비처를 추가로 만들지는 않는다.

사용자가 선택한 순서는 **실습 종료 → 성장 연출 → 감정·메모 → 기존 최종 완료 안내**다.
실습 종료의 성장과 마지막 감정·메모 저장은 서로 다른 저장 시점이다. 감정 화면을 나가거나
앱을 종료해도 이미 반영한 성장은 유지되며, 다시 들어와도 같은 실습이 두 번 성장시키지 않는다.

이미 완성됐거나 사진첩에 보내 빈 화분이라면 사용자의 추가 결정에 따라 성장 횟수를 보관한다.
`새 식물 키우기`는 보관분을 새 식물의 최대 단계까지 자동 적용하고 초과 횟수는 남긴다.
이 보관·적용 역시 기존 저장 트랜잭션에 포함하며 코인은 지급하거나 사용하지 않는다.

성장 연출은 일반 UI와 입력을 숨긴 정원 전체 화면에서 약 4.6초 동안 진행한다. 기존 정원
Render Texture와 카메라를 사용하고, 느린 수평 회전·회오리·흰빛 식물·다음 단계 교체·파티클
폭발을 순서대로 보여 준 뒤 감정·메모 화면으로 이어진다. 앱이 배경으로 가거나 연출 객체가
비활성화되면 연출을 정리하고 저장된 성장 결과로 돌아간다. 미션 성장 저장 실패 시에는
연출을 보여 주지 않고 재시도를 안내한다.

## 데이터·호환 계약

- 기존 XML `Nutrient`와 PlayerPrefs `capstone.garden.nutrient`는 이관 키로 유지한다.
  `Coins`/`CoinBalance`는 같은 지갑을 가리키며 새 지갑이나 별도 저장소를 만들지 않는다.
- `CommitPracticeGrowth(sessionId)`는 실습 완료 조건을 확인하고 `PracticeGrowthRecords`에
  출처·고정된 날짜·개별 식물 ID·전후 단계·결과를 같은 원자 저장으로 남긴다.
- 동일 세션, 기존 지급 범위의 동일 미션 회차·날짜, 과거 보상 영수증은 재성장을 막는다.
  복습과 기존 지급 제외 규칙은 유지하며, 과거 완료 기록에 성장을 소급하지 않는다.
- 식물 ID·씨앗·외형 레시피·기존 성장값·과거 구매/보상/기록은 이관 시 보존한다.
  기존 레시피의 `MaxGrowthSteps`를 읽고 `Advance`를 한 번 호출한다. 별도 작업자가 맡은
  1주·21단계/기간별 조기 완성 정책을 이 연출에서 하드코딩하지 않는다.
- 실습 성장 이후 최종 완료는 코인이나 성장을 다시 지급하지 않는다. 이미 성공한 과거
  구매 명령의 재시도는 인정하되 신규 유료 성장 명령은 거절한다.
- 기본 미션의 기존 하루 1회·주 6회 제한은 실습 성장 성공 시점에도 적용한다. 감정 입력 전
  참여 종료/다른 코스 선택으로 제한을 우회할 수 없다. 자기 회고 완료는 허용하며,
  실습 때 확정된 날짜를 사용한다. 명시적 미션 기록 초기화는 기존 일정 제한을 초기화하지만
  남겨 둔 성장 원장으로 동일 성장의 재지급을 막는다.
- 완료된 식물의 사진첩 저장·빈 화분·새 식물 순환은 유지한다.

## 주요 변경 파일

| 파일 | 책임 |
| --- | --- |
| `Assets/Scripts/Runtime/LocalGardenState.cs` | 실습 성장·보관·새 개체 적용, 코인 호환, 원자 저장과 중복/일정 제한 |
| `Assets/Scripts/Runtime/GardenPlantAppearance.cs` | 기존 외형 보존 이관과 레시피 기반 한 단계 성장 |
| `Assets/Scripts/Runtime/WeekOneQuestDemo.MissionGrowth.cs` | 실습 종료 저장·실패 재시도·연출 후 감정 화면 연결 |
| `Assets/Scripts/Runtime/WeekOneQuestDemo.FocusPractice.cs`, `.Mbct.cs`, `.FocusHome.cs` | 각 실습 경로와 완료/보관 안내 |
| `Assets/Scripts/Runtime/GardenMissionGrowthSequence.cs` | 기존 카메라·Render Texture의 연출, UI/입력 숨김과 복원 |
| `Assets/Scripts/Runtime/GardenGrowthEffect.cs`, `GardenPlantEvolutionEffect.cs` | 교체 가능한 이펙트 계약, 회오리·흰빛·파티클 및 자원 정리 |
| `Assets/Resources/GardenAtmosphere/PlantGrowth*.shader` | 흰 식물과 부드러운 파티클 표현 |
| `Assets/Scripts/Runtime/SensorRawDisplay.cs`, `Assets/Scripts/Prototype/PrototypeUiIcon.cs` | 개발용 코인 100개 버튼과 코인 아이콘 |
| `tests/MissionPracticeGrowthTests.cs`, `Assets/Editor/GardenMissionGrowthPlayValidation.cs` | 저장 실패·중복·성장 보관 및 실제 Play 흐름 검증 |

## 책임 분리와 검증 기준

`GardenStateService`/`GardenPlantVisuals`가 규칙·원자 저장·중복 방지를 소유한다.
`WeekOneQuestDemo.MissionGrowth`는 실패 재시도와 감정 화면의 연결을 담당한다.
`GardenMissionGrowthSequence`는 저장 후의 화면·카메라 수명만 관리한다.
`GardenGrowthEffect`는 표현 계약이고 현재 `GardenPlantEvolutionEffect`가 절차적 이펙트를
구현한다. 추후 3D 애니메이션 프리팹으로 바꿔도 저장/성장/미션 규칙은 바뀌지 않는다.

검증은 메모리·임시 파일과 별도의 ignored `artifacts/mission-growth-20261105/`를 사용한다.
실제 사용자 저장과 기기 데이터는 열거나 덮어쓰지 않는다. 단위 검사는 코인 보존, 한 단계,
반복/복습, 쓰기 실패/재시도, 감정 단계 재진입, 보관 성장과 새 식물 적용을 포함한다.
Unity 검사는 실제 버튼·카메라 회전·UI 숨김·회오리/흰빛/폭발 화면, 배경 전환 후 복구와
재실행 무중복을 확인한다. 기존 코스·미션·사진첩·출시 스크립트 검사는 새 정책에 맞는
검증 항목을 유지해 실행하며, 완료된 과거 작업의 결과를 이번 성공으로 인용하지 않는다.

## 이번 변경의 검증 결과

모두 합성 상태와 별도 시험 저장소로 실행했다. 실제 사용자 저장·연결 기기 데이터는 사용하지 않았다.
최종 순수 검사 로그는 `artifacts/mission-growth-20261105/unit-02/`에 있다.

| 실행 검사 | 결과 |
| --- | --- |
| 미션 실습 성장 (release / development) | 255 / 262 assertions PASS |
| 기존 Local state (release / development) | 325 / 347 assertions PASS |
| Focus 미션 / 모든 미션 / MBCT 2·4·8주 | 1,647 / 2,819 / 1,629 assertions PASS |
| 활동 알림 | 45 assertions PASS |
| 기존 외형·이관 / 사진 저장 | 568 / 192 assertions PASS |

순수 검사는 실제 임시 파일 쓰기 실패·재시도·재열기, 동시 중복 요청, 과거 잔액/보상/외형
보존, 기존 완료의 비소급, 21단계 레시피, 실습 후 날짜 변경/코스 변경/참여 종료, 자기 회고
완료, 보관 성장 적용/초과분/실패 시 보존을 포함한다.

Unity 6000.3.24f1 최종 Editor 결과는 `artifacts/plant-album-20261105/editor-12/`에 있다.
사진첩/성장 보관/실패 재시도/그래픽 자원 수명/기존 회귀를 포함한 18개 그룹이 모두 통과했다.
그 안에서 LocalLoop 41개, 모바일 UI 11개, Focus 14개 그룹이 각각 통과했고,
Android release용 22개 스크립트 어셈블리가 컴파일됐다. 개발용 코인 추가를 포함한
10개 디버그 메서드가 release 빌드에서 제외되는 것도 확인했다.

최종 실제 Play 검사는 각각 현재 `MockupMain`을 3회 진입/종료하며 별도의 MOCK 파일 저장소를
재열었다. 두 실행 모두 런타임 오류/예외 0건이며 환경 변수·Gizmos 설정을 복원했다.

| 실행 | 결과와 확인한 흐름 |
| --- | --- |
| `artifacts/mission-growth-20261105/play-03/` | 70 assertions, 실제 버튼 6회/레이캐스트 6회 PASS. 실습 저장 실패→재시도→1단계 원자 저장→UI 숨김/느린 카메라 회전/회오리/흰빛/단계 교체/폭발→감정→최종 완료, 연출 중 중단 복구, 재진입 무중복 |
| `artifacts/plant-album-20261105/play-07/` | 67 assertions, 실제 버튼 21회/레이캐스트 21회 PASS. 완성→사진첩 보관→빈 화분→재진입→새 개체 시작/성장→재진입 후 이전 사진과 현재 식물 각각 보존, 상세 회전/확대 |

실제 720×1600 Game view 캡처에서 흰빛의 교체 전 식물, 다음 단계와 폭발 입자,
일반 UI 없는 정원 연출, 감정 화면 복귀를 직접 확인했다. Editor의 360×640·390×844
화면에서는 사진첩·빈 상태·성장 보관/초과분·잔디와 공유 시간의 하늘·페이지 탐색·자원 해제를 확인했다.

처음 Play 실행에서 발견한 `FocusMissionEntrance`의 Unity 객체 초기화 오류는
`CanvasGroup` 필수 구성 및 Unity null 검사로 수정했다. `editor-11`의 LocalLoop 한 항목
실패는 preview fixture가 실제 `Awake`의 초기 홈 방문을 재현하지 않은 문제였으며,
초기조건을 맞추고 기존 뒤로가기 assertions를 유지해 `editor-12`에서 통과했다.
실패한 실행의 원본 로그는 덮어쓰지 않았다.

### 실행 범위와 제한

- Android 기기 설치·실측 FPS/메모리·물리적인 백그라운드 전환은 실행하지 않았다.
  Play의 중단 검사는 실제 연출 중 `OnApplicationPause(true)` 콜백으로 재현한다.
- 기존 Python QA runner 자체 검사 10개는 **2 PASS, 7 ERROR, 1 FAIL**이다.
  7 ERROR는 기존 `qa.py`의 `os.fchmod`가 Windows Python에 없어 발생했고,
  SIGTERM 검사는 합성 자식 프로세스 시작 전 실패했다. PS1 및 Unity 검사 통과와 구분한다.
- `DemoPolishValidation`과 Editor를 종료하는 `LocalLoopPlaySmoke` CLI는 이번에 실행하지 않았다.
  겹치는 사용자 흐름은 격리된 통합 검사와 Play 하네스로 확인한다.
- 기본 새 식물은 현재 레시피의 12단계를 유지한다. 별도 담당자가 변경할 1주·21단계 및
  기간별 낮은 단계 완성 조건은 이 작업에서 확정하거나 변경하지 않았다.
- 사용자에게 남은 질문은 없다. 별도 담당자는 `MaxGrowthSteps`와
  `SetPlantCompleted(instanceId, true)` 연결 계약을 이어서 사용한다.

작업 브랜치는 `codex/integrate-garden-missions-20261029`이며 커밋·푸시는 하지 않았다.
