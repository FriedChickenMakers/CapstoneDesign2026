# 2026-11-09 통합 및 QA 배포 준비 현황

이 문서는 **게시되지 않은 로컬 통합 후보의 검증 기록**이다. `misssion`의 일반 merge는 진행 중이며 merge commit·push는 수행하지 않았다. 마지막 표시 보완 전 동결 소스에서 host **39/39**, Unity HTTP 수집·철회 **클라이언트 37개 + 서버 2개**, **7종 통합 UI/Play**가 통과했다. 그 뒤 P21/P28 관찰 기준점과 P19/P26→P02 역할 표시를 포함한 최종 Unity 회귀 **12/12(256 assertions)**가 통과했고, non-debug QA APK의 오프라인 검사와 **A7/API29의 인플레이스 교체·등록/기존 정원 파일 보존·기기 설정 복원**도 확인했다. 외부 HTTPS 서버 연결은 아직 검증하지 않았다. 최신 사용자 지시로 서버 준비 조사를 시작했으나 SSH 공개키 인증이 거부되어 정확한 계정·키 등록 확인을 기다리고 있다. 원격 조사·디렉터리 생성은 미실행이며 실제 참가자 A/B 평가도 미수행이다.

현재 `AGENTS.md`의 명시적 게시 규칙에 따라 commit/push는 별도 사용자 게시 요청이 있을 때만 수행한다. 검증 중인 작업 트리나 이전 HEAD를 이미 게시한 통합 커밋으로 안내하지 않는다.

이 문서는 브랜치 통합, 회의 담당 범위, 계약 변경과 회귀 검증, 현재 배포 운영의 제약을 다룬다. 실제 참가자 기록·기기 식별자·내부 문서 URL·서버 주소·자격 증명은 포함하지 않는다.

## 1. 통합 기준과 보존한 브랜치

최신 작업은 이름이나 시각만으로 선택하지 않았다. fetch 후 커밋 그래프, 포함 관계, 소스 및 [11월 7일 통합 결정](GARDEN_BRANCH_INTEGRATION_20261107.md)을 대조했다. `combine-garden`은 이전 guided UI와 texture 작업을 선택적으로 결합한 별도 구현이므로, 단순히 texture의 모든 동작을 다시 덮어넣지 않는다.

아래 수치는 **통합 직전 HEAD `255fb98`에만 있는 커밋 / 비교 ref에만 있는 커밋**이다. 로컬 merge가 끝난 뒤의 수치가 아니다.

| 비교 대상 | 확인 SHA | HEAD에만 / 대상에만 | 처리 |
| --- | --- | ---: | --- |
| `misssion` 로컬 통합 기준, `origin/texture-rework` | `255fb987b4a4f56768d0d626cefff9262f74d6c9` | 0 / 0 | 기존 `misssion`에서 texture의 후속 2개 커밋을 fast-forward로 보존한 기준 |
| `origin/misssion` | `8b5d963f03709e236675397ce1f8c7ecfcbefd42` | 2 / 0 | 원격 기준 보존. 이번 통합 결과는 아직 push하지 않음 |
| `origin/main` | `b7332ee6eaf5c8db51ddc561aa88b2e3d26935dd` | 11 / 0 | 현재 작업과 combine 양쪽의 공통 조상. 별도 중복 merge 불필요 |
| 로컬 `main` | `be160d8373d11b7365aedfd764b682d796f30e01` | 87 / 1 | 고유 커밋 보존. reset·merge·push하지 않음 |
| `origin/codex/combine-garden-20261107` | `3ac0c8078735b49e0983dff6de4c6e537660f8f0` | 11 / 3 | 최신 guided·정원·일지·초기화 계약의 통합 대상. 일반 merge 진행 |
| `origin/codex/integrate-garden-missions-20261029` | `a05634cbf6f33161dfb3a2a12b294b68356cd2ab` | 11 / 1 | combine에 포함된 이전 기준. 따로 중복 merge하지 않음 |
| `origin/codex/camera-mission-validation` | `e1954c1155eb6b9f6c569fd45feab919254bb760` | 9 / 0 | texture 기준에 이미 포함. 사진 검증 연결을 통합 과정에서 보존 |
| `origin/codex/procedural-plant-generation` | `644fec06960c31226d3364c2082412c04ed2baa5` | 7 / 0 | texture 기준에 이미 포함. 형태·기억 잎 변경을 최신 정원과 결합 |

