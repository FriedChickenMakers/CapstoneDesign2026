# 콘텐츠 출처 및 미정 결정

2026-10-03 후속: 이 문서의 Notion 4주 카탈로그는 이전 기록 조회를 위해 보존한다. 새 활동 화면은 MBCT 원리를 참고한 8주·48회 코스를 사용한다. 현재 구현 기준은 [MBCT 구현 문서](MBCT_IMPLEMENTATION.md)와 Docs 저장소의 `기획서/MBCT 활동 설계.md`를 따른다.

상태: IMPLEMENTED (정의 및 메타데이터). 실제 사용자 대상 콘텐츠 검토와 음원 탑재는 NOT_TESTED / 미완료.
조회 기준: 2026-09-23 실행에서 Notion 연결 도구로 아래 페이지 본문을 읽었다. 모두 Notion `verification.state=unverified`; 응답에 잘림 또는 unknown block 경고가 없었으며 코스 28행을 확인했다. 수정 날짜는 출처의 최신성 단서이며 팀 확정 또는 전문가 검토의 증거가 아니다. 내부 페이지 주소와 고유 ID는 저장소에 게시하지 않으며, 원문은 팀의 비공개 자료 목록에서 찾는다.

## 직접 읽은 자료

| 출처 | 앱 내 출처 식별자 | Notion 수정 날짜 (UTC) | 구현한 정의 |
| --- | --- | --- | --- |
| 게임 기획 (09.18) (팀 내부 자료) | `source:team-course-draft` | 2026-09-22 | course:P01–P28, 주차 주제, 선택 돌아보기, 권장 시간, 음원 A–E 후보 |
| 미션 리스트 (팀 내부 자료) | `source:team-free-missions` | 2026-09-18 | free-mission:M01–M17, 원문 실천 안내 |
| 식물/환경 리스트 (팀 내부 자료) | `source:team-garden-catalog` | 2026-09-18 | plant:P01–P06, environment:E01–E06, 후보 동물 연결 |
| 동물 리스트 (팀 내부 자료) | `source:team-animal-catalog` | 2026-09-18 | animal:A01–A12, 후보 환경, 자율 행동, 탭 반응 |

Google Docs Proposal은 이 콘텐츠 작업에서 읽거나 변경하지 않았다. 외부 음원 페이지의 권리 조건을 독립 검증하거나 음원을 다운로드하지 않았다. 이 표에 없는 외부 자료를 직접 읽은 출처로 표시하지 않는다.

## 정의와 런타임의 경계

`Assets/Scripts/Runtime/MindfulnessContent.cs`는 Unity 의존성이 없는 직렬화 가능한 정의와 앱 기본 목록이다. `id`는 유형 접두사가 있는 영구 식별자이고 `sourceId`는 원문 ID이다. 예를 들어 `course:P01`과 `plant:P01`은 서로 다른 대상이다. 제목, 번역, 배열 순서, 콘텐츠 버전은 저장 데이터의 식별 키가 아니다.

코스 `instructions`와 `optionalReflection`은 원문 표의 지침과 질문을 보존한다. `textOnlyInstructions`는 음원이 준비되지 않은 상태에서도 사용할 수 있도록 별도로 작성한 개발용 텍스트 대안이다. 외부 음원 대본, 번역, 임상적으로 검증된 안내라고 표시하지 않는다. 특히 P19는 산 명상의 대본을 만들지 않고 기획서가 허용한 소리 관찰 대안을 안내한다. 사용자 대상 배포 전에 원문 자체 안내와 이 대안을 모두 검토해야 한다.

`suggestedDurationLabel`은 표의 자료/시간 표현을 보존한다. min/max초는 그 권장 범위이며, `suggestedDurationSeconds`는 타이머 UI용 상한값이다. 시간은 완료 조건이 아니다. P19의 4–9분 범위는 서로 다른 선택지(C와 E)를 합친 범위이고, 원문 레이블은 그 구분을 유지한다. 자유 미션 시간은 미정이므로 0이다. 0을 강제 즉시 완료로 해석해서는 안 된다.

`alternativeMissionIds`는 비어 있다. 원문은 호흡 → 소리 등의 대체 방식을 제시하지만 저장용 대체 코스 ID를 확정하지 않았으므로 임의 ID 연결을 만들지 않았다. `audioReference`는 명시된 A–E 후보 코드이며 선택형 행에서 코드를 지정하지 않은 경우 빈 값이다.

