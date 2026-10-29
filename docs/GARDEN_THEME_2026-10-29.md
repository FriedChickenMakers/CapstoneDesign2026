# 정원 게임 UI 테마 적용 — 2026-10-29

사용자가 승인한 무료 UI 에셋 적용표를 기준으로, 기존 화면에 밝은 종이·목재 명패·초록 버튼·채색 원예 아이콘을 적용했다. 새 이미지 팩을 구매하거나 내려받지 않고 저장소 안에서 직접 작성한 uGUI 벡터 도형을 사용한다. 미션 완료 조건, 추천 정책, 저장 형식과 데이터 전송 계약은 이 변경의 대상이 아니다.

## 적용표

`적용`은 현재 화면에 연결한 부품, `부분`은 공통 테마를 적용했지만 제안했던 전용 그림 일부가 남은 항목, `보류`는 이번에 추가하지 않은 선택적 아트다. 같은 부품을 여러 화면에서 재사용한다. 스크린샷으로 제안 형태와 대조한 결과, 총 41개 항목 중 적용 31개, 부분 적용 7개, 선택 항목 보류 3개다. 아이콘 버튼·활동 상태 표식·설정 아이콘은 전용 그림 세트 일부가 없어 부분 적용으로 정정했다.

| 유형 | 상태 | 실제 적용 및 남은 범위 |
| --- | --- | --- |
| 공통 화면 배경 | 적용 | 따뜻한 크림색 바탕과 낮은 대비의 가장자리 잎 장식. |
| 공통 카드·패널 | 적용 | 밝은 종이, 얇은 테두리, 은은한 그림자와 종이 가장자리 표현. |
| 화면 제목·섹션 명패 | 적용 | 목재 제목판과 설정 섹션 명패. 긴 한글은 기존 자동 레이아웃을 사용. |
| 패널 모서리 장식 | 적용 | 화면 가장자리 식물 장식과 선택된 패널의 잎 표식. 모든 카드에 장식을 반복하지 않는다. |
| 구분선 | 적용 | 공통 목재색의 얇은 선으로 내용과 조작 영역을 구분. |
| 강조 버튼 | 적용 | 두께 있는 초록 표면, 밝은 안쪽 테두리, 양끝의 큰 잎·덩굴·꽃 장식. |
| 보조 버튼 | 적용 | 따뜻한 목재 표면과 얕은 입체 테두리. |
| 아이콘 버튼 | 부분 | 작은 목재 확대·축소 버튼은 문자 기호를 사용. 원형 뒤로·닫기 그림 세트는 미구현. |
| 선택형 버튼 | 적용 | 초록 테두리와 밝은 잎색 면, 모서리 잎 표식. |
| 버튼 상태 표현 | 적용 | 기본·눌림·선택·비활성 표면. 기존 버튼의 활성 여부와 콜백을 유지. |
| 하단 탐색 바 | 적용 | 목재 선반 형태의 공통 바탕. |
| 하단 탐색 아이콘 | 적용 | 화분·수첩·목재 톱니 아이콘. 기존 탭 기능과 순서를 유지. |
| 선택된 탭 표시 | 적용 | 잎색 패널, 초록 테두리, 작은 잎과 굵은 글자. |
| 스크롤 표시 막대 | 적용 | 종이에 어울리는 얇은 목재색 손잡이. |
| 홈 정보판 | 적용 | 날짜·식물 이름·영양제를 담는 목재 명패. |
| 정원 시점 조작 버튼 | 부분 | 목재 버튼에 기존 보기·확대·축소 문자를 유지. 전용 확대경 그림은 아직 연결하지 않음. |
| 성장 단계 표시 | 적용 | 씨앗·잎·화분·꽃 그림과 도달/미도달 상태의 차이. |
| 영양제 아이콘·수량 표시 | 적용 | 채색 영양제 병과 기존 수량 텍스트. |
| 추천 활동 카드 | 적용 | 밝은 잎색 종이 카드와 모서리 잎 표식. |
| 활동 목록 항목 | 적용 | 종이 목록과 채색 수첩·잎·상태 아이콘. |
| 활동 종류별 삽화 — 선택 | 보류 | 걷기·호흡·먹기 등 각 활동에 대한 큰 전용 삽화는 제작하지 않음. |
| 코스 선택 카드 | 적용 | 종이/선택 패널과 식물 아이콘으로 기존 기간 선택 목록을 통일. |
| 코스 진행 막대 | 적용 | 목재 트랙 안에 초록색이 차는 진행 막대. |
| 개별 미션 단계 표시 | 부분 | 현재 단계 번호와 설명을 목재 제목판에 배치. 별도 책갈피·단계별 잎 그림은 미적용. |
| 미션 안내문 패널 | 적용 | 테두리가 있는 밝은 종이 패널. 중앙 읽기 영역은 단순하게 유지. |
| 서술형 입력란 | 적용 | 밝은 메모지 표면과 입력 영역 경계. 기존 초안 저장·선택 입력 동작 유지. |
| 기분 선택 아이콘 | 적용 | 구별되는 다섯 표정, 따뜻한 원형 테두리, 선택 체크 표시. 기분 의미와 개수 유지. |
| 활동 상태 표식 | 부분 | 화살표·자물쇠와 기존 상태 설명을 사용. 다섯 상태 각각의 고유 그림 세트는 미완성. |
| 활동 완료·보상 표시 | 적용 | 꽃 또는 체크 그림과 기존 완료·영양제 안내. |
| 성장 전후 미리보기 틀 | 적용 | 종이 액자와 안쪽 여백. 기존 3D 미리보기와 비교용 카메라 동작 유지. |
| 정원 꾸미기 선택 항목 | 부분 | 기존 선택 버튼과 미리보기 틀에 공통 테마 적용. 항목별 상품 삽화는 미적용. |
| 참여·성장 기록 카드 | 적용 | 종이 목록, 수첩 아이콘, 기존 날짜와 기록 요약. |
| 기록 상세 패널 | 적용 | 밝은 수첩 페이지 형태의 읽기 영역. 긴 기록의 스크롤 유지. |
| 건강 기록 그래프 패널 | 적용 | Unity 기록 화면의 공통 종이 표면. 그래프 선·눈금·수치의 의미와 조회 로직 유지. |
| 발견한 동물 삽화 — 선택 | 보류 | 기존 동물 기록을 유지하며 별도 도감 그림은 추가하지 않음. |
| 설정 섹션 패널 | 적용 | 종이 패널 안의 목재 제목표. |
| 설정 아이콘 | 부분 | 종·화분·하트·자물쇠를 연결. 별도 음표·연결·정보 아이콘 세트는 미연결. |
| 켜짐·꺼짐 토글 | 적용 | 목재색/초록 트랙, 손잡이 위치와 켜짐·꺼짐 글자를 함께 표시. |
| 안내·오류 표식 | 부분 | 기존 명시적인 상태 문구와 경고색을 유지. 별도의 오류 삽화나 애니메이션은 추가하지 않음. |
| 기록이 없는 화면 삽화 — 선택 | 적용 | 빈 참여 기록 화면에 수첩 그림 추가. 기존 빈 상태 안내문 유지. |
| 완료·성장 효과 — 선택 | 보류 | 정적인 꽃·잎 표현을 우선 적용. 움직이는 꽃잎·물방울·반짝임은 추가하지 않음. |

