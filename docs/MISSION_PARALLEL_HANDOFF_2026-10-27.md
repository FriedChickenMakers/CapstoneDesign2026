# 개별 미션 UI · 미션 개인화 병행 개발 인수인계

작성일: 2026-10-27. **현재 동작을 보존한 개발 기준점**이다. 담당 A=개별 미션 UI, 담당 B=미션 개인화로 표기하며 실제 담당자 이름은 미정이다. “완전 개인화”의 최종 정책은 아래 미정 항목을 합의해야 한다. 이번 준비에서 두 기능 전체를 구현하지 않았다.

## 1. 시작 기준과 이번 변경

- 코드 저장소: `FriedChickenMakers/CapstoneDesign2026`, 통합 대상: `codex/overnight-local-loop`. `main`이나 `mockup`에서 분기하면 현재 기능이 빠진다.
- 조사 시작 시 현재 브랜치·원격은 `80a42cd43b26cf4c1356a0409baeb4013ef9b8ce`로 일치했고 미커밋 변경은 없었다. 다른 로컬 브랜치의 미푸시 커밋은 이번에 포함하지 않았다.
- 이번 변경: UI 안의 **기존 복습 추천 계산**을 [OptionalActivityRecommendation.cs](../Assets/Scripts/Runtime/OptionalActivityRecommendation.cs)로 추출했다. [UI 연결](../Assets/Scripts/Runtime/WeekOneQuestDemo.Personalization.cs)은 결과 표시·노출·선택을 담당한다. 선택 ID·특징·정책 ID를 결정 시점에 보존하며 특징 배열은 복사본으로 제공한다.
- 후보·점수·동점 규칙과 코스/보상 규칙은 유지한다. 새 저장 스키마·서버 API·건강 입력·미션 난이도 정책을 추가하지 않았다. 새 모듈 검사 23개를 필수 `unit` 검사에 포함했다.
- 직전 식물 고품질화·8천 삼각형 제한 해제는 논의된 후속 작업이다. 현재 코드에는 기존 제한이 남아 있고 이번 인수인계 범위에 섞지 않았다.
- 팀 기획·이번 push 기록·정확한 분기 SHA는 별도 `CapstoneDesign2026-Docs` 저장소의 `기획서/미션 UI와 개인화 병행 개발 인수인계.md`를 따른다.

## 2. 브랜치와 통합 방식

두 작업 브랜치는 **이 문서를 포함한 동일한 준비 커밋**에서 시작한다.

| 담당 | 작업 브랜치 | 최초 작업 |
| --- | --- | --- |
| A | `codex/individual-mission-ui` | 기존 미션 화면·상태 전이 확인 후 활동 유형별 표현을 별도 UI 파일로 확장 |
| B | `codex/full-mission-personalization` | 현재 추천/걷기/알림 개인화의 차이를 확인하고, 확장할 입력·출력·정책의 계약 제안 |

각자 다른 clone 또는 worktree를 사용한다. 같은 Unity `Library`와 저장 디렉터리를 공유하지 않는다. 원격 작업 브랜치가 준비된 경우:

```bash
git fetch origin
# 담당 A
git switch --track origin/codex/individual-mission-ui
# 담당 B는 자신의 clone에서
git switch --track origin/codex/full-mission-personalization
```

이미 같은 이름의 로컬 브랜치가 있으면 재설정하지 말고 upstream·차이를 확인한다. 분기 SHA는 Docs의 작업 기록과 대조한다. 작업 PR의 base는 `codex/overnight-local-loop`로 지정한다. 이번에는 기능 PR을 미리 만들거나 `main`에 병합하지 않는다.

**통합 순서:** (1) B가 입력·출력/저장 확장이 필요한지 짧은 계약 변경안을 작성하고 A가 필요한 표시 상태를 확인 → (2) 필요한 공통 모델·마이그레이션만 작은 선행 PR로 통합 → (3) 각자 그 통합 커밋을 일반 merge로 가져와 개발 → (4) B의 순수 정책·저장/대체 경로 통합 → (5) A의 소비 UI와 함께 통합 검증. A는 그동안 현재 계약과 합성 결과로 독립 개발할 수 있다. 공통 계약 변경이 없으면 UI PR도 먼저 통합할 수 있다. 공유 브랜치 rebase/force push나 타인의 파일 덮어쓰기는 하지 않는다.

