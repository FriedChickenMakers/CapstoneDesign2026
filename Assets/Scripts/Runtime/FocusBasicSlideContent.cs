using System;
using System.Collections.Generic;

namespace CapstoneDesign.Runtime
{
    // Display copy only: catalog keys, options, timing, branching and saved answers
    // remain owned by the existing mission definition and session policy.
    public static class FocusBasicSlideContent
    {
        sealed class SlideCopy
        {
            public readonly string Title, Body;
            public readonly string[] Help;
            public SlideCopy(string title, string body, params string[] help)
            { Title = title; Body = body; Help = help; }
        }

        static readonly Dictionary<string, SlideCopy> Slides = new Dictionary<string, SlideCopy>(StringComparer.Ordinal)
        {
            { "P08:feeling", new SlideCopy("지금의 마음", "지금 마음을 고르거나 적어요.", "적지 않고 넘어가도 괜찮아요.") },
            { "P08:breath", new SlideCopy("자연스러운 숨", "평소처럼 자연스러운 숨을 느껴요.") },
            { "P08:finish", new SlideCopy("그대로 마무리", "지금 기분 그대로 마쳐요.", "기분이 달라지지 않아도 괜찮아요.") },

            { "P09:prepare", new SlideCopy("몸이 닿는 곳", "몸이 의자나 바닥에 닿는 곳을 찾아요.") },
            { "P09:contact", new SlideCopy("접촉 느끼기", "몸이 닿는 곳의 감각을 느껴요.", "불편하면 자세를 바꾸거나 쉬어요.") },
            { "P09:notice", new SlideCopy("몸의 느낌", "눈에 띈 몸의 느낌을 골라요.", "잘 모르겠어도 괜찮아요.") },

            { "P10:awareness", new SlideCopy("지금 살펴보기", "지금 생각·감정·몸의 감각을 살펴요.") },
            { "P10:breath", new SlideCopy("숨에 주의 모으기", "자연스러운 숨이 느껴지는 곳에 머물러요.") },
            { "P10:expand", new SlideCopy("주의 넓히기", "몸 전체와 주변으로 주의를 넓혀요.") },
            { "P10:ready", new SlideCopy("마무리하기", "준비되면 하던 일로 돌아가요.") },

            { "P11:breath", new SlideCopy("숨 느끼기", "자연스러운 숨을 잠시 느껴요.") },
            { "P11:thought", new SlideCopy("생각 알아차리기", "떠오른 생각을 ‘생각이구나’ 알아차려요.") },
            { "P11:return", new SlideCopy("호흡으로 돌아오기", "지금 느껴지는 숨으로 돌아와요.") },
            { "P11:notice", new SlideCopy("여기서 마쳐요", "생각이 없었어도 여기서 마쳐요.", "생각의 내용을 적지 않아도 괜찮아요.") },

            { "P12:experience", new SlideCopy("경험 고르기", "편하게 살펴볼 경험 하나를 골라요.") },
            { "P12:observe", new SlideCopy("경험에 머물기", "고른 경험을 잠시 느껴요.") },
            { "P12:notice", new SlideCopy("괜찮은 순간", "괜찮게 느껴지는 순간이 있었나요?", "찾지 못했어도 괜찮아요.") },

            { "P13:before", new SlideCopy("실습 전 마음", "지금 기분을 골라요.", "건너뛰어도 괜찮아요.") },
            { "P13:awareness", new SlideCopy("현재 알아차리기", "지금 생각·감정·몸의 감각을 알아차려요.") },
            { "P13:breath", new SlideCopy("숨에 주의 모으기", "자연스러운 숨에 주의를 모아요.") },
            { "P13:expand", new SlideCopy("주변으로 넓히기", "몸 전체와 주변으로 주의를 넓혀요.") },
            { "P13:after", new SlideCopy("실습 후 마음", "실습 전과 비교해 마음은 어떤가요?", "좋아져도, 같아도, 불편해져도 괜찮아요.") },

            { "P14:activity", new SlideCopy("오늘 관찰할 대상", "오늘 살펴볼 대상 하나를 골라요.") },
            { "P14:practice", new SlideCopy("잠시 머물기", "고른 대상에 잠시 머물러요.", "딴생각을 알아차리면 돌아와요.") },
            { "P14:preference", new SlideCopy("앞으로의 실습", "더 자주, 덜 자주 할 활동을 남겨요.", "활동과 원하는 빈도를 적어도 돼요.", "건너뛰어도 괜찮아요.") },

            { "P15:prepare", new SlideCopy("편한 자세", "편한 자세를 찾아요.", "숨은 평소처럼 자연스럽게 쉬어요.") },
            { "P15:breath", new SlideCopy("숨에 머물기", "숨이 느껴지는 곳에 머물러요.", "딴생각을 알아차리면 돌아와요.") },
            { "P15:kindness", new SlideCopy("나에게 건넬 말", "나에게 건네고 싶은 말을 남겨요.", "고르거나 직접 적어도 돼요.", "건너뛰어도 괜찮아요.") },

            { "P16:adjust", new SlideCopy("편하게 조절하기", "의자·등받이·발 위치를 편하게 조절해요.") },
            { "P16:contact", new SlideCopy("차례로 느끼기", "몸이 닿는 곳을 차례로 느껴요.", "불편하면 자세를 바꾸거나 쉬어요.") },
            { "P16:notice", new SlideCopy("편했던 조절", "어떤 조절이 편했나요?", "차이가 없거나 잘 모르겠어도 괜찮아요.") },

            { "P17:awareness", new SlideCopy("지금 살펴보기", "지금 생각·감정·몸의 감각을 살펴요.") },
            { "P17:breath", new SlideCopy("숨에 주의 모으기", "자연스러운 숨에 주의를 모아요.") },
            { "P17:expand", new SlideCopy("몸 전체로 넓히기", "발에서 몸 전체로 주의를 넓혀요.") },
            { "P17:activity", new SlideCopy("필요한 행동", "나를 돕는 행동 하나를 골라요.") },
            { "P17:action", new SlideCopy("선택한 행동 하기", "고른 행동을 편한 만큼 해요.") },

            { "P18:activity", new SlideCopy("오늘 편한 대상", "호흡과 소리 중 편한 대상을 골라요.") },
            { "P18:practice", new SlideCopy("필요하면 바꿔요", "편한 만큼 고른 대상을 살펴요.", "실습 중 호흡이나 소리로 바꿔도 돼요.", "딴생각을 알아차리면 돌아와요.", "불편하면 바꾸거나 잠시 쉬어요.") },
            { "P18:notice", new SlideCopy("살펴본 대상", "살펴본 대상을 골라요.", "바꾸거나 쉬었어도 괜찮아요.", "고르지 않아도 괜찮아요.") },

            { "P19:route", new SlideCopy("오늘의 실습 선택", "산 명상이나 짧은 소리 실습을 골라요.", "고르지 않으면 산 명상으로 이어져요.") },
            { "P19:mountain-prepare", new SlideCopy("몸의 접촉 느끼기", "편한 자세로 바닥에 닿는 몸을 느껴요.") },
            { "P19:mountain-shape", new SlideCopy("산을 떠올려요", "산의 모양과 넓은 밑부분을 떠올려요.") },
            { "P19:mountain-weather", new SlideCopy("지나가는 날씨", "햇빛·구름·비가 산을 지나는 모습을 떠올려요.") },
            { "P19:mountain-change", new SlideCopy("변하는 경험", "생각·감정이 날씨처럼 변하는지 살펴요.") },
            { "P19:mountain-return", new SlideCopy("지금으로 돌아오기", "닿는 감각과 자연스러운 숨으로 돌아와요.") },
            { "P19:sound-prepare", new SlideCopy("편한 자세 찾기", "몸을 편하게 두어요.") },
            { "P19:sound-listen", new SlideCopy("변하는 소리", "가깝고 먼 소리의 변화를 들어요.") },
            { "P19:notice", new SlideCopy("떠오른 느낌", "편했던 점이나 들린 소리를 남겨요.", "고르거나 직접 적어도 돼요.", "건너뛰어도 괜찮아요.") },

            { "P20:activity", new SlideCopy("실습 대상 고르기", "호흡과 소리 중 편한 대상을 골라요.") },
            { "P20:practice", new SlideCopy("잠시 관찰하기", "고른 대상에 잠시 머물러요.", "딴생각을 알아차리면 돌아와요.") },
            { "P20:action", new SlideCopy("오늘 나를 위한 일", "나를 위해 한 작은 행동을 남겨요.", "고르거나 직접 적어도 돼요.", "잘 모르겠다면 건너뛰어도 괜찮아요.") },

            { "P21:activity", new SlideCopy("내게 편한 활동", "편하게 할 활동을 골라요.") },
            { "P21:practice", new SlideCopy("직접 해보기", "고른 활동을 편한 만큼 해요.", "딴생각을 알아차리면 돌아와요.") },
            { "P21:save", new SlideCopy("내 쉬는 방법", "이 활동을 내 쉬는 방법으로 남길까요?", "이번에는 건너뛰어도 괜찮아요.") },

            { "P22:activity", new SlideCopy("안전한 방법 선택", "안전한 걷기나 앉아서 살피기를 골라요.", "불편하면 앉아서 살펴도 돼요.") },
            { "P22:contact", new SlideCopy("닿는 곳 느끼기", "발이나 몸이 닿는 감각을 느껴요.") },
            { "P22:move", new SlideCopy("달라지는 접촉", "움직일 때 닿는 감각의 변화를 살펴요.") },
            { "P22:notice", new SlideCopy("눈에 띈 감각", "눈에 띈 감각을 골라요.", "뚜렷한 감각이 없어도 괜찮아요.") },

            { "P23:prepare", new SlideCopy("한입 준비", "먹을 수 있는 음식 한입을 준비해요.") },
            { "P23:observe", new SlideCopy("향과 모양", "먹기 전 향과 모양을 살펴요.") },
            { "P23:bite", new SlideCopy("한입 느끼기", "한입의 온도와 질감을 느껴요.") },
            { "P23:sensation", new SlideCopy("감각 돌아보기", "눈에 띈 감각을 남겨요.", "특별한 감각이 없어도 괜찮아요.", "선택하지 않아도 괜찮아요.") },

            { "P24:route", new SlideCopy("들을 이야기 선택", "상대나 앱의 짧은 이야기를 골라요.", "고르지 않으면 앱 이야기로 이어져요.") },
            { "P24:listen", new SlideCopy("들리는 말에 주의 두기", "지금 들리는 말에 귀 기울여요.", "답은 듣고 난 뒤에 생각해도 돼요.") },
            { "P24:notice", new SlideCopy("기억나는 말이나 장면", "기억나는 말이나 장면을 남겨요.", "떠오르지 않으면 적지 않아도 돼요.") },

            { "P25:pause", new SlideCopy("잠깐 멈추기", "말하거나 보내기 전에 숨을 한 번 골라요.") },
            { "P25:message", new SlideCopy("전할 말 확인", "전할 핵심을 한 문장으로 떠올려요.", "속으로만 정리해도 괜찮아요.") },
            { "P25:speak", new SlideCopy("이제 전하기", "이제 말하거나 메시지를 보내요.") },
            { "P25:check", new SlideCopy("순서 확인", "멈춘 뒤 내용을 확인했나요?") },

            { "P26:route", new SlideCopy("도움 떠올리기", "사람·물건·환경에서 도움받은 일이 있나요?", "떠오르지 않아도 괜찮아요.") },
            { "P26:help", new SlideCopy("어떤 도움이었나요?", "떠오른 도움의 종류를 골라요.") },
            { "P26:sound", new SlideCopy("소리 하나 관찰", "주변 소리 하나를 살펴요.", "도움이 떠오르지 않아도 괜찮아요.") },
            { "P26:check", new SlideCopy("해본 경로 확인", "앞에서 고른 경험을 돌아봐요.") },

            { "P27:trigger", new SlideCopy("생활 계기 고르기", "늘 하던 생활 행동을 골라요.", "직접 정해도 괜찮아요.") },
            { "P27:activity", new SlideCopy("이어 할 실습", "생활 행동 뒤 이어 할 실습을 골라요.") },
            { "P27:wait", new SlideCopy("행동 뒤 돌아오기", "정한 행동을 마친 뒤 돌아와요.", "나중에도 여기서 이어갈 수 있어요.", "이미 마쳤다면 바로 이어가도 돼요.") },
            { "P27:practice", new SlideCopy("생활 뒤 짧은 실습", "고른 실습을 편한 만큼 해요.", "딴생각을 알아차리면 돌아와요.") },
            { "P27:notice", new SlideCopy("부담 없는 방식", "실습한 때와 방식은 어땠나요?", "다음에 바꿔도 괜찮아요.") },

            { "P28:activity", new SlideCopy("편한 실습 선택", "해본 실습 중 하나를 골라요.") },
            { "P28:practice", new SlideCopy("한 번 더 실습", "고른 실습을 편한 만큼 해요.", "딴생각을 알아차리면 돌아와요.") },
            { "P28:next-activity", new SlideCopy("다음 주의 활동", "다음 주에 다시 할 활동을 골라요.") },
            { "P28:time", new SlideCopy("부담 없는 시간", "편한 시간대를 골라요.", "시간은 나중에 정해도 괜찮아요.") },
            { "P28:review", new SlideCopy("나의 계획 확인", "고른 활동과 시간대를 확인해요.", "이전 단계에서 선택을 바꿀 수 있어요.") }
        };

