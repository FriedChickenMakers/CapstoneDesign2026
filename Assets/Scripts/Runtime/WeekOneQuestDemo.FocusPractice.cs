using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        const string FocusArt = "FocusMissions/Practice/";
        static readonly string[] FocusEmotions = {
            "기쁨", "만족감", "설렘", "기대감", "편안함", "안도감", "고마움", "뿌듯함",
            "궁금함", "반가움", "놀람", "무덤덤함", "슬픔", "외로움", "서운함", "아쉬움",
            "불안함", "두려움", "답답함", "막막함", "화남", "짜증", "부끄러움", "미안함"
        };
        string focusDraftSession, focusAnswer = "", focusMood = "", focusNote = "";
        int focusDraftStep = -1;
        bool focusDirty, focusAllEmotions;
        InputField focusAnswerInput, focusNoteInput;
        float focusNextAction;
        static Sprite focusCircleSprite;

        static Color FocusAlpha(Color color, float alpha) { color.a = alpha; return color; }
        static Color FocusAccent(FocusMissionDefinition mission) => FigmaMissionUi.ActionColor(FigmaMissionUi.Hex(mission.AccentHex));
        static Color FocusInk(FocusMissionDefinition mission) => FigmaMissionUi.Hex(mission.TextHex);
        static Color FocusMuted(FocusMissionDefinition mission) => FigmaMissionUi.Hex(mission.Code == "P10" ? "587572" : mission.Code == "M03" ? "466A7A" : "466A75");
        static Color FocusCircleColor(FocusMissionDefinition mission) => FigmaMissionUi.Hex(mission.CircleHex ?? (mission.Code == "M03" ? "D9F1FA" : mission.Code == "P10" ? "DDF2EE" : mission.Code == "P23" ? "F4E2CB" : "EDDCE2"));

        void BindFocusDraft(ActivitySession session, int step)
        {
            if (focusDraftSession != session.SessionId)
            {
                focusDraftSession = session.SessionId; focusMood = session.Mood ?? ""; focusNote = session.Note ?? "";
                focusDraftStep = -1; focusAnswer = ""; focusDirty = false; focusAllEmotions = false;
            }
            if (focusDraftStep == step) return;
            focusDraftStep = step;
            focusAnswer = step >= 0 && step < session.FocusStepRecords.Count ? session.FocusStepRecords[step].Value ?? "" : "";
            focusDirty = false;
        }

        bool FocusSaved(bool saved)
        {
            error = saved ? null : Service.LastError;
            if (notice != null)
            {
                notice.gameObject.SetActive(!saved);
                notice.text = saved ? "" : "저장하지 못했어요. 입력을 유지했으니 다시 시도해 주세요.";
                notice.transform.SetAsLastSibling();
            }
            if(saved)ObserveOptionalRecommendation(force:true);
            return saved;
        }

        public bool SaveFocusDraft()
        {
            if (opened && screen == "focus-practice" && FocusMissionCatalog.IsGuided(ActiveSession())
                && ActiveSession().InstructionStep == 0) return SaveGuidedFocusDraft();
            if (!SaveFocusActivity()) return false;
            if (!opened || !screen.StartsWith("focus-", StringComparison.Ordinal) || !focusDirty) return true;
            var session = ActiveSession();
            if (!FocusMissionPolicy.IsFocus(session) || session.Status == ParticipationState.Selected || session.Status == ParticipationState.RewardCommitted) return true;
            if (focusAnswerInput != null && screen == "focus-practice") focusAnswer = focusAnswerInput.text;
            if (focusNoteInput != null && (screen == "focus-note" || screen == "focus-save-error")) focusNote = focusNoteInput.text;
            bool result;
            if (screen == "focus-practice" && focusDraftStep >= 0 && focusDraftStep < session.FocusStepRecords.Count)
                result = Service.SaveFocusDraft(session.SessionId, focusDraftStep, focusAnswer, string.IsNullOrEmpty(focusAnswer));
            else if (screen == "focus-emotion" || screen == "focus-note" || screen == "focus-save-error")
                result = Service.SaveFocusReflection(session.SessionId, focusMood, focusNote, screen == "focus-emotion" ? 0 : 1);
            else return true;
            if (FocusSaved(result)) { focusDirty = false; return true; }
            return false;
        }

        void CloseFocusPractice()
        {
            if (!SaveFocusDraft()) return;
            focusAnswerInput = null; focusNoteInput = null; ShowHome();
        }

        public bool TryFocusBack()
        {
            if(focusAdjustmentCancel!=null){focusAdjustmentCancel();return true;}
            if(screen=="focus-photo"){var photoSession=ActiveSession();if(photoSession!=null)SkipMissionPhoto(photoSession.SessionId);return true;}
            if (screen == "focus-story") { ShowFocusPractice(); return true; }
            if (screen != "focus-practice" && screen != "focus-emotion" && screen != "focus-note" && screen != "focus-save-error" && screen != "focus-complete") return false;
            if (screen == "focus-complete") { ShowHome(); return true; }
            if (screen == "focus-save-error") { ShowFocusNote(); return true; }
            var session = ActiveSession();
            if (screen == "focus-practice" && UsesM01LightGuide(session)) return TryM01LightBack();
            if (screen == "focus-practice" && FocusMissionCatalog.IsGuided(session) && session.InstructionStep == 0)
                return TryGuidedPrevious();
            if (screen == "focus-practice" && UsesBasicFocusSlides(session)) return TryBasicFocusBack();
            if (!SaveFocusDraft()) return true;
            if (!FocusMissionPolicy.IsFocus(session)) return false;
            if (screen == "focus-note")
            {
                if (FocusSaved(Service.SaveFocusReflection(session.SessionId, focusMood, focusNote, 0))) ShowFocusEmotion();
            }
            else if (screen == "focus-emotion")
            {
                int previous = FocusMissionCatalog.IsGuided(session) ? 0 : session.FocusStepRecords.Count - 1;
                if (FocusSaved(Service.MoveFocusStep(session.SessionId, previous))) ShowFocusPractice();
            }
            else if (screen == "focus-practice" && session.Status != ParticipationState.Selected && session.InstructionStep > 0)
            {
                var definition = FocusMissionCatalog.FindForSession(session);
                int previous = session.InstructionStep - 1;
                while (previous > 0 && !FocusMissionCatalog.IsApplicable(definition, session.FocusStepRecords, previous)) previous--;
                if (FocusSaved(Service.MoveFocusStep(session.SessionId, previous))) ShowFocusPractice();
            }
            else CloseFocusPractice();
            return true;
        }

        public void ShowFocusPractice()
        {
            if (!opened) { ShowHome(); return; }
            var session = ActiveSession();
            var mission = FocusMissionCatalog.FindForSession(session);
            if (!FocusMissionPolicy.IsFocus(session) || mission == null) { ShowHome(); return; }
            if(session.Status==ParticipationState.Ended) { ShowFocusHome();return; }
            if (session.Status == ParticipationState.RewardCommitted) { ShowFocusCompletion(session.SessionId); return; }
            if (!RequireMissionPlant()) return;
            if (session.Status == ParticipationState.Paused)
            {
                if (!FocusSaved(Service.ResumeSession(session.SessionId))) return;
                session = ActiveSession();
            }
            if (UsesBasicFocusSlides(session)) BindBasicFocusSlides(session);
            bool overview = session.Status == ParticipationState.Selected || UsesBasicFocusSlides(session) && basicOverview;
            if (!overview && session.InstructionStep >= mission.Steps.Length)
            {
                FinishFocusMission();
                return;
            }
            int step = overview ? -1 : session.InstructionStep;
            if (FocusMissionCatalog.IsGuided(session) && (overview || step == 0))
            {
                if (guidedDraftSession == session.SessionId) GuidedElapsed();
                focusAnswerInput = null; focusNoteInput = null; focusDirty = false;
                var guidedRoot = BeginFigmaPage("focus-practice", ShowFocusPractice, FigmaMissionUi.Hex(mission.BackgroundHex));
                BuildGuidedFocusPractice(guidedRoot, mission, session);
                FocusEnter(guidedRoot);
                return;
            }
            BindFocusDraft(session, step);
            focusAnswerInput = null; focusNoteInput = null;focusPlanPersist=null;
            var root = BeginFigmaPage("focus-practice", ShowFocusPractice, FigmaMissionUi.Hex(mission.BackgroundHex));
            if (UsesM01LightGuide(session))
            {
                BuildM01LightGuide(root, mission, session);
                FocusEnter(root);
                return;
            }
            if (UsesBasicFocusSlides(session))
            {
                BuildBasicFocusSlides(root, mission, session, step);
                FocusEnter(root);
                return;
            }
            string frame = FocusMissionCatalog.HasOriginalLayout(mission.Code) ? FocusFrame(mission.Code, step) : "86_770";
            FocusPracticeHeader(root, mission, step, frame);
            string id = session.SessionId;
            Action next = () => AdvanceFocus(id, step);
            if (mission.Code == "M03") BuildFocusWater(root, mission, step, next);
            else if (mission.Code == "P10") BuildFocusBreath(root, mission, step, next);
            else if (mission.Code == "P23") BuildFocusEating(root, mission, step, next);
            else if (mission.Code == "P25" && session.FocusVersion<3) BuildFocusSpeaking(root, mission, step, next);
            else BuildAllFocusPractice(root, mission, step, next);
            AttachAllFocusClock(root, mission, step);
            if (step >= 0) FigmaMissionUi.Button(root, "이전 단계", 24, 776, 164, 48, () => TryFocusBack(), Color.clear, FocusMuted(mission), 14).name = "Focus.Back";
            FigmaMissionUi.Button(root,"이번 미션 넘기기",202,776,164,48,()=>ShowFocusEndPrompt(root,mission,id),Color.clear,FocusMuted(mission),14).name="Focus.EndParticipation";
            // Untimed actions already have their completion control. Keep the
            // optional step-start marker unset unless a real input/audio/timer
            // interaction records it; rendering is not evidence of practice.
            FocusEnter(root);
        }

        void AdvanceFocus(string id, int step)
        {
            if ((Application.isPlaying && Time.unscaledTime < focusNextAction) || sessionId != id || screen != "focus-practice") return;
            var session = ActiveSession();
            if (session == null || (step < 0 ? session.Status != ParticipationState.Selected : session.InstructionStep != step)) return;
            if (!SaveFocusActivity()) return;
            focusNextAction = Time.unscaledTime + .2f;
            if (focusAnswerInput != null) focusAnswer = focusAnswerInput.text;
            if(step>=0 && FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(FocusMissionCatalog.FindForSession(session),session.FocusStepRecords[step].Key,focusAnswer)!=null)
            {
                string alternativeId=Guid.NewGuid().ToString("N");
                if(FocusSaved(Service.BeginFocusAlternative(id,step,focusAnswer,alternativeId))) {sessionId=alternativeId;focusDirty=false;ShowFocusPractice();}
                return;
            }
            bool walkingStart = step < 0 && session.FocusVersion >= 2 && session.MissionId == "free-mission:M15";
            bool success = walkingStart
                ? Service.StartWalkingSession(id, WalkingPersonalization.Evaluate(ReadWalkingHealth()?.healthConnect?.personalization, Service.CurrentUtc, Service.CurrentTimeZone))
                : step < 0 ? Service.StartSession(id) : Service.ConfirmFocusStep(id, step, focusAnswer, string.IsNullOrEmpty(focusAnswer));
            if (!FocusSaved(success)) return;
            if (walkingStart) RequestStepSyncSoon();
            focusDirty = false; ShowFocusPractice();
        }

        static string FocusFrame(string code, int step)
        {
            string[] frames = code == "M03" ? new[] { "86_770", "87_990", "87_770" }
                : code == "P10" ? new[] { "87_1048", "87_1182", "87_840", "87_1222", "87_1371" }
                : code == "P23" ? new[] { "87_1103", "87_1254", "87_1284", "87_891", "87_1407" }
                : new[] { "87_1156", "87_942", "87_1314", "87_1343", "87_1446" };
            return frames[step + 1];
        }

        void FocusPracticeHeader(Transform root, FocusMissionDefinition mission, int step, string frame)
        {
            var ink = FocusInk(mission); var accent = FocusAccent(mission); bool compact = mission.Code == "P23" || mission.Code == "P25";
            var close = FigmaMissionUi.Button(root, "", 24, 24, 88, 48, CloseFocusPractice, Color.clear, ink);
            close.name = "Focus.Close"; FigmaMissionUi.Asset(close.transform, FocusArt + frame + "_imgIconClose", 10, 14, 20, 20);
            FigmaMissionUi.Label(close.transform, "닫기", 36, 0, 42, 48, 14, ink, false, TextAnchor.MiddleLeft);
            FigmaMissionUi.Label(root, FocusPracticeContext(step < 0 ? "시작하기" : "지금 할 일"), 172, 36, 194, 24, 14, accent, false, TextAnchor.MiddleRight);
            FigmaMissionUi.Label(root, mission.Title, 24, 81, 342, 20, 14, accent, false, TextAnchor.UpperCenter);
            // Texture-v3 P25 has longer reviewed purpose/choice copy and uses
            // this legacy page instead of the current v4/v5 basic slides.
            bool textureSpeaking = mission.Code == "P25" && ActiveSession()?.FocusVersion == FocusMissionCatalog.TextureVersion;
            bool original = FocusMissionCatalog.HasOriginalLayout(mission.Code) && !textureSpeaking;
            string heading = step < 0 ? mission.Title : mission.Steps[step].Title;
            int headingSize = original ? (compact ? 26 : 32) : heading.Length > 13 ? 22 : 26;
            FigmaMissionUi.Label(root, heading, 24, 103, 342, original ? (compact ? 34 : 40) : 48, headingSize, ink, true, TextAnchor.UpperCenter).name = "Focus.PracticeHeading";
            FigmaMissionUi.Label(root, step < 0 ? mission.Overview : mission.Steps[step].Body, 24, original ? (compact ? 140 : 147) : 153, 342, original ? 48 : textureSpeaking ? 84 : 60, 18, FocusMuted(mission), false, TextAnchor.UpperCenter).name = "Focus.PracticeBody";
        }

        static void FocusCircle(Transform root, float x, float y, float size, Color color, Color? border = null, float borderWidth = 1.5f)
        {
            var rect = FigmaMissionUi.Rect(root, "중앙 원", x, y, size, size);
            var graphic = rect.gameObject.AddComponent<Image>(); graphic.sprite = FocusCircleSprite(); graphic.color = border ?? color; graphic.raycastTarget = false;
            if (border.HasValue)
            {
                var inner = FigmaMissionUi.Rect(rect, "원 내부", borderWidth, borderWidth, size - 2 * borderWidth, size - 2 * borderWidth);
                var fill = inner.gameObject.AddComponent<Image>(); fill.sprite = FocusCircleSprite(); fill.color = color; fill.raycastTarget = false;
            }
        }

        static Sprite FocusCircleSprite()
        {
            if (focusCircleSprite != null) return focusCircleSprite;
            // A geometric layout primitive, separate from the original exported illustrations.
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Focus circle", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = x + .5f - size / 2f, dy = y + .5f - size / 2f;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(size / 2f - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            focusCircleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
            focusCircleSprite.name = "Focus circle"; focusCircleSprite.hideFlags = HideFlags.HideAndDontSave;
            return focusCircleSprite;
        }

        static void FocusArtAt(Transform root, string asset, float x, float y, float w, float h) => FigmaMissionUi.Asset(root, FocusArt + asset, x, y, w, h);

        static Button FocusSlideIcon(Transform root, FocusMissionDefinition mission, Action action, out RectTransform illustration)
        {
            var button = FigmaMissionUi.Button(root, "", 83, 293, 224, 224, action, Color.clear, FocusAccent(mission));
            button.name = "Focus.IconAction"; button.transition = Selectable.Transition.None;
            button.gameObject.AddComponent<FocusMissionTapFeedback>().Button = button;
            illustration = FigmaMissionUi.Rect(button.transform, "Focus.Illustration", 0, 0, 180, 180);
            illustration.anchorMin = illustration.anchorMax = illustration.pivot = new Vector2(.5f, .5f);
            illustration.anchoredPosition = Vector2.zero;
            FocusCircle(illustration, 0, 0, 180, FocusCircleColor(mission));
            AllFocusIcon(illustration, mission, 28, 28, 124);
            return button;
        }

        static Button FocusTap(Transform root, string label, Action action, Color color, float y = 319, float h = 292)
        {
            var button = FigmaMissionUi.Button(root, "", 45, y, 300, h, action, Color.clear, color);
            button.name = "Focus.IconAction";
            button.transition = Selectable.Transition.None;
            var feedback = button.gameObject.AddComponent<FocusMissionTapFeedback>(); feedback.Button = button;
            return button;
        }

        static void FocusCaption(Transform root, string text, Color color, float y = 571.2f)
        {
            FigmaMissionUi.Box(root, text, 45, y, 300, 48, FocusAlpha(Color.white, .72f), 18);
            FigmaMissionUi.Label(root, text, 53, y, 284, 48, 16, color, false, TextAnchor.MiddleCenter);
        }

        static void FocusActionText(Transform root, string label, string hint, FocusMissionDefinition mission, Action action, float actionY = 650)
        {
            FigmaMissionUi.Button(root, label, 45, actionY, 300, 56, action, FocusAccent(mission), Color.white, 18).name = "Focus.Action";
            FigmaMissionUi.Label(root, hint, 45, actionY + 64, 300, 24, 14, FocusMuted(mission), false, TextAnchor.MiddleCenter);
        }

        void BuildFocusWater(Transform root, FocusMissionDefinition mission, int step, Action next)
        {
            var tap = FocusTap(root, step < 0 ? "시작" : mission.Steps[step].ActionLabel, next, FocusAccent(mission));
            FigmaMissionUi.SetRect((RectTransform)tap.transform, 45, 310, 300, 300);
            // The full illustration and its label form a single large hit target.
            if (step < 1)
            {
                FocusCircle(root, 71, 326, 248, FocusCircleColor(mission), FigmaMissionUi.Hex("B2E0F0"));
                if (step < 0) FocusArtAt(root, "86_770_imgIconWaterDropFriendly", 110.76f, 351.72f, 168.48f, 196.56f);
                else FocusArtAt(root, "87_990_imgIconM03WaterCup", 111, 366, 168, 168);
                FocusActionText(root, step < 0 ? "시작" : "준비했어요", step < 0 ? "물방울을 눌러도 시작해요" : "준비한 뒤 가운데 그림을 눌러도 좋아요", mission, next, 670);
            }
            else
            {
                FocusArtAt(root, "87_770_img280280", 45, 310, 300, 300);
                FocusActionText(root, "물 마셨어요", "마신 뒤 물방울을 눌러도 좋아요", mission, next, 670);
            }
            tap.transform.SetAsLastSibling();
        }

        void BuildFocusBreath(Transform root, FocusMissionDefinition mission, int step, Action next)
        {
            if (step >= 0 && mission.Steps[step].RecommendedSeconds > 0)
            {
                var accentColor = FocusAccent(mission);
                var illustration = FigmaMissionUi.Rect(root, "호흡에 머무르기", 69, 300, 252, 252);
                FocusCircle(illustration, 0, 0, 252, FocusCircleColor(mission));
                if (step == 1)
                {
                    FocusArtAt(illustration, "87_840_img182", 35, 35, 182, 182);
                    FocusArtAt(illustration, "87_840_img128", 62, 62, 128, 128);
                    FigmaMissionUi.Label(illustration, "숨", 86, 101, 80, 48, 30, accentColor, true, TextAnchor.MiddleCenter);
                    illustration.gameObject.AddComponent<FocusBreathingPulse>();
                }
                else
                {
                    FocusArtAt(illustration, "87_1222_img218", 17, 17, 218, 218);
                    FocusArtAt(illustration, "87_1222_img164", 44, 44, 164, 164);
                    FigmaMissionUi.Label(illustration, "주변", 86, 106, 80, 40, 23, accentColor, true, TextAnchor.MiddleCenter);
                }
                FigmaMissionUi.Label(root, "충분히 느꼈다면 언제든 마쳐도 좋아요.", 45, 570, 300, 30, 14, FocusMuted(mission), false, TextAnchor.MiddleCenter);
                FigmaMissionUi.Button(root, "실습 마치기", 45, 714, 300, 48, next, Color.clear, accentColor, 16).name = "Focus.Action";
                return;
            }
            var accent = FocusAccent(mission); var tap = FocusTap(root, step < 0 ? "시작" : mission.Steps[step].ActionLabel, next, accent);
            if (step < 1)
            {
                FocusCircle(root, 85.8f, 340.8f, 218.4f, FocusCircleColor(mission));
                if (step < 0) FocusArtAt(root, "87_1048_imgGroup", 145.08f, 400.08f, 120.12f, 134.74f);
                else for (int i = 0; i < 3; i++)
                {
                    FocusArtAt(root, "87_1182_img", 108 + 70 * i, 421, 34, 34);
                    FigmaMissionUi.Label(root, new[] { "생각", "감정", "몸" }[i], 93 + 70 * i, 463, 64, 22, 13, accent, false, TextAnchor.MiddleCenter);
                }
                FocusCaption(root, step < 0 ? "잠시 멈추고 나를 살펴요" : "있는 그대로 살펴요", accent);
                FocusActionText(root, step < 0 ? "시작" : "계속", step < 0 ? "아이콘을 눌러도 시작해요" : "살펴본 뒤 아이콘을 눌러도 좋아요", mission, next);
            }
            else if (step < 3)
            {
                FocusCircle(root, 69, 354, 252, FocusCircleColor(mission));
                if (step == 1)
                {
                    FocusArtAt(root, "87_840_img182", 104, 389, 182, 182); FocusArtAt(root, "87_840_img128", 131, 416, 128, 128);
                    FigmaMissionUi.Label(root, "숨", 155, 457, 80, 48, 30, accent, true, TextAnchor.MiddleCenter);
                }
                else
                {
                    FocusArtAt(root, "87_1222_img218", 86, 371, 218, 218); FocusArtAt(root, "87_1222_img164", 113, 398, 164, 164); FocusArtAt(root, "87_1222_img110", 140, 425, 110, 110);
                    FigmaMissionUi.Label(root, "주변", 155, 458, 80, 40, 23, accent, true, TextAnchor.MiddleCenter);
                }
                FocusActionText(root, "계속", step == 1 ? "숨을 느낀 뒤 아이콘을 눌러요" : "주의를 넓힌 뒤 아이콘을 눌러요", mission, next);
            }
            else
            {
                FocusCircle(root, 113.8f, 364.8f, 162.4f, FocusCircleColor(mission));
                FigmaMissionUi.Label(root, "✓", 153, 401, 84, 86, 62, accent, true, TextAnchor.MiddleCenter);
                for (int i = 0; i < 3; i++)
                {
                    FigmaMissionUi.Box(root, "완료한 실습", 45 + i * 102, 539.2f, 96, 56, FocusAlpha(accent, .12f), 16);
                    FigmaMissionUi.Label(root, new[] { "현재 ✓", "숨 ✓", "주변 ✓" }[i], 45 + i * 102, 539.2f, 96, 56, 13, accent, false, TextAnchor.MiddleCenter);
                }
                FocusActionText(root, "완료", "준비되면 아이콘을 눌러도 좋아요", mission, next);
            }
            tap.transform.SetAsLastSibling();
        }

        void BuildFocusEating(Transform root, FocusMissionDefinition mission, int step, Action next)
        {
            var accent = FocusAccent(mission);
            if (step == 0) FocusArtAt(root, "87_1254_img", 55, 335, 280, 280);
            else if (step == 3)
            {
                FocusCircle(root, 99.8f, 327.8f, 190.4f, FocusCircleColor(mission), FigmaMissionUi.Hex("E7CEB4"));
                FocusArtAt(root, "87_1407_imgIconP23", 132.44f, 360.44f, 122.4f, 122.4f);
                for (int i = 0; i < mission.Steps[step].Options.Length; i++)
                {
                    string option = mission.Steps[step].Options[i]; bool selected = focusAnswer == option;
                    var choice = FigmaMissionUi.Button(root, option, 45 + (i % 2) * 154, 526 + (i / 2) * 56, 146, 48, () =>
                    {
                        focusAnswer = focusAnswer == option ? "" : option; focusDirty = true; if (SaveFocusDraft()) ShowFocusPractice();
                    }, selected ? accent : FocusAlpha(Color.white, .65f), selected ? Color.white : accent, 14);
                    choice.name = "Focus.Sensation." + option;
                }
            }
            else
            {
                FocusCircle(root, 55, 335, 280, FocusCircleColor(mission), FigmaMissionUi.Hex("E7CEB4"), 2);
                if (step < 0) FocusArtAt(root, "87_1103_imgIconP23", 103, 383, 180, 180);
                else if (step == 1)
                {
                    FocusArtAt(root, "87_1284_imgIconP23", 103, 404, 180, 180); FocusArtAt(root, "87_1284_img", 147, 339, 92, 68);
                }
                else
                {
                    FocusArtAt(root, "87_891_img", 76, 356, 234, 234); FocusArtAt(root, "87_891_imgIconP23", 112, 375, 162, 162);
                    FigmaMissionUi.Box(root, "감각 단서", 113, 563, 160, 32, FocusAlpha(accent, .14f), 16);
                    FigmaMissionUi.Label(root, "온도  ·  질감", 113, 563, 160, 32, 14, accent, false, TextAnchor.MiddleCenter);
                }
            }
            FigmaMissionUi.Button(root, step < 0 ? "시작" : mission.Steps[step].ActionLabel, 45, 650, 300, 56, next, accent, Color.white, 18).name = "Focus.Action";
        }

        void BuildFocusSpeaking(Transform root, FocusMissionDefinition mission, int step, Action next)
        {
            var accent = FocusAccent(mission);
            if (step == 0) FocusArtAt(root, "87_942_img", 55, 335, 280, 280);
            else if (step == 1)
            {
                FocusCircle(root, 119, 280, 152, FocusCircleColor(mission), FigmaMissionUi.Hex("E0C8D1"));
                FocusArtAt(root, "87_1314_imgIconP25", 145, 306, 100, 100);
                FigmaMissionUi.Label(root, "전할 말 · 선택", 45, 478, 300, 24, 14, accent, true);
                focusAnswerInput = FigmaMissionUi.Input(root, mission.Steps[step].InputHint, 45, 510, 300, 80, 500);
                focusAnswerInput.name = "Focus.MessageInput"; focusAnswerInput.SetTextWithoutNotify(focusAnswer);
                focusAnswerInput.onValueChanged.AddListener(value => { focusAnswer = value; focusDirty = true; });
                focusAnswerInput.onEndEdit.AddListener(value => SaveFocusDraft());
                AttachFocusKeyboard(root, focusAnswerInput, "전할 말 확인", FigmaMissionUi.Hex(mission.BackgroundHex), accent);
                FigmaMissionUi.Label(root, "적지 않고 속으로만 정리해도 좋아요.", 45, 598, 300, 36, 14, FocusMuted(mission));
                FigmaMissionUi.Button(root, "속으로 확인했어요", 75, 714, 240, 48, () =>
                { focusAnswer = ""; focusAnswerInput.SetTextWithoutNotify(""); focusDirty = true; next(); }, Color.clear, accent, 14).name = "Focus.ThinkOnly";
            }
            else
            {
                FocusCircle(root, 55, 335, 280, FocusCircleColor(mission), FigmaMissionUi.Hex("E0C8D1"), 2);
                FocusArtAt(root, (step < 0 ? "87_1156" : step == 2 ? "87_1343" : "87_1446") + "_imgIconP25", 103, 383, 180, 180);
                if (step == 2) FocusArtAt(root, "87_1343_img", 241, 430, 72, 72);
                if (step == 3) for (int i = 0; i < 3; i++)
                {
                    FigmaMissionUi.Box(root, "확인한 순서", 77, 381 + i * 58, 232, 48, FocusAlpha(Color.white, .86f), 16);
                    FocusCircle(root, 91, 392 + i * 58, 26, accent);
                    FigmaMissionUi.Label(root, "✓", 91, 392 + i * 58, 26, 26, 16, Color.white, true, TextAnchor.MiddleCenter);
                    FigmaMissionUi.Label(root, new[] { "멈춤", "핵심 확인", "전달" }[i], 127, 381 + i * 58, 164, 48, 16, accent, false, TextAnchor.MiddleLeft);
                }
            }
            FigmaMissionUi.Button(root, step < 0 ? "시작" : mission.Steps[step].ActionLabel, 45, 650, 300, 56, next, accent, Color.white, 18).name = "Focus.Action";
        }

        bool FocusReflectionReady(out ActivitySession session)
        {
            session = opened ? ActiveSession() : null;
            if (!FocusMissionPolicy.IsFocus(session)) { ShowHome(); return false; }
            if (session.Status == ParticipationState.RewardCommitted) { ShowFocusCompletion(session.SessionId); return false; }
            if (session.Status == ParticipationState.Selected || session.FocusStepRecords.Any(record => string.IsNullOrEmpty(record.ConfirmedUtc))) { ShowFocusPractice(); return false; }
            BindFocusDraft(session, session.FocusStepRecords.Count);
            focusAnswerInput = null; focusNoteInput = null;
            return true;
        }

        // Retain serialized entry points from the former reflection flow. New
        // and resumed practices now finish without another emotion/note page.
        public void ShowFocusEmotion() => FinishFocusMission();
        public void ShowFocusNote() => FinishFocusMission();

        void FinishFocusMission()
        {
            if (!SaveFocusDraft() || !FocusReflectionReady(out var session)) return;
            if (focusPracticeClock != null) focusPracticeClock.Pause();
            if (!IsMissionDebugActive && NeedsPhotoChoice(session)) { ShowMissionPhoto(); return; }
            HandlePracticeCompletion(session.SessionId, () =>
            {
                focusDirty = false;
                focusAnswerInput = focusNoteInput = null;
                ResetCompletedNavigation();
                ShowFocusCompletion(session.SessionId);
            });
        }
        public void ShowFocusCompletion(string completedSessionId)
        {
            var session = opened ? Service.Snapshot.Sessions.FirstOrDefault(item => item.SessionId == completedSessionId) : null;
            var mission = FocusMissionCatalog.FindForSession(session);
            if (!FocusMissionPolicy.IsFocus(session) || mission == null || session.Status != ParticipationState.RewardCommitted) { ShowHome(); return; }
            focusDirty = false; focusAnswerInput = null; focusNoteInput = null;
            var root = BeginFigmaPage("focus-complete", () => ShowFocusCompletion(completedSessionId), FigmaMissionUi.Hex(mission.BackgroundHex));
            var accent = FocusAccent(mission);
            FigmaMissionUi.Label(root, mission.Title, 24, 24, 342, 24, 14, FocusMuted(mission));
            FigmaMissionUi.Label(root, "미션 완료!", 24, 90, 342, 40, 32, FocusInk(mission), true);
            var plantState = Service.Snapshot;
            string storedGrowth = StoredGrowthSummary(plantState);
            var growth = Service.GetPracticeGrowth(completedSessionId);
            var grownPlant = growth == null ? null : CombinedPlants.Find(plantState, growth.PlantInstanceId);
            bool plantCompleted = grownPlant != null && growth.AfterStep > growth.BeforeStep
                && growth.BeforeStep < grownPlant.Recipe.MaxGrowthSteps
                && growth.AfterStep == grownPlant.Recipe.MaxGrowthSteps;
            string result = plantCompleted ? "식물이 완성됐어요" + (storedGrowth == null ? "" : "\n" + storedGrowth)
                : storedGrowth ?? (growth != null && growth.AfterStep > growth.BeforeStep ? "식물이 한 단계 자랐어요" : "기록을 저장했어요");
            FigmaMissionUi.Label(root, result, 24, 138, 342, plantCompleted && storedGrowth != null ? 52 : 28,
                plantCompleted && storedGrowth != null ? 16 : 18, FocusMuted(mission)).name = "Focus.GrowthResult";
            if (session.RewardAmount > 0)
                FigmaMissionUi.Label(root, "코인 +" + (session.RewardAmount + session.PhotoBonusAmount), 24, 206, 342, 32, 22, accent, true)
                    .name = "Focus.CoinReward";
            bool illustratedBreath = mission.Code == "P01" && FocusMissionCatalog.IsGuided(session);
            if (illustratedBreath)
            {
                var illustration = FigmaMissionUi.Rect(root, "P01.CompletedIllustration", 55, 319.5f, 280, 280);
                illustration.gameObject.AddComponent<P01BreathIllustration>().Configure(P01BreathIllustration.Stage.Reflection,
                    session.FocusStepRecords.Count > 0 ? session.FocusStepRecords[0].Value : "");
            }
            else if (mission.Code == "P02" && FocusMissionCatalog.IsGuided(session))
            {
                var illustration = FigmaMissionUi.Rect(root, "P02.CompletedIllustration", 55, 319.5f, 280, 280);
                illustration.gameObject.AddComponent<P02SoundIllustration>().Configure(P02SoundIllustration.Stage.Reflection);
            }
            else if (UsesM01LightGuide(session))
            {
                var illustration = FigmaMissionUi.Rect(root, "M01.CompletedIllustration", 55, 319.5f, 280, 280);
                illustration.gameObject.AddComponent<M01LightIllustration>().Configure(M01LightIllustration.Stage.Complete);
            }
            else if (FocusMissionCatalog.IsGuided(session) || UsesBasicFocusSlides(session))
            {
                var illustration = FigmaMissionUi.Rect(root, "Focus.CompletedIllustration", 55, 319.5f, 280, 280);
                string choice = SavedPracticeIllustrationChoice(mission, session);
                illustration.gameObject.AddComponent<FocusPracticeIllustration>().Configure(mission.Code, "reflection", choice, accent);
            }
            else
            {
                FocusCircle(root, 55, 319.5f, 280, FocusCircleColor(mission));
                if (FocusMissionCatalog.HasOriginalLayout(mission.Code))
                {
                    string asset = mission.Code == "M03" ? "87_799_imgIconWaterDropComplete" : mission.Code == "P10" ? "87_871_imgIconPausebreathComplete" : mission.Code == "P23" ? "87_922_imgIconP23Complete" : "87_972_imgIconP25Complete";
                    bool tall = mission.Code == "M03" || mission.Code == "P10";
                    FocusArtAt(root, asset, 105, tall ? 354.5f : 369.5f, 180, tall ? 210 : 180);
                }
                else
                {
                    FigmaMissionUi.Asset(root, mission.IconAsset, 105, 369.5f, 180, 180);
                    FocusCircle(root, 265, 529, 42, accent);
                    FigmaMissionUi.Label(root, "✓", 265, 529, 42, 42, 26, Color.white, true, TextAnchor.MiddleCenter);
                }
            }
            string message = mission.Code == "M03" ? "물 마시기 완료" : mission.Code == "P10" ? "잠시 숨을 골랐어요" : mission.Code == "P23" ? "한입을 느꼈어요" : mission.Code == "P25" ? "전할 말을 확인했어요" : "오늘의 실천을 남겼어요";
            FigmaMissionUi.Label(root, message, 45, 613.5f, 300, 36, 24, accent, true, TextAnchor.MiddleCenter);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsMissionDebugActive)
            {
                FigmaMissionUi.Label(root, "테스트 결과 · 실제 기록에 반영되지 않아요", 24, 662, 342, 28, 14, FocusMuted(mission));
                FigmaMissionUi.Button(root, "다른 P/M 테스트", 24, 706, 342, 48, ShowMissionDebugPicker, Color.clear, accent, 16).name = "Focus.DebugAgain";
                FigmaMissionUi.Button(root, "테스트 종료", 24, 764, 342, 52, ShowHome, accent).name = "Focus.DebugExit";
                FocusEnter(root);
                return;
            }
