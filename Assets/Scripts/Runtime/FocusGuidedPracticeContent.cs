using System;

namespace CapstoneDesign.Runtime
{
    // App-authored guidance for the P01-P07 instruction slides and practice view.
    // Timing, progress, and completion remain the responsibility of the session policy.
    public static class FocusGuidedPracticeContent
    {
        public static string Preparation(string code)
        {
            switch (code)
            {
                case "P01": return "편하게 앉거나 누워, 숨이 느껴지는 곳을 골라요.";
                case "P02": return "편하게 앉아 눈을 뜨거나 감고 소리에 귀 기울여요.";
                case "P03": return "편하게 앉거나 누워요.";
                case "P04": return "편한 온도의 음료를 준비해요.";
                case "P05": return "편한 자세로 자연스럽게 숨 쉬어요.";
                case "P06": return "기기를 물이 닿지 않는 곳에 두어요.";
                case "P07": return "오늘 편하게 살펴볼 대상을 떠올려요.";
                default: return "";
            }
        }

        public static string ChoiceLabel(string code)
        {
            switch (code)
            {
                case "P01": return "숨을 느낄 곳";
                case "P06": return "손 씻기와 설거지 중 골라요.";
                case "P07": return "오늘 살펴볼 대상을 골라요.";
                default: return "";
            }
        }

        public static string[] Guidance(string code, string choice)
        {
            switch (code)
            {
                case "P01":
                    string breath = choice == "코" ? "코끝에서 숨이 드나들 때의 온도나 스침을 살펴봐요."
                        : choice == "가슴" ? "숨을 쉴 때 가슴이 부풀고 가라앉는 움직임을 느껴봐요."
                        : choice == "배" ? "숨을 쉴 때 배가 부풀고 가라앉는 움직임을 느껴봐요."
                        : "코·가슴·배 중 숨이 가장 선명한 한 곳에 주의를 두어요.";
                    return new[]
                    {
                        breath,
                        "숨을 고치려 하지 않고, 그곳의 감각이 달라지는 모습을 느껴요.",
                        "주의가 벗어나면 알아차리고, 고른 곳의 감각으로 돌아와요."
                    };
                case "P02":
                    return new[]
                    {
                        "가까이 또는 멀리에서 들리는 소리 하나에 귀 기울여요.",
                        "소리의 크기와 높낮이, 시작과 끝을 살펴봐요.",
                        "다른 생각이 들면 알아차리고, 지금 들리는 소리로 돌아와요."
                    };
                case "P03":
                    return new[]
                    {
                        "의자나 바닥에 닿는 곳을 느껴요.",
                        "닿는 곳의 압력과 온도를 느껴요.",
                        "딴생각을 알아차리면 접촉으로 돌아와요."
                    };
                case "P04":
                    return new[]
                    {
                        "컵의 감촉과 온도를 느껴요.",
                        "한 모금의 맛과 온도를 느껴요.",
                        "삼킨 뒤 남은 맛과 느낌을 살펴요."
                    };
                case "P05":
                    return new[]
                    {
                        "숨이 느껴지는 한 곳에 머물러요.",
                        "주의가 옮겨간 순간을 알아차려요.",
                        "부드럽게 숨으로 돌아와요."
                    };
                case "P06":
                    if (choice == "설거지")
                        return new[]
                        {
                            "그릇의 감촉과 물의 온도를 느껴요.",
                            "닦는 움직임과 물소리를 살펴요.",
                            "딴생각을 알아차리면 감각으로 돌아와요."
                        };
                    return new[]
                    {
                        "손에 닿는 물의 온도를 느껴요.",
                        "손의 움직임과 감촉, 물소리를 살펴요.",
                        "딴생각을 알아차리면 감각으로 돌아와요."
                    };
                case "P07":
                    if (choice == "몸의 접촉") return Guidance("P03", null);
                    if (choice == "주변 소리") return new[]
                    {
                        "가깝거나 먼 소리 하나를 들어요.",
                        "크기·높낮이·시작과 끝을 살펴요.",
                        "딴생각을 알아차리면 소리로 돌아와요."
                    };
                    return new[]
                    {
                        "코·가슴·배 중 한 곳을 느껴요.",
                        "숨은 평소처럼 자연스럽게.",
                        "딴생각을 알아차리면 숨으로 돌아와요."
                    };
                default: return Array.Empty<string>();
            }
        }

