# 2·4주 중심 QA 구현 검증 — 2026-10-27

## 구현 결과

2주 12개, 4주 24개 활동을 독립된 완결 과정으로 제공하고 선택 화면에서 앞에 배치한다. 기존 8주 48회 진도와 기록을 유지하며 기간별 진도를 따로 보존한다. 전환·복습으로 보상을 중복 지급하지 않고 모든 기간에 하루 한 개·주 여섯 개 한도를 합산한다. 하루 전환 직후 완료도 보상·진도·일일 결정을 한 저장으로 처리한다.

Wanted Sans 본문·설정·버튼과 LINE Seed KR의 선택적 정원 제목을 적용했다. 넓은 주 버튼 가장자리에 직접 그린 잎·덩굴을 넣고 터치 영역과 기존 글자 크기를 유지했다. 성장 구매에 사용자가 고른 활동을 연결해 원래 활동/감정을 다시 열 수 있으며, 주간·누적 기록 요약과 배운 활동의 선호·시간 기반 추천을 제공한다.

선택적 개인화 서버 경로는 기본 꺼짐이다. 별도 연결 설정과 동의가 있을 때 최소 행동 이벤트를 저장·전송하고 철회 시 대기 기록 및 서버 기록을 삭제한다. 인증·중복 방지·정책 만료 시 로컬 대체와 재현 가능한 모델 평가를 구현했다. 운영 배포나 실제 참여자에게 더 좋은 추천을 입증했다는 의미는 아니다.

세부 정책은 [코스와 성장 기록](COURSE_PROGRAMS_AND_GROWTH_RECORDS.md), [글꼴 출처](UI_FONT_ATTRIBUTION.md), [서버와 평가](../server/README.md)를 따른다.

## 실행 근거

원시 화면·기기 기록은 공개 저장소에 포함하지 않는다. 다음은 무시되는 `artifacts/qa-final-20261027/` 아래 실행 결과다.

| 실행 | 결과와 범위 |
| --- | --- |
| `standalone-unit/results.json` | 독립 Mono/Kotlin/Java 환경의 필수 순수 검증 16/16 PASS. Python 실행기/기기 세션 모사 42개와 서버/평가 22개 포함. |
| `unity-wrapper-harness/results.json` | Unity 실행기가 시간 초과 후 분리된 Editor를 남기지 않도록 수정한 뒤 Python 44/44 PASS. 모사 Editor의 PID·종료 코드·잠금 해제 검증 포함. |
| `remote-ci-c57b6eb/results.json` | 원격 GitHub Actions에서도 커밋 `c57b6eb`의 필수 순수 검증 16/16 PASS, SKIP 없음. |
| `qa-cancellation-harness/results.json` | 외부 QA 취소가 분리된 Editor까지 전달되는 회귀 검증을 추가해 Python 45/45 PASS. 보호 옵션을 제거하면 실패하는 음성 대조도 확인. |
| `remote-ci-4482912/synthetic-unit-results/results.json` | 실행 취소 수정까지 포함한 원격 커밋 `4482912`에서 필수 순수 검증 16/16 PASS. Python 45개 포함, SKIP 없음. |
| `boundary-unit/results.json` | 하루 전환 수정 후 관련 네 묶음 재실행 PASS. MBCT 1,424개와 로컬 상태 324개 assertion 포함. |
| `editor-boundary/local-loop-validation/results.json` | 최종 도메인 수정 후 Unity 통합 39/39 PASS. 코스·성장 연결·요약·추천 설정과 저장 보존 포함. |
| `unity-other/editmode.xml` | Unity EditMode 151/151 PASS, SKIP 없음. |
| `play-synthetic/play-fresh.json`, `play-synthetic/play-restore.json` | 별도 합성 저장의 실제 Editor Play 새 실행/복원 PASS. Unity 검색 인덱스 예외와 오디오 초기화 경고는 로그에 남아 있으며, 이 결과가 무경고 실행이나 스피커 청취 검증은 아니다. |
| `personalization-network/result.json` | 실제 UnityWebRequest의 동의·전송·철회·삭제 PASS. 클라이언트 10개 확인 및 독립 DB 검사, 임시 자격·큐·DB 정리 완료. |
| `preferences-gallery` | 추천 설정 8가지 구성 × 위/아래 스크롤 × 3크기 = 48장, 자동 텍스트 overflow 0. 대표 12장은 Vision 직접 검토. |
| `music-buffer` | 음악 Play 54개 확인 PASS. 낮/밤 실제 오디오 PCM, 06시/18시 가상 시각 전환, 페이드·음소거·일시정지·복귀 포함. |

