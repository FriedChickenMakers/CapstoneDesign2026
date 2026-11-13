# 2026-11-10 QA 배포 준비·검증 인수인계

현재 `misssion` 작업 트리의 UI·성능·수집 안정성 결과를 정리한다. 실제 A7의 오프라인·절전 및 전용 API 장애 후 백그라운드 재전송을 검사용 APK에서 확인했다. 1007 APK에서는 기존 등록을 유지한 채 GLES 실행·HOME/화면 잠금 후 복귀·서버 방문 기록과 실제 화면 20장을 확인했다. 단말 설정도 원복·검증했다. 마지막으로 코스 미선택 상태의 문구만 고친 1008의 배포 식별·기기 결과는 아래에 별도로 기록한다. Editor/host 통과를 실제 기기 전송 성공으로 대신하지 않는다. 구현은 아래 게시 기록의 `misssion` 커밋으로 묶었으며, APK·기기 원본은 Git 게시 대상에서 제외한다.

추가 자동 수집 구현은 [자동 등록·로그 수집](AUTOMATIC_TELEMETRY_ENROLLMENT.md)에 분리해 기록한다. 이 문서의 1007·1008 결과와 후속 S24 1010 결과는 수동 등록 APK의 검사이며, 자동 등록 APK의 실기기 검증 결과가 아니다. 신규 자동 등록은 단위·Unity·외부 서버 검증 및 별도 A7 debug probe의 실제 오프라인 첫 실행→백그라운드 수신을 확인했다. 표준 non-debuggable QA 1011 APK의 빌드·정적 preflight도 통과했으며, 최종 APK 자체는 기기에 설치하지 않았다.

## 요청 9개 대조

| 요청 | 반영 내용 | 확인 결과·남은 범위 |
| --- | --- | --- |
| 1. 하단 메뉴 배경을 조금 더 갈색 목재로 | 제목과 같은 밝은 목재 계열에 별도 갈색 색조·확대 비율 적용. 버튼·제목 색과 분리 | 현재 화면 촬영·테마 회귀 PASS. 기존 잎 버튼·숫자 배지 색 유지 |
| 2. A7 정원 성능 측정·개선 | 회전·확대·구름 갱신의 중복 카메라 렌더를 프레임당 한 번으로 합침 | 실제 A7 Vulkan 비교에서 회전 p95 100.01→66.68ms. 정지 상태는 개선되지 않았음. 텍스처·식물·그림자 품질을 낮추지 않음. 이후 적용한 GLES 호환성 수정의 최종 기기 검증은 별도 |
| 3. 전체 화면 텍스처·정렬 | 주요 메뉴·기록·코스, 45개 미션의 개요/첫 단계, 설정·정원 낮/밤 촬영. 설정 스위치·상태 글자·화살표를 행 안쪽으로 12 canvas 단위 이동 | Editor 325장 중 126장 개별 육안 확인. 실제 A7 1007 화면 20장도 개별 확인. 설정 4개 비율·on/off·스크롤 24장 PASS. 코스 미선택 표기 추가 수정은 별도 16장·18 checks PASS. 실제 폰 키보드·모든 분기 단계까지 검증한 것은 아님 |
| 4. 정원 설정 버튼 간소화 | 목재 장식을 제거하고 글자만 표시. 기존 터치 영역 유지, 낮/밤에 글자색 변경 | 실제 장면의 낮/밤·폰/태블릿 4장 확인 |
| 5. 신규 설치·사용자별 서버 수집 | 영구 기기 ID 대신 설치/참여 단위 난수 subject와 개별 등록 token 사용. 기존 4개 앱 변형 백업·제거 후 새 등록 | `visualaudit`, `qa`, `notificationpreview`, `mockup` 제거 및 신규 등록 PASS. 1007로 업데이트해 등록 보존·버전별 서버 방문 수신 PASS. 검사 자료와 참가자 자료는 구분 |
| 6. 수집 항목·절전·오프라인 | native 영속 큐와 WorkManager 재시도, 부분 ACK·중복·철회·저장 실패 처리 보강. 앱 방문 사건 추가 | native 46개, C# 336/40 assertions PASS. 실제 A7 오프라인/Doze 후 원래 사건·전체 payload 1건이 약 1분 뒤 자연 백그라운드 재시도로 한 번 수신됨. 최종 1007의 긴 복귀 방문 추가·짧은 복귀 중복 없음 PASS |
| 7. 서버 자동 복구 | 전용 API/proxy 자동 시작·API 비정상 종료 후 재시작, 온라인 백업·철회 정리 timer 설정 | 서버 검증 68개, 외부 복구 검사 27개, 최종 배포 확인 9개 PASS. 실제 A7도 API 45초 중단 후 원래 payload 1건의 백그라운드 재전송 PASS. 공유 호스트 재부팅·더미 RAM 점유·Minecraft 변경은 하지 않음 |
| 8. 간단한 무단 업로드 차단 | 기존 설치별 bearer 인증·철회를 유지하고 추가 자동 등록에 공유 배포 키를 사용 | 과거 1008 검사는 개별 token만 사용. 새 APK 공유 키는 신규 등록용 최소 장치이며 추출 가능. 강한 기기 인증이 아님. 새 경로의 외부 HTTPS 31 checks·A7 별도 debug probe 수신 확인 |
| 9. 작은 안정성 개선 | 같은 방문의 큐 용량 거절 반복 방지, 일시 저장 실패 재시도 유지, 제한된 서버 요청 처리·백업 정리 | 독립 리뷰에서 찾은 용량 거절 문제 수정 및 회귀 PASS. 미션·보상·콘텐츠 정책 변경 없음 |

