# 배경 음악

사용자가 프로젝트 BGM으로 제공한 기존 곡은 낮에, 추가 제공한 `the moon.wav`는 밤에 사용한다. 런타임 자산은 각각 `Assets/Resources/Audio/GardenTheme.ogg`, `Assets/Resources/Audio/GardenNightTheme.ogg`다. 원본의 저작권이나 라이선스를 변경하지 않으며 외부 무료 음원으로 표시하지 않는다. 원본 WAV와 검토용 미리보기는 저장소에 포함하지 않는다.

기존 낮 음원:

- 원본: 147.428583초, 48 kHz, 스테레오, 24-bit PCM WAV, 약 42.46 MB.
- 런타임 자산: 같은 길이·샘플레이트·채널, Ogg Vorbis quality 6, 약 3.23 MB. 손실 압축이며 무손실 변환으로 표현하지 않는다.
- 제목·작성자·인코더 등 선택적 태그를 제거했다. Ogg/Vorbis의 필수 코덱 헤더는 유지한다.
- 처음 80 ms와 끝 2초에 페이드를 적용했다. 원곡은 루프용으로 제공되지 않았으므로 곡 끝에 음량이 내려갔다가 다시 시작한다. 박자나 화성이 끊김 없이 연결되는 seamless loop가 아니다.
- 원본 수치 검사: 평균 -18.8 dBFS, 최대 -2.3 dBFS. 플레이어 기본 음량은 0.25다. 최종 체감 음량은 기기 미디어 음량과 스피커에 따라 달라진다.

추가 밤 음원:

- 원본: 154.285737초, 44.1 kHz, 스테레오, 32-bit float PCM WAV, 약 54.43 MB.
- 런타임 자산: 같은 길이·샘플레이트·채널, Ogg Vorbis quality 6, 약 3.41 MB. 트림·정규화·음원 자체의 페이드는 추가하지 않았다.
- 원본의 선택적 태그는 없었으며 변환 시 메타데이터 전달을 차단했다. 생성된 코덱 인코더 태그만 남는다.
- 변환본 전체 디코딩 검사 통과. 평균 -23.2 dBFS, 최대 -3.3 dBFS. 원곡의 상대적인 음량과 강약을 유지한다.
- 시작 약 3초는 거의 무음이다. 시간대 전환은 8초 동안 이전 곡을 겹쳐 재생해 이 도입부를 덮는다. 곡 자체는 전체 길이로 반복하며 seamless loop로 편집하지 않았다.

## 트랙 지정과 앱 연결

`Assets/Resources/Audio/GardenMusicProfile.asset`의 `homeGarden`, `activity`, `settings`는 낮에 사용할 곡이다. 현재 세 항목 모두 기존 곡을 가리킨다. `night`는 모든 탭에서 사용할 밤 곡이다. 낮은 **06:00 이상 18:00 미만**, 밤은 **18:00 이상 다음 날 06:00 미만**이며 가로등과 같은 시간대 판정을 사용한다. 활동 또는 설정의 낮 곡 참조를 비우면 홈/정원 곡을 쓰고, 밤 곡이 없으면 기존 화면별 곡으로 돌아간다. 프로필의 기본 음량은 0.25, 곡 전환 페이드 시간은 8초다.

실제 기기 시각으로 시작하며 앱을 켜 둔 동안에도 시간대를 확인한다. 06:00 또는 18:00에 탭 이동 없이 전환을 시작한다. 설정의 ‘정원의 시간’ 미리보기에서도 곡이 함께 바뀌며 ‘실제 시간으로’를 누르면 현재 시각에 맞는 곡으로 돌아간다. 같은 낮·밤 구간에서 시간을 바꾸거나 자정을 넘어도 곡을 다시 시작하지 않는다. 이 표시는 시스템 시각·활동 날짜·센서 기록·알림을 변경하지 않는다.

```csharp
GardenMusicPlayer.Ensure().SetContext(GardenMusicContext.HomeGarden);
GardenMusicPlayer.Ensure().SetContext(GardenMusicContext.Activity);
GardenMusicPlayer.Ensure().SetContext(GardenMusicContext.Settings);
GardenMusicPlayer.SetMuted(true);
bool muted = GardenMusicPlayer.IsMuted;
```

플레이어는 탭이나 카메라와 독립된 영속 오브젝트다. 홈과 정원은 같은 컨텍스트를 사용한다. 같은 AudioClip으로 이동할 때 `Play`를 다시 호출하지 않아 재생 위치가 유지된다. 다른 곡으로 바꾸면 두 AudioSource의 게인 제곱을 보간하는 equal-power 교차 페이드를 적용하고, 완료 후 이전 채널을 정지한다. 빠르게 이전 곡으로 돌아올 때 아직 재생 중인 채널이 있으면 그 위치와 현재 음량을 이어 쓴다. 최대 두 채널만 사용한다.

음소거는 앱의 `capstone.music.muted` 설정에 저장한다. 음소거·앱 일시정지 중에는 음악 채널을 `Pause`하고 페이드 진행도 멈춘다. 데스크탑에서는 포커스 상실도 일시정지 이유다. Android에서는 소프트 키보드를 열어도 포커스 상실 이벤트가 발생하므로 `OnApplicationPause`를 백그라운드 전환 기준으로 사용한다. 키보드만 열고 활동을 입력할 때는 음악이 이어진다. 해당 일시정지 사유가 모두 해제되고 음소거가 꺼져야 `UnPause`하며 곡을 처음부터 재생하지 않는다. 기기 전체 음량이나 다른 앱 음악을 설정으로 변경하지 않는다. 근거: [Unity 포커스와 Android 키보드](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnApplicationFocus.html).

