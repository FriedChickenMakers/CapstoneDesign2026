# 마음의 정원 — 야간 구현 결과 (2026-09-23)

P0 저장 무결성과 P1 코스·정원·건강정보 표시의 테스트 가능한 최소 흐름을 구현했다.
알림/백그라운드 Health Worker 및 전체 콘텐츠 제작은 남겨 두었다. 이번 실행에는
연결된 ADB 기기가 없었다. 이전 A7 결과와 이번 합성/Unity 검증을 구분한다.

실행 산출물 루트:
`/workspace/CapstoneDesign2026/artifacts/overnight-20260922T184659Z`

## 1. 실제 추가·수정 기능

- **IMPLEMENTED / UNIT_TESTED**: 원자적 단일 상태 저장, 체크섬·스키마 검증,
  단일 writer, 이전 저장본 유지, 손상 시 신규 자원 재지급 차단, PlayerPrefs 이관.
- **IMPLEMENTED / UNIT_TESTED**: 일일 회차와 코스·영구 정원 분리, 가상 시계,
  날짜 역행/시간대 변경 시 회차 중복 방지, 미접속 기간 몰아 지급·추첨 없음.
- **IMPLEMENTED / UNIT_TESTED / PREVIEW_TESTED**: P01~P28 코스, M01~M17 자유 선택,
  안내→시작→일시정지/복귀→직접 완료 또는 참여 종료→선택 기록→보상 저장.
- **IMPLEMENTED / UNIT_TESTED / PREVIEW_TESTED**: 영양제 사용, 식물 성장,
  꽃밭/쉼터 미리보기·취소·확정, 토끼 임시 모델 방문/미방문, 발견 목록.
- **IMPLEMENTED / UNIT_TESTED / PREVIEW_TESTED**: HC 전체 페이지 처리와 부분 상태,
  전체 출처/삼성 출처 Steps 집계 구분, 심박 샘플 시각·출처·구간 필터·정렬·중복 제거,
  활동 구간 재조회와 빈 구간/다른 출처를 잇지 않는 그래프, 수면 세션 길이 표현.
- **IMPLEMENTED / 일부 UNIT_TESTED**: 센서 상시 wake lock 제거, 비동기 단일 로그 writer,
  큐/파일 상한, 회전 실패·깨진 꼬리 줄 처리, reporting mode/wake-up/드롭 메타데이터.
- **IMPLEMENTED**: 명시적 serial S24 사전 점검 및 5분/30분/장시간 센서 검증 절차.

## 2. 테스트와 실행 환경

Unity 6000.3.24f1, 기존 Kotlin/HC 1.1.0·AGP 9, Linux 개발 VM,
Intel UHD Graphics 630 Vulkan/Xvfb. Unity 실행은 프로젝트별 파일 lock으로 직렬화했다.

| 검증 | 최종 결과 | 근거 |
|---|---|---|
| 기존 프로젝트 검증 | PASS | `baseline-validation.log` |
| 기존 APK 재빌드 | PASS | `baseline-build.log`, `baseline/build/capstone-mockup.apk` |
| 순수 C# 저장 도메인 | 288 assertions PASS, 0 FAIL | `local-state-results.txt`, `.json` |
| Kotlin 합성/파일 처리 | 20 cases PASS, 0 FAIL | `health-query-tests/results.json` |
| Unity 편집 모드 통합 | 14 cases PASS, 0 FAIL | `local-loop-validation/results.json`, `results.xml` |
| Unity 실제 Play 모드 | fresh / 별도 프로세스 restore PASS | `play-smoke-final/play-fresh.json`, `play-restore.json`, `play-smoke-final.log` |
| 실제 그래픽 렌더링 | MOCK 28 PNG 생성, 대표 화면 양 비율 육안 확인 | `visual/`, `local-loop-preview-reviewed.log` |
| 통합 Android 빌드 | 최종 진단 화면 포함 PASS | `final-build.log`, `final-build-reviewed.log`, `final-build-release-candidate.log` |
| 기기 권한·동기화·장시간 수집 | NOT_TESTED / SKIPPED | 시작 시 `adb devices -l` 장치 0개 |

초기 실패도 로그를 보존했다. 편집 모드 fixture의 임시 scene 오류, 그래프의
CanvasRenderer 누락 및 그래픽 중복 컴포넌트 오류를 수정하고 재검증했다.
그래프는 최종 두 비율에서 실제 24개 vertex 제출과 화면의 분리된 선을 확인했다.
메모 초안 손실, 비활성 활동 화면에서 회차 미갱신, 일일 저장 실패 후 이전 회차에
세션 생성, 화면 이동 후 무료 미리보기 잔존도 회귀 테스트에 추가했다.
첫 Play 시도는 Unity 검색 인덱서 예외로 callback이 정체되어 해당 프로젝트 프로세스만
종료하고 bounded update 기반 점검으로 바꿨다. 두 번째 fresh/restore가 통과했다.
VM의 ALSA 출력 장치 경고는 음원 재생 검증으로 해석하지 않는다.