## 출처와 라이선스

새 표면, 목재 무늬, 잎·꽃 장식, 아이콘 및 표정은 아래 C# 파일에 직접 작성한 벡터 지오메트리다. 외부 게임 화면이나 유료 팩의 이미지를 앱에 복사하지 않았다. 새 외부 에셋 구매 비용과 추가 텍스처 의존성은 없다. 이 문서는 새 아트를 별도의 공개 도메인 라이선스로 재지정하지 않는다.

기존 Wanted Sans와 LINE Seed KR 글꼴은 그대로 사용하며 출처·저작권·OFL 파일은 [글꼴 출처 문서](UI_FONT_ATTRIBUTION.md)를 따른다. 기존 3D 정원·식물·배경음악의 자산과 출처도 유지한다.

## 코드 연결과 협업

| 파일 | 역할 |
| --- | --- |
| `Assets/Scripts/Prototype/GardenThemePalette.cs` | 종이·목재·식물·본문의 공통 색상. |
| `Assets/Scripts/Prototype/GardenThemeSurface.cs` | 확장 가능한 표면과 버튼 상태. `Apply(owner, style)`은 장식 자식 하나를 생성/재사용한다. |
| `Assets/Scripts/Prototype/GardenButtonBotanicals.cs` | 기존 호출부와의 호환 연결. 강조 버튼을 새 식물 표면으로 연결한다. |
| `Assets/Scripts/Prototype/PrototypeUiIcon.cs`, `PrototypeMoodFace.cs` | 채색 아이콘과 표정. 기존 enum 값과 호출 API 유지. |
| `Assets/Scripts/Runtime/GardenUi.cs` | 활동·기록·입력·버튼·진행 막대의 공통 연결. |
| `Assets/Scripts/Runtime/GardenHomePresenter.cs`, `MockupApplication.cs` | 홈·공유 제목·하단 탐색·성장 단계의 연결. |
| `Assets/Scripts/Runtime/WeekOneQuestDemo.cs`, `WeekOneQuestDemo.ActivityLayout.cs` | 활동 제목·목록·미리보기·완료·빈 기록 화면의 연결. |
| `Assets/Scripts/Runtime/WeekOneQuestDemo.HeartHistory.cs` | 건강 그래프를 새 배경 위, 기존 조작부 아래에 유지. |
| `Assets/Scripts/Runtime/SensorRawDisplay.cs` | 설정 패널·섹션 아이콘·토글의 연결. |
| `Assets/Editor/Automation/GardenThemeCapture.cs`, `GardenLightingValidation.cs` | 합성 화면 캡처, 상태 보존·그래프 레이어·실제 글자 노출 검사. |