동물 `candidateEnvironmentIds`에는 원문에 제시된 식물과 환경의 typed ID가 함께 들어간다. 이름을 ID로 연결하는 것은 이번 정적 카탈로그 작성 시에만 수행했다. 런타임 저장/자격 판정은 typed ID를 사용해야 한다. 후보 연관은 필수 조건의 AND/OR 판정, 성장 단계, 등급 또는 확률이 확정되었다는 뜻이 아니다.

정의는 재화, 코스 진도, 동물 추첨 또는 보상 지급 상태를 소유하지 않는다. 정의 목록 갱신이나 버전 변경은 진행 초기화 또는 재추첨의 이유가 될 수 없다. 서버 동기화/캐시는 이번 정의 작업에 포함되지 않은 후속 항목이다.

## 음원 상태

A: Three minute breathing (3:35, Peter Morgan), B: Four minute body scan (4:01, Melbourne Mindfulness Centre & Still Mind), C: Three minute mindfulness of sounds (3:02, Peter Morgan), D: Three Step Breathing Space (3:34, Peter Morgan), E: Mountain meditation (8:12, Peter Morgan).

위 메타데이터는 Notion 표에서 옮겼다. 참조 URL은 [Free Mindfulness](https://www.freemindfulness.org/download)이며 로컬 파일 또는 재생 가능한 직접 주소가 아니다. 전 항목 `bundled=false`, 미션 `audioAvailable=false`, `NOT_BUNDLED_CONTENT_AND_LICENSE_REVIEW_PENDING`이다. 재생 UI는 준비되지 않았음을 표시해야 하며 무음 타이머를 음원 재생으로 표시해서는 안 된다.

Notion은 CC BY-NC-SA 3.0을 언급하지만 이번 작업에서 각 파일의 권리, 앱 배포 조건, 번역/편집 허용 또는 상업성 해석을 확인하지 않았다. 전체 청취, 한국어 안내 검토, 출처/라이선스/수정 표시 검토 후 실제 탑재 여부를 결정한다. [Mindfulness Daily](https://courses.tarabrach.com/courses/mindfulness-daily)는 Notion이 밝힌 주제 흐름 참고 자료이며 그 강의나 대본을 복제하지 않았다.

이 코스는 자체 4주 초안이다. 정식 8주 MBSR, 원래 40일 강좌의 번역/재현 또는 치료 효과가 검증된 자체 프로그램으로 표시하지 않는다.

## 팀 결정이 필요한 값

- 일일 갱신 시각과 시간대 변경 규칙.
- 코스 보상량, 일일 한도, 반복/건너뛰기 규칙, 자유 미션과의 의미상 중복 처리.
- 중단 참여의 보상과 단계 이동: 직접 완료와 자동으로 동일시하지 않는다.
- 식물 성장 비용/단계/효과, 환경 비용/적용 범위/위치 규칙.
- 동물 종별 등급, 조건 조합, 확률, 체류 시간, 재방문 변주.
- M15 목표 걸음 수: 원문도 미정이며 센서로 완료 인증을 강제하지 않는다.
- 한국어 음원 제작/탑재 범위, 각 자료의 권리 및 전문가 검토.
- 서버 정의 갱신과 캐시, 버전 호환성 정책.

개발 밸런스가 필요한 곳에서는 별도의 `DemoBalanceConfig` 등 DEV_DEFAULT 설정을 사용한다. 이 카탈로그에는 비용/확률/보상 숫자를 넣지 않았다. DEV_DEFAULT는 위 원문에서 확정된 값이라고 문서화해서는 안 된다.

## 검증 경계

원문 표의 ID/제목/지침/질문과 생성 카탈로그 대조, 수량과 ID 유일성, 동물/환경 연결의 참조 무결성 등 58개 정적 검증이 통과했다. 결과는 `artifacts/content-validation/source-comparison.json`에 있다. 기존 Unity 설치의 Mono C# 컴파일러로 이 파일 단독 라이브러리 컴파일도 통과했다 (`artifacts/content-validation/MindfulnessContent.dll`). Unity Editor를 실행하지 않았으며 Unity 통합 컴파일 및 게임 자동 테스트 실행은 상위 통합 작업의 결과를 따른다. 이 문서만으로 게임 루프, 음원 재생 또는 실제 기기 검증을 통과했다고 간주하지 않는다.

## Proposal 접근 결과

루트 작업에서 2026-09-23 Google Docs의 지정 Proposal 탭 URL을 웹 도구로
열었으나 `not accessible via this tool`로 본문을 읽지 못했다. BLOCKED(해당
자료만). 접근 가능한 최신 Notion 원문과 인수 문서로 진행했으며 기존
Flutter/AWS 기술 스택으로 이전하거나 팀 문서를 수정하지 않았다.
