using System;
using System.Collections.Generic;

namespace CapstoneDesign.Runtime
{
    // Presentation copy only. Existing catalog versions still own the step keys,
    // required answers, recommendations, saved prompts and completion policy.
    public static class FocusAdditionalSlideContent
    {
        sealed class MissionCopy
        {
            public readonly string Overview, Help;
            public MissionCopy(string overview, string help) { Overview = overview; Help = help; }
        }

        static readonly Dictionary<string, MissionCopy> Missions = new Dictionary<string, MissionCopy>(StringComparer.Ordinal)
        {
            { "M02", new MissionCopy("창밖 풍경 하나를\n잠시 살펴봐요.", "창문을 열 필요는 없어요.\n보이는 풍경 하나면 돼요.\n눈에 띄는 모습이 없어도 괜찮아요.") },
            { "M03", new MissionCopy("물 한 모금으로\n잠시 쉬어가요.", "마실 물을 가까이 두어요.\n편하게 한 모금 마셔요.\n마신 뒤 아이콘을 눌러요.") },
            { "M04", new MissionCopy("편한 방식으로\n얼굴을 씻어봐요.", "편한 온도의 물을 써요.\n얼굴을 부드럽게 씻어요.\n마친 뒤 아이콘을 눌러요.") },
            { "M05", new MissionCopy("무리 없는 동작 하나로\n몸을 펴봐요.", "앉아서 해도 괜찮아요.\n손이나 어깨를 편한 만큼 펴요.\n아프면 바로 멈춰요.") },
            { "M06", new MissionCopy("눈앞의 물건\n하나만 정리해요.", "작은 물건 하나면 돼요.\n둘 곳을 정해 옮겨요.\n주변을 모두 정리할 필요는 없어요.") },
            { "M07", new MissionCopy("지금 기분을\n짧게 표현해봐요.", "한 단어만 적어도 돼요.\n모르겠다면 해당 선택지를 골라요.\n기분이 달라질 필요는 없어요.") },
            { "M08", new MissionCopy("듣고 싶은 음악에\n잠시 머물러봐요.", "평소 쓰는 앱에서 음악을 틀어요.\n다른 앱에 다녀와도 이어가요.\n편한 만큼 듣고 마쳐요.") },
            { "M09", new MissionCopy("하던 일을\n잠시 멈춰봐요.", "몸을 편하게 두어요.\n아무것도 하지 않아도 돼요.\n편한 만큼 쉬고 마쳐요.") },
            { "M10", new MissionCopy("오늘 하고 싶은\n작은 일 하나를 정해요.", "부담 없는 작은 일을 정해요.\n정확한 시각은 필요 없어요.\n알림과 별개로 편할 때 실천해요.") },
            { "M11", new MissionCopy("떠오르는 선이나\n모양을 그려봐요.", "잘 그릴 필요는 없어요.\n빈 공간에 자유롭게 그려요.\n이전으로 돌아가 더 그릴 수 있어요.") },
            { "M12", new MissionCopy("가까운 식물 하나를\n살펴봐요.", "가까이 보이는 식물이면 돼요.\n만지거나 이름을 알 필요는 없어요.\n눈에 띈 모습이 없어도 괜찮아요.") },
            { "M13", new MissionCopy("오늘 괜찮았던\n순간 하나를 떠올려요.", "평범한 순간이어도 좋아요.\n떠오르지 않아도 괜찮아요.\n한 단어 기록은 선택이에요.") },
            { "M14", new MissionCopy("관심 있는 글을\n편한 만큼 읽어봐요.", "책이나 짧은 글 모두 좋아요.\n전부 읽지 않아도 괜찮아요.\n편한 만큼 읽고 마쳐요.") },
            { "M15", new MissionCopy("안전한 곳에서\n편한 속도로 걸어봐요.", "걸을 때는 화면을 내려두어요.\n멈춘 뒤 걸음 수를 확인해요.\n동기화가 늦으면 직접 확인해요.") },
            { "M16", new MissionCopy("가까운 공원에\n다녀와요.", "안전하고 편한 경로를 골라요.\n이동 중에는 화면을 내려두어요.\n도착해 멈춘 뒤 방문을 확인해요.") },
            { "M17", new MissionCopy("편한 속도로\n자연스러운 숨을 살펴요.", "원은 참고만 해요.\n숨을 억지로 맞추지 않아요.\n불편하면 쉬거나 멈춰요.") }
        };

