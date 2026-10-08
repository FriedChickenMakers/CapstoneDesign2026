# MBCT 로컬라이제이션 구현

팀 콘텐츠 기준은 별도 `CapstoneDesign2026-Docs` 저장소의 `기획서/MBCT 활동 설계.md`, `MBCT 주차별 임무표.md`, `MBCT 구현과 검증.md`에 있다. 원래 [대화 결정 기록](MBCT_LOCALIZATION_DECISIONS.md)은 초기 검토의 이력으로 보존한다.

## 코드와 저장

- `Assets/Scripts/Runtime/MbctContent.cs`: 8주·48회 앱용 카탈로그, 1~13번 목적, 단계별 안내와 선택 입력.
- `WeekOneQuestDemo.cs` 및 `WeekOneQuestDemo.Mbct.cs`: 기존 화면과 연결한 추천·복습·실습·경험 기록·개인 계획.
- `LocalGardenState.cs`: MBCT 진도, 세션별 단계·답변, 재사용 선호, 경험 기록. 기존 schema-1 XML의 새 필드가 없으면 기본값으로 연다.
- `GardenUi.cs`: 긴 기록의 스크롤과 터치 입력 대상.

구 코스 `course:P01`~`P28`과 진행은 보존한다. 새 카탈로그는 `course:MBCT01`~`MBCT48`과 별도 `MbctNextOrder`/`MbctCompletedIds`를 사용한다. 새 추천 임무는 하루 1개·월~일 주 최대 6개이며 참여일을 건너뛰어도 진행을 초기화하지 않는다. 완료 시점 회차에 진도와 보상 영양제 5개를 함께 저장한다. 복습과 독립 경험 기록에는 추가 보상이 없다. 기존 걸음 기능과 디버그 정산은 유지한다.

선택지·직접 입력·건너뛰기는 단계별로 제공한다. 활동의 입력은 일시정지·화면 이동·앱 배경 진입 시 저장하고, 완료한 선호는 다음 실습과 개인 계획에 재사용한다. 경험 유형과 기존 디자인의 감정 선택값을 구분한다. 개인 계획은 기록 화면에서 언제든 수정한다. 알림 선호 저장은 자동 알림 예약을 의미하지 않는다.

## 검증 실행

```bash
./scripts/test-mbct.sh
./scripts/test-local-state.sh
CAPSTONE_ARTIFACTS="$PWD/artifacts/mbct-localization" ./scripts/test-local-loop-unity.sh
CAPSTONE_SMOKE_SAVE_ROOT="$PWD/artifacts/mbct-localization/play-state-new" ./scripts/test-local-loop-play.sh
CAPSTONE_ARTIFACTS="$PWD/artifacts/mbct-localization" ./scripts/build-android.sh
```

Unity는 라이선스가 활성화된 개발 계정으로 실행한다. 기존 저장본을 수정하는 Play 검증에 실제 사용자 저장 위치를 사용하지 않는다. 새 합성 저장 폴더를 지정한다. 화면 검증 진입점은 `CapstoneDesign.EditorTools.MbctPreview.Capture`이며 이 실행 환경에서는 OpenGL 소프트웨어 렌더러를 사용한다. 렌더러와 이미지 개수는 `artifacts/mbct-localization/visual/manifest.json`에 기록한다.

## 초기 구현 확인 결과

- MBCT: 507개 도메인·콘텐츠 검증 통과.
- 기존 로컬 저장: 324개 회귀 검증 통과. 과거 10개 경제를 전제로 한 회귀 fixture는 명시적인 설정값 10으로 유지하고 새 MBCT 검증은 기본값 5를 확인한다.
- Unity 편집 모드: 최종 UI 변경 후 19개 통과.
- Play: fresh/restore 모두 PASS. 실제 파일 저장 후 재실행에서 진도·기록·계획·정원 복원 확인.
- 화면: 세 비율 42장. 합성 데이터이며 실기기 확인이 아니다.
- Android: 최종 소스의 APK 빌드 성공. `artifacts/mbct-localization/build/capstone-mockup.apk` (A7 검증 결과는 아래 링크).

검증 자료는 `artifacts/mbct-localization/`, 실행 로그는 `artifacts/mbct-*.log`에 있다. 사용자 안내문 검토와 임상 효과 검증은 이 개발 작업의 결과로 주장하지 않는다.

## A7 실기기 후속 검증

Galaxy Tab A7 SM-T500 / Android 10에 동일 APK를 설치해 실제 보상·기록·계획·재실행 복원을 확인했다. 별도 MOCK 저장본으로 48개 활동의 147개 단계와 완료 저장을 확인했다. 자세한 범위와 자동 터치 재시도는 [A7 검증 기록](MBCT_A7_VALIDATION.md)을 참고한다.

## UI 개선 후속 검증

화면 겹침·기존 디자인 얼굴 선택·상태 복사 병목·강조와 아이콘 개선의 최신 검증은 [MBCT UI 개선과 응답 속도 검증](MBCT_UI_POLISH.md)에 기록한다. 위 수치는 초기 구현 시점의 결과다.