## 3. 실제 실행 경로와 데이터 소유권

주 화면은 Unity가 생성한 `MockupMain`의 `WeekOneQuestDemo`와 partial 클래스들이다. `Prototype`의 디자인 자산·컴포넌트는 재사용하지만 `PrototypeController`를 새 제품 진입점으로 교체하지 않는다. 씬/프리팹 YAML을 손으로 재생성하지 않는다.

```text
MbctContent / MindfulnessContent (콘텐츠, ID, 안내)
  → WeekOneQuestDemo.ShowHome / ShowCourse / ShowFree
  → SelectMission → GardenStateService.BeginSession
  → ShowSession → ShowMbctSession 또는 기존 걷기/자유 활동
  → Start / SavePracticeStep / Pause / Resume / End / Complete
  → GardenStateService의 한 트랜잭션으로 저장·진도·보상

GardenState snapshot + ResponsePolicy + 결정 시각
  → OptionalActivityRecommender.Select
  → 결정 ID 생성·추천 카드 표시 → RecommendationExposure
  → SelectMission(mission, RecommendationContext)
  → 선택적 PersonalizationClient 이벤트 큐/서버 전송
```

`GardenStateService.Snapshot`은 깊은 복사다. UI/정책에서 수정해도 영구 상태가 되지 않는다. 저장은 서비스 명령을 거친다. `bool` 실패 시 `LastError`를 처리하고 현재 입력·화면을 유지한다. 서비스에 범용 `Changed` 이벤트 버스는 없다. 현재는 명령 성공 후 화면을 재구성하고 Unity Update에서 건강/추천 이벤트를 폴링한다. 새 이벤트 버스가 있다고 가정하지 않는다.

### 공유 데이터 모델

| 모델·위치 | 현재 주요 계약 | 변경 시 조율할 점 |
| --- | --- | --- |
| [MissionDefinition / MindfulnessContent](../Assets/Scripts/Runtime/MindfulnessContent.cs) | `id`, `type`, `recommendedOrder`, `mbctActivity`, `purpose`, `steps`, 권장 시간, 출처·콘텐츠 버전. 상태/보상 소유자가 아님 | 전역 카탈로그 객체를 개인별 값으로 직접 수정하지 않기. 개인별 문구/시간이 필요하면 별도 결과·세션 스냅샷 계약 합의 |
| [MbctStep / MbctContent](../Assets/Scripts/Runtime/MbctContent.cs) | `title`, `guidance`, `answerKey`, `options`; 2/4/8주 12/24/48개, 모든 코스 `AllCourses`, `Find(id)` | 문구/입력 키/단계 순서 변경은 진행 중 세션 복원에 영향. 렌더 유형 추가 필드는 미정 |
| [ActivitySession](../Assets/Scripts/Runtime/LocalGardenState.cs) | `SessionId`, `MissionId`, `InstanceId`, 시각들, `Status`, `CourseOrder`, `InstructionStep`, `Answers`, 선택 Mood/Note, 걷기 목표·조회 구간, `Recommendation` | 상태/목표를 UI 필드에만 두지 않기. 타입 변경과 저장 마이그레이션은 선행 계약 PR |
| `GardenState / MbctPolicy` | 기간별 진도·완료 ID, `PracticePreferences`, `Experiences`, 보상/구매 영수증, 정원·성장 이력 | 기존 8주 ID·필드를 짧은 코스로 재해석하지 않기. 기간 간 일일·주간 제한 보존 |
| [OptionalActivityRecommendation](../Assets/Scripts/Runtime/OptionalActivityRecommendation.cs) | 읽기 전용 `MissionId`, `PolicyId`, `Score`, 복사된 `Features`. 후보 없으면 null | 메모리 결과이며 저장/API 스키마 아님. 설명·대체 사유·시간·강도·상태 필드는 아직 없음 |
| `RecommendationContext` | 결정 ID, 정책 ID, 앱 버전, UTC offset, 4개 특징의 세션별 복사 | 이벤트 귀속을 재시도나 새 추천 결과로 바꾸지 않기. 개인 기록·건강 원본을 넣지 않기 |

