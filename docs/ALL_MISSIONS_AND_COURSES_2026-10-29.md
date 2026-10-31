# 전체 M/P 미션과 2·4·8주 코스

작성일: 2026-10-29. 현재 구현 계약은 이 문서를 따른다. 종전 [4개 예시 구현 기록](FOCUS_MISSION_UI_IMPLEMENTATION_2026-10-29.md)은 이전 단계의 검증 이력이다.

> 2026-10-31: 아래 모바일 변경을 정원 테마 통합본과 결합했다. 이번 사용자의 추가 merge/push 요청에 따라 main을 갱신하는 기준·충돌·검증은 [후속 통합 기록](BRANCH_INTEGRATION_2026-10-31.md)을 따른다. 아래 main 유지 문장은 원본 작업 당시의 방침이다.

## 모바일 UI 후속 개선 · misssion

작업 전 상태는 `misssion`의 `1e60135`에 저장·푸시했다. 후속 UI 개선은 같은 브랜치에서 수행하며, 별도 게시 요청 전에는 로컬 변경으로 유지한다. `main`은 통합 머지 직후 상태로 유지한다.

| 사용자 목표 | 현재 동작 | 검증 기준 |
| --- | --- | --- |
| 모바일에서 읽고 누르기 | 화면 폭 기준 글자 크기, 독립된 본문 스크롤·하단 동작·오류 영역, 작은 높이에서는 여백 축소 | 360×640·390×844·412×915에서 첫 동작 접근, 영역 겹침과 재시도 확인 |
| 미션에만 집중 | 단계 숫자·진행 막대 제거, 현재 행동 제목만 표시, 누름 피드백·본문 전환, 움직임 줄이기 설정 | 전체 45개 수행·분기 경로와 숫자 표시 제거 확인 |
| 코스와 추천을 쉽게 선택 | 간결한 2·4·8주 선택, 상단의 코스 없이 추천 시작, 기본·추천 우선순위 분리, 추천 목록의 활동 유형·집중 권장 시간·분류 | 코스 미선택 상태의 추천 수행, 17개 전체 분류 접근 |
| 입력과 집중 동작 이해 | 필수·선택 표시와 가까운 도움말, 타이머 시작을 주 동작으로 표시, 수동 마치기 유지, 음성 시간·재생 상태, 그림 전체 지우기 후 복원 | 공백 입력·실패·재시도, 0초 수동 완료, 저장 시간, 그림 복원 확인 |
| 선택 기록을 생략 | 감정 8개 우선·24개 펼치기, 기록 없이 완료, 완료 후 정원·미션 목록·저장 기록으로 이동 | 실패 시 감정·메모 초안 유지, 성공 시 보상 1회 |
| 내 기록 다시 읽기 | 현재 코스 우선, 최근 저장 기록 우선, 전체 실습 순서와 수행 상세는 펼치기, 감정·글·그림·응답 먼저 표시 | 48회차 접근·코스별 분리·기록 불변 확인 |
| 시험 기능 분리 | 설정의 접힌 테스트 도구에 미션 기록 초기화·날짜 넘김·상세 진단, 초기화 범위 확인 후 실행 | Editor/개발 빌드만 구성, Android 출시 조건 컴파일, 실패 시 데이터 유지 |
| 상태와 가독성 | 진행 중 미션/가능한 기본 미션은 `!`, 선택 추천만 남으면 중립 점, 모두 마치면 체크. 정원 주야간 글자색 유지와 얇은 대비선. 장식색과 별도로 동작색 명도 보정 | 45개 버튼색의 흰색 대비 4.5:1 이상, 정원·미션 하단 탭 통일 |

이 변경은 저장 형식·코스 순서·보상 규칙을 바꾸지 않는다. 과정의 완료 회수는 기록 요약으로 유지한다. 감정과 메모는 개별 실습의 필수 입력과 구분한다. Figma 파일은 이번 모바일 구현 수정에 포함하지 않았으므로 화면 배치·동작 표기의 최신 기준은 Unity 구현이다.

검증 도구는 `MobileUiValidation`(11개), `AllCourseValidation`(45개 미션의 전 경로), `FocusMissionValidation`(기존 기능·설정·키보드 회귀)이며, 실제 사용자 저장본 대신 폐기 가능한 합성 상태를 사용한다. Android 조건 컴파일과 화면 크기별 Editor 검증은 실기기 실행 검증과 구분한다.