        static readonly Dictionary<string, string> Steps = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "M02:choose", "창밖에서 눈에 들어오는\n풍경 하나를 골라요." },
            { "M02:observe", "고른 풍경의 색과 모양,\n작은 움직임을 살펴요." },
            { "M02:notice", "눈에 띈 모습을 골라요.\n고르지 않아도 괜찮아요." },
            { "M03:prepare", "마실 물 한 잔을\n가까이 두어요." },
            { "M03:drink", "편하게 한 모금 마셔요.\n마신 뒤 아이콘을 눌러요." },
            { "M04:wash", "편한 방식으로 얼굴을 씻어요.\n마친 뒤 아이콘을 눌러요." },
            { "M05:prepare", "앉거나 서서\n편한 자세를 잡아요." },
            { "M05:stretch", "손이나 어깨를 편한 만큼 펴요.\n아프면 멈춰요." },
            { "M06:choose", "제자리에 둘 물건\n하나를 골라요." },
            { "M06:tidy", "고른 물건을\n제자리에 놓아요." },
            { "M07:feeling", "지금 기분을 한 단어로 적거나\n‘잘 모르겠어요’를 골라요." },
            { "M07:review", "남긴 기분을 확인해요.\n고치려면 이전을 눌러요." },
            { "M08:prepare", "듣고 싶은 음악을 골라\n재생해 주세요." },
            { "M08:listen", "편한 만큼 음악을 들어요.\n다른 앱에 다녀와도 이어가요." },
            { "M09:prepare", "앉거나 몸을 기대어\n편한 자세를 찾아요." },
            { "M09:rest", "아무것도 하지 않아도 돼요.\n편한 만큼 쉬어요." },
            { "M10:plan", "작은 일과\n편한 시간대를 골라요." },
            { "M10:review", "고른 계획을 확인해요.\n고치려면 이전을 눌러요." },
            { "M11:drawing", "빈 공간에 선과 모양을\n자유롭게 그려요." },
            { "M11:review", "내 그림을 잠시 살펴요.\n더 그리려면 이전을 눌러요." },
            { "M12:choose", "가까이에서 살펴볼\n식물 하나를 골라요." },
            { "M12:observe", "잎의 모양과 색을\n천천히 살펴봐요." },
            { "M12:notice", "눈에 띈 모습을 골라요.\n고르지 않아도 괜찮아요." },
            { "M13:recall", "오늘 괜찮았던 순간을 떠올려요.\n평범한 순간이어도 좋아요." },
            { "M13:word", "원한다면 그 순간을\n한 단어로 남겨요." },
            { "M14:choose", "읽고 싶은 책이나\n짧은 글 하나를 골라요." },
            { "M14:read", "천천히 읽어봐요.\n편한 만큼 읽고 마쳐요." },
            { "M15:goal", "목표 걸음을 확인하고\n안전한 곳을 골라요." },
            { "M15:walking", "휴대폰을 내려두고\n편한 속도로 걸어요." },
            { "M15:check", "안전한 곳에 멈춘 뒤\n실제 걸음을 확인해요." },
            { "M16:prepare", "가까운 공원까지\n안전한 경로로 이동해요." },
            { "M16:visit", "공원 주변을 살펴본 뒤\n방문을 확인해요." },
            { "M17:prepare", "편한 자세에서\n숨을 자연스럽게 두어요." },
            { "M17:breath", "원은 참고만 해요.\n내 호흡 속도를 따라가요." }
        };

        public static bool Supports(string code) => code != null && Missions.ContainsKey(code);

        public static string Overview(string code)
            => code != null && Missions.TryGetValue(code, out var copy) ? copy.Overview : "";

        public static string Body(string code, string stepKey, string choice = "", int version = 5)
        {
            if (code == "M13" && stepKey == "word" && choice == "잘 모르겠어요")
                return "떠오르지 않아도 괜찮아요.\n기록 없이 마쳐도 돼요.";
            // v4 asks for a required written plan; v5 offers activity/time choices.
            // A display improvement must not suggest controls absent from a save.
            if (version == 4 && code == "M10" && stepKey == "plan")
                return "할 수 있는 작은 일\n하나를 적어요.";
            if (version == 4 && code == "M10" && stepKey == "review")
                return "적은 계획을 확인해요.\n고치려면 이전을 눌러요.";
            return code != null && stepKey != null && Steps.TryGetValue(code + ":" + stepKey, out var body) ? body : "";
        }

        public static string Help(string code, int version = 5)
        {
            if (version == 4 && code == "M10")
                return "부담 없는 작은 일을 정해요.\n짧은 한 문장으로 적어요.\n이전으로 돌아가 고칠 수 있어요.";
            return code != null && Missions.TryGetValue(code, out var copy) ? copy.Help : "";
        }
    }
}
