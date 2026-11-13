# QA 앱의 자동 등록·로그 수집

2026-11-10 추가 변경이다. 서면 동의를 확인한 대상에게 배포하는 Android APK는 첫 LIVE 실행에서 설치별 난수 token과 수집 시작 경계를 저장한다. 기기마다 ADB로 등록 파일을 넣는 절차를 없애며, 인터넷이 없으면 로컬에 기록하고 연결 복구 후 등록·전송을 이어간다. 앱 내 동의 화면은 추가하지 않는다.

**자동 등록의 단위·Unity·외부 서버 검증과 별도 A7 debug probe의 오프라인 첫 실행→백그라운드 수신을 확인했다.** probe의 원래 방문 사건 1/1건이 같은 payload로 도착했고 중복은 0건이다. 표준 non-debuggable QA 1011 APK 빌드와 정적 preflight도 통과했다. 최종 APK는 기기에 설치하지 않았으며 probe 결과를 그 최종 APK의 실기기 검사로 표시하지 않는다. A7 1008과 S24 1010의 기존 수동 등록·수신 성공도 별도 과거 결과다.

## 설치와 수집 시작

| 상황 | 동작 |
| --- | --- |
| 신규 설치의 첫 LIVE 실행 | 암호학적 난수 32 bytes를 64자리 소문자 hex token으로 만들어 앱 private 저장소에 먼저 기록한다. 기기 serial·Android ID·광고 ID는 쓰지 않는다. |
| 첫 실행이 오프라인 | 서버 등록을 기다리지 않고 정상 저장된 설치 ID에 로컬 큐를 연결한다. 수집 시작 이후 자료를 보관하고 연결 복구 때 전송한다. 첫 실행 이전 기록을 소급 수집하지 않는다. |
| 기존 수동 등록 기기의 업데이트 | 기존 `personalization-enrollment.json`과 token을 그대로 사용한다. 수동 등록에 자동 등록 키를 덧씌우거나 ID를 바꾸지 않는다. |
| 자동 등록 기기의 일반 업데이트·재시작 | 기존 token·수집 경계·전송 대기열을 유지한다. 이미 등록된 token의 정상 업로드에는 등록 API를 다시 호출하지 않는다. |
| 앱 삭제·데이터 삭제 후 새 설치 | 이전 앱 private ID를 복원하지 않으므로 새 ID가 만들어진다. 같은 사람의 이전 참여와 자동 연결할 수 없다. |
| 앱 내부 로컬 초기화 | 기존 reset marker로 자동 재등록을 차단한다. 서버 삭제 대기 중에는 해당 삭제 요청과 credentials를 먼저 보존한다. |
| 철회·서버 삭제 대기 또는 완료 | 새 ID로 자동 대체하지 않는다. 이미 폐기된 token의 등록·업로드를 거부한다. 최초 서버 등록 전 철회도 별도 업로드 없이 처리하는 경로를 검증했다. 재설치를 통한 같은 사람의 재참여까지 영구 차단하는 기기 식별 기능은 없다. |
| 기존 큐의 credentials만 사라진 상태 | 새 token을 만들어 이전 자료의 귀속을 바꾸지 않고 수집을 중지한다. 저장 오류 진단 대상이다. |

`personalization-enrollment.json.new`에 flush한 뒤 최종 파일로 원자 이동한다. 완성된 `.new`는 동일 token으로 복구한다. 아직 사용된 적 없는 잘린 JSON·필드 누락 파일만 지우고 재생성한다. 유효하지만 다른 서버·키에 속한 `.new`와 읽기 실패는 보존한다. 최종 파일 저장이 확인되기 전에는 큐를 활성화하지 않는다.

## 앱·서버 계약