후속 검증 결과: 모바일 11/11(버튼 79회·입력 대상 80회), 기존 회귀 9/9(201회·201회), 전체 미션 5/5(631회·634회) 통과. 기존 저장 로직 1,640개와 전체 미션 로직 2,559개 확인도 통과했다. Unity Editor 컴파일과 Android 출시 조건 컴파일이 성공했고, 출시 조건 산출물에서 테스트 초기화·날짜 넘김 문구가 제외됨을 확인했다. 작은 화면·저장 오류·입력·낙서·감정·음성·완료·코스 기록 캡처를 검토했다. 최종 변경 파일 검사와 1,678개 파일의 개인정보 노출 검사를 통과했다. 실기기 터치·키보드·오디오 재생 검증은 이번 작업에서 실행하지 않았다.

## 범위와 화면 원칙

- 기본 실습 `P01–P28` 28개와 추천·자유 미션 `M01–M17` 17개, 총 45개를 사용한다. 영구 ID는 각각 `course:Pxx`, `free-mission:Mxx`로 유지한다.
- 미션 개요 → 한 화면에 한 행동씩 수행 → 완료 흐름을 사용한다. 수행 중에는 탐색 메뉴를 숨기고 미션별 색·중앙 그림·주 행동 버튼에 집중한다. 준비, 관찰, 집중, 선택, 입력, 확인 화면은 역할에 맞춰 구분한다.
- 타이머가 있는 단계는 수행 시간을 보존한다. 권장 시간이 지나도 자동 완료하지 않으며 사용자가 현재 행동을 확인해야 한다.
- 미션에는 기분·계획 입력, 실제 낙서 캔버스, 이야기 듣기, 걸음 확인 등의 조작을 연결한다. M07은 기분 입력 또는 `잘 모르겠어요`, M10은 짧은 계획 입력, M11은 유효한 그림을 남겨야 해당 단계를 완료한다. 코스의 선택적 회고 입력은 비워도 진행할 수 있다.
- P19의 산/소리 대체, P26의 도움/소리 대체는 선택한 경로만 보여준다. 이전 화면에서 경로를 바꾸면 이후 확인 이력을 다시 수행하도록 해, 이전 경로의 확인만으로 새 경로를 완료하지 않는다.
- 화면별 문구·유형·선택지는 [FocusMissionCatalog.cs](../Assets/Scripts/Runtime/FocusMissionCatalog.cs)를 기준으로 Figma와 Unity에 공통 적용한다. 이전 버전의 4개 예시 정의도 별도로 유지한다.

## 코스 선택과 추천 미션

코스를 아직 선택하지 않았으면 미션 탭에서 2·4·8주 선택 화면을 연다. 선택은 저장하며 이후 미션 탭은 해당 코스의 현재 순서를 연다. 코스 선택 화면에서도 추천 미션과 자유 미션 전체 목록에 접근할 수 있다.

추천 미션은 선택한 코스와 독립적이다. 코스가 없어도, 기본 미션을 진행 중이거나 오늘 완료했어도 수행할 수 있다. 17개 중 각 미션은 하루 한 번 신규 완료 보상을 받을 수 있고, 같은 날 같은 미션을 반복해도 보상을 중복 지급하지 않는다. 추천 미션 완료는 기본 코스의 순서를 앞당기지 않는다.

기본 코스는 하루 신규 실습 1회, 월요일–일요일 사이 최대 6회다. 다른 코스로 바꿔도 같은 날 기본 미션 완료 제한을 우회하지 못한다. 쉬는 날에는 진도를 잃지 않는다. 코스별 진도와 보상 영수증은 별도로 저장하고, 완료 미션 복습에는 추가 보상이 없다.

코스 변경은 이전 진도·기록·정원·잔액을 보존한다. 진행 중인 기본 미션이 있으면 먼저 이어서 하거나 참여를 종료한 뒤 다른 코스를 선택한다. 추천 미션 초안은 코스 선택을 막지 않는다. 현재 코스를 다시 선택하는 동작은 진행 중인 기본 미션을 유지한다.

## 새로 구성한 P 커리큘럼

아래는 이번 전체 M/P 구현을 위해 **새로 구성한 순서**다. 과거 `MBCT2Wxx`, `MBCT4Wxx`, `MBCTxx`를 P 번호로 이름만 바꾼 목록이 아니다. 기존 코스의 감각·몸·일상 → 잠시 멈춤·생각 → 돌봄·지속이라는 학습 순서를 참고해 P 실습을 배치했다. 과거 저장 ID와 데이터는 그대로 보존한다.

2주는 핵심 경험을 12회로 구성하고, 4주는 변형 실습까지 24회로 넓힌다. 8주는 P 28개를 모두 포함한 뒤 20회 반복 실습으로 일상 적용을 이어가며 총 48회다. 같은 P 번호가 다시 등장해도 각 회차의 단계·응답·시각·보상 결과는 별도로 남는다.