사용자 후속 결정은 **담당 b·c·h에서 확정한 계약은 기존 작업을 채택하고 나머지는 incoming을 채택**하는 것이다. 이를 content v5로 합쳤으며, 아래 충돌 표에서 실제 유지·결합 결과를 구분한다.

복구 및 비교 근거는 ignored `artifacts/qa-release-20261109/branches-before.json`과 각 원본 SHA다. 이 JSON의 ahead/behind 기준은 최초 `misssion@8b5d963`이므로 위 HEAD 기준 표와 혼동하지 않는다. 마지막 fetch의 `artifacts/qa-release-20261109/branches-final-fetch.json`에서도 위 원격 SHA가 유지되어 추가 원격 커밋은 없었다. 새 게시 SHA는 아직 없으며 게시 후 별도 확인한다. force push·브랜치 삭제·로컬 main 덮어쓰기는 수행하지 않는다.

## 2. 실제 텍스트 충돌과 해결

**48개 텍스트 경로에서 충돌이 발생했다.** `.meta` 2개가 포함된다. 원본 3-way 자료는 `artifacts/qa-release-20261109/conflicts/`, 전체 목록은 해당 디렉터리의 `files.json`에 보존했다. 파일 전체를 일괄 ours/theirs로 고르는 방식으로 해결하지 않았다. 의미를 대조한 결과 일부 파일이 incoming과 동일해진 경우도 아래에 명시한다.