표면 스타일은 `Paper`, `Wood`, `Primary`, `Selected`, `Backdrop`, `Header`, `Shelf`, `Input`, `ProgressTrack`, `ProgressFill`이다. 장식은 `raycastTarget=false`와 `LayoutElement.ignoreLayout=true`로 기존 터치 영역과 레이아웃 계산에 참여하지 않는다. 제목의 글자 오브젝트 자체에 불투명 표면을 붙이지 않고, 그 글자를 담는 부모 패널에 붙인다.

이번 작업 브랜치는 `codex/overnight-local-loop`다. 작업 시작 후 원격을 확인했을 때 팀원의 `codex/individual-mission-ui`는 `5a235e0`까지 진행되어 있었다. 해당 브랜치나 `codex/mission-ui-next`, `codex/mission-personalization-next`를 변경·덮어쓰지 않는다.

개별 미션 UI 작업자는 새 컨트롤을 `GardenUi`와 공통 표면으로 연결하고, 특히 `GardenUi.cs`, `WeekOneQuestDemo.cs`, `WeekOneQuestDemo.ActivityLayout.cs`의 병행 수정 충돌을 확인한다. 개인화 작업자는 추천 입출력·저장 계약을 유지하고 기존 화면 생성 경로를 사용하면 테마가 이어진다. 각자의 기능 브랜치에 최신 기준을 통합할 때에는 공통 테마 파일을 먼저 반영하고 화면 변경을 수동 비교한 뒤 저장·입력·탭 이동 검증을 실행한다. 아직 타인의 브랜치에 자동 통합하지 않았다.

## 검증과 재현

검증은 OS 사용자 `codex`와 해당 계정의 HOME 환경에서 실행한다. 원시 화면과 로그는 무시되는 `artifacts/garden-theme-20261029/` 아래에 있으며 공개 저장소에 추가하지 않는다. 캡처의 상태와 기록은 합성 데이터이고 실제 사용자 저장을 사용하지 않는다.

