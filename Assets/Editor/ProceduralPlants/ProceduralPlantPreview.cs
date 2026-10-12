using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using UnityEditor;
using UnityEngine;

namespace CapstoneDesign.PlantEditor
{
    public sealed class ProceduralPlantPreview : EditorWindow
    {
        readonly string[] categories={"호흡","몸 감각","움직임","감각","생각·감정","자기돌봄"};
        int[] counts={6,1,0,0,0,3};
        string owner="synthetic-owner-A",instance="synthetic-plant-A",error;
        int preset,historyIndex,growthPreset=1,maxGrowthSteps=28;
        PlantGrowthMode growthMode=PlantGrowthMode.MissionCompletions;
        float angle=-25;
        PlantRecipe recipe;
        readonly List<PlantRecipe> history=new List<PlantRecipe>();
        PreviewRenderUtility preview;
        GameObject model;
        ProceduralPlantView view;
        [MenuItem("Tools/Mindful Garden/Procedural Plant Preview")]
        public static void Open() { GetWindow<ProceduralPlantPreview>("Plant Preview").minSize=new Vector2(440,780); }
        void OnEnable()
        {
            preview=new PreviewRenderUtility();preview.camera.orthographic=true;preview.camera.orthographicSize=.78f;
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.95f,.93f,.86f);
            model=new GameObject("Synthetic procedural plant preview") { hideFlags=HideFlags.HideAndDontSave };
            view=model.AddComponent<ProceduralPlantView>();preview.AddSingleGO(model);
            ResetPlant();
        }
        void OnDisable() { if(model!=null) DestroyImmediate(model);preview?.Cleanup();preview=null; }
        void ResetPlant()
        {
            try { recipe=PlantGenerator.Create(owner,instance,maxGrowthSteps,growthMode);history.Clear();history.Add(recipe);historyIndex=0;Show(recipe); }
            catch(Exception e) { error=e.Message; }
        }
        void Show(PlantRecipe p) { view.Render(p);error=null;Repaint(); }
        void Grow()
        {
            try
            {
                var input=PlantCodec.Clone(recipe);input.Counts=(int[])counts.Clone();
                var next=PlantGenerator.Advance(input);Show(next);
                if(historyIndex<history.Count-1) history.RemoveRange(historyIndex+1,history.Count-historyIndex-1);
                recipe=next;history.Add(next);historyIndex=history.Count-1;
            }
            catch(Exception e) { error=e.Message; }
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("절차적 식물 생성 — 격리된 합성 데이터 미리보기",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("기존 정원 저장·영양제는 변경하지 않는 미리보기입니다. 종류는 목표의 약 1/3에서 확정되고 줄기는 개화 전 길이 성장을 끝냅니다. 아래 활동 수는 외형 계산용 합성 데이터입니다.",MessageType.Info);
            growthMode=(PlantGrowthMode)EditorGUILayout.Popup("성장 기준",(int)growthMode,new[]{"영양제 구매 1회","인정 미션 완료 1회"});
            int nextPreset=EditorGUILayout.Popup("프로그램 목표",growthPreset,new[]{"기존 12회","4주 · 매일 1회 = 28회","8주 · 주 6회 = 48회","8주 · 매일 1회 = 56회","직접 입력"});
            if(nextPreset!=growthPreset) { growthPreset=nextPreset;if(growthPreset<4) maxGrowthSteps=new[]{12,28,48,56}[growthPreset]; }
            using(new EditorGUI.DisabledScope(growthPreset!=4)) maxGrowthSteps=EditorGUILayout.IntField("최대 성장 횟수 (6~1000)",maxGrowthSteps);
            if(GUILayout.Button("목표 적용 · 새 씨앗")) ResetPlant();
            if(recipe!=null&&(recipe.MaxGrowthSteps!=maxGrowthSteps||recipe.GrowthMode!=growthMode)) EditorGUILayout.HelpBox("변경한 목표/기준은 '목표 적용 · 새 씨앗'을 눌러 적용하세요. 기존 개체의 목표는 보존됩니다.",MessageType.Info);
            string nextOwner=EditorGUILayout.TextField("합성 사용자 ID",owner);
            string nextInstance=EditorGUILayout.TextField("합성 개체 ID",instance);
            owner=nextOwner;instance=nextInstance;
            EditorGUILayout.BeginHorizontal();
            preset=EditorGUILayout.Popup("종별 활동 예시",preset,PlantCatalog.All.Select(s=>s.Name).ToArray());
            if(GUILayout.Button("적용 + 새 씨앗",GUILayout.Width(130))) { counts=PlantCatalog.All[preset].ExampleCounts();ResetPlant(); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            for(int i=0;i<6;i++) { EditorGUILayout.BeginVertical();GUILayout.Label(categories[i]);counts[i]=Math.Max(0,EditorGUILayout.IntField(counts[i]));EditorGUILayout.EndVertical(); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("새 씨앗 / ID 반영")) ResetPlant();
            using(new EditorGUI.DisabledScope(recipe==null||recipe.Mature))
            {
                if(GUILayout.Button("성장 1회 미리보기")) Grow();
                if(GUILayout.Button("성숙까지")) { while(recipe!=null&&!recipe.Mature) { int step=recipe.GrowthStep;Grow();if(recipe.GrowthStep==step) break; } }
            }
            EditorGUILayout.EndHorizontal();
            if(recipe==null) return;
            int h=EditorGUILayout.IntSlider("저장한 성장 이력",historyIndex,0,Math.Max(0,history.Count-1));
            if(h!=historyIndex) { historyIndex=h;recipe=history[h];Show(recipe); }
            string name=recipe.SpeciesId==null?"공통 씨앗/새싹":PlantCatalog.Get(recipe.SpeciesId).Name;
            EditorGUILayout.LabelField(name+" · 성장 "+recipe.GrowthStep+"/"+recipe.MaxGrowthSteps+" · 종류 확정 "+recipe.DifferentiationStep+"회 · "+view.TriangleCount+" triangles");
            EditorGUILayout.LabelField(recipe.MeshVersion>=2?"형태: Refined · 두께 있는 다각형 잎과 꽃잎":"형태: 기존 저장 식물");
            EditorGUILayout.LabelField("잎 "+PlantGenerator.LeafCount(recipe)+" · 꽃 자리 "+recipe.Heads.Sum(x=>PlantGenerator.VisibleFlorets(recipe,x))+" · 열매 "+recipe.Fruits.Count(x=>x.Present));
            if(recipe.SpeciesId==null) EditorGUILayout.LabelField("현재 후보: "+string.Join(" / ",PlantGenerator.Candidates(counts).Take(3).Select(x=>PlantCatalog.Get(x.SpeciesId).Name)));
            if(error!=null) EditorGUILayout.HelpBox(error,MessageType.Error);
            Rect rect=GUILayoutUtility.GetRect(200,200,GUILayout.ExpandHeight(true),GUILayout.ExpandWidth(true));
            if(Event.current.type==EventType.MouseDrag&&rect.Contains(Event.current.mousePosition)) { angle+=Event.current.delta.x;Event.current.Use();Repaint(); }
            if(Event.current.type==EventType.Repaint&&preview!=null)
            {
                var rotation=Quaternion.Euler(18,angle,0);preview.camera.transform.SetPositionAndRotation(new Vector3(0,.57f,0)+rotation*(Vector3.back*3),rotation);
                preview.BeginPreview(rect,GUIStyle.none);preview.Render(true);GUI.DrawTexture(rect,preview.EndPreview(),ScaleMode.StretchToFill,false);
            }
            if(GUILayout.Button("현재 Recipe XML 내보내기"))
            {
                string path=EditorUtility.SaveFilePanel("Export synthetic recipe","","synthetic-plant-recipe","xml");
                if(!string.IsNullOrEmpty(path)) File.WriteAllText(path,PlantCodec.Encode(recipe));
            }
        }
    }
}