        public static string Reassurance(string code)
        {
            switch (code)
            {
                case "P01": return "숨의 감각이 뚜렷하지 않아도 괜찮아요.";
                case "P02": return "소리의 원인이나 이름을 맞힐 필요는 없어요.";
                case "P03": return "감각이 흐릿해도 괜찮아요.";
                case "P04": return "새로운 맛을 찾지 않아도 괜찮아요.";
                case "P05": return "생각이 없어도 그대로 머물러요.";
                case "P06": return "새로운 감각이 없어도 괜찮아요.";
                case "P07": return "벗어난 것을 알아차리면 돌아와요.";
                default: return "";
            }
        }

        public static string ReflectionPrompt(string code)
        {
            switch (code)
            {
                case "P01": return "어디에서 숨이 느껴졌나요?";
                case "P02": return "기억에 남는 소리가 있나요?";
                case "P03": return "닿는 곳의 느낌을 남겨요.\n건너뛰어도 괜찮아요.";
                case "P04": return "기억나는 감각을 남겨요.\n건너뛰어도 괜찮아요.";
                case "P05": return "돌아온 순간이 있었나요?\n건너뛰어도 괜찮아요.";
                case "P06": return "눈에 띈 감각을 남겨요.\n건너뛰어도 괜찮아요.";
                case "P07": return "다음에도 해볼 대상을 남겨요.\n건너뛰어도 괜찮아요.";
                default: return "";
            }
        }

        public static string Help(string code)
        {
            switch (code)
            {
                case "P01": return "숨을 깊게 쉬거나 속도를 맞출 필요는 없어요.\n고른 곳이 잘 느껴지지 않으면 더 선명한 곳을 살펴봐요.\n불편하면 눈을 뜨거나 잠시 쉬어도 돼요.";
                case "P02": return "소리를 찾아내거나 무엇인지 판단하려 애쓰지 않아도 돼요.\n뚜렷한 소리가 없으면 잠시 기다려봐요.\n주의가 벗어난 것을 알았다면 다시 들리는 소리로 돌아와요.";
                case "P03": return "감각을 만들려 힘주지 않아도 돼요.\n느낌이 흐릿해도 괜찮아요.\n불편하면 자세를 바꾸거나 쉬어요.";
                case "P04": return "한 모금은 천천히 마셔요.\n기기를 내려놓고 마셔도 돼요.\n감각이 흐릿해도 괜찮아요.";
                case "P05": return "생각이 떠올라도 괜찮아요.\n자신을 탓하거나 횟수를 세지 않아도 돼요.\n불편하면 잠시 쉬어요.";
                case "P06": return "먼저 안내를 읽고 실습해요.\n손을 말린 뒤 화면을 눌러요.\n새로운 감각이 없어도 괜찮아요.";
                case "P07": return "고른 대상의 감각을 살펴요.\n느낌이 흐릿해도 괜찮아요.\n불편하면 잠시 쉬어요.";
                default: return "";
            }
        }

        public static string PracticeTitle(string code)
        {
            switch (code)
            {
                case "P01": return "숨의 감각에 머물러요";
                case "P02": return "소리에 귀 기울여요";
                case "P03": return "닿는 감각에 머물러요";
                case "P04": return "천천히 한 모금 마셔요";
                case "P05": return "알아차리고 돌아와요";
                case "P06": return "선택한 행동을 해봐요";
                case "P07": return "고른 대상을 살펴봐요";
                default: return "편한 만큼 실습해요";
            }
        }

        public static string PracticeHint(string code)
        {
            switch (code)
            {
                case "P03": return "편한 만큼 닿는 감각을 느껴요.";
                case "P04": return "기기를 내려놓고 천천히 한 모금 마셔요.";
                case "P05": return "편한 만큼 자연스러운 숨을 느껴요.";
                case "P06": return "실습 뒤 손을 말리고 돌아와 마쳐요.";
                case "P07": return "편한 만큼 고른 감각에 머물러요.";
                default: return Reassurance(code);
            }
        }
    }
}
