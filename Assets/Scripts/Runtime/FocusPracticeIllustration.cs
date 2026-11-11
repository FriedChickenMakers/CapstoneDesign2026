using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // These are illustrations, not extra controls, prescribed movements, sensor
    // readings or timers. Only the current mission, slide and saved choice draw.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class FocusPracticeIllustration : MaskableGraphic
    {
        public string MissionCode = "", StepKey = "", Choice = "";
        string contextTrigger = "", contextActivity = "", contextPeriod = "";
        bool contextCurrentReview;
        Color accent = C(80, 115, 139);
        static readonly Color Paper = C(247, 244, 231), Skin = C(235, 199, 169), SkinShade = C(209, 164, 140);
        static readonly Color Ink = C(66, 87, 83), Wood = C(169, 145, 118), Sky = C(158, 188, 193);
        VertexHelper mesh;
        Rect drawingRect;
        float unit, antialias, modelScale = 1;
        Vector2 modelCenter = new Vector2(128, 128);
        Color Light => Color.Lerp(accent, Paper, .68f);
        Color Middle => Color.Lerp(accent, Paper, .36f);
        bool Quiet => Is("overview", "preparation", "prepare", "adjust", "mountain-prepare", "sound-prepare");

        public void Configure(string code, string key, string choice, Color themeAccent)
        {
            code = code ?? ""; key = key ?? ""; choice = choice ?? "";
            if (MissionCode == code && StepKey == key && Choice == choice && accent == themeAccent) return;
            if (MissionCode != code)
            { contextTrigger = contextActivity = contextPeriod = ""; contextCurrentReview = false; }
            MissionCode = code; StepKey = key; Choice = choice; accent = themeAccent; SetVerticesDirty();
        }
        // Display context supplied by the presenter. No service or saved state is owned here.
        public void ConfigureContext(string trigger, string activity, string period, bool currentReview = false)
        {
            trigger = trigger ?? ""; activity = activity ?? ""; period = period ?? "";
            if (contextTrigger == trigger && contextActivity == activity && contextPeriod == period
                && contextCurrentReview == currentReview) return;
            contextTrigger = trigger; contextActivity = activity; contextPeriod = period;
            contextCurrentReview = currentReview; SetVerticesDirty();
        }
        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); drawingRect = GetPixelAdjustedRect();
            unit = Mathf.Min(drawingRect.width, drawingRect.height) / 232f;
            if (unit <= 0 || MissionCode.Length != 3 || MissionCode[0] != 'P'
                || !int.TryParse(MissionCode.Substring(1), out int number) || number < 3 || number > 28) return;
            mesh = helper; modelScale = 1; modelCenter = new Vector2(128, 128);
            antialias = .85f / Mathf.Max(.01f, unit * (canvas == null ? 1 : canvas.scaleFactor));
            DrawMission(number);
            if (StepKey == "guide-help" || StepKey == "help" && MissionCode != "P26") At(204, 193, .28f, Book);
            else if (StepKey == "reflection") At(51, 191, .28f, Note);
            mesh = null;
        }
        bool Is(params string[] keys) => Array.IndexOf(keys, StepKey) >= 0;
        bool Chosen(string value) => string.Equals(Choice, value, StringComparison.Ordinal);
        void At(float x, float y, float scale, Action draw)
        {
            var previousCenter = modelCenter; float previousScale = modelScale, previousAa = antialias;
            modelCenter += new Vector2(x - 128, y - 128) * modelScale;
            modelScale *= scale; antialias /= scale;
            try { draw(); } finally { modelCenter = previousCenter; modelScale = previousScale; antialias = previousAa; }
        }

        void DrawMission(int number)
        {
            switch (number)
            {
                case 3: ContactScene(false); break;
                case 4: DrinkScene(); break;
                case 5: ReturningScene(false); break;
                case 6: WashingScene(); break;
                case 7: SelectionScene(7); break;
                case 8: FeelingScene(); break;
                case 9: ContactScene(true); break;
                case 10: AwarenessScene(10); break;
                case 11: ReturningScene(true); break;
                case 12: EverydayScene(); break;
                case 13: AwarenessScene(13); break;
                case 14: SelectionScene(14); break;
                case 15: KindWordScene(); break;
                case 16: PostureScene(); break;
                case 17: AwarenessScene(17); break;
                case 18: SelectionScene(18); break;
                case 19: MountainScene(); break;
                case 20: SelectionScene(20); break;
                case 21: SelectionScene(21); break;
                case 22: MovingScene(); break;
                case 23: EatingScene(); break;
                case 24: ListeningScene(); break;
                case 25: SpeakingScene(); break;
                case 26: HelpedScene(); break;
                case 27: RoutineScene(); break;
                case 28: PlanningScene(); break;
            }
        }
        void ContactScene(bool front)
        {
            if (front)
            {
                At(130, 132, .91f, Person); Ground(58, 209, 198);
                if (Is("contact", "notice")) { Contact(94, 210, 16); Contact(162, 210, 16); }
                if (StepKey == "notice") At(207, 84, .21f, Leaf);
            }
            else
            {
                Chair();
                if (StepKey == "overview") At(211, 191, .24f, Leaf);
                if (Is("cue0", "cue1")) { Contact(86, 146, 14); Contact(157, 216, 19); }
                if (StepKey == "cue1") Curve(accent, 2.4f, 184, 173, 194, 180, 194, 198, 184, 206);
                if (StepKey == "cue2") { At(199, 58, .27f, Cloud); Arrow(207, 89, 223, 150, 170, 204); }
                if (StepKey == "practice") Contact(157, 216, 17);
            }
        }
        void DrinkScene()
        {
            Cup();
            if (!Is("overview", "preparation")) At(131, 173, .74f, Hands);
            if (Is("overview", "cue0"))
            {
                Curve(A(accent, .48f), 2.1f, 87, 56, 105, 48, 147, 48, 164, 56);
                if (StepKey == "cue0") Contact(86, 181, 13);
            }
            if (StepKey == "preparation") Ground(67, 211, 191);
            if (StepKey == "cue1") { At(205, 80, .23f, Drop); Arrow(191, 117, 215, 100, 207, 89); }
            if (StepKey == "cue2") { Ellipse(128, 197, 45, 5, A(accent, .11f)); Curve(accent, 2.2f, 190, 91, 204, 109, 204, 128, 193, 144); }
        }
        void ReturningScene(bool thoughtFirst)
        {
            if (thoughtFirst)
            {
                At(110, 107, .77f, Cloud); At(137, 184, .47f, Breath);
                if (Is("overview", "thought")) At(191, 64, .24f, Cloud);
                if (Is("return", "breath")) Arrow(190, 109, 220, 156, 166, 178);
                if (StepKey == "notice") At(197, 190, .23f, Note);
            }
            else
            {
                At(127, 140, .85f, Person);
                if (Is("overview", "cue1", "cue2")) At(202, 57, .31f, Cloud);
                if (Is("cue0", "practice")) Air(60, 100, .86f);
                if (StepKey == "cue2") Arrow(217, 89, 230, 133, 156, 145);
            }
        }
        void WashingScene()
        {
            Faucet();
            bool dishes = Chosen("설거지");
            if (dishes) At(130, 155, .72f, Plate);
            else At(128, 166, .92f, Hands);
            if (Is("overview", "choice") && string.IsNullOrEmpty(Choice)) At(204, 185, .36f, Plate);
            Ellipse(128, 211, 74, 6, A(Sky, .16f));
            if (!Is("preparation", "help", "reflection"))
            {
                At(112, 114, .13f, Drop); At(127, 132, .11f, Drop); At(141, 115, .13f, Drop);
            }
            if (StepKey == "cue1")
            {
                Ellipse(79, 148, 7, 7, A(Paper, .85f)); Ellipse(67, 158, 4, 4, Light);
                Curve(accent, 2.4f, 177, 149, 190, 158, 191, 171, 180, 183);
            }
            if (StepKey == "cue2") { At(204, 73, .25f, Cloud); Arrow(217, 99, 228, 144, 174, 157); }
        }
        void SelectionScene(int kind)
        {
            if (kind == 18)
            {
                TargetPad(81, 126, 43, 64); TargetPad(179, 126, 43, 64);
                At(81, 126, .58f, Breath); At(179, 126, .58f, Ear);
                if (StepKey == "practice" && (Chosen("호흡") || Chosen("주변 소리")))
                { SoftFrame(Chosen("호흡") ? 81 : 179, 125, 44, 64); }
                if (Is("overview", "activity", "practice"))
                { Arrow(75, 59, 123, 31, 176, 59); Arrow(179, 196, 129, 220, 78, 196); }
            }
            else if (string.IsNullOrEmpty(Choice) || !KnownTarget())
            {
                TargetPad(73, 102, 43, 54); TargetPad(181, 102, 43, 54); TargetPad(128, 188, 39, 44);
                At(73, 102, .49f, Breath); At(181, 102, .49f, Ear); At(128, 188, .43f, Chair);
            }
            else At(128, 122, .92f, Target);
            if (kind == 14 && !Is("guide-help", "help")) At(211, 203, .30f, Frequency);
            if (kind == 20)
            {
                At(59, 198, .25f, Heart);
                if (StepKey == "action") At(202, 197, .25f, Note);
            }
            if (kind == 21 && Is("overview", "save")) At(211, 202, .28f, Bookmark);
            if (kind == 7 && StepKey == "preparation") At(128, 36, .21f, Cloud);
            if (kind == 7 && StepKey == "cue2") { At(221, 44, .24f, Cloud); Arrow(218, 77, 230, 135, 174, 149); }
            if (kind != 14 && Is("preference", "notice")) At(203, 200, .24f, Note);
        }
        string TargetChoice => (MissionCode == "P27" || MissionCode == "P28"
            && (Is("next-activity", "time") || StepKey == "review" && !contextCurrentReview))
            && !string.IsNullOrEmpty(contextActivity) ? contextActivity : Choice;
        bool KnownTarget() => TargetChoice == "호흡" || TargetChoice == "몸의 접촉" || TargetChoice == "주변 소리" || TargetChoice == "소리" || TargetChoice == "잠시 멈춤";
        void Target()
        {
            if (TargetChoice == "호흡") Breath();
            else if (TargetChoice == "몸의 접촉") Chair();
            else if (TargetChoice == "주변 소리" || TargetChoice == "소리") { Ear(); if (!Quiet) SoundExamples(); }
            else if (TargetChoice == "잠시 멈춤") Rest();
            else { At(80, 117, .48f, Breath); At(175, 117, .48f, Ear); }
        }
        void FeelingScene()
        {
            At(128, 133, .94f, Bubble);
            // A neutral feeling symbol does not turn every answer into a happy heart.
            Ellipse(128, 113, 34, 32, A(Middle, .72f));
            Curve(accent, 2.6f, 110, 114, 114, 110, 119, 110, 123, 114);
            Curve(accent, 2.6f, 134, 114, 138, 110, 143, 110, 147, 114);
            Curve(A(accent, .7f), 2.2f, 118, 131, 124, 132, 132, 132, 138, 131);
            if (StepKey == "overview") At(201, 194, .26f, Note);
            if (StepKey == "feeling") { Curve(accent, 2.3f, 89, 173, 112, 175, 139, 175, 160, 173); }
            if (StepKey == "breath") { Air(48, 99, .7f); Air(194, 175, .5f); }
            if (StepKey == "finish") At(202, 194, .27f, Leaf);
        }
        void AwarenessScene(int kind)
        {
            if (kind == 17 && Is("activity", "action"))
            {
                if (Chosen("물 마시기")) Cup(); else if (Chosen("쉬기")) Rest();
                else if (Chosen("창밖 보기")) Window();
                else { At(76, 115, .49f, Cup); At(180, 115, .49f, Window); At(128, 190, .35f, Rest); }
                return;
            }
            At(128, 131, .88f, Person);
            if (kind == 13 && Is("overview", "before", "after"))
            {
                TargetPad(43, 149, 23, 29); TargetPad(211, 149, 23, 29);
                Curve(A(accent, .66f), 2.2f, 30, 151, 38, 140, 46, 160, 56, 148);
                Curve(A(accent, .66f), 2.2f, 198, 151, 206, 140, 214, 160, 224, 148);
                if (Is("before", "after")) SoftFrame(StepKey == "before" ? 43 : 211, 149, 25, 31);
            }
            if (kind == 17 && StepKey == "overview") { At(48, 194, .23f, Cup); At(211, 194, .23f, Window); }
            if (Is("awareness", "overview")) At(205, 59, .26f, Cloud);
            if (StepKey == "breath") Air(50, 110, .88f);
            if (StepKey == "expand")
            {
                Curve(accent, 2.8f, 65, 78, 34, 111, 35, 166, 64, 196);
                Curve(accent, 2.8f, 192, 78, 222, 111, 222, 166, 193, 196);
            }
            if (Is("ready", "after")) Ground(72, 219, 184);
        }
        void EverydayScene()
        {
            if (Chosen("음료")) Cup();
            else if (Chosen("음악")) { At(128, 126, .92f, Ear); SoundExamples(); }
            else if (Chosen("다른 경험")) { At(128, 140, .85f, Hands); At(128, 95, .49f, Leaf); }
            else Window();
            if (StepKey == "observe" && !Chosen("음악"))
            { Curve(A(accent, .78f), 2.6f, 42, 153, 32, 125, 35, 97, 46, 85); Curve(A(accent, .55f), 2.2f, 210, 83, 219, 100, 221, 120, 215, 135); }
            if (StepKey == "notice") At(202, 200, .26f, Note);
        }
        void KindWordScene()
        {
            At(107, 148, .72f, Person); At(180, 80, .58f, Bubble); At(181, 76, .25f, Heart);
            if (StepKey == "breath") Air(41, 111, .63f);
            if (StepKey == "kindness") Arrow(181, 119, 178, 146, 146, 153);
            if (StepKey == "prepare") Ground(56, 224, 183);
        }
        void PostureScene()
        {
            Chair();
            if (StepKey == "adjust")
            { Arrow(57, 101, 42, 130, 61, 154); Arrow(163, 217, 184, 231, 199, 215); }
            if (StepKey == "contact") { Contact(86, 146, 15); Contact(157, 216, 19); }
            if (StepKey == "notice") At(207, 78, .26f, Note);
            if (StepKey == "overview") At(207, 181, .28f, Cushion);
        }
        void MountainScene()
        {
            bool sound = StepKey.StartsWith("sound-", StringComparison.Ordinal) || Chosen("짧은 소리 실습");
            if (sound)
            { At(116, 133, .9f, Ear); if (Is("sound-listen", "notice")) SoundExamples(); }
            else
            {
                Mountain();
                if (Is("overview", "mountain-shape")) At(192, 65, .28f, Sun);
                if (Is("mountain-weather", "mountain-change"))
                { At(184, 69, .4f, Cloud); At(177, 109, .13f, Drop); At(199, 120, .12f, Drop); }
                if (StepKey == "mountain-change") At(64, 64, .30f, Cloud);
                if (StepKey == "mountain-return") At(128, 190, .29f, Breath);
                if (StepKey == "mountain-prepare") Ground(50, 217, 210);
            }
            if (StepKey == "route") At(208, 195, .25f, Ear);
            if (StepKey == "notice") At(47, 193, .25f, Note);
        }
        void MovingScene()
        {
            bool seated = Chosen("앉아서 살피기");
            if (seated) Chair();
            else { At(94, 145, .75f, Shoe); At(167, 105, .67f, Shoe); Ground(50, 218, 210); }
            if (StepKey == "activity" && string.IsNullOrEmpty(Choice)) At(200, 199, .28f, Chair);
            if (StepKey == "contact")
            {
                if (seated) { Contact(86, 146, 15); Contact(157, 216, 19); }
                else { Contact(101, 181, 23); Contact(166, 137, 18); }
            }
            if (StepKey == "move")
            {
                if (seated) { Arrow(57, 101, 42, 130, 61, 154); Contact(157, 213, 15); }
                else { Arrow(44, 130, 45, 88, 83, 72); Curve(accent, 2.2f, 159, 196, 179, 198, 197, 188, 207, 173); }
            }
            if (StepKey == "notice") At(206, 68, .25f, Note);
        }
        void EatingScene()
        {
            Plate(); At(130, 128, .75f, Food);
            if (Is("observe", "overview"))
            { Curve(A(accent, .62f), 2.3f, 91, 69, 106, 63, 144, 63, 160, 71); }
            if (StepKey == "bite") { At(206, 77, .19f, Food); Arrow(176, 105, 204, 93, 205, 88); }
            if (StepKey == "sensation") At(202, 198, .25f, Note);
            if (StepKey == "prepare") Ground(58, 220, 198);
        }
        void ListeningScene()
        {
            if (Chosen("상대의 이야기"))
            { At(88, 153, .57f, Person); At(174, 151, .57f, Person); At(129, 66, .48f, Bubble); }
            else { At(126, 139, .94f, Book); At(198, 65, .40f, Speaker); }
            if (StepKey == "listen") Air(39, 111, .72f);
            if (StepKey == "transcript") At(125, 127, .62f, Note);
            if (StepKey == "notice") At(54, 188, .25f, Note);
        }
        void SpeakingScene()
        {
            At(127, 119, .96f, Bubble);
            if (Is("overview", "pause")) At(128, 112, .48f, Pause);
            else At(127, 112, .57f, SpeechLines);
            if (StepKey == "speak") { At(205, 188, .30f, Bubble); Arrow(150, 177, 178, 202, 194, 193); }
            if (StepKey == "check") At(196, 196, .25f, Leaf);
        }
        void HelpedScene()
        {
            if (StepKey == "sound" || Chosen("아직 없어요"))
            { At(112, 128, .92f, Ear); if (StepKey == "sound") SoundExamples(); }
            else if (Chosen("사람")) { At(82, 134, .58f, Person); At(174, 134, .58f, Person); At(128, 196, .35f, Hands); }
            else if (Chosen("물건")) Cup(); else if (Chosen("환경")) Window();
            else { At(128, 166, .92f, Hands); At(128, 88, .59f, Leaf); }
            if (StepKey == "check") At(204, 196, .26f, Note);
            if (StepKey == "help")
            { Curve(A(accent, .75f), 2.5f, 48, 119, 33, 148, 38, 185, 63, 201); Curve(A(accent, .75f), 2.5f, 210, 119, 225, 148, 220, 185, 195, 201); }
        }
        void RoutineScene()
        {
            string trigger = string.IsNullOrEmpty(contextTrigger) ? Choice : contextTrigger;
            TargetPad(69, 110, 43, 58); TargetPad(176, 148, 43, 67);
            if (trigger == "샤워 후") At(69, 108, .54f, Drop);
            else if (trigger == "점심 후" || trigger == "식사 후") At(69, 109, .54f, Plate);
            else At(69, 109, .54f, Bookmark);
            At(176, 148, .60f, Target);
            if (StepKey == "wait") At(127, 108, .23f, Pause);
            else Arrow(101, 107, 120, 90, 133, 121);
            Ground(42, 208, 211);
            if (StepKey == "trigger") At(192, 64, .25f, Note);
            if (StepKey == "practice") Curve(accent, 2.5f, 176, 200, 198, 201, 216, 186, 220, 167);
            if (StepKey == "notice") At(52, 192, .24f, Note);
        }
        void PlanningScene()
        {
            Calendar();
            Rounded(69, 104, 117, 88, 10, A(Paper, .94f), A(Paper, .90f));
            At(127, 148, .57f, Target);
            if (StepKey == "time" || StepKey == "review" && !contextCurrentReview)
            {
                string period = string.IsNullOrEmpty(contextPeriod) ? Choice : contextPeriod;
                if (period == "저녁") At(203, 202, .30f, Moon);
                else if (period == "아침" || period == "점심") At(203, 202, .30f, Sun);
                else At(202, 193, .25f, Bookmark);
            }
            if (StepKey == "next-activity") Arrow(80, 201, 125, 225, 170, 201);
        }

        void Person()
        {
            Ellipse(128, 221, 68, 5, A(accent, .09f));
            Fill(new Path(91, 175).Curve(75, 177, 60, 194, 65, 205).Curve(75, 219, 179, 222, 191, 205)
                .Curve(197, 192, 177, 176, 162, 174).Line(91, 175), Middle, Color.Lerp(accent, Ink, .18f));
            Fill(new Path(80, 191).Curve(102, 188, 125, 209, 166, 207).Curve(155, 219, 98, 212, 76, 202)
                .Curve(70, 198, 72, 193, 80, 191), Light, Middle);
            Fill(new Path(113, 102).Line(142, 102).Line(147, 119).Line(107, 119).Line(113, 102), Skin, SkinShade);
            Fill(new Path(107, 111).Curve(91, 115, 85, 132, 83, 153).Line(84, 183)
                .Curve(107, 194, 151, 194, 173, 183).Curve(175, 154, 173, 124, 147, 112)
                .Curve(136, 123, 119, 123, 107, 111), Light, Middle);
            Curve(A(Paper, .6f), 1.7f, 107, 130, 103, 146, 104, 166, 108, 178);
            Curve(A(accent, .7f), 1.3f, 111, 113, 123, 127, 135, 127, 145, 113);
            Fill(new Path(93, 124).Curve(82, 134, 76, 158, 81, 174).Line(95, 170)
                .Curve(90, 156, 103, 134, 93, 124), Light, Middle);
            Fill(new Path(162, 124).Curve(173, 134, 180, 158, 174, 174).Line(160, 170)
                .Curve(165, 156, 153, 134, 162, 124), Light, Middle);
            Ellipse(92, 175, 13, 6, Skin); Ellipse(162, 175, 13, 6, Skin);
            Curve(A(SkinShade, .7f), 1.2f, 85, 175, 90, 178, 96, 178, 101, 176);
            Curve(A(SkinShade, .7f), 1.2f, 153, 176, 158, 178, 164, 178, 169, 175);
            Curve(A(Paper, .34f), 1.3f, 146, 141, 151, 154, 152, 171, 149, 183);
            Curve(A(Paper, .5f), 1.2f, 86, 197, 108, 201, 127, 214, 153, 212);
            Face();
        }
        void Face()
        {
            Ellipse(103, 77, 5, 8, SkinShade); Ellipse(153, 77, 5, 8, SkinShade);
            Fill(new Path(105, 58).Curve(106, 41, 119, 34, 132, 36).Curve(149, 37, 155, 51, 153, 70)
                .Line(151, 87).Curve(148, 101, 139, 109, 127, 108).Curve(114, 106, 106, 96, 105, 81)
                .Line(105, 58), C(244, 216, 186), Skin);
            Fill(new Path(103, 72).Curve(96, 57, 100, 40, 113, 32).Curve(129, 24, 149, 31, 156, 46)
                .Curve(161, 56, 158, 68, 153, 75).Line(148, 64).Curve(147, 55, 144, 51, 139, 48)
                .Curve(130, 55, 117, 57, 108, 54).Line(107, 73).Line(103, 72), C(119, 107, 91), C(83, 84, 76));
            Curve(A(C(165, 148, 122), .5f), 1.2f, 110, 43, 119, 34, 133, 32, 145, 39);
            Curve(Ink, 1.4f, 112, 76, 116, 79, 120, 79, 123, 76);
            Curve(Ink, 1.4f, 135, 76, 139, 79, 143, 79, 146, 76);
            Curve(C(191, 143, 118), 1.1f, 129, 78, 128, 82, 127, 86, 132, 86);
            Curve(C(158, 114, 99), 1.2f, 123, 94, 127, 97, 133, 97, 138, 92);
            Ellipse(113, 86, 4, 2, A(C(214, 149, 129), .22f));
            Ellipse(143, 86, 4, 2, A(C(214, 149, 129), .22f));
        }
        void Chair()
        {
            Ground(48, 222, 211);
            Rounded(64, 81, 27, 84, 10, C(191, 174, 146), Wood);
            Rounded(71, 153, 92, 17, 7, C(189, 174, 146), Wood);
            Rounded(73, 165, 10, 55, 4, Wood, C(143, 122, 102));
            Rounded(147, 165, 10, 55, 4, Wood, C(143, 122, 102));
            Curve(A(Paper, .42f), 1.4f, 69, 91, 70, 108, 70, 131, 70, 148);
            Curve(A(Paper, .38f), 1.3f, 78, 158, 101, 160, 132, 160, 153, 157);
            Rounded(118, 91, 18, 23, 6, Skin, SkinShade);
            Fill(new Path(104, 105).Curve(90, 111, 88, 132, 88, 149).Curve(104, 155, 124, 155, 139, 151)
                .Line(142, 126).Curve(138, 110, 125, 105, 104, 105), Light, Middle);
            // A quieter far leg and shoe keep the seated silhouette human, not a single block.
            Fill(new Path(104, 153).Line(139, 156).Curve(146, 172, 142, 193, 139, 209)
                .Line(123, 209).Line(122, 174).Line(104, 169).Line(104, 153), Color.Lerp(Middle, Paper, .12f), Middle);
            Fill(new Path(123, 205).Line(139, 205).Curve(145, 209, 156, 214, 153, 217)
                .Curve(146, 221, 127, 220, 119, 216).Line(123, 205), C(207, 203, 180), C(170, 168, 149));
            Fill(new Path(89, 148).Curve(106, 148, 136, 146, 154, 154).Curve(163, 167, 163, 188, 163, 207)
                .Line(145, 207).Line(137, 171).Curve(115, 172, 97, 168, 90, 164).Line(89, 148), Middle, accent);
            Fill(new Path(145, 204).Line(164, 204).Curve(169, 209, 182, 211, 184, 216)
                .Curve(180, 222, 154, 221, 144, 217).Line(145, 204), C(224, 216, 194), C(179, 172, 151));
            Curve(A(Paper, .55f), 1.5f, 98, 116, 95, 128, 96, 140, 101, 145);
            Curve(Skin, 9, 128, 128, 129, 140, 143, 143, 151, 145);
            Ellipse(152, 146, 7, 4, Skin);
            Curve(A(SkinShade, .72f), 1.1f, 148, 146, 151, 148, 155, 148, 159, 146);
            Curve(A(Paper, .35f), 1.4f, 101, 158, 116, 158, 131, 158, 142, 163);
            Curve(A(Paper, .32f), 1.3f, 153, 173, 155, 182, 155, 193, 155, 201);
            At(126, 113, .72f, Face);
        }
        void Ear() { ListeningPortrait(); }
        void ListeningPortrait(bool listening = true)
        {
            Ellipse(128, 224, 61, 5, A(accent, .08f));
            Fill(new Path(110, 128).Curve(87, 136, 78, 162, 75, 216).Curve(104, 230, 160, 230, 184, 216)
                .Curve(181, 165, 171, 137, 147, 129).Curve(137, 143, 121, 143, 110, 128), Light, Middle);
            Rounded(115, 112, 28, 30, 10, Skin, SkinShade);
            // Face already draws a matching pair of small ears. Listening uses
            // the surrounding sound cues, not an extra enlarged ear overlay.
            At(128, 139, 1.18f, Face);
            Curve(A(Paper, .42f), 1.7f, 104, 156, 98, 174, 99, 194, 104, 207);
            if (listening)
            {
                // A one-sided sound cue distinguishes listening from the chest
                // breathing symbol without suggesting a required hand gesture.
                Curve(A(accent, .82f), 2.8f, 67, 83, 57, 95, 57, 112, 66, 124);
                Curve(A(accent, .48f), 2.1f, 55, 75, 40, 93, 41, 116, 54, 132);
            }
        }
        void Breath()
        {
            ListeningPortrait(false);
            Ellipse(128, 170, 35, 23, A(Paper, .30f));
            Air(121, 165, 1.12f);
        }
        void Rest() { Cushion(); At(128, 126, .83f, Person); }
        void Cushion()
        {
            Fill(new Path(49, 198).Curve(71, 188, 187, 188, 209, 199).Curve(219, 213, 197, 226, 178, 227)
                .Curve(138, 233, 82, 228, 62, 222).Curve(47, 217, 41, 207, 49, 198), C(211, 196, 168), C(176, 160, 136));
            Curve(A(Paper, .65f), 1.5f, 58, 204, 95, 214, 165, 215, 201, 205);
        }
        void Cup()
        {
            Ellipse(129, 208, 68, 10, A(accent, .11f));
            Ellipse(128, 198, 67, 10, C(225, 230, 216));
            Curve(Middle, 13, 172, 91, 222, 83, 221, 153, 170, 149);
            Fill(new Path(75, 76).Curve(102, 68, 151, 68, 178, 76).Line(172, 170)
                .Curve(170, 198, 84, 199, 81, 171).Line(75, 76), Light, Middle);
            Ellipse(127, 77, 51, 11, Paper); Ellipse(127, 78, 43, 7, C(150, 187, 186));
            Curve(A(Paper, .75f), 1.6f, 94, 78, 112, 72, 146, 73, 160, 79);
            Curve(A(Paper, .62f), 3, 90, 96, 91, 125, 91, 151, 96, 167);
            Curve(A(accent, .3f), 1.4f, 105, 183, 126, 190, 151, 186, 161, 177);
            Curve(A(Paper, .35f), 1.8f, 184, 97, 205, 100, 206, 132, 183, 139);
        }
        void Hands()
        {
            Fill(new Path(46, 135).Curve(60, 126, 72, 117, 81, 112).Curve(87, 108, 94, 111, 91, 117)
                .Line(78, 130).Curve(94, 127, 107, 128, 115, 134).Curve(122, 142, 115, 151, 104, 158)
                .Curve(85, 168, 71, 171, 60, 169).Line(46, 135), C(245, 214, 183), SkinShade);
            Fill(new Path(210, 135).Curve(196, 126, 184, 117, 175, 112).Curve(169, 108, 162, 111, 165, 117)
                .Line(178, 130).Curve(162, 127, 149, 128, 141, 134).Curve(134, 142, 141, 151, 152, 158)
                .Curve(171, 168, 185, 171, 196, 169).Line(210, 135), C(245, 214, 183), SkinShade);
            Curve(A(C(170, 120, 101), .55f), 1.2f, 76, 139, 88, 139, 96, 142, 103, 146);
            Curve(A(C(170, 120, 101), .55f), 1.2f, 180, 139, 168, 139, 160, 142, 153, 146);
            Curve(A(SkinShade, .72f), 1.2f, 72, 149, 82, 150, 90, 153, 97, 154);
            Curve(A(SkinShade, .72f), 1.2f, 184, 149, 174, 150, 166, 153, 159, 154);
            Curve(A(Paper, .48f), 1.8f, 55, 138, 61, 134, 70, 128, 77, 124);
            Curve(A(Paper, .48f), 1.8f, 201, 138, 195, 134, 186, 128, 179, 124);
        }
        void Faucet()
        {
            Rounded(64, 39, 20, 50, 6, C(195, 207, 205), C(148, 170, 173));
            Fill(new Path(75, 43).Line(127, 43).Curve(146, 43, 157, 55, 157, 72).Line(157, 88)
                .Line(135, 88).Line(135, 72).Curve(135, 67, 131, 64, 126, 64).Line(75, 64).Line(75, 43), C(205, 217, 214), C(149, 175, 179));
            Rounded(57, 35, 35, 10, 4, C(155, 176, 178), C(128, 156, 163));
            Curve(A(Paper, .8f), 2, 86, 49, 115, 47, 141, 48, 145, 62);
            Ellipse(145, 88, 11, 3, C(112, 143, 153));
        }
        void Drop()
        {
            Fill(new Path(128, 48).Curve(117, 76, 80, 115, 79, 149).Curve(77, 187, 103, 209, 128, 210)
                .Curve(158, 210, 181, 184, 176, 148).Curve(170, 111, 141, 77, 128, 48), C(178, 210, 214), C(99, 159, 178));
            Curve(A(Paper, .65f), 5, 103, 117, 86, 149, 94, 171, 108, 182);
        }
        void Plate()
        {
            Ellipse(128, 180, 82, 25, A(accent, .08f));
            Ellipse(128, 156, 82, 49, C(202, 215, 211));
            Ellipse(128, 150, 79, 46, C(250, 248, 235));
            Ellipse(128, 151, 63, 33, C(224, 234, 225));
            Curve(A(accent, .35f), 1.5f, 66, 168, 94, 190, 159, 192, 189, 169);
            Curve(A(Paper, .80f), 1.8f, 62, 138, 83, 116, 112, 112, 145, 116);
        }
        void Food()
        {
            Fill(new Path(75, 117).Curve(86, 84, 156, 79, 177, 110).Line(174, 155)
                .Curve(147, 169, 102, 169, 78, 152).Line(75, 117), C(220, 168, 108), C(184, 125, 72));
            Fill(new Path(79, 111).Curve(97, 88, 150, 86, 174, 110).Curve(154, 136, 100, 135, 79, 111), C(245, 218, 162), C(226, 182, 119));
            Curve(A(Paper, .65f), 2, 100, 106, 113, 97, 137, 96, 151, 104);
            Curve(A(C(166, 111, 65), .48f), 2, 112, 96, 111, 103, 108, 110, 104, 114);
            Curve(A(C(166, 111, 65), .48f), 2, 137, 94, 138, 102, 135, 111, 131, 116);
            At(173, 145, .25f, Leaf);
        }
        void Window()
        {
            Rounded(47, 40, 162, 172, 13, C(203, 190, 164), C(162, 148, 126));
            Rounded(57, 50, 142, 150, 7, C(219, 234, 230), C(158, 191, 194));
            Ellipse(164, 86, 21, 21, C(239, 217, 154));
            Fill(new Path(57, 175).Curve(91, 137, 120, 144, 143, 166).Curve(161, 148, 185, 147, 199, 169)
                .Line(199, 199).Line(57, 199).Line(57, 175), C(166, 190, 153), C(128, 163, 143));
            Rounded(123, 50, 9, 150, 2, C(225, 216, 193), C(180, 169, 145));
            Rounded(57, 124, 142, 8, 2, C(225, 216, 193), C(180, 169, 145));
            Rounded(40, 204, 177, 12, 4, C(214, 201, 174), Wood);
            Curve(A(Paper, .6f), 2, 67, 63, 66, 79, 66, 98, 67, 110);
            Curve(A(Paper, .30f), 1.4f, 76, 61, 76, 72, 76, 88, 76, 96);
            Curve(A(C(95, 139, 121), .48f), 1.4f, 65, 186, 82, 173, 102, 170, 115, 174);
        }
        void Mountain()
        {
            Ellipse(128, 218, 94, 6, A(accent, .10f));
            Fill(new Path(31, 211).Line(86, 108).Curve(92, 101, 97, 103, 102, 110).Line(156, 211)
                .Line(31, 211), C(177, 197, 187), C(118, 155, 148));
            Fill(new Path(66, 215).Line(119, 67).Curve(123, 56, 131, 55, 136, 66).Line(221, 216)
                .Curve(174, 226, 117, 225, 66, 215), C(148, 177, 167), C(88, 130, 124));
            Fill(new Path(120, 67).Curve(124, 57, 131, 57, 136, 67).Line(160, 110).Line(140, 102)
                .Line(130, 111).Line(118, 99).Line(103, 113).Line(120, 67), C(236, 240, 226), C(208, 223, 211));
            Fill(new Path(135, 69).Line(221, 216).Curve(190, 221, 172, 222, 153, 221).Line(135, 69), A(C(82, 122, 117), .38f), A(C(69, 112, 108), .4f));
            Curve(A(Paper, .30f), 1.7f, 115, 122, 104, 150, 95, 175, 87, 197);
            Curve(A(C(61, 105, 101), .30f), 1.6f, 148, 129, 161, 153, 166, 177, 174, 204);
            At(48, 197, .24f, Leaf); At(203, 200, .22f, Leaf);
        }
        void Shoe()
        {
            Fill(new Path(73, 98).Line(117, 88).Curve(129, 107, 152, 124, 181, 131)
                .Curve(205, 137, 212, 155, 205, 170).Curve(173, 182, 95, 181, 66, 166)
                .Curve(65, 143, 69, 118, 73, 98), Light, Middle);
            Fill(new Path(66, 162).Curve(107, 176, 172, 176, 207, 164).Line(207, 178)
                .Curve(163, 189, 99, 188, 66, 174).Line(66, 162), C(233, 223, 198), C(182, 170, 146));
            Curve(Paper, 3, 116, 111, 123, 112, 131, 112, 137, 110);
            Curve(Paper, 3, 126, 122, 134, 123, 143, 123, 148, 121);
            Curve(Paper, 3, 139, 132, 145, 133, 154, 133, 160, 131);
            Curve(A(Paper, .4f), 1.5f, 80, 113, 75, 130, 76, 144, 82, 154);
            Curve(A(accent, .52f), 2, 77, 101, 91, 104, 105, 101, 117, 95);
            Curve(A(accent, .34f), 1.4f, 146, 146, 165, 143, 187, 146, 196, 155);
            Curve(A(Paper, .72f), 1.5f, 82, 174, 115, 181, 166, 182, 194, 174);
        }
        void Bubble()
        {
            Fill(new Path(62, 60).Curve(41, 61, 36, 73, 37, 92).Line(39, 146)
                .Curve(40, 165, 53, 174, 78, 175).Line(91, 175).Line(84, 198).Line(118, 177)
                .Line(181, 174).Curve(206, 172, 219, 158, 219, 139).Line(218, 90)
                .Curve(218, 68, 204, 57, 180, 57).Line(62, 60), Paper, Light);
            Curve(A(Paper, .9f), 2, 56, 89, 56, 75, 68, 70, 87, 70);
        }
        void Heart()
        {
            Fill(new Path(128, 89).Curve(99, 45, 53, 64, 55, 103).Curve(56, 138, 103, 181, 128, 202)
                .Curve(156, 179, 202, 138, 202, 102).Curve(201, 63, 157, 46, 128, 89), C(223, 176, 163), C(185, 129, 120));
            Curve(A(Paper, .6f), 3, 78, 96, 78, 80, 95, 79, 107, 87);
        }
        void Cloud()
        {
            Fill(new Path(56, 109).Curve(43, 76, 73, 56, 100, 68).Curve(114, 39, 155, 44, 166, 73)
                .Curve(196, 64, 215, 87, 207, 113).Curve(223, 135, 206, 159, 183, 160)
                .Line(77, 162).Curve(48, 160, 35, 133, 56, 109), Color.Lerp(Light, Paper, .3f), Middle);
            Curve(A(Paper, .65f), 2, 72, 91, 76, 78, 90, 77, 101, 82);
            Ellipse(92, 181, 9, 7, Light); Ellipse(77, 199, 4, 4, Middle);
        }
        void Leaf()
        {
            Fill(new Path(128, 207).Curve(68, 183, 64, 110, 126, 48).Curve(188, 105, 190, 178, 128, 207), C(175, 194, 148), C(112, 151, 124));
            Curve(C(101, 137, 111), 2.7f, 128, 207, 130, 165, 126, 112, 126, 71);
            Curve(A(Paper, .45f), 1.6f, 125, 145, 109, 133, 99, 121, 95, 107);
            Curve(A(Paper, .45f), 1.6f, 129, 167, 145, 151, 155, 137, 160, 121);
        }
        void Note()
        {
            Rounded(60, 47, 136, 169, 13, Paper, C(221, 224, 204));
            Rounded(91, 39, 74, 20, 7, Light, Middle);
            Curve(accent, 3, 81, 86, 104, 86, 147, 86, 174, 86);
            Curve(A(accent, .8f), 2.6f, 81, 112, 104, 112, 147, 112, 174, 112);
            Curve(A(accent, .65f), 2.6f, 81, 138, 104, 138, 147, 138, 174, 138);
            Curve(A(accent, .5f), 2.6f, 81, 164, 104, 164, 126, 164, 144, 164);
        }
        void Book()
        {
            Fill(new Path(40, 73).Curve(74, 64, 104, 67, 128, 80).Curve(156, 64, 187, 67, 218, 80)
                .Line(211, 202).Curve(177, 187, 151, 188, 128, 202).Curve(102, 187, 73, 186, 38, 196)
                .Line(40, 73), Middle, Color.Lerp(accent, Wood, .45f));
            Fill(new Path(45, 67).Curve(75, 57, 107, 61, 128, 73).Curve(153, 59, 183, 61, 213, 73)
                .Line(205, 194).Curve(176, 181, 150, 181, 128, 194).Curve(104, 181, 73, 179, 44, 187)
                .Line(45, 67), Paper, C(214, 226, 215));
            Curve(accent, 2.7f, 128, 76, 128, 111, 128, 155, 128, 190);
            Curve(A(Paper, .8f), 1.4f, 51, 181, 79, 174, 104, 179, 122, 187);
            Fill(new Path(178, 66).Line(189, 69).Line(186, 115).Line(181, 109).Line(175, 112).Line(178, 66), Middle, accent);
            for (int i = 0; i < 4; i++)
            {
                float y = 91 + i * 21;
                Curve(A(accent, .65f), 2, 62, y, 81, y - 4, 102, y - 1, 115, y + 5);
                Curve(A(accent, .65f), 2, 141, y + 5, 158, y - 2, 179, y, 195, y + 5);
            }
        }
        void Calendar()
        {
            Rounded(43, 52, 170, 165, 15, A(Wood, .13f), A(Wood, .08f));
            Rounded(45, 47, 166, 166, 14, Paper, C(218, 228, 213));
            Rounded(45, 47, 166, 44, 13, Light, Middle);
            Curve(accent, 6, 77, 42, 73, 48, 73, 57, 77, 63);
            Curve(accent, 6, 179, 42, 183, 48, 183, 57, 179, 63);
            for (int row = 0; row < 3; row++) for (int col = 0; col < 7; col++)
                Rounded(58 + col * 21, 106 + row * 29, 14, 17, 3, A(Light, .65f), A(Middle, .3f));
        }
        void Frequency() { Calendar(); At(128, 140, .50f, Bookmark); }
        void Bookmark()
        {
            Fill(new Path(83, 47).Line(174, 47).Line(174, 213).Line(128, 180).Line(83, 213).Line(83, 47), Light, Middle);
            Curve(A(Paper, .65f), 3, 99, 64, 97, 95, 97, 121, 98, 148);
        }
        void Pause()
        {
            Rounded(77, 58, 35, 144, 13, Light, Middle);
            Rounded(144, 58, 35, 144, 13, Light, Middle);
        }
        void Speaker()
        {
            Ellipse(128, 128, 100, 100, A(Paper, .90f));
            Fill(new Path(60, 108).Line(89, 108).Line(128, 74).Line(128, 183).Line(89, 149).Line(60, 149).Line(60, 108), Middle, accent);
            Curve(accent, 6, 151, 101, 170, 117, 170, 141, 151, 157);
            Curve(A(accent, .65f), 4.5f, 170, 80, 204, 106, 204, 151, 170, 178);
        }
        void SpeechLines()
        {
            Curve(accent, 7, 69, 96, 95, 96, 151, 96, 187, 96);
            Curve(A(accent, .8f), 6, 69, 126, 95, 126, 139, 126, 166, 126);
            Curve(A(accent, .6f), 5, 69, 156, 88, 156, 112, 156, 137, 156);
        }
        void Sun()
        {
            Ellipse(128, 128, 45, 45, C(236, 208, 143));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4; var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var a = new Vector2(128, 128) + direction * 61; var b = new Vector2(128, 128) + direction * 76;
                Curve(C(212, 181, 117), 4, a.x, a.y, a.x, a.y, b.x, b.y, b.x, b.y);
            }
        }
        void Moon()
        {
            Fill(new Path(160, 53).Curve(94, 45, 57, 103, 76, 156).Curve(95, 207, 155, 220, 194, 177)
                .Curve(140, 188, 102, 150, 111, 109).Curve(117, 83, 137, 64, 160, 53), C(232, 225, 184), C(195, 200, 168));
        }
        void Rounded(float x, float y, float w, float h, float r, Color top, Color bottom)
            => Fill(new Path(x+r, y).Line(x+w-r, y).Curve(x+w, y, x+w, y, x+w, y+r)
                .Line(x+w, y+h-r).Curve(x+w, y+h, x+w, y+h, x+w-r, y+h)
                .Line(x+r, y+h).Curve(x, y+h, x, y+h, x, y+h-r).Line(x, y+r)
                .Curve(x, y, x, y, x+r, y), top, bottom);
        void Ground(float x, float y, float end)
        { Ellipse((x+end)/2, y+2, (end-x)/2, 3, A(accent, .08f)); Curve(A(accent, .35f), 1.5f, x, y, x+20, y, end-20, y, end, y); }
        void Contact(float x, float y, float radius)
        {
            Ellipse(x, y+1, radius+4, 7, A(Paper, .64f));
            Curve(accent, 2.7f, x-radius, y, x-4, y+5, x+4, y+5, x+radius, y);
            Curve(A(accent, .43f), 1.9f, x-radius-3, y+5, x-4, y+11, x+4, y+11, x+radius+3, y+5);
        }
        void TargetPad(float x, float y, float rx, float ry)
        { Ellipse(x, y+2, rx, ry, A(accent, .07f)); Ellipse(x, y, rx-2, ry-2, A(Paper, .64f)); }
        void SoftFrame(float x, float y, float rx, float ry)
        {
            var tint = A(accent, .7f);
            Curve(tint, 2.4f, x, y-ry, x+rx, y-ry, x+rx, y+ry, x, y+ry);
            Curve(tint, 2.4f, x, y+ry, x-rx, y+ry, x-rx, y-ry, x, y-ry);
        }
        void Air(float x, float y, float scale)
        {
            Curve(A(accent, .9f), 2.5f, x-16*scale, y, x-4*scale, y-13*scale, x+12*scale, y+10*scale, x+24*scale, y-4*scale);
            Curve(A(accent, .65f), 2, x-17*scale, y+15*scale, x-4*scale, y+3*scale, x+14*scale, y+25*scale, x+25*scale, y+10*scale);
        }
        void Steam(float x, float y)
        {
            Curve(A(accent, .55f), 2.4f, x-12, y+10, x-23, y-4, x-2, y-13, x-10, y-25);
            Curve(A(accent, .4f), 2, x+10, y+12, x, y-2, x+21, y-10, x+13, y-23);
        }
        void SoundExamples()
        {
            SoundWave(66, 12, 3.8f); SoundWave(102, 8, 2.7f); SoundWave(138, 4.5f, 1.8f);
        }
        void SoundWave(float y, float amplitude, float width)
        {
            float bend = amplitude*4/3;
            Curve(accent, width, 185, y, 193, y-bend, 203, y-bend, 211, y);
            Curve(accent, width, 211, y, 219, y+bend, 229, y+bend, 237, y);
        }
        void Arrow(float x, float y, float cx, float cy, float ex, float ey)
        {
            // A narrow paper keyline separates the direction cue from colored
            // objects. Rounded joins remain legible at the smallest phone scale.
            Curve(A(Paper, .82f), 5.4f, x, y, cx, cy, cx, cy, ex, ey);
            Curve(accent, 3.1f, x, y, cx, cy, cx, cy, ex, ey);
            var direction = (new Vector2(ex, ey) - new Vector2(cx, cy)).normalized;
            var normal = new Vector2(-direction.y, direction.x); var tip = new Vector2(ex, ey);
            var a = tip - direction*9.5f + normal*4.8f; var b = tip - direction*9.5f - normal*4.8f;
            Curve(A(Paper, .82f), 5.4f, a.x, a.y, a.x, a.y, ex, ey, ex, ey);
            Curve(A(Paper, .82f), 5.4f, b.x, b.y, b.x, b.y, ex, ey, ex, ey);
            Curve(accent, 3.1f, a.x, a.y, a.x, a.y, ex, ey, ex, ey);
            Curve(accent, 3.1f, b.x, b.y, b.x, b.y, ex, ey, ex, ey);
        }
        sealed class Path
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public Path(float x, float y) { Points.Add(new Vector2(x, y)); }
            public Path Line(float x, float y) { Points.Add(new Vector2(x, y)); return this; }
            public Path Curve(float ax, float ay, float bx, float by, float x, float y)
            {
                var start = Points[Points.Count - 1]; var a = new Vector2(ax, ay); var b = new Vector2(bx, by); var end = new Vector2(x, y);
                for (int i = 1; i <= 12; i++) { float t = i / 12f, s = 1 - t; Points.Add(s * s * s * start + 3 * s * s * t * a + 3 * s * t * t * b + t * t * t * end); }
                return this;
            }
        }
        void Ellipse(float x, float y, float rx, float ry, Color tint)
        {
            var points = new List<Vector2>(48);
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI / 24; points.Add(new Vector2(x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry)); }
            Polygon(points, tint, tint);
        }
        void Curve(Color tint, float width, float x, float y, float ax, float ay, float bx, float by, float ex, float ey)
        {
            var points = new Path(x, y).Curve(ax, ay, bx, by, ex, ey).Points;
            var outline = new List<Vector2>(points.Count * 2 + 16); float radius = width / 2;
            for (int side = 1; side >= -1; side -= 2)
            {
                for (int j = 0; j < points.Count; j++)
                {
                    int i = side > 0 ? j : points.Count - 1 - j;
                    var direction = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                    outline.Add(points[i] + new Vector2(-direction.y, direction.x) * (radius * side));
                }
                int index = side > 0 ? points.Count - 1 : 0;
                var tangent = side > 0 ? points[index] - points[index - 1] : points[0] - points[1];
                float angle = Mathf.Atan2(tangent.y, tangent.x) + Mathf.PI / 2;
                for (int i = 1; i < 8; i++) { float a = angle - i * Mathf.PI / 8; outline.Add(points[index] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius); }
            }
            Polygon(outline, tint, tint);
        }
        void Fill(Path path, Color top, Color bottom) => Polygon(path.Points, top, bottom);
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        void Polygon(List<Vector2> points, Color top, Color bottom)
        {
            if (points.Count > 2 && (points[0] - points[points.Count - 1]).sqrMagnitude < .0001f) points.RemoveAt(points.Count - 1);
            int count = points.Count; if (count < 3) return;
            float area = 0, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < count; i++) { area += Cross(points[i], points[(i + 1) % count]); minY = Mathf.Min(minY, points[i].y); maxY = Mathf.Max(maxY, points[i].y); }
            if (Mathf.Abs(area) < .0001f) return;
            float sign = Mathf.Sign(area); int start = mesh.currentVertCount;
            for (int i = 0; i < count; i++) Vertex(points[i], Color.Lerp(top, bottom, Mathf.InverseLerp(minY, maxY, points[i].y)));
            var remaining = new List<int>(count); for (int i = 0; i < count; i++) remaining.Add(i);
            while (remaining.Count > 2)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(points[b] - points[a], points[c] - points[b]) * sign <= .00001f) continue;
                    bool inside = false;
                    foreach (int p in remaining)
                        if (p != a && p != b && p != c && Cross(points[b] - points[a], points[p] - points[a]) * sign >= -.00001f
                            && Cross(points[c] - points[b], points[p] - points[b]) * sign >= -.00001f
                            && Cross(points[a] - points[c], points[p] - points[c]) * sign >= -.00001f) { inside = true; break; }
                    if (inside) continue;
                    mesh.AddTriangle(start + a, start + b, start + c); remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            // An outward transparent fringe antialiases curves without any texture.
            int fringe = mesh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                var before = (points[i] - points[(i + count - 1) % count]).normalized;
                var after = (points[(i + 1) % count] - points[i]).normalized;
                var n1 = new Vector2(before.y, -before.x) * sign; var n2 = new Vector2(after.y, -after.x) * sign;
                var normal = (n1 + n2).normalized;
                float distance = Mathf.Min(antialias * 2, antialias / Mathf.Max(.5f, Vector2.Dot(normal, n2)));
                Vertex(points[i] + normal * distance, A(Color.Lerp(top, bottom, Mathf.InverseLerp(minY, maxY, points[i].y)), 0));
            }
            for (int i = 0; i < count; i++) { int next = (i + 1) % count; mesh.AddTriangle(start + i, fringe + i, fringe + next); mesh.AddTriangle(start + i, fringe + next, start + next); }
        }
        void Vertex(Vector2 point, Color tint)
        {
            point = modelCenter + (point - new Vector2(128, 128)) * modelScale;
            mesh.AddVert(new Vector3(drawingRect.center.x + (point.x - 128) * unit, drawingRect.center.y + (128 - point.y) * unit), tint * color, Vector2.zero);
        }
        static Color C(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        static Color A(Color value, float alpha) { value.a = alpha; return value; }
    }
}
