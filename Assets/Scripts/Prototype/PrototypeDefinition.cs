using System;
using System.Linq;
using UnityEngine;

namespace CapstoneDesign.Prototype
{
    public enum PlantChoice { None, Quiet, Together }
    public enum Mood { None, Happy, Calm, Neutral, Tired, Down }
    public enum FlowerShape { Chamomile = 1, Hydrangea = 2 }

    [Serializable]
    public sealed class DailyQuestion
    {
        public string question;
        public string quietLabel;
        public string togetherLabel;
    }

    [Serializable]
    public sealed class FlowerDefinition
    {
        public string id;
        public PlantChoice affinity;
        public string displayName;
        public string meaning;
        public Color color = Color.white;
        public FlowerShape shape;
    }

    [CreateAssetMenu(menuName = "Capstone/Prototype Definition")]
    public sealed class PrototypeDefinition : ScriptableObject
    {
        [HideInInspector] public int schemaVersion;
        public string missionTitle = "한 모금 천천히";
        public string missionGuide = "물 한 모금의 온도와 맛을 느껴 보세요.";
        // Zero-based index: seed + three completed actions = four visible stages.
        public const int StageCount = 4;
        public int maximumStage = StageCount - 1;
        public DailyQuestion[] questions;
        public FlowerDefinition[] flowers;
        public static readonly string[] MoodLabels = { "미선택", "기뻐요", "차분해요", "평온해요", "조금 지쳐요", "울적해요" };
        public static readonly string[] StageLabels = { "작은 씨앗", "새싹", "꽃봉오리", "활짝 핀 꽃" };

        public FlowerDefinition FindFlower(PlantChoice choice) => flowers.FirstOrDefault(f => f.affinity == choice);
        public DailyQuestion QuestionAt(int completedCount) => questions[Math.Min(completedCount, 3)];

        public void ValidateDefinition()
        {
            if (maximumStage != StageCount - 1 || questions == null || questions.Length != 4)
                throw new InvalidOperationException("Prototype requires four visible stages (including seed) and four daily question variants.");
            if (!OneLine(missionTitle) || !OneLine(missionGuide)
                || questions.Any(q => q == null || !OneLine(q.question) || !OneLine(q.quietLabel) || !OneLine(q.togetherLabel)))
                throw new InvalidOperationException("Mission and daily question text must be nonempty single lines.");
            if (flowers == null || flowers.Length != 2 || flowers.Any(f => f == null)
                || flowers.Select(f => f.id).Distinct().Count() != 2
                || flowers.Select(f => f.shape).Distinct().Count() != 2)
                throw new InvalidOperationException("Exactly two distinct flowers are required.");
            foreach (var choice in new[] { PlantChoice.Quiet, PlantChoice.Together })
            {
                var f = FindFlower(choice);
                if (f == null || !OneLine(f.id) || !OneLine(f.displayName) || !OneLine(f.meaning)
                    || !Enum.IsDefined(typeof(FlowerShape), f.shape))
                    throw new InvalidOperationException("Incomplete flower mapping: " + choice);
            }
        }

        private static bool OneLine(string s) => !string.IsNullOrWhiteSpace(s) && !s.Contains("\n") && !s.Contains("\r");
    }
}