저장 ID: `course:MBCT01`–`48`, `course:MBCT2W01`–`12`, `course:MBCT4W01`–`24`, 걷기 `free-mission:M15`. 과거 `course:Pxx`와 다른 자유 미션 카탈로그도 코드에 남아 있다. 모든 카탈로그 항목이 현재 주 UI에 노출되는 것은 아니다. `sourceId`는 출처 연결이고 실제 선택·저장 키는 `id`다. UI에는 `mbctActivity`와 단계 정보를 활용하되 세부 변형 구분이 더 필요하면 공통 메타데이터를 먼저 합의한다.

### 서비스 명령과 UI 연결

| 동작 | 호출 | 현재 보존 기준 |
| --- | --- | --- |
| 선택 | `SelectMission(mission, context)` → `BeginSession(id, missionId, order, context)` | 활성 세션 하나. 새 선택 대신 기존 세션으로 복귀. 새 코스는 선택한 기간·순차 조건 확인 |
| 시작/쉬기/재개/종료 | `StartSession`, `PauseSession`, `ResumeSession`, `EndParticipation`, `CancelSelection` | `Selected → InProgress ↔ Paused → Ended/RewardCommitted`. 선택 취소와 종료·완료의 의미 구분 |
| 안내/선택 입력 | `ShowMbctSession`, `SavePracticeStep`, `SkipPracticeStep` | 입력 선택 사항. 저장 실패 시 화면 유지. skip은 해당 세션 답을 제거하고 단계 이동 |
| 초안/뒤로가기 | `SaveActivePracticeDraft`, `TryNavigateBack`, `LeaveActivityPanel`, pause/focus hooks | 새 UI도 탭 이동·키보드·뒤로가기·일시정지에서 초안 유실을 막아야 함 |
| 완료/보상 | `ShowReflection → Finish → CompleteSession` | 기분·메모 생략 허용. 권장 시간/좋은 감정은 완료 조건 아님. 보상·진도는 원자 저장, 재시도 중복 지급 방지 |
| 걷기 | `StartSelectedWalking → StartWalkingSession`; `ObserveMissionSteps`, `TryCompleteWalkingGoal` | 시작 때 목표·조회 구간 고정. 일반 MBCT의 수동 완료와 별도 경로. 센서값을 마음챙김 인증으로 해석하지 않음 |

`RewardCommitted`는 완료 상태명이다. 복습으로 실제 새 영양제가 지급됐다는 뜻은 아니다. 신규 코스 완료는 임시 5개, 복습에는 추가 보상 없음. 새 코스 진도는 하루 1개·달력 주 6개까지, 놓친 날에 만료/벌점 없음. 일반 사이클은 기본 현지 04:00, 걷기 집계는 현지 자정 경계라 둘을 동일시하지 않는다.

## 4. 개인화 입력·출력과 미구현 경계

| 경로 | 현재 입력 | 현재 출력·UI 연결 | 아직 하지 않는 것 |
| --- | --- | --- | --- |
| 선택적 복습 추천 | 완료한 MBCT ID, `favorite`, `recommendation-minutes`(1/3/5, 기본 3), 해당 ID의 시작/완료 이력, 결정 시각, `ResponsePolicy` | `OptionalActivityRecommender.Select(state, policy, now)` → 최고 점수 1개. 동점은 ID ordinal. `AddOptionalRecommendation`이 표시 | 새 필수 미션 순서 재편·실습 안내/강도 변경·모든 미션 개인화 |
| 걷기 목표 | [WalkingBaselineSnapshot](../Assets/Scripts/Runtime/AndroidPlatformModels.cs): 상태·완결성·최신성, 날짜별 걸음과 수면 | [WalkingPersonalization.Evaluate](../Assets/Scripts/Runtime/WalkingPersonalization.cs) → `WalkingTarget`. [WalkingBaseline UI 연결](../Assets/Scripts/Runtime/WeekOneQuestDemo.WalkingBaseline.cs)에서 시작 시 저장 | 모든 활동의 건강 기반 난이도, 의료적 처방, 누락을 0으로 채우기 |
| 알림 시각 | 사용자 설정·허용 상태, 기존 클릭 관측 등 네이티브 정책 | `ActivityNotificationBridge`, Android `ActivityReminderPolicy`/`ActivityNotifications` | 미션 선택 정책과 같은 모델이 아님. 전체 알림→실습 관측 연결·온라인 bandit은 미완성 |
| 선택적 서버 정책 | 명시 동의·별도 연결 설정·최소 행동 이벤트 | `PersonalizationClient.Policy`; 유효하지 않거나 미동의면 로컬 규칙 | 운영 서버 배포, 실제 사용자에서 개선 효과 입증, 원문 기록·건강 원본 업로드 |

