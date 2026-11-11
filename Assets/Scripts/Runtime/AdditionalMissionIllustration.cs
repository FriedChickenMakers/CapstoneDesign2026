using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // UI illustration only: scenes explain a real-world action without measuring
    // its completion, suggesting a clinical outcome, or owning mission state.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class AdditionalMissionIllustration : MaskableGraphic
    {
        [SerializeField] string missionCode = "M02", stage = "overview", choice = "";
        [SerializeField] Color accent = new Color(.3f, .5f, .5f);
        public string Code => missionCode;
        public string Stage => stage;
        public string Choice => choice;
        static readonly Color Paper = C(249, 245, 231), Cream = C(232, 224, 201);
        static readonly Color Sage = C(139, 165, 138), Green = C(83, 124, 96);
        static readonly Color Blue = C(125, 177, 190), DeepBlue = C(66, 119, 142);
        static readonly Color Wood = C(164, 137, 104), Ink = C(76, 84, 81);
        static readonly Color Skin = C(239, 207, 178), SkinShade = C(210, 168, 144);
        static readonly Color Gold = C(219, 187, 115), Rose = C(191, 139, 151);
        VertexHelper mesh;
        Rect drawingRect;
        float unit, antialias, modelScale = 1;
        float breathPhase, breathMotion;
        Vector2 modelCenter = new Vector2(128, 128);

        public void Configure(string code, string stage, string choice, Color accent)
        {
            code = code ?? ""; stage = stage ?? "overview"; choice = choice ?? "";
            if (missionCode == code && this.stage == stage && this.choice == choice && this.accent == accent) return;
            missionCode = code; this.stage = stage; this.choice = choice; this.accent = accent;
            breathPhase = breathMotion = 0;
            SetVerticesDirty();
        }
        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }
        void Update()
        {
            // Only the surrounding rings move. The person, icon hitbox and text
            // remain anchored. This is a gentle optional cue, not a breathing target.
            if (!Application.isPlaying || missionCode != "M17" || !Is("breath")) return;
            if (FigmaMissionUi.ReducedMotion)
            {
                if (Mathf.Abs(breathMotion) > .001f) { breathPhase = breathMotion = 0; SetVerticesDirty(); }
                return;
            }
            breathPhase += Time.unscaledDeltaTime * Mathf.PI / 4;
            float next = Mathf.Sin(breathPhase) * 3f;
            // Avoid rebuilding the illustration for visually identical subpixel changes.
            if (Mathf.Abs(next - breathMotion) < .08f) return;
            breathMotion = next; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            // All shapes, emphasis and arrows come exclusively from this frame's
            // stage. Back/help cannot leave a prior stage's decoration behind.
            helper.Clear(); drawingRect = GetPixelAdjustedRect();
            unit = Mathf.Min(drawingRect.width, drawingRect.height) / 232f;
            if (unit <= 0) return;
            mesh = helper; modelScale = 1; modelCenter = new Vector2(128, 128);
            antialias = .85f / Mathf.Max(.01f, unit * (canvas == null ? 1 : canvas.scaleFactor));
            switch (missionCode)
            {
                case "M02": Window(); break;
                case "M03": Water(); break;
                case "M04": Wash(); break;
                case "M05": Stretch(); break;
                case "M06": Tidy(); break;
                case "M07": Feeling(); break;
                case "M08": Music(); break;
                case "M09": Rest(); break;
                case "M10": Plan(); break;
                case "M11": Drawing(); break;
                case "M12": Plant(); break;
                case "M13": Memory(); break;
                case "M14": Reading(); break;
                case "M15": Walking(); break;
                case "M16": Park(); break;
                case "M17": Breathing(); break;
            }
            if (stage == "guide-help") At(197, 208, .37f, OpenBook);
            mesh = null;
        }
        bool Is(string value) => stage == value;
        Color Tint => Color.Lerp(accent, Paper, .18f);
        void Shadow(float x = 128, float y = 226, float radius = 82) => Ellipse(x, y, radius, 7, A(Ink, .07f));
        void Line(Color tint, float width, float x, float y, float endX, float endY)
            => Curve(tint, width, x, y, x, y, endX, endY, endX, endY);
        void Ring(float x, float y, float radius, Color tint, float width = 2)
        {
            // One continuous annulus: translucent quarter-arcs used to double
            // their round caps at the cardinal points and leave four dark dots.
            const int segments = 72;
            int start = mesh.currentVertCount;
            float inner = Mathf.Max(0, radius-width*.5f), outer = radius+width*.5f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var center = new Vector2(x,y);
                Vertex(center+direction*Mathf.Max(0,inner-antialias), A(tint,0));
                Vertex(center+direction*inner,tint); Vertex(center+direction*outer,tint);
                Vertex(center+direction*(outer+antialias),A(tint,0));
            }
            for (int i = 0; i < segments; i++)
            for (int band = 0; band < 3; band++)
            {
                int a=start+i*4+band, b=start+(i+1)%segments*4+band;
                mesh.AddTriangle(a,b,b+1); mesh.AddTriangle(a,b+1,a+1);
            }
        }
        void Leaf(float x, float y, float tipX, float tipY, Color tint)
        {
            var d = new Vector2(tipX-x, tipY-y); var n = new Vector2(-d.y, d.x) * .3f;
            Fill(new Path(x,y).Curve(x+d.x*.35f+n.x,y+d.y*.35f+n.y,tipX+n.x*.65f,tipY+n.y*.65f,tipX,tipY)
                .Curve(tipX-n.x*.65f,tipY-n.y*.65f,x+d.x*.35f-n.x,y+d.y*.35f-n.y,x,y), Color.Lerp(tint,Paper,.22f),tint);
            Line(A(Paper,.5f),1.2f,x,y,tipX,tipY);
            // Short secondary veins stop inside the leaf, never at its silhouette.
            var mid = new Vector2(x,y)+d*.52f;
            var end = new Vector2(x,y)+d*.69f;
            Line(A(tint,.3f),.8f,mid.x,mid.y,end.x+n.x*.33f,end.y+n.y*.33f);
            Line(A(tint,.26f),.8f,mid.x,mid.y,end.x-n.x*.3f,end.y-n.y*.3f);
        }
        void Drop(float x, float y, float scale = 1)
        {
            Fill(new Path(x,y-12*scale).Curve(x-4*scale,y-5*scale,x-9*scale,y,x-8*scale,y+6*scale)
                .Curve(x-6*scale,y+15*scale,x+7*scale,y+15*scale,x+8*scale,y+6*scale)
                .Curve(x+9*scale,y,x+3*scale,y-6*scale,x,y-12*scale), C(161,211,220),Blue);
            Curve(A(Paper,.75f),1.6f*scale,x-4*scale,y+2*scale,x-5*scale,y+6*scale,x-3*scale,y+8*scale,x-1*scale,y+9*scale);
        }
        void Check(float x, float y)
        {
            Ellipse(x,y,19,19,C(220,233,211));
            Line(Green,3.4f,x-9,y,x-2,y+7); Line(Green,3.4f,x-2,y+7,x+11,y-8);
        }
        void Pencil(float x, float y, float scale = 1)
        {
            Fill(new Path(x,y).Line(x+10*scale,y+7*scale).Line(x-29*scale,y+64*scale)
                .Line(x-40*scale,y+70*scale).Line(x-38*scale,y+56*scale).Line(x,y), Gold,Wood);
            Line(Paper,1.7f*scale,x+3*scale,y+9*scale,x-30*scale,y+58*scale);
            Fill(new Path(x-38*scale,y+56*scale).Line(x-29*scale,y+64*scale).Line(x-40*scale,y+70*scale).Line(x-38*scale,y+56*scale),Cream,Cream);
            Fill(new Path(x-38*scale,y+63*scale).Line(x-34*scale,y+66*scale).Line(x-40*scale,y+70*scale).Line(x-38*scale,y+63*scale),Ink,Ink);
        }

        void Window()
        {
            Shadow();
            Rounded(48,31,160,180,6,C(205,188,157),Wood);
            Rounded(56,39,144,161,2,C(205,227,231),C(232,239,226));
            Line(A(Paper,.68f),1.6f,52,36,203,36);
            Line(A(Ink,.13f),1.5f,203,41,203,197);
            Curve(A(Paper,.58f),2.5f,65,47,65,69,66,90,66,110);
            Curve(A(Paper,.36f),1.3f,71,47,71,61,72,73,72,83);
            Ellipse(165,72,18,18,C(240,215,155));
            Fill(new Path(56,157).Curve(81,128,104,143,123,151).Curve(149,127,174,137,200,149)
                .Line(200,200).Line(56,200).Line(56,157),C(168,194,159),C(138,170,140));
            Line(Wood,5,89,159,89,110);
            Ellipse(89,102,22,27,Sage); Ellipse(77,109,17,20,C(153,181,147));
            Curve(A(Paper,.36f),1.7f,77,96,78,88,85,82,94,83);
            Line(A(Wood,.55f),1.3f,90,131,98,120);
            Curve(Paper,5,119,89,127,76,143,77,150,89); Curve(Paper,5,113,90,127,92,143,91,154,91);
            Rounded(124,39,7,161,1,Cream,Wood); Rounded(56,120,144,6,1,Cream,Wood);
            Rounded(42,201,172,11,3,Cream,Wood);
            Line(A(Paper,.62f),1.4f,49,204,207,204);
            if (Is("choose")) { Ring(91,103,30,A(accent,.65f),2.4f); }
            if (Is("observe") || Is("notice"))
            {
                if (!Is("notice") || choice == "색" || string.IsNullOrEmpty(choice))
                { Ellipse(52,228,6,6,Sage); Ellipse(70,228,6,6,Blue); Ellipse(88,228,6,6,Gold); }
                if (!Is("notice") || choice == "모양") Ring(89,105,31,A(accent,.65f),2.3f);
                if (!Is("notice") || choice == "움직임")
                { Curve(A(accent,.72f),2.4f,143,160,159,151,173,167,192,158); Curve(A(accent,.44f),1.8f,150,174,166,166,178,181,194,172); }
            }
        }
        void Glass(bool sip)
        {
            Fill(new Path(79,66).Line(177,66).Line(166,206).Curve(157,220,99,220,90,206).Line(79,66),C(219,237,237),C(169,208,216));
            Fill(new Path(87,sip?136:112).Curve(110,sip?128:104,148,sip?145:122,169,sip?136:112)
                .Line(162,202).Curve(148,212,108,212,94,202).Line(87,sip?136:112),C(174,216,224),Blue);
            Ellipse(128,66,49,10,C(190,218,222)); Ellipse(128,65,44,6,C(237,247,244));
            Curve(A(Paper,.8f),4,95,82,95,112,98,156,101,192);
            Curve(A(DeepBlue,.22f),1.4f,155,94,153,132,151,171,150,198);
            Ellipse(129,204,32,4,A(Paper,.25f));
            Curve(A(Paper,.65f),1.6f,91,sip?137:113,114,sip?133:109,144,sip?143:119,166,sip?136:112);
            Curve(A(DeepBlue,.3f),1.7f,94,205,109,215,149,216,162,205);
        }
        void Water()
        {
            Shadow(); Ellipse(128,217,62,10,C(215,206,183)); Ellipse(128,215,56,7,Cream);
            At(Is("drink")?111:128,132,Is("drink")?.87f:1,()=>Glass(Is("drink")));
            if (Is("prepare")) Drop(199,106,.75f);
            if (Is("drink")) { Drop(192,91,1.7f); Arrow(170,154,205,151,209,125,DeepBlue); }
        }
        void Face()
        {
            Ellipse(128,102,43,53,C(112,107,91));
            Fill(new Path(97,78).Curve(105,59,152,57,161,80).Line(160,117)
                .Curve(156,148,137,160,119,151).Curve(103,145,96,132,96,111).Line(97,78),C(247,221,194),Skin);
            Fill(new Path(90,91).Curve(82,61,104,44,128,45).Curve(155,42,173,65,167,91)
                .Curve(158,85,153,73,145,67).Curve(132,82,114,82,98,83).Line(96,99).Line(90,91),C(123,116,97),C(91,87,76));
            Curve(A(Paper,.19f),2,99,64,110,51,132,50,145,57);
            Ellipse(106,124,7,3.5f,A(Rose,.16f)); Ellipse(148,124,7,3.5f,A(Rose,.16f));
            Curve(Ink,1.8f,106,107,110,111,115,111,119,107);
            Curve(Ink,1.8f,137,107,141,111,146,111,150,107);
            Curve(C(176,130,108),1.4f,128,112,126,120,127,122,131,122);
            Curve(C(166,117,103),1.8f,121,134,126,138,133,138,138,132);
        }
        void Wash()
        {
            Shadow();
            Fill(new Path(107,141).Curve(90,149,76,156,72,184).Line(184,184)
                .Curve(180,156,163,149,148,141).Line(107,141),C(202,219,205),Sage);
            Face();
            Rounded(72,192,112,21,10,Cream,C(204,196,176));
            Fill(new Path(62,173).Curve(85,186,173,186,196,173).Curve(193,211,68,214,62,173),C(209,229,230),Blue);
            Ellipse(129,173,67,13,C(226,242,241)); Ellipse(129,173,57,8,C(179,216,220));
            Curve(A(Paper,.72f),1.8f,78,176,102,183,157,183,181,176);
            Curve(A(DeepBlue,.22f),1.5f,79,190,101,205,157,206,181,190);
            if (Is("wash"))
            {
                Curve(SkinShade,15,81,173,75,153,82,139,99,127); Curve(Skin,11,81,173,75,153,82,139,99,127);
                Curve(SkinShade,15,176,173,181,153,173,139,157,127); Curve(Skin,11,176,173,181,153,173,139,157,127);
                Curve(A(SkinShade,.72f),1.1f,88,137,91,132,94,130,98,129);
                Curve(A(SkinShade,.72f),1.1f,169,137,165,132,162,130,158,129);
                Drop(70,110,.7f); Drop(186,101,.9f); Drop(178,137,.55f);
            }
            else { Rounded(185,147,35,55,4,Cream,Paper); Line(Sage,3,191,158,214,158); Line(Sage,3,191,164,214,164); }
        }
        void Person(bool raised = false)
        {
            // The same calm face and central body remain anchored as arms change.
            Fill(new Path(111,111).Curve(102,115,102,131,104,145).Line(105,169)
                .Curve(120,176,140,176,152,168).Line(152,140).Curve(153,124,153,115,144,111)
                .Curve(134,116,121,116,111,111),C(159,180,155),Green);
            Fill(new Path(117,111).Line(120,103).Line(137,103).Line(140,111).Curve(135,119,122,119,117,111),Skin,SkinShade);
            At(128,90,.61f,Face);
            Curve(A(Paper,.6f),1.5f,112,116,121,122,136,122,145,116);
            Fill(new Path(112,161).Curve(101,171,71,183,73,198).Curve(77,218,179,220,183,200)
                .Curve(186,186,155,172,144,162).Curve(136,171,119,170,112,161),C(119,145,123),C(84,115,100));
            Curve(C(76,106,94),3,83,201,105,189,140,190,174,203);
            Curve(A(Paper,.4f),2,86,197,107,192,131,201,157,205);
            if (raised)
            {
                Curve(Sage,16,110,124,86,123,78,85,67,65); Curve(Skin,12,67,65,63,58,61,51,60,46);
                Curve(Sage,16,146,124,170,123,178,85,189,65); Curve(Skin,12,189,65,193,58,195,51,196,46);
                Curve(A(Paper,.35f),1.4f,102,126,87,119,78,92,72,77);
                Curve(A(Green,.55f),1.5f,154,126,169,119,178,93,184,77);
                Line(A(SkinShade,.65f),1,61,47,64,55); Line(A(SkinShade,.65f),1,195,47,192,55);
            }
            else
            {
                Curve(Sage,17,110,126,97,142,99,165,87,177); Curve(Sage,17,146,126,159,142,157,165,169,177);
                Curve(Skin,11,87,177,97,184,108,182,114,177); Curve(Skin,11,169,177,158,184,150,182,142,177);
                Curve(A(SkinShade,.55f),1,95,181,102,183,108,180,110,178);
                Curve(A(SkinShade,.55f),1,161,181,154,183,148,180,146,178);
            }
            Curve(A(Paper,.55f),1.7f,116,128,111,142,113,155,116,164);
        }
        void Stretch()
        {
            Shadow(); Ellipse(128,219,89,8,Cream); Person(Is("stretch"));
            if (Is("stretch"))
            { Arrow(47,128,30,87,44,63,A(accent,.8f)); Arrow(209,128,226,87,212,63,A(accent,.8f)); }
            else if (Is("prepare")) { Line(A(accent,.45f),2,57,228,199,228); }
        }
        void BookSpine(float x,float y,float width,float height,Color tint)
        {
            Rounded(x,y,width,height,3,Color.Lerp(tint,Paper,.2f),tint);
            Rounded(x+4,y+height-8,width-8,5,1,Paper,Cream);
            Line(A(Ink,.2f),1,x+7,y+4,x+7,y+height-11);
            if(height>20)
            {
                Line(A(Paper,.55f),1.1f,x+width-4,y+5,x+width-4,y+height-11);
                Line(A(Paper,.42f),1,x+12,y+10,x+width-8,y+10);
            }
        }
        void Tidy()
        {
            Shadow(); Rounded(33,208,190,10,4,Cream,Wood);
            Rounded(140,112,71,88,5,C(209,192,161),Wood); Rounded(148,123,55,66,2,C(235,225,205),Cream);
            Rounded(148,123,7,66,1,C(185,160,123),C(198,176,139));
            Line(A(Paper,.55f),1.5f,144,117,206,117);
            Rounded(146,190,59,5,1,Cream,C(177,149,112));
            BookSpine(Is("tidy")?152:57,Is("tidy")?138:153,43,48,Sage);
            BookSpine(42,196,82,9,C(183,164,142));
            if (Is("tidy")) { Arrow(65,127,94,98,126,131,accent); Check(198,69); }
            else if (Is("choose")) { Ring(77,175,34,A(accent,.6f),2.2f); }
            else { Ellipse(84,101,21,21,A(Gold,.15f)); }
        }
        void Heart(float x,float y,float scale,Color tint)
        {
            Fill(new Path(x,y+18*scale).Curve(x-34*scale,y-2*scale,x-20*scale,y-26*scale,x,y-11*scale)
                .Curve(x+20*scale,y-26*scale,x+34*scale,y-2*scale,x,y+18*scale),Color.Lerp(tint,Paper,.25f),tint);
        }
        void Feeling()
        {
            Shadow(); At(105,150,.74f,()=>Person());
            Fill(new Path(139,41).Curve(171,20,220,38,221,70).Curve(224,101,197,118,166,109)
                .Line(143,124).Line(146,104).Curve(120,86,119,59,139,41),C(238,222,231),C(216,190,208));
            if (Is("review")) { Line(A(accent,.65f),3,151,64,195,64); Line(A(accent,.4f),3,151,78,185,78); Check(206,116); }
            else
            {
                if(choice=="잘 모르겠어요")
                { Ellipse(158,73,3.4f,3.4f,accent); Ellipse(172,73,3.4f,3.4f,accent); Ellipse(186,73,3.4f,3.4f,accent); }
                else Heart(172,73,1,Rose);
                if(Is("feeling")) Pencil(210,149,.62f);
            }
        }
        void Note(float x,float y,float scale,Color tint)
        {
            Ellipse(x,y,6*scale,4.5f*scale,tint); Line(tint,2.8f*scale,x+5*scale,y,x+5*scale,y-23*scale);
            Curve(tint,3*scale,x+5*scale,y-23*scale,x+11*scale,y-22*scale,x+16*scale,y-19*scale,x+13*scale,y-13*scale);
        }
        void Music()
        {
            Shadow(); Rounded(88,52,80,157,11,C(137,135,155),C(100,102,127));
            Rounded(94,60,68,136,5,C(235,231,235),C(218,217,229));
            Rounded(101,75,54,52,4,C(197,184,210),C(153,154,184));
            Ellipse(128,101,17,17,C(233,222,235)); Ellipse(128,101,6,6,C(158,150,178));
            Ring(128,101,12,A(Paper,.65f),1.1f);
            Line(A(Paper,.58f),1.5f,100,66,153,66);
            Rounded(104,141,48,3,1,A(accent,.35f),A(accent,.35f));
            Ellipse(128,168,12,12,Tint);
            Fill(new Path(125,161).Line(135,168).Line(125,175).Line(125,161),Paper,Paper);
            Curve(C(110,110,130),9,67,141,44,53,205,47,190,143);
            Curve(A(Paper,.35f),2,64,119,59,65,193,62,194,121);
            Rounded(57,125,19,49,8,C(175,168,185),C(116,118,143)); Rounded(181,125,19,49,8,C(175,168,185),C(116,118,143));
            Rounded(61,131,8,37,4,C(199,190,209),C(153,148,175));
            Rounded(188,131,8,37,4,C(199,190,209),C(153,148,175));
            if (Is("listen")) { Note(42,91,1.1f,accent); Note(214,61,.85f,Tint); Curve(A(accent,.5f),2,210,161,224,172,223,186,211,194); }
            if (Is("prepare")) Ring(128,168,18,A(accent,.6f),2);
        }
        void Rest()
        {
            Shadow(); Rounded(61,74,134,122,35,C(199,191,211),C(161,160,184));
            Rounded(70,82,116,97,26,C(211,202,222),C(171,166,194));
            Curve(A(Paper,.45f),1.5f,79,108,78,95,88,88,102,88);
            Line(A(Ink,.13f),1.3f,73,153,73,177); Line(A(Ink,.13f),1.3f,184,153,184,177);
            Rounded(58,181,140,29,10,C(186,179,200),C(148,151,174));
            Line(Wood,9,73,203,66,224); Line(Wood,9,183,203,190,224);
            At(128,137,.77f,()=>Person());
            Rounded(49,141,22,59,10,C(192,185,208),C(159,159,183)); Rounded(185,141,22,59,10,C(192,185,208),C(159,159,183));
            Curve(A(Paper,.35f),1.3f,54,149,60,145,65,148,66,154);
            Curve(A(Paper,.35f),1.3f,190,149,196,145,201,148,202,154);
            if (Is("rest"))
            { Curve(A(accent,.4f),2.3f,51,58,82,39,102,53,111,43); Curve(A(accent,.25f),1.6f,151,45,171,37,196,44,210,58); }
            else if (Is("prepare")) { Arrow(37,109,37,133,45,145,A(accent,.6f)); }
        }
        void Notebook()
        {
            Shadow(); Rounded(58,38,143,183,8,C(203,184,145),Wood);
            Rounded(64,43,130,169,4,Paper,Cream);
            Line(A(Paper,.9f),1.6f,82,47,188,47); Line(A(Wood,.27f),1.3f,190,54,190,205);
            Rounded(64,43,15,169,2,C(217,206,178),C(202,189,159));
            for(int i=0;i<5;i++)
            {
                float y=65+i*29; Ellipse(72,y,3.5f,3.5f,Wood);
                Curve(C(195,180,145),3.5f,71,y,71,y-10,48,y-10,49,y);
                Curve(Paper,1.4f,69,y-5,63,y-10,53,y-9,51,y-3);
            }
            Line(A(Wood,.35f),2,91,88,174,88); Line(A(Wood,.25f),2,91,116,174,116); Line(A(Wood,.25f),2,91,144,174,144);
        }
        void Plan()
        {
            Notebook(); Ellipse(170,60,12,12,A(Gold,.5f));
            if (Is("review")) { Check(142,106); Line(A(accent,.65f),3,92,157,166,157); Line(A(accent,.4f),3,92,173,145,173); }
            else if (Is("plan-0"))
            {
                Rounded(88,96,89,19,4,A(Gold,.45f),A(Gold,.45f));
                for(int i=0;i<3;i++) { Rounded(91,102+i*28,9,9,2,A(accent,.45f),A(accent,.45f)); Line(A(accent,.6f),2,109,106+i*28,165-i*8,106+i*28); }
                Arrow(214,99,207,107,189,108,accent);
            }
            else if (Is("plan-1"))
            {
                // Broad parts of the day, rather than an invented exact alarm time.
                Rounded(86,125,93,36,4,C(235,228,205),C(225,218,196));
                Ellipse(101,144,9,9,Gold); Rounded(89,145,24,10,1,C(235,228,205),C(225,218,196));
                Line(Wood,1.5f,90,146,112,146);
                Ellipse(132,140,9,9,Gold); Ring(132,140,13,A(Gold,.4f),1.4f);
                Ellipse(162,140,10,10,C(146,153,177)); Ellipse(166,136,8,8,C(235,228,205));
                Line(A(accent,.6f),2,97,176,169,176);
                Ellipse(132,176,4,4,accent);
            }
            else
            {
                Pencil(216,104,1.1f);
                if(Is("plan-2")) { Curve(A(accent,.65f),2.4f,92,106,108,95,118,116,138,104); Curve(A(accent,.4f),2,92,135,109,124,132,147,153,133); }
                if(Is("plan")) { Rounded(90,100,65,10,3,A(Gold,.45f),A(Gold,.45f)); Ring(115,167,15,A(accent,.7f),2); Line(accent,2,115,167,115,158); Line(accent,2,115,167,123,171); }
            }
        }
        void Drawing()
        {
            Shadow(); Rounded(46,47,160,168,7,C(230,217,215),C(207,188,190)); Rounded(52,52,148,153,3,Paper,C(247,239,227));
            Line(A(Paper,.85f),1.5f,58,57,177,57);
            Fill(new Path(183,52).Line(200,69).Line(183,69).Line(183,52),C(226,213,194),Cream);
            Line(A(Wood,.3f),1,183,69,200,69);
            Curve(Is("overview")?A(Rose,.65f):Rose,4,76,132,57,82,136,68,130,122);
            Curve(Is("overview")?A(Rose,.65f):Rose,4,130,122,123,160,66,173,86,126);
            Curve(Is("overview")?A(Blue,.6f):Blue,3.3f,111,169,134,120,167,127,179,91);
            if (Is("review")) { Leaf(145,159,174,137,Sage); Ellipse(163,175,9,9,A(Gold,.6f)); }
            else Pencil(Is("drawing")?213:215,Is("drawing")?31:112,Is("drawing")?.85f:.95f);
        }
        void Sprout()
        {
            Curve(Green,5,128,193,130,144,124,96,130,47);
            Leaf(128,135,70,99,Sage); Leaf(129,111,181,71,Green); Leaf(129,84,101,43,Sage);
            Leaf(128,167,179,133,C(135,168,118)); Leaf(129,179,79,146,C(115,151,118));
            Fill(new Path(86,185).Line(171,185).Line(160,222).Curve(151,232,107,232,98,222).Line(86,185),C(212,173,141),C(177,133,106));
            Rounded(80,178,97,14,4,C(225,190,155),C(193,151,118));
            Ellipse(128,183,39,4.3f,C(140,108,78));
            Curve(Green,4,128,174,128,177,128,180,128,184);
            Curve(A(Paper,.5f),1.5f,86,188,109,192,151,193,172,187);
            Line(A(Paper,.55f),2,98,197,102,217);
        }
        void Magnifier(float x,float y,float radius)
        {
            Ellipse(x,y,radius,radius,A(Paper,.23f)); Ring(x,y,radius,C(191,162,109),4);
            Line(Wood,8,x+radius*.72f,y+radius*.72f,x+radius*1.65f,y+radius*1.65f);
            Curve(A(Paper,.8f),2,x-radius*.6f,y,x-radius*.65f,y-radius*.6f,x-radius*.3f,y-radius*.7f,x,y-radius*.7f);
        }
        void Plant()
        {
            Shadow(); Sprout();
            if (Is("choose")) Ring(128,112,80,A(accent,.2f),1.6f);
            if (Is("observe")) Magnifier(171,90,29);
            if (Is("notice"))
            {
                if(choice=="잎") Ring(160,91,30,A(accent,.7f),2.5f);
                else if(choice=="색") { Ellipse(204,136,8,8,Sage); Ellipse(204,157,8,8,Green); Ellipse(204,178,8,8,C(212,173,141)); }
                else if(choice=="모양") { Ring(128,109,80,A(accent,.5f),2); }
                else Magnifier(174,92,26);
            }
        }
        void Tea(float x,float y,float scale)
        {
            Ellipse(x,y+30*scale,38*scale,6*scale,Cream);
            Ring(x+29*scale,y+6*scale,13*scale,Wood,5*scale);
            Fill(new Path(x-28*scale,y-9*scale).Line(x+28*scale,y-9*scale).Line(x+23*scale,y+20*scale)
                .Curve(x+18*scale,y+35*scale,x-19*scale,y+35*scale,x-24*scale,y+20*scale).Line(x-28*scale,y-9*scale),Paper,C(219,208,180));
            Ellipse(x,y-8*scale,28*scale,6*scale,Wood); Ellipse(x,y-9*scale,23*scale,4*scale,C(168,136,97));
            Curve(A(Wood,.4f),1.7f*scale,x-9*scale,y-20*scale,x-23*scale,y-32*scale,x+7*scale,y-37*scale,x-5*scale,y-50*scale);
        }
        void Memory()
        {
            Shadow(); Rounded(48,50,160,158,5,C(225,209,174),Wood); Rounded(56,58,144,141,2,Paper,Cream);
            Rounded(68,70,120,87,2,C(227,234,211),C(209,224,199));
            Line(A(Paper,.75f),1.6f,61,62,199,62);
            Line(A(Wood,.25f),1.3f,201,65,201,194);
            if(choice=="잘 모르겠어요")
            {
                Curve(A(accent,.28f),2,82,132,104,121,139,141,175,128);
                Ellipse(113,108,3,3,A(accent,.7f)); Ellipse(128,108,3,3,A(accent,.7f)); Ellipse(143,108,3,3,A(accent,.7f));
            }
            else { Ellipse(164,92,13,13,Gold); Tea(125,129,.7f); }
            if (Is("word")) { Rounded(81,172,94,18,2,C(245,225,173),C(232,209,154)); Pencil(216,149,.7f); }
            else if(Is("recall"))
            {
                if (choice != "잘 모르겠어요") Heart(207,57,.65f,Rose);
                else { Ring(210,57,15,A(accent,.35f),2); Ellipse(192,80,4,4,A(accent,.3f)); Ellipse(185,91,2.5f,2.5f,A(accent,.2f)); }
            }
            else { Curve(A(accent,.3f),2,39,101,25,96,24,81,32,74); }
        }
        void OpenBook()
        {
            Fill(new Path(37,68).Curve(69,53,105,62,128,77).Curve(153,61,190,55,219,70)
                .Line(214,203).Curve(185,191,155,195,128,212).Curve(102,195,68,191,40,201).Line(37,68),C(192,170,136),Wood);
            Fill(new Path(44,64).Curve(74,52,103,63,126,77).Line(126,202).Curve(102,185,72,182,46,192).Line(44,64),Paper,Cream);
            Fill(new Path(130,77).Curve(156,61,189,55,212,64).Line(208,192).Curve(181,182,155,187,130,202).Line(130,77),Paper,C(236,226,205));
            Line(A(Wood,.5f),2,128,78,128,205);
            Curve(A(Paper,.7f),1.5f,44,196,74,187,105,194,126,207);
            Curve(A(Paper,.7f),1.5f,131,207,155,194,184,188,211,196);
            Curve(A(Wood,.3f),1.2f,47,190,72,182,103,188,123,200);
            Curve(A(Wood,.25f),1.2f,134,200,159,188,184,182,207,190);
            for(int i=0;i<5;i++)
            {
                Curve(A(Wood,.4f),1.8f,58,87+i*17,78,82+i*17,95,88+i*17,112,97+i*17);
                Curve(A(Wood,.4f),1.8f,144,97+i*17,162,87+i*17,183,83+i*17,198,88+i*17);
            }
            Fill(new Path(171,66).Line(183,64).Line(182,113).Line(176,108).Line(170,115).Line(171,66),Sage,Green);
        }
        void Reading()
        {
            Shadow();
            if (Is("choose"))
            { BookSpine(49,161,151,30,Sage); BookSpine(61,125,143,31,C(196,167,127)); BookSpine(53,89,141,31,C(160,177,192)); Arrow(211,81,224,101,215,118,accent); }
            else
            { OpenBook(); if(Is("read")) { Curve(A(Gold,.48f),9,58,120,78,115,95,121,112,130); Curve(A(accent,.6f),1.7f,59,126,78,121,96,127,111,135); } }
        }
        void Shoe(float x,float y,float scale,bool right)
        {
            float d=right?1:-1;
            Ellipse(x+8*scale*d,y+22*scale,34*scale,4*scale,A(Ink,.08f));
            Fill(new Path(x-19*scale*d,y-24*scale).Curve(x-10*scale*d,y-18*scale,x-2*scale*d,y-18*scale,x+7*scale*d,y-24*scale)
                .Curve(x+10*scale*d,y-12*scale,x+30*scale*d,y-4*scale,x+38*scale*d,y+5*scale)
                .Curve(x+45*scale*d,y+13*scale,x+39*scale*d,y+20*scale,x+21*scale*d,y+20*scale)
                .Line(x-15*scale*d,y+20*scale).Curve(x-25*scale*d,y+19*scale,x-26*scale*d,y+12*scale,x-24*scale*d,y+3*scale)
                .Curve(x-22*scale*d,y-8*scale,x-24*scale*d,y-17*scale,x-19*scale*d,y-24*scale),C(178,196,174),Green);
            Curve(A(Ink,.35f),3*scale,x-17*scale*d,y-22*scale,x-9*scale*d,y-15*scale,x-1*scale*d,y-16*scale,x+5*scale*d,y-22*scale);
            Curve(A(Paper,.5f),1.2f*scale,x-17*scale*d,y-14*scale,x-11*scale*d,y-6*scale,x-15*scale*d,y+6*scale,x-11*scale*d,y+11*scale);
            Curve(Paper,5*scale,x-21*scale*d,y+16*scale,x-2*scale*d,y+17*scale,x+24*scale*d,y+19*scale,x+40*scale*d,y+12*scale);
            Line(Paper,2*scale,x+1*scale*d,y-8*scale,x+13*scale*d,y-8*scale);
            Line(Paper,2*scale,x+7*scale*d,y-2*scale,x+20*scale*d,y-2*scale);
            Curve(A(Green,.6f),1.2f*scale,x+14*scale*d,y+4*scale,x+22*scale*d,y+1*scale,x+31*scale*d,y+5*scale,x+35*scale*d,y+9*scale);
        }
        void Walking()
        {
            Shadow(); Curve(Cream,24,49,184,73,143,169,116,196,48); Curve(A(Wood,.3f),1.5f,49,184,73,143,169,116,196,48);
            Leaf(52,129,31,97,Sage); Leaf(193,169,223,137,Sage);
            Shoe(91,181,1.08f,true); Shoe(154,133,.88f,true);
            if(Is("walking")) { Ellipse(154,91,5,9,A(accent,.45f)); Ellipse(172,73,5,9,A(accent,.35f)); Arrow(80,84,96,63,121,58,accent); }
            else if(Is("check")) Check(197,51);
            else
            {
                Ring(195,46,13,accent,3); Ellipse(195,46,4,4,accent);
                if(Is("goal")) { Ring(195,46,23,A(accent,.4f),2); Arrow(111,83,143,55,164,51,accent); }
            }
        }
        void Tree(float x,float y,float scale)
        {
            Line(Wood,9*scale,x,y,x,y-65*scale);
            Line(A(Paper,.28f),1.6f*scale,x-2*scale,y-6*scale,x-2*scale,y-54*scale);
            Ellipse(x-18*scale,y-66*scale,29*scale,30*scale,C(164,188,144));
            Ellipse(x+18*scale,y-74*scale,31*scale,34*scale,Sage);
            Ellipse(x,y-98*scale,30*scale,32*scale,C(143,173,127));
            Curve(A(Paper,.35f),2*scale,x-19*scale,y-96*scale,x-17*scale,y-106*scale,x-9*scale,y-112*scale,x+1*scale,y-113*scale);
        }
        void Park()
        {
            Shadow(); Ellipse(129,212,102,13,C(216,230,200));
            Curve(Cream,17,65,229,91,207,139,218,192,195);
            Tree(76,197,.95f); Tree(202,180,.62f);
            // Both back slats attach to the same supports beneath the seat.
            // Draw the posts first so they are behind the wooden boards.
            Line(Ink,4,117,149,117,169); Line(Ink,4,198,149,198,169);
            Rounded(111,145,92,10,2,C(197,170,131),Wood); Rounded(111,159,92,10,2,C(197,170,131),Wood);
            Rounded(105,181,104,8,2,Cream,Wood); Line(Ink,4,117,169,114,207); Line(Ink,4,198,169,201,207);
            Line(A(Paper,.6f),1.3f,116,147,198,147); Line(A(Paper,.5f),1.2f,116,161,198,161);
            Ellipse(118,150,1.8f,1.8f,A(Ink,.4f)); Ellipse(196,150,1.8f,1.8f,A(Ink,.4f));
            Curve(A(Wood,.42f),1.1f,133,164,148,160,173,167,188,162);
            Line(A(Green,.5f),1.5f,51,213,47,206); Line(A(Green,.5f),1.5f,51,213,55,205);
            Line(A(Green,.4f),1.3f,213,211,216,205); Line(A(Green,.4f),1.3f,213,211,210,206);
            if(Is("prepare")) { Line(Wood,4,37,211,37,167); Fill(new Path(23,166).Line(49,166).Line(58,175).Line(49,184).Line(23,184).Line(23,166),Cream,Wood); }
            if(Is("visit")) { Curve(A(accent,.7f),2,131,76,138,67,144,71,148,77); Curve(A(accent,.7f),2,148,77,155,67,161,69,167,75); Ellipse(157,215,50,5,A(accent,.13f)); }
        }
        void Breathing()
        {
            Shadow(); Ellipse(128,218,84,9,Cream);
            if(Is("breath")) { Ring(128,128,96+breathMotion,A(accent,.16f),3); Ring(128,128,85+breathMotion*.7f,A(accent,.24f),2); }
            Person();
            if(Is("breath"))
            { Curve(A(accent,.65f),2.5f,180,112,208,109,215,130,206,143); Curve(A(accent,.38f),2,187,104,224,100,233,134,219,153); }
            else if(Is("prepare")) Line(A(accent,.4f),2,76,231,181,231);
        }

        void At(float x, float y, float scale, Action draw)
        {
            var previousCenter = modelCenter; float previousScale = modelScale, previousAa = antialias;
            modelCenter += new Vector2(x - 128, y - 128) * modelScale;
            modelScale *= scale; antialias /= scale;
            try { draw(); } finally { modelCenter = previousCenter; modelScale = previousScale; antialias = previousAa; }
        }
        void Rounded(float x, float y, float width, float height, float radius, Color top, Color bottom)
            => Fill(new Path(x + radius, y).Line(x + width - radius, y)
                .Curve(x + width, y, x + width, y, x + width, y + radius).Line(x + width, y + height - radius)
                .Curve(x + width, y + height, x + width, y + height, x + width - radius, y + height)
                .Line(x + radius, y + height).Curve(x, y + height, x, y + height, x, y + height - radius)
                .Line(x, y + radius).Curve(x, y, x, y, x + radius, y), top, bottom);
        void Arrow(float x, float y, float controlX, float controlY, float endX, float endY, Color tint)
        {
            var direction = (new Vector2(endX, endY) - new Vector2(controlX, controlY)).normalized;
            var normal = new Vector2(-direction.y, direction.x); var tip = new Vector2(endX, endY);
            var join = tip-direction*4;
            Curve(tint, 2.6f, x, y, controlX, controlY, controlX, controlY, join.x, join.y);
            var a = tip - direction * 9 + normal * 4.8f; var b = tip - direction * 9 - normal * 4.8f;
            Fill(new Path(tip.x,tip.y).Line(a.x,a.y).Line(b.x,b.y).Line(tip.x,tip.y),tint,tint);
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
            for (int i = 0; i < 48; i++) { float angle = i * Mathf.PI / 24; points.Add(new Vector2(x + Mathf.Cos(angle) * rx, y + Mathf.Sin(angle) * ry)); }
            Polygon(points, tint, tint);
        }
        void Curve(Color tint, float width, float x, float y, float ax, float ay, float bx, float by, float endX, float endY)
        {
            var points = new Path(x, y).Curve(ax, ay, bx, by, endX, endY).Points;
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
            // A transparent outward fringe smooths the vector edges at every
            // screen scale without generating textures or persistent resources.
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
