# 확정된 실습·알림 정책 구현 및 다음 판단 — 2026-11-06

기준: `misssion` / `1c9f2fe049f32abe628559456fa3085526cf7223` 위 로컬 변경. 사용자의 최신 저장소 지침에 따라 **unstaged, commit/push 없음**. Google Docs 원문 b·c·h 전체의 완료를 주장하지 않는다. 아래 구현·검증 표는 11/06 작업의 기록이며, 최신 작업 범위는 **11/07 사용자 범위 확정과 현재 작업**을 따른다. 전문가 검토·임상 효과 검증을 받았다는 의미가 아니다.

## 구현 계약

| 영역 | 연결 파일 | 실제 동작 |
| --- | --- | --- |
| b-ii·b-iii·c-i 카탈로그 | `FocusMissionCatalog.cs` | v3 목적·허용 방법·보존 단계·근거 범위·전문가 미검토 상태. v1/v2 저장 정의 유지. 새 다른 목적 대안 P19/P26→P02 분리 |
| 실습·저장 | `LocalGardenState.cs`, `FocusPracticeInteractions.cs`, `WeekOneQuestDemo.FocusPractice.cs`, `.AllFocusPractice.cs`, `.FocusAdjustments.cs` | 실제 시작/현재 구간 단축/전체 중단/정상 완료 구별. 타이머 시작 뒤 too_long, 팝업 pause·취소 복귀. 확정 단축만 다음 추천에 사용, 시작 시 추천 snapshot 저장 |
| 비시간 실습 | 같은 파일 | 그리기 첫 획, 이야기 재생/읽기, 실제 입력·계획 등 실습 의미에 맞춰 시작 기록. 단순 준비·방법 선택으로 보상 자격을 만들지 않음. 직접 수행 활동은 명시적 실습 시작 지원 |
| M10 | 같은 파일 | 활동·시간대·선택적 직접 입력, 부분 선택/최신 입력 원자적 저장. 명시적으로 새 활동 프리셋을 선택할 때만 기존 직접 쓴 계획 교체. 알림 시각과 연동하지 않음 |
| 실제 대안 활동 | `LocalGardenState.cs`, `WeekOneQuestDemo.FocusHome.cs` | 원 회차는 Ended/alternative, P02는 additional/support. 실제 활동·출처 연결을 저장. P02 지원 활동을 기본 코스로 오인하지 않음. 과거 기록 재분류 없음 |
| c-ii 추가 추천 | 카탈로그·FocusHome·LocalGardenState | 진행 중 활동 우선, 오늘 완료 회피, 기존 선호·참여 이력 사용, 동일 근거는 날짜별 순환. 정책·이유·결정 ID 저장. 원격 고정 4특성 계약과 별개 |
| 참여 성장 | `WeeklyPlantState.cs`, `GardenHomePresenter.cs` | 명시적 미완료 종료+실제 시작만 지급. 영양제 없음. 기본 동일 회차/추가 동일 미션·일 중복 제한, A안 합산 정산, 원자적 저장. 기존 Recipe·Seed·21회 성숙·하루 정상 성장3회 유지 |
| 알림 | `ActivityNotification*`, `AndroidPlatformBridge*`, `SensorRawDisplay.cs` | 최초 설정/미정12:00/자동 기본 ON/유효 시각 편집/이력 유지. 옵션 전용 원자적 native 명령으로 토글이 시각을 덮어쓰지 않음. 새 정규 알림 클릭에서 최적화, 오래된 알림 클릭 제외. 게시 확인 실패 안내 |

표의 파일은 별도 표시가 없으면 `Assets/Scripts/Runtime/` 아래다. Kotlin 파일은 Android platform 플러그인 안에 있다. 기존 단계 키·그림 저장·기본 코스 순서·M15 걸음 목표 고정·건강 결측 처리·원격 API는 유지한다. M11 회고록 보관 설계는 추가하지 않았다.

참여 성장 수치는 개발 실험값이다: 정상100, 부분20, 부분 일일60, 식물별 미정산100. 첫 부분 성장의 크기 배율 약 +2.68%, 합계100에서 +6%. 지급 조건/A안은 확정이지만 이 수치와 시각 강도는 사용자 검토 대상이다. 원장에 당시 정책과 지급량을 보존해 나중 튜닝이 과거 성장을 바꾸지 않게 했다.