복습 특징 순서는 **preference / completion_rate / days_since / duration_fit**이며 모두 유한한 [0,1]이다. 완료율은 해당 ID의 시작된 세션 중 `RewardCommitted` 비율(시작 이력 없으면 0.5), 경과는 마지막 시작 이후 14일로 정규화해 자름, 시간 적합도는 권장 시간에 대한 가용 시간 비율을 최대 1로 자른다. 시간 적합도가 실제 활동 길이를 변경하지는 않는다. 기본 점수는 `min(.99, .15 + .30*p + .20*c + .15*d + .20*t)`다. 기존 모델은 같은 네 특징의 로지스틱 추론이며 feature 순서·만료·최소 학습량 검증을 거친다. 입력·정책 객체는 null이 아닌 유효한 앱 상태를 전달한다. 결정 후 새 이력으로 노출 특징을 다시 계산하지 않는다.

걷기는 최신·완결 조건을 통과한 과거 14일 내 최소 5개 유효 일자의 중앙값을 500걸음 단위로 조정하고 5,000–10,000 범위로 제한한다. 쓸 수 있는 짧은 수면 기록이면 0.8배 조정하며 기본 목표는 5,000이다. 이 숫자는 **현재 구현 설명**이며 새 요구사항이나 의료적 근거를 승인한 것이 아니다. 시작된 세션에 새 목표를 덮어쓰지 않는다. 결측/부분 조회/권한/지연과 실제 0을 구분한다.

### 이벤트와 서버 API

- `RecommendationExposure`는 카드가 viewport에 절반 이상 보이거나 선택될 때 1회 노출을 보고한다. 카드를 만들었다는 이유만으로 보고하지 않는다.
- UI의 `ObserveOptionalRecommendation`은 약 3초 간격으로 추천 context가 있는 세션을 확인한다. `PersonalizationClient.RecordSession`이 `activity_started`, `activity_completed`, `activity_ended`를 기록하고 세션+종류의 안정적 ID로 중복을 막는다. pause/resume 이벤트는 현재 없다.
- `recommendation_shown`과 위 수명주기 이벤트는 연결됨. `notification_eligible/scheduled/post_accepted/blocked/failed/opened`는 스키마에 있지만 모든 생산자가 연결된 상태는 아니다.
- [서버 계약](../server/README.md): Bearer 인증, `POST /events/batch`, `GET /policy/current`, `DELETE /participant`. JSON schema/consent version 1, 4개 특징, batch 최대 100, 중복 ID 충돌 거절, 철회 뒤 지연 업로드 차단. 필드 추가는 C#·Python 양쪽 검증과 테스트를 동시에 바꾼다.
- 이벤트의 현재 `content_version`은 `courses-v1`; 콘텐츠 항목의 개별 버전과 별개다. 이를 바꾸거나 안내 변형 이력을 추가하려면 버전·평가 호환을 조율한다. 점수 예측을 치료 효과로 설명하지 않는다.

## 5. 저장 형식·권한 경계

