using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Selection presentation only. Controller supplies state; nothing is stored or awarded here.
    public sealed class PrototypeUiSelectionView : MonoBehaviour
    {
        public PrototypeUiSurface homeSurface, historySurface;
        public TMP_Text homeLabel, historyLabel;
        public PrototypeUiIcon homeIcon, historyIcon;
        public PrototypeUiSurface[] moodTiles;
        public TMP_Text[] moodLabels;
        public GameObject[] moodChecks;
        public PrototypeUiIcon quietCheck, togetherCheck;
        public Color accent = new Color32(49,91,68,255);
        public Color ink = new Color32(40,61,48,255);
        public Color muted = new Color32(94,110,98,255);
        public Color selectedFill = new Color32(227,238,220,255);

        public void Render(PrototypePage page, Mood mood, PlantChoice choice)
        {
            bool history=page==PrototypePage.MoodHistory;
            Tab(homeSurface,homeLabel,homeIcon,!history);
            Tab(historySurface,historyLabel,historyIcon,history);
            for(int i=0;i<moodTiles.Length;i++)
            {
                bool selected=(int)mood==i+1;
                Fill(moodTiles[i],selected?selectedFill:Color.clear);
                moodLabels[i].color=selected?accent:ink;
                moodLabels[i].fontStyle=selected?FontStyles.Bold:FontStyles.Normal;
                moodChecks[i].SetActive(selected);
            }
            quietCheck.gameObject.SetActive(choice==PlantChoice.Quiet);
            togetherCheck.gameObject.SetActive(choice==PlantChoice.Together);
        }
        private void Tab(PrototypeUiSurface surface,TMP_Text label,PrototypeUiIcon icon,bool selected)
        {
            Fill(surface,selected?selectedFill:Color.clear);
            label.color=icon.color=selected?accent:muted;
            label.fontStyle=selected?FontStyles.Bold:FontStyles.Normal;
        }
        private static void Fill(PrototypeUiSurface surface,Color tint)
        {
            if(surface.topColor==tint&&surface.bottomColor==tint)return;
            surface.topColor=surface.bottomColor=tint;surface.SetVerticesDirty();
        }
    }
}