## 3. 달라진 사용자 흐름

이전에는 Week 1 버튼에서 여러 PlayerPrefs 값을 바로 변경했다. 이제 실제 활동을
시작하고 직접 완료한 뒤 선택 기록을 남기거나 건너뛰면 완료·보상·코스 진도를 함께
저장한다. 저장 실패 시 직전 상태와 현재 메모 초안을 유지한다. 영양제로 기존 나무를
키우거나 꽃밭/쉼터를 미리 보고 확정할 수 있고, 다음 회차에 조건에 맞는 방문을 판정한다.
날짜만으로 코스가 진행되거나 자원이 줄지 않는다. 센서 권한 없이도 활동을 마칠 수 있다.
기록 화면의 건강 재조회는 보상/활동 상태를 바꾸지 않는다.

## 4. 보존·마이그레이션

기존 변경은 `baseline.patch`, `baseline-untracked-source.tar.gz`, `baseline-status.txt`,
`baseline-head.txt`, `previous-run.apk`로 먼저 보존했다. stash/reset/clean/강제 push는 하지 않았다.
실제 개인 기기 저장본은 접근하지 않았다. 이관 검증은 합성 PlayerPrefs-equivalent fixture다.
LIVE 시작 시 nutrient, garden XP, growth, last unlock, 알려진 Week 1 seed 및 완료 ID를
새 상태에 한 번 복사하며 원래 PlayerPrefs를 삭제하지 않는다. 날짜 미상은 그대로 남기고
Notion 코스 완료로 바꾸거나 XP를 영양제로 환산하지 않는다. MOCK/REPLAY는 별도 저장소다.

## 5. APK·이미지·재현 정보

- 최신 APK: `final/build/capstone-mockup.apk` (약 95 MiB).
- SHA-256: `cf0e0123b9df185352e63b8548ee0bda03af9d2a7560cdfdfb35a7de2feff9af`.
- 14개 장면 × 900×1600/1200×800: `visual/*.png`.
- 추천 확인: `visual/course-900x1600.png`, `visitor-900x1600.png`,
  `records-heart-900x1600.png`, `save-error-1200x800.png`.
- 해시/소스 목록: `source-and-apk-manifest.json`.
- 전체 소스 보존: `final-source.tar.gz`, `final-working-tree.patch`, `final-status.txt`.
- 모든 이미지는 **Linux Unity MOCK**이며 A7/S24 화면이 아니다.

## 6. 미검증·남은 범위

**DEVICE_TESTED_A7 / DEVICE_TESTED_S24: 이번 실행 없음.** 이전 A7 10초 화면 OFF 성공은
기존 문서의 역사적 결과다. 이번 wake lock 제거 후 결과로 재사용하지 않는다.
Android 파일 교체의 전원 차단 내구성, IL2CPP의 실제 기기 XML 저장/복원,
HC 권한 UI·출처 우선순위·Samsung/Watch 동기화는 NOT_TESTED다.

알림, background HC Worker, 앱 내 기록 삭제/보관 정책, 전체 이력 페이지·주간 집계,
음원 재생, 전체 동물 모델/행동은 미완료다. 건강정보로 정신 상태나 치료 효과를 판정하지 않는다.
Google Docs 지정 탭은 웹 도구 접근 실패(BLOCKED)였고 최신 Notion 4개 원문을 직접 읽었다.
상세 범위는 `PLAN_IMPLEMENTATION_GAPS.md`와 콘텐츠 출처 문서를 따른다.

## 7. DEV_DEFAULT

로컬 04:00 갱신; 회차·typed mission당 영양제 10, 중단 참여 0;
성장 비용 10/+0.1, 환경 비용 10; 조건 충족 회차 방문 50%.
현재 시각 모델은 단순 날짜 high-water이며 서버 부정행위 방지가 아니다.
기존 나무는 `plant:P06`의 교체 가능한 임시 시각물이고, 방문 가능 모델은 `animal:A07`
토끼만 켰다. 환경 후보 OR 연결·확률·회차 내 체류는 시연 정책으로 팀 확정값이 아니다.
음원은 준비 중이며 표의 원문과 별도로 작성한 텍스트 대안을 표시한다.

## 8. 다음 실행 명령

저장소 소유자 `codex` 환경에서 실행한다. root 셸이면 `runuser -u codex --`를 앞에 붙인다.