## A7 성능 결과와 해석

**아래는 A7에 GLES 호환성 처리를 적용하기 전, 양쪽 모두 Vulkan으로 실행한 렌더 요청 병합 전후 비교다.** 동일 A7의 정원에서 정지·반복 회전 조건을 비교했다. 표의 `n`은 중복을 제거한 SurfaceFlinger 표시 시각 사이의 간격 수이며 사용자 수나 독립 반복 실험 수가 아니다. 단위는 ms이고 p95는 관측 간격의 95백분위다. 최종 GLES APK의 측정값을 이 표와 섞어 렌더 요청 병합의 효과로 해석하지 않는다.

| 조건 | n 전→후 | 중앙값 전→후 | 평균 전→후 | p95 전→후 | 50ms 초과 비율 전→후 |
| --- | --- | --- | --- | --- | --- |
| 정지 | 572→567 | 33.35→33.32 | 32.81→33.01 | 50.07→50.13 | 10.1%→12.3% |
| 회전 | 750→806 | 33.39→33.42 | 40.47→37.16 | 100.01→66.68 | 23.1%→19.5% |

회전 중 긴 표시 간격은 줄었지만 중앙값은 약 33ms로 유지됐다. 정지 상태에는 개선을 주장하지 않는다. 단일 기기의 짧은 통제 측정이므로 장시간 발열·모든 정원 규모·다른 기기에 대한 보장은 아니다. 표시 간격은 Unity CPU/GPU 처리 시간 자체와도 구분한다.

`GardenSkyEnvironment`가 입력·구름 요청을 늦은 프레임에 모으고, `PrototypeIslandDrag`는 선택적 콜백으로 요청을 전달한다. 이미 명시적으로 렌더한 프레임의 요청은 다음 프레임까지 보존한다. 환경이 꺼져 있으면 기존 즉시 렌더·시점 초기화로 돌아간다. 실제 Play 검증 20개에서 프레임당 최대 1회·재귀 깊이 최대 1을 확인했으며 텍스처·해상도·식물 형태를 줄이는 최적화는 하지 않았다.

실제 화면 복귀 검사에서 `vulkan.bengal.so`의 render pass/descriptor binding 중 native SIGSEGV가 발견됐다. 이전 시각 검토 APK에도 같은 제조사 Vulkan 드라이버의 command buffer 종료 충돌이 있어 이번 렌더 요청 병합이 새 원인이라고 단정할 수 없다. 복귀 직후 방문이 없는 현상도 프로세스 충돌과 겹쳤으므로 Unity callback 누락이 원인이라고 입증된 것은 아니다.

