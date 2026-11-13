using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public enum QuestCategory
    {
        BodyScan,
        Breathing,
        MindfulWalk,
        MindfulMovement,
        Reflection,
        EmotionAwareness
    }

    [Serializable]
    public sealed class QuestReward
    {
        public int nutrient;
        public int gardenXp;
        public float growth;
        public string unlock = string.Empty;
    }

    [Serializable]
    public sealed class QuestDefinition
    {
        public string id = string.Empty;
        public string title = string.Empty;
        public QuestCategory category;
        public string evidenceSource = string.Empty;
        public int week;
        public int durationMinutes;
        public string frequency = string.Empty;
        public string difficulty = string.Empty;
        public string completionCondition = string.Empty;
        public bool weekly;
        public QuestReward reward = new QuestReward();
    }

    public interface IQuestRecommendationEngine
    {
        QuestDefinition Select(IReadOnlyList<QuestDefinition> library);
    }

    public sealed class FirstIncompleteQuestRecommendation : IQuestRecommendationEngine
    {
        public QuestDefinition Select(IReadOnlyList<QuestDefinition> library)
        {
            return library.FirstOrDefault(quest => PlayerPrefs.GetInt(CompletionKey(quest.id), 0) == 0);
        }

        internal static string CompletionKey(string id) => "capstone.quest.complete." + id;
    }

    public static class WeekOneQuestLibrary
    {
        public static IReadOnlyList<QuestDefinition> Create()
        {
            const string source = "MBSR-inspired POC; Brown/Palouse curriculum themes";
            return new[]
            {
                Daily("week1.body_scan_5", "5분 바디 스캔", QuestCategory.BodyScan, 5, source),
                Daily("week1.breathing_3", "3분 호흡 마음챙김", QuestCategory.Breathing, 3, source),
                Daily("week1.everyday_action", "평소 행동 하나를 천천히 알아차리기", QuestCategory.EmotionAwareness, 3, source),
                new QuestDefinition
                {
                    id = "week1.reflection",
                    title = "한 주의 경험을 짧게 돌아보기",
                    category = QuestCategory.Reflection,
                    evidenceSource = source,
                    week = 1,
                    durationMinutes = 3,
                    frequency = "WEEKLY",
                    difficulty = "GENTLE",
                    weekly = true,
                    completionCondition = "Complete three Week 1 daily activities, then reflect",
                    reward = new QuestReward { gardenXp = 5, growth = 0.03f, unlock = "Week 1 seed" }
                }
            };
        }

        private static QuestDefinition Daily(
            string id,
            string title,
            QuestCategory category,
            int durationMinutes,
            string source)
        {
            return new QuestDefinition
            {
                id = id,
                title = title,
                category = category,
                evidenceSource = source,
                week = 1,
                durationMinutes = durationMinutes,
                frequency = "DAILY",
                difficulty = "GENTLE",
                weekly = false,
                completionCondition = "User confirms the shortened practice",
                reward = new QuestReward { nutrient = 1, gardenXp = 1, growth = 0.01f }
            };
        }
    }

}