| 저장 대상 | 형식·위치 | 협업 규칙 |
| --- | --- | --- |
| 게임·코스·세션 | `SaveRoot/garden-live`, `garden-mock`, `garden-replay` 아래 `state.xml`; **첫 줄 SHA-256 + 다음 줄부터 XML**, `GardenState.SchemaVersion=1` | 원자 교체·백업·단일 writer. 손상 시 새 계정을 만들어 덮어쓰지 않음. 서비스/코덱을 통해 확장 |
| 추천 전송·동의 | 앱 private `behavior-queue.xml`, schema 1; 최대 256 이벤트, 90일 보존 | 게임 저장과 별개. 철회/삭제 대기는 재시작 후 보존. 용량 초과·네트워크 실패가 게임 완료를 막지 않게 유지 |
| 연결 설정 | 앱 private `personalization-enrollment.json`의 endpoint/token | 운영자가 별도 설정하고 사용자가 동의. APK/Git/샘플에 실제 값을 넣지 않음 |
| Android 건강·알림 상태 | 네이티브 브리지/저장, [플랫폼 구조](ANDROID_PLATFORM_ARCHITECTURE.md) | UI가 직접 파일을 읽거나 새 writer를 만들지 않기. 실제 기기 검증은 별도 |

이번 분리로 위 파일 형식은 바뀌지 않는다. 새 모델에는 nullable/선택 필드의 구버전 기본값, 읽기·쓰기·실패·재실행 검사를 함께 설계한다. `Answers`, `PracticePreferences`, 감정/메모는 개인 입력이며 서버에 통째로 보내지 않는다. 합성 저장만 테스트·CI에 사용한다.

## 6. 담당 파일과 충돌 방지

경로의 기본 디렉터리는 `Assets/Scripts/Runtime/`다. 아래 소유권은 병행 작업을 위한 제안이며 공통 파일 담당을 실제 팀원과 확정한다.

| 담당 | 우선 작업 파일/영역 | 경계 |
| --- | --- | --- |
| A: 개별 UI | `WeekOneQuestDemo.Mbct.cs`, `.ActivityLayout.cs`, `.Navigation.cs`, `.Personalization.cs`의 표시·설정, 새 활동별 View/Presenter; `GardenActivityLayout.cs`, `GardenInputField.cs`, `GardenFlowButton.cs` | 추천 계산은 새 모듈 호출. 완료·보상·목표를 UI에서 재정의하지 않음 |
| B: 개인화 | `OptionalActivityRecommendation.cs`, `PersonalizationCore.cs`, `PersonalizationClient.cs`, `WalkingPersonalization.cs`, `WalkingBaselineRefresh.cs`, 필요 시 `server/`와 Android 정책 | 콘텐츠·UI 직접 수정 대신 결과 계약 제안. 현재 기능을 “완전 개인화 완료”로 표시하지 않음 |
| 공통: 변경 전 합의 | `LocalGardenState.cs`, `MissionDefinition`/`MbctContent.cs`, `AndroidPlatformModels.cs`, `AndroidPlatformBridge.cs`, `WeekOneQuestDemo.cs`, `.WalkingBaseline.cs`, `.Notifications.cs` | 통합 담당 1명이 작은 연결 커밋으로 관리. 양쪽에서 상태 모델·필드 이름을 동시에 변경하지 않음 |
| 공통: 화면/빌드 자산 | `GardenUi.cs`, `GardenTypography.cs`, `MockupNavigation`/`GardenHomePresenter`, 씬·프리팹·ProjectSettings·Packages·asmdef/link.xml | 넓은 파급과 Unity 직렬화 충돌. 필요 변경만 명시하고 `.meta` GUID 유지 |
| 테스트 | A: `LocalLoopValidation`, `TabConsistencyCapture` 및 UI 회귀. B: `MissionRecommendationTests`, `PersonalizationTests`, 걷기/서버/네이티브 계약 | `scripts/qa.py`, CI, 공통 테스트 파일은 변경 예고. 신규 테스트는 가능하면 별도 파일 |

## 7. 미정 요구사항 — 구현 전에 확인

1. **개별 UI 범위:** MBCT 활동 유형별 전용 화면인지, 84개 회차별 표현인지, 과거 자유 미션 전체까지 포함하는지. 애니메이션·오디오·신규 자산의 범위/권리와 디자인 기준.
2. **완전 개인화 범위:** 복습 순위만인지, 오늘의 필수 미션·실습 시간·안내 문구·대체 활동·걷기 강도·알림까지인지. 기존 코스 순서/선행 학습과의 관계.
3. **입력·동의:** 명시 선호, 가용 시간, 행동 이력, 건강 집계, 감정 중 실제 사용할 항목과 보존/전송 범위. 자유서술 분석·진단 추정은 확정되지 않음.
4. **출력 계약:** 설명·대체 사유·loading/stale/unsupported/denied/insufficient-data 상태, 개인별 목표/기간의 상하한, 사용자의 거절·수동 변경. 이름·형식은 아직 없음.
5. **진행 중 일관성:** 개인별 안내 순서/목표를 언제 고정하고 어떤 버전으로 복원할지. 기존 `InstructionStep`만으로 동적 안내 변경을 안전하게 복원할 수 있다고 가정하지 않음.
6. **운영·검토:** 콘텐츠·목표의 전문가 검토, 서버 운영 담당, 효과 평가, 모델 배포/되돌리기, 실제 통합 담당. 담당자·일정 미확정.