#endif
            FigmaMissionUi.Button(root, "오늘의 미션으로", 24, 698, 166, 48, ShowHome, Color.clear, accent, 16).name = "Focus.ConfirmHome";
            FigmaMissionUi.Button(root, "기록으로 이동", 200, 698, 166, 48, () => ShowActivityJournalForMission(completedSessionId), Color.clear, accent, 16).name = "Focus.ViewRecord";
            if (plantCompleted)
                FigmaMissionUi.Button(root, "사진첩으로", 24, 756, 166, 58, ShowPlantAlbum, accent, Color.white, 18).name = "Focus.PlantAlbum";
            FigmaMissionUi.Button(root, "정원으로", plantCompleted ? 200 : 24, 756, plantCompleted ? 166 : 342, 58,
                GoGarden, accent, Color.white, 18).name = "Focus.GoGarden";
            FocusEnter(root);
        }

        static void FocusEnter(RectTransform root)
        {
            root.GetComponent<FocusMobilePageLayout>()?.Refresh();
            root.GetComponent<FocusGuidedSlideLayout>()?.Refresh();
        }

        string FocusPracticeContext(string regular)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsMissionDebugActive) return "테스트 · 실제 기록에 미반영";
#endif
            return regular;
        }

        void AttachFocusKeyboard(Transform root, InputField input, string title, Color background, Color accent)
        {
            var keyboard = root.gameObject.AddComponent<FocusMissionKeyboardAvoidance>();
            keyboard.Input = input; keyboard.Title = title; keyboard.Background = background; keyboard.Accent = accent;
            keyboard.Save = () => SaveFocusDraft();
        }
    }
}