SM-T500의 관측 조합인 Samsung·Adreno 610·`gta4lwifixx`·Android 10만 Vulkan 제외 규칙에 추가해 OpenGLES3로 대체하도록 설정했다. 다른 기기는 Vulkan을 유지한다. Unity 6000.3.24f1의 native 기본 Google 기기 제외 규칙 1개와 기존 항목을 함께 보존했고, 설정 검사 PASS(제외 규칙 1→2)를 확인했다. 이 기본 규칙은 확인한 Unity 버전에 고정되어 있어 Editor 업그레이드 때 재검토해야 한다. 최종 1007에서 Unity 스레드의 Adreno GLES 드라이버 사용, 같은 프로세스의 HOME 5초·35초 두 차례·화면 잠금 35초 후 복귀 PASS를 확인했다. 이 범위에서 이전 native 충돌은 재현되지 않았다. 최종 화면 검토와 장시간 안정성은 별도이며 모든 기기의 충돌 해소를 뜻하지 않는다.

최종 1007의 GLES 실행 상태에서 새 해바라기 정원을 별도로 측정했다. 계산법은 이전 SurfaceFlinger 자료 4개를 다시 집계해 동일한 결과가 나오는 것으로 확인했다. **아래는 그래픽 API와 식물 seed가 다른 별도 상태 관측이며 위 Vulkan 전후표에 대한 추가 개선율로 해석하지 않는다.**

| 최종 GLES 조건 | n | 중앙값 ms | 평균 ms | p95 ms | 50ms 초과 비율 |
| --- | --- | --- | --- | --- | --- |
| 정지 | 727 | 16.72 | 24.64 | 50.00 | 5.0% |
| 회전 | 970 | 16.92 | 29.74 | 66.67 | 12.2% |

관측 구간은 정지 약 17.9초·회전 약 28.9초다. 일부 회전 프레임 지연은 남아 있으며 장시간 발열·더 복잡한 정원은 이 결과의 검증 범위 밖이다.

## 화면·회귀 근거

| 검사 | 확인 결과 |
| --- | --- |
| 테마 EditMode | `GardenThemeSurfaceTests` 24/24 PASS |
| 통합 기능 | 15개 시나리오·1,568 assertions·84회 버튼 클릭/레이캐스트 PASS. 짧게 하기/타이머 pause, M10 선택, 코스·보상, 저장 실패 복구 등 |
| 설정 배치 | 360×640, 390×844, 412×915, 1200×2000에서 24장 PASS. 스위치 손잡이·상태 글자·화살표의 행 내부 배치 확인 |
| 현재 페이지·미션 | 390×844·1200×2000의 297장 PASS. 텍스트 영역 3,167개 측정, 의심 항목 0. 스크롤/마스크와 의도적 말줄임을 구분 |
| 사진첩 보완 촬영 | 4장·텍스트 영역 56개 PASS. Editor에 `LateUpdate`가 없어 흰색이던 썸네일을 촬영 도구에서 명시적으로 렌더. 제품 오류로 처리하지 않음 |
| 최종 갤러리 | 기존 297장 중 사진첩 4장을 보완본으로 교체하고 설정 24장·정원 4장을 합쳐 325장. 126장에 개별 육안 확인 기록 |
| Unity 등록·철회 | 실제 Play/UnityWebRequest와 격리된 검사 서버의 등록·저장 재시도·철회·삭제 확인 PASS. 외부 운영 서버/A7 검사와 구분 |

육안 확인에는 45개 미션의 폰 첫 안내 화면 전체, 주요 폰 메뉴, 태블릿 대표 메뉴·미션, 긴 본문과 스크롤 하단을 포함했다. 본문·버튼 겹침은 발견하지 않았다. 일부 기존 활동 삽화는 태블릿에서 글자보다 부드럽게 보이며, Editor 전용 P/M 테스트 버튼의 잎 장식은 후속 미세 조정 대상이다. 모든 실습 분기·오디오·실제 키보드·장시간 터치 동작을 이 촬영으로 대체하지 않는다.

실제 A7 1007 화면 20장의 검토에서 설정 토글/화살표의 행 내부 배치, 하단 아이콘/간격/갈색 받침대/색상 배지, 사진첩 및 꽃밭 미리보기 렌더를 확인했다. 세수하기 추가 활동은 완료 버튼을 누르지 않고 ‘미뤄두기’로 정리했으며 서버에 `activity_ended`가 도착했다. 코인 0개·코스 미선택·씨앗 상태를 유지했다. 미리보기는 취소했고 환경 구매·가짜 완료 보상은 만들지 않았다. 기존 개별미션의 파란 본문 버튼/하단 영역은 유지했으며, 추가 미션의 미루기 팝업에도 기본 코스 설명이 함께 나오는 범용 문구는 바꾸지 않았다.