1. 앱의 private 등록 파일은 `endpoint`, `token`, 자동 생성인 경우 `automaticEnrollmentKey`를 담는다. 기존 수동 파일은 앞의 두 필드만 유지한다.
2. native WorkManager가 HTTPS `/measurements/batch`를 시도한다. 서버의 최초 `401`과 자동 등록 키가 있는 경우에만 같은 원점의 `POST /enrollments`를 호출한다.
3. 등록 헤더는 `X-Enrollment-Key`, JSON은 정확히 `token`, `app_version`이다. 서버의 `200` 및 `registered: true`를 확인한 뒤 **동일 ID·내용의 원래 batch를 한 번 재시도**한다. 새 사건이나 새 token을 만들어 재시도하지 않는다.
4. 서버는 token의 SHA-256과 난수 subject의 매핑을 SQLite에 보관한다. 기존 수동 매핑을 유지하며, 동일 token의 재요청·동시 요청·서비스 재시작은 같은 subject로 귀속한다. token 평문을 자동 등록 DB에 저장하지 않는다.
5. 등록 실패·timeout·잘못된 응답·429·5xx는 전송 성공이 아니다. 미확인 사건은 큐에 남고 기존 backoff로 재시도한다. redirect를 따라 credentials를 다른 서버에 보내지 않는다.
6. reset epoch·LIVE 상태·등록 binding·collection을 다시 확인하여 철회 이후 도착한 응답이 새로운 수집 구간을 활성화하거나 삭제하지 못하게 한다. 최초 등록 전 철회는 `DELETE /participant`에 해당 설치의 bearer와 `X-Enrollment-Key`를 함께 보내며, 서버가 token 등록과 폐기를 같은 transaction에서 처리한다. 측정자료 업로드나 일시적인 수집 재활성화가 필요하지 않다. 기존 수동 등록의 삭제는 bearer만 사용하고, 폐기 후 늦은 등록·업로드도 거부한다.

서버의 새 등록 요청 본문은 최대 1,024 bytes다. 신규 등록은 서비스 전체에서 시간당 240개·하루 1,000개로 제한하며 이미 등록된 token의 재시도는 새 등록 한도에 넣지 않는다. 학교의 공용 NAT IP를 참가자 한 명으로 취급하지 않는다. 정책 변경 시 배포 코드와 이 설명을 함께 갱신한다.

측정 종류·기존 payload·미션·보상 정책은 바꾸지 않는다. native 측정의 연결 후 백그라운드 전송과 기존 C# 추천 특징 `/events/batch`의 전경 전송을 구분한다. 저장 한도·90일 만료·강제 중지·OEM 절전 지연은 [전송 안정성 문서](QA_TELEMETRY_RELIABILITY_20261110.md)의 기존 경계를 따른다.

## 빌드·서버 설정

| 위치 | 설정 |
| --- | --- |
| 빌드 OS 사용자 | Unity 라이선스가 있는 `codex`와 그 HOME 환경 사용 |
| 기본 private build config | `/home/codex/.config/capstone/telemetry-build.json`; 다른 실행 계정에서는 해당 계정의 home 기준 |
| 별도 경로 지정 | `CAPSTONE_TELEMETRY_BUILD_CONFIG`에 저장소 밖의 절대 경로 지정. 대소문자 차이를 이용한 저장소 내부 경로와 경로의 symlink/junction을 거부 |
| build config 내용 | JSON의 `endpoint`와 `automaticEnrollmentKey`만 사용. HTTPS 원점과 64자리 소문자 hex 공유 등록 키. 설치별 `token`을 넣으면 빌드 거부 |
| Android 빌드 | 일반 Unity Build와 QA 빌드 모두 build callback이 설정을 검증한다. 설정이 없거나 잘못됐으면 조용히 수집 없는 APK를 만들지 않고 빌드 실패 |
| APK 내 설정 | 생성된 Gradle project의 `src/main/assets/capstone-enrollment-config.json`에만 삽입. 프로젝트 `Assets`에 비밀값 파일을 생성하지 않으며 incremental build 때도 갱신 |
| 서버 | `CAPSTONE_ENROLLMENT_KEY_FILE`이 가리키는 소유자 전용 파일에 같은 공유 키 저장. 미설정이면 등록 route는 404로 닫힘. 설정 파일이 잘못되면 서버 시작 실패 |

private build config와 서버 키 파일은 소유자만 읽을 수 있도록 관리한다. 값·원본 파일·기기별 등록 파일을 Git, 명령행 인자, 채팅, 보고서에 붙이지 않는다. APK와 생성된 Gradle 캐시에도 공유 키가 포함되므로 공개 저장소 산출물로 취급하지 않는다.

