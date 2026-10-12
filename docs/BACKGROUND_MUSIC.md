# 배경 음악

사용자가 프로젝트 BGM으로 제공한 곡을 `Assets/Resources/Audio/GardenTheme.ogg`로 변환했다. 원본의 저작권이나 라이선스를 변경하지 않으며 외부 무료 음원으로 표시하지 않는다. 원본 WAV와 검토용 미리보기는 저장소에 포함하지 않는다.

- 원본: 147.428583초, 48 kHz, 스테레오, 24-bit PCM WAV, 약 42.46 MB.
- 런타임 자산: 같은 길이·샘플레이트·채널, Ogg Vorbis quality 6, 약 3.23 MB. 손실 압축이며 무손실 변환으로 표현하지 않는다.
- 제목·작성자·인코더 등 선택적 태그를 제거했다. Ogg/Vorbis의 필수 코덱 헤더는 유지한다.
- 처음 80 ms와 끝 2초에 페이드를 적용했다. 원곡은 루프용으로 제공되지 않았으므로 곡 끝에 음량이 내려갔다가 다시 시작한다. 박자나 화성이 끊김 없이 연결되는 seamless loop가 아니다.
- 원본 수치 검사: 평균 -18.8 dBFS, 최대 -2.3 dBFS. 플레이어 기본 음량은 0.25다. 최종 체감 음량은 기기 미디어 음량과 스피커에 따라 달라진다.

## 트랙 지정과 앱 연결

`Assets/Resources/Audio/GardenMusicProfile.asset`에서 `homeGarden`, `activity`, `settings`에 각 화면의 AudioClip을 지정한다. 현재는 제공된 한 곡을 세 항목에 동일하게 지정했다. 활동 또는 설정의 참조를 비우면 홈/정원 곡을 사용한다. 프로필에서 기본 음량과 곡 전환 페이드 시간(현재 0.8초)을 조절할 수 있다.

```csharp
GardenMusicPlayer.Ensure().SetContext(GardenMusicContext.HomeGarden);
GardenMusicPlayer.Ensure().SetContext(GardenMusicContext.Activity);
GardenMusicPlayer.Ensure().SetContext(GardenMusicContext.Settings);
GardenMusicPlayer.SetMuted(true);
bool muted = GardenMusicPlayer.IsMuted;
```

플레이어는 탭이나 카메라와 독립된 영속 오브젝트다. 홈과 정원은 같은 컨텍스트를 사용한다. 같은 AudioClip으로 이동할 때 `Play`를 다시 호출하지 않아 재생 위치가 유지된다. 다른 곡으로 바꾸면 두 AudioSource의 음량을 교차 전환한다. 빠르게 이전 곡으로 돌아올 때 아직 재생 중인 채널이 있으면 그 위치를 이어 쓴다. 최대 두 채널만 사용한다.

음소거는 앱의 `capstone.music.muted` 설정에 저장한다. 음소거·앱 일시정지 중에는 음악 채널을 `Pause`하고 페이드 진행도 멈춘다. 데스크탑에서는 포커스 상실도 일시정지 이유다. Android에서는 소프트 키보드를 열어도 포커스 상실 이벤트가 발생하므로 `OnApplicationPause`를 백그라운드 전환 기준으로 사용한다. 키보드만 열고 활동을 입력할 때는 음악이 이어진다. 해당 일시정지 사유가 모두 해제되고 음소거가 꺼져야 `UnPause`하며 곡을 처음부터 재생하지 않는다. 기기 전체 음량이나 다른 앱 음악을 설정으로 변경하지 않는다. 근거: [Unity 포커스와 Android 키보드](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnApplicationFocus.html).

전용 AudioListener를 플레이어에 두어 홈 카메라가 비활성화되어도 활동과 설정에서 들을 수 있게 한다. 탭/장면 진입 때 다른 활성 AudioListener를 끄고, 플레이어가 제거되면 원래 활성화되어 있던 Listener를 복구한다. 음악은 2D이며 센서 수집 서비스나 Android 백그라운드 재생 서비스를 실행하지 않는다.

## 가져오기와 검증 범위

Unity importer는 스테레오 유지, Streaming, 백그라운드 로딩, preload 해제로 지정했다. 장시간 음악의 전체 PCM을 메모리에 상주시킬 필요가 없다. 근거: [Unity Audio Clip 설정](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioClip.html), [Pause](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.Pause.html), [UnPause](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.UnPause.html).

변환 결과는 ffprobe로 길이·채널·샘플레이트·태그를 검사하고 ffmpeg 전체 디코딩을 통과했다. 시작과 끝을 담은 짧은 청취용 미리보기를 비공개 작업 경로에 만들었다. 이 실행 환경은 오디오 입력의 청각적 평가를 지원하지 않아, 음악적 연결감과 실제 스피커 음량을 청취로 확인했다고 주장하지 않는다. Unity 재생 위치·전환·포커스 동작과 실제 기기 재생은 별도의 런타임 검증으로 확인한다.

`MusicPlayValidation.Start`의 Unity Play 검사 23개가 통과했다. 실제 제공곡의 `timeSamples` 증가와 비영 출력(PCM peak 0.07917947), 같은 곡의 탭 전환 연속성, 단일 Listener, 포커스·pause 순서, 음소거·복귀, Android IME 정책의 합성 이벤트, 빠른 다른 곡 교차 전환과 이전 곡 위치 재사용을 확인했다. 음소거는 상태를 즉시 검사한 뒤 DSP 한 버퍼 전달 이후 위치가 멈춰 있는지 검사한다. 환경은 FMOD nosound 출력이므로 실제 스피커 청취 및 기기의 실제 IME 이벤트 전달과 구분한다. 테스트는 격리된 MOCK 저장소를 사용하고 기존 음악 설정을 복구한다.