| 파일·영역 | 양쪽 변경과 충돌 원인 | 해결 및 최종 동작 | 검증·주의 |
| --- | --- | --- | --- |
| `FocusMissionCatalog.cs`, `LocalGardenState.cs` | 양쪽이 서로 다른 실습 구조에 Focus v3를 사용함 | 신규 목적·허용범위 계약은 v5. incoming guided v4는 `AllV4`, texture v3는 `FocusMissionCatalog.TextureHistory.cs`의 역사 정의로 분리. schema1 guided는 v4, schema2 texture는 v3로 정확히 이행 | 원본 3개 브랜치가 생성한 합성 저장파일과 실패/재시도 검사. 기존 v4 P25의 빈 완료 응답과 M10 입력을 신규 v5 필수 선택으로 재해석하지 않음 |
| `LocalGardenState.cs`, `WeeklyPlantState.cs`, `GardenPlantAppearance.cs` | 부분 성장·영양제/옛 성장 장부와 완료 성장·코인·정원 컬렉션 계약이 충돌 | 최신 완료 성장·하루 기본/추가 한도·별도 감정 기록·자동 사진 보관 유지. 기존 식물 recipe/mesh/목표와 과거 부분 성장 영수증은 보존하되 새 부분 성장은 지급하지 않음 | CombinedPlants, FocusState, 사진·감정·일일 한도 검사. `GardenStateService`가 단일 저장 경계 |
| `LocalGardenState.cs` 저장·삭제 | 기존 privacy revision/삭제 백업 보호와 신규 전체 초기화 journal이 같은 저장 경로를 변경 | journal의 원자적 초기화와 기존 삭제 시 `.bak`에 이전 개인 기록을 남기지 않는 처리를 결합. 기존 경험 삭제 tombstone 및 추천 대상 일치 검증도 유지 | 실제 파일 실패·재시작 검사. 일지 기록 삭제와 식물 소유 감정 메모 보존 범위를 구분 |
| `FocusPracticeInteractions.cs`, `WeekOneQuestDemo.AllFocusPractice.cs`, `FocusPractice.cs` | 옛 조절 UI/타이머와 guided·basic slide·성장 연출 흐름이 충돌 | 최신 실습 화면·오디오 위치 복원을 유지하고, 사용자의 담당 정책 우선 지시에 따라 실제 타이머 시작 후 `너무 길어요`→일시정지→여기까지/취소 및 확인된 실제 시간에 따른 다음 추천을 결합. 미완료는 무보상이며 정상 필수 단계 완료만 보상 | Unity 추가 회귀에서 팝업 pause/cancel, 실제 시간, 미루기 무보상, M10 선택, v4 타이머·v5 목적 copy 통과. 7종 통합 UI는 보완 전 동결 소스에서 PASS. P21/P28 문구와 P19/P26→P02 역할 표시를 포함한 최종 12개 회귀도 PASS |
| `CameraMissionPolicy.cs`, `PhotoEvidence.cs` | 카메라 대상 버전과 보상 용어/완료 순서 변경 | v2/v3/v4 과거 및 현재 v5 대상 판정을 지원. 선택 촬영·건너뛰기·하루 1코인 보너스를 최신 완료 흐름에 연결 | 카메라/추천 도메인 326 assertions. 객체 검출은 실제 활동 수행의 증명이 아님 |
| `GardenThemeSurface.cs`, `FigmaShell.cs`, `FocusMobileLayout.cs`, `MissionBottomNavigation.cs` | 기존 목재·잎 테마와 새 톤/대비/인셋·탭 배치가 같은 영역을 수정 | texture 소재를 유지하면서 최신 제목 대비·불투명 하단 선반·pivot/inset 배치를 적용 | 이번 실행의 화면·raycast 검사 사용. 7종 통합 UI는 보완 전 소스에서 PASS. 과거 스크린샷을 새 문구 검증으로 사용하지 않음 |
| `GardenHomePresenter*.cs`, `GardenMemoryPicker.cs`, `PlantMemoryGeometry.cs` 및 `.meta` | 기존 선택 식물·포인터 소유와 새 canonical plant·기억 잎 paging/연출 경로 충돌 | 공용 Camera→RenderTexture→RawImage를 유지하고 현재 식물/미리보기/앨범·기록 선택을 결합. 체적 잎에 최신 잎맥 표시 추가. `.meta`는 실제 incoming 참조에 맞는 GUID 유지 | 기억 잎/기하 검사. old pulse는 최신 성장 연출로 대체. 보완 전 동결 소스의 combined-memory UI/기하 검사 PASS; 실제 사용자 장시간 조작 평가는 별도 |
| `WeekOneQuestDemo.cs`, `MissionControls.cs`, `GrowthRecords.cs`, `FocusHome.cs` | 기존 사진 정리·추천 노출과 신규 초기화·일지·일일 진행 화면 충돌 | 최신 안전한 로드/초기화·활동 일지·일일 제한을 적용하고 성공한 열기/초기화 뒤 사진 prune·삭제 및 추천 노출 귀속을 함께 보존 | 저장·일지·추천 검사. 초기화 도중 옛 callback은 복원된 새 세대를 변경하면 안 됨 |
| Android `ActivityNotifications.kt`, `AndroidPlatformBridge.kt`, `MissionPhotoActivity.kt`, `MissionPhotoController.kt`; 관련 Robolectric 2개 | 기존 알림 학습·시간 설정·사진 처리에 새 reset epoch가 추가됨 | 학습/설정/선택 촬영을 보존하면서 `LocalResetEpoch`와 늦은 worker·Intent·촬영 callback 차단을 결합. 25 hunk를 대조한 결과 당시 incoming 구현과 일치 | native host 31/31. 실제 단말 알림·촬영·업데이트는 별도 |
| Editor 검사 4개, `scripts/qa.py`, 테스트 실행 스크립트 및 충돌한 도메인 테스트 | 신규 소스·일일 한도·보상·schema3와 이전 컴파일 목록/기대값이 달라짐 | 공통 도메인 소스 manifest를 도입하고 Linux/PowerShell 경로를 맞춤. 최신 정책으로 기대값을 갱신하면서 실패 원자성·재시작·중복 방지 검증 유지 | 통합 1차 실패 기록 보존. 실패 수정 후 개별 재실행 결과는 아래 표 참고 |

Core 6개 파일의 47 hunk 판단은 `artifacts/qa-release-20261109/host/core-followup/merge-decisions.json`, UI/검사 쪽 결정은 `artifacts/qa-release-20261109/root-conflict-resolutions.json`에 기록했다. 모든 표의 검증이 최종 APK에서 끝났다는 의미는 아니다.

## 3. 자동 merge 뒤 발견한 동작·검증 충돌

