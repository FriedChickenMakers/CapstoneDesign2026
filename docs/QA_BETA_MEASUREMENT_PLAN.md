# QA·베타 측정 범위와 발표 근거

2026-10-27 추가 범위. 목적은 **알림 제공 시각과 클릭 시각의 차이가 실제로 줄었는지 평가할 수 있는 원자료**를 남기는 것이다. 감소·지속사용 향상·임상 효과를 미리 확정하지 않는다. 사용자 타겟, 2주 MBCT의 근거, 실험군 배정과 설문은 회의에서 결정할 별도 항목이다.

## 구현한 범위

| 영역 | 수집/동작 | 해석과 한계 |
| --- | --- | --- |
| 일일 알림·걷기 달성 알림 | 같은 `notification_id`로 due·게시 수락·클릭·권한 차단·게시 실패·복구 영수증 연결 | `notify()` 반환 직후를 **게시 수락**으로 기록. 사람이 실제로 보거나 소리를 들었다는 증거가 아님 |
| 예정/제공/클릭 시각 | 예정 UTC ms, 이벤트 UTC ms, UTC offset, monotonic elapsed ms와 boot count | 일일 예정 시각은 worker가 due를 관측할 때 계산한 정책 예정값. Android 정확 알람 예약/실제 화면 표시 시각이 아님. 걷기는 시각 예약형이 아니므로 예정값 0 |
| 정책 정보 | `daily-fixed-v1` / `daily-adaptive-v2` / `walking-goal-v1`, 사용자가 정한 분, 적용 분, 학습 표본 수, 앱 버전 | 기존 학습 알고리즘·기본값은 유지. 이 정보만으로 무작위 대조실험이 되지는 않음 |
| 활동 이용 | 동의 이후 선택한 전체 미션의 선택·시작·완료·종료 시각, 카탈로그 ID, 가명 세션 키 | 추천 미션만 수집하던 기존 경로에 더한 별도 측정. 일기·감정 원문·건강 수치·위치·연락처·기기 식별자 제외 |
| 전달 신뢰도 | pending outbox, 재전송·중복 제거, 용량 초과 시 거절 횟수, 만료 제거 건수, 저장 오류 여부 | `dropped_attempts`는 재시도를 포함한 **거절 시도 수**이며 유실된 고유 알림 수가 아님 |
| 분석 산출물 | `notification-pairs.csv`, `summary.json`, `notification-evidence.png` | 기본 24시간 응답 창, 클릭 지연 중앙값/p90·클릭률·관측 불완전 건수를 함께 제시. `live`와 `synthetic`을 필수로 분리 |

알림을 누른 시점은 Android 진입 Activity에서 즉시 기록한다. 기록 업로드는 Unity 앱의 연결 가능한 동기화 주기에 실행되므로, 알림을 누른 시각을 앱의 나중 polling 시각으로 바꾸지 않는다. 미클릭 알림도 게시 영수증을 보존한다. 권한 차단/실패는 실제 worker가 그 상태를 관측한 경우만 기록하며, 사용자가 알림을 꺼 worker 자체가 취소된 모든 날짜를 자동 관측한 것으로 세지 않는다.

정상 게시 뒤 프로세스가 죽어 영수증 저장이 끝나지 않은 경우 복구 사실을 별도 사건으로 남긴다. 복구 시각을 최초 제공 시각으로 꾸며 지연 계산에 사용하지 않는다. 동일 알림의 재시도와 중복 클릭은 최초 기록을 유지한다. 사용자가 나중에 다시 동의해도 이전 동의 구간의 notification ticket을 새 데이터로 수집하지 않는다.

## 동의·저장·전송

- 기본 수집 OFF. 기존 앱 등록 파일과 참여 동의가 있어야 한다. 신규 참여 화면에는 알림·전체 활동·품질 정보, QA/발표 용도, 90일 보관, 철회·삭제를 안내한다.
- 기존 추천 연구 동의만 있는 사용자는 추가 측정이 자동 활성화되지 않는다. 설정의 **추가 항목 확인 · 알림과 전체 활동 측정에 동의**를 눌러야 한다. 동의 경계 `MeasurementConsentUtcMs` 이전에 선택한 활동 이력을 소급 전송하지 않는다.
- native `measurement-outbox.json`은 앱 private files에 원자 저장한다. 1,024 pending events / 8,192 중복 방지 영수증, 최대 100개 batch. 90일이 지난 자료는 다음 수집/동기화 시 정리한다. 앱/worker가 실행되지 않으면 즉시 만료 삭제를 수행할 수는 없다.
- 기존 추천용 네 특징 API와 분리한 **`POST /measurements/batch`**, schema 1 / consent 2를 사용한다. 동일 bearer 등록과 가명 subject를 사용하고 새 endpoint도 인증·필드 제한·배치 원자성·ID 충돌·멱등성을 검사한다. journal text나 임의 필드는 거부한다.
- 서버 `measurements`와 `measurement_quality`를 함께 보관한다. 기존 `DELETE /participant`가 추천/측정/품질 행을 모두 삭제하고 revoked 등록의 늦은 업로드를 차단한다. 별도 export와 백업은 운영 담당자가 삭제해야 한다.
- 철회 시 native outbox를 먼저 끄고 지운 뒤 기존 삭제 대기를 저장한다. 저장 실패는 성공으로 표시하지 않고 재시도가 필요하다. 새 동의 구간·계정의 outbox에 이전 HTTP 응답으로 ACK하지 않는다.
- MOCK/REPLAY는 실사용 측정 경로에서 제외한다. 합성 native instrumentation만 내부 dataset override를 사용하며, 일반 UI나 외부 Intent로 이를 켤 수 없다. `live` 라벨은 기기 실행 경로이며 참가자 모집·실사용 진위를 대신 증명하지 않는다. QA와 베타는 등록·DB·결과 폴더도 분리한다.