| 검증 | 현재 결과 | 근거 |
| --- | --- | --- |
| 로컬 상태·MBCT·개인화·미션 추천 단위 검증 | 4/4 PASS | `unit/results.json` |
| 초기 Unity 프로젝트 연결·로컬 기능 통합 | 2/2 PASS | `unity-initial/results.json` |
| 초기 주요 화면 캡처 | 5장 PASS | `smoke/theme-validation.txt` 및 `smoke/visual/` |
| 최종 Unity 프로젝트 연결·로컬 기능 통합 | PASS, local-loop 39/39 | `unity-final/results.json`, `unity-final/local-loop/local-loop-validation/results.xml` |
| EditMode | 161/161 PASS, 실패·건너뜀 0 | `unity-final/editmode.xml` |
| Play Mode 저장·재시작 복원 | fresh/restore PASS | `unity-final/results.json` 및 `play-save-restore/` |
| 다중 화면·해상도 캡처 | 20개 화면·상태 × 4개 크기, 80장 PASS | `gallery/theme-validation.txt` |
| 그래프 레이어 수정 후 재캡처 | 상세·건강 기록 × 4개 크기, 8장 PASS | `gallery-records-final/theme-validation.txt` |
| 긴 제목·선택지·스크롤 등 기존 화면 검사 | 138장 PASS, 잠재적 글자 넘침 0 | `visual-final/tabs/environment.txt` |
| 홈 조작·성장 구매 검증 | 4개 크기 PASS: 버튼 raycast·보기·시간 프리셋·성장 비교·취소·실패 저장 복구·중복 구매 방지 | `demo-polish-verified/` |
| Android 개발용 APK | Unity 전체 빌드 PASS, APK 서명 검사 PASS, 약 118 MiB | `android-complete/output.log`, `android-complete/build-result.json`, `android-complete/build/capstone-mockup.apk` |

초기 검증은 이후 성장 단계·미리보기 여백 보강 전 결과이므로 최종 검증과 구분한다. Unity는 `LicenseClient-codex`에서 사용권을 인식했으며 라이선스 파일을 변경하지 않았다. `GardenThemeCapture`는 화면 렌더링 전후 저장 내용이 같고 장식이 터치를 가로채지 않는지 확인한다. 눌림 화면은 합성 포인터 이벤트로 만든다. 상세 글자 잘림과 스크롤 검사는 기존 `TabConsistencyCapture`로 보완한다.

초기 갤러리를 직접 확인하면서 건강 그래프가 새 배경 뒤로 가려진 문제를 발견해 형제 오브젝트 순서를 수정했다. 수정 후 캡처에는 그래프가 배경 위에 있는지도 검사한다. 홈 가로 화면 검사는 기존의 초록색 픽셀 조건이 목재 위의 보조 글자를 놓쳐 실패했다. 글자를 잠시 숨긴 대조 프레임과 비교해 실제 글자가 화면에 기여하는지 검사하도록 수정했고, 연속 프레임 일치와 최소 20픽셀 조건은 유지한다. 최초 실패 로그는 보존한다. Android 빌드는 초기 병렬 컴파일 자원 압박과 디스크 부족을 겪었다. 사용자 디스크 10 GB 증설 후 `BEE_BUILD_THREADS=2`로 원래 Unity 빌드 명령을 끝까지 재실행해 성공했다. 임시로 옮긴 빌드 캐시는 원래 위치와 계정 소유권으로 복원했고, 소스·기기 백업·기록은 제거하지 않았다.

검토용 `review/index.html`은 수정 후 80개 화면을 선택해 볼 수 있고, 그중 21개는 보관된 이전 화면과 나란히 비교한다. 이전 자료는 10월 27~28일 합성 캡처이므로 날짜·진도·수량까지 같은 상태는 아니다. 기록 화면 링크는 수정 후 `gallery-records-final/`을 가리킨다. 홈·활동·미션·기분 선택·성장 미리보기·기록 그래프·설정·가로 화면과 눌림/비활성을 직접 확인했다.

새 결과 디렉터리를 준비한 뒤 실행한다. 모든 Unity 실행은 프로젝트 공통 잠금을 사용하는 `scripts/unity-run.sh`를 통한다.

```bash
runuser -u codex -- mkdir -p artifacts/garden-theme-review
runuser -u codex -- env HOME=/home/codex DISPLAY=:99 \
  CAPSTONE_ARTIFACTS=/workspace/CapstoneDesign2026/artifacts/garden-theme-review \
  bash scripts/unity-run.sh -batchmode -force-vulkan -quit \
  -projectPath /workspace/CapstoneDesign2026 \
  -executeMethod CapstoneDesign.EditorTools.GardenThemeCapture.Capture \
  -logFile /workspace/CapstoneDesign2026/artifacts/garden-theme-review/capture.log
```