이 점검에서 코스 미선택인데 정원 꾸미기/배치 미리보기에 ‘8주 코스 0/48’이 보이는 기존 문구 오류를 찾았다. 저장 모델의 기본 `8w`를 선택 여부 없이 표시하던 문제다. 1008에서는 `WeekOneQuestDemo.cs`의 공통 표기만 분기해 ‘아직 선택한 코스가 없어요.’로 표시하고 미선택 진행 막대를 숨겼다. 선택된 2/4/8주 진행 수치와 저장 값은 유지한다. 실제 Unity의 폰/태블릿 미선택·2/4/8주 shop/placement 16장, 18 checks, 텍스트 영역 120개·의심 항목 0, 화면 전후 저장 상태 동일 검증을 통과했다. 이 소형 수정에는 알림·업로드·그래픽·미션 완료 로직 변경이 없다.

## 수집·서버 운영 경계

- 과거 1008·1010은 서면 동의 후 운영자 수동 등록으로 검사했다. 추가 자동 등록 빌드는 배포 담당자가 첫 실행 전에 서면 확인하고 앱이 설치별 ID를 생성한다. 앱 내 동의 UI는 추가하지 않는다. 난수와 서면 명부를 연결한다면 엄밀히는 가명 자료다. 이름·기기 serial·Android ID를 분석 식별자로 쓰지 않는다.
- 알림 예정/도래/게시 수락/클릭 시각, 정책·적용 시각·표본 수, 활동 선택/시작/완료/중단, 추천 노출/선택, 앱 방문과 큐 품질 지표를 기록한다. **게시 수락은 사용자가 실제로 봤다는 뜻이 아니며**, 단순 시작→종료 차이는 실제 집중 시간이 아니다.
- 일기·감정 원문, 그림, 위치·연락처, 건강 원본, 전 화면 터치, 렌더 프레임 원본을 새로 서버에 보내지 않는다. 과제 완료·조작 수·설문·사용성 문제는 별도 QA 관찰로 남긴다. `live`와 `synthetic`, 구현 검증과 사용자 효과를 구분한다.
- native 측정 큐는 1,024 pending/8,192 receipts·최대 90일이다. 새 사건 뒤 5초 지연 작업, 15분 주기 작업, 전경 복귀 재시도를 사용한다. 전송 실패만으로 ACK 없는 자료를 삭제하지 않으며 보관 기한 만료·철회는 별도 처리한다. 중복 ID는 서버에서 한 번만 저장한다. 무한 오프라인 저장을 약속하지 않으며 큐 거절·만료·저장 오류를 함께 해석한다.
- WorkManager는 Doze·제조사 절전·강제 중지 제한을 우회하지 않는다. 복구 후 재시도와 보존을 검증하며, 강제 중지 후 앱 재실행 전의 즉시 전송은 보장하지 않는다. 기존 C# `/events/batch` 큐의 전경 전송과 native `/measurements/batch` 백그라운드 경로도 구분한다.
- 방문 키는 Android Activity START/STOP과 monotonic 시각으로 공급하도록 보강했다. 실제 A7 HOT 복귀에서 방문이 없던 시점에는 native Vulkan 프로세스 충돌도 확인됐다. 이를 Unity pause callback 실패로 확정하거나 방문 경로 변경만으로 해결됐다고 주장하지 않는다. 최종 GLES APK의 35초 HOME/잠금 복귀에서 버전별 서버 방문 수가 1→2→3→4로 늘고, 마지막 5초 HOME 재검사에서는 4로 유지됐다. 첫 기준 화면에는 검사 도구가 연 알림창이 있어 짧은 복귀 중복 없음 판정은 마지막 재검사를 근거로 삼았다.
- 실제 A7의 오프라인/Doze 검사에서는 원래 사건 ID·전체 payload 1건이 복구 후 약 1분의 자연 백그라운드 재시도로 한 번 수신됐다. 전경 재실행을 유도하지 않았다. 전용 API를 45초 중단한 별도 검사에서도 원래 payload 1건의 백그라운드 수신과 서비스 자동 복구 후 active 상태를 확인했다. 이 관측은 모든 절전 환경의 즉시 전달을 보장하지 않는다.
- 자연 알림의 `notification_due`와 게시 수락 사건은 약 02:49 서버 도착을 확인했다. 이번 시도에서는 강제 중지가 게시 알림을 제거해 실제 클릭은 검증하지 못했다. 임시 알림 시각·학습·게시 guard는 `restore_notification_trial`로 검사 전 12:00/자동 최적화 상태로 복원했다. 큐와 서버 사건은 삭제하지 않았으며 이 운영자 검사 자료는 참여자 분석에서 제외한다.
- 서버는 앱 내부가 아닌 외부 HTTPS 수신기다. 설치별 token으로 무단 입력과 개별 철회를 처리한다. 자동 등록에는 신규 등록 전용 공유 키를 추가하며 APK에서 추출 가능하므로 강한 기기 무결성 인증을 주장하지 않는다. 전용 서비스만 관리하고 기존 Minecraft·다른 서비스는 보존했다.
- 서버 검사에서 API 비정상 종료 후 자동 복구와 DB 유지, 온라인 백업·철회 반영을 확인했다. hourly timer는 UTC 일별 백업 7개를 관리하며 철회·90일 만료를 관리 백업에도 반영한다. 기존 수동 export·다른 위치 백업, 전체 재해 복구·외부 백업은 별도 운영 사항이다.
- RAM을 50% 채우는 더미 작업은 하지 않았다. 공유 서버 자원을 낭비하지 않고 실제 OCI 계정/shape·사용량·회수 안내를 소유자가 확인해야 한다. 자동 시작은 제공자가 회수한 인스턴스를 되살리지 못한다. **호스트 자체 재부팅은 수행하지 않았다.** 자세한 근거·운영 경계는 [서버 운영 문서](../server/OPERATIONS.md#oracle-idle-instance-constraint)를 따른다.

수집 계약과 테스트 상세는 [수집·전송 안정성 점검](QA_TELEMETRY_RELIABILITY_20261110.md), [측정 범위](QA_BETA_MEASUREMENT_PLAN.md), [서버 운영](../server/OPERATIONS.md)을 함께 확인한다. 현재 서버 release 이름은 작업 트리 source manifest 식별자이며 Git commit이 아니다.

## 수동 등록 APK의 실제 전송 결과와 실기기·배포 판정

오프라인·장애 복구의 실제 관측과 1007 APK의 GLES 실행·반복 복귀, 문구만 수정한 최종 1008의 결과를 구분해 기록한다. 앞선 검사용 APK·host 검사 결과만으로 최종 APK에서 같은 검사를 전부 반복했다고 표시하지 않는다.

| 항목 | 상태 | 기록할 근거 |
| --- | --- | --- |
| 최종 APK | **1008 설치·non-debuggable 확인 PASS** | 기존 등록·외부 save 보존, 같은 서명으로 in-place 업데이트. GLES 시작 및 새 버전 방문 수신 확인. 아래 배포 식별 정보 참조 |
| A7 앱 정리·새 등록 | **PASS** | 기존 4개 변형 백업·제거 후 새 난수 등록. 1007·1008 업데이트에서도 같은 등록 유지·버전별 방문 수신. 실제 ID/token은 문서에 쓰지 않음 |
| Activity 방문 수정 재검증 | **최종 1007 PASS** | HOME 35초 두 차례·잠금 35초 복귀에서 방문 1→2→3→4. 마지막 HOME 5초 재검사에서는 4 유지. 같은 프로세스 복귀·업데이트 등록 보존 확인 |
| 실제 오프라인·절전 | **검사용 APK 실제 A7 PASS** | `offline07`: 원래 사건·전체 payload 1건, 복구 약 1분 후 자연 백그라운드 수신. 전경 재실행 없음 |
| 실제 서버 장애·재전송 | **검사용 APK 실제 A7 PASS** | `cold-outage-later-delivery`: 전용 API 45초 중단·복구 후 원래 payload 1건 백그라운드 수신. API 자동 복원 active 확인. 공유 호스트 재부팅 검사는 아님 |
| 자연 알림 게시·클릭 | **도래/게시 수락 수신 PASS·클릭 미확인** | 약 02:49 도래·게시 수락 사건 도착. 강제 중지로 알림이 제거되어 이번 시도의 실제 클릭 근거 없음 |
| 최종 A7 그래픽 호환성 | **설정·실제 GLES 실행·반복 복귀 PASS** | Unity 스레드에서 Adreno GLES 드라이버 사용, 같은 프로세스 HOME/잠금 복귀·방문 수신 확인. 이 검사 동안 native 충돌 없음. 새 해바라기 정원의 최종 GLES 성능은 위 별도 표에 기록 |
| A7 화면·조작 | **1007 실제 20장·1008 실제 7장 PASS** | 설정·정원·미션/미루기·기록·사진첩·정원 꾸미기/미리보기. 1008의 미선택 문구와 0코인·씨앗 유지 확인. 실제 키보드·오디오·모든 분기 조작 검사를 뜻하지 않음 |
| 1008 업데이트 후 복귀·수신 | **PASS** | GLES 시작, HOME 35초 후 같은 프로세스로 복귀. 같은 등록의 1008 방문 수 1→2, drop/expiry/storage error 0. 1007의 전체 검사를 반복한 것은 아님 |
| 단말 설정 원복 | **1008 종료 감사 PASS** | 알림 12:00/자동 최적화·학습·게시 guard 복원. 화면 timeout·밝기·밝기 모드, Wi-Fi, 절전, 강제 idle, BatteryService 시뮬레이션 복원 확인. pending journal 없음. 앱을 강제 중지하지 않고 백그라운드로 두어 수집 가능 |

당시 A7 1008 배포 식별 정보:

- 파일: ignored `artifacts/qa-release-20261110/final-v8/build/qa-candidate.apk`
- 패키지: `com.capstonedesign2026.qa` · 버전 `0.1.0-qa.20261110.8` · versionCode `2026111008`
- 크기: 93,751,619 bytes · SHA-256 `ed682ead3929f7a4075709ff25abe7b5e2688288894ab536dca54b441859d151`
- 이 1008 APK는 등록 정보/개인키·공유 등록 키를 포함하지 않음. non-debuggable·동일 서명·버전 증가·HTTPS 제한 확인. 실제 설치 파일 SHA도 일치.
- 기존 full private backup은 1007 설치 직전에 확보했다. non-debuggable 1007은 private `run-as` 접근을 막으므로 1008 전에는 외부 save를 추가 백업하고 데이터 삭제 없는 업데이트를 사용했다. 새 private 전체 백업을 했다고 주장하지 않는다.
- Android 런처 표시는 기존 `Sky Island Mockup`이다. 이번 배포에서 이름이나 package 정책을 바꾸지 않았다.

최종 서버 확인에서도 API·proxy·maintenance timer 모두 active/enabled다. 현재 A7에는 프로젝트 앱 중 `.qa`만 설치되어 있다. 학교에서 사용하는 동안 로그를 수신할 수 있는 등록 상태이며, 이번 운영자 QA 등록의 자료를 참가자 효과·재참여 통계에 섞지 않는다.

전체 갤러리는 **366장**이다: 기존 Editor 325장 + 코스 표기 보완 Editor 16장 + A7 1007 18장 + 최종 A7 1008 7장. 실제로 검토한 1007 20장 중 잘못된 코스 문구가 보인 2장은 원본 기록에 보존하되 최종 갤러리에서 보완본으로 대체했다. Editor 개별 육안 확인은 기존 126장과 보완 16장, 실기기는 27장이다.

## 재현·인수인계 위치

검증은 라이선스가 있는 OS 사용자 `codex`와 해당 HOME 환경에서 수행했다. Unity를 동시에 열지 않고 기존 프로젝트 잠금을 확인한 뒤 실행한다.

- `SettingsLayoutValidation.Run`: 실제 설정의 on/off·스크롤·4개 비율.
- `ReleaseQaCapture.RunBatch`: `CAPSTONE_ARTIFACTS`를 새 폴더로 지정. 기본 주요 페이지·미션 촬영, `CAPSTONE_RELEASE_UI_SCOPE=albums`는 사진첩만 재촬영한다.
- `GardenRenderSchedulingValidation.Start`: 실제 Play 모드 검사. 자체 종료하므로 `-quit`을 붙이지 않는다.
- private 원본은 ignored `artifacts/qa-release-20261110/`, `artifacts/qa-client-reliability-1110/`, `artifacts/qa-server-20261110/`에 보존한다. `screenshots.html`, `ui-visual-review.json`, `performance-comparison.json`이 화면/육안/Vulkan 전후 성능 요약이며, `final-gles-performance-summary.json`과 `final-resume-evidence.json`은 최종 GLES 성능·복귀 근거다. 이 디렉터리 전체를 Git이나 공개 첨부로 올리지 않는다.
- 1008 근거는 같은 private 폴더의 `final-v8-preflight/result.json`, `device-release-v8/result.json`, `final-v8-evidence.json`, `final-device-audit-v8/result.json`, `device-v8-visual-review.json`이다. `final-gles-performance-summary.json`과 반복 복귀 증거의 관측 버전은 1007로 유지하며 최종 APK 측정으로 이름을 바꾸지 않는다.
- 갤러리는 `python3 artifacts/qa-release-20261110/build-gallery.py`로 다시 생성한다. 최종 A7 사진을 별도 device 폴더에 두면 Editor 합성 자료와 구분해 표시한다. 실패한 이전 갤러리와 대체된 흰 사진첩 이미지는 제외한다.

이전 1008·1010은 별도 수동 등록이 필요한 APK다. 신규 자동 등록은 첫 LIVE 실행에서 ID를 저장하고 연결 후 서버 등록·전송한다. 별도 A7 debug probe로 실제 신규 설치·외부 수신을 확인하고 서버 검사에서 철회 경계를 확인했지만, 표준 non-debuggable 최종 APK의 기기 설치 검사를 대신하지 않는다. 배포 전 서면 동의·철회·보관 절차는 계속 필요하다.

### 후속 S24 수동 등록 확인

S24는 데이터 보존을 위한 1009 bootstrap을 거쳐 같은 서명의 non-debuggable 1010으로 업데이트했다. 등록 파일을 안전하게 배치하고 실제 외부 서버에서 1010의 `app_session_started` 1건을 확인했다. 화면 꺼짐 시간·밝기·밝기 모드는 원래 값으로 복원·검증했다. 최종 private 등록 파일의 별도 직접 검사는 하지 않았으며, 실제 같은 등록의 1010 서버 수신을 수집 근거로 사용한다. 이 과정은 새 자동 등록 검증이 아니다.

근거는 ignored `artifacts/s24-logging-20261110/manual-device-result.json`, `server-receipt-verification.json`과 private 복원 journal이다. 기기 주소·ID·token·원본 로그는 문서에 복제하지 않는다.

## 후속 자동 등록 통합 결과

| 검사 | 결과와 범위 |
| --- | --- |
| C# helper·개인화 | 55 / 336 assertions PASS |
| 실제 Unity 자동 등록 | 22 checks PASS. 초기 생성·오프라인 큐·재시작·수동 등록 보존·철회·저장 실패 복구 |
| 기존 UnityWebRequest 회귀 | client 42·server 2 checks PASS. 실제 격리 서버 전송·철회 확인 |
| native | 58/58 PASS. 등록 실패/재시도·동일 batch·부분 ACK·철회/reset 경합 포함 |
| 서버 | 89/89 PASS. 실제 외부 HTTPS 31 checks 및 배포 후 상태 17 checks PASS |
| 실제 A7 자동 등록 | **별도 debug probe 1011 PASS**. 오프라인 초기 설정 완료 뒤 방문 1건 저장→HOME→네트워크 복구→재실행 없이 같은 payload 1/1건 수신, 중복·불일치 0 |
| probe 정리 | **PASS**. 같은 ID 유지·ACK, 검사 subject 철회·재등록 거부, 임시 앱만 제거, 기존 A7 APK 보존·화면 설정 복원 |
| 표준 non-debuggable QA 1011 | **빌드·정적 preflight PASS**. 1010과 동일 서명·버전 증가·HTTPS 설정·개별 token 미포함·private config byte 일치. 실제 기기 설치하지 않았으므로 probe 검사와 구분 |

처음 A7 probe 시도는 초기 알림 팝업에 응답하지 않아 방문 사건이 없어 실패로 기록했다. 초기 설정을 마친 별도 재시도만 수신 성공으로 인정했다. 이전 앱 데이터와 이미 설치한 A7 1008·S24 1010은 유지했다.

근거는 ignored `artifacts/automatic-enrollment-20261110/`의 `unity-play-final/automatic-enrollment-play.json`, `manual-regression/result.json`, `device-offline-first-result.json`, `probe-server-result.json`, `probe-cleanup-result.json` 및 `artifacts/qa-auto-enrollment-native-20261110/result.json`, `artifacts/qa-server-autoenrollment-deploy-20261110/external-protocol-result.json`·`final-status-summary.json`이다. 최종 APK 근거는 `artifacts/automatic-enrollment-20261110/final-preflight/result.json`이며 `display-restoration-audit.json`에서 미복원 항목 0건도 확인했다. 서버 release 표시는 소스 manifest 식별자이며 Git commit이 아니다.

자동 등록 배포 파일:

- ignored `artifacts/automatic-enrollment-20261110/final-build/build/qa-candidate.apk`
- package `com.capstonedesign2026.qa` · version `0.1.0-qa.20261110.11` · versionCode `2026111011` · non-debuggable
- 93,756,823 bytes · SHA-256 `c3e4955b86278b6740740b2d3279d06872b9e4358cdfe35fb1c3b8834985d11a`
- A7에 설치·검증한 자동 등록 앱은 같은 runtime의 별도 debug probe다. 해당 임시 앱은 철회 후 제거했고, 표준 1011은 아직 실기기에 설치하지 않았다. S24 1010·A7 1008의 기존 등록과 앱을 유지했다.

독립 Opus 검토의 P0/P1 지적은 없었다. 공유 키 추출로 신규 등록 한도를 소진시킬 수 있는 P2는 최소 등록 장치의 한계로 명시했다. 느린 요청의 P3는 현재 Caddy timeout과 API loopback-only 설정을 확인했으며 실제 공격 검사를 뜻하지 않는다. build config 경로 별칭의 P3는 대소문자·symlink/junction 검사를 보완했다. 키 변경 때 기존 private 등록 파일이 유지되어 APK 업데이트만으로 아직 미등록인 설치의 키가 바뀌지 않는 제한은 [운영 문서](AUTOMATIC_TELEMETRY_ENROLLMENT.md)에 명시했다.

2026-11-10 후속 명시 승인에 따라 아래 구현과 검증·운영 문서를 Git 게시 묶음으로 정리했다. 각 커밋 전에 publishable worktree와 정확한 Git index를 gitleaks 포함 privacy scanner로 검사한다. APK·장치 원본·복원 journal·등록 정보는 Git 대상에서 제외한다. 게시 과정에서 새로 바꾼 것은 무료 에셋 파생 파일의 출처 표시, Unity 직렬화 파일의 후행 공백, 이 문서의 상태·링크이며 실행 동작은 바꾸지 않았다.

## Git 게시 기록

| 구현 묶음 | 커밋 | 검증 대응 |
| --- | --- | --- |
| 정원 재질·배치·렌더·A7 호환성 | [da5fec2](https://github.com/FriedChickenMakers/CapstoneDesign2026/commit/da5fec2) | 기존 테마 24 tests, 렌더 20 checks, 설정·코스·홈 화면 및 A7 결과 |
| 서버 등록·철회·복구 | [0a4fc52](https://github.com/FriedChickenMakers/CapstoneDesign2026/commit/0a4fc52) | 배포 Python 15개 hash 일치, 서버 89 tests·HTTPS 31·운영 17 checks |
| 앱 자동 등록·오프라인 수집·빌드 | [7bffc65](https://github.com/FriedChickenMakers/CapstoneDesign2026/commit/7bffc65) | native 핵심 2개 hash 일치·58 tests, C# 55/336, Unity 22 및 42+2, A7 별도 probe |

검증 구현 기준은 `7bffc65b39cab92ca7ed311f88f71dfe2db59fc0`이다. 후속 문서 커밋에는 본 보고서·수집 계획·운영 절차와 v5 근거 연결을 포함한다. 서버 release `qa-auto-20261110-7249cb5ad7c1`는 배포 소스 manifest 식별자이며 Git SHA와 구분한다. 표준 APK1011 실기기 설치·장기 QA·실제 알림 클릭은 이 게시 작업으로 추가 검증되지 않았다. 기존 검사 실패와 후속 성공의 범위는 위 기록을 유지한다.
