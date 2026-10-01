using CapstoneDesign.Prototype;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Tests
{
    public sealed class PrototypeUiPolishTests
    {
        private GameObject root;
        private PrototypeUiSelectionView view;
        [SetUp] public void Setup()
        {
            root=new GameObject("UI polish test",typeof(RectTransform));
            view=root.AddComponent<PrototypeUiSelectionView>();
            view.homeSurface=Child<PrototypeUiSurface>();view.historySurface=Child<PrototypeUiSurface>();
            view.homeLabel=Child<TextMeshProUGUI>();view.historyLabel=Child<TextMeshProUGUI>();
            view.homeIcon=Child<PrototypeUiIcon>();view.historyIcon=Child<PrototypeUiIcon>();
            view.quietCheck=Child<PrototypeUiIcon>();view.togetherCheck=Child<PrototypeUiIcon>();
            view.moodTiles=new PrototypeUiSurface[5];view.moodLabels=new TMP_Text[5];view.moodChecks=new GameObject[5];
            for(int i=0;i<5;i++)
            {view.moodTiles[i]=Child<PrototypeUiSurface>();view.moodLabels[i]=Child<TextMeshProUGUI>();view.moodChecks[i]=Child<PrototypeUiIcon>().gameObject;}
        }
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(root);
        private T Child<T>() where T:Component
        {var go=new GameObject(typeof(T).Name,typeof(RectTransform));go.transform.SetParent(root.transform,false);return go.AddComponent<T>();}

        [TestCase(PrototypePage.Garden)] [TestCase(PrototypePage.CheckIn)] [TestCase(PrototypePage.Mission)] [TestCase(PrototypePage.MoodHistory)]
        public void NavigationShowsExactlyOneActiveDestination(PrototypePage page)
        {
            view.Render(page,Mood.None,PlantChoice.None);
            bool history=page==PrototypePage.MoodHistory;
            Assert.That(view.homeSurface.topColor,Is.EqualTo(history?Color.clear:view.selectedFill));
            Assert.That(view.historySurface.topColor,Is.EqualTo(history?view.selectedFill:Color.clear));
            Assert.That(view.historyIcon.color,Is.EqualTo(history?view.accent:view.muted));
            Assert.That(view.homeIcon.color,Is.EqualTo(history?view.muted:view.accent));
        }
        [TestCase(Mood.Happy)] [TestCase(Mood.Calm)] [TestCase(Mood.Neutral)] [TestCase(Mood.Tired)] [TestCase(Mood.Down)]
        public void MoodSelectionAndClearingNeverLeaveStaleCheckmarks(Mood mood)
        {
            view.Render(PrototypePage.CheckIn,mood,PlantChoice.None);
            for(int i=0;i<5;i++)
            {
                bool selected=i+1==(int)mood;
                Assert.That(view.moodChecks[i].activeSelf,Is.EqualTo(selected));
                Assert.That(view.moodTiles[i].topColor,Is.EqualTo(selected?view.selectedFill:Color.clear));
            }
            view.Render(PrototypePage.CheckIn,Mood.None,PlantChoice.None);
            foreach(var check in view.moodChecks)Assert.That(check.activeSelf,Is.False);
        }
        [TestCase(PlantChoice.None)] [TestCase(PlantChoice.Quiet)] [TestCase(PlantChoice.Together)]
        public void ChoiceMarksMatchOnlyTheSelectedBranch(PlantChoice choice)
        {
            view.Render(PrototypePage.CheckIn,Mood.None,choice);
            Assert.That(view.quietCheck.gameObject.activeSelf,Is.EqualTo(choice==PlantChoice.Quiet));
            Assert.That(view.togetherCheck.gameObject.activeSelf,Is.EqualTo(choice==PlantChoice.Together));
        }
        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)]
        public void CompletedMissionStyleOnlyAffectsHomeAndRestoresOnNewDay(bool inProgress, bool completed)
        {
            var presentation=root.AddComponent<PrototypeMissionPresentation>();
            presentation.background=Child<PrototypeUiSurface>();
            presentation.header=Child<TextMeshProUGUI>();presentation.header.text="마음 정원";
            presentation.backgroundLabels=new TMP_Text[]{presentation.header};
            var home=Child<PrototypeUiSurface>();var other=Child<PrototypeUiSurface>();
            var homeButton=home.gameObject.AddComponent<Button>();homeButton.targetGraphic=home;
            var otherButton=other.gameObject.AddComponent<Button>();otherButton.targetGraphic=other;
            var homeLabel=Child<TextMeshProUGUI>();homeLabel.transform.SetParent(home.transform,false);
            var otherLabel=Child<TextMeshProUGUI>();otherLabel.transform.SetParent(other.transform,false);
            presentation.primaryButtons=new[]{homeButton,otherButton};presentation.completedMissionButton=homeButton;
            presentation.Render(inProgress,completed);
            Assert.That(home.topColor,Is.EqualTo(completed?presentation.completedButtonFill:inProgress?presentation.activeButtonFill:presentation.idleButtonFill));
            Assert.That(homeLabel.color,Is.EqualTo(completed?presentation.completedButtonInk:inProgress?presentation.activeButtonInk:Color.white));
            Assert.That(homeButton.interactable,Is.True,"Completion notice must remain clickable");
            Assert.That(other.topColor,Is.EqualTo(inProgress?presentation.activeButtonFill:presentation.idleButtonFill));
            Color.RGBToHSV(presentation.completedButtonFill,out _,out float saturation,out _);
            Color.RGBToHSV(presentation.idleButtonFill,out _,out float idleSaturation,out _);
            Assert.That(saturation,Is.LessThan(idleSaturation));
            presentation.Render(false,false);
            Assert.That(home.topColor,Is.EqualTo(presentation.idleButtonFill));
            Assert.That(homeLabel.color,Is.EqualTo(Color.white));
        }

        [Test] public void LargeRecordNoteWrapsWithoutClippingAndEmptyRowCompacts()
        {
            var row=Child<PrototypeMoodHistoryRow>();
            var rect=(RectTransform)row.transform;rect.sizeDelta=new Vector2(610,188);
            row.noteLabel=Child<TextMeshProUGUI>();row.noteLabel.transform.SetParent(row.transform,false);
            row.noteLabel.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Prototype/Fonts/NotoSansKR SDF.asset");
            row.noteLabel.fontSize=30;row.noteLabel.enableAutoSizing=false;
            row.noteLabel.textWrappingMode=TextWrappingModes.Normal;
            row.noteLabel.text="오늘은 조금 지쳤지만 내 속도로 천천히 움직였어요. 작은 마음을 잊지 않고 남겨 둡니다.";
            row.noteTop=186;row.noteHorizontalPadding=52;row.noteBottomPadding=28;row.emptyHeight=188;
            row.CalculateLayoutInputVertical();float wide=row.preferredHeight;
            rect.sizeDelta=new Vector2(300,188);row.CalculateLayoutInputVertical();
            Assert.That(row.preferredHeight,Is.GreaterThan(wide));
            Assert.That(row.preferredHeight,Is.EqualTo(row.noteTop+row.noteLabel.rectTransform.rect.height+row.noteBottomPadding).Within(.01f));
            row.noteLabel.gameObject.SetActive(false);row.CalculateLayoutInputVertical();
            Assert.That(row.preferredHeight,Is.EqualTo(188));
        }
    }
}
