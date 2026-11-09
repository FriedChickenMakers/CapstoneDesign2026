# QA 후보 독립 리뷰 및 수정 추적 — 2026-11-09

GitHub Copilot의 `claude-opus-5.5`를 읽기 전용 독립 리뷰어로 실행했다.
리뷰어에게 구현자의 판단이나 다른 리뷰 결과를 전달하지 않았다. 리뷰는 정적
분석이며 아래 실행 검증은 별도로 수행한다. 최종 v5 core 검토에서는 P0–P2 없이
P3 세 건이 확인됐고 수정·재검증했다. 초기 다른 범위에서 P1/P2가 없었다는 뜻은 아니다. 원문·요청 범위·모델 사용 기록은
저장소 밖의 비공개 `~/.local/state/opus-review/`에 보관한다.

큰 UI 요청 두 건은 모델 입력 한도에서 거부되어 결과가 없었다. 성공으로
세지 않고 범위를 나눈 별도 요청으로 화면 코드를 검토했다. 서버 전체와
개인화 큐는 최초 커밋을 기준으로 별도 검토해 변경되지 않은 구현도 포함했다.

## 확인·수정 내역

| 영역 / 지적 | 판단·실제 조치 | 실행 근거 / 남은 확인 |
|---|---|---|
| P1 Health 걸음 조회 | timeout을 실제 coroutine 취소와 구분하고 finally에서 현재 세대의 busy를 해제한다. 초기화 전 조회 결과가 새 상태를 덮어쓰지 않는다. | native host timeout·취소·generation 회귀. 실제 Health Connect 공급자 응답 지연 검사는 별도 |
| P1 서버 삭제 대기 중 전체 초기화 | durable DeletePending을 먼저 읽는다. 삭제가 대기 중이면 등록·큐·요청을 보존하고 PENDING을 반환하여 네트워크 삭제를 계속한다. 읽기 실패도 보존한다. 서버 확인 후 로컬 초기화를 마친다. | 실제 Editor HTTP 업로드 → 철회 → 초기화 보류 → 서버 삭제 확인 → 초기화 완료 검사 |
| P2 노출 로그 재시도 | JNI 결과에서 영구 거부와 일시 실패를 구분한다. 원본 결정 ID·노출 시각을 보존하며 지수 backoff, 시도·회당 처리 한도를 적용한다. 용량 초과를 반복 손실로 누적하지 않는다. | native host 거부/저장 오류, Editor 클라이언트 검사. HTTP/event/file schema 변경 없음 |
| P2 초기화 대기 중 foreground service | bridge에서 새 요청을 차단하고, 이미 시작된 서비스는 foreground 계약을 충족한 뒤 demote·종료한다. | host에서 promotion·종료·상태 재생성 방지를 확인. 실제 Android deadline crash를 재현했다고 주장하지 않음 |
| P2 하단 탐색 위치 | 새 390×84 선반과 이전 Fit의 12pt inset이 충돌했다. Fit도 화면 양끝과 하단에 맞춘다. | 실제 렌더·raycast를 사용하는 Daily UI의 경계 검사 |
| P2 사진 기록 중복 | `ShowFocusRecord`의 두 번째 `BuildMissionPhotoRecord` 호출을 제거한다. | Unity 추가 회귀: 이미지 1개·삭제 조작 1개·삭제 효과 1회 확인 |
| P2 실습 종료 경로 부재 | 최신 안내 UI에서 담당자의 기존 중단·넘김 계약이 빠진 것이 확인됐다. 사용자 후속 지시에 따라 안내 화면에 복원한다. 하루 1+1·코인·부분 성장 제거는 유지한다. | v5 도메인 adjustments 967·contracts 135 PASS. Unity 팝업 pause/cancel·미루기·M10·목적 문구 검사 PASS |
| P3 불러오기 재시도 | 서비스 생성 전에 writer lock 확보가 실패한 경우에도 재시도에서 서비스를 다시 만든다. 정상 재열기 시 세션·알림·사진 정리 연결을 초기 진입과 맞춘다. | Unity preview에서 실제 파일 잠금 해제 재시도와 실패한 Open 재시도를 각각 검사. 활성 세션 복원·사진 prune 1회 확인 |
| P3 초기화 상태 문구 | 자동 재시도 중에도 현재 메시지로 표시를 갱신한다. | 보완 전 동결 소스의 full-reset-ui 8 checks/389 assertions 및 full-reset Play 80 assertions PASS. 실제 단말의 전체 초기화 반복 QA는 별도 |
| P3 성장 완료 문구 | 현재 정원의 성숙 여부 대신 해당 세션의 성장 receipt와 해당 식물의 마지막 단계를 기준으로 표시한다. | 보완 전 동결 소스의 mission-growth Play 134 assertions PASS. 이후 콘텐츠 표시 수정은 별도 최종 12개 회귀로 확인 |
| P3 사진 보너스만 있는 완료 | **미채택·조건 변경 원복.** `CameraMissionPolicy`/`StateCodec`은 기본 보상 0 + 사진 보너스 1 상태를 거부한다. 처음 시도한 표시 조건 수정을 되돌리고 해당 불변식을 유지했다. | Unity에서 유효한 50+1=51 표시와 잘못된 단독 보너스 상태 거부·원래 상태 불변을 검증 |
| P3 구버전 실습 시간 추천 | 현재 v5 단계 이름으로만 찾던 경로를 저장 세션 정의를 받는 overload로 수정한다. v2/v3 `breath` 등 기존 키와 같은 버전의 확정 피드백을 사용한다. | 실제 legacy 재개에서 17초 추천 유지, 수행 시간 0 유지, 다음 저장/재시작 검사 |
| P3 저장된 목적/실제 시작 불변식 | v3/v5에서 다른 목적 fallback을 기본 회차 완료로 주장하는 저장과 준비 단계의 실제 시작 마커를 거부한다. schema에 맞게 해석한 정의를 사용한다. | 손상된 저장 거부, 원본 `a05634c` writer의 schema1 guided fallback 완료는 v4 의미로 보존. 새 v5 의미를 소급 적용하지 않음 |
| P3 종료된 guided 세션의 늦은 callback | 멱등 성공은 active 또는 이미 RewardCommitted일 때만 반환한다. | Ended에 normal/shortening callback 모두 실패·상태 불변, 정상 보상 뒤 중복 callback 성공·상태 불변 |
| 과거 texture-v3 P25 제목/본문 잘림 | 새 짧은 설명용 헤더 높이가 역사 세션의 긴 문구와 맞지 않았다. v3 P25에 원래 확장 헤더 공간을 적용하고 글을 삭제하지 않는다. | Unity의 실제 레이아웃·preferredHeight 검사와 `qa-texture-v3-p25-header.png` |
| P21/P28 잠시 멈춤의 관찰 기준점 누락 | v5에서 몸이 닿는 감각을 관찰하도록 실제 화면·카탈로그 문구를 보완하고 편안해질 의무 없음을 명시했다. v4·단계·시간·선택값·저장 버전은 유지한다. | 두 미션×v4/v5×두 해상도를 포함한 최종 Unity 12개/256 assertions PASS. 임상적 충분성을 입증한 수정은 아님 |
| P19/P26 대안 P02의 역할 표기 | `FocusSupport=true`인 별도 소리 실습에 `추가 활동`을 표시한다. 일반 P02 기본 활동의 v4/v5 표기는 유지한다. | 최종 Unity 회귀에서 원 회차 미완료·무보상, 별도 P02 완료 후 추가 슬롯만 점유하는 동작과 표시를 확인 |
| 로드 재시도 검사 자체의 scene 간섭 | 합성 검사에서 활성 scene을 바꾸지 않고 preview scene을 사용한다. 기존 scene·카메라 위치·조명·환경 변수를 복원한다. | 같은 Unity 추가 회귀의 실제 writer lock/Open 재시도 PASS. 제품의 저장 검증을 완화한 수정 아님 |
| P2/P3 QA 등록 verifier | 등록 파일 exact hash 외에 queue의 endpoint/token fingerprint를 대조한다. 활성 등록만 검사하는 `expectedEnabled=true` 계약을 명확히 하고 새 출력 폴더를 요구해 오래된 소스 재사용을 차단한다. | 오프라인 preflight는 `--verifier-apk`를 필수로 요구하고 고정 QA 패키지·서명을 확인. 최종 non-debug APK 오프라인 검사와 A7/API29의 등록 hash·큐 binding·수집 시작 경계 보존 PASS |
| A7 등록 verifier의 Android parser 거부 | Android `Xml.newPullParser`가 DOCDECL 처리를 기본으로 켜므로 정상 XML 입력 전 거부되던 도구 결함이다. 입력 전에 명시적으로 끄고 설정을 재확인하며, DOCDECL/DTD 입력은 계속 거부한다. | SDK33 host 정상 XML 2건 실패 재현 → host 10/10 PASS → 실제 A7/API29 PASS. 제품 APK는 변경하지 않음. `artifacts/qa-readiness-1109/verifier-parser-fix-result.json` 및 최종 실기기 result |
| P3 기기 시계 복귀 시 Behavior 큐 손실 | 미래 행을 삭제하지 않고 보류하며, 뒤에 있는 정상 행부터 전송한다. 재시작·ACK·시간 회복 시 ID와 발생 시각이 유지된다. | C# 회귀 및 서버 원자적 거부/정상 행/원본 재수락 검사. 기기 시계가 계속 서버보다 5분 이상 앞선 상황은 미해결 |