전용 AudioListener를 플레이어에 두어 홈 카메라가 비활성화되어도 활동과 설정에서 들을 수 있게 한다. 탭/장면 진입 때 다른 활성 AudioListener를 끄고, 플레이어가 제거되면 원래 활성화되어 있던 Listener를 복구한다. 음악은 2D이며 센서 수집 서비스나 Android 백그라운드 재생 서비스를 실행하지 않는다.

## 가져오기와 검증 범위

Unity importer는 스테레오 유지, Streaming, 백그라운드 로딩, preload 해제로 지정했다. 장시간 음악의 전체 PCM을 메모리에 상주시킬 필요가 없다. 근거: [Unity Audio Clip 설정](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioClip.html), [Pause](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.Pause.html), [UnPause](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.UnPause.html).

변환 결과는 ffprobe로 길이·채널·샘플레이트·태그를 검사하고 ffmpeg 전체 디코딩을 통과했다. 기존 낮 곡의 시작과 끝을 담은 짧은 청취용 미리보기는 비공개 작업 경로에 있다. 이 실행 환경에서는 음악적 연결감과 실제 스피커 음량을 청취로 확인하지 않았다. Unity 재생 위치·전환·포커스 동작과 실제 기기 재생은 별도의 런타임 검증으로 구분한다.

2026-10-15 `MusicPlayValidation.Start`의 Unity Play 검사 **49개가 통과**했다. 두 곡의 스트리밍 가져오기, 실제 음원 길이, 비영 PCM 출력(낮 peak 0.04953253, 밤 peak 0.02322660), 자정 실행 시 밤 곡 선택을 확인했다. 05:59→06:00 및 17:59→18:00은 시간 변경 이벤트 없이 시계 조회만으로 전환되며, 8초 페이드의 중간 음량·채널 파워 유지·완료 후 이전 채널 해제를 검사했다. 시간대 안의 탭 이동 및 자정 통과, 음소거·백그라운드 중 경계 통과 후 복귀, 빠른 낮·밤 왕복에서도 위치와 음량이 이어지고 최대 두 채널만 사용한다. 밤 곡이 없는 프로필의 기존 화면별 곡 선택도 보존한다.

기존 단일 Listener, 포커스·pause 순서, Android IME 정책의 합성 이벤트, 빠른 다른 곡 교차 전환 검사도 포함한다. 음소거는 상태를 즉시 검사한 뒤 DSP 한 버퍼 전달 이후 위치가 멈춰 있는지 검사한다. 환경은 FMOD nosound 출력이므로 실제 스피커 청취 및 기기의 실제 IME 이벤트 전달과 구분한다. 테스트는 격리된 MOCK 저장소를 사용하며 기존 음악 설정과 시간 미리보기를 복구했다. 보고서는 비공개 `artifacts/night-music-20261015/play-validation/music-validation.json`에 있다.

## Android 출력 진단

개발 빌드의 실행 Intent에 `capstoneAudioProbe=true`를 명시하면 `MusicDeviceProbe`가 켜진다. 일반 실행과 릴리스 빌드에서는 기록하지 않는다. 앱 저장 경로의 `capstone-music-probe.jsonl`에 0.5초마다 최대 1,200행을 기록하며, 포커스·pause 전환도 구분한다. 기존 진단 파일은 새 진단 세션 시작 시 교체된다. 백업과 격리 MOCK 실행을 먼저 준비한다.

기록 값은 음악 컨텍스트, 재생 위치, 음량, 음소거·일시정지 상태 및 PCM 최대 절댓값이다. 원본 PCM, 센서 값, 사용자 입력이나 기기 식별자는 저장하지 않는다. Unity master의 좌우 출력 중 큰 값을 `outputPeak`에 기록하고 `outputChannels`로 검사 범위를 남긴다. AudioSource의 `peak`도 최대 두 채널의 최댓값이다. 기기의 실제 스피커 소리나 OS 음량을 측정한 값은 아니다.

초기 출력 버퍼나 전환 직후의 0 한 건만으로 무음을 판정하지 않는다. 재생 중 비영 출력·위치 증가, 음소거 후 여러 안정된 표본의 출력 0·위치 정지, 해제 후 출력·위치 재개를 함께 확인한다. 앱이 배경에 있는 동안에는 Unity 업데이트가 멈출 수 있으므로 pause/resume 사이의 경과 시간과 재생 위치를 대조한다. 파일 기록 비용이 포함되므로 이 진단을 켠 실행의 프레임 수치를 일반 실행 성능으로 간주하지 않는다.

기존 낮 곡의 2026-10-12 A7 실기기 검증에서는 출력·위치 증가, 4.63초 음소거 중 출력 0·위치 정지, 해제 후 재개, 같은 곡의 탭 이동 연속성을 확인했다. 실제 키보드를 연 상태에서도 재생이 이어졌으며, 약 4.80초의 앱 백그라운드 구간에서는 위치가 유지되고 복귀 후 재개됐다. 해당 IME에서는 blur 이벤트가 발생하지 않았으므로 Android의 모든 키보드 이벤트 조합을 검증한 것은 아니다. 기기 진단 파일을 회수·정리하고 원래 음악 설정과 데이터 보존을 확인했다. 원본 trace는 비공개 `artifacts/evolution-20261012/a7/ui-device-atmosphere-final-audio/`에 보관한다. 새 밤 곡과 시간대 자동 전환의 실기기 검증은 아직 수행하지 않았다.