새 활동/시간 추천은 관측값을 사용하는 로컬 규칙이며 임상 최적화나 효과 검증 결과가 아니다. 카탈로그의 30/60초 등 기본값을 표준 MBCT 권장량이라고 표기하지 않는다. 임상적 목적·실습량은 전문가 미검토 상태다. 11/07 사용자 지시에 따라 상담사 검토를 이번 개발의 필수 선행 작업이나 재질문 항목으로 두지 않는다.

## 11/06 구현 검증 기록

| 검사 | 결과 |
| --- | --- |
| Focus 상태 / 조절 통합 / 계약 / 전체 미션 | 1679 / 503 / 129 / 2559 assertions PASS |
| 로컬 저장 / 걷기 / 기존 추천 / 외형 / MBCT / 원격 개인화 | 324 / 55 / 23 / 115 / 1500 / 329 assertions PASS |
| 참여 성장 | 741 checks PASS — rollback, 중복, 삭제, 일자 경계, 구버전 XML, 성숙/식물 전환 |
| 건강 조회 | 합성 JVM 33/33 PASS |
| 알림 C# / Kotlin 정책 | 42 assertions / 27 cases PASS |
| Android 호스트 / 합성 APK | Robolectric 10/10 PASS, 제품 Kotlin을 사용한 테스트 APK 컴파일 PASS |
| Unity 조절 UI | 5/5 PASS, 42 clicks/42 first-hit raycasts |
| Unity 코스 UI | 5/5 PASS, 전체45 미션 완료·기록, 610 clicks/613 raycasts |
| Unity local-loop | 39/39 PASS |
| Unity project bindings | PASS |
| Android release C# scripts | PASS, runtime assemblies22, development-only methods8 excluded |
| Privacy | Gitleaks v8.30.1(공식 릴리스 checksum 확인) + 저장소 scanner, 앱/Docs publishable worktree PASS. staged 검사는 게시하지 않아 미실행 |

Unity 6000.3.24f1은 OS 사용자 codex와 기존 HOME 환경에서 실행했다. root 실행·라이선스 복사/수정 없음. Unity가 부수적으로 갱신한 폰트 feature table은 ignored artifacts에 백업하고 변경 전 원본으로 복원했다. 기능 변경과 함께 올릴 생성 산출물이 아니다.

주요 증거는 ignored `artifacts/confirmed-policy-20261106/`와 `artifacts/confirmed-policy-20261106-final/`에 있다. 후자의 `unit-tests.log`, `focus-adjustment-validation/report.json`, `local-loop-validation/results.json`, `release-scripts-report.json`을 확인한다. 전체 코스 결과는 전자의 `all-course-validation/report.json`. 알림은 `artifacts/notification-1106-settings/`의 호스트 XML 및 빌드 로그를 확인한다.

검토 화면:
- `artifacts/confirmed-policy-20261106-final/focus-validation/adjust-02-paused-shortening-modal.png`
- `artifacts/confirmed-policy-20261106-final/focus-validation/adjust-06-plan-choices.png`
- `artifacts/confirmed-policy-20261106-final/focus-validation/adjust-08-communication-choices.png`

모든 화면은 합성 메모리 상태다. 타이머8초는 검증용 주입값이며 실시간 정확도 시험이 아니다. 실제 기기 알림 전달률·오디오 청취·장기간 배터리/백그라운드·사용자 효과를 검증했다고 해석하지 않는다. 11/06의 위 검증에는 기기 접근/설정 변경이 없었다. 11/07의 기기 사전 점검은 아래에 별도로 기록한다.

## 11/07 사용자 범위 확정과 현재 작업

사용자는 앞서 제안한 다음 작업 중 상담사 문의는 할 수 없고, 별도 워치 앱 개발은 하지 않으며, 성장 체감은 직접 사용한 뒤 판단하고 베타 준비는 이번 작업에서 다루지 않겠다고 명시했다. 이어 남은 개발 작업이 있으면 진행하도록 승인했다. 따라서 이전의 ‘상담사 검토 → 워치 앱 범위 선택’은 현재 진행 순서가 아니다.