## 수정하지 않은 지적과 이유

| 지적 | 확인한 범위와 판단 |
|---|---|
| schema1 + texture v3가 guided v4로 잘못 이행될 가능성 | 정상 writer 이력에서는 schema2 도입이 texture v3 도입의 조상이다. 원본 브랜치가 만든 P01/M10 fixture는 schema2이며 이행·재열기·수정이 통과했다. 알려진 정상 저장에서 해당 조합이 생성되지 않아 추측에 따른 migration 변경은 하지 않았다. 임의 가공 저장까지 보장한다는 뜻은 아니다. |
| P24 오디오 자연 종료 뒤 글 보기/복귀 시 0:00 가능성 | 정적 검토에서 제기된 가정이며 실제 재현은 미수행이다. 현재 저장·보상·수집 blocker로 확인하지 않았고, 별도 UX 재현 항목으로 남긴다. |
| 비활성 모드 초기화의 4시 경계 상수 | 현행 제품의 ResetHourLocal은 4이며 scene/prefab override가 없다. 사용자 설정으로 경계를 바꾸는 기능은 현재 없으므로 제품 오류로 재현되지 않았다. 향후 reset hour를 변경하면 이 API도 함께 수정해야 한다. |

## 실행 검증과 남은 범위

- 최종 v5 host gate: **39/39 PASS, FAIL 0, SKIP 0**. Python harness 66 tests 및 서버 58 tests를 포함한다. 근거: `artifacts/qa-release-20261109/host/v5-final02/results.json`.
- Unity 추가 통합 회귀: **10/10 PASS, 158 assertions**, 실제 버튼 21회·raycast 21회. 근거: `artifacts/qa-release-20261109/unity/qa-regression-v5-final/qa-integration-validation/report.json`.
- UnityWebRequest와 격리 서버의 수집/철회/초기화: **클라이언트 37 + 서버 2 checks PASS**, cleanup 완료. 근거: `artifacts/qa-release-20261109/unity/personalization-final/result.json`.
- v5 도메인 수정: adjustments 967 / contracts 135 / 원본 저장 호환 95 / guided 437 / 카메라·추천 326 PASS. 원본 `3ac0c80`과 `a05634c` writer로 생성한 합성 저장을 사용한다.
- 7종 통합 UI/Play는 **617,024 assertions·PNG 717개, 7/7 PASS**다. 직접 확인한 근거는 `artifacts/qa-release-20261109/unity/integrated-ui-v5-reviewed-final/final-seven-summary.json`이다. 이는 P25 hint 수정 후·P21/P28 마지막 문구 보완 전의 동일 동결 소스 결과다.
- 마지막 표시 보완 후 Unity 추가 회귀는 **12/12 PASS, 256 assertions, 버튼 27회·raycast 27회**다. P21/P28의 관찰 기준점과 v4 문구 보존, P19/P26→P02의 추가 활동 표기·추가 슬롯만 점유·원 회차 유지가 포함된다. 근거: `artifacts/qa-release-20261109/unity/qa-regression-final12/qa-integration-validation/report.json`.
- 최종 **non-debug `.2`/2026110903 APK**의 오프라인 검사와 A7/API29 인플레이스 교체가 PASS다. 기존 정원 파일 6개·등록 hash·큐 binding·수집 시작 경계 보존, 원래 기기 설정 복원을 확인했고 기존 제품 앱은 건드리지 않았다. 근거: `artifacts/qa-release-20261109/candidate-apk-preflight-final/result.json`, `device-qa/final-verified/result.json`. 최초 verifier의 Android parser 오류는 검증 도구에서만 수정했으며 제품 APK는 변경하지 않았다. 임시 bootstrap 화면이나 잠금화면 캡처를 기능 PASS로 확대하지 않는다.
- 최신 사용자 지시로 서버 접속 조사를 추가했으나 지정 계정의 SSH 공개키 인증이 거부되어 원격 조사·전용 디렉터리 생성은 미실행이다. 정확한 로그인 계정·공개키 등록 확인 후 진행하며, 외부 HTTPS 수집 검증은 아직 미수행이다. 실제 참가자 수집 전 검증은 여전히 필요하며 실제 A/B 시험은 미수행이다.

