# 합성 데이터로 UI 전체 경로 검토

`FullChoiceVisualCapture`와 `VisualAuditBuild`는 동일한 화면 순회 코드를
사용한다. 미션·코스·기록·정원·설정·입력 폼의 분기와 스크롤 위치를
합성 메모리 상태로 실행한다. 실제 사용자 건강 기록이나 정원 저장을
테스트 fixture로 사용하지 않는다.

## Editor 캡처

프로젝트의 `scripts/unity-run.sh`를 통해 Unity를 하나씩 실행한다.
렌더러가 필요하므로 `-nographics`는 사용하지 않는다.

```bash
export CAPSTONE_ARTIFACTS="$PWD/artifacts/visual-audit/example"
export CAPSTONE_VISUAL_AUDIT_SIZES="1200x2000,1080x2400"
bash scripts/unity-run.sh -batchmode -quit -projectPath "$PWD" \
  -executeMethod CapstoneDesign.EditorTools.FullChoiceVisualCapture.Capture \
  -logFile "$CAPSTONE_ARTIFACTS/run.log"
```

출력 폴더를 먼저 생성한다. `CAPSTONE_VISUAL_AUDIT_SECTIONS`는
`missions,shell,supplement,forms` 중 필요한 부분을 쉼표로 지정할 수 있다.
`CAPSTONE_VISUAL_AUDIT_FILTER`는 캡처 이름의 접두사 목록이다.
`choices`를 단독 지정하면 선택·입력·확인창을 합성 fixture로 재검토한다.
필터와 관계없이 상태 전이는 실행하여 선행 경로를 유지한다.
`CAPSTONE_VISUAL_AUDIT_RESUME=1`은 이미 존재하는 이미지를 건너뛴다.
소스가 달라졌으면 새 폴더를 사용하거나 관련 이미지를 먼저 보존한 뒤
다시 캡처해야 한다.

캡처 manifest, 텍스트 치수, 순회 기록은 출력 폴더에 저장된다.
`FULL_CHOICE_CAPTURE_PASS`는 경로 실행의 완료만 뜻하며 시각적 검토의
통과를 뜻하지 않는다. 별도로 실제 이미지를 검토한다.

## Android 실제 렌더링

`CapstoneDesign.EditorTools.VisualAuditBuild.Build`는 임시 장면과 별도
`com.capstonedesign2026.visualaudit` 패키지로 개발용 APK를 만든다.
빌드 종료 시 원래 패키지 ID와 장면을 복구한다. 일반 앱 장면에는
감사 실행기가 부착되지 않으며 릴리스 컴파일에서는 관련 타입을 제외한다.

ADB 작업은 `AGENTS.md`와 `scripts/android_device_session.py`를 따른다.
명시적 대상 serial, 기존 앱 백업, 디스플레이 설정 원본의 영구 기록과
복구 검증이 필요하다. 기기 캡처와 복구 기록은 ignore된 `artifacts/`에
보관하며 커밋하지 않는다. 화면 크기나 density를 임시로 변경하면
기존 override 유무까지 기록하고 작업 종료 시 복구한다.

실행 Intent에는 반드시 `--es platformInputMode MOCK`를 전달한다.
`AndroidTestMode.Awake`는 이 값이 없으면 실행기가 먼저 선택한 mock
플랫폼을 LIVE로 덮어쓴다. 지정한 mock 플랫폼과 합성 메모리 저장소로
순회해야 하며, 일반 앱 아이콘을 눌러 시작하는 방식은 사용하지 않는다.

진행 및 완료 표시는 별도 앱의 외부 파일 폴더 아래 `visual-audit/`에
저장된다. Intent의 `visualAuditSections=forms`를 추가하면 입력 폼만 실행하며
출력은 `visual-audit-forms/`에 저장된다. `--es visualAuditSections choices`는
선택·입력·확인창을 재검토하고 `visual-audit-choices/`에 저장한다. `FAILED.txt`와 실행 오류를
확인하고, `COMPLETE.txt` 및 manifest가 있어야 완료로 판단한다.
다시 실행할 때는 감사 앱을 종료하고 해당 출력 폴더의 이전 결과를
보존한 뒤 비운다. 이전 `COMPLETE.txt`, `FAILED.txt`, 키보드 요청 파일이
남아 있으면 새 실행의 상태로 오인할 수 있다. 이 정리는 별도 감사 앱의
출력 폴더에만 적용하며 일반 앱 데이터나 디스플레이 복구 기록을 지우지 않는다.

Unity framebuffer에는 Android 키보드가 포함되지 않는다.
`awaiting-system-capture.txt`가 생기면 호스트에서 키보드 표시 상태와
`adb exec-out screencap -p` 결과를 보관한 뒤 이 요청 파일을 삭제해야
순회가 계속된다. Editor 캡처만으로 키보드 가림 문제가 없다고
판정하지 않는다. 전체 순회 완료 후 native history 및 시간 선택기
검토용 명령도 별도 앱에서만 허용된다. 시간 선택기는 취소하여
알림 설정을 바꾸지 않는다.

네이티브 시간 선택기만 확인할 때는 같은 감사 APK를
`--es platformInputMode MOCK --es visualAuditSections native`로 실행한다.
이 모드는 UI 순회나 history Activity 자동 전환 없이
`visual-audit-native/NATIVE_READY.txt`를 기록하고 명령을 기다린다.
`COMPLETE.txt`나 화면 manifest는 만들지 않는다. 디스플레이 크기와 density를
변경할 필요가 있으면 실행 전에 적용하고 네이티브 화면 검토가 끝난 뒤 복구한다.
Unity Activity가 전면에서 실행 중이고 새 `NATIVE_READY.txt`가 생성되었음을
확인한 후 같은 폴더의 `native-command.txt`에 `time-picker`를 기록한다.
허용 명령은 `time-picker`와 `sensor-history`뿐이며, 별도 패키지 및 MOCK
모드인지 다시 확인한다. 이전 `native-result.txt`는 명령 처리 시 교체되고,
새 결과와 명령 파일 삭제를 함께 확인해야 응답으로 판단한다.
응답은 호출 결과이므로 실제 다이얼로그 표시 여부는 시스템 캡처로 별도 확인한다.
재실행 전에는 해당 출력 폴더의 이전 결과와 준비·명령 파일을 보존한 뒤 비워야 한다.

## 회귀 검증과 한계

- `ScreenDensityTextValidation.Run`: 중첩 배율의 텍스트 측정·렌더링,
  InputField caret 규약, 글꼴 atlas 갱신 중 재진입을 검증한다.
- `BuildTools.ValidateAndroidReleaseScripts`: 개발용 디버그 메서드와
  visual audit 타입이 릴리스 어셈블리에 남지 않는지 확인한다.

이 순회는 UI 표시 검토를 돕는다. 합성 사진 인식 결과·mock 건강
데이터·가상 시간은 실제 카메라 인식, 센서 수집, 권한 승인, 서버 통신,
일 단위 예약 동작의 기능 검증을 대신하지 않는다.