| 문제 | 구분 | 실제 조치 |
| --- | --- | --- |
| 과거 M10 선택형 계획은 읽히지만 현재 텍스트 입력으로 수정하면 구조화 필드와 Value가 달라 저장 실패 | 실제 호환 결함 | `LocalGardenState.UpdateFocusAnswer`에서 **명시적 Value 변경 시에만** 과거 `PlanActivity/PlanTimePeriod/PlanNote`를 비움. 이행·동일값 저장은 원본 유지. 저장 실패 시 양쪽 모두 유지. 원본 `255fb98` 코드가 생성한 두 종류의 draft로 재현·재검증 |
| 이전 조절/주간 식물/화면 audit 코드가 신규 UI에 없는 필드·API를 참조 | 자동 merge 뒤 소스 계약 불일치 | 사용하지 않는 주간 식물/화면 audit 진입점은 정리하고 원본은 ignored `artifacts/qa-release-20261109/retired-source/`에 보존. 사용자 담당 범위로 확정된 조절 정책은 최신 guided/basic slide 연결로 포팅하고 contracts/adjustments를 release gate에 다시 포함 |
| FullReset 테스트가 열린 `FileStream`으로 삭제 실패를 기대함 | 테스트 플랫폼 의존 | Unix에서는 unlink가 가능함을 incoming 원본에서도 재현. `reset.tmp` 디렉터리로 실제 파일 삭제 실패를 만들고, fresh primary 기록 후 pending journal 유지→재시작→재시도를 검증. runtime 우회·강제 PASS·skip 없음 |
| 5코인·복습 성장·하루 추가 활동 반복·수동 보관 후 빈 화분·새 식물 12단계 기대 | 구 정책 테스트 | 50코인·복습 무성장/무보상·하루 추가 1회·완료 식물 계속 표시·새 식물 21단계로 수정. 과거 영수증/식물·메모 보존, 동시 재시도, 손상된 primary 복구 거부 등 기존 보장은 유지 |
| 일시적 연구 등록 읽기/queue 초기화 실패를 철회로 취급해 로그를 지울 수 있음 | 로그 보존 결함 | 수집 상태를 일시 중단하고 3초 재시도. 실제 철회/초기화와 구분하여 기존 queue를 보존 |
| 추천 노출 저장 실패 또는 재시작 뒤 최초 노출 귀속이 달라질 수 있음 | 측정 신뢰성 결함 | 같은 decision ID·최초 시각으로 재시도하고 저장된 snapshot에서 복구 |
| 잘못 앞선 기기 시계의 이벤트가 정상 배치 전송까지 막음 | 측정 배치 결함 | 미래 이벤트 원본은 보존하며 전송 보류. 정상 이벤트는 전송하고 미전송분이 있으면 completeness를 0으로 유지 |

마지막 세 항목은 native/개인화 담당의 독립 수정 및 해당 검사 결과를 반영했다. 이후 Opus 검토로 health timeout/cancel 구분·busy 해제·reset 세대 차단, reset 중 foreground-service 시작 차단, 영구 거부/일시 실패를 구분한 노출 재시도(backoff·횟수 한도), 시계 복구 때 미래 behavior 행을 지우지 않는 보류 처리를 추가했다. 현행 서버 통신·Unity UI·실제 기기에서 전체 흐름이 확인됐다는 주장으로 확대하지 않는다.

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

최종 merge commit이 아직 없으므로 팀원에게 새로운 배포 기준 SHA를 지정하지 않는다. 명시적 게시 요청 후 `misssion`의 실제 merge SHA·검증한 APK SHA-256을 기록하고, 각 진행 브랜치는 게시된 기준을 일반 merge로 가져온다. 원격에 팀원 후속 작업이 있으면 포함 관계와 의미를 다시 확인한다. 로컬 `main`의 고유 커밋을 reset으로 제거하지 않는다.

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
| 최종 QA APK 오프라인 검사 | **PASS**, non-debug·동일 패키지/서명·버전 상승 | `artifacts/qa-release-20261109/candidate-apk-preflight-final/result.json`; bootstrap-final 및 verifier 검사, APK 메타데이터는 아래 기록 |
| A7/API29 최종 APK 인플레이스 교체·보존 | **PASS**, 정원 파일 6개·등록 hash/큐 binding/수집 시작 경계 보존·설정 복원 | `artifacts/qa-release-20261109/device-qa/final-verified/result.json`; 원래 제품 앱 미접촉. 합성 등록 사용, 외부 HTTPS 업로드 미검증 |
| Android 등록 verifier parser 호환 | **SDK33 host 10/10 및 실제 API29 PASS** | `artifacts/qa-readiness-1109/verifier-parser-fix-result.json`, 최종 실기기 result. Android parser의 DOCDECL 기본 설정을 입력 전에 끄고 DTD 거부 유지. 제품 APK는 변경하지 않음 |
| Copilot Opus 5.5 독립 리뷰 | 분할 리뷰와 수정 disposition 기록, v5 core 최종 리뷰에서 P0–P2 없음·P3 3건 수정 | [리뷰 추적](QA_REVIEW_FINDINGS_20261109.md); 다른 초기 리뷰의 P1/P2가 없었다는 뜻이 아니며 정적 리뷰를 실행 검증으로 세지 않음 |
| 최종 privacy scanner·정확한 staged 검사·commit/push | **게시 미수행** | 현재 AGENTS의 명시적 게시 요청 규칙 적용. 게시 전 working tree 및 정확한 staged 검사와 일반 push 후 remote SHA 확인 필요 |