| 코스 | 권장 구간 | 실제 수행 순서 |
| --- | --- | --- |
| 2주 · 12회 | 1–6회 | P04 → P01 → P03 → P06 → P22 → P10 |
| 2주 · 12회 | 7–12회 | P02 → P11 → P17 → P21 → P27 → P28 |
| 4주 · 24회 | 1–6회 | P01 → P02 → P03 → P04 → P06 → P08 |
| 4주 · 24회 | 7–12회 | P09 → P10 → P11 → P12 → P13 → P15 |
| 4주 · 24회 | 13–18회 | P16 → P17 → P19 → P20 → P21 → P22 |
| 4주 · 24회 | 19–24회 | P23 → P24 → P25 → P26 → P27 → P28 |
| 8주 · 48회 | 1–6회 | P01 → P02 → P03 → P04 → P05 → P06 |
| 8주 · 48회 | 7–12회 | P07 → P08 → P09 → P10 → P11 → P12 |
| 8주 · 48회 | 13–18회 | P13 → P14 → P15 → P16 → P17 → P18 |
| 8주 · 48회 | 19–24회 | P19 → P20 → P21 → P22 → P23 → P24 |
| 8주 · 48회 | 25–30회 | P25 → P26 → P27 → P28 → P01 → P03 |
| 8주 · 48회 | 31–36회 | P02 → P10 → P23 → P06 → P05 → P09 |
| 8주 · 48회 | 37–42회 | P11 → P13 → P16 → P17 → P18 → P20 |
| 8주 · 48회 | 43–48회 | P21 → P22 → P24 → P25 → P27 → P28 |

이 표의 6회 구간은 권장 일정과 제작 대조를 위한 표시다. 내 과정의 기록 탐색은 주차별 묶음 대신 **2주 코스 / 4주 코스 / 8주 코스**로 나눈다.

## 기록과 이전 저장본 보존

- 새 기본 세션에는 `FocusProgramId`를 저장한다. 선택한 코스를 나중에 바꿔도 과거 기록이 새 코스로 이동하지 않는다.
- 내 과정에서 코스별 완료 수·전체 순서·회차별 수행 기록을 열 수 있다. 추천·자유 미션은 별도 묶음으로 본다. 단계별 안내, 실제 선택·입력, 확인 시각, 수행 시간, 그림, 감정·메모, 지급된 영양제를 기록한다.
- 이전 4개 예시의 `FocusVersion=1` 세션은 `이전 예시 과정`으로 구분한다. 기존 P23/P10/P25 반복 12회 순서와 당시 안내를 그대로 해석하며, 새 코스에 완료 수를 합산하지 않는다. 기록과 이어하기·복습을 유지한다.
- 이전 P/MBCT 카탈로그로 저장된 세션·진도도 삭제하지 않는다. 새로운 P 커리큘럼과 구별해 이전 기록에서 조회한다. 기존에 명시적으로 선택했던 기간도 보존한다.
- M15는 최초 목표·조회 시작/끝·개인화 근거·수면 조정을 해당 세션에 저장한다. 동기화·날짜 변경·재열기 이후에도 이 값을 다시 계산하지 않으며, 목표가 없는 수행은 완료하지 않는다. 진행 걸음은 저장된 조회 범위의 검증된 누적 관측값으로 표시한다.
- 저장 명령 실패 시 코스·진도·입력·시각·잔액을 이전 상태로 유지한다. 완료 재시도와 중복 클릭은 보상을 다시 지급하지 않는다.
- 설정의 미션 새로고침은 모든 미션 기록·진도·보상 영수증을 초기화하고, 정원 외형·성장·구매·잔액·선택 코스·시험 날짜는 보존한다. 날짜 넘김은 앱의 날짜만 바꾼다.

## 요구사항과 구현·검증 연결