공유 등록 키는 허가되지 않은 단순 요청을 막기 위한 **최소 장치**다. APK에서 추출할 수 있으며 강한 기기 인증·앱 무결성 증명·자동화 공격 방지를 제공하지 않는다. 모든 기기의 실제 업로드는 서로 다른 설치별 bearer token을 사용하고 개별 철회가 가능하다. RSA 개인키를 공통으로 넣지는 않는다.

공유 키를 바꾸면 기존에 등록된 설치의 업로드는 계속되지만, 이전 키를 private 등록 파일에 저장한 미등록 설치는 신규 등록하지 못한다. **일반 APK 업데이트는 기존 등록 파일을 보존하므로 새 APK 설치만으로 이 키가 바뀌지 않는다.** 운영자는 같은 token·큐를 유지한 채 해당 private 파일의 키를 갱신하거나 별도 migration을 준비해야 한다. 이번 변경에는 자동 키 migration을 추가하지 않았다. 키 변경 전 미등록·오프라인 참가자를 먼저 확인하고, 새 token 발급으로 기존 자료 귀속을 바꾸지 않는다.

## 서면 동의와 QA 운영

- 배포 담당자는 APK 전달·첫 실행 전에 서면 동의를 확인한다. 앱 자체는 종이 동의 여부를 판별하지 않으며 설치별 운영자 등록 단계가 이를 대신해 주지도 않는다.
- 안내문에는 첫 LIVE 실행부터 로컬 측정이 시작되고 연결되면 외부 HTTPS 서버로 전송됨을 적는다. 전송 항목·목적·철회·보관 설명은 [측정 계획](QA_BETA_MEASUREMENT_PLAN.md)을 사용한다.
- 명부와 난수 subject를 연결하면 엄밀히는 가명 자료다. 실제 이름·연락처를 수집 API에 추가하지 않는다. 명부·운영 자료와 분석 자료는 별도로 관리한다.
- 알림 게시·클릭 지연, 참여·재방문 통계에서 운영자 QA subject와 모집한 참가자를 구분한다. 실제 기기에서 만들어진 `live` 사건이라도 설치 확인·장애 시험 자료를 사용자 효과 분석에 넣지 않는다.
- 측정자료의 90일 정리와 등록 해시·철회 표식의 보관 목적은 다르다. 철회된 token의 재활성화를 막는 표식을 측정자료 삭제와 함께 지우지 않는다. 별도 명부·수동 export의 삭제 운영도 필요하다.
- 설치 안내의 완료 기준은 앱 실행만이 아니라 해당 버전의 외부 서버 수신과 저장 오류 여부 확인이다. 오프라인 최초 실행에서는 즉시 도착을 요구하지 않고 재연결 후 같은 사건의 수신을 확인한다.

## 검증 상태와 배포 전 확인

| 검사 | 현재 확인 상태 |
| --- | --- |
| C# 저장·개인화 | 저장 helper 55 assertions와 기존 개인화 336 assertions PASS. 최초 생성, 재시작, 저장 실패·손상 복구, 동시 호출, 수동 등록 보존, reset/철회 차단 입력, 잘못된 설정 |
| 실제 Unity 자동 등록 클라이언트 | 22 checks PASS. 실제 Awake/재생성·초기 활성화·오프라인 큐·수동 등록·철회·실패 복구. 합성 fixture이며 네트워크 요청 없음과 static state 복원도 확인 |
| 기존 Unity 수동 등록 회귀 | 실제 UnityWebRequest 및 격리된 서버에서 client 42·server 2 checks PASS. 기존 등록·전송·철회·삭제 동작 유지 |
| native 등록·전송 | 58/58 PASS. 최초 401→등록→동일 batch 재시도, 오류·부분 ACK·철회/reset 경합 등. host/native APK 검증이며 실기기 검사는 아래와 구분 |
| 서버·실제 외부 HTTPS | 서버 89/89, 외부 프로토콜 31 checks, 배포 후 상태 17 checks PASS. 기존 수동 등록 보존, 등록 전 철회, 중복·재시작·폐기 후 재등록 거부·DB 상태 확인 |
| 실제 A7의 신규 자동 등록 | 별도 debug probe 1011에서 PASS. 초기 알림 설정까지 오프라인 완료 후 방문 1건 로컬 저장→HOME→네트워크 복구→앱을 다시 열지 않고 동일 payload 1/1건 외부 수신. 중복·payload 불일치 0 |
| probe 정리·기기 복원 | PASS. 등록 ID 유지·로컬 ACK 확인, 검사 subject 철회 및 재등록 거부, 임시 probe만 제거, 기존 A7 APK 보존·화면 설정 원복 확인 |
| 표준 non-debuggable QA 1011 | 빌드·preflight PASS. 1010과 동일 서명·버전 증가·HTTPS 자동 등록 설정·개별 token 미포함 확인. 실제 기기 설치는 하지 않았으며 debug probe 결과와 구분 |
| 과거 수동 등록 | A7 1008 및 S24 1010 수신·화면 설정 원복 성공. 자동 등록 성공 근거로 재사용하지 않음 |