7종 검사 내역은 daily 4,102, guided 16,510, basic-slides 19,631, combined-memory 576,178, full-reset-ui 389, mission-growth Play 134, full-reset Play 80 assertions다. 여러 기하/화면 조건의 반복 검사를 포함한 수이며 독립 참가자·사용 과제 수가 아니다. 모든 gate는 같은 동결 runtime을 사용했고 각 실행 전후 source map 비교가 일치했다. source map SHA-256은 `1baa123fd62ffff8f32d68b08faa5dbb48dbe37ed6b07a9b30905b59f916c31f`다. 이 source map에는 뒤의 P21/P28 문구 보완이 들어 있지 않다.

P19/P26→P02의 지원 활동은 도메인의 `additional/support` 기록과 화면의 `추가 활동` 표기를 연결했다. 최종 Unity 회귀에서 원 회차 미완료·무보상 유지, 별도 P02 완료 시 추가 슬롯만 사용, 일반 P02의 기존 v4/v5 기본 활동 표기 보존을 확인했다. 기본 코스 완료나 저장 계약을 바꾸어 해결하지 않았다.

A7에서 첫 등록 verifier는 Android parser가 `FEATURE_PROCESS_DOCDECL=true`로 만들어지는 기본 동작을 입력 전부터 거부하여 실패했다. 등록 파일 exact hash·non-debug/version은 이미 통과했으므로 등록 손실로 해석하지 않았다. SDK33 host에서 정상 XML 2건의 실패를 재현한 뒤 입력 전에 해당 기능을 명시적으로 끄고 확인하며, 실제 DOCDECL/DTD 입력 거부는 유지했다. SDK33 host 10/10과 A7/API29 최종 verifier가 통과했다. 수정 대상은 검증 도구이며 위 최종 제품 APK 해시는 그대로다.

P21/P28 보완은 `잠시 멈춤`에서 **몸이 닿는 감각을 관찰**하도록 v5 카탈로그와 실제 화면에 명시하고, 편안해질 의무가 없음을 안내한다. 단계·기본 시간·선택값·저장 버전은 변경하지 않고 v4 문구는 유지했다. 구현 반영과 임상적 충분성 심사를 구분한다.

Unity 추가 회귀에는 사진 기록 이미지/삭제 조작 중복 방지, 정상 50+1 코인 표시, 잘못된 사진 보너스 단독 상태 거부, too-long 팝업의 pause/cancel과 실제 시간 보존, 기본 회차 미루기 무보상, M10 선택, 기존 v4 타이머, 과거 v3 P25의 긴 한글 제목/본문, v5 목적 문구 분기, writer lock/Open 실패 후 재시도가 포함된다. 마지막 두 테스트는 preview scene에서 실행하며 활성 scene·기존 카메라/조명·환경 변수 등을 복원한다. 이 테스트 격리를 실제 사용자 scene 복구 기능으로 과장하지 않는다.

최종 화면 증거는 ignored `artifacts/qa-release-20261109/unity/qa-regression-final12/focus-validation/`에 있다. `qa-photo-record-single.png`, `qa-too-long-paused.png`, `qa-m10-plan-choices.png`, `qa-texture-v3-p25-header.png`, P21/P28 해상도별 화면 및 `qa-p19-additional-p02.png`·`qa-p26-additional-p02.png`를 포함한다. A7 최종 화면은 `artifacts/qa-release-20261109/device-qa/final-verified/`의 `final-home.png`, `final-missions.png`, `final-settings.png`에 보관했다. 모두 합성 자료이며 공개 저장소에는 넣지 않는다.

## 7. QA 측정과 배포 전 남은 작업