| 요구사항 | 구현 계약 | 검증 |
| --- | --- | --- |
| 45개 M/P 전체 | `FocusMissionCatalog.All`, 버전별 `FindForSession` | 28 P / 17 M ID 고유성, 전체 정의·단계 검사 |
| 2·4·8주 분리 | `CourseCodes`, `MissionAt`, `TotalPracticesFor` | 12 / 24 / 48회 전체 완료 후 재열기 |
| 코스 미선택 시 선택 | `MbctProgramChosen`, `SelectMbctProgram`, `BeginFocusSession` | 선택 전 기본 시작 거부, 추천 시작 허용, 선택 저장 실패 원자성 |
| 코스와 독립적인 추천 | `AdditionalCompletedToday(state, missionId)` | 17개 당일 개별 지급, 반복 지급 차단, 코스 진도 불변 |
| 코스별 기록·복습 | `ProgramFor`, 세션 `FocusProgramId`, 코스별 `Completed` | 코스 전환·재가입·타 코스 복습·이전 예시 버전 보존 |
| 확인 순서와 대체 경로 | `ConfirmFocusStep`, `MoveFocusStep`, `IsApplicable` | 앞 단계 우회 차단, P19 소리/산 경로 전환과 시각 순서 |
| 걷기 목표 보존 | `EnsureFocusWalkingTarget`, `StartWalkingSession`, 기존 걷기 세션 필드 | 목표/조회 범위/근거 원자 저장, 재동기화·날짜 변경·재열기·완료 후 동일성 |
| 입력·그림·수행 시간 | `RequiredAnswer`, `FocusDrawingData`, `SaveFocusElapsed` | 빈 필수 입력·손상 그림 거부, 시간 단조 저장·재열기·실패 복구 |
| 초기화·날짜·정원 보존 | `ResetMissionHistory`, `AdvancePreviewDay` | 신규·구버전 기록 초기화, 외형·성장·잔액 보존, 실제 파일 실패 후 재시도 |

도메인 구현은 [LocalGardenState.cs](../Assets/Scripts/Runtime/LocalGardenState.cs), 화면 연결은 [WeekOneQuestDemo.FocusHome.cs](../Assets/Scripts/Runtime/WeekOneQuestDemo.FocusHome.cs), [WeekOneQuestDemo.FocusPractice.cs](../Assets/Scripts/Runtime/WeekOneQuestDemo.FocusPractice.cs)를 따른다.

## 확인한 검증 결과

모든 데이터는 합성 데이터이며 실제 사용자 저장본은 초기화하지 않았다.

| 검증 | 결과 |
| --- | --- |
| `scripts/test-focus-state.ps1` | 1,640개 통과: 구버전 회귀, 새 84회 전체 커리큘럼, 추천 17개, 입력·그림·경로·시각·보상·저장 실패 |
| `LocalGardenStateTests` | 324개 통과 |
| 기존 `MbctTests` | 1,511개 통과: 기존 2·4·8주 카탈로그·저장 진도 보존 |
| `GardenPlantAppearanceTests` | 232개 통과 |
| `MissionRecommendationTests` | 23개 통과 |
| `WalkingBaselineRefreshTests` | 30개 통과 |
| `ActivityNotificationLogicTests` | 42개 통과 |
| `WalkingPersonalizationTests` | Windows의 대응 시간대 ID를 사용하는 임시 테스트 사본으로 55개 통과. 원본의 IANA 시간대 ID는 이 Windows Mono 환경에서 조회 불가하므로 저장소 테스트 파일은 변경하지 않음 |

## Figma 통합과 최종 화면 검증

[전체 미션 인덱스와 코스](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=110-800)에서 M01–M17, P01–P28을 각각 찾을 수 있다. 개별 파일의 41개 미션을 단계별 구성으로 개편해 통합 파일에 이전했고, 기존 4개 예시를 포함한 **45개 미션 · 231개 개별 화면**을 확인했다. 개요·준비·수행·선택/입력·회고·완료를 필요한 만큼 나누며, 개별 페이지에 공통 감정·한 줄 기록을 복제하지 않는다. 색 토큰과 공통 버튼을 사용하며, 문구·아이콘·컨테이너를 편집 가능한 레이어로 유지했다.

통합 파일의 `Archive · 코드 기반 초기안` 페이지와 4개 예시의 오래된 구현 전 메모는 삭제했다. 이전 개별 미션 파일의 기존 47개 페이지도 삭제하고, 통합본으로 이동하는 안내 페이지 하나만 남겼다. 파일 항목 자체를 삭제하려는 브라우저 작업은 현재 URL 확인 실패로 컴퓨터 제어가 자동 중단되어 완료하지 못했다.

