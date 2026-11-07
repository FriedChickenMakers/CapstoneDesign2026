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
                case "P03": return "편하게 앉거나 누워, 몸이 의자나 바닥에 닿는 곳을 살펴봐요.";
                case "P04": return "편한 온도의 음료를 준비하고, 안내를 먼저 읽어요.";
                case "P05": return "편한 자세에서 자연스러운 숨을 느낄 준비를 해요.";
                case "P06": return "안내를 먼저 읽고, 기기를 물이 닿지 않는 곳에 두어요.";
                case "P07": return "오늘 부담이 적은 관찰 대상을 골라요.";
                default: return "";
            }
        }

        public static string ChoiceLabel(string code)
        {
            switch (code)
            {
                case "P01": return "숨을 느낄 곳";
                case "P06": return "지금 할 행동";
                case "P07": return "오늘 관찰할 대상";
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
                        "발바닥이나 등처럼 의자나 바닥에 닿는 곳 하나를 골라요.",
                        "그곳의 압력이나 온도를 더 강하게 만들려 하지 않고 느껴요.",
                        "주의가 벗어나면 접촉으로 돌아오고, 불편하면 자세를 바꿔요."
                    };
                case "P04":
                    return new[]
                    {
                        "컵을 잡은 손의 감촉과 컵의 온도를 느껴봐요.",
                        "천천히 한 모금을 마시며 입안의 맛과 온도를 살펴봐요.",
                        "삼킨 뒤 남은 맛이나 느낌을 살펴보고, 다음 모금을 서두르지 않아요."
                    };
                case "P05":
                    return new[]
                    {
                        "숨이 느껴지는 한 곳에서 자연스러운 움직임에 잠시 머물러요.",
                        "생각이나 감각으로 주의가 옮겨가면, 벗어났음을 알아차려요.",
                        "자신을 탓하거나 횟수를 세지 않고, 부드럽게 호흡으로 돌아와요."
                    };
                case "P06":
                    if (choice == "설거지")
                        return new[]
                        {
                            "그릇을 잡은 손의 감촉과 손에 닿는 물의 온도를 느껴봐요.",
                            "닦는 움직임과 그릇 표면의 감촉, 물소리를 살펴봐요.",
                            "다른 생각이 들면 지금 닦고 헹구는 감각으로 돌아와요."
                        };
                    return new[]
                    {
                        "손을 씻으며 손에 닿는 물의 온도를 살펴봐요.",
                        "손을 문지를 때의 움직임과 감촉, 흐르는 물소리를 살펴봐요.",
                        "다른 생각이 들면 지금 씻고 헹구는 감각으로 돌아와요."
                    };
                case "P07":
                    if (choice == "몸의 접촉") return Guidance("P03", null);
                    if (choice == "주변 소리") return Guidance("P02", null);
                    return Guidance("P01", null);
                default: return Array.Empty<string>();
            }
        }

        public static string Reassurance(string code)
        {
            switch (code)
            {
                case "P01": return "숨의 감각이 뚜렷하지 않아도 괜찮아요.";
                case "P02": return "소리의 원인이나 이름을 맞힐 필요는 없어요.";
                case "P03": return "특별한 감각이 느껴지지 않아도 괜찮아요.";
                case "P04": return "새로운 맛이나 감각을 찾아내지 않아도 괜찮아요.";
                case "P05": return "생각이 떠오르지 않으면 호흡에 그대로 머물러도 좋아요.";
                case "P06": return "평소와 다른 감각을 찾아내지 않아도 괜찮아요.";
                case "P07": return "잘 집중하려 애쓰지 않아도, 벗어난 뒤 돌아오면 돼요.";
                default: return "";
            }
        }

        public static string ReflectionPrompt(string code)
        {
            switch (code)
            {
                case "P01": return "어디에서 숨이 느껴졌나요?";
                case "P02": return "기억에 남는 소리가 있나요?";
                case "P03": return "몸이 닿는 곳에서 어떤 느낌이 있었나요?";
                case "P04": return "손의 감촉, 온도, 맛 중 눈에 띈 감각이 있나요?";
                case "P05": return "주의가 벗어났다가 돌아온 순간이 있었나요?";
                case "P06": return "행동하는 동안 어떤 감각이 눈에 띄었나요?";
                case "P07": return "다음에도 해보고 싶은 대상이 있나요?";
                default: return "";
            }
        }

        public static string Help(string code)
        {
            switch (code)
            {
                case "P01": return "숨을 깊게 쉬거나 속도를 맞출 필요는 없어요.\n고른 곳이 잘 느껴지지 않으면 더 선명한 곳을 살펴봐요.\n불편하면 눈을 뜨거나 잠시 쉬어도 돼요.";
                case "P02": return "소리를 찾아내거나 무엇인지 판단하려 애쓰지 않아도 돼요.\n뚜렷한 소리가 없으면 잠시 기다려봐요.\n주의가 벗어난 것을 알았다면 다시 들리는 소리로 돌아와요.";
                case "P03": return "감각을 만들려고 몸에 힘을 줄 필요는 없어요.\n느낌이 없으면 닿아 있다는 사실만 알아차려도 좋아요.\n불편하면 자세를 조절하거나 다른 부위를 살펴봐요.";
                case "P04": return "손과 컵, 한 모금, 삼킨 뒤의 감각을 차례로 살펴봐요.\n기기를 내려놓고 마신 뒤 돌아와도 돼요.\n느낌이 흐릿하면 그대로 마쳐도 좋아요.";
                case "P05": return "생각을 없애거나 마음을 비우는 연습은 아니에요.\n벗어난 것을 알아차리면 부드럽게 호흡으로 돌아와요.\n잘했는지 평가하거나 돌아온 횟수를 셀 필요는 없어요.";
                case "P06": return "안내를 읽고, 기기를 물이 닿지 않는 곳에 두어요.\n젖은 손으로 화면을 조작할 필요는 없어요.\n평소와 다른 감각을 찾아내지 않아도 괜찮아요.";
                case "P07": return "호흡은 코·가슴·배, 접촉은 의자나 바닥에 닿는 감각을 느껴요.\n소리를 골랐다면 크기와 시작·끝을 들어봐요.\n느낌이 불분명해도 괜찮고, 불편하면 잠시 쉬어도 돼요.";
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
                case "P04": return "기기를 내려놓고 한 모금을 마신 뒤 돌아와 실습을 마쳐요.";
                case "P06": return "기기를 물이 닿지 않게 두고 실습한 뒤, 손을 말리고 돌아와 마쳐요.";
                default: return Reassurance(code);
            }
        }
    }
}
