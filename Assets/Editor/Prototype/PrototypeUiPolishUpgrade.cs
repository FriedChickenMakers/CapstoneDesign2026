using System;
using System.Linq;
using CapstoneDesign.Prototype;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // One authoring pass for both an existing scene and future scene-builder runs.
    public static class PrototypeUiPolishUpgrade
    {
        private static readonly Color Paper=Hex("F7F8F0"), Ink=Hex("283D30"), Muted=Hex("5E6E62"),
            Green=Hex("315B44"), Soft=Hex("E3EEDC"), Line=Hex("D9E1D4");

        [MenuItem("Capstone Prototype/Apply Readable UI Polish")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!=PrototypeSceneBuilder.ScenePath||scene.isDirty)
                throw new InvalidOperationException("Open the saved Prototype scene and stop Play Mode first.");
            var app=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PrototypeController>(true)).Single();
            Undo.RegisterFullObjectHierarchyUndo(app.portraitLayout.gameObject,"Improve prototype UI readability");
            Undo.RegisterFullObjectHierarchyUndo(app.gameObject,"Bind selection presentation");
            Configure(app);
            EditorUtility.SetDirty(app);
            EditorUtility.SetDirty(app.missionPresentation);
            EditorUtility.SetDirty(app.selectionView);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PROTOTYPE_UI_POLISH_OK larger type, editable vector UI and selection states");
        }

        public static void Configure(PrototypeController app)
        {
            PrototypeSceneBuilder.EnsureMissionCompletedPopup(app);
            var frame=app.portraitLayout.designFrame;
            var content=app.screenRoot.transform;
            foreach(var text in frame.GetComponentsInChildren<TMP_Text>(true))
            {
                int size=text.name=="Title"?44:text.name=="Guide"?28:30;
                Type(text,size,text.name=="Guide"?Muted:Ink,text.name=="Title");
            }
            foreach(var surface in frame.GetComponentsInChildren<PrototypeUiSurface>(true))
            {
                if(surface.GetComponent<Button>()!=null)continue;
                surface.raycastTarget=surface.name=="MoodNoteInput";
                if(surface.name.EndsWith("_GroundGlow"))surface.gameObject.SetActive(false);
            }
            foreach(var button in frame.GetComponentsInChildren<Button>(true))StyleButton(button);
            Paint(app.missionPresentation.background,Paper,0,0);
            app.missionPresentation.idleBackground=Paper;
            app.missionPresentation.missionBackground=Hex("274A3B");
            app.missionPresentation.missionForeground=Hex("F7F8F0");
            Type(content.Find("Brand").GetComponent<TMP_Text>(),30,Green,true);
            Rect(content.Find("Brand"),48,24,250,54);
            Type(app.dateText,26,Muted);
            Rect(app.dateText.transform,306,30,256,48);
            Rect(app.developerPanel.transform,582,24,90,58);
            Type(app.developerPanel.GetComponentInChildren<TMP_Text>(),26,Green,true);
            Stretch(app.developerPanel.GetComponentInChildren<TMP_Text>().rectTransform,12,12);

            // Stable header, title and CTA rows on all four pages.
            foreach(var page in app.pages)
            {
                var root=page.transform;
                Rect(root.Find("Title"),48,12,624,78);
                Type(root.Find("Title").GetComponent<TMP_Text>(),root==app.pages[1].transform?42:46,Ink,true);
                Rect(root.Find("Guide"),48,96,624,48);
            }
            string[] actions={"GardenAction","ConfirmCheckIn","MissionAction","HistoryAction"};
            for(int i=0;i<actions.Length;i++)Place(app.pages[i].transform.Find(actions[i]),48,940,624,96,0);
            app.missionPresentation.primaryButtons=actions.Select((name,i)=>app.pages[i].transform.Find(name).GetComponent<Button>()).ToArray();
            app.missionPresentation.idleButtonFill=Green;
            app.missionPresentation.activeButtonFill=Soft;
            app.missionPresentation.activeButtonInk=Green;
            app.missionPresentation.completedMissionButton=app.gardenButton;
            app.missionPresentation.completedButtonFill=Hex("DADFDA");
            app.missionPresentation.completedButtonInk=Hex("4F5E53");

            var home=app.pages[0].transform;
            Paint(home.Find("TodayMissionCard").GetComponent<PrototypeUiSurface>(),Color.white,24,1.5f);
            home.Find("TodayMissionCard_Edge").gameObject.SetActive(false);
            Place(home.Find("TodayMissionCard"),48,796,624,122,0);
            Place(app.dailyStatus.transform,76,806,468,42,0);Type(app.dailyStatus,26,Muted);
            Place(home.Find("MissionName"),76,850,468,54,0);Type(home.Find("MissionName").GetComponent<TMP_Text>(),34,Ink,true);
            var missionEmblem=Surface("MissionEmblem",home,Soft,32);
            Place(missionEmblem.transform,580,825,64,64,0);
            Icon(missionEmblem.transform,"Leaf",PrototypeUiIcon.Symbol.Leaf,12,12,40,Green);
            Place(app.stageText.transform,64,688,592,60,.5f);Type(app.stageText,32,Green,true);
            for(int i=0;i<4;i++)Place(app.progressDots[i].transform,243+i*62,766,48,8,.5f);

            var mission=app.pages[2].transform;
            Place(app.missionStatus.transform,48,760,624,58,.5f);Type(app.missionStatus,32,Green,true);

            var selection=app.GetComponent<PrototypeUiSelectionView>();
            if(selection==null)selection=app.gameObject.AddComponent<PrototypeUiSelectionView>();
            app.selectionView=selection;
            selection.accent=Green;selection.ink=Ink;selection.muted=Muted;selection.selectedFill=Soft;
            selection.homeLabel=app.homeTabLabel;selection.historyLabel=app.historyTabLabel;
            var nav=content.Find("BottomNavigation").GetComponent<PrototypeUiSurface>();
            Paint(nav,Color.white,28,1);
            Bottom(nav.rectTransform,24,116,672,106);
            var homeTab=content.Find("HomeTab").GetComponent<Button>();
            var historyTab=content.Find("MoodTab").GetComponent<Button>();
            Nav(homeTab,40,out selection.homeSurface,out selection.homeIcon,PrototypeUiIcon.Symbol.Leaf);
            Nav(historyTab,368,out selection.historySurface,out selection.historyIcon,PrototypeUiIcon.Symbol.Journal);

            var check=app.pages[1].transform;
            selection.moodTiles=new PrototypeUiSurface[5];selection.moodLabels=new TMP_Text[5];selection.moodChecks=new GameObject[5];
            for(int i=0;i<5;i++)
            {
                var button=app.moodButtons[i];
                Place(button.transform,40+i*128,186,128,156,.5f);
                var tile=Surface("SelectionTile",button.transform,Color.clear,24);
                Rect(tile.transform,4,0,120,152);tile.transform.SetAsFirstSibling();
                selection.moodTiles[i]=tile;
                Rect(app.moodFaces[i].transform,18,9,92,92);
                var label=button.transform.Find("MoodLabel").GetComponent<TMP_Text>();
                Type(label,26,Ink);Rect(label.transform,0,104,128,44);
                // Same emotion, shorter selector copy to keep five large, single-line labels.
                if(i==3)label.text="지쳐요";
                selection.moodLabels[i]=label;
                selection.moodChecks[i]=Icon(button.transform,"SelectionCheck",PrototypeUiIcon.Symbol.Check,94,3,26,Green).gameObject;
            }
            Place(app.moodStatus.transform,48,350,624,46,.5f);Type(app.moodStatus,28,Green);
            Place(check.Find("NoteTitle"),48,400,420,48,.5f);Type(check.Find("NoteTitle").GetComponent<TMP_Text>(),32,Ink,true);
            Place(app.noteCounter.transform,522,400,150,48,.5f);Type(app.noteCounter,26,Muted);
            Place(app.moodNoteInput.transform,48,458,624,166,.5f);
            Paint(app.moodNoteInput.GetComponent<PrototypeUiSurface>(),Color.white,22,1.5f);
            Rect(app.moodNoteInput.textViewport,24,16,576,134);
            Type(app.moodNoteInput.textComponent,32,Ink);
            var placeholder=(TMP_Text)app.moodNoteInput.placeholder;
            placeholder.text="지금 느끼는 마음을 적어주세요";Type(placeholder,30,Muted);
            app.moodNoteInput.caretWidth=3;app.moodNoteInput.caretColor=Green;
            app.moodNoteInput.selectionColor=new Color(.63f,.77f,.59f,.40f);
            Place(app.questionText.transform,48,650,624,56,.5f);Type(app.questionText,32,Ink,true);
            Place(app.quietButton.transform,48,718,624,96,.5f);
            Place(app.togetherButton.transform,48,828,624,96,.5f);
            Choice(app.quietButton,app.quietLabel,out selection.quietCheck);
            Choice(app.togetherButton,app.togetherLabel,out selection.togetherCheck);

            // Readable, dynamic record cards; height remains based on the complete note.
            Type(app.moodHistory.countLabel,28,Green,true);
            Rect(app.moodHistory.countLabel.transform,48,160,624,48);
            var scrollRect=(RectTransform)app.moodHistory.scroll.transform;
            scrollRect.offsetMin=new Vector2(48,130);scrollRect.offsetMax=new Vector2(-48,-226);
            app.moodHistory.scroll.content.GetComponent<VerticalLayoutGroup>().spacing=20;
            var row=app.moodHistory.rowTemplate;
            Paint(row.GetComponent<PrototypeUiSurface>(),Color.white,24,1.5f);
            Type(row.dateLabel,26,Muted);Rect(row.dateLabel.transform,26,16,410,46);
            Type(row.timeLabel,26,Muted);
            row.timeLabel.rectTransform.anchoredPosition=new Vector2(-122,-16);
            row.timeLabel.rectTransform.sizeDelta=new Vector2(96,46);
            Rect(row.face.transform,26,78,82,82);Rect(row.skippedIcon.transform,26,78,82,82);
            Type(row.moodLabel,34,Ink,true);
            row.moodLabel.rectTransform.anchoredPosition=new Vector2(132,-90);
            row.moodLabel.rectTransform.sizeDelta=new Vector2(-158,56);
            Type(row.noteLabel,30,Ink);row.noteLabel.lineSpacing=8;
            row.noteLabel.rectTransform.anchoredPosition=new Vector2(26,-186);
            row.noteLabel.rectTransform.sizeDelta=new Vector2(-52,46);
            row.noteTop=186;row.noteHorizontalPadding=52;row.noteBottomPadding=28;row.emptyHeight=188;
            Type(app.moodHistory.emptyState.transform.Find("EmptyLabel").GetComponent<TMP_Text>(),32,Muted);

            foreach(var popup in new[]{app.growthPopup,app.settingsPopup,app.resetPopup,app.missionCompletedPopup})
                Paint(popup.transform.Find("Card").GetComponent<PrototypeUiSurface>(),Paper,32,1.5f);
            var completed=app.missionCompletedPopup.transform;
            Rect(completed.Find("Card"),28,452,664,376);
            Type(completed.Find("Title").GetComponent<TMP_Text>(),34,Ink,true);
            Rect(completed.Find("Title"),64,518,592,72);
            Rect(completed.Find("ConfirmMissionCompleted"),76,676,568,96);
            Type(app.growthTitle,42,Ink,true);Type(app.growthStage,34,Green,true);Type(app.growthSummary,28,Muted);
            Rect(app.growthTitle.transform,48,182,624,72);
            Rect(app.growthGuide.transform,48,260,624,50);
            Rect(app.growthStage.transform,48,814,624,62);
            Rect(app.growthSummary.transform,48,883,624,52);
            Rect(app.growthPopup.transform.Find("ConfirmGrowth"),76,991,568,96);
            Type(app.settingsDate,30,Green,true);
            foreach(var name in new[]{"CloseSettings","CancelReset"})
            {
                var button=frame.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
                ((RectTransform)button.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,88);
            }
            selection.Render(PrototypePage.Garden,Mood.None,PlantChoice.None);
            PrototypePreviewUpgrade.Configure(app);
            app.portraitLayout.Apply();
            Canvas.ForceUpdateCanvases();
        }

        private static void Choice(Button button,TMP_Text label,out PrototypeUiIcon check)
        {
            Type(label,32,Green);label.alignment=TextAlignmentOptions.MidlineLeft;
            label.rectTransform.offsetMin=new Vector2(28,0);label.rectTransform.offsetMax=new Vector2(-76,0);
            check=Icon(button.transform,"SelectionCheck",PrototypeUiIcon.Symbol.Check,552,29,38,Green);
            var ring=Surface("ChoiceRing",button.transform,Color.white,18);
            ring.borderWidth=2;ring.borderColor=Line;Rect(ring.transform,552,29,38,38);
            ring.transform.SetSiblingIndex(1);check.transform.SetAsLastSibling();
        }
        private static void Nav(Button button,float x,out PrototypeUiSurface surface,out PrototypeUiIcon icon,PrototypeUiIcon.Symbol symbol)
        {
            Bottom((RectTransform)button.transform,x,108,312,90);
            surface=button.GetComponent<PrototypeUiSurface>();Paint(surface,Color.clear,22,0);
            var label=button.GetComponentInChildren<TMP_Text>(true);Type(label,28,Green,true);
            label.alignment=TextAlignmentOptions.MidlineLeft;
            label.rectTransform.offsetMin=new Vector2(82,0);label.rectTransform.offsetMax=new Vector2(-12,0);
            icon=Icon(button.transform,"TabIcon",symbol,28,24,42,Green);
        }
        private static void StyleButton(Button button)
        {
            if(button.name.StartsWith("Mood_"))return;
            var surface=button.GetComponent<PrototypeUiSurface>();
            bool primary=new[]{"GardenAction","MissionAction","ConfirmCheckIn","HistoryAction","ConfirmGrowth","ConfirmMissionCompleted","NextDay","ConfirmReset"}.Contains(button.name);
            Paint(surface,primary?Green:Color.white,24,primary?0:1.5f);
            surface.raycastTarget=true;
            var c=button.colors;c.normalColor=Color.white;c.highlightedColor=new Color(.94f,.98f,.92f);
            c.pressedColor=new Color(.78f,.87f,.74f);c.selectedColor=Color.white;
            c.disabledColor=new Color(.66f,.71f,.64f,1);c.fadeDuration=.12f;button.colors=c;
            var label=button.GetComponentInChildren<TMP_Text>(true);
            Type(label,primary?34:32,primary?Color.white:Green,true);
            Stretch(label.rectTransform,24,24);
            if(primary)
            {
                var shadow=surface.GetComponent<Shadow>()??surface.gameObject.AddComponent<Shadow>();
                shadow.effectColor=new Color(.14f,.23f,.15f,.10f);shadow.effectDistance=new Vector2(0,-4);shadow.useGraphicAlpha=true;
            }
        }
        private static void Type(TMP_Text text,int size,Color tint,bool bold=false)
        {
            text.fontSize=size;text.fontSizeMin=size;text.fontSizeMax=size;text.enableAutoSizing=false;
            text.color=tint;text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;text.raycastTarget=false;
            if(text.rectTransform.anchorMin.y==text.rectTransform.anchorMax.y)
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(text.rectTransform.rect.height,size*1.5f));
        }
        private static PrototypeUiSurface Surface(string name,Transform parent,Color tint,float radius)
        {
            var child=parent.Find(name);
            var surface=child!=null?child.GetComponent<PrototypeUiSurface>():Create(name,parent).gameObject.AddComponent<PrototypeUiSurface>();
            Paint(surface,tint,radius,0);surface.raycastTarget=false;return surface;
        }
        private static PrototypeUiIcon Icon(Transform parent,string name,PrototypeUiIcon.Symbol symbol,float x,float y,float size,Color tint)
        {
            var child=parent.Find(name);
            var icon=child!=null?child.GetComponent<PrototypeUiIcon>():Create(name,parent).gameObject.AddComponent<PrototypeUiIcon>();
            icon.symbol=symbol;icon.color=tint;icon.raycastTarget=false;Rect(icon.transform,x,y,size,size);icon.SetVerticesDirty();return icon;
        }
        private static void Paint(PrototypeUiSurface surface,Color tint,float radius,float edge)
        { surface.topColor=surface.bottomColor=tint;surface.cornerRadius=radius;surface.borderWidth=edge;surface.borderColor=Line;surface.SetVerticesDirty(); }
        private static RectTransform Create(string name,Transform parent)
        { var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect; }
        private static void Rect(Transform transform,float x,float y,float w,float h)
        {
            var rect=(RectTransform)transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
        }
        private static void Place(Transform transform,float x,float top,float w,float h,float anchor)
        {
            Rect(transform,x,top,w,h);var rect=(RectTransform)transform;
            rect.anchorMin=rect.anchorMax=new Vector2(0,anchor);
            rect.anchoredPosition=new Vector2(x,(1-anchor)*1044-top);
        }
        private static void Bottom(RectTransform rect,float x,float top,float w,float h)
        { Rect(rect,x,0,w,h);rect.anchorMin=rect.anchorMax=Vector2.zero;rect.anchoredPosition=new Vector2(x,top); }
        private static void Stretch(RectTransform rect,float left,float right)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(left,0);rect.offsetMax=new Vector2(-right,0);}
        private static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var color);return color;}
    }
}
