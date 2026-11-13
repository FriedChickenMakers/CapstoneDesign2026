# 모바일 미션 UI 후속 통합 · 2026-10-31

2026-10-29 통합 이후 원격을 다시 fetch하여 `misssion`의 신규 `000b7e5`를 기존 정원 테마 통합본에 병합한다. 원본 브랜치를 이동하지 않고 기존 `codex/integrate-garden-missions-20261029`에서 검증한 뒤 main에도 일반 push한다. 이번 사용자는 추가 merge와 push를 명시적으로 요청했다.

## 기준과 변경 범위

| 기준 | 시작 SHA | 판단 |
| --- | --- | --- |
| 현재 통합 브랜치 / origin/main | `dae45c54d5fd807ff7e64e95bb055c1c2f015c56` | 작업 트리 clean, 양쪽 원격 일치 |
| origin/misssion | `000b7e5ec6a3c1a3e5ec2576cb884802152952da` | 10-30 23:36:58 +09:00, 이전 `1e60135` 이후 신규 1커밋. 현재 기준 ahead 1 / behind 4 |
| 나머지 원격 작업 브랜치 | 이전 통합 문서의 SHA와 동일 | 새 기능 없음, 이미 포함된 내용을 재병합하지 않음 |
| 로컬 main | `be160d8373d11b7365aedfd764b682d796f30e01` | 고유 회의 기록 보존, 이동·reset하지 않음 |

새 작업은 모바일 폭 기준 배치, 권장·기본 미션/코스/기록 구성, 감정 8개 우선·24개 펼치기, 선택 기록 생략, 타이머·오디오·그림 복원, 움직임 줄이기, 개발 전용 설정 분리다. 새 원본의 22개 파일을 검토했으며 저장 모델·보상·식물 생성·추천 알고리즘·서버·네이티브 알림 변경은 없다.

## 실제 충돌과 해결

텍스트 충돌은 **4개 파일의 5개 구간**이다. 파일 전체를 ours/theirs로 선택하지 않았다.

| 종류 / 파일·영역 | 양쪽 의도·원인 | 결합 방법·최종 동작 | 확인 방법 |
| --- | --- | --- | --- |
| 텍스트 · `FigmaMissionUi.cs` 버튼 | 기존 정원 표면·장식 여백 vs 새 누름/키보드 포커스 피드백이 같은 생성 코드를 수정 | 테마·여백 유지 후 `FocusControlFeedback` 연결. 자동 탐색과 움직임 줄이기 유지 | 모바일 크기별 실제 버튼/레이캐스트, 테마 캡처 |
| 텍스트 · `SensorRawDisplay.cs` 제목 | 기존 목재 명패 vs 새 모바일 제목 간격 | 명패 유지, 새 24 간격·64 높이 결합. 개발 도구 접기/초기화 확인/출시 제외 분기는 유지 | 설정·초기화·실패 회귀, Android 출시 스크립트 컴파일 |
| 텍스트 · `FocusHome.cs` 내 과정 2구간 | 기존 복습·정원/건강 연결 vs 새 현재 코스 우선·동적 높이 목록 | 새 정렬/행 높이/y 누적에 기존 두 진입점을 추가하고 전체 콘텐츠 높이 계산 | 이전 활동 추천·건강 기록 복귀, 코스별 기록, 48회차 접근 |
| 텍스트 · `FocusMissionValidation.cs` Reveal | 기존 그래프 RectTransform 지원 vs 새 중첩 스크롤 지원 | RectTransform 오버로드와 모든 활성 상위 스크롤 순회를 결합. 최초 위치 검증은 Reveal 전에 수행 | 건강 그래프 가시성·모바일 입력 대상 검증 |
| 동작 · `FigmaShell.cs` / `FocusMobileLayout.cs` | 기존 628 높이 스크롤 막대가 새 가변 뷰포트와 불일치; 작은 화면에서 하단 탭까지 침범 | 막대를 뷰포트 자식으로 늘림 배치하고, 중첩 스크롤을 펼쳐 비활성화할 때 해당 막대도 숨김 | 세 화면 크기에서 막대 경계/높이 assertion 및 캡처 |
| 동작 · `FocusMissionVisuals.cs` | 새 등장 효과는 .18초 보간인데 기존 .16초 종료가 남아 불투명도가 덜 복구됨 | .18초 완료 시 alpha를 1로 확정 | 컴파일·Play 화면 회귀 |
| 검증 연결 · Focus/LocalLoop/모바일·코스 도구 | 기존 버튼명·진단 진입·가로 Game 뷰 가정과 새 UI 불일치; 새 결과 경로의 캡처 폴더 누락 | 실제 버튼 ID·개발 도구 진입·컴팩트 세로 조건 적용, 공통 fixture가 캡처 경로 생성. 기존 실패 검사를 제거하지 않음 | 같은 검사들을 새 결과 디렉터리에서 재실행 |

