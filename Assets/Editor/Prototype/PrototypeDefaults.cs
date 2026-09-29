using CapstoneDesign.Prototype;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    public static class PrototypeDefaults
    {
        public static void Populate(PrototypeDefinition d)
        {
            d.flowers = new[] {
                Flower("chamomile", PlantChoice.Quiet, "캐모마일", "역경 속의 힘", "FFF4D6", FlowerShape.Chamomile),
                Flower("hydrangea", PlantChoice.Together, "수국", "이해해 주어 고마워요", "ABBDE3", FlowerShape.Hydrangea)
            };
            MigrateDailyPrototype(d);
        }

        public static void MigrateDailyPrototype(PrototypeDefinition d)
        {
            if (d.schemaVersion >= 3) return;
            // Keep authored questions/flowers when upgrading an existing scene.
            if (d.schemaVersion >= 2)
            {
                d.maximumStage = PrototypeDefinition.StageCount - 1;
                d.schemaVersion = 3;
                return;
            }
            d.maximumStage = PrototypeDefinition.StageCount - 1;
            d.questions = new[] {
                Question("오늘은 어떻게 쉬어가고 싶나요?", "조용히 나만의 시간", "누군가와 연결되는 시간"),
                Question("오늘 마음이 향하는 쪽은?", "혼자 풍경 바라보기", "좋은 풍경 나누기"),
                Question("오늘 어떤 여유를 갖고 싶나요?", "내 마음에 귀 기울이기", "누군가의 마음 들어주기"),
                Question("내일은 어떻게 이어가고 싶나요?", "내 속도로 천천히", "주변과 함께 가볍게")
            };
            if (d.flowers == null || d.flowers.Length != 2) { Populate(d); return; }
            foreach (var f in d.flowers) f.affinity = f.shape == FlowerShape.Chamomile ? PlantChoice.Quiet : PlantChoice.Together;
            d.schemaVersion = 3;
        }

        private static DailyQuestion Question(string question, string quiet, string together) =>
            new DailyQuestion { question = question, quietLabel = quiet, togetherLabel = together };
        private static FlowerDefinition Flower(string id, PlantChoice affinity, string name, string meaning, string hex, FlowerShape shape)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return new FlowerDefinition { id = id, affinity = affinity, displayName = name, meaning = meaning, color = color, shape = shape };
        }
    }
}