| 원문/연결 요구 | 현재 결정 | 이번 처리 |
| --- | --- | --- |
| b-ii·b-iii-4 | 상담사 검토를 이번 필수 작업에서 제외 | 45개 목적·대체 검토표와 카탈로그의 전문가 미검토 상태는 보존. 사용자 수용을 임상 승인으로 바꾸지 않음 |
| c-ii | 새 Focus 실습에서 확정한 방법·빈도 선택을 추가 활동 추천에 연결 | 정상 완료의 확정 응답을 연결하고 단위·Unity 검증 완료. 임시 입력·자유서술·다른 목적의 선택을 선호로 확대 해석하지 않음. 상세는 아래 11/07 구현 결과. 시간 조절은 기존 확정 too_long 경로 유지 |
| c-ii 비교 평가 | 추천을 본 뒤 실제로 선택·시작·완료/중단했는지 연결 | 노출 결정 ID·정책·이유와 추천/실제 선택을 분리하는 연결 구현. 단위·Unity 및 A7 네이티브 합성 측정 검증 완료. 외부 서버 미배포. A7 알림 설정·중단·타이머·완료 UI 검증 완료, 키보드·입력 복원 및 최종 정리는 진행 중 |
| c-iii·h-i | 별도 워치 앱과 직접 IBI 수집 개발 제외 | 기존 걸음·수면 입력 유지. 새 건강 권한·HRV 수집이나 스트레스 추정을 이번 작업에 추가하지 않음 |
| c-iv·h-ii | 스트레스 신뢰도 검증 완료로 표시하지 않음 | 기존 데이터 경로의 검증과 스트레스 해석 검증을 분리. A7의 일반 앱 QA를 c-iv 스트레스 정확도 검증으로 집계하지 않음 |
| 연결 요구 — A7 QA | 연결된 A7에서 가능한 검증 진행 | 사전 백업·설정 복원 및 네이티브 측정/프로세스 복구 검증 완료. 같은 소스의 별도 앱 QA APK 빌드와 주요 UI 검증 완료. 키보드·입력 복원 및 최종 정리만 진행 중 |
| 대화 추가 — 참여 성장 | 사용자가 직접 체감한 뒤 조정 | 지급 조건/A안 및 현재 실험 수치를 유지. 체감 평가·수치 재설계는 이번 작업에서 제외 |
| 팀 공통 후속 — 베타 | 이번 작업 밖에서 준비 | 모집·기간·서면 동의 문서 결정을 다시 요구하지 않음. 실사용 효과·재참여 개선을 관측 전 주장하지 않음 |

11/07 기기 사전 점검 자료는 ignored `artifacts/focus-personalization-20261107/device-preflight/`에 보관한다. A7(SM-T500, Android 10/API 29)은 연결되어 있었으며, 기존 앱 내부 자료·외부 정원 저장·설치 APK를 백업하고 화면 꺼짐 시간·밝기 설정을 복원·확인했다. 이 단계에서는 앱을 업데이트하거나 실행하지 않았다. 개인 자료·기기 식별자·원본 캡처는 공개 문서에 옮기지 않는다.

A7 기능 QA는 같은 소스의 별도 패키지 `com.capstonedesign2026.focusqa`와 합성 입력으로 진행하고 있다. 기존 제품 앱은 업데이트하거나 실행하지 않았다. 설치 패키지를 분리해 기존 데이터를 보존한다. 네이티브 알림·측정은 별도 `notificationpreview` 테스트 패키지에서 검증했으며 아래 결과에 기록한다. 두 방식 모두 실제 사용자 장기 사용·건강 기록 공급 검증과 구분한다.

## 11/07 구현 결과 — c-ii 명시적 선호와 추천 측정

### 선호 입력과 추천의 연결

`FocusMissionCatalog.cs`의 로컬 정책을 `focus-additional-history-preference-v2`로 버전 구분했다. 아래 입력은 **정상 완료된 세션에서 해당 선택을 확정한 경우**에만 사용한다. 단계의 임시 입력, 건너뛴 응답, 자유서술, ‘잘 모르겠음’, 미완료 세션을 학습 입력으로 만들지 않는다.