미정 항목이 해결되기 전 A는 현재 카탈로그와 상태 계약으로 표현을 개발하고 B는 기존 입력 기반 순수 정책·결측 대체 검증을 진행할 수 있다. 새 요구사항을 임의로 확정해 저장 형식을 먼저 바꾸지 않는다.

## 8. 실행·검증·수용 기준

Unity **6000.3.24f1**. 저장소의 `Packages/manifest.json`/lock을 사용한다. 기존 프로젝트를 열 때 씬을 무조건 재생성하지 않는다. Editor를 라이선스가 설정된 OS 계정과 해당 HOME으로 실행한다. 이 공유 worker는 `codex`·`HOME="/home/codex"`이며 root로 Unity를 실행하지 않는다. 라이선스 파일/키를 복사·수정하지 않는다. 자세한 환경변수·도구 설치는 [scripts 안내](../scripts/README.md), [QA 범위](QA_AUTOMATION_AND_COVERAGE.md), [개인정보 검사](REPOSITORY_PRIVACY.md)를 따른다.

```bash
# 빠른 개발 검사
bash scripts/test-mission-recommendation.sh
bash scripts/test-personalization.sh
bash scripts/test-mbct.sh
bash scripts/test-walking-personalization.sh
# 모든 필수 순수 검사: Python, Mono/.NET, Kotlin, Java 필요
python3 scripts/qa.py --tier unit --output artifacts/team-unit
# 라이선스가 있는 Unity 환경의 통합·Play·화면 검사
python3 scripts/qa.py --tier unity --output artifacts/team-unity
python3 scripts/qa.py --tier visual --output artifacts/team-visual
# Android 플랫폼 경로를 바꾼 경우
./scripts/build-android.sh
```

각 실행은 새 output 경로를 쓴다. unit은 Unity/ADB를 실행하지 않는다. Unity tier는 같은 Library에서 직렬 실행한다. `test.sh` 하나를 전체 검사 통과로 표현하지 않는다. 실제 기기 테스트는 명시 serial·기존 데이터 백업·설정 복원 절차를 따라 별도로 실행하고 raw 자료를 Git에 넣지 않는다.

| 수용 대상 | 필수 확인 |
| --- | --- |
| A/UI | 유형·기간별 안내/선택/진행/중단/복습, 글 없이 완료, 선택 입력과 skip, 저장 실패에서 초안 유지, pause/Back/탭/재시작, 긴 한국어·키보드·다양한 해상도, 실제 터치 검증 |
| B/개인화 | 같은 입력의 재현성·정책 버전, 후보 적격성, 미학습/무동의/만료/네트워크 실패 대체, 결측≠0, 시작한 목표 유지, 시간 경계, 새 저장 호환, 동의 철회·삭제·늦은 응답 경합 |
| 공통 | 단일 활성 세션, 기존 2/4/8주 진도·정원 보존, 중복 보상 방지, UI가 후보/특징을 재계산하지 않음, 노출→선택→세션 귀속 일치, API schema parity, 개인정보 검사 |

### 이번 준비 작업의 검증 기록