이전 변경 단계의 `artifacts/qa-implementation-20261026/gallery`와 `growth-gallery`에는 코스/성장/요약 화면이 있으며 각 실행에서 overflow 0을 기록했다. 대표 화면 31장을 별도 Vision 검토했다. 전체 상태 조합이나 접근성 인증을 의미하지 않는다.

공유 제목·하단 메뉴의 임시 조사 단계에서 `chrome-regression/synthetic-save/play-fresh.json`과 `play-restore.json`도 PASS다. 원본 A7 PNG의 제목·설정 아이콘·글자 영역은 화면 전환 전후 픽셀이 동일했으며, 처음의 표시 누락 판정은 이미지 판독 오류였다. 임시 갱신·진단 코드는 모두 제거했고 그래픽 API 설정도 변경하지 않았다. 별도 그래픽 Editor 검증은 약관 동의창에서 실행 전 시간 초과되어 통과로 세지 않는다. 해당 미실행 기록은 보존하며 실제 A7 전환 검증과 구분한다.

음악 첫 실행의 일시정지 검증은 실패했다. 즉시 source 정지 상태와 믹서 버퍼 처리 후 샘플 안정성을 구분하도록 테스트를 수정했고, 클립·재생 위치 보존도 함께 확인했다. 처음 실패한 기록은 유지하며 runtime 음악 코드는 변경하지 않았다.

## 기기와 배포

최종 APK는 `android-release/build/capstone-mockup.apk`이며 크기는 122,577,630바이트, SHA-256은 `d0c15e2959f050a050d5f047b6fb29b9d56a1f90b63020f48b6e74a7bac3a810`다. 기존 자료를 백업하고 A7에 `install -r`로 업데이트했으며 설치된 APK의 해시 일치를 확인했다. 앱 데이터와 logcat은 지우지 않았다.

실기기에서 2주 선택→프로세스 재시작→12회 코스 유지, 4주 24회 전환, 원래 8주 진도 복귀를 확인했다. 참여 기록·성장 연결 없음 안내·주간/누적 요약·설정·네이티브 알림 시각 선택기 취소를 확인했다. 마지막 빈 성장 목록 카드 제거도 최종 APK에서 직접 확인했다. 활동 완료·구매·일지 입력은 실기기에 추가하지 않았다. 합성 저장에서는 성장 연결·실패/복원까지 별도로 검증했다.

`device-chrome-final/analysis/DATA_PRESERVATION.md`의 비교에서 테스트 전후 정원 주 저장 전체가 바이트까지 동일했으며, 기존 활동·일지·진도·정원·보상·설정·플랫폼 기록을 보존했다. 런타임 메타데이터와 DB 보조 파일 외에 개인 자료 변경은 없었다. 최종 설치와 화면 확인 자료는 `device-release/`에 있으며 낮 12시와 밤 0시는 앱의 가상 정원 시각으로 촬영했다.

종료 시 가상 정원 시각을 해제하고 기존 코스 선택을 유지했다. 화면 꺼짐 시간·밝기·자동 밝기는 세션 원래 값으로 복구하고 기기에서 다시 읽어 확인했다. 최종 APK의 제한된 logcat 검사에서 앱 예외·치명 종료·ANR 표시는 발견되지 않았으며, 이것이 장시간 안정성 보증을 의미하지는 않는다.

첫 최종 패키징은 빌드 환경 공간 부족으로 실패했다. 기기 자료와 실행 근거를 보존하고 재다운로드 가능한 도구 캐시 및 완료된 빌드 중간 산출물을 정리한 후 같은 소스로 재빌드해 성공했다. 실패 로그도 보존했다.

## 남은 검증 경계

[자동화 범위표](QA_AUTOMATION_AND_COVERAGE.md)의 실제 기기 알림 소리·OS 지연, 지원 휴대폰/워치 Health Connect, 접근성, 장시간 성능/배터리, 운영 TLS 경로 및 실제 사용자 평가를 별도로 추적한다. 합성 자료의 통과로 이를 대신하지 않는다. 알림 이벤트 전체 생산자 연결도 현재 서버 계약 지원과 구분한다.