- 알림은 예약 시각, 시스템 게시 요청 수락/실패/복구, 클릭을 같은 알림 ID로 연결한다. 시각 차이를 발표할 때 “게시 요청 수락”과 “사용자가 실제 알림을 봄”을 구분한다. 실제 단말에서 알림 권한 거부·백그라운드 제한·재부팅·시계 변경도 확인해야 한다.
- 추천은 노출→실제 선택→시작→완료/종료와 추천/실제 활동을 분리한다. 합성 페르소나 결과와 실제 QA 참가자 결과를 섞지 않는다. 클릭하지 않은 알림을 지연 0으로 처리하지 않으며, queue 미전송/누락 상태도 함께 보고한다.
- **최신 사용자 지시로 서버 접속·격리 디렉터리 준비 조사를 추가했다.** 지정 계정으로 SSH 서비스에는 도달했고 공개키를 실제 제공했으나 서버가 인증을 거부했다. 키 로드 오류는 없었으며, 원격 환경 조사·디렉터리 생성·서비스 변경은 전혀 실행되지 않았다. 정확한 로그인 계정·공개키 등록 확인 후 재개한다. 기존 서비스의 중단·재시작·방화벽 변경은 하지 않는다. 이후 실제 배포 APK의 등록·HTTPS 업로드·서버 ACK·오프라인 재시도·앱 재시작·철회/삭제 경로를 검증해야 한다. 이는 참가자 수집 시작 전 확인 항목이며 단위 outbox 통과로 대신하지 않는다. 상세 연결 증거는 비공개 `artifacts/qa-release-20261109/server-preflight/`에 보관한다.
- 현행 outbox에는 용량/보존기간 제한이 있다. 장기간 오프라인이나 저장 실패를 “무손실”로 표현하지 않고 dropped/expired·오류 상태를 확인한다. 운영 절차와 한도는 별도 배포 문서 기준으로 고정한다.
- 앱 내 동의 화면을 다시 추가하지 않는다. 서면 동의와 개별 연구 등록·철회 처리, 최종 앱 버전 고정은 운영 절차로 준비한다.
- 최종 QA에서 바뀐 입력/정책으로 인해 평가 자료가 섞이지 않도록 commit·버전·dataset을 남긴다. 연구 설계 타당성, 구현, 합성 검증, 실제 사용자 결과를 나눠 제시한다.

확인한 최종 평가 후보는 `versionName=0.1.0-qa.20261109.2`, `versionCode=2026110903`, package `com.capstonedesign2026.qa`, **non-debug**이다. APK는 `artifacts/qa-release-20261109/android-candidate-final/build/qa-candidate.apk`, 크기 **92,418,991 bytes**, SHA-256 **`e0fe874e47303b93d21cdf0de3bd841cd475e18abc7b1b33dc24957dbbf6fe9e`**다. `2026110902`는 P25 hint 조정 전 중간 후보이며 최종 버전이 아니다. bootstrap `.1`/`2026110901`의 등록·설정 자료와 최종 `.2`의 평가 자료는 `app_version`으로 구분한다. 실제 참가자는 아직 없고 A7은 합성 HTTPS 등록을 사용했다. 따라서 APK·업데이트·등록 보존 PASS가 외부 HTTPS 수집 성공을 뜻하지 않는다.

### 실제 배포 절차와 현재 운영 제한