| 미션 | 이름 | 개별 화면 | Figma 페이지 |
| --- | --- | --- | --- |
| M01 | 주변 밝히기 | 3 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=105-770) |
| M02 | 창밖 바라보기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=105-841) |
| M03 | 물 마시기 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=84-770) |
| M04 | 세수하기 | 3 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=105-982) |
| M05 | 가볍게 몸 펴기 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=105-1056) |
| M06 | 물건 하나 정리 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=105-1155) |
| M07 | 기분 살피기 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=105-1251) |
| M08 | 음악 듣기 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=105-1346) |
| M09 | 잠깐 쉬기 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=105-1445) |
| M10 | 작은 계획 세우기 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=105-1552) |
| M11 | 자유롭게 낙서 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=105-1642) |
| M12 | 주변 식물 살피기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=106-770) |
| M13 | 괜찮았던 순간 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=106-896) |
| M14 | 짧은 글 읽기 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=106-995) |
| M15 | 일정 걸음 걷기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=106-1086) |
| M16 | 공원 방문 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=106-1240) |
| M17 | 안내 따라 호흡 | 4 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=106-1344) |
| P01 | 잠깐 멈춰보기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=106-1443) |
| P02 | 주변 소리 만나기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=106-1574) |
| P03 | 몸에 안부 묻기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=106-1702) |
| P04 | 한 모금 천천히 | 6 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=107-770) |
| P05 | 다시 돌아오기 | 6 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=107-932) |
| P06 | 익숙한 행동 새롭게 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=107-1101) |
| P07 | 나에게 편한 실습 찾기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=107-1234) |
| P08 | 마음에 이름 붙이기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=107-1391) |
| P09 | 감정과 몸 살펴보기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=107-1523) |
| P10 | 잠시 숨 고르기 | 6 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=85-770) |
| P11 | 생각이 왔음을 알아차리기 | 6 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=107-1656) |
| P12 | 작은 편안함 찾기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=107-1822) |
| P13 | 마음이 바뀌어도 괜찮기 | 7 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=107-1952) |
| P14 | 내게 맞는 방식 확인하기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=108-770) |
| P15 | 나에게 한마디 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=108-912) |
| P16 | 몸이 편한 자세 찾기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=108-1038) |
| P17 | 나를 돕는 작은 행동 | 7 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=108-1186) |
| P18 | 실습을 바꿔도 괜찮기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=108-1384) |
| P19 | 산 명상 경험하기 | 11 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=108-1527) |
| P20 | 오늘의 시도 인정하기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=108-1905) |
| P21 | 나만의 쉬는 방법 정하기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=108-2040) |
| P22 | 이동하는 감각 느끼기 | 6 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=108-2173) |
| P23 | 한입에 주의 기울이기 | 6 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=85-771) |
| P24 | 들리는 말에 집중하기 | 5 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=109-770) |
| P25 | 말하기 전에 잠깐 멈추기 | 6 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=85-772) |
| P26 | 사소한 도움 알아차리기 | 6 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=109-900) |
| P27 | 생활에 실습 연결하기 | 7 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=109-1067) |
| P28 | 다음 주의 나 돌보기 | 7 | [열기](https://www.figma.com/design/3CSQ40rpnnEYnwUt4NZrf8/?node-id=109-1279) |

### 최종 검증 근거

- `scripts/test-all-missions.ps1`: 2,559개 확인 통과. 전체 45개 미션, 8주 전체 회차, 경로 분기·그림·중간 저장·타이머·보상 재시도 포함.
- Unity 합성 상태/분리 미리보기 전체 미션 검증: 5/5 통과, 실제 버튼 콜백 627회·첫 입력 대상 판정 630회. 전체 45개 아이콘의 로드·비율과 수행 화면·완료 화면, 대체 경로를 확인했다.
- 기존 화면 회귀 검증 9/9 통과, 버튼 193회·입력 대상 판정 193회.
- 입력·선택·낙서 변형 검증 통과, 버튼 30회·입력 대상 판정 33회. 빈 낙서 영역의 첫 획과 저장한 그림 조회를 포함한다.
- 최종 걷기 시작의 저장 실패/재시도·최초 목표 고정·기존 자동 완료 우회 차단·기록의 목표/조회 기간·한국어 음성 자산 검증 통과, 버튼/입력 대상 판정 각각 14회.
- 런타임·Android 조건·Editor 자동화 코드 컴파일 성공. Android 실기기 실행 검증은 이번 변경에서 수행하지 않았다.
- SVG/PNG 45쌍의 원본 비율·비어 있지 않은 투명 영역·아이콘을 검사했다. P24는 직접 작성한 한국어 이야기와 설치된 한국어 합성 음성으로 만든 34.9초 음성을 포함한다.
- 사용자 저장 파일·운영체제 날짜는 변경하지 않았으며, 테스트 상태·화면·로그는 무시되는 `artifacts/`에만 보관했다.

구버전 저장본은 새 코드에서 호환 검사를 거쳐 유지한다. 변경을 되돌려야 할 때는 새로 저장한 v2 세션을 구버전 코드에 쓰기 전에 저장본을 별도 보관하고, 동일한 새 저장 형식을 이해하는 버전으로 복귀해야 한다.