이 값들은 서로 다른 검사 단위이며 합쳐서 참가자 수나 사용자 효과 크기로 제시하지 않는다. 7종 통합 UI는 마지막 표시 보완 전 동결 소스, 최종 12개 회귀는 보완 후 소스, APK/단말 검사는 `.2`/2026110903을 대상으로 한다. 이전 snapshot 성공을 최종 변경 전체의 재실행으로 확대하지 않는다. Git은 `misssion` 로컬 merge 중으로 commit/push하지 않았다. 게시에는 현재 AGENTS의 별도 명시적 요청 규칙이 적용된다.

## 평가 해석

알림 예약·OS 게시 요청 수락·클릭은 별도 사건이다. 같은 boot의 monotonic
시각으로 지연을 비교할 수 있는 자료와 벽시계 품질을 구분한다. OS 수락은
사용자가 실제로 보았다는 증거가 아니다. 합성 persona·Editor/단말 기능
검사는 실제 참가자의 지속 사용·임상 효과·A/B 효과가 아니다.
기기가 서버보다 5분 이상 앞선 경우의 배치 거부와 Behavior 큐의 완료 watermark/손실
counter 부재는 남은 측정 제한이다. 미검증 관측 구간을 성과 근거에 포함하지 않는다.

실제 A7의 격리 native APK에서 측정 생성 24개·새 프로세스 복원 16개 검사가
통과했다. 알림 검사는 가상 시각/5개 persona를 사용한 476개 검사가 통과했다.
두 실행의 화면 꺼짐 시간과 밝기 복원도 확인했다. 이 결과는 최종 Unity APK의
HTTPS 전송 검증과 구분한다.

최종 배포 판단, 정확한 후보 APK·소스 기준 및 후속 검증은
[QA 배포 준비 기록](QA_RELEASE_READINESS_20261109.md)에 합친다.