        static bool TryGet(string code, string key, out SlideCopy copy)
            => Slides.TryGetValue((code ?? "") + ":" + (key ?? ""), out copy);

        public static string Title(string code, string key, string fallback)
            => TryGet(code, key, out var copy) ? copy.Title : fallback;

        public static string Body(string code, string key, string fallback, string choice = "")
        {
            if (!TryGet(code, key, out var copy)) return fallback;
            if (code == "P17" && key == "action")
            {
                if (choice == "물 마시기") return "편한 만큼 물을 마셔요.";
                if (choice == "쉬기") return "필요한 만큼 잠시 쉬어요.";
                if (choice == "창밖 보기") return "편한 만큼 창밖을 바라봐요.";
            }
            if (code == "P22" && key == "contact")
            {
                if (choice == "걷기") return "안전한 곳에서 발의 접촉을 느껴요.";
                if (choice == "앉아서 살피기") return "앉아서 몸이 닿는 감각을 느껴요.";
            }
            if (code == "P22" && key == "move")
            {
                if (choice == "걷기") return "걸음을 옮길 때 발의 접촉을 살펴요.";
                if (choice == "앉아서 살피기") return "자세를 바꿀 때 닿는 감각을 살펴요.";
            }
            if (code == "P24" && key == "listen")
            {
                if (choice == "상대의 이야기") return "상대가 지금 하는 말에 귀 기울여요.";
                if (string.IsNullOrEmpty(choice) || choice == "앱의 짧은 이야기")
                    return "앱의 짧은 이야기에 귀 기울여요.";
            }
            if (key == "practice" && (code == "P14" || code == "P18" || code == "P20"
                || code == "P21" || code == "P27" || code == "P28"))
            {
                if (choice == "호흡") return "자연스러운 숨을 느껴요.";
                if (choice == "몸의 접촉" && (code == "P14" || code == "P21" || code == "P27" || code == "P28"))
                    return "의자나 바닥에 닿는 감각을 느껴요.";
                if (code == "P21" ? choice == "소리" : choice == "주변 소리")
                    return "지금 들리는 소리에 귀 기울여요.";
                if ((code == "P21" || code == "P28") && choice == "잠시 멈춤")
                    return "하던 일을 멈추고 잠시 쉬어요.";
            }
            return copy.Body;
        }

        public static string[] Help(string code, string key)
            => TryGet(code, key, out var copy) ? (string[])copy.Help.Clone() : Array.Empty<string>();

        // Review values remain saved record values; only their on-screen context
        // is supplied here after the repeated heading has been removed.
        public static string ReviewBody(string code, string recordKey)
        {
            if (code == "P28")
            {
                if (recordKey == "activity") return "이번에 한 실습을 확인해요.";
                if (recordKey == "next-activity") return "다음 주 실습을 확인해요.";
                if (recordKey == "time") return "이어갈 시간대를 확인해요.";
            }
            if (code == "P26")
            {
                if (recordKey == "route") return "도움이 떠올랐는지 확인해요.";
                if (recordKey == "help") return "선택한 도움의 종류를 확인해요.";
            }
            return "남긴 내용을 확인해요.";
        }
    }
}