`CAPSTONE_GARDEN_THEME_CASES`에 쉼표로 화면 이름을 주거나 `CAPSTONE_GARDEN_THEME_SIZES=900x1950`처럼 크기를 지정하면 일부만 재현할 수 있다. 기본 캡처는 20개 화면·상태와 네 해상도 조합이다. 새 검증 클래스는 `Assets/Tests/Prototype/Editor/GardenThemeSurfaceTests.cs`에 있다.

## 41개 항목별 스크린샷 검토 자료

사용자가 제공한 목록과 같은 순서로 01~41 번호를 부여했다. 로컬 검토 자료는 `artifacts/ui-item-screenshots-20261029/index.html`이며, 총 52개 원본 화면 참조로 41개 항목을 설명한다. 각 항목에는 확인 영역 확대, 원본 전체 화면, 현재 구현과 제안 형태의 차이를 포함한다. 사진·월페이퍼는 아직 제공받지 않았으므로 현재 벡터 종이 표면으로 표시한다. 보류된 활동별 삽화·동물 그림·움직이는 효과에는 현재 적용 위치의 화면만 첨부한다.

- `screenshots/01-01.png` 형식: 번호별 Unity 원본 PNG 복사본. 원본과 SHA-256을 대조해 픽셀 파일이 바뀌지 않았음을 확인한다.
- `item-sheets/01.png` 형식: 항목 제목·비고와 원본 확대 영역을 담은 검토 페이지의 브라우저 캡처. 앱 화면 원본과 구별한다.
- `manifest.json`: 원본 경로, 해시, 이미지 크기와 확대 영역 좌표.
- `ui-41-screenshots.zip`: 오프라인 검토 HTML·원본 PNG·항목별 검토 이미지 묶음.

추가 검증은 모두 codex 계정에서 진행했다. 기존 `TabConsistencyCapture.CaptureRemaining`으로 꾸미기·동물·성장 기록 등 합성 화면 195장을 촬영했고 잠재적 글자 넘침은 0이었다. `GardenThemeCapture`에는 기본 20개 경우를 유지하면서 선택 실행 가능한 `settings-toggle-off`와 `shop-progress`를 추가했다. 전자는 실제 토글 렌더러에 꺼짐 표현만 적용해 PlayerPrefs·음악 설정을 저장하지 않는다. 후자는 합성 2주 코스에서 3개 활동을 완료해 3/12 진행 막대를 보여준다. 두 경우와 기본 설정 화면 총 3장 모두 상태 보존·장식 비입력 검사에 통과했다. 근거는 `controls-final/theme-validation.txt`와 `supplemental/environment.txt`다. 제품 런타임 동작은 이 캡처 보강에서 수정하지 않았다.

## 변경 기록

기준 커밋은 `3457b7d`이며, 위 파일과 이 문서만 현재 작업 브랜치에 커밋하고 해당 upstream으로 일반 push한다. 검증 중 Unity가 자동 생성한 동적 글꼴 캐시 변경은 제외한다. 원시 로그·스크린샷·APK·전후 비교 ZIP은 무시되는 `artifacts/`에 보존한다. 실제 commit/push와 원격 HEAD 대조 기록은 같은 결과 디렉터리의 `workflow-record.json`에 남긴다.

## 검증 경계

- 이 문서 작성 시점에는 S24·A7 실기기 테스트를 하지 않았다. Editor 화면으로 실제 기기의 터치·키보드·성능 검증을 대신하지 않는다.
- Android 시스템 권한 창, 알림 시간 선택기와 네이티브 건강 차트의 디자인은 유지한다. Unity 화면과 구분한다.
- 기존 3D 식물의 메시·재질 품질을 이번 UI 테마 변경으로 개선한 것은 아니다.
- 선택적 활동별 대형 삽화·동물 도감 그림·애니메이션 효과는 위 표에 표시한 범위로 남는다.