## 분석 규칙

1. **주 지연:** 최초 게시 수락→최초 클릭. 동일 부팅의 monotonic 시각으로 계산한다. wall time 차이와 2분 넘게 어긋나거나 재부팅/시각 누락이 있으면 `clock_unreliable`로 분리하며 지연 통계에 섞지 않는다. 2분은 분석 품질 점검 허용치이며 임상 기준이 아니다.
2. **OS 지연:** 정책 예정 시각→게시 수락을 별도 그래프로 표시한다. WorkManager/Doze 지연을 사용자 반응 지연과 섞지 않는다. 이 값은 wall clock이므로 기기 시계 상태도 함께 검토한다.
3. **분모:** 닫힌 응답 창의 게시 수락만 기본 클릭률 분모 후보로 삼는다. 미클릭 건은 마지막 서버 동기화가 응답 창 종료를 덮는지 확인하며, 아직 업로드 확인이 없으면 `awaiting_upload_confirmation`으로 분리한다. 종료 시점 직전의 알림은 `window_open`이다.
4. **누락:** 수집 구간에 용량 초과·만료·저장 오류가 있으면 해당 구간의 요약 클릭률/지연에서 보수적으로 제외하고 원자료와 제외 건수를 남긴다. 아무 클릭이 관측되지 않았다는 사실과 클릭하지 않았다는 확정 사실을 구분한다.
5. **고정/개인화 비교:** 정책별 표본 수·중앙값·p90·클릭률과 모든 관측 결과를 함께 본다. 클릭한 표본의 지연만 줄고 미클릭이 늘었다면 개선으로 단정하지 않는다. 동일인 전후 비교에도 순서·시간·학습 효과가 있으므로 무작위 배정이나 적절한 대조 없이 인과 효과로 발표하지 않는다.
6. **활동:** 선택/시작/완료/종료 세션 수를 별도로 출력한다. 활동 시각은 저장된 session에서 복구하며 앱에 머문 시간·중단 시간·치료 효과를 뜻하지 않는다. 알림→미션 세션의 직접 attribution ID는 이번 범위에 없으므로 알림 때문에 활동을 시작했다고 주장하지 않는다.

베타 종료 시 모든 참여 기기의 마지막 동기화를 확인하고, 마지막 분석 대상 알림의 응답 창이 닫힌 뒤 명시적인 `as-of`를 정한다. 참가자별 누락·철회 여부와 대상/대조 배정 기준은 운영 기록에 남긴다. 데이터가 부족하거나 지연이 늘어난 경우도 그대로 보고한다.

## 실행·운영 순서

1. [서버 안내](../server/README.md)의 등록·인증·HTTPS·private DB 준비. **서버를 먼저 갱신**하여 새 endpoint와 삭제 처리가 있는지 확인한다. 이 작업은 공개 서버 배포나 실제 참여자 등록을 자동 수행하지 않았다.
2. 최신 APK를 QA 기기에 설치하고 새 동의/철회를 확인한다. 실제 기기 작업은 [기존 보존 절차](REPOSITORY_PRIVACY.md)와 AGENTS의 백업·설정 복원 규칙을 따른다.
3. 고정/개인화 정책, 알림 권한 허용/차단, 클릭/미클릭/중복 클릭, 재시작·오프라인 후 재전송, 시계/시간대 변경·재부팅, 철회 후 늦은 업로드를 확인한다. 기기 자료는 ignored artifacts 밖으로 게시하지 않는다.
4. 앱에 동의가 있고 연결이 가능한 동안 데이터가 서버에 도착하는지 확인한다. native outbox·서버 카운트를 대조한다. 실사용 자료로 과거 합성 시나리오의 수치를 대체하거나 섞지 않는다.
5. 분석용 읽기 전용 DB 접근과 private 출력 폴더를 사용한다.