| 실습·선택 | 실제 추천 반영 | 경계 |
| --- | --- | --- |
| P07 `next=호흡` | 추가 M17 자연호흡 관찰 선호 | 오늘의 준비 활동 선택을 장기 선호로 추론하지 않고, 다음 실습에 관한 응답을 사용 |
| P21 `save=내 쉬는 방법으로 저장` + `activity` | 호흡→M17, 잠시 멈춤→M09 | 저장 의사가 명시된 경우만 사용 |
| P28 `next-activity` | 호흡→M17, 잠시 멈춤→M09 | 다음에 이어갈 활동에 관한 확정 선택만 사용 |
| P14 호흡 + `더 자주`/`덜 자주` | M17 추천 점수 +0.30/−0.30 | 개발 실험 가중치다. 추천 빈도 보장이나 실습 시간 변경이 아님. ‘지금처럼’은 이전 입력 유지 |
| 기존 설정 선호와 새 응답 | 가장 최근 명시적 선택 우선 | `PracticeAnswer.UpdatedUtc`를 저장하고 후속 설정 편집을 반영. 시각 없는 구버전 설정은 fallback으로 보존 |

몸의 접촉을 M05 스트레칭으로, 주변 소리를 M08 음악으로 새로 매핑하지 않는다. 의미가 다르기 때문이다. 명시적이지만 대응 추가 활동이 없는 새 선택은 이전 선호 가중치를 해제할 수 있으나 다른 목적의 활동을 대신 추천하는 근거로 쓰지 않는다. 알림 클릭 시각·새 건강 입력은 이 정책에 넣지 않는다. 기존 기본 코스 순서, M15 걸음 목표 고정, 확정 too_long 시간 추천은 유지한다.

### 실제 노출부터 결과까지의 측정

`WeekOneQuestDemo.FocusHome.cs`에서 화면에 보인 추천을 기록하고, `LocalGardenState.cs`의 세션 snapshot으로 실제 선택에 연결한다. `PersonalizationClient.Measurements.cs`와 Android `MeasurementTelemetry.kt`/`MeasurementBuffer.kt`가 기존 동의·등록 경계의 영속 outbox에 사건을 남긴다. `server/measurements.py`는 허용한 필드·카탈로그·정책·이유만 받으며, `server/analyze_measurements.py`는 정책별 추천 흐름을 분리해서 분석한다.

- 사건 종류는 `focus_recommendation_shown`, `selected`, `started`, `completed`, `ended`이며 뒤 네 이름에도 같은 `focus_recommendation_` 접두사를 사용한다.
- 추천 카드가 viewport에 절반 이상 보이거나 해당 추천을 눌렀을 때 노출을 만든다. 재구성 때 최초 결정 ID·시각을 재사용한다. 라이브러리 직접 시작은 가짜 노출을 만들지 않는다.
- 노출한 추천과 다른 미션을 골라도 `recommended_activity_id`와 실제 `activity_id`를 별도 보존한다. 저장한 노출 snapshot이 재시작 후 선택·시작·종료의 귀속 기준이며, 뒤늦은 선택으로 누락 노출을 만들어내지 않는다.
- 전송 대기·재전송·확인 응답과 중복 처리는 기존 outbox 경로를 사용한다. 앱/기기에서 내보내는 값에 응답 원문·기분·새 건강 수치를 추가하지 않는다.
- 기존 알림 게시/클릭 시각과 일반 활동 사건은 유지한다. 원격 추천의 고정 4특성 모델 계약도 유지한다. **측정 endpoint의 허용 사건 종류는 확장됐으므로 외부 서버를 같은 코드로 갱신해야 한다.** 외부 서버는 이번 작업에서 배포하지 않았다. 구버전 서버는 새 사건을 거부하므로 batch가 전송 대기로 남는다.