- 필수 `unit`: **17/17 PASS**, SKIP 없음. Python 실행기 45개·서버 22개 포함. 새 추천 선택 검사 **23개 PASS**. 최초 준비 작업의 결과이며 아래 Unity 결과를 대신하지 않는다.
- 최초 Roslyn 검사는 실제 Unity/package 캐시 참조를 사용한 컴파일 성공이었다. 이후 **2026-10-27, 코드 `49b84399429ddac2bf7dab208a082b7202173433`에서 실제 Unity 6000.3.24f1 재검증**을 실행했다. 시작 시 작업 트리는 깨끗했고 제품 소스·저장 계약을 수정하지 않았다.
- 최초 root 실행의 `project-bindings`·`local-loop`는 라이선스 인식 실패로 실행 전 FAIL이었다. 이를 라이선스 만료로 단정하지 않는다. 기존 **OS 사용자 codex·HOME="/home/codex"**로 실행하자 entitlement를 정상 인식하고 검사가 진행되었다. 라이선스 파일/키 복사·수정과 새 사용자 로그인 없이 해결했다.
- **Unity tier 6/6 PASS, FAIL/SKIP 0:** project-bindings, local-loop **39/39**, EditMode **151/151**, Play fresh/restore, 실제 UnityWebRequest→로컬 API 동의/업로드/철회/삭제, 음악 Play **54개 확인**. 통신 fixture의 임시 인증·서버 데이터 정리도 확인했다.
- **Visual tier 7/7 PASS, FAIL/SKIP 0:** tabs, supplemental-ui, guided-ui, demo-polish, sky, lighting, procedural-scene. 기본 138장·추가 상태 195장·8주 전체 안내 585장, 합계 **918장**의 UI 캡처에서 보고된 텍스트 넘침은 **0건**이었다. 복습 추천·미션 단계·추천 설정의 대표 화면 3장도 직접 확인했다. 전체 이미지에 대한 사람의 검토나 모든 2/4주 화면·실기기 터치 검증을 뜻하지 않는다. 두 tier 합계 **13/13 PASS**.
- Play 로그에는 Unity 내부 `UnityEditor.Search.SearchDatabase` 초기화의 `ArgumentOutOfRangeException`이 기록됐다. 해당 검증은 계속 실행되어 위 assertion과 결과 파일이 PASS였으며, 제품 호출 스택이 없는 Editor 검색 초기화 예외로 구분해 기록한다. 로그에 오류가 전혀 없었다는 의미는 아니다.
- 실행 전 Editor 프로세스와 잠금을 확인했다. 이전 root 실행이 남긴 `Temp` 및 `Temp/UnityLockfile` 두 경로만 codex 소유로 수정했으며 전체 checkout chown/권한 완화는 하지 않았다. 검증 중 생성된 폰트 캐시 변경은 별도 보존 후 모든 Editor 종료를 확인하고 기준 자산으로 복원하여 배포 대상에서 제외했다.
- 최초 증거는 ignored `artifacts/team-handoff-20261027/`에 유지한다. 재검증은 별도 `artifacts/team-handoff-20261027-codex-unity/`의 `results.json`, `editmode.xml`, 각 검사 결과·캡처에 보존한다. 실제 원격 commit/push/CI 기록은 Docs 저장소 인수인계에 남긴다.
- 이번 추가 배포는 검증 기록·실행 계정 안내를 갱신하는 문서 변경이다. 두 팀원 브랜치는 최초 기능 기준 `49b8439`에 유지하며 강제 갱신하지 않는다. 통합 브랜치의 문서 갱신은 필요할 때 일반 merge로 가져온다.
- Android APK·실기기·키보드/터치·배포 서버 검증과 향후 두 기능의 수용 검사는 별도다. 이번 변경에 네이티브 수정은 없고 실제 기기에 접속하지 않았으므로 신규 기기 설정 변경·복원 대기는 없다.

이번 worker에서 실행한 명령(관리 셸에서 OS 계정을 전환하며, 팀원은 자신의 라이선스가 설정된 계정에서 실행):

```bash
runuser -u codex -- env HOME="/home/codex" USER=codex LOGNAME=codex \
  UNITY_BIN=/opt/unity/editors/6000.3.24f1/Editor/Unity \
  xvfb-run -a -s '-screen 0 1920x1080x24' \
  python3 scripts/qa.py --tier unity --tier visual \
  --output artifacts/team-handoff-20261027-codex-unity --timeout 600
```

이 명령의 output 경로는 이미 사용했으므로 재실행 시 새 경로로 바꾼다. 라이선스 원문·로그·캡처·합성 저장 파일은 커밋하지 않는다.