```bash
python3 scripts/qa.py --tier unit --output artifacts/measurement-unit-new
# 라이선스가 설정된 OS 계정에서
python3 scripts/qa.py --tier unity --output artifacts/measurement-unity-new

# 차트가 필요할 때 별도 Python 환경에 설치
python3 -m pip install -r scripts/requirements-measurement-plots.txt
umask 077
python3 -m server.analyze_measurements \
  --database /private/path/events.sqlite \
  --output-dir /private/path/measurement-report \
  --as-of-ms <명시적인-UTC-epoch-ms> --dataset live
# 그래프 의존성이 없으면 --no-plots 로 CSV/JSON만 생성 가능
```

원자료 CSV에는 가명 subject와 정확한 시각이 있으므로 private로 유지한다. 발표에는 검토한 집계와 그래프만 사용한다. 기본 차트는 참가자별 알림 순번에 따른 클릭 지연·게시 지연과 정책별 관측 결과를 보여주며 결과를 자동으로 "개선"이라고 판정하지 않는다.

## 코드 소유·병행 작업 연결

| 영역 | 시작 파일 | 협업 주의 |
| --- | --- | --- |
| Android 수집 | `MeasurementBuffer.kt`, `MeasurementTelemetry.kt`, `ActivityNotifications.kt`, `MissionRewardNotifications.kt` | 알림 동작/정책 자체를 바꾸지 않고 관측. 개인정보 필드를 임의 추가하지 않음 |
| 앱 연결·동의 | `PersonalizationClient.Measurements.cs`, `PersonalizationCore.cs`, `SensorRawDisplay.cs` | 기존 동의 v1과 추가 측정 동의의 경계를 유지. 개인화 담당과 공통 변경으로 조율 |
| 미션 UI 연결 | `WeekOneQuestDemo.Personalization.cs` | `RecordMeasuredSession`은 저장된 세션만 읽는다. UI 담당이 기록 원문을 이벤트 payload에 넣지 않음 |
| 서버·발표 분석 | `server/measurements.py`, `server/analyze_measurements.py` | 수집 계약 변경은 API 검사·내보내기와 같이 검토. 추천 모델 특징 schema는 그대로 유지 |

기존 두 작업 브랜치를 강제로 갱신하지 않는다. 이번 측정 변경이 들어간 통합 브랜치를 일반 merge하여 시작 기준을 맞춘 뒤 공통 파일 변경을 진행한다. UI 전체 구현이나 미션 완전 개인화 구현을 대신 완료한 것은 아니다.

## 미정·추가 구현 범위

- 타겟층(회복 중인 환자/저위험 일반인), 2주 변형 근거와 설명, 전문가 검토 범위.
- 실험군 배정·관측 기간·표본 수·기본 발송 시각·비교 기준. 현재 정책 ID를 기록한다고 연구 설계가 확정되는 것은 아니다.
- 지속사용 장벽 설문 문항·실시 시점·별도 동의와 설문 응답 관리. 현재 앱은 설문 원문을 수집하지 않는다.
- 알림→활동 직접 귀속, 앱 방문/화면 흐름·pause/resume 이력, 수집 누락의 더 세밀한 진단, 배포 서버 운영 대시보드. 이번에는 구현하지 않았다.
- 공학적 기여와 절차적 생성의 발표 방식. 회의 참고 안건이며 이번 작업에서 제품 방향을 확정하지 않는다.

## 2026-10-27 검증 기록

| 검증 | 실제 결과 | 범위 |
| --- | --- | --- |
| unit QA | 18개 suite PASS, skip 0 | 측정 outbox를 필수 검사에 추가. C# 개인화 329개 assertion 포함 |
| 서버 최종 회귀 | 36개 test PASS | 실제 HTTP endpoint, 중복·동의 구간 충돌, 삭제·늦은 전송, 시간/누락/미클릭 분석 포함 |
| Unity QA | 6개 suite PASS | project-bindings·local-loop 39개·EditMode 151개·PlayMode 새 실행/복원·기존 개인화 네트워크·음악 검사. 라이선스가 있는 OS 사용자 codex로 실행 |
| Android 빌드 | native instrumentation APK 빌드 및 제품 APK 패키징 성공 | Unity 전체 빌드 명령은 패키징 중 디스크 부족으로 2회 실패. 생성된 임시 objcopy/중복 IL2CPP 백업만 정리하고 동일 Gradle 프로젝트의 `:launcher:assembleDebug`를 재개하여 성공 |
| 분석 export | 합성 DB → CSV/JSON/PNG 생성 확인 | 파이프라인 smoke test이며 실사용 개선이나 실제 페르소나 실험 결과가 아님 |

원본 검증 기록과 APK는 ignored `artifacts/measurement-*`에 보존했다. 빌드 산출물과 로그는 Git에 넣지 않는다. Unity 검증과 APK 빌드는 **실기기에서 새 native 수집→앱 업로드→서버→그래프를 종단 검증한 결과가 아니다.** 이번 작업에서 기기에 접속하거나 실제 참가자 데이터를 수집하지 않았다. QA 배포 전 위 운영 순서의 실기기 검사와 서버 배포가 남아 있다.