기존 서면 동의·등록 절차를 유지하고 앱 내 동의 UI는 추가하지 않는다. 합성/실사용 dataset을 분리한다. 분석 도구의 정책별 선택·완료 결과는 관측을 정리하는 기능이며, 이번 합성 검증을 개인화의 효과·참여 증가·알림 지연 감소 실증으로 제시하지 않는다. 세부 계약은 [QA·베타 측정 문서](./QA_BETA_MEASUREMENT_PLAN.md)의 Focus 추천 계측 절을 따른다.

### 11/07 변경에 대해 새로 수행한 검증

| 검사 | 새 결과 | 확인 범위 |
| --- | --- | --- |
| Focus 선호 | 73 assertions PASS | 확정 응답, 목적별 연결, 설정 우선순위, 구버전 fallback, 빈도/시간 분리 |
| 조절 통합 / 카탈로그 계약 | 503 / 129 assertions PASS | 기존 조절·목적 계약 회귀 |
| Focus 상태 / 전체 미션 / 로컬 저장 | 1679 / 2559 / 324 assertions PASS | 저장·전체 미션 진행 회귀 |
| 걷기 / 원격 개인화 | 55 / 329 assertions PASS | 기존 M15와 고정 4특성 계약 회귀 |
| Focus 측정 상태 | 40 assertions PASS | XML 복원·복사·노출 시각 경계·저장 실패·직접 시작 |
| Unity FocusAdjustment 최종 재실행 | 8/8 PASS, 50 clicks/50 raycasts | 기존 5개 흐름 + 노출 귀속·직접 시작·다른 활동 선택 3개 화면 검증 |
| Unity local-loop 최종 재실행 | 39/39 PASS | 기존 정원·활동·저장 루프 회귀 |
| Android QA APK / release scripts | 최종 빌드 PASS / release scripts PASS | 같은 소스의 별도 QA 패키지와 release용 C# 컴파일 |
| A7 네이티브 측정 생성 | 24 checks PASS | 실제 OS 알림 게시·PendingIntent 클릭, 추천 수락/다른 선택, 중복 노출, 정리 |
| A7 새 프로세스 복구 | 16 checks PASS | 동일 outbox 복구·ACK·철회·저장 오류/손상 경계·정리 |
| A7 네이티브 알림 | 476 checks PASS, 가상 페르소나 5종 | 약55초의 기기 실행 안에서 가상 시각으로 정책 검증. 기기 시스템 시각 변경 없음 |
| A7 화면 설정 복원 | 측정·알림 작업 모두 `displayRestored=true` | 작업 종료 후 원래 화면 꺼짐 시간·밝기·밝기 모드 복원 확인 |
| 서버 측정/분석 | 52 tests PASS, 그중 신규 Focus13 | 새 사건의 strict 계약·분석 경계 검증. 외부 서버 배포 결과는 아님 |
| Android 호스트 | Robolectric14 PASS | 알림·Focus 측정의 네이티브 호스트 회귀 |
| 공통 QA·기기 실행기 | harness50 PASS | 신규 단위4종·시각 검사 등록. Android 10의 `pm path` 결과·빈 백업·입력 stdin 처리 보완 및 회귀 |
| Privacy | 앱1731 / Docs52 publishable files PASS | 최종 문서 갱신 후 재검사 결과. 게시하지 않아 staged 검사·commit/push는 하지 않음 |
| A7 초기 알림·설정 UI | PASS | 미정 선택→12:00·자동 최적화 ON, 자동 최적화 OFF 후 시각 유지, 권한 거부 흐름 |
| A7 기본 P04 전체 넘김 | PASS | 완료 보상0·영양제0·참여 성장20, 기본 코스 현재 회차 유지 |
| A7 M02 타이머·완료 | PASS | 팝업 pause·취소 복귀·현재 구간 단축·나머지 단계 후 정상 완료. 상세 아래 |
| A7 키보드·입력 복원 / 최종 정리 | PASS | M10 차 마시기·저녁·합성 입력이 강제 종료/재실행 후 복원. 검증용 패키지 삭제, 화면 설정3종 원복 검증 |

