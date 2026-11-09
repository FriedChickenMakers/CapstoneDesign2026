# 2026-11-09 통합 및 QA 배포 준비 현황

**현재 후보 `.4`/2026110906은 빌드·오프라인 검사, A7 인플레이스 업데이트·등록/정원 보존, 실제 화면 조작과 새 버전 HTTPS 사건 수신을 확인했다.** 앞선 `.3`/2026110905는 HTTPS 전송 성공과 별개로 사용자가 하단 메뉴 회귀를 발견하여 **UI HOLD·대체됨**으로 표시한다. UI 수정 commit **[`37a4076`](https://github.com/FriedChickenMakers/CapstoneDesign2026/commit/37a4076500dc7fdde7b8f979b31a4a658d7a569a)**도 일반 push하고 원격 SHA 일치를 확인했다. 최종 수정 소스의 Unity 회귀 **15/15, 1,283 assertions, 버튼 84회·raycast 84회**가 통과했다.

앞서 사용자의 명시적 게시 승인에 따라 `misssion`의 일반 merge commit **[`17c1978`](https://github.com/FriedChickenMakers/CapstoneDesign2026/commit/17c19788d7d82281e5cdffef886c353245b81beb)**을 push하고 원격 HEAD 일치를 확인했다. 이전 동결 소스의 host **39/39**, Unity HTTP 수집·철회 **클라이언트 37개 + 서버 2개**, **7종 통합 UI/Play**, 표시 보완 후 **12/12(256 assertions)** 및 이전 후보의 A7 보존 검사는 각각 당시 버전의 근거다. 이 성공을 905의 시각적 회귀 부재나 906의 실기기 검증 완료로 해석하지 않는다.

외부 서버는 지정 계정으로 접속하여 전용 디렉터리와 API/HTTPS 서비스를 구성했다. 외부 HTTPS 합성 API **18/18**, 전용 API 재시작·SQLite 백업/상태 보존 **10/10**, 원격 서버 단위 **58/58**이 통과했다. 904·905·906의 A7 HTTPS 결과는 7절에 별도로 기록하며, 서버 검사만으로 단말 전송 성공을 대신 주장하지 않는다. 실제 참가자 A/B 평가는 미수행이다.

이 문서는 브랜치 통합, 회의 담당 범위, 계약 변경과 회귀 검증, 현재 배포 운영의 제약을 다룬다. 실제 참가자 기록·기기 식별자·내부 문서 URL·서버 주소·자격 증명은 포함하지 않는다.

## 1. 통합 기준과 보존한 브랜치

최신 작업은 이름이나 시각만으로 선택하지 않았다. fetch 후 커밋 그래프, 포함 관계, 소스 및 [11월 7일 통합 결정](GARDEN_BRANCH_INTEGRATION_20261107.md)을 대조했다. `combine-garden`은 이전 guided UI와 texture 작업을 선택적으로 결합한 별도 구현이므로, 단순히 texture의 모든 동작을 다시 덮어넣지 않는다.

아래 수치는 **통합 직전 HEAD `255fb98`에만 있는 커밋 / 비교 ref에만 있는 커밋**이다. 로컬 merge가 끝난 뒤의 수치가 아니다.

| 비교 대상 | 확인 SHA | HEAD에만 / 대상에만 | 처리 |
| --- | --- | ---: | --- |
| `misssion` 로컬 통합 기준, `origin/texture-rework` | `255fb987b4a4f56768d0d626cefff9262f74d6c9` | 0 / 0 | 기존 `misssion`에서 texture의 후속 2개 커밋을 fast-forward로 보존한 기준 |
| `origin/misssion` | `8b5d963f03709e236675397ce1f8c7ecfcbefd42` | 2 / 0 | 통합 전 원격 기준 보존. 이후 `17c1978`로 일반 push |
| `origin/main` | `b7332ee6eaf5c8db51ddc561aa88b2e3d26935dd` | 11 / 0 | 현재 작업과 combine 양쪽의 공통 조상. 별도 중복 merge 불필요 |
| 로컬 `main` | `be160d8373d11b7365aedfd764b682d796f30e01` | 87 / 1 | 고유 커밋 보존. reset·merge·push하지 않음 |
| `origin/codex/combine-garden-20261107` | `3ac0c8078735b49e0983dff6de4c6e537660f8f0` | 11 / 3 | 최신 guided·정원·일지·초기화 계약을 일반 merge로 통합 |
| `origin/codex/integrate-garden-missions-20261029` | `a05634cbf6f33161dfb3a2a12b294b68356cd2ab` | 11 / 1 | combine에 포함된 이전 기준. 따로 중복 merge하지 않음 |
| `origin/codex/camera-mission-validation` | `e1954c1155eb6b9f6c569fd45feab919254bb760` | 9 / 0 | texture 기준에 이미 포함. 사진 검증 연결을 통합 과정에서 보존 |
| `origin/codex/procedural-plant-generation` | `644fec06960c31226d3364c2082412c04ed2baa5` | 7 / 0 | texture 기준에 이미 포함. 형태·기억 잎 변경을 최신 정원과 결합 |

사용자 후속 결정은 **담당 b·c·h에서 확정한 계약은 기존 작업을 채택하고 나머지는 incoming을 채택**하는 것이다. 이를 content v5로 합쳤으며, 아래 충돌 표에서 실제 유지·결합 결과를 구분한다.

복구 및 비교 근거는 ignored `artifacts/qa-release-20261109/branches-before.json`과 각 원본 SHA다. 이 JSON의 ahead/behind 기준은 최초 `misssion@8b5d963`이므로 위 HEAD 기준 표와 혼동하지 않는다. 마지막 fetch의 `artifacts/qa-release-20261109/branches-final-fetch.json`에서도 위 원격 SHA가 유지되어 추가 원격 커밋은 없었다. 게시 직전 fetch에도 추가 원격 커밋은 없었다. `17c1978`의 두 부모는 `255fb98`와 `3ac0c80`이며 일반 push 후 원격 SHA 일치를 확인했다. 기록은 `artifacts/qa-release-20261109/publication/merge-push-result.json`에 있다. force push·브랜치 삭제·로컬 main 덮어쓰기는 수행하지 않았다.

## 2. 실제 텍스트 충돌과 해결

**48개 텍스트 경로에서 충돌이 발생했다.** `.meta` 2개가 포함된다. 원본 3-way 자료는 `artifacts/qa-release-20261109/conflicts/`, 전체 목록은 해당 디렉터리의 `files.json`에 보존했다. 파일 전체를 일괄 ours/theirs로 고르는 방식으로 해결하지 않았다. 의미를 대조한 결과 일부 파일이 incoming과 동일해진 경우도 아래에 명시한다.

| 파일·영역 | 양쪽 변경과 충돌 원인 | 해결 및 최종 동작 | 검증·주의 |
| --- | --- | --- | --- |
| `FocusMissionCatalog.cs`, `LocalGardenState.cs` | 양쪽이 서로 다른 실습 구조에 Focus v3를 사용함 | 신규 목적·허용범위 계약은 v5. incoming guided v4는 `AllV4`, texture v3는 `FocusMissionCatalog.TextureHistory.cs`의 역사 정의로 분리. schema1 guided는 v4, schema2 texture는 v3로 정확히 이행 | 원본 3개 브랜치가 생성한 합성 저장파일과 실패/재시도 검사. 기존 v4 P25의 빈 완료 응답과 M10 입력을 신규 v5 필수 선택으로 재해석하지 않음 |
| `LocalGardenState.cs`, `WeeklyPlantState.cs`, `GardenPlantAppearance.cs` | 부분 성장·영양제/옛 성장 장부와 완료 성장·코인·정원 컬렉션 계약이 충돌 | 최신 완료 성장·하루 기본/추가 한도·별도 감정 기록·자동 사진 보관 유지. 기존 식물 recipe/mesh/목표와 과거 부분 성장 영수증은 보존하되 새 부분 성장은 지급하지 않음 | CombinedPlants, FocusState, 사진·감정·일일 한도 검사. `GardenStateService`가 단일 저장 경계 |
| `LocalGardenState.cs` 저장·삭제 | 기존 privacy revision/삭제 백업 보호와 신규 전체 초기화 journal이 같은 저장 경로를 변경 | journal의 원자적 초기화와 기존 삭제 시 `.bak`에 이전 개인 기록을 남기지 않는 처리를 결합. 기존 경험 삭제 tombstone 및 추천 대상 일치 검증도 유지 | 실제 파일 실패·재시작 검사. 일지 기록 삭제와 식물 소유 감정 메모 보존 범위를 구분 |
| `FocusPracticeInteractions.cs`, `WeekOneQuestDemo.AllFocusPractice.cs`, `FocusPractice.cs` | 옛 조절 UI/타이머와 guided·basic slide·성장 연출 흐름이 충돌 | 최신 실습 화면·오디오 위치 복원을 유지하고, 사용자의 담당 정책 우선 지시에 따라 실제 타이머 시작 후 `너무 길어요`→일시정지→여기까지/취소 및 확인된 실제 시간에 따른 다음 추천을 결합. 미완료는 무보상이며 정상 필수 단계 완료만 보상 | Unity 추가 회귀에서 팝업 pause/cancel, 실제 시간, 미루기 무보상, M10 선택, v4 타이머·v5 목적 copy 통과. 7종 통합 UI는 보완 전 동결 소스에서 PASS. P21/P28 문구와 P19/P26→P02 역할 표시를 포함한 최종 12개 회귀도 PASS |
| `CameraMissionPolicy.cs`, `PhotoEvidence.cs` | 카메라 대상 버전과 보상 용어/완료 순서 변경 | v2/v3/v4 과거 및 현재 v5 대상 판정을 지원. 선택 촬영·건너뛰기·하루 1코인 보너스를 최신 완료 흐름에 연결 | 카메라/추천 도메인 326 assertions. 객체 검출은 실제 활동 수행의 증명이 아님 |
| `GardenThemeSurface.cs`, `FigmaShell.cs`, `FocusMobileLayout.cs`, `MissionBottomNavigation.cs` | 기존 목재·잎 테마와 새 톤/대비/인셋·탭 배치가 같은 영역을 수정 | texture 소재와 최신 제목 대비·눌림 pivot 처리를 유지. 사용자 화면 검토에서 하단바 치수 혼합을 발견하여 직전 로컬의 108 폭·12 간격·12 외곽 여백·둥근 선반으로 복원 | 이번 실행의 화면·raycast 검사 사용. 7종 통합 UI는 보완 전 소스에서 PASS. 과거 스크린샷을 새 문구 검증으로 사용하지 않음 |
| `GardenHomePresenter*.cs`, `GardenMemoryPicker.cs`, `PlantMemoryGeometry.cs` 및 `.meta` | 기존 선택 식물·포인터 소유와 새 canonical plant·기억 잎 paging/연출 경로 충돌 | 공용 Camera→RenderTexture→RawImage를 유지하고 현재 식물/미리보기/앨범·기록 선택을 결합. 체적 잎에 최신 잎맥 표시 추가. `.meta`는 실제 incoming 참조에 맞는 GUID 유지 | 기억 잎/기하 검사. old pulse는 최신 성장 연출로 대체. 보완 전 동결 소스의 combined-memory UI/기하 검사 PASS; 실제 사용자 장시간 조작 평가는 별도 |
| `WeekOneQuestDemo.cs`, `MissionControls.cs`, `GrowthRecords.cs`, `FocusHome.cs` | 기존 사진 정리·추천 노출과 신규 초기화·일지·일일 진행 화면 충돌 | 최신 안전한 로드/초기화·활동 일지·일일 제한을 적용하고 성공한 열기/초기화 뒤 사진 prune·삭제 및 추천 노출 귀속을 함께 보존 | 저장·일지·추천 검사. 초기화 도중 옛 callback은 복원된 새 세대를 변경하면 안 됨 |
| Android `ActivityNotifications.kt`, `AndroidPlatformBridge.kt`, `MissionPhotoActivity.kt`, `MissionPhotoController.kt`; 관련 Robolectric 2개 | 기존 알림 학습·시간 설정·사진 처리에 새 reset epoch가 추가됨 | 학습/설정/선택 촬영을 보존하면서 `LocalResetEpoch`와 늦은 worker·Intent·촬영 callback 차단을 결합. 25 hunk를 대조한 결과 당시 incoming 구현과 일치 | native host 31/31. 실제 단말 알림·촬영·업데이트는 별도 |
| Editor 검사 4개, `scripts/qa.py`, 테스트 실행 스크립트 및 충돌한 도메인 테스트 | 신규 소스·일일 한도·보상·schema3와 이전 컴파일 목록/기대값이 달라짐 | 공통 도메인 소스 manifest를 도입하고 Linux/PowerShell 경로를 맞춤. 최신 정책으로 기대값을 갱신하면서 실패 원자성·재시작·중복 방지 검증 유지 | 통합 1차 실패 기록 보존. 실패 수정 후 개별 재실행 결과는 아래 표 참고 |

Core 6개 파일의 47 hunk 판단은 `artifacts/qa-release-20261109/host/core-followup/merge-decisions.json`, UI/검사 쪽 결정은 `artifacts/qa-release-20261109/root-conflict-resolutions.json`에 기록했다. 모든 표의 검증이 최종 APK에서 끝났다는 의미는 아니다.

## 3. 자동 merge 뒤 발견한 동작·검증 충돌

| 문제 | 구분 | 실제 조치 |
| --- | --- | --- |
| 하단바 버튼 폭 122와 기존 108 기준 아이콘/글자 좌표가 섞임 | 병합 해결 뒤 실제 화면 회귀 | 중앙이 7pt 왼쪽으로 밀리고 간격이 0이 됨. `37a4076`에서 기존 108 폭·12 간격·외곽 여백을 복원하고 페이지/사진첩의 하단 예약 영역을 96으로 통일. 최신 기록 탭 라우팅과 눌림 pivot은 유지. 기존 화면 간 동일성 검사만으로는 놓친 문제이므로 실제 중심·간격 검사 추가. 906 A7 재촬영에서 중앙·간격 복원과 실제 탐색 조작 확인 |
| 비타이머 추가 활동에 별도 실습 시작 버튼 중복 | 사용자 화면 확인 중 발견한 포팅 회귀 | M03에서는 완료 버튼·힌트와 겹치고 M04에서는 불필요한 동작이 추가됨. 중복 버튼만 제거하고 session 시작 및 실제 타이머/입력/그림/오디오 시작 기록을 유지. 관측하지 않은 단계 시작 시각은 빈 값 유지 |
| 코스 선택 화면의 개발용 P/M 런처와 복원한 선반이 겹침 | 독립 UI 리뷰에서 발견한 하단 예약 영역 누락 | 기존 중심 y=108·높이 44의 하단 86이 선반 상단 96보다 낮아 10pt 겹침. `ReservedHeight + 24`로 이동하여 런처 하단 98을 확보. Editor/Development에서만 나타나는 버튼이며 최종 15개 회귀에 실제 경계·raycast 검사 포함 |
| 과거 M10 선택형 계획은 읽히지만 현재 텍스트 입력으로 수정하면 구조화 필드와 Value가 달라 저장 실패 | 실제 호환 결함 | `LocalGardenState.UpdateFocusAnswer`에서 **명시적 Value 변경 시에만** 과거 `PlanActivity/PlanTimePeriod/PlanNote`를 비움. 이행·동일값 저장은 원본 유지. 저장 실패 시 양쪽 모두 유지. 원본 `255fb98` 코드가 생성한 두 종류의 draft로 재현·재검증 |
| 이전 조절/주간 식물/화면 audit 코드가 신규 UI에 없는 필드·API를 참조 | 자동 merge 뒤 소스 계약 불일치 | 사용하지 않는 주간 식물/화면 audit 진입점은 정리하고 원본은 ignored `artifacts/qa-release-20261109/retired-source/`에 보존. 사용자 담당 범위로 확정된 조절 정책은 최신 guided/basic slide 연결로 포팅하고 contracts/adjustments를 release gate에 다시 포함 |
| FullReset 테스트가 열린 `FileStream`으로 삭제 실패를 기대함 | 테스트 플랫폼 의존 | Unix에서는 unlink가 가능함을 incoming 원본에서도 재현. `reset.tmp` 디렉터리로 실제 파일 삭제 실패를 만들고, fresh primary 기록 후 pending journal 유지→재시작→재시도를 검증. runtime 우회·강제 PASS·skip 없음 |
| 5코인·복습 성장·하루 추가 활동 반복·수동 보관 후 빈 화분·새 식물 12단계 기대 | 구 정책 테스트 | 50코인·복습 무성장/무보상·하루 추가 1회·완료 식물 계속 표시·새 식물 21단계로 수정. 과거 영수증/식물·메모 보존, 동시 재시도, 손상된 primary 복구 거부 등 기존 보장은 유지 |
| 일시적 연구 등록 읽기/queue 초기화 실패를 철회로 취급해 로그를 지울 수 있음 | 로그 보존 결함 | 수집 상태를 일시 중단하고 3초 재시도. 실제 철회/초기화와 구분하여 기존 queue를 보존 |
| 추천 노출 저장 실패 또는 재시작 뒤 최초 노출 귀속이 달라질 수 있음 | 측정 신뢰성 결함 | 같은 decision ID·최초 시각으로 재시도하고 저장된 snapshot에서 복구 |
| 잘못 앞선 기기 시계의 이벤트가 정상 배치 전송까지 막음 | 측정 배치 결함 | 미래 이벤트 원본은 보존하며 전송 보류. 정상 이벤트는 전송하고 미전송분이 있으면 completeness를 0으로 유지 |

마지막 세 항목은 native/개인화 담당의 독립 수정 및 해당 검사 결과를 반영했다. 이후 Opus 검토로 health timeout/cancel 구분·busy 해제·reset 세대 차단, reset 중 foreground-service 시작 차단, 영구 거부/일시 실패를 구분한 노출 재시도(backoff·횟수 한도), 시계 복구 때 미래 behavior 행을 지우지 않는 보류 처리를 추가했다. 현행 서버 통신·Unity UI·실제 기기에서 전체 흐름이 확인됐다는 주장으로 확대하지 않는다.

### 하단 메뉴 회귀의 전후 기준과 독립 검토

| 코드 기준 | 버튼·선반 배치 | 아이콘·글자 중앙 |
| --- | --- | --- |
| 마지막 로컬 `255fb98` | 버튼 폭 108pt, 버튼 간격 12pt. 선반 366×84pt, 좌우·하단 12pt 여백, 둥근 코너 | 버튼 안 x=54pt에 아이콘·글자 중앙 일치 |
| 통합 `17c1978`, 905 후보 | 버튼 폭 122pt, 간격 0. 전체 폭 390pt의 각진 선반 | 버튼 중앙 61pt에 이전 중앙 54pt가 남아 **7 디자인 pt 왼쪽**. A7 1200px 폭에서는 약 21.5 화면 px 차이 |
| 수정 `37a4076`, 906 후보 | 108/12 및 366×84·외곽 12pt 복원. 모든 관련 페이지는 하단 총 96pt 예약 | 실제 렌더 좌표로 버튼·아이콘·글자 중앙 확인. 투명 코너 아래 흰 사각형을 깔지 않으며 기존 넓은 터치 영역은 유지 |

독립 Copilot **Opus 5.5**의 UI 재검토는 정적 분석으로 수행했다. P2의 예전 flush 검사 지적은 이미 수정한 `DailyMissionValidation`과 신규 `NavigationLayoutValidation`로 해결했다. 테스트 기대값은 runtime 상수를 그대로 베끼지 않고 승인된 12/108/366/84를 고정하여 같은 오류가 양쪽에 들어가도 실패하도록 했다. P3의 개발 런처 겹침은 위와 같이 수정했다. 비타이머 실습을 열기만 해서는 시작 시각을 만들지 않는 동작은 유지한다. 관측 없는 시각을 기록하거나 이미 제외한 미완료 부분 성장을 복원하지 않는다.

검증은 360×640·390×844·1200×2000에서 미션→달력→정원→기록 허브→앨범→미션의 실제 버튼/raycast를 수행한다. 미션의 `기록`은 달력, 정원의 `기록`은 기록 허브로 연결하는 최신 계약을 보존한다. 기본 helper의 정원은 합성 shell이고 앨범 상세는 배치 연결 검사이므로 실제 정원·식물·사진 상세 겹침은 별도 `combined-memory` 실행으로 확인했다. 원인 조사: `artifacts/qa-release-20261109/ui-navigation-regression-audit.json`.

### 독립 core 리뷰 지적의 근거 대조

Opus 리뷰는 검토 범위와 미확인 가정을 함께 제시했다. 아래는 그중 core 지적을 원본 이력과 실제 호출 경로에 대조한 결과이며, 전체 독립 리뷰 종료를 의미하지 않는다.

| 지적 | 추가 확인 | 판정 |
| --- | --- | --- |
| schema1+texture Focus v3도 무조건 guided v4로 변환될 수 있음(P2) | schema2 도입 `ae18372`가 texture v3 도입 `da015dd`의 조상임을 Git으로 확인. `da015dd`의 `GardenStateService.Open`은 신규/기존 저장 모두 `WeeklyPlants.Migrate`로 schema2에 저장한 후에만 `state`를 공개하며, 실패하면 미션 작업이 열리지 않음. 원본 `255fb98`에서 생성한 P01 및 M10 저장은 schema2/focus3이고 현재 이행 검사에 통과. guided 원본 `a05634c`의 schema1/focus3는 별도 정의로 검사 | 확인된 원본 writer 경로에서 문제의 조합은 생성되지 않으므로 **현재 제품 결함으로 채택하지 않음**. 임의로 schema 숫자를 편집한 저장파일은 정상 배포 저장 이력의 증거가 아님. 알려지지 않은 다른 writer의 실제 파일이 발견되면 별도 확인 |
| inactive mode 초기화가 `ResetHourLocal` 대신 4시를 고정 사용(P3) | 호출은 `AppLocalReset.ResetOtherModes`→`FileStateStore.ResetInactiveForFullReset`. 제품 `WeekOneQuestDemo.Balance`의 기본값은 4이며 소스·scene·prefab·asset에서 다른 배포값 설정을 찾지 못함 | configurable API 관점의 제한은 사실이나 **현행 4시 제품 경로에서는 일자 오류가 확인되지 않음**. reset hour를 제품 설정으로 확장할 때 호출 인자로 전달해야 함. 이번 배포에 새 설정/API를 추가하지 않음 |

위 두 초기 지적은 runtime 변경을 추가하지 않았다. 50코인 고정과 초기화 후 영수증 보존은 최신 통합 결정 및 중복 보상 방지 계약에 따른 동작이다. `AppLocalReset.Cleanup`의 null 결과도 `TryContinueFullReset`에서 실패로 처리하고 pending 상태를 유지하는 것을 코드로 확인했다.

### v5 포팅 후 독립 core 리뷰와 수정

v5 frozen source의 두 번째 독립 리뷰에서는 P0–P2가 없었고 P3 세 건을 확인했다. 모두 제품 불변식을 유지하는 작은 수정과 회귀 검사를 수행했다.

| 확인한 문제 | 수정 | 실제 재검증 |
| --- | --- | --- |
| 현재 v5에서만 단계 키를 찾으면 이전 v2/v3의 `breath`/`sound` 확정 시간 피드백을 무시함 | `RecommendDuration`에 저장 세션의 정의를 받는 overload를 추가. 이력은 같은 버전에서만 수집 | v2/v3 P01의 실제 shortening→완료→같은 버전 재개→권장 17초 보존. 실제 수행 시간 0은 그대로 유지 |
| 손상된 저장에서 다른 목적의 경로를 원래 회차 완료로 주장하거나 준비 단계에 실제 시작 마커를 둘 수 있음 | `ValidateFocus`에서 두 원래 불변식을 복구. schema에 따라 이미 해석한 정의를 사용하여 v4 의미를 바꾸지 않음 | v3/v5 P19/P26 잘못된 완료·준비 시작 마커 거부, 원래 상태 불변. 원본 `a05634c` writer가 실제 생성한 schema1/focus3의 P19/P26 fallback 완료는 기존 guided v4 의미로 정상 보존 |
| guided 두 단계를 확정한 뒤 미뤄둔 세션에 늦은 완료 callback이 성공을 반환함 | 멱등 성공을 active 또는 이미 RewardCommitted인 세션으로 제한 | Ended에서 normal/shortening callback 모두 거부하고 원래 답변·무보상 유지. 이미 정상 보상한 세션의 중복 callback은 읽기만 하며 성공 유지 |

근거: `artifacts/qa-release-20261109/host/core-followup/v5-opus-*.log`. adjustments 967, contracts 135, source compatibility 95, guided 437, 카메라/추천 326 PASS. source compatibility의 95개는 원본 v4와 schema1 guided writer의 저장을 대상으로 한다. 원본 writer 대신 schema 숫자만 바꾼 식물 fixture는 유효하지 않아 사용하지 않았으며, 검증 규칙을 완화하지 않았다.

## 4. 기존 작업과 공유 계약에 미치는 영향

| 작업 | 현재 기준과 다시 연결할 지점 | 팀원 주의 사항 |
| --- | --- | --- |
| 정원 테마·아트·화면 | `GardenThemeSurface`, `FigmaShell`, `GardenHomePresenter`, `FocusMobileLayout` | 목재/잎 소재는 유지하되 현재 톤과 인셋 기준에서 배치. 과거 UI 전체 파일을 다시 가져오면 guided/앨범/탭 계약이 되돌아갈 수 있음 |
| 개별 미션 UI | `FocusMissionCatalog` v5 및 보존된 v4/v3, `GuidedFocus*`, `BasicFocusSlides`, `FocusPractice` | P01–P07 guided, 나머지 기본 실습은 현재 basic slide 경로. v3/v4 과거 세션은 `FindForSession`으로 해석. 새 v5의 M10 선택형 계획·P09/P24/P25 목적과 선택·P19/P26 별도 P02를 유지하며, v4의 저장 답변에는 새 요구사항을 소급 적용하지 않음. 완료 후 선택 촬영→성장 연출→완료와 별도 감정 기록을 유지 |
| 미션 개인화 | `FocusAdditionalRecommendation`, `FocusRecommendationMeasurement`, `PersonalizationController`, 걷기/알림 정책 | 추가 미션 선호·이력 추천과 측정은 유지. `RecommendedFocusSeconds`/`StartFocusPracticeStep`/`ShortenFocusStep`/`ShortenGuidedFocusPractice`가 현재 실습 시간을 연결하며, 다른 콘텐츠 버전의 피드백을 임의로 합치지 않음. 추천과 실제 선택을 구분하며 하루 추가 1회 제한을 우회하지 않음. guided v4/v5 P07은 `notice`, 이전 v2/v3는 `next`를 사용 |
| 식물 생성·성장 | `CombinedPlantCollection`, `WeeklyPlantState`, `GardenPlantAppearance`, procedural plant 소스 | 컬렉션이 개체/recipe/기억 연결의 기준. 새 식물만 21단계이며 기존 목표/mesh는 유지. 부분 성장 영수증은 역사 자료; 새 부분 성장 금지 |
| 데이터·저장·API | `LocalGardenState`, `StateCodec`, `FileStateStore`, `AppLocalReset`, native reset epoch | schema1/2→3 이행을 건너뛰지 않음. 저장은 `GardenStateService` 경계로 실행. 별도 지갑/저장소를 만들지 않으며 초기화 세대가 다른 callback은 반영하지 않음 |
| 기획·근거 표 | 최신 통합 결정과 현재 v5 실습 원문 | [P01–P28 v5 재매핑](FOCUS_PURPOSE_EVIDENCE_MAP_V5_20261109.md)으로 코드 대응·시간·방법·보존 사항·기존 근거 연결을 완료. 자료/전문가 미확인은 별도 표시. P21/P28 관찰 기준점 문구는 v5 코드 보완과 최종 Unity 회귀 완료 |

팀원은 하단바 수정까지 포함한 `origin/misssion@37a4076`을 새 코드 기준으로 가져온다. 기존 진행 브랜치에서 `git fetch origin` 후 `git merge origin/misssion`을 실행하고, 공통 파일은 위 계약에 맞춰 의미를 확인한다. `17c1978`만 포함한 작업 브랜치는 `37a4076`까지 추가 통합해야 한다. 이미 `37a4076`을 포함했다면 중복 merge를 만들 필요는 없다. 이후 문서 전용 커밋은 코드 기준을 바꾸지 않는다. 로컬 `main`의 고유 커밋은 그대로 보존했다.

## 5. 회의록 b·c·h 간단 대조

사용자가 말한 “11.03 회의”와 관련해 접근 가능한 원문에서 확인한 문서명은 **`1104회의록`**이다. 확인된 초록색 담당 범위는 b·c·h이며, 해당 원문의 수정 시각은 11월 4일 09:53 KST였다. 이름 차이만으로 별도의 11.03 문서가 없다고 단정하지 않는다. 이전 파란색 a·g 진행표와 구분한다.

| 담당 원문 범위 | 현재 판정 | 남은 실제 차이 |
| --- | --- | --- |
| b-i: 2·4주 과정과 기간 변경 근거 | 근거 정리 있음 | 2주 온라인 mindfulness는 간접 근거, 4주 앱 MBCT는 선행 사례. 우리 콘텐츠 효과나 표준 MBCT 동등성 입증으로 표현하지 않음 |
| b-ii, b-iii: 미션 목적·개인화 허용범위·기본/추가·수정 근거 | **28개 v5 코드·근거 재매핑 완료**, 담당 정책 복원 구현 | [목적·근거 표](FOCUS_PURPOSE_EVIDENCE_MAP_V5_20261109.md)에 v4 차이·시간·필수 보존·기존 근거 연결을 정리. P21/P28 관찰 기준점은 v5 문구 보완과 최종 Unity 회귀 완료. P19 태도 안내 충분성, P26 원 프로그램 계열, 임상 실습량·효과는 별도 미확인으로 표시. 전문가 심사 완료를 뜻하지 않음 |
| c-i: 미션 분류 | 구현 | 현재 기본/추가 각 1회와 복습 무보상 기준으로 설명 갱신 |
| c-ii: 걸음·스트레스·수면 등 입력 개인화 | 걸음/수면 및 추가 선호·이력 추천 구현 | 입력 출처·결측·지연·권한 거부를 실제 QA에서 확인. 측정되지 않은 스트레스를 측정값처럼 사용하지 않음 |
| c-iii, h: wearable 경로·추가 데이터 | 가능한 기존 수집 경로/한계를 문서화 | 별도 watch 앱·직접 IBI/새 HRV 구현은 사용자 제외 범위. 제외 기능을 이번 미완료 필수 과제로 다시 올리지 않음 |
| c-iv: 스트레스 신뢰도 QA | 실제 신뢰도 검증 미완료 | UI/단위 테스트 통과가 스트레스 신뢰도 증거는 아님. 제공된 값과 결측 상태를 확인하는 단말 QA와 구분 |

대조한 Docs 근거는 `단축·변형 MBCT 근거 재검토 2026-10-31`, `MBCT 실습 대체와 목적 보존 근거 2026-11-05`, `미션별 목적·대체 검토표 2026-11-05`, `Google Docs b·c·h 진행표와 세부 설계 2026-11-06`, `c-iii·c-iv·h 웨어러블 스트레스 데이터 경로와 QA 근거 2026-11-06`이다. 상담사 추가 검토는 사용자가 수행 불가로 정리한 사항이므로 이번 코드 통합의 새로운 선행 승인 조건으로 만들지 않는다.

**사용자 판단 반영:** 사용자는 “내 담당 부분에서 결정했던 사안이면 내 브랜치 채택하고 아니면 incoming accept”로 범위를 확정했다. 이에 신규 콘텐츠 v5에 `너무 길어요`, 회차 미루기, M10 선택형 계획, P19/P26→별도 P02, P09/P24/P25 목적·선택을 복원하고 최신 guided 화면과 결합했다. 기본 회차를 넘겨도 다음 회차를 열지 않으며, 별도 P02는 실제 수행한 소리 실습으로 기록하고 하루 추가 1회 한도에 포함한다. 미완료 부분 성장은 복원하지 않는다. 기존 incoming v4 저장은 정의·응답·단계 수를 그대로 읽으며 새 v5의 선택 의무를 소급 적용하지 않는다.

실제 경과 시간과 추천 시간은 별도 필드로 저장한다. 팝업 취소/보통 넘기기/준비 화면은 `too_long` 학습 자료로 만들지 않는다. guided P01–P07의 마지막 관찰 응답은 현재 모두 선택이며, 도메인의 공통 단계 확인에서 `RequiredAnswer`를 검사하므로 향후 필수 기록이 생겨도 빈 답변으로 건너뛰지 못한다. Unity 추가 회귀에서 실제 버튼·raycast·pause/cancel·선택 입력과 정상 완료를 확인했다. 7종 통합 UI/Play도 마지막 P21/P28 문구 보완 전 동결 소스에서 통과했다. 보완 후 선택/실습 문구와 지원 활동 역할 표시는 최종 12개 추가 회귀에서 별도로 확인했다.

## 6. 현재 확인된 검증

모든 아래 수치는 합성 데이터 또는 격리된 host 검사 결과다. 서로 다른 검사 단위를 합쳐 사용자 표본 수처럼 제시하지 않는다. 통합 첫 실행의 실패 로그도 삭제하지 않는다.

| 검사 | 확인 결과 | 근거 |
| --- | --- | --- |
| 마지막 문구 보완 전 v5 전체 host gate | **39/39 PASS, FAIL 0, SKIP 0** | `artifacts/qa-release-20261109/host/v5-final02/results.json`; Python harness 66 tests, 서버 58 tests 및 전체 등록된 도메인/native policy suite |
| FullReset | 387 assertions PASS | `scripts/test-full-reset.sh`; `artifacts/qa-release-20261109/host/core-followup/verification.md` |
| CombinedPlants | 199 checks PASS | `scripts/test-combined-plants.sh`; 과거 M10 24개 회귀 포함 |
| 카메라/추천 도메인 | 326 assertions PASS | `scripts/test-combined-camera-recommendation.sh` |
| MBCT / Focus 상태 | 1,647 / 1,645 assertions PASS | `scripts/test-mbct.sh`, `scripts/test-focus-state.sh` |
| 걷기 개인화 / 식물 사진 | 56 / 269 assertions PASS | `scripts/test-walking-personalization.sh`, `scripts/test-garden-photos.sh`; 사진 baseline 10개 함수 모두 실행 |
| 신규 v5 목적/허용범위 및 실제 v4 저장 호환 | contracts 135 / adjustments 967 / 원본 guided 저장 95 PASS | `scripts/test-focus-contracts.sh`, `test-focus-adjustment.sh`, `test-guided-v4-compatibility.sh`; fixture는 원본 `3ac0c80` 및 schema1 guided `a05634c` 코드로 생성 |
| guided / 일일 운영 / 전체 미션 | 437 / 225 / 2,711 assertions PASS | v5로 재실행; `artifacts/qa-release-20261109/host/core-followup/v5-*.log` |
| 원본 schema1·2 저장 이행 | 58 checks PASS | 원본 `a05634c`, `255fb98` 생성 fixture의 이행·실패·재개 |
| 기타 1차 단위 검사 | 항목별 결과 보존 | `artifacts/qa-release-20261109/host/unit-integration01/results.json`. 당시 FAIL을 수정 후 재실행한 위 결과와 구분 |
| 꽃 배치 / 식물 체적 | 2개 suite PASS | `artifacts/qa-release-20261109/host/geometry-final01/results.json` |
| native Robolectric 및 합성 Android 빌드 | 31/31, 합성 APK build PASS | `artifacts/qa-readiness-1109/native-opus-final.log`. 최종 Unity 제품 APK 아님 |
| native buffer | PASS | `artifacts/qa-readiness-1109/native-buffer.log` |
| 개인화 C# / 서버 대상 검사 / Play runner harness | 336 assertions / 14 tests / 10 tests PASS | `artifacts/qa-readiness-1109/{behavior-clock-after,server-targeted,play-runner}.log`. harness 통과는 Unity 실행 결과 아님 |
| APK preflight 검사 자체 | 8 tests PASS | `tests/test_qa_apk_preflight.py`; 최종 host harness에서 verifier 서명/대상 회귀까지 실행. 실제 최종 APK 판정은 별도 |
| Unity 추가 통합 회귀 | **10/10 PASS, 158 assertions, 버튼 21회·raycast 21회** | `artifacts/qa-release-20261109/unity/qa-regression-v5-final/qa-integration-validation/report.json`; 합성 preview scene, 실제 기기/네트워크 검사 아님 |
| Unity HTTP 수집·철회·초기화 | **클라이언트 37 + 서버 2 checks PASS**, cleanup 완료 | `artifacts/qa-release-20261109/unity/personalization-final/result.json`; 실제 UnityWebRequest와 격리 서버. 외부 HTTPS 운영 서버 검증 아님 |
| 전체 7종 통합 UI/Play | **7/7 PASS, 617,024 assertions, PNG 717개** | `artifacts/qa-release-20261109/unity/integrated-ui-v5-reviewed-final/final-seven-summary.json`; P25 필수 선택 hint 수정 후·P21/P28 마지막 문구 보완 전 동결 소스 |
| 마지막 표시 보완 후 최종 Unity 추가 회귀 | **12/12 PASS, 256 assertions, 버튼 27회·raycast 27회** | `artifacts/qa-release-20261109/unity/qa-regression-final12/qa-integration-validation/report.json`; P21/P28×v4/v5×두 해상도와 P19/P26→P02 추가 활동 표기·추가 슬롯만 점유·원 회차 유지 포함. 앞선 동결 7종 검사와 구분 |
| 이전 903 QA APK 오프라인 검사 | **PASS**, non-debug·동일 패키지/서명·버전 상승 | `artifacts/qa-release-20261109/candidate-apk-preflight-final/result.json`; bootstrap-final 및 verifier 검사. 현재 906 후보 판정과 구분 |
| A7/API29 이전 903 APK 인플레이스 교체·보존 | **PASS**, 정원 파일 6개·등록 hash/큐 binding/수집 시작 경계 보존·설정 복원 | `artifacts/qa-release-20261109/device-qa/final-verified/result.json`; 원래 제품 앱 미접촉. 합성 등록 사용, 당시 외부 HTTPS 업로드 미검증 |
| UI 수정 최종 Unity 회귀 | **15/15 PASS, 1,283 assertions, 버튼 84회·raycast 84회** | `artifacts/qa-release-20261109/unity/ui-navigation-fix02/qa-integration-validation/report.json`; 개발 런처 마지막 위치 보완 포함. 3개 해상도의 중앙·간격·외곽 여백, 비타이머 단일 완료 동작과 실제 타이머 단축 보존 |
| UI 수정 Daily UI | **23/23 PASS, 4,627 assertions, 버튼 391회·raycast 926회** | `artifacts/qa-release-20261109/unity/ui-navigation-integrated/runs/daily/renders/report.json`; 개발 런처 마지막 위치 보완 전 실행이며 그 변경은 최종 15개 회귀에서 확인 |
| UI 수정 정원·앨범 통합 | **19/19 PASS, 583,078 assertions, 버튼 94회·raycast 288회** | `artifacts/qa-release-20261109/unity/ui-navigation-integrated/runs/combined-memory/renders/report.json`; 실제 scene의 정원 정보판·선반 비겹침 및 사진 상세 배치/기억 잎 검증. 개발 런처 마지막 위치 보완 전 실행 |
| UI 선반 표면 EditMode / runner harness | **24/24 PASS / Python 8/8 PASS** | `artifacts/qa-release-20261109/unity/ui-theme-unit/results.xml`, `host/integrated-runner-tests/output-opus-fix-final.log`; 둥근 코너 투명·중앙/변 face 불투명·장식 입력 비간섭. harness는 Unity 실행 자체를 대신하지 않음 |
| 906 UI 수정 후보 빌드·오프라인 검사 | **PASS**, `.4`/2026110906 non-debug | `artifacts/qa-release-20261109/ui-candidate906-preflight/result.json`; 동일 패키지/서명·버전 상승 확인. A7 업데이트 보존과 실제 화면·새 버전 HTTPS 사건 수신은 아래 별도 증거로 PASS |
| Android 등록 verifier parser 호환 | **SDK33 host 10/10 및 실제 API29 PASS** | `artifacts/qa-readiness-1109/verifier-parser-fix-result.json`, 최종 실기기 result. Android parser의 DOCDECL 기본 설정을 입력 전에 끄고 DTD 거부 유지. 제품 APK는 변경하지 않음 |
| Copilot Opus 5.5 독립 리뷰 | 분할 리뷰와 수정 disposition 기록, v5 core 최종 리뷰에서 P0–P2 없음·P3 3건 수정 | [리뷰 추적](QA_REVIEW_FINDINGS_20261109.md); 다른 초기 리뷰의 P1/P2가 없었다는 뜻이 아니며 정적 리뷰를 실행 검증으로 세지 않음 |
| merge 게시 privacy scanner·정확한 staged 검사·commit/push | **PASS**, working tree 및 staged 각각 1,987개 파일 | 사용자 승인 후 `17c1978` 일반 push·remote SHA 일치. outgoing 5개 기존 커밋도 gitleaks PASS. 공개 파일의 문장/fixture/라이선스 별도 검토. 후속 문서 변경은 다시 검사 |

7종 검사 내역은 daily 4,102, guided 16,510, basic-slides 19,631, combined-memory 576,178, full-reset-ui 389, mission-growth Play 134, full-reset Play 80 assertions다. 여러 기하/화면 조건의 반복 검사를 포함한 수이며 독립 참가자·사용 과제 수가 아니다. 모든 gate는 같은 동결 runtime을 사용했고 각 실행 전후 source map 비교가 일치했다. source map SHA-256은 `1baa123fd62ffff8f32d68b08faa5dbb48dbe37ed6b07a9b30905b59f916c31f`다. 이 source map에는 뒤의 P21/P28 문구 보완이 들어 있지 않다.

P19/P26→P02의 지원 활동은 도메인의 `additional/support` 기록과 화면의 `추가 활동` 표기를 연결했다. 최종 Unity 회귀에서 원 회차 미완료·무보상 유지, 별도 P02 완료 시 추가 슬롯만 사용, 일반 P02의 기존 v4/v5 기본 활동 표기 보존을 확인했다. 기본 코스 완료나 저장 계약을 바꾸어 해결하지 않았다.

A7에서 첫 등록 verifier는 Android parser가 `FEATURE_PROCESS_DOCDECL=true`로 만들어지는 기본 동작을 입력 전부터 거부하여 실패했다. 등록 파일 exact hash·non-debug/version은 이미 통과했으므로 등록 손실로 해석하지 않았다. SDK33 host에서 정상 XML 2건의 실패를 재현한 뒤 입력 전에 해당 기능을 명시적으로 끄고 확인하며, 실제 DOCDECL/DTD 입력 거부는 유지했다. SDK33 host 10/10과 A7/API29 최종 verifier가 통과했다. 수정 대상은 검증 도구이며 위 최종 제품 APK 해시는 그대로다.

P21/P28 보완은 `잠시 멈춤`에서 **몸이 닿는 감각을 관찰**하도록 v5 카탈로그와 실제 화면에 명시하고, 편안해질 의무가 없음을 안내한다. 단계·기본 시간·선택값·저장 버전은 변경하지 않고 v4 문구는 유지했다. 구현 반영과 임상적 충분성 심사를 구분한다.

Unity 추가 회귀에는 사진 기록 이미지/삭제 조작 중복 방지, 정상 50+1 코인 표시, 잘못된 사진 보너스 단독 상태 거부, too-long 팝업의 pause/cancel과 실제 시간 보존, 기본 회차 미루기 무보상, M10 선택, 기존 v4 타이머, 과거 v3 P25의 긴 한글 제목/본문, v5 목적 문구 분기, writer lock/Open 실패 후 재시도가 포함된다. 마지막 두 테스트는 preview scene에서 실행하며 활성 scene·기존 카메라/조명·환경 변수 등을 복원한다. 이 테스트 격리를 실제 사용자 scene 복구 기능으로 과장하지 않는다.

표시 보완 당시 화면 증거는 ignored `artifacts/qa-release-20261109/unity/qa-regression-final12/focus-validation/`에 있다. `qa-photo-record-single.png`, `qa-too-long-paused.png`, `qa-m10-plan-choices.png`, `qa-texture-v3-p25-header.png`, P21/P28 해상도별 화면 및 `qa-p19-additional-p02.png`·`qa-p26-additional-p02.png`를 포함한다. 903의 A7 화면은 `artifacts/qa-release-20261109/device-qa/final-verified/`에 보존한다. **UI 수정 후 최신 Editor 화면**은 `artifacts/qa-release-20261109/unity/ui-navigation-fix02/focus-validation/`의 `qa-navigation-*` 및 비타이머/타이머 실습 캡처다. 906 A7 화면은 `artifacts/qa-release-20261109/device-qa/https/final-live906/`의 `home.png`, `missions.png`, `records.png`, `settings-clean.png`, `practice.png`, `defer-dialog.png`, `deferred.png`다. 직접 확인한 정렬·간격과 탐색 동작을 기록하며 모든 실기기 과제를 수행했다고 확대하지 않는다. 모두 합성 자료이며 공개 저장소에는 넣지 않는다.

## 7. 외부 HTTPS 및 실제 A7 검증

외부 서버는 전용 디렉터리, loopback Python API와 HTTPS 프록시 두 서비스로 구성했다. 기존 Minecraft·SSH·NAT·다른 서비스를 보존하고 HTTP/HTTPS용 IPv4 규칙 한 줄만 추가했다. 원격 단위 58/58, 실제 HTTPS 18/18, 재시작·DB·온라인 백업 10/10, 재시작 뒤 HTTPS 4/4가 통과했다. 배포·제한·백업·철회·정확한 원복 절차는 [운영 문서](../server/OPERATIONS.md)에 있다. 주소·계정·키·참가자별 자격 증명과 raw 증거는 공개 저장소에 넣지 않는다.

| 실제 기기 단계 | 확인 결과와 범위 |
| --- | --- |
| 기존 903 non-debug → 904 bootstrap | 같은 서명·패키지의 상위 버전 정상 업데이트. 904 첫 실행 전에 전체 백업. 기존 측정 4개 파일군을 원본대로 보관하고 다른 내부 10개·외부 정원 6개 파일의 바이트 보존 확인. 새 등록·binding·collection·수집 시작 경계 일치 |
| 904 실제 Wi-Fi 단절·프로세스 재시작 | 실제 UI에서 활동 선택/시작 사건 2개 생성. 활성 기본 네트워크 없음 확인. 재시작 뒤 같은 ID·발생 시각·내용·수집 경계 보존 |
| 904 연결 복구 → HTTPS 전송 | 서버의 유일 행 2개가 원본과 일치. 단말 큐 0개·receipt 2개 유지, dropped/expired=0, health clean, 서버 quality/completeness 확인 |
| 904 → 905 non-debug | 교체 직전 전체 백업, 정원 파일 6개 보존, 등록 exact hash·queue fingerprint·수집 시작 경계 verifier PASS. non-debug 및 설치 APK hash 확인. 새로 설치한 verifier만 제거 |
| 905 실제 HTTPS | 최종 `.3` 버전의 활동 종료 사건을 같은 collection으로 서버 수신. 904의 선택/시작과 연결되며 중복 없음. non-debug private outbox를 다시 읽은 검증은 아님 |
| 905 → 906 non-debug | 같은 패키지·서명으로 정상 업데이트. 첫 906 UI 실행 전 등록 exact hash·queue binding·수집 시작 경계 verifier PASS, 외부 정원 파일 6개의 바이트 동일 확인. 설치 APK hash 확인, 이번에 만든 verifier 제거, 업데이트 세션의 원래 화면 설정 복원 |
| 906 실제 화면·HTTPS | A7에서 홈·미션·기록·설정의 중앙 정렬·12pt 간격 복원과 탐색 버튼을 직접 확인. 합성 실습의 선택·시작·미루기에 따른 종료 사건 3건이 `.4` 버전의 새 고유 행으로 서버에 도착. 이전 3개 행도 그대로 보존되어 총 6개. 906 private outbox 직접 열람 검사는 아님 |
| 기기 작업 종료 | 원래 제품 앱 미접촉, app data/logcat 초기화 없음. Wi-Fi·화면 꺼짐 시간·밝기·자동 밝기 원복 확인. QA 합성 자료 보존·앱 정지 |

903은 non-debug여서 설치 전 새로운 `run-as` private 백업을 만들 수 없었다. 기존 백업을 보존하고 package-replaced 수신기 부재를 확인한 뒤 정상 업데이트 직후·첫 실행 전에 새 전체 백업을 만들었다. 설치 전 새 private 백업이 있었던 것처럼 보고하지 않는다. 이후 등록 전환은 활성 큐의 fingerprint를 강제로 편집하지 않고, 알려진 합성 등록의 네 파일군을 원본 그대로 보관한 후 새 수집 구간을 시작했다. 실제 참가자의 재등록/철회를 대신 시험한 것이 아니다.

이번 기기 사건은 LIVE 실행 경로에서 만들어졌지만 등록 대상은 운영자가 만든 합성 QA identity다. `dataset=live`만 보고 실제 참가자 분석에 포함하면 안 된다. 운영자는 별도 QA identity 목록과 bootstrap/final `app_version`을 보관하고 실제 참가자 집계에서 제외한다. 별도 삭제 시험 identity는 철회했고 A7 등록은 그 시험에 사용하지 않았다. 서버 DELETE가 로컬 수집까지 중단한다고 표현하지 않는다.

기기 증거는 `artifacts/qa-release-20261109/device-qa/https/`에 있고, `retry904/https-retry-result.json`, `final905/result.json`, `final905-https-result.json`에서 각 범위를 나눠 확인한다. 토큰·원본 앱 자료·기기 식별자가 있으므로 디렉터리 전체를 배포하지 않는다.

906 업데이트 근거는 `final906/result.json`이다. 905와 906은 모두 non-debug이므로 이 구간에서 새 `run-as` 전체 private 백업을 만든 것은 아니다. 이전 904 전체 백업을 유지하고 업데이트 전후 등록 verifier와 외부 정원 파일 바이트 비교로 보존을 확인했다. **업데이트 세션과 후속 `final-live906/` 조작 세션 모두 화면 설정 복원 PASS**다. `final-live906/restoration-result.json`에서 원래 제품 앱 미접촉·네트워크 설정 불변·QA 앱 종료도 확인했다. 후속 화면과 `.4` 사건 3건 수신은 `final906-https-ui-result.json`에 별도 기록했다. 업데이트 결과의 `interactiveUiReviewStillPending`/`externalHttpsUploadVerified`를 자동으로 바꿔 읽지 않고 후속 증거로 연결한다. 오프라인·앱 재시작 후 재전송 증거는 904 bootstrap의 결과이며 906 private outbox를 직접 확인했다고 주장하지 않는다.

### 배포 후보와 사용자 화면 검토

`.3`/2026110905는 HTTPS 검증을 통과했으나 사용자가 하단 메뉴바의 실제 회귀를 발견하여 **UI HOLD·대체됨**으로 처리했다. 후속 `37a4076`에서 버튼 중앙/간격·선반 모서리, 비타이머 중복 버튼, 개발용 P/M 런처 겹침을 수정했다. `.4`/2026110906은 빌드·오프라인 preflight와 위 Unity 검사를 통과했고 **A7 905→906 인플레이스 교체·등록 및 정원 보존, 실제 화면의 중앙·간격과 탐색 조작, `.4`로 발생한 새 선택·시작·종료 사건 3건의 HTTPS 수신을 확인**했다. 이는 실제 참가자 배포나 전체 실기기 QA 완료를 뜻하지 않는다. 앞선 7종 화면 검사 성공이 905의 회귀가 없었다는 뜻은 아니며 실제 발견·원인·수정은 3절에 기록했다.

| 906 후보 식별 | 값 |
| --- | --- |
| 버전 | `0.1.0-qa.20261109.4` / `2026110906`, non-debug |
| APK | `artifacts/qa-release-20261109/ui-candidate906/build/qa-candidate.apk` |
| 크기 / SHA-256 | `92,422,339` bytes / `2d01f4a7cee8f92790a597468bac7b76266f5017a9532ace021932988381469c` |
| 작업 소스 기준 | `37a4076500dc7fdde7b8f979b31a4a658d7a569a`; APK manifest에 소스 SHA를 내장하지 않았으므로 소스 commit의 binary attestation은 아님 |

메타데이터 원본은 같은 디렉터리의 `qa-candidate.apk.json`, 오프라인 판정은 `ui-candidate906-preflight/result.json`이다. 비공개 등록·키는 APK에 포함하지 않는다.

### 실제 배포 절차와 운영 제한

1. 서면 동의 후 참가자별 등록을 운영자가 준비한다. 앱 내 동의 화면은 다시 추가하지 않는다. 등록 없는 앱은 로컬로 동작하고 token은 APK에 넣지 않는다.
2. 최초 등록은 **bootstrap Development → 같은 패키지·서명의 더 높은 non-debug final**로 진행한다. 최종 교체 전에 private 기록·등록·설정·정원 백업을 마친다. 데이터 삭제·다운그레이드로 배포 검증을 통과시키지 않는다.
3. `scripts/check-qa-apk.py --distribution bootstrap-final`은 bootstrap과 verifier APK를 요구한다. verifier의 `expectedSha256`는 등록 파일의 정확한 바이트 해시이며 APK 해시가 아니다. 수집 시작 경계는 서면 서명 시각과 다르다. verifier는 활성 등록 확인 도구이며 HTTPS 전송·철회 증거를 대신하지 않는다.
4. native outbox는 이벤트 1,024개·receipt 8,192개·90일·배치 100개, foreground 업로드 간격 60초다. dropped/expired·storage error·completeness를 함께 본다. 90일은 기술 한도이며 서면 동의의 보관기간을 임의로 결정하지 않는다. Behavior 큐는 256행·receipt 8,192행이며 완료 watermark/손실 counter가 없다. 기기 시계가 서버보다 5분 이상 앞선 구간의 거부와 해당 관측 공백을 성공/무응답으로 해석하지 않는다.
5. 알림 예약·OS 게시 요청 수락·클릭은 서로 다른 사건이다. 실제 알림을 보았다는 뜻으로 확대하지 않으며 미클릭을 지연 0으로 바꾸지 않는다. 실제 참가자의 장기 백그라운드·재부팅·시계 변경·알림 권한과 S24 등 추가 기기 QA는 이번 A7 전송 시험과 구분한다.
6. **실제 참가자 Beta와 무작위 A/B는 미수행**이다. 자동/고정 알림의 현재 비교는 관찰 비교이며 무작위 배정·실험 ID 기능을 구현한 것으로 발표하지 않는다. 사용성·지속 사용·임상 효과는 합성 UI/네트워크 시험으로 입증하지 않는다.
7. 서버 삭제, 기기 수집 중단, 내보낸 자료 및 백업의 삭제/보관을 별도로 확인한다. 전체 단말 철회 운영 흐름, 정기 백업 주기·보관 대상과 실제 복구 연습은 참가자 운영 전에 확정·실행할 항목이다. 이번에는 온라인 SQLite 백업 무결성과 API 재시작 보존을 검증했고 자동 백업이나 호스트 재부팅을 수행했다고 하지 않는다.

3차 멘토링 보고서는 2차 형식을 유지하면서 진행 상황의 세 문단에 외부 HTTPS 배포·실제 A7 재전송과 최종906 업로드·하단바 회귀 수정을 반영했다. 원격 재다운로드의 바이트 일치와 3페이지 레이아웃을 확인했다. 멘토링 관련 두 칸은 공란으로 유지하고 교수 피드백·다른 문단은 보존했다. 내부 보고서 링크는 공개 저장소에 넣지 않는다.

## 8. 게시와 후속 검증 기록

- 사용자 승인 후 `17c1978` merge commit·일반 push·remote SHA 일치를 확인했다. working tree와 staged 1,987개 파일의 privacy scanner/gitleaks가 통과했고 키·토큰·원본 단말 자료는 제외했다.
- 이전 원본 SHA·실제 48개 충돌·해결과 영향은 1–4절에 있다. 로컬 main 고유 커밋과 팀원 브랜치는 보존했다.
- 사용자 화면 검토로 발견한 하단바 회귀는 후속 commit `37a4076500dc7fdde7b8f979b31a4a658d7a569a`에 분리했다. 일반 push 후 원격 HEAD가 해당 SHA와 일치함을 확인했다. 이 코드 게시 직전 privacy 검사도 working tree 1,992개·index 1,991개 파일에서 PASS였다. 게시 기록은 ignored `publication/ui-code-push-result.json`에 보관한다. 905를 배포 후보로 다시 안내하지 않는다. 906의 확인된 실기기 범위와 후속 조작 세션의 설정 복원 완료는 7절에 구분한다.