버전별 저장 제목·non-Focus 기록 분기, `FocusSaved`의 저장 성공 관측, `AppliedCycleId` 읽기 전용 날짜 조회, 정원 성장/보상 연결은 보존한다. 신규 M 추천의 완전 개인화나 고품질 식물 제작을 이번 병합으로 완료했다고 해석하지 않는다.

## 담당자 영향

- UI: `FigmaMissionUi`, `FigmaShell`, `FocusHome`, `FocusMobileLayout`, `SensorRawDisplay`가 공통 수정 지점이다. 본문·하단 동작·오류·탐색 영역을 분리하며 스크롤 막대는 가변 뷰포트에 종속된다.
- 개인화: 이전 활동 복습 허브와 관측 연결 유지. 새 추천 정책/입출력/API 변경 없음. 코스·기록 UI 연결 시 새 동적 배치를 따른다.
- 식물: 생성기·Recipe·성장 계산 변경 없음. `GardenHomePresenter`의 설정 터치 영역과 낮/밤 글자 대비선, 탐색 상태 배지만 변경된다.
- 저장: 직렬화 형식·세션 ID·보상 계약 변경 없음. 새 `ui.reduce-motion`은 로컬 UI 선호 설정이다. 실제 사용자 데이터·기기 설정은 이번 검증에 사용하지 않는다.
- 검증: 기존 Focus 통합 검증을 유지하면서 새 모바일·전체 코스 도구를 `qa.py`에 연결한다. 결과는 `CAPSTONE_ARTIFACTS`별 새 디렉터리로 분리하고 검사 실패 시 비정상 종료한다.

## 검증과 게시

| 검사 | 결과 | 증거 |
| --- | --- | --- |
| 단위 계약 | 20/20 그룹 PASS, FAIL/SKIP 0 | `unit/results.json` |
| Python 도구·서버 재검증 | 2/2 그룹 PASS | `python-final/results.json` |
| 프로젝트 연결·컴파일 | PASS | `unity-initial/project-bindings` |
| EditMode | 161/161 PASS | `unity-initial/editmode.xml` |
| Android 출시 스크립트 | PASS, 개발 전용 메서드 8개 및 설정 진입 시 디버그 수집 제외 확인 | `unity-recheck/android-release-scripts/release-scripts-report.json` |
| 로컬 루프 | 39/39 PASS | `unity-recheck/local-loop/local-loop-validation/results.json` |
| Focus 통합 | 13/13 PASS, 버튼 709회·레이캐스트 712회 | `unity-recheck/focus-missions/focus-validation/report.json` |
| 모바일 UI | 11/11 PASS, 360×640·390×844·412×915, 버튼 79회·레이캐스트 80회 | `unity-recheck/mobile-ui/mobile-ui-validation/report.json` |
| 전체 코스 | 5/5 PASS, 전체 45종 분기, 버튼 631회·레이캐스트 634회 | `unity-recheck/all-courses/all-course-validation/report.json` |
| 실제 Play 저장·복원 | fresh/restore PASS, 실제 390×844, 원래 GameView 선택 복원 확인 | `play-gl/play-save-restore/synthetic-save/play-*.json` |