A7 네이티브 시험은 합성 등록 구간의 12개 사건(알림·일반 활동 기존 사건 4개 + Focus 사건 8개)을 만들고 새 프로세스에서 정확히 복구했다. 측정 생성·복구40개 검사는 실제 시각·PendingIntent와 새 프로세스 복구를 사용했다. 별도 알림476개 검사의 5개 페르소나는 가상 시각을 사용하는 합성 정책 시나리오이며 약55초 실행을 장기 실사용 관측으로 표현하지 않는다. 기기의 시스템 시각은 바꾸지 않았다. 두 검사 모두 실제 A7에서 실행했지만 모집된 베타 이용자의 사용 기록은 아니다. 각종 시간·실제 선택이 저장되는 경로를 검증한 것이며 장기 전달률이나 효과를 검증하지 않았다.

증거는 ignored `artifacts/focus-personalization-20261107/`의 `host-unit-results.json`, `unity/focus-adjustment-validation/report.json`, `native-measurements/produce-result.json`, `restore-result.json`, `result.json`과 `native-notifications/results.json`에 있다. Unity는 기존 codex 사용자/HOME 환경으로 실행했다. 전체 원본 로그·캡처·기기 자료는 비공개 위치를 유지한다. 최종 Unity 빌드·재실행 묶음은 같은 경로의 `unity-final-progress.json`이 PASS다. 중간 빌드 중단이나 테스트 실행기 오류를 앱 기능 실패로 집계하지 않으며, 위 표에는 최종 재실행 결과만 기입했다.


### A7 앱 UI에서 실제 확인한 경계

같은 소스의 분리된 QA APK에서 직접 터치하고 합성 저장 상태를 대조했다. 초기 미정 선택은 12:00·자동 최적화 ON으로 저장됐고, 자동 최적화를 OFF로 바꿔도 알림 시각은 유지됐다. 권한을 거부한 상태의 안내 흐름도 확인했다. 기본 P04를 실제 시작한 뒤 전체 넘김하면 정상 완료 보상과 영양제는 0, 참여 성장은20이며 현재 기본 회차는 그대로 남았다.

M02에서는 타이머 시작 뒤 ‘너무 길어요’ 팝업을 열었을 때 경과값9초에서 멈췄고, 실제로6초 기다린 뒤에도9초였다. 취소 후에는 다시 진행됐다. 이어 경과15초에서 ‘여기까지 하기’를 확정하면 현재 구간만 끝나고 단계2로 이동했으며 정상 완료 보상은 아직0이었다. 나머지 단계를 수행하고 선택적인 기록을 생략한 뒤 정상 완료하면 보상5가 지급됐다. 현재 구간 단축·전체 미션 넘김·선택적 기록 생략을 서로 다른 동작으로 확인했다.

이 UI 증거는 ignored `artifacts/focus-personalization-20261107/device-ui-final/`과 `device-ui-final2/`에 있다. 11/06 Editor의 합성8초 주입과 이번 A7의 실제 대기 관측을 구분한다. M10에서는 실제 Android 키보드를 열어 합성 문자열을 입력하고 입력 마치기를 눌렀다. 앱 강제 종료·재실행 후에도 차 마시기·저녁·입력문이 화면과 저장 파일에서 그대로 복원됐다. M02의 정상 완료 보상5와 추천 M01/실제 선택 M02의 구분도 유지됐다. 최종 `device-ui-final2/result.json`은 PASS, `temporaryPackageRemoved=true`, `displayRestored=true`이며 화면 꺼짐 시간·밝기·밝기 모드 복원을 확인했다. 저장 증거에 대한 추가13개 검사도 `device-ui-review.json`에서 PASS다. 기존 제품 앱은 업데이트하거나 실행하지 않았다. 이 결과는 개발용 APK의 기능 QA이며 배포 빌드 성능 측정이나 실사용 베타 결과가 아니다.

Docs의 `기획서/Google Docs b·c·h 진행표와 세부 설계 2026-11-06.md`와 `기획서/c-iii·c-iv·h 웨어러블 스트레스 데이터 경로와 QA 근거 2026-11-06.md`에 세부 근거와 원문 번호가 연결되어 있다. 코드와 문서 변경은 로컬 unstaged 상태로 유지하며, 별도 공개 요청 없이는 commit/push하지 않는다.
