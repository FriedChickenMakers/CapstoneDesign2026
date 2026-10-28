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
        bool focusDirty;
        InputField focusAnswerInput, focusNoteInput;
        float focusNextAction;
        static Sprite focusCircleSprite;

        static Color FocusAlpha(Color color, float alpha) { color.a = alpha; return color; }
        static Color FocusAccent(FocusMissionDefinition mission) => FigmaMissionUi.Hex(mission.AccentHex);
        static Color FocusInk(FocusMissionDefinition mission) => FigmaMissionUi.Hex(mission.TextHex);
        static Color FocusMuted(FocusMissionDefinition mission) => FigmaMissionUi.Hex(mission.Code == "P10" ? "587572" : mission.Code == "M03" ? "466A7A" : "466A75");
        static Color FocusCircleColor(FocusMissionDefinition mission) => FigmaMissionUi.Hex(mission.CircleHex ?? (mission.Code == "M03" ? "D9F1FA" : mission.Code == "P10" ? "DDF2EE" : mission.Code == "P23" ? "F4E2CB" : "EDDCE2"));

        void BindFocusDraft(ActivitySession session, int step)
        {
            if (focusDraftSession != session.SessionId)
            {
                focusDraftSession = session.SessionId; focusMood = session.Mood ?? ""; focusNote = session.Note ?? "";
                focusDraftStep = -1; focusAnswer = ""; focusDirty = false;
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
            return saved;
        }

        public bool SaveFocusDraft()
        {
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
            if (screen == "focus-story") { ShowFocusPractice(); return true; }
            if (screen != "focus-practice" && screen != "focus-emotion" && screen != "focus-note" && screen != "focus-save-error" && screen != "focus-complete") return false;
            if (screen == "focus-complete") { ShowHome(); return true; }
            if (screen == "focus-save-error") { ShowFocusNote(); return true; }
            if (!SaveFocusDraft()) return true;
            var session = ActiveSession();
            if (!FocusMissionPolicy.IsFocus(session)) return false;
            if (screen == "focus-note")
            {
                if (FocusSaved(Service.SaveFocusReflection(session.SessionId, focusMood, focusNote, 0))) ShowFocusEmotion();
            }
            else if (screen == "focus-emotion")
            {
                if (FocusSaved(Service.MoveFocusStep(session.SessionId, session.FocusStepRecords.Count - 1))) ShowFocusPractice();
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
            if (session.Status == ParticipationState.RewardCommitted) { ShowFocusCompletion(session.SessionId); return; }
            if (session.Status == ParticipationState.Paused)
            {
                if (!FocusSaved(Service.ResumeSession(session.SessionId))) return;
                session = ActiveSession();
            }
            bool overview = session.Status == ParticipationState.Selected;
            if (!overview && session.InstructionStep >= mission.Steps.Length)
            {
                if (session.InstructionStep == mission.Steps.Length) ShowFocusEmotion(); else ShowFocusNote();
                return;
            }
            int step = overview ? -1 : session.InstructionStep;
            BindFocusDraft(session, step);
            focusAnswerInput = null; focusNoteInput = null;
            var root = BeginFigmaPage("focus-practice", ShowFocusPractice, FigmaMissionUi.Hex(mission.BackgroundHex));
            string frame = FocusMissionCatalog.HasOriginalLayout(mission.Code) ? FocusFrame(mission.Code, step) : "86_770";
            FocusPracticeHeader(root, mission, step, frame);
            string id = session.SessionId;
            Action next = () => AdvanceFocus(id, step);
            if (mission.Code == "M03") BuildFocusWater(root, mission, step, next);
            else if (mission.Code == "P10") BuildFocusBreath(root, mission, step, next);
            else if (mission.Code == "P23") BuildFocusEating(root, mission, step, next);
            else if (mission.Code == "P25") BuildFocusSpeaking(root, mission, step, next);
            else BuildAllFocusPractice(root, mission, step, next);
            AttachAllFocusClock(root, mission, step);
            if (step >= 0) FigmaMissionUi.Button(root, "이전 단계", 125, 766, 140, 48, () => TryFocusBack(), Color.clear, FocusMuted(mission), 14).name = "Focus.Back";
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
            int total = mission.Steps.Length + 2;
            FigmaMissionUi.Label(root, (step + 2) + " / " + total, 314, 36, 52, 24, 14, ink, false, TextAnchor.MiddleRight);
            FigmaMissionUi.Label(root, mission.Code + " · " + mission.Title, 24, 81, 342, 18, 13, FocusAlpha(accent, .82f));
            bool original = FocusMissionCatalog.HasOriginalLayout(mission.Code);
            string heading = step < 0 ? mission.Title : mission.Steps[step].Title;
            int headingSize = original ? (compact ? 26 : 32) : heading.Length > 13 ? 22 : 26;
            FigmaMissionUi.Label(root, heading, 24, 103, 342, original ? (compact ? 34 : 40) : 48, headingSize, ink, true);
            FigmaMissionUi.Label(root, step < 0 ? mission.Overview : mission.Steps[step].Body, 24, original ? (compact ? 140 : 147) : 153, 342, original ? 48 : 60, 18, FocusMuted(mission));
            float width = (342 - 4 * (total - 1)) / (float)total;
            for (int i = 0; i < total; i++)
            {
                float x = 24 + i * (width + 4), y = original ? (compact ? 190 : 197) : 222;
                var tint = FocusAlpha(accent, i <= step + 1 ? 1 : .18f);
                FigmaMissionUi.Box(root, "단계 " + (i + 1), x, y, width, 6, tint, 3);
            }
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

        static Button FocusTap(Transform root, string label, Action action, Color color, float y = 319, float h = 392)
        {
            var button = FigmaMissionUi.Button(root, "", 45, y, 300, h, action, Color.clear, color);
            button.name = label;
            button.transition = Selectable.Transition.None;
            var feedback = button.gameObject.AddComponent<FocusMissionTapFeedback>(); feedback.Button = button;
            return button;
        }

        static void FocusCaption(Transform root, string text, Color color, float y = 571.2f)
        {
            FigmaMissionUi.Box(root, text, 45, y, 300, 48, FocusAlpha(Color.white, .72f), 18);
            FigmaMissionUi.Label(root, text, 53, y, 284, 48, 16, color, false, TextAnchor.MiddleCenter);
        }

        static void FocusActionText(Transform root, string label, string hint, FocusMissionDefinition mission, float y = 631.2f)
        {
            FigmaMissionUi.Label(root, label, 45, y, 300, 32, 24, FocusAccent(mission), true, TextAnchor.MiddleCenter);
            FigmaMissionUi.Label(root, hint, 45, y + 41, 300, 24, 14, FocusMuted(mission), false, TextAnchor.MiddleCenter);
        }

        void BuildFocusWater(Transform root, FocusMissionDefinition mission, int step, Action next)
        {
            var tap = FocusTap(root, step < 0 ? "시작" : mission.Steps[step].ActionLabel, next, FocusAccent(mission));
            // The full illustration and its label form a single large hit target.
            if (step < 1)
            {
                FocusCircle(root, 85.8f, 340.8f, 218.4f, FocusCircleColor(mission), FigmaMissionUi.Hex("B2E0F0"));
                if (step < 0) FocusArtAt(root, "86_770_imgIconWaterDropFriendly", 123.24f, 366.54f, 140.4f, 163.8f);
                else FocusArtAt(root, "87_990_imgIconM03WaterCup", 123.44f, 378.44f, 140, 140);
                FocusCaption(root, step < 0 ? "오늘의 한 모금" : "물을 가까이 두었나요?", FocusAccent(mission));
                FocusActionText(root, step < 0 ? "시작" : "준비했어요", step < 0 ? "물방울을 눌러 시작" : "준비한 뒤 물방울을 눌러요", mission);
            }
            else
            {
                FocusArtAt(root, "87_770_img280280", 55, 340, 280, 280);
                FocusActionText(root, "물 마셨어요", "마신 뒤 물방울을 눌러요", mission, 632);
            }
            tap.transform.SetAsLastSibling();
        }

        void BuildFocusBreath(Transform root, FocusMissionDefinition mission, int step, Action next)
        {
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
                FocusCaption(root, step < 0 ? "3단계로 잠시 멈춰요" : "있는 그대로 살펴요", accent);
                FocusActionText(root, step < 0 ? "시작" : "계속", step < 0 ? "아이콘을 눌러 시작" : "살펴본 뒤 아이콘을 눌러요", mission);
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
                FocusActionText(root, "계속", step == 1 ? "숨을 느낀 뒤 아이콘을 눌러요" : "주의를 넓힌 뒤 아이콘을 눌러요", mission, 618);
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
                FocusActionText(root, "완료", "준비되면 아이콘을 눌러요", mission, 607.2f);
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
                    var choice = FigmaMissionUi.Button(root, option, 45 + (i % 2) * 154, 534.2f + (i / 2) * 48, 146, 40, () =>
                    {
                        focusAnswer = focusAnswer == option ? "" : option; focusDirty = true; SaveFocusDraft(); ShowFocusPractice();
                    }, selected ? accent : FocusAlpha(Color.white, .65f), selected ? Color.white : accent, 13);
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
            FigmaMissionUi.Button(root, step < 0 ? "시작" : mission.Steps[step].ActionLabel, 45, step == 3 ? 638.2f : 631, 300, 64, next, accent, Color.white, 18).name = "Focus.Action";
        }

        void BuildFocusSpeaking(Transform root, FocusMissionDefinition mission, int step, Action next)
        {
            var accent = FocusAccent(mission);
            if (step == 0) FocusArtAt(root, "87_942_img", 55, 335, 280, 280);
            else if (step == 1)
            {
                FocusCircle(root, 99.8f, 331.8f, 190.4f, FocusCircleColor(mission), FigmaMissionUi.Hex("E0C8D1"));
                FocusArtAt(root, "87_1314_imgIconP25", 132.44f, 364.44f, 122.4f, 122.4f);
                FigmaMissionUi.Label(root, "전할 말 · 선택", 61, 534, 268, 20, 13, accent);
                focusAnswerInput = FigmaMissionUi.Input(root, mission.Steps[step].InputHint, 45, 558, 300, 60, 500);
                focusAnswerInput.name = "Focus.MessageInput"; focusAnswerInput.SetTextWithoutNotify(focusAnswer);
                focusAnswerInput.onValueChanged.AddListener(value => { focusAnswer = value; focusDirty = true; });
                focusAnswerInput.onEndEdit.AddListener(value => SaveFocusDraft());
                AttachFocusKeyboard(root, focusAnswerInput, "전할 말 확인", FigmaMissionUi.Hex(mission.BackgroundHex), accent);
                FigmaMissionUi.Button(root, "속으로 확인했어요", 75, 704, 240, 44, () =>
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
            FigmaMissionUi.Button(root, step < 0 ? "시작" : mission.Steps[step].ActionLabel, 45, step == 1 ? 634.2f : 631, 300, 64, next, accent, Color.white, 18).name = "Focus.Action";
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

        static void FocusRoleBadge(Transform root, ActivitySession session)
        {
            bool basic = session.FocusRole == FocusMissionCatalog.BasicRole;
            FigmaMissionUi.Box(root, "미션 구분", 24, 140, 78, 37, basic ? FigmaMissionUi.Soft : FigmaMissionUi.Warm, 12);
            FigmaMissionUi.Label(root, basic ? "기본 미션" : "추가 미션", 24, 140, 78, 37, 14, basic ? FigmaMissionUi.Green : FigmaMissionUi.Brown, true, TextAnchor.MiddleCenter);
        }

        public void ShowFocusEmotion()
        {
            if (!FocusReflectionReady(out var session)) return;
            var root = BeginFigmaPage("focus-emotion", ShowFocusEmotion, FigmaMissionUi.Background);
            FigmaHeader(root, "지금 느낌은 어떤가요?", "감정 기록 1 / 2 · 선택"); FocusRoleBadge(root, session);
            FigmaMissionUi.Label(root, "가장 가까운 감정 하나를 골라요.\n다시 누르면 선택이 해제돼요.", 24, 193, 342, 50, 16, FigmaMissionUi.Muted);
            for (int i = 0; i < FocusEmotions.Length; i++)
            {
                string emotion = FocusEmotions[i]; bool selected = focusMood == emotion;
                FigmaMissionUi.Button(root, emotion, 24 + (i % 4) * 87.5f, 259 + (i / 4) * 60, 79.5f, 52, () =>
                {
                    focusMood = focusMood == emotion ? "" : emotion; focusDirty = true; SaveFocusDraft(); ShowFocusEmotion();
                }, selected ? FigmaMissionUi.Green : Color.white, selected ? Color.white : FigmaMissionUi.Ink, 14).name = "Focus.Emotion." + emotion;
            }
            FigmaMissionUi.Label(root, "선택한 감정", 24, 632, 104, 24, 14, FigmaMissionUi.Muted);
            FigmaMissionUi.Label(root, string.IsNullOrEmpty(focusMood) ? "선택하지 않았어요" : focusMood, 130, 632, 236, 24, 16, FigmaMissionUi.Green, true);
            FigmaMissionUi.Label(root, "고르지 않아도 다음으로 갈 수 있어요.", 24, 670, 342, 24, 14, FigmaMissionUi.Muted);
            FocusBackControl(root, "23_192_imgIconBack");
            FigmaMissionUi.Button(root, "다음", 84, 768, 282, 52, () =>
            {
                if (!FocusSaved(Service.SaveFocusReflection(session.SessionId, focusMood, focusNote, 1))) return;
                focusDirty = false; ShowFocusNote();
            }).name = "Focus.ReflectionNext";
        }

        void FocusBackControl(Transform root, string asset)
        {
            var back = FigmaMissionUi.Button(root, "", 24, 768, 48, 52, () => TryFocusBack(), Color.clear);
            back.name = "Focus.Back"; FocusArtAt(back.transform, asset, 12, 14, 24, 24);
        }

        public void ShowFocusNote() => RenderFocusNote(false);

        void RenderFocusNote(bool saveError)
        {
            if (!FocusReflectionReady(out var session)) return;
            var root = BeginFigmaPage(saveError ? "focus-save-error" : "focus-note", () => RenderFocusNote(saveError), FigmaMissionUi.Background);
            if (notice != null && saveError) notice.gameObject.SetActive(false);
            FigmaHeader(root, "한 줄 남기기", "감정 기록 2 / 2 · 선택"); FocusRoleBadge(root, session);
            float shift = saveError ? 124 : 0;
            if (saveError)
            {
                FigmaMissionUi.Box(root, "저장 실패", 24, 193, 342, 108, FigmaMissionUi.Hex("FFF0EB"), 24);
                FigmaMissionUi.Label(root, "아직 저장하지 못했어요.", 40, 209, 310, 26, 18, FigmaMissionUi.Hex("923E30"), true);
                FigmaMissionUi.Label(root, "작성한 내용은 그대로 남아 있어요.\n내용을 확인하고 다시 시도해주세요.", 40, 243, 310, 42, 14, FigmaMissionUi.Hex("923E30"));
            }
            FigmaMissionUi.Label(root, "고른 감정", 24, 193 + shift, 342, 21, 14, FigmaMissionUi.Muted);
            FigmaMissionUi.Label(root, string.IsNullOrEmpty(focusMood) ? "선택하지 않았어요" : focusMood, 24, 222 + shift, 342, 26, 18, FigmaMissionUi.Green, true);
            FigmaMissionUi.Label(root, "어떤 순간이 떠오르나요?", 24, 264 + shift, 342, 26, 18, null, true);
            focusNoteInput = FigmaMissionUi.Input(root, "감정이나 생각을 편하게 적어보세요.", 24, 306 + shift, 342, 164, 500);
            focusNoteInput.name = "Focus.NoteInput"; focusNoteInput.SetTextWithoutNotify(focusNote);
            focusNoteInput.onValueChanged.AddListener(value => { focusNote = value; focusDirty = true; });
            focusNoteInput.onEndEdit.AddListener(value => SaveFocusDraft());
            AttachFocusKeyboard(root, focusNoteInput, "한 줄 남기기", FigmaMissionUi.Background, FigmaMissionUi.Green);
            FigmaMissionUi.Label(root, "적지 않아도 미션을 완료할 수 있어요.", 24, 486 + shift, 342, 21, 14, FigmaMissionUi.Muted);
            if (saveError)
                FigmaMissionUi.Button(root, "입력 내용 확인하기", 24, 768, 342, 52, () => { focusNote = focusNoteInput.text; ShowFocusNote(); }).name = "Focus.ReviewFailedSave";
            else
            {
                FocusBackControl(root, "47_1511_imgIconBack");
                FigmaMissionUi.Button(root, "미션 완료", 84, 768, 282, 52, () => CompleteFocusPractice(session.SessionId)).name = "Focus.Complete";
            }
        }

        void CompleteFocusPractice(string id)
        {
            if ((Application.isPlaying && Time.unscaledTime < focusNextAction) || sessionId != id || screen != "focus-note") return;
            focusNextAction = Time.unscaledTime + .2f;
            if (focusNoteInput != null) focusNote = focusNoteInput.text;
            // Keep the editable draft until the session and reward commit succeeds atomically.
            focusDirty = true;
            if (!FocusSaved(Service.CompleteFocusSession(id, focusMood, focusNote))) { RenderFocusNote(true); return; }
            focusDirty = false; focusAnswerInput = null; focusNoteInput = null;
            ResetCompletedNavigation(); UpdateGarden(); ShowFocusCompletion(id);
        }

        public void ShowFocusCompletion(string completedSessionId)
        {
            var session = opened ? Service.Snapshot.Sessions.FirstOrDefault(item => item.SessionId == completedSessionId) : null;
            var mission = FocusMissionCatalog.FindForSession(session);
            if (!FocusMissionPolicy.IsFocus(session) || mission == null || session.Status != ParticipationState.RewardCommitted) { ShowHome(); return; }
            focusDirty = false; focusAnswerInput = null; focusNoteInput = null;
            var root = BeginFigmaPage("focus-complete", () => ShowFocusCompletion(completedSessionId), FigmaMissionUi.Hex(mission.BackgroundHex));
            var accent = FocusAccent(mission); int total = mission.Steps.Length + 2;
            FigmaMissionUi.Label(root, total + " / " + total + " · 미션 완료", 24, 24, 342, 24, 14, FocusMuted(mission));
            FigmaMissionUi.Label(root, "미션 완료!", 24, 90, 342, 40, 32, FocusInk(mission), true);
            FigmaMissionUi.Label(root, session.RewardAmount > 0 ? "영양제 +" + session.RewardAmount : "기록을 저장했어요", 24, 138, 342, 28, 18, FocusMuted(mission));
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
            string message = mission.Code == "M03" ? "물 마시기 완료" : mission.Code == "P10" ? "잠시 숨을 골랐어요" : mission.Code == "P23" ? "한입을 느꼈어요" : mission.Code == "P25" ? "전할 말을 확인했어요" : "오늘의 실천을 남겼어요";
            FigmaMissionUi.Label(root, message, 45, 613.5f, 300, 36, 24, accent, true, TextAnchor.MiddleCenter);
            FigmaMissionUi.Button(root, "확인", 125, 698, 140, 48, ShowHome, Color.clear, accent, 16).name = "Focus.ConfirmHome";
            FigmaMissionUi.Button(root, "정원으로", 24, 756, 342, 58, GoGarden, accent, Color.white, 18).name = "Focus.GoGarden";
            FocusEnter(root);
        }

        static void FocusEnter(RectTransform root)
        {
            if (Application.isPlaying) root.gameObject.AddComponent<FocusMissionEntrance>();
        }

        void AttachFocusKeyboard(Transform root, InputField input, string title, Color background, Color accent)
        {
            var keyboard = root.gameObject.AddComponent<FocusMissionKeyboardAvoidance>();
            keyboard.Input = input; keyboard.Title = title; keyboard.Background = background; keyboard.Accent = accent;
            keyboard.Save = () => SaveFocusDraft();
        }
    }
}
