# 정원 테마와 최신 미션 브랜치 통합 · 2026-10-29

**코드 통합·아래 선별 검증·원격 게시를 완료했다.** 실제 merge는 [`78a1d29201511a78710027ea5fcd09992a655f09`](https://github.com/FriedChickenMakers/CapstoneDesign2026/commit/78a1d29201511a78710027ea5fcd09992a655f09)이며, `main`과 `codex/integrate-garden-missions-20261029` 양쪽에 일반 push한 뒤 원격 SHA 일치를 확인했다. 이후 문서 기록 커밋은 이 merge의 후손이다. 팀원 기준은 최신 `origin/main`이다.

## 통합 기준과 원본 보존

사용자는 최신 원격 브랜치를 조사하고 필요한 일반 merge, 수정, 검증, commit 및 push까지 수행하도록 명시적으로 승인했다. 시작 시 `codex/overnight-local-loop`의 작업 트리는 깨끗했고 원격과 일치했다. 보존할 미커밋 변경이 없어서 빈 커밋을 만들지 않았다.

`git fetch --all --prune` 이후 아래 SHA를 기준으로 비교했다. 시각은 Git committer 시각이며 모두 한국 표준시(+09:00)다. 브랜치 이름이나 마지막 수정 시각만으로 통합 대상을 정하지 않고, 조상 관계와 실제 파일 변경을 확인했다.

| 원본 | 전체 SHA | 마지막 커밋 시각 | 보존 방식 |
| --- | --- | --- | --- |
| 시작 작업 브랜치·원격 overnight | `e7c00111368a8bc360bdfee866cf2f4c87ae0d99` | 2026-10-29 15:47:25 | 기존 로컬·원격 브랜치 유지, 통합 merge의 부모로 포함 |
| fetch 후 원격 main | `e0234eacea99d6c3fc8dead0aa10ae0255c6b56a` | 2026-10-28 20:17:32 | 새 통합 브랜치의 출발점, 최종 결과의 조상으로 유지 |
| 최신 미션 작업 | `1e601351fe208bbb09a56faede4c6f0df830fbd3` | 2026-10-29 19:47:19 | `origin/misssion` 유지, 통합 merge의 첫 부모 |
| 로컬 main | `be160d8373d11b7365aedfd764b682d796f30e01` | 2026-10-01 01:02:09 | 로컬 main을 이동·reset하지 않고 그대로 유지 |
| fetch 전 main 추적 값 | `1ec8e76dc91c8afe487209a991b19a0656532d96` | 2026-10-29 07:11:16 | misssion의 조상으로 보존. 아래 원격 이동 관측 참조 |

위 SHA는 Git 객체와 대조했다. 복구 작업에는 짧은 이름보다 실제 통합 커밋과 부모 SHA를 먼저 확인한다.

## 모든 원격 작업 브랜치 비교

`앞/뒤`는 **해당 행의 브랜치에만 있는 커밋 수 / 비교 기준에만 있는 커밋 수**다. 현재 기준은 시작 시점 `e7c0011`, main 기준은 fetch 후 `e0234ea`로 고정했다. `origin/HEAD`는 main을 가리키는 별칭이므로 별도 작업 브랜치로 세지 않았다.

| 브랜치 | SHA | 마지막 커밋 시각 | 현재 대비 앞/뒤 | main 대비 앞/뒤 | 현재와 공통 조상 | main에 포함 | 내용과 통합 판단 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `codex/overnight-local-loop` | `e7c0011` | 10-29 15:47:25 | 0/0 | 2/4 | `e7c0011` | 아니오 | 정원 테마·화면 캡처 2커밋. 보존하여 통합 |
| `main` | `e0234ea` | 10-28 20:17:32 | 4/2 | 0/0 | `3457b7d` | 기준 | 측정 통합, 미션 기획, 식물 원본 계보 포함 |
| `misssion` | `1e60135` | 10-29 19:47:19 | 14/2 | 10/0 | `3457b7d` | 아니오 | 45개 M/P 미션·코스·기록·홈 조작·metadata. 실제 최신 기능으로 통합 |
| `codex/mission-ui-next` | `26b6063` | 10-28 20:18:49 | 5/2 | 1/0 | `3457b7d` | 아니오 | main 기준 안내 문서 1커밋. misssion에 포함되므로 별도 merge 불필요 |
| `codex/mission-personalization-next` | `26b6063` | 10-28 20:18:49 | 5/2 | 1/0 | `3457b7d` | 아니오 | 위와 동일한 준비 기준점. 새로운 개인화 구현은 없음 |
| `codex/individual-mission-ui` | `5a235e0` | 10-28 19:55:35 | 1/6 | 0/7 | `49b8439` | 예 | 미션 UI 벤치마크·기획. main에 이미 포함 |
| `codex/full-mission-personalization` | `49b8439` | 10-27 18:09:52 | 0/6 | 0/8 | `49b8439` | 예 | 복습 추천 정책 분리·병행 개발 준비. main에 이미 포함 |
| `codex/procedural-plant-generation` | `7bdf52a` | 10-12 16:49:31 | 1/41 | 0/42 | `01d7257` | 예 | 원본 생성기. 현재 계보의 동일 패치와 main 부모 관계로 이미 반영 |
| `mockup` | `696af51` | 09-29 20:25:40 | 0/55 | 0/57 | `696af51` | 예 | 초기 프로토타입. 별도 merge 불필요 |
| `unity-demo` | `94860ed` | 08-31 14:57:59 | 0/58 | 0/60 | `94860ed` | 예 | 최초 저장소 문서. 별도 merge 불필요 |
| 로컬 `main` 참고 | `be160d8` | 10-01 01:02:09 | 1/58 | 1/60 | `94860ed` | 아니오 | 고유 회의 전사 1커밋. 로컬 ref 보존, 기능 통합에서 제외 |

### 원격 main 이동과 로컬 main

이번 fetch는 기존 추적 값 `1ec8e76`에서 `e0234ea`로의 **원격 main 강제 이동**을 보고했다. 이는 원격에 이미 발생한 변화를 관찰한 것이며 이번 작업에서 force push한 결과가 아니다. 이동 이유는 커밋 이력만으로 단정하지 않는다. 기존 `1ec8e76`은 최신 `misssion`의 조상이므로 그 구현이 유실된 상태는 아니다. 게시 직전에는 원격을 다시 조회하여 이후 타인 커밋을 확인한다.

로컬 main의 고유 `be160d8`은 `docs/meeting-transcript.md`, `docs/meeting-transcript.srt`, README 링크를 추가한 회의 전사 커밋이다. 현재 기능 통합과 별개이며 원문 재공개 여부를 검토하지 않았으므로 자동으로 합치지 않는다. 로컬 main 자체와 위 SHA를 유지한다. 원격 main 갱신을 위해 이 로컬 브랜치를 reset하거나 강제 이동할 필요가 없다.

### 절차적 식물 중복 확인

원본 `7bdf52a`와 현재 계보의 `47ac8a2`는 `git patch-id --stable` 결과가 모두 `a1f6fa132fea2ecc24793d49afe36b4562fb93bd`로 같다. main의 merge `e0234ea`는 원본 식물 브랜치를 두 번째 부모로 기록하지만 tree는 첫 부모 `3f545d0`와 동일하다. 따라서 원본 생성기를 다시 덮어쓰지 않는다.

정원에 저장된 Recipe를 표시하고 미리보기·영양제·보상을 연결하는 후속 개선은 기존 통합 계보에 유지한다. 이번 최신 식물 변경은 `misssion`의 `PlantGenerator.cs`와 `GardenPlantAppearance.cs` 등에서 함께 확인한다. 새 고품질 식물 제작이나 기존 삼각형 제한 해제를 완료한 것으로 해석하지 않는다.

## 통합 흐름과 의미 충돌 처리

[최신 인수인계](MISSION_PARALLEL_HANDOFF_2026-10-27.md)의 2026-10-28 안내와 README는 통합 대상을 `main`으로 정했다. 옛 overnight 통합 지침은 당시 기록이다. 이번에는 로컬 main의 고유 커밋을 건드리지 않기 위해 원격 main에서 별도 통합 브랜치를 생성했다.

1. `origin/main=e0234ea`에서 `codex/integrate-garden-missions-20261029` 생성.
2. main의 후손인 `origin/misssion=1e60135`을 `--ff-only`로 반영.
3. `origin/codex/overnight-local-loop=e7c0011`을 `--no-ff --no-commit`으로 병합. 충돌과 자동 병합된 공통 파일을 함께 검토.
4. 필요한 연결 수정과 회귀 검증 후 merge commit 생성. privacy 검사와 원격 상태 재확인 후 통합 브랜치에 일반 push.
5. 검증한 결과가 최신 원격 main의 후손인지 확인하고 정상 fast-forward push로 main에 반영. 원격이 새로 진행됐으면 먼저 추가 변경을 검토·병합·재검증하고, force push하지 않음.

기능 브랜치들을 삭제하거나 갱신하지 않는다. `ours`/`theirs`로 공통 파일 전체를 선택하여 어느 쪽 변경을 통째로 버리지 않는다.

| 영역 | 보존·연결 기준 |
| --- | --- |
| 정원 테마 | 종이·목재 표면, 식물 장식, 버튼 상태, 글자 대비, 그래프 앞뒤 순서, 장식의 비터치·비레이아웃 속성 보존 |
| 최신 미션 | M01–M17/P01–P28, 각 단계의 실제 동작, 입력·그림·타이머·대체 경로, 과정별 기록·복습·저장 실패 복구 보존 |
| 공통 탐색 | 최신 홈·미션·내 과정 목적지와 홈 설정 진입을 유지. 미션 수행 중 탐색 숨김·닫기·뒤로·초안 보존 검증 |
| 홈·카메라 | 최신 투명 정보·제스처 확대와 성장 패널을 고려하여 테마의 표시 위치를 결합. 과거 버튼을 되살려 새 조작을 가리지 않기 |
| 저장·보상 | 기존 MBCT와 새 Focus 기록을 구분. 코스 전환, 하루/주간 제한, 중복 지급 차단, 걷기 목표 고정, 정원 Recipe·잔액 유지 |
| 개인화·측정 | 기존 복습 추천·걷기·알림의 입력/출력과 저장 귀속 유지. 새 UI에서 경로가 단절되지 않는지 별도 확인. 측정 등록·수집 경계·서면 동의 운영 보류 유지 |
| 자동 검증 | 기존 UI를 전제한 캡처/탐색 검사를 새 실제 흐름에 맞게 보완하되, 저장·보상·터치 검증의 assertion을 단순 삭제하여 통과시키지 않기 |
| 저장소 지침 | misssion의 AGENTS.md는 명시 게시 요청 때만 commit/push하도록 변경됨. 이번 요청에는 명시 승인이 있으므로 게시 가능. 해당 지침 변경을 보존 |

`mission-personalization-next`에는 새 알고리즘 구현이 없다. 이 통합은 **완전 개인화 완료**, 실제 사용자 참여 증가, 알림 효과 개선 또는 치료 효과 입증을 뜻하지 않는다. 기존 추천 정책을 보존·연결하고 관측 경로를 검증하는 범위다. 새로운 추천 대상·입력·정책 설계가 필요하면 별도로 합의한다.

### 실제 충돌과 해결 내용

Git이 표시한 텍스트 충돌은 `GardenHomePresenter.cs`의 **5개 구간**이었다. 나머지는 자동 병합되어도 화면·진입점·관측 동작이 함께 유지되는지 확인해야 하는 의미 충돌이다. 아래의 “확인 항목”은 필요한 확인 항목을 적은 것으로, 수정했다는 사실만으로 통과를 뜻하지 않는다. 실제 실행 결과는 다음 절의 최종 검증 표를 따른다.

| 파일·영역 | 양쪽 변경의 의도 | 충돌 원인 | 해결 | 최종 동작 | 확인 항목 |
| --- | --- | --- | --- | --- | --- |
| `GardenHomePresenter.cs` · 텍스트 충돌 5개 구간 | overnight는 불투명 정보판·목재/잎 표면·성장 그림을 추가. misssion은 투명 식물 정보·제스처 확대·새 Focus 상태·공유 탐색으로 홈을 개편 | 같은 홈 생성·갱신 코드를 서로 다른 정보 구조로 수정 | 5개 구간에서 최신 미션 구조를 기준으로 수동 결합. 종이 성장 패널, 높이 34의 씨앗/잎/화분/꽃 그림, 흰 글자의 식물 강조 성장 버튼, 목재 설정 버튼을 다시 연결 | 상단 식물 이름·날짜·영양제는 투명 표시하고 낮/밤 대비를 유지. 핀치/휠 확대와 회전, 하단 성장 패널, 홈·미션·내 과정 탐색 유지 | 실제 정원의 낮/밤 글자·성장 단계·설정 진입·제스처·탭 복귀·터치 판정 |
| `FigmaMissionUi.cs`, `WeekOneQuestDemo.FigmaShell.cs`, `MissionBottomNavigation.cs` · 테마 적용 | overnight는 공통 GardenUi 테마를 적용. misssion은 390×844 Figma 헬퍼와 미션별 색·중앙 삽화를 사용 | 새 렌더 경로가 기존 GardenUi 테마를 우회하여 자동 병합만으로 새 주 화면에 테마가 나타나지 않음 | 탐색·코스·기록 페이지에만 `FigmaGardenThemeScope`를 두고 종이 배경/카드, 목재 제목표·보조 버튼, 잎 강조 버튼, 선반·실제 탭 선택 상태를 연결 | 새 미션 구조와 레이아웃을 유지하면서 공통 화면에 테마 표시. 수행 화면의 고유색·삽화·입력·그림·선택 상태는 유지 | 첫 화면 제목·긴 한글·상태별 대비·아이콘 비율·장식의 비터치 속성·동일 탭 좌표 |
| `WeekOneQuestDemo.FocusHome.cs`, `.Personalization.cs`, `.cs` · 기존 복습 추천 | 기존 정책은 완료한 MBCT 활동과 선호·시간을 사용. 새 Focus 홈은 M03 우선의 추가 미션 목록을 사용 | 기존 추천/선호 코드가 남아도 새 주 화면에서는 접근할 수 없게 됨 | 내 과정에 조건부 **이전 활동 복습·추천** 진입점 추가. 기존 추천 카드·선호 설정·배운 활동 목록과 돌아가기 콜백을 전용 허브로 연결 | 과거 MBCT 학습·복습 추천을 계속 사용 가능. 새 M 추천을 임의로 개인화하거나 과거 MBCT 진도를 새 P 코스로 바꾸지 않음 | 실제 이전 완료 상태에서 허브 진입·선호 저장·후보 선택·세션 귀속·복습 보상 중복 방지 |
| `WeekOneQuestDemo.FocusHome.cs`, `.cs`, `.GrowthRecords.cs` · 정원·건강 기록 | 기존 화면은 성장·동물·건강 그래프와 재조회를 제공. 새 내 과정은 Focus 회차·단계 기록을 제공 | 모든 기록을 Focus 요약으로 보내면 기존 부가 기록과 건강 그래프가 사용자 동선에서 사라짐 | 내 과정에 **정원·건강 기록**을 항상 추가. Focus가 아닌 기록은 `ShowLegacyRecord`로 분기하고 기존 목록·상세·성장 기록의 복귀 경로를 보존 | 새 Focus 상세와 이전 기록 상세를 각각 유지. 저장 데이터나 건강 수치를 새 형식으로 변환하지 않음 | 기록 목록→이전 상세→그래프·재조회→목록, 성장·동물·건강 기록의 진입·복귀 |
| `WeekOneQuestDemo.FocusPractice.cs` · `FocusSaved` | 기존 `Apply`는 저장 성공 직후 측정 세션을 관측. 새 Focus는 입력 보존을 위해 별도 `FocusSaved`를 사용 | 새 성공 경로가 기존 즉시 관측 호출을 우회하여 주기 관측에만 의존 | 성공한 `FocusSaved` 뒤에도 `ObserveOptionalRecommendation(force:true)` 호출. 실패 시 관측하지 않음 | 저장된 시작·완료 상태를 기존 수집 경계·중복 방지 규칙으로 관측. 자유 입력·그림·기분을 새 업로드 항목으로 추가하지 않음 | 합성 등록 상태의 시작/완료·저장 실패·재시도·중복 이벤트·기존 수집 시작 경계 |
| `WeekOneQuestDemo.FigmaShell.cs`, `GardenUi.cs` · 스크롤 초기 위치 | 새 공통 페이지에 정원 테마 스크롤 표시를 연결 | 첫 레이아웃에서 Scrollbar의 기본값 0이 ScrollRect로 전달되어 기본 미션 카드 제목이 화면 위로 밀림. 실제 첫 캡처에서 확인 | `FigmaScroll`와 기존 `AddScrollIndicator`에서 ScrollRect 콜백 연결 전에 `scrollbar.SetValueWithoutNotify(1f)`로 초기 위쪽 위치 지정 | 새 페이지가 제목부터 열리고 사용자의 이후 스크롤은 그대로 동작. 캡처 직전 위치 보정으로 숨기지 않음 | 첫 레이아웃의 normalized position·본문 위치·제목 가시성, 아래로 이동 후 페이지 재진입 |
| `WeekOneQuestDemo.MissionControls.cs`, `LocalGardenState.cs` · 날짜 표시 | 최신 설정은 적용 날짜를 지속 표시하고 기존 설정 검사는 닫힌 진단의 불필요한 상태 복제를 금지 | 날짜 한 줄을 위해 전체 세션·그림·Recipe를 포함한 `Snapshot`을 매번 깊은 복사 | 저장이 확정된 `LastCycleId`만 잠금 안에서 반환하는 읽기 전용 `AppliedCycleId` 추가, `MissionDateLabel`에 연결 | 날짜 넘김/저장 실패 의미를 유지하면서 반복 복제 제거. 저장 필드·형식은 추가하지 않음 | 닫힌 진단 SnapshotReads 불변, Focus 날짜 넘김·초기화·실패 재시도 |
| `WeekOneQuestDemo.Personalization.cs` · 설정 뒤로가기 | 기존 선호 설정과 새 복습 허브를 모두 보존 | 기존 설정→새 허브→설정 재진입 때 오래된 설정 이력이 허브 뒤에 남아 Back이 허브를 건너뜀 | 허브로 돌아갈 때 이전 `recommendation-preferences` 화면 이력만 제거. 입력·선호·세션은 삭제하지 않음 | 설정을 다시 열어도 Back은 복습 허브로 돌아옴 | 모든 기간의 선호 저장·재열기·뒤로가기·보상 불변 |
| `WeekOneQuestDemo.cs`, `.GrowthRecords.cs`, `.FocusHome.cs` · 새 미션과 성장 연결 | 새 Focus 코스 진도/제목과 기존 MBCT 진도/성장 일지를 각각 보존 | 공유 정원 가꾸기에서 MBCT 진도만 읽어 Focus 완료를 0으로 표시하고, 성장 목록은 새 세션의 제목 snapshot 대신 옛 카탈로그를 조회 | shop/growth/placement는 Focus 진도 사용, legacy 화면은 MBCT 진도 유지. 기록 제목은 Focus 버전·저장 snapshot과 legacy 카탈로그를 명시 분기 | 새 미션 완료 수와 원래 제목이 성장 화면에 표시됨. 보상·Recipe·성장 분류·저장값은 변경하지 않음 | Focus 1회 완료→1/12, 성장 연결의 새/구버전 제목 구별, 기존 성장 취소/중복 차감 회귀 |
| `FocusMissionValidation`, 기존 로컬 루프·화면 자동화 | 기존 검사는 MBCT 화면 이름·버튼을 전제. 새 공개 진입점은 Focus 화면으로 전환 | 구형 검사를 새 화면에 그대로 실행하면 잘못된 버튼을 찾거나 기존 계약 검증을 건너뛰게 됨 | 기존 MBCT 회귀는 보존된 legacy 경로에서 검사하고, 새 Focus 전체 미션·코스·복습 연결·실제 버튼/레이캐스트를 별도로 검사. 실패 보고서는 실행 실패로도 전파 | 구형 저장·보상 회귀와 새 제품 동선을 구분하여 확인. assertion을 삭제하거나 이전 커밋의 결과를 재사용하여 통과 처리하지 않음 | 실제 병합 후보의 unit·Unity·화면 결과 및 FAIL/SKIP·검증 한계 기록 |

**의도적으로 복원하지 않은 이전 화면:** 불투명 홈 정보판, `보기 +` 버튼과 그 아래 확대/시점 버튼 줄, 숨겨진 `TodayMissionCard` 및 기존 활동 요약 부품. 이들은 최신 투명 정원 정보·제스처 조작·미션 탭 상태 배지와 목적이 충돌한다. 관련 테마 자산이나 기존 Git 커밋을 삭제하는 대신 새 구조의 성장 패널·성장 버튼·설정·공유 탐색에 시각 요소를 연결했다. 따라서 이전 41항목 캡처를 통합 후 화면과 동일하다고 설명하지 않는다.

### 작업별 영향과 새 연결점

| 작업 | 통합 후 영향을 받는 부분 | 새로 확인할 연결점·공통 파일 | 유지하거나 보류하는 범위 |
| --- | --- | --- | --- |
| 개별 미션 UI | 45개 M/P, 새 2·4·8주 과정, 집중 수행·기록은 최신 구현 유지. 공통 페이지 외형과 내 과정의 부가 기록 진입점 추가 | `FigmaMissionUi`, `FigmaShell`, `MissionBottomNavigation`, `FocusHome`는 공유 화면 파일. 테마 선택과 버튼 콜백을 분리하여 후속 변경 | 수행 화면의 중앙 그림·고유색·키보드 회피·낙서·타이머를 유지. 전체 수행 화면을 목재 테마로 다시 디자인하는 작업은 별도 |
| 미션 개인화 | 이전 MBCT 복습 추천과 선호 설정을 다시 도달 가능한 동선으로 연결 | `ShowFocusCourse` → `ShowLearnedPracticeRecommendations` → 기존 `AddOptionalRecommendation` / `ShowRecommendationPreferences` / `ShowLegacyFree`; 추천 선택은 기존 `SelectMission`과 저장 계약 사용 | 새 M 추천은 진행 중 추가 미션 우선, M03 우선, 이후 미완료 목록 순서 그대로. 완전 개인화·새 입력·새 정책·서버 운영은 이번에 확정하지 않음 |
| 식물·정원 | 저장 Recipe와 성장·미리보기·보상 연결, 새 종 이름 예측·제스처 확대를 보존. 성장 패널에는 테마 그림 재결합 | `GardenHomePresenter`가 UI 공통 충돌 지점. `PlantGenerator.SpeciesFor`, `GardenPlantAppearance`의 기존 기록 보존·초기화 후 외형 원장은 최신 기능 사용 | 고품질 식물 메시·사진 텍스처 적용·삼각형 제한 해제는 미완료. 사용자가 제공할 UI 식물/목재 사진도 아직 반영하지 않음 |
| 참여·성장·건강 기록 | Focus 회차 기록과 이전 활동 상세·성장/동물/건강 화면을 모두 접근 가능하게 유지 | `ShowFocusCourse` → `ShowGardenJournal` → 기존 목록; `ShowFocusRecord`는 non-Focus 세션을 `ShowLegacyRecord`로 분기 | 새 건강 분석·원문 업로드·기존 기록 재분류를 추가하지 않음. 실제 사용자 데이터로 검증하지 않음 |
| QA·베타 측정 | 새 Focus 저장 성공도 기존 관측 경로에 즉시 연결 | `FocusSaved` → `ObserveOptionalRecommendation(force:true)` → 기존 `RecordSession` / `RecordMeasuredSession`; 앱 시작 시 등록 확인 경로 유지 | 서면 동의·운영자 등록 방식 유지. 실제 참가자 등록과 공개 서버 운영은 기존 보류 상태. 이번 통합은 알림 반응시간 개선 효과의 증명이 아님 |
| 공통 검증·협업 | 구형 저장 회귀와 새 Focus 동선을 함께 검증. 새 Linux 전체 미션 검사 실행 경로 보강 | `scripts/qa.py`, `test-all-missions.sh`, Unity 실행 잠금·새 결과 디렉터리·실패 exit 처리 | 두 작업 브랜치나 로컬 main을 덮어쓰지 않음. 각 팀원은 검증된 통합 main을 자신의 브랜치로 일반 merge |

공통 저장·API 확인: `misssion` 원본의 Focus v1/v2 세션, 회차/코스 구분, 단계 답·시각·그림, `PreviewDayOffset`, 초기화 후 외형 원장과 Editor 저장 분리를 그대로 통합했다. 이번 충돌 연결 수정은 새 직렬화 필드를 만들지 않으며 `AppliedCycleId`는 읽기 전용 쿼리다. `server/`, 네이티브 알림·측정, `PersonalizationClient`/정책 모델, API payload는 main 대비 변경되지 않았다. 미션 UI와 개인화 담당자는 `LocalGardenState`의 추가 getter와 `FocusSaved` 관측 진입점을 공유 파일 변경으로 확인한다.

통합 연결 수정에서 외부 에셋·개인정보·실제 사용자 기록을 새로 추가하지 않았다. 최신 기능 브랜치에 포함된 기존 Figma 미션 자산과 원본 SVG/변환 PNG, 기존 무료 자산의 출처·라이선스 표기는 보존한다. 미션별 추천의 임상적 의미, 새 커리큘럼 근거, 사용자 타겟과 실제 효과 평가처럼 이미 미정인 요구사항은 이번 merge로 합의된 것으로 취급하지 않는다.

## 검증·게시 기록

Unity는 기존 라이선스가 있는 OS 사용자 `codex`, `HOME="/home/codex"`로 실행했고 같은 프로젝트의 Editor 잠금을 사용했다. root 실행·라이선스 복사/변경은 없었다. 모든 시험 기록·캡처는 합성 데이터이며 ignored `artifacts/integration-20261029/`에 보존한다.

| 항목 | 결과 | 증거·확인 범위 |
| --- | --- | --- |
| 필수 unit | **20/20 PASS**, FAIL/SKIP 0 | `unit-final/results.json`. Focus 상태 1,640·전체 미션 2,559 확인 포함. 개인화·걷기·알림·측정·서버·식물·저장 계약 |
| 프로젝트 연결·컴파일 | **PASS** | `release-checks/project-bindings`. 최종 후속 UI 코드도 아래 Unity/Android 실행에서 재컴파일 |
| EditMode | **161/161 PASS** | `release-checks/editmode.xml`. 표면 재적용·입력 비차단 등 기존 컴포넌트 검사 |
| 로컬 루프 | **39/39 PASS** | `review-checks/local-loop/local-loop-validation/results.json`. 기존 MBCT·성장·건강·추천 설정·저장 실패·취소·보상·뒤로가기 |
| 실제 Play 저장·복원 | **fresh/restore PASS** | `review-checks/play-save-restore`. 새 내비게이션·실제 입력 대상/제스처·합성 파일 저장·다음 실행 복원 |
| 개인화 네트워크 | **PASS** | `unity-components/personalization-network`. 실제 UnityWebRequest와 격리 API의 등록·업로드·철회·삭제 및 정리. Android 알림 실측을 뜻하지 않음 |
| Focus 통합 | **13/13 PASS**, 버튼 701·레이캐스트 704 | `review-checks/focus-missions/focus-validation/report.json`. 45종 수행·저장/재열기, 2/4/8주, 입력·낙서·분기·걷기, legacy 연결, 초기 스크롤, 새 진도/저장 제목 |
| 실제 씬 화면 | **44장 PASS** | `scene-reviewed`: 11개 상태 × 900×1950/900×1600/1200×2000/1200×800. 장식 비차단·렌더 전후 영구 상태 불변 |
| Focus UI 캡처 | **147장 생성** | 실제 UI를 격리한 합성 fixture. 실제 씬 44장과 합계 191장. 전체 화면의 사람 전수검토·텍스트 넘침 0건을 주장하지 않음 |
| Android IL2CPP 빌드·APK 서명 | **Success / PASS** | `android-final/build/capstone-mockup.apk`, 188,199,907 bytes. `BEE_BUILD_THREADS=2`. 개발용 APK, 실제 기기 설치 없음 |
| privacy·diff | **PASS** | 공개 대상 1,685개 파일의 worktree·정확한 index 검사와 `git diff --check` 통과. 각 push 전 worktree 재검사. 원시 로그·화면·APK 제외, PNG 100개에 텍스트/EXIF 메타데이터 없음 확인 |
| GitHub push CI | **양쪽 success** | 통합 커밋 `78a1d29`의 [main 실행](https://github.com/FriedChickenMakers/CapstoneDesign2026/actions/runs/40882361486)과 [통합 브랜치 실행](https://github.com/FriedChickenMakers/CapstoneDesign2026/actions/runs/40882297180). 독립 Linux runner의 unit·privacy이며 Unity 검증은 위 로컬 결과로 구분 |
| merge SHA / 원격 게시 | **`78a1d29` 게시·일치 확인** | 첫 부모 `1e60135`, 두 번째 부모 `e7c0011`. 통합 브랜치 신규 일반 push, main은 `e0234ea`에서 fast-forward. 로컬 main `be160d8`과 원본 기능 브랜치 유지 |

검증 JSON의 `commit=1e60135`, `workingTreeDirty=true`는 merge commit 생성 전 통합 작업 트리를 실행했기 때문이다. 원본 misssion만 검사한 결과가 아니다. 검증 후 생성 캐시 정리와 문서 갱신을 거쳐 커밋한 통합 tree는 `383deb44eab8d465c6404e43c31eec2c43df6f72`이며, 이후 게시 기록 갱신은 문서만 변경한다.

게시 명령은 `git push -u origin codex/integrate-garden-missions-20261029`, `git push origin HEAD:main`이었다. 게시 전 원격을 재조회하고 main의 조상 관계를 확인했으며, 게시 후 `git ls-remote`로 양쪽 `78a1d29`를 확인했다. force push·브랜치 삭제·타인 작업 브랜치 갱신은 수행하지 않았다. 상세 명령 로그와 최종 검증·원격 확인 기록은 비공개 `artifacts/integration-20261029/workflow-record.json` 및 `push-*.log`에 보관한다.

APK SHA-256: `ee77fe056364c58896971cbca1217c2330fb6c6b368c8b6d8541619b4049500e`.

직접 검토한 대표 화면에서 공통 테마, 새 미션 그림·감정/완료/저장 실패, 낮/밤 정원, 성장·설정·건강 그래프를 확인했다. 최초 스크롤의 제목 잘림을 발견한 뒤 공통/Figma 스크롤 초기값을 수정하고 새 결과 디렉터리에서 재캡처했다. 가로 화면은 기존 세로 비율 유지 구조이며 실제 기기 가로 UX를 최적화했다는 뜻은 아니다.

초기 실패도 보존한다. `legacy-initial`은 23 PASS/16 FAIL(구 화면 기대값과 접근 단절), `unity-loop-final`은 37/2, `local-loop-recheck`는 38/1이었다. 실제 경로 복구·테스트 기준 이식·반복 Snapshot 제거·설정 Back 이력을 수정한 최종 `review-checks`가 39/39다. 과거 실패나 단위 성공을 Unity 성공으로 대체하지 않았다. Editor에는 오디오 장치/검색 초기화 등의 환경 경고가 남을 수 있으며 각 assertion·결과 파일·종료 상태로 판정했다.

원본 PNG를 보여주는 비공개 검토 자료는 `review/index.html`, `integration-review.zip`이다. 생성된 폰트 캐시와 Android 기본 importer metadata만 별도 patch로 보존한 뒤 원본으로 되돌렸으며, 팀원이 커밋한 GUID·자산을 재생성하지 않았다.

이번 실행은 **전체 `visual` tier 통과가 아니다.** 옛 대량 캡처 `tabs`/`supplemental-ui`는 legacy 진입점 명시화가 필요하고, `demo-polish`의 제거된 보기 버튼, `lighting`의 옛 TMP 탭/불투명 정보판, `procedural-scene`의 최대 확대 fit 기준은 새 제스처·투명 홈에 맞춰 갱신해야 한다. `guided-ui`는 기존 MBCT 48회 회귀이며 새 Focus 45종 검사의 대체가 아니다. 그 도구들을 이번에 실행해 통과했다고 기록하지 않는다. 새 흐름은 위의 Focus·실제 씬·Play 검증으로 확인했다.

재현 시 기존 결과 디렉터리를 덮어쓰지 않는다. 라이선스가 설정된 계정 환경에서:

```bash
python3 scripts/qa.py --tier unit --output artifacts/integration-unit-new
python3 scripts/qa.py --tier unity --only project-bindings --only local-loop --only editmode --only play-save-restore --only personalization-network --output artifacts/integration-unity-new
python3 scripts/qa.py --tier visual --only focus-missions --output artifacts/integration-focus-new
CAPSTONE_ARTIFACTS="$PWD/artifacts/integration-android-new" BEE_BUILD_THREADS=2 bash scripts/build-android.sh
```

### 남은 조율·검증

- 실제 S24/A7의 터치·키보드·백그라운드 복귀·메모리/발열·알림 게시/클릭 시각은 별도 기기 QA다. 이번 작업은 기기에 접속하거나 앱 데이터·기기 설정을 변경하지 않는다.
- 설정의 날짜 넘김은 합성 QA용 `PreviewDayOffset`을 저장한다. 날짜를 앞당긴 세션은 미래 시각 검사에서 측정 이벤트가 거부될 수 있으므로 실제 참가자 측정은 offset 0인 별도 등록 환경에서 진행한다. 기존 저장본을 임의로 초기화하여 맞추지 않는다.
- Focus M 추천을 기존 MBCT 복습 개인화와 합칠 정책·후보·설명 계약은 미정이다. 새 P 커리큘럼의 근거·타겟·전문가 검토와 베타 운영 보류도 유지한다.
- 41항목 구형 캡처와 통합본의 구조는 다르다. 실물 식물/목재 사진, 수행 화면 전체의 게임 테마, 고품질 식물·8천 제한 조정은 이번 통합의 완료 범위가 아니다.

## 복구와 후속 작업

통합 후 팀원은 먼저 자신의 미커밋 변경을 보존하고 다음처럼 새 main을 일반 merge한다. `misssion`, 두 `*-next`, 기존 overnight 모두 원본 ref를 유지했으므로 후속 작업에는 이 추가 merge가 필요하다.

```bash
git fetch origin
git switch <자신의-작업-브랜치>
git merge origin/main
```

새 작업은 `origin/main`에서 새 `codex/` 브랜치를 만들면 된다. 담당 UI는 `FigmaMissionUi`/`FigmaShell`/`FocusHome`/`MissionBottomNavigation`, 개인화는 `OptionalActivityRecommendation`와 새 복습 허브 연결, 식물 담당은 `GardenHomePresenter`·저장 Recipe/ArchivedCredits를 먼저 대조한다. 공통 파일은 한 담당자가 연결을 관리한다. 진행 중인 타인 브랜치를 reset하거나 force push하지 않는다. 로컬 main에 고유 커밋이 있는 이 작업공간에서는 별도 새 브랜치에서 원격 main을 사용한다.

공개 이후 되돌리기는 새 revert commit으로 수행한다. **이 통합 merge의 첫 부모는 최신 misssion, 두 번째 부모는 overnight**이므로, 실제 부모를 `git show --no-patch --format='%H %P' <merge-sha>`로 확인한 뒤 `git revert -m 1 <merge-sha>`를 사용하면 해당 merge가 더한 테마·연결 변경을 첫 부모 기준으로 되돌릴 수 있다. 후속 수정 커밋이 있으면 그 의존 관계도 함께 검토한다. 이것만으로 새 미션 전체가 통합 전 main `e0234ea` 상태로 돌아가는 것은 아니다.

새 Focus 기능은 세션 버전, 과정 구분, 단계별 입력·확인·시간·그림 등 저장 내용을 확장한다. 구버전 앱이 새 저장본의 모든 필드를 보존하여 다시 쓸 수 있다고 가정하지 않는다. 코드 되돌리기와 사용자 저장 복구는 별도 작업이다. 이전 앱으로 실행하기 전 합성 저장본으로 읽기·재저장 호환을 확인하고, 실제 데이터는 백업·보존한 상태에서 필요한 복구 경로를 결정한다. 앱 데이터 삭제나 과거 저장본의 무조건 덮어쓰기는 복구 방법으로 사용하지 않는다.