최초 `unity-initial`은 2 PASS/5 FAIL이었다. 기존 진단/완료 버튼/스크롤 전제, 독립 실행 시 캡처 디렉터리 누락, 기본 가로 Game 뷰 조건을 수정했다. `unity-recheck`는 5 PASS/1 FAIL로, 나머지 Play 실패는 Xvfb의 Vulkan 화면 출력(DRI3 미지원)으로 보고 전 종료된 경우다. OpenGL로 전환한 별도 `play-gl`에서 fresh/restore를 실제 실행·통과했고 임시 GameView를 복원했다. 이전 실패 로그를 삭제하거나 단위 성공으로 대체하지 않는다. 신규 캡처 215장은 위 Focus/모바일/코스 합성 fixture 결과다.

실제 `MockupMain` 장면은 8개 상태 × 360×640·900×1950의 **16장 PASS**다 (`scene-final/theme-validation.txt`). 낮/밤 홈·미션·내 과정·설정 위/아래·성장 후·건강 기록을 캡처했으며 장식의 입력 비차단과 저장 불변을 검사했다. 대표 8개 상태를 직접 검토하여 명백한 겹침·잘림·건강 그래프 가림이 없는 것을 확인했다. 전체 미션 화면의 사람 전수검토를 뜻하지 않는다.

공개 파일은 코드·메타데이터·문서만 포함하며 외부 아트/실제 사용자 데이터는 추가하지 않는다. commit 전 worktree/index 개인정보 검사와 diff 검사를 수행하고, 게시 직전 원격을 다시 조회한다. 정상 push 대상은 `HEAD:refs/heads/codex/integrate-garden-missions-20261029` 및 `HEAD:refs/heads/main`이다. 원격이 앞서면 덮어쓰지 않으며 force push·원본 기능 브랜치 이동·브랜치 삭제는 금지한다. 최종 커밋·게시 명령·원격 일치는 비공개 `workflow-record.json`과 `push.log`로 남긴다.

검증은 merge commit 생성 전 작업 트리에서 실행되었으므로 결과 JSON의 `commit=dae45c5`, `workingTreeDirty=true`는 통합 전 코드만 검사했다는 뜻이 아니다. 새 통합 작업 트리를 컴파일하고 검사한 기록이다. Editor/단위·출시 조건 컴파일과 실제 기기 실행은 구분한다. 이번에는 APK를 새로 빌드하거나 S24/A7에 설치하지 않았으며, 기기 터치/키보드·백그라운드·발열·실제 알림 전달은 남은 기기 QA다. 기존 타겟·임상 근거·새 M 개인화 정책·실사용자 등록 보류도 변경하지 않는다.

재실행은 라이선스가 있는 기존 계정의 HOME 환경과 새 출력 디렉터리에서 한다:

```bash
python3 scripts/qa.py --tier unit --output artifacts/integration-unit-new
python3 scripts/qa.py --tier unity --only project-bindings --only android-release-scripts --only editmode --only local-loop --only play-save-restore --output artifacts/integration-unity-new
python3 scripts/qa.py --tier visual --only focus-missions --only mobile-ui --only all-courses --output artifacts/integration-visual-new
```

Play 검증은 Xvfb에서 화면을 띄울 수 있는 OpenGL을 사용하며 임시 390×844 GameView 선택을 복원한다. Android 출시 검사는 실제 player 스크립트 DLL의 조건부 컴파일 검사로, 전체 APK 빌드의 대체가 아니다. 모든 Unity 실행은 `codex` 및 해당 계정의 HOME 환경이며 라이선스 파일을 변경하지 않는다. 합성 상태·화면·로그는 ignored `artifacts/integration-20261031/`에 보관한다.

## 후속 작업 기준

검증·게시 완료 후 팀원은 자기 변경을 먼저 보존하고 `git fetch origin` 후 자기 작업 브랜치에서 `git merge origin/main`한다. `misssion` 원본은 그대로 유지하므로 후속 개발 전에 새 통합 main을 가져와야 한다. 이미 포함된 과거 기능 브랜치를 다시 따로 합칠 필요는 없다.

[이전 통합과 원본 SHA·저장 호환 주의사항](BRANCH_INTEGRATION_2026-10-29.md)을 함께 참고한다. 원격 게시 후 복구가 필요하면 실제 merge의 첫 부모를 확인한 새 revert commit을 사용하며, 저장본 삭제·과거 데이터 덮어쓰기로 코드 되돌리기를 대신하지 않는다.
