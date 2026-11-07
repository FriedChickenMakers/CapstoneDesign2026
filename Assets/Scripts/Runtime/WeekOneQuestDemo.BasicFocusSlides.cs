using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        string basicSlideSession;
        int basicSlideStep = -2, basicSlideRevision, basicElapsed, basicHelpPage = -1, basicTranscriptPage = -1, basicReviewPage;
        bool basicOverview, basicResumeClock, basicAudioStarted;
        float basicAudioPosition;
        FocusAudioPlayer basicAudio;

        static bool UsesBasicFocusSlides(ActivitySession session)
        {
            var mission = FocusMissionCatalog.FindForSession(session);
            return session != null && session.FocusVersion >= 3 && mission != null
                && mission.Role == FocusMissionCatalog.BasicRole && int.TryParse(mission.Code.Substring(1), out int code)
                && code >= 8 && code <= 28;
        }

        void ResetBasicFocusSlides()
        {
            basicSlideSession = null; basicSlideStep = -2; basicSlideRevision++;
            basicElapsed = 0; basicHelpPage = basicTranscriptPage = -1; basicReviewPage = 0;
            basicOverview = basicResumeClock = basicAudioStarted = false;
            basicAudioPosition = 0; basicAudio = null;
        }

        void CaptureBasicFocusActivity()
        {
            if (focusInteractionSession != basicSlideSession || focusInteractionStep != basicSlideStep) return;
            if (focusPracticeClock != null)
            {
                basicElapsed = Math.Max(basicElapsed, focusPracticeClock.ElapsedSeconds);
                basicResumeClock = focusPracticeClock.Running;
            }
            if (basicAudio != null && basicAudio.Source != null)
            {
                basicAudioPosition = basicAudio.PlaybackPosition;
                basicAudioStarted = basicAudio.HasStarted;
            }
        }

        void BindBasicFocusSlides(ActivitySession session)
        {
            CaptureBasicFocusActivity();
            if (basicSlideSession == session.SessionId && basicSlideStep == session.InstructionStep) return;
            ResetBasicFocusSlides();
            basicSlideSession = session.SessionId; basicSlideStep = session.InstructionStep;
            basicOverview = session.Status == ParticipationState.Selected;
            if (basicSlideStep >= 0 && basicSlideStep < session.FocusStepRecords.Count)
                basicElapsed = session.FocusStepRecords[basicSlideStep].ElapsedSeconds;
        }

        bool SaveBasicFocusActivity(ActivitySession session)
        {
            if (basicSlideSession != session.SessionId || basicSlideStep != session.InstructionStep
                || session.Status == ParticipationState.Selected || basicSlideStep < 0
                || basicSlideStep >= session.FocusStepRecords.Count) return true;
            CaptureBasicFocusActivity();
            if (basicElapsed <= session.FocusStepRecords[basicSlideStep].ElapsedSeconds) return true;
            return FocusSaved(Service.SaveFocusElapsed(session.SessionId, basicSlideStep, basicElapsed));
        }

        void PauseBasicFocusActivity()
        {
            CaptureBasicFocusActivity();
            if (focusPracticeClock != null) focusPracticeClock.Pause();
            if (basicAudio != null) basicAudio.PausePlayback();
            basicResumeClock = false;
        }

        static string[] BasicTranscriptPages()
        {
            // Keep the complete existing story, with short reading slides instead of a text wall.
            var pages = new List<string>(); string page = "";
            foreach (string word in FocusMissionCatalog.StoryText.Split(' '))
            {
                if (page.Length > 0 && page.Length + word.Length + 1 > 48) { pages.Add(page); page = ""; }
                page += (page.Length > 0 ? " " : "") + word;
            }
            if (page.Length > 0) pages.Add(page);
            return pages.ToArray();
        }

        FocusStepRecord[] BasicReviewRecords(ActivitySession session, int step) => session.FocusStepRecords.Take(step)
            .Where(record => !record.Skipped && !string.IsNullOrEmpty(record.Value)).ToArray();

        string BasicFocusChoice(FocusMissionDefinition mission, ActivitySession session, int step)
        {
            if (mission.Code == "P18" && mission.Steps[step].Key == "practice" && !string.IsNullOrEmpty(focusAnswer)) return focusAnswer;
            string key = mission.Code == "P24" ? "route" : mission.Code == "P12" ? "experience" : "activity";
            return session.FocusStepRecords.FirstOrDefault(record => record.Key == key)?.Value ?? "";
        }

        void BuildBasicFocusSlides(RectTransform root, FocusMissionDefinition mission, ActivitySession session, int step)
        {
            int revision = ++basicSlideRevision; string id = session.SessionId;
            Func<bool> current = () => root != null && view == root.gameObject && root.gameObject.activeInHierarchy
                && isActiveAndEnabled && screen == "focus-practice" && sessionId == id && basicSlideRevision == revision;
            Action<Action> act = action => { if (current()) action(); };
            var ink = FocusInk(mission); var accent = FocusAccent(mission); var muted = FocusMuted(mission);
            var instruction = step < 0 ? null : mission.Steps[step];
            var help = instruction == null ? Array.Empty<string>() : FocusBasicSlideContent.Help(mission.Code, instruction.Key);
            bool readingHelp = basicHelpPage >= 0 && help.Length > 0, transcript = basicTranscriptPage >= 0;
            bool reviewing = instruction != null && instruction.Kind == "review" && !readingHelp && !transcript;
            var records = reviewing ? BasicReviewRecords(session, step) : Array.Empty<FocusStepRecord>();
            basicReviewPage = Math.Min(basicReviewPage, Math.Max(0, records.Length - 1));
            root.GetComponent<FocusMobilePageLayout>().FixedContent = true;
            focusDrawing = null; focusPracticeClock = null; basicAudio = null;
            focusInteractionSession = id; focusInteractionStep = step;
            FigmaMissionUi.Button(root, "나가기", 24, 24, 86, 48, () => act(CloseFocusPractice), Color.clear, ink, 15).name = "Focus.Close";
            string progress = step < 0 ? "" : "안내 " + mission.Steps.Take(step + 1).Select((item, index) => index)
                .Count(index => FocusMissionCatalog.IsApplicable(mission, session.FocusStepRecords, index)) + "/"
                + mission.Steps.Select((item, index) => index).Count(index => FocusMissionCatalog.IsApplicable(mission, session.FocusStepRecords, index));
            if (readingHelp) progress = "도움말 " + (basicHelpPage + 1) + "/" + help.Length;
            if (transcript) progress = "이야기 " + (basicTranscriptPage + 1) + "/" + BasicTranscriptPages().Length;
            if (reviewing) progress = "확인 " + (basicReviewPage + 1) + "/" + Math.Max(1, records.Length);
            FigmaMissionUi.Label(root, FocusPracticeContext(progress), 148, 36, 218, 24, 14, accent, false, TextAnchor.MiddleRight).name = "Basic.Progress";
            FigmaMissionUi.Label(root, mission.Code + " · " + (step < 0 ? "기본 미션" : mission.Title), 24, 86, 342, 24, 14, accent).name = "Basic.Mission";
            string title = step < 0 ? mission.Title : FocusBasicSlideContent.Title(mission.Code, instruction.Key, instruction.Title);
            string body = step < 0 ? mission.Overview : FocusBasicSlideContent.Body(mission.Code, instruction.Key, instruction.Body, BasicFocusChoice(mission, session, step));
            string bodyName = step < 0 ? "Basic.Overview" : "Basic.Body";
            if (readingHelp) { title = "잠깐 도움말"; body = help[Math.Min(basicHelpPage, help.Length - 1)]; bodyName = "Basic.Help"; }
            if (transcript) { title = "창가의 작은 화분"; body = BasicTranscriptPages()[basicTranscriptPage]; bodyName = "Basic.Transcript"; }
            if (reviewing) { title = records.Length > 0 ? records[basicReviewPage].Title : title; body = records.Length > 0 ? "내가 남긴 내용을 확인해요." : "고른 내용이 없어도 마칠 수 있어요."; }
            FigmaMissionUi.Label(root, title, 24, 122, 342, 42, title.Length > 14 ? 24 : 27, ink, true).name = "Basic.Title";
            var copy = FigmaMissionUi.Label(root, body, 24, 174, 342, 68, 18, muted); copy.name = bodyName; copy.lineSpacing = 1.15f;
            var icon = FigmaMissionUi.Button(root, "", 105, 315, 180, 180, () => act(() => AdvanceBasicFocusSlide(id, step)), Color.clear, accent);
            icon.name = "Focus.IconAction"; icon.transition = Selectable.Transition.None;
            icon.gameObject.AddComponent<FocusMissionTapFeedback>().Button = icon;
            FocusCircle(icon.transform, 0, 0, 180, FocusCircleColor(mission)); AllFocusIcon(icon.transform, mission, 28, 28, 124);
            var hint = FigmaMissionUi.Label(root, BasicFocusActionHint(session, instruction, step, records.Length), 24, 507, 342, 28, 14, accent, false, TextAnchor.MiddleCenter);
            hint.name = "Basic.ActionHint";
            if (instruction != null && !readingHelp && !transcript && instruction.RequiredAnswer) icon.interactable = !string.IsNullOrWhiteSpace(focusAnswer);
            var viewport = FigmaMissionUi.Box(root, "Basic.AnswersViewport", 24, 545, 342, 220, Color.clear);
            viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            var content = FigmaMissionUi.Rect(viewport, "Basic.Answers", 0, 0, 342, 1);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            float height = 1;
            if (step >= 0 && !readingHelp && !transcript)
            {
                if (reviewing) height = BuildBasicReview(content, session, step, records, act, ink, accent);
                else if (instruction.Kind == "listen") height = BuildBasicListening(content, root, mission, session, current, act);
                else
                {
                    if (instruction.Options.Length > 0 || instruction.OptionalInput)
                        height = BuildBasicAnswers(root, content, mission, session, step, copy, icon, hint, act);
                    if (instruction.RecommendedSeconds > 0)
                        height = BuildBasicClock(root, content, mission, session, step, height > 1 ? height + 12 : 0, current);
                    if (instruction.Kind == "wait")
                    {
                        height = BuildBasicWait(content, session, height > 1 ? height : 0, act, ink, accent);
                    }
                }
            }
            var navigation = FigmaMissionUi.Rect(root, "Basic.Navigation", 24, 780, 342, 48);
            if (step >= 0)
                FigmaMissionUi.Button(navigation, "이전", 0, 0, 88, 48, () => act(() => TryBasicFocusBack()), Color.clear, muted, 15).name = "Basic.Previous";
            if (step >= 0 && help.Length > 0 && !readingHelp && !transcript)
                FigmaMissionUi.Button(navigation, "도움말", 246, 0, 96, 48, () => act(() =>
                {
                    PauseBasicFocusActivity(); SaveFocusDraft(); basicHelpPage = 0; ShowFocusPractice();
                }), Color.clear, accent, 15).name = "Basic.HelpToggle";
            var layout = root.gameObject.AddComponent<FocusGuidedSlideLayout>();
            layout.Icon = (RectTransform)icon.transform; layout.ActionHint = hint.rectTransform;
            layout.Viewport = viewport; layout.Content = content; layout.ContentHeight = height;
            layout.Navigation = navigation; layout.Notice = notice; layout.Refresh();
        }

        string BasicFocusActionHint(ActivitySession session, FocusMissionStep instruction, int step, int reviewCount)
        {
            if (basicHelpPage >= 0) return "아이콘을 눌러 " + (basicHelpPage + 1 < FocusBasicSlideContent.Help(FocusMissionCatalog.FindForSession(session).Code, instruction.Key).Length ? "다음" : "돌아가기");
            if (basicTranscriptPage >= 0) return "아이콘을 눌러 " + (basicTranscriptPage + 1 < BasicTranscriptPages().Length ? "다음" : "듣기로 돌아가기");
            if (step < 0) return session.Status == ParticipationState.Selected ? "아이콘을 눌러 시작" : "아이콘을 눌러 이어하기";
            if (instruction.Kind == "review" && basicReviewPage + 1 < reviewCount) return "아이콘을 눌러 다음 항목";
            if (instruction.Kind == "wait") return "행동을 마쳤다면 아이콘을 눌러요";
            if ((instruction.Kind == "choice" || instruction.Kind == "input") && string.IsNullOrEmpty(focusAnswer))
                return "아이콘을 눌러 건너뛰기";
            string action = instruction.ActionLabel == "계속" ? "다음" : instruction.ActionLabel == "완료" ? "마치기" : instruction.ActionLabel;
            return "아이콘을 눌러 " + action;
        }

        float BuildBasicAnswers(RectTransform root, Transform parent, FocusMissionDefinition mission, ActivitySession session,
            int step, Text copy, Button icon, Text hint, Action<Action> act)
        {
            var instruction = mission.Steps[step]; var options = instruction.Options;
            var buttons = new Button[options.Length]; InputField input = null;
            Action refresh = () =>
            {
                for (int i = 0; i < buttons.Length; i++) StyleGuidedChoice(buttons[i], focusAnswer == options[i], FocusAccent(mission), FocusInk(mission));
                if (instruction.RequiredAnswer) icon.interactable = !string.IsNullOrWhiteSpace(focusAnswer);
                copy.text = FocusBasicSlideContent.Body(mission.Code, instruction.Key, instruction.Body, BasicFocusChoice(mission, ActiveSession(), step));
                hint.text = BasicFocusActionHint(ActiveSession(), instruction, step, 0);
            };
            bool wide = options.Any(option => option.Length > 11);
            int columns = wide ? 1 : 2; float width = wide ? 342 : 168;
            for (int i = 0; i < options.Length; i++)
            {
                string option = options[i];
                buttons[i] = FigmaMissionUi.Button(parent, option, (i % columns) * 174, (i / columns) * 54, width, 48, () => act(() =>
                {
                    focusAnswer = option; focusDirty = true;
                    if (input != null) input.SetTextWithoutNotify(focusAnswer);
                    refresh();
                    if (instruction.Kind == "choice") AdvanceBasicFocusSlide(session.SessionId, step);
                    else SaveFocusDraft();
                }), Color.white, FocusInk(mission), 15);
                buttons[i].name = "Focus.Option." + option;
            }
            float height = Mathf.CeilToInt(options.Length / (float)columns) * 54;
            if (instruction.OptionalInput)
            {
                input = FigmaMissionUi.Input(parent, instruction.InputHint ?? "짧게 남기기 · 선택", 0, height, 342, 88, 500);
                input.name = "Focus.AnswerInput"; input.SetTextWithoutNotify(focusAnswer); focusAnswerInput = input;
                input.onValueChanged.AddListener(value => act(() => { focusAnswer = value; focusDirty = true; refresh(); }));
                input.onEndEdit.AddListener(value => act(() => SaveFocusDraft()));
                AttachFocusKeyboard(root, input, instruction.Title, FigmaMissionUi.Hex(mission.BackgroundHex), FocusAccent(mission));
                height += 100;
            }
            refresh(); return height;
        }

        float BuildBasicClock(RectTransform root, Transform parent, FocusMissionDefinition mission, ActivitySession session,
            int step, float top, Func<bool> current)
        {
            var label = FigmaMissionUi.Label(parent, "", 0, top, 342, 28, 14, FocusMuted(mission), false, TextAnchor.MiddleCenter);
            label.name = "Focus.TimerStatus";
            var toggle = FigmaMissionUi.Button(parent, "집중 시작", 58, top + 36, 226, 48, () => { }, Color.white, FocusAccent(mission), 15);
            toggle.name = "Focus.TimerToggle";
            focusPracticeClock = root.gameObject.AddComponent<FocusPracticeClock>();
            focusPracticeClock.Label = label; focusPracticeClock.Toggle = toggle;
            focusPracticeClock.RecommendedSeconds = mission.Steps[step].RecommendedSeconds;
            focusPracticeClock.ElapsedSeconds = Math.Max(basicElapsed, session.FocusStepRecords[step].ElapsedSeconds);
            focusPracticeClock.Persist = elapsed =>
            {
                if (!current() || ActiveSession()?.InstructionStep != step) return true;
                basicElapsed = Math.Max(basicElapsed, elapsed);
                return SaveBasicFocusActivity(ActiveSession());
            };
            focusPracticeClock.Initialize();
            if (basicResumeClock) { basicResumeClock = false; focusPracticeClock.ToggleRunning(); }
            return top + 84;
        }

        float BuildBasicReview(Transform parent, ActivitySession session, int step, FocusStepRecord[] records,
            Action<Action> act, Color ink, Color accent)
        {
            if (records.Length == 0) return 1;
            var record = records[basicReviewPage];
            var text = FigmaMissionUi.Label(parent, record.Value, 12, 0, 318, 48, 18, ink, true);
            text.name = "Basic.ReviewValue"; float height = Math.Max(48, text.preferredHeight + 8);
            text.rectTransform.sizeDelta = new Vector2(318, height);
            FigmaMissionUi.Button(parent, "수정", 114, height + 12, 114, 48, () => act(() =>
            {
                if (!SaveFocusDraft()) return;
                int target = session.FocusStepRecords.FindIndex(item => item.Key == record.Key);
                if (FocusSaved(Service.MoveFocusStep(session.SessionId, target))) ShowFocusPractice();
            }), Color.white, accent, 15).name = "Basic.ReviewEdit";
            return height + 60;
        }

        float BuildBasicWait(Transform parent, ActivitySession session, float top, Action<Action> act, Color ink, Color accent)
        {
            string trigger = session.FocusStepRecords.FirstOrDefault(record => record.Key == "trigger")?.Value;
            string activity = session.FocusStepRecords.FirstOrDefault(record => record.Key == "activity")?.Value;
            string plan = (string.IsNullOrEmpty(trigger) ? "정한 생활 행동 뒤" : trigger) + " · " + (string.IsNullOrEmpty(activity) ? "편한 실습" : activity);
            var label = FigmaMissionUi.Label(parent, plan, 8, top, 326, 48, 16, ink, false, TextAnchor.MiddleCenter);
            label.name = "Basic.WaitPlan"; float height = Math.Max(48, label.preferredHeight + 8);
            label.rectTransform.sizeDelta = new Vector2(326, height);
            FigmaMissionUi.Button(parent, "나중에 이어하기", 58, top + height + 8, 226, 48,
                () => act(CloseFocusPractice), Color.white, accent, 15).name = "Focus.ContinueLater";
            return top + height + 56;
        }

        float BuildBasicListening(Transform parent, RectTransform root, FocusMissionDefinition mission, ActivitySession session,
            Func<bool> current, Action<Action> act)
        {
            string route = session.FocusStepRecords.FirstOrDefault(record => record.Key == "route")?.Value;
            if (route == "상대의 이야기") return 1;
            var player = parent.gameObject.AddComponent<FocusAudioPlayer>(); basicAudio = player;
            player.Source = parent.gameObject.AddComponent<AudioSource>(); player.Source.playOnAwake = false;
            player.Source.clip = Resources.Load<AudioClip>("FocusMissions/Audio/P24-story");
            player.Status = FigmaMissionUi.Label(parent, "", 0, 0, 342, 44, 14, FocusMuted(mission), false, TextAnchor.MiddleCenter);
            player.Status.name = "Basic.AudioStatus";
            var track = FigmaMissionUi.Box(parent, "음성 재생 구간", 37, 50, 268, 4, FocusCircleColor(mission));
            player.Progress = FigmaMissionUi.Box(parent, "음성 재생 위치", 37, 50, 0, 4, FocusAccent(mission)).GetComponent<Image>();
            player.Duration = FigmaMissionUi.Label(parent, "", 0, 60, 342, 24, 14, FocusMuted(mission), false, TextAnchor.MiddleCenter);
            player.Toggle = FigmaMissionUi.Button(parent, "이야기 듣기", 0, 94, 166, 48, () => { }, FocusAccent(mission), Color.white, 15);
            player.Toggle.name = "Focus.AudioToggle";
            player.Replay = FigmaMissionUi.Button(parent, "다시 듣기", 176, 94, 166, 48, () => { }, Color.white, FocusAccent(mission), 15);
            player.Replay.name = "Focus.AudioReplay";
            FigmaMissionUi.Button(parent, "이야기 글 보기", 0, 152, 342, 48, () => act(() =>
            {
                PauseBasicFocusActivity(); SaveFocusDraft(); basicTranscriptPage = 0; ShowFocusPractice();
            }), Color.clear, FocusAccent(mission), 15).name = "Focus.AudioTranscript";
            player.Initialize();
            if (basicAudioStarted) player.RestorePaused(basicAudioPosition);
            return 200;
        }

        void AdvanceBasicFocusSlide(string id, int step)
        {
            var session = ActiveSession(); var mission = FocusMissionCatalog.FindForSession(session);
            if (session == null || session.SessionId != id || mission == null) return;
            if (basicHelpPage >= 0)
            {
                var pages = FocusBasicSlideContent.Help(mission.Code, mission.Steps[step].Key);
                if (++basicHelpPage >= pages.Length) basicHelpPage = -1;
                ShowFocusPractice(); return;
            }
            if (basicTranscriptPage >= 0)
            {
                if (++basicTranscriptPage >= BasicTranscriptPages().Length) basicTranscriptPage = -1;
                ShowFocusPractice(); return;
            }
            if (step >= 0 && mission.Steps[step].Kind == "review" && basicReviewPage + 1 < BasicReviewRecords(session, step).Length)
            { basicReviewPage++; ShowFocusPractice(); return; }
            if (step < 0 && session.Status != ParticipationState.Selected)
            { basicOverview = false; ShowFocusPractice(); return; }
            if (step < 0) basicOverview = false;
            // Completion, branch skipping and receipts remain owned by the existing service.
            PauseBasicFocusActivity();
            AdvanceFocus(id, step);
        }

        bool TryBasicFocusBack()
        {
            PauseBasicFocusActivity();
            if (basicHelpPage >= 0 || basicTranscriptPage >= 0 || basicReviewPage > 0)
            {
                SaveFocusDraft();
                if (basicHelpPage >= 0) basicHelpPage--;
                else if (basicTranscriptPage >= 0) basicTranscriptPage--;
                else basicReviewPage--;
                ShowFocusPractice(); return true;
            }
            if (!SaveFocusDraft()) return true;
            var session = ActiveSession();
            if (basicOverview) { CloseFocusPractice(); return true; }
            if (session.InstructionStep == 0) { basicOverview = true; ShowFocusPractice(); return true; }
            var mission = FocusMissionCatalog.FindForSession(session); int previous = session.InstructionStep - 1;
            while (previous > 0 && !FocusMissionCatalog.IsApplicable(mission, session.FocusStepRecords, previous)) previous--;
            if (FocusSaved(Service.MoveFocusStep(session.SessionId, previous))) ShowFocusPractice();
            return true;
        }
    }
}