1. 배포 전 기존 앱 기록·등록·설정의 백업과 복구 기준을 마련한다. 실제 기기 작업에서는 저장된 원래 화면 설정을 기록하고 작업 종료 시 복원한다. 기존 기록을 지우는 재설치/초기화로 검증을 통과시키지 않는다.
2. 준비 경로는 **bootstrap Development APK → 같은 패키지·같은 서명의 non-debug 최종 APK로 in-place 교체**다. 별도 등록 UI/전용 등록 CLI는 현재 없으므로 운영자가 서면 동의 후 bootstrap 단계에서 참가자별 private 등록 파일을 배치해야 한다. 등록 내용은 저장소·공유 APK·로그에 넣지 않는다.
3. 최종 non-debug APK에서는 `run-as` 백업을 사용할 수 없다. 기존 `deploy-a7`의 설치 후 `run-as` 백업도 그대로 적용할 수 없으므로 **교체 전 백업 완료**가 필요하다. `scripts/check-qa-apk.py --distribution bootstrap-final`은 `--previous-apk`와 `--verifier-apk`를 모두 요구한다. 후보와 verifier의 같은 서명 및 고정 QA package를 검사한다. 최종 APK의 오프라인 검사와 A7/API29 인플레이스 교체·등록 보존 검사는 통과했다. verifier의 `expectedSha256`는 **등록 파일 정확한 바이트의 SHA-256**이며 APK 해시가 아니다. `expectedMeasurementConsentUtcMs`는 서면 서명 시각이 아니라 bootstrap의 수집 활성화 경계다. 활성 등록만 검사하므로 철회 검증에 사용할 수 없다. final 설치 후 재등록에는 더 높은 버전의 bootstrap과 그보다 높은 final로 같은 패키지/서명 인플레이스 업데이트가 필요하다.
4. 최종 앱은 HTTP loopback 서버를 허용하지 않으므로 실제 수집에는 HTTPS 서버가 필요하다. 서버 접속 준비는 다시 범위에 포함했으나 현재 SSH 공개키 인증 확인이 필요하다. 원격 조사와 격리 작업 디렉터리 생성은 아직 실행되지 않았다. 서버 준비 후 기기 등록·TLS·업로드·ACK·오프라인 복구를 별도로 확인하고 실제 참가자 수집을 시작한다. 이번 앱 배포 후보 준비와 외부 수집 개시는 구분한다.
5. native outbox 한도는 이벤트 1,024개, 중복 방지 receipt 8,192개, 보존기간 90일, 배치 100개다. foreground 업로드 간격은 60초다. 용량 거부 횟수·expired·storage error·completeness를 함께 확인한다. 90일은 현행 기술 보존 한도이며 연구 동의서의 보존기간을 대신 결정하는 값이 아니다. Behavior outbox는 256행, receipt는 8,192행이며 native와 달리 완료 watermark/손실 counter가 없어 해당 관측 공백이 평가에서 자동 제외되지는 않는다. 서버보다 5분 이상 앞선 기기 시계는 서버 배치 거부를 일으킬 수 있다. 이 구간을 참여 성과의 근거로 삼지 않는다.
6. **실제 참가자 A/B 테스트는 아직 수행하지 않았다.** 현재 자동/고정 알림 비교는 관찰 비교다. 무작위 배정·실험 ID를 가진 A/B 배정 기능은 구현되지 않았으므로 이를 무작위 A/B 실험 결과라고 발표하지 않는다.
7. 철회/삭제는 서버 DELETE와 기기·내보낸 자료·백업 처리를 각각 확인한다. **서버 DELETE만으로 기기의 로컬 수집이 중단되지는 않으므로**, 운영자가 로컬 수집 중단과 자료 정리를 별도로 확인한다. 앱의 로컬 전체 초기화도 서버 데이터 삭제를 뜻하지 않는다. 새 앱 내 동의 UI를 추가하는 절차가 아니다.

멘토링 보고서는 2차 형식을 참고한 **3차 진행 상황 부분의 작성 완료가 담당 작업에서 보고됨**을 기록한다. 멘토링 관련 항목은 비워두며, 최종 제출 상태와 문서 반영 결과는 보고서 담당의 실제 확인에 따른다. 본 문서에는 내부 보고서 링크를 싣지 않는다.

## 8. 후속 결과 반영 지점

- **완료:** P21/P28 문구 및 지원 활동 역할 표시 보완 후 12개/256 assertions를 별도 report로 확인했다. 앞선 7종 동결 검사와 10개/158 assertions는 해당 실행의 이력으로 유지하며 중복 합산하지 않는다.
- **완료:** 최종 non-debug APK의 package·버전·서명·해시와 bootstrap→final 전환, 등록 파일/큐 시작 경계 보존, 단말 원래 설정 복원을 6·7절에 기록했다. 읽기 전용 verifier는 등록 파일 exact hash·큐 등록 fingerprint·수집 시작 경계·non-debug/version을 확인한다. 이 통과를 외부 HTTPS 업로드·ACK·철회 성공으로 표시하지 않는다.
- 서버 SSH 계정·공개키 등록 확인 후: 원격 사용 현황을 읽기 전용으로 확인하고 전용 작업 디렉터리를 준비한다. 이후 구성에 맞춘 배포와 운영 HTTPS 등록·전송·ACK·오프라인 복구·철회를 검증하고 7절의 상태를 갱신한다. 실제 A/B 배정·실험은 별도 계획/수행 결과가 생기기 전까지 미수행으로 둔다.
- 게시 요청을 받은 뒤: 최종 privacy 결과, merge SHA, 일반 push 결과와 remote HEAD 일치를 기록한다. 현재 상태는 로컬 merge 중이며 commit/push 미수행이다.