Unity gate는 새 `CAPSTONE_PERSONALIZATION_TEST_ROOT`와 `CAPSTONE_ARTIFACTS`를 지정하고 `CapstoneDesign.EditorTools.AutomaticEnrollmentPlayValidation.Start`를 실행한다. 자체 종료하므로 `-quit`을 붙이지 않는다. 합성 fixture만 쓰며 외부 운영 서버로 보내지 않는다. 결과는 `automatic-enrollment-play.json`이다.

실기기 최초 시도는 초기 알림 팝업에 응답하지 않아 방문 사건이 생성되지 않았고 실패로 보존했다. 초기 설정을 마친 별도 재시도에서만 위 수신 결과를 얻었다. 첫 시도를 성공 횟수에 넣지 않으며, 앱 설치만으로 아직 생성되지 않은 방문 사건의 수신을 주장하지 않는다.

검증 원본은 ignored `artifacts/automatic-enrollment-20261110/`의 `unity-play-final/automatic-enrollment-play.json`, `manual-regression/result.json`, `device-offline-first-result.json`, `probe-server-result.json`, `probe-cleanup-result.json`, `final-preflight/result.json`, `display-restoration-audit.json`에 있다. 최종 화면 설정 감사에서 미복원 항목 0건을 확인했다. native 결과는 `artifacts/qa-auto-enrollment-native-20261110/result.json`, 외부 서버 결과는 `artifacts/qa-server-autoenrollment-deploy-20261110/external-protocol-result.json`·`final-status-summary.json`이다. ID·키·원본 payload는 공개 문서에 복제하지 않는다.

최종 배포 APK는 ignored `artifacts/automatic-enrollment-20261110/final-build/build/qa-candidate.apk`이다. package `com.capstonedesign2026.qa`, version `0.1.0-qa.20261110.11`, versionCode `2026111011`, non-debuggable, 93,756,823 bytes, SHA-256 `c3e4955b86278b6740740b2d3279d06872b9e4358cdfe35fb1c3b8834985d11a`다. APK 내 자동 등록 설정과 private build config의 byte 일치도 확인했다. S24의 수동 1010·A7의 수동 1008을 그대로 유지했으며 최종 APK를 해당 기기에 설치했다고 기록하지 않는다.

## 독립 검토와 남은 제한

독립 Opus 검토에서 P0/P1 지적은 없었다. P2인 공유 APK 키 추출 후 새 등록 한도 소진 위험은 최소 등록 장치의 한계로 남는다. 현재 rate limit은 자원 사용을 제한하지만 공격자의 한도 선점을 막는 강한 인증은 아니다.

P3의 느린 요청 문제는 현재 외부 Caddy의 `read_header=10s`, `read_body=30s`, `write=30s`와 API의 loopback-only 바인딩을 확인했다. 기존 프록시 보호 범위이며 실제 공격 부하 검사를 통과했다고 해석하지 않는다. P3인 build config 경로 별칭 문제는 대소문자 무시 비교와 모든 경로 요소의 `ReparsePoint` 거부로 보완했다.

구현 위치: [저장 helper](../Assets/Scripts/Runtime/AutomaticEnrollment.cs), [클라이언트 연결](../Assets/Scripts/Runtime/PersonalizationClient.AutomaticEnrollment.cs), [Android build hook](../Assets/Editor/Automation/TelemetryEnrollmentBuild.cs), [native uploader](../Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/MeasurementUploadWorker.kt), [서버 등록](../server/enrollments.py). 외부 배포와 표준 QA APK의 최종 판정은 [QA 배포 기록](QA_RELEASE_20261110.md)을 따른다.