```bash
cd /workspace/CapstoneDesign2026
export CAPSTONE_ARTIFACTS="$PWD/artifacts/next-verification"
./scripts/test-local-state.sh
HEALTH_TEST_OUTPUT="$CAPSTONE_ARTIFACTS/health" ./scripts/test-health-query.sh
./scripts/test-local-loop-unity.sh
./scripts/preview-local-loop.sh
CAPSTONE_SMOKE_SAVE_ROOT="$CAPSTONE_ARTIFACTS/new-synthetic-save" ./scripts/test-local-loop-play.sh
./scripts/build-android.sh
./scripts/archive-local-loop.sh
./scripts/test-s24-health.sh --serial YOUR_EXPLICIT_SERIAL
```

Play smoke에는 새 비공개 합성 저장 경로를 지정한다. 시스템 시각은 변경하지 않는다.
실기기 설치는 사용자 지정 기기에 `adb -s SERIAL install -r APK`로 기존 데이터를 보존한다.

## 9. 로컬 커밋·미커밋 상태

원본 HEAD는 `a5e190b586b1cde4e09b526b52642d5029d22873`, 새 브랜치는
`codex/overnight-local-loop`다. 원본 `.git/objects` 일부가 다른 사용자 소유여서
소유자 계정의 커밋 staging이 거절됐다. 권한이나 safe.directory 보안 예외를 바꾸지 않았다.

대신 이 실행 산출물 아래 `review-repository`에 독립 복사한 Git 객체로 검토용 체크아웃을
만들었다. `codex/overnight-review`의 기준 커밋 `dfe0996`은 이번 실행 이전 작업만 보존한다.
구현 커밋은 `be1ad15`이다. 최종 문서 커밋과 bundle 정보는 `review-commits.txt`에 기록한다. 원본 작업 디렉터리는
기존 변경+이번 변경을 미커밋 상태로 그대로 유지하며 `final-status.txt`에 열거한다.
원격에 push하거나 공개 artifact를 게시하지 않았다.

## 10. S24/Watch에서 가장 먼저 확인할 것

명시적 serial로 사전 점검한 뒤 HC 가용성과 권한을 수동 확인한다. 같은 날짜 구간·시간대의
전체 출처 Steps와 Samsung 출처 Steps를 나누어 보고, 이어 실제 심박 샘플의 측정 시각과
출처가 활동 구간에 들어오는지 확인한다. Watch 측정→Samsung 표시→HC 표시→앱 조회의
각 시각을 기록한다. 값 차이는 구간·출처 우선순위·동기화 지연부터 비교한다.
Samsung 출처 기록만으로 워치 직접 연결/실시간 측정/진동 제어 성공으로 처리하지 않는다.

## 2026-10-02 S24 메인·출처 선택 검증

사용자 지정 무선 ADB로 SM-S921N(Android 16)에 연결하고, 요청에 따라
화면 꺼짐 시간을 30초에서 30분으로 늘렸다. `install --no-streaming -r`로
기존 저장과 권한을 보존해 최신 APK를 설치했다.

- DEVICE_TESTED_S24: 메인 날짜·탭 배치, 저장된 개화/환경 복원, 정원 회전,
  활동·설정 이동, 성장 미리보기와 취소, 설정 스크롤/새로고침, 그래프 열기/복귀.
- DEVICE_TESTED_S24: Samsung-origin 걸음·수면·심박 조회와 권한 4/4 유지.
  운동은 NO_DATA. 오늘 걸음은 단일 후보와 선택된 값이 일치했다.
- 실제 겹치는 복수 출처 및 워치 생성 여부는 NOT_TESTED. 이번 일일 구간에는
  후보가 하나뿐이고 device metadata도 unspecified였다. 합성 정책 테스트 27개로
  중복 출처, 동일 앱의 watch/phone, 구간 중복, 종류별 독립 선택을 검증했다.
- 실기기에서 비동기 걸음 보상 후 활동 헤더만 이전 영양제 값을 표시하는 문제를
  발견해 저장 성공 시 헤더도 갱신하도록 수정했다. Play 모드 fresh/restore와
  숨겨진 활동 화면의 보상 갱신 회귀 테스트 PASS. 수정 APK 재설치 후 S24의
  메인/활동 표시 일치와 Health Connect 조회를 다시 확인했다.

최종 APK: `artifacts/s24-summary-fix-build/build/capstone-mockup.apk`.
SHA256: `2bb69c23c89c33cb25807a89e62928cbc74583880bda0c3f785a8a58edf94642`.
빌드/Play 로그와 개인 스크린샷은 ignored artifacts 아래 보존했다.
기기의 기존 디버그 설정은 유지하고 마지막 화면은 메인으로 복귀했다.
