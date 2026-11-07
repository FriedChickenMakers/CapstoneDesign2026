using System;
using System.Linq;
namespace CapstoneDesign.Runtime.LocalState
{
    public static partial class FocusMissionCatalog
    {
        // Historical texture-rework definitions only. New sessions use the guided version 4.
        // Partial declarations have no guaranteed field initialization order.
        static readonly Lazy<FocusMissionDefinition[]> TextureV3=
            new Lazy<FocusMissionDefinition[]>(BuildTextureVersion);
        static FocusMissionDefinition[] BuildTextureVersion()
        {
            var definitions=AllV2.Select(m=>m.Copy()).ToArray();
            foreach(var definition in definitions)
            {
                definition.ContentVersion=TextureVersion;
                definition.PurposeId="focus-purpose-v1-"+definition.Code;
                definition.Purpose=PurposeFor(definition.Code);
                definition.EvidenceBasis="product-purpose-review-2026-11-05";
                definition.ReviewStatus="product-reviewed-clinical-review-pending";
            }
            Action<string,string,string,string> text=(code,key,title,body)=>{
                var step=definitions.First(m=>m.Code==code).Steps.First(s=>s.Key==key);
                if(title!=null)step.Title=title;step.Body=body;
            };
            text("P03","contact",null,"몸이 의자나 바닥에 닿는 곳의\n감각을 살펴요. 전신을 훑지 않아도 돼요.");
            text("P09","prepare","지금 마음과 몸","지금의 감정을 속으로 살펴봐요.\n떠오르는 이름이 없어도 괜찮아요.");
            text("P09","contact","감정과 함께 느껴지는 몸","지금 마음과 함께 몸에서\n어떤 감각이 느껴지는지 살펴요.");
            text("P09","notice",null,"감정과 함께 눈에 띈 몸의 느낌이 있나요?\n연결이 뚜렷하지 않아도 괜찮아요.");
            text("P12","observe",null,"고른 경험의 감각을 살펴봐요.\n편안해지거나 좋은 기분을 만들 필요는 없어요.");
            text("P18","practice",null,"고른 대상을 살피고 주의가 벗어나면 돌아와요.\n대상을 바꾸거나 쉰 뒤에도 다시 살펴요.");
            text("P18","notice",null,"실제로 살펴본 대상을 떠올려요.\n쉬기만 했다면 미션을 넘겨도 괜찮아요.");
            text("P19","route",null,"산 명상을 해보거나 소리 실습으로 옮겨요.\n소리 실습은 별도 활동으로 기록해요.");
            text("P19","mountain-change",null,"생각과 감정은 날씨처럼 달라져요.\n없애려 하지 않고 산처럼 머물러봐요.");
            text("P21","practice",null,"고른 대상의 감각을 살펴요. 잠시 멈춤은\n몸이 닿는 곳을 느껴요. 편안해질 필요는 없어요.");
            text("P24","route",null,"상대 또는 앱 이야기를 골라요.\n앱 이야기는 대화 대신 듣는 중 반응을 살펴요.");
            text("P24","listen",null,"답하려는 생각과 몸의 반응을 알아차리면\n지금 들리는 말로 부드럽게 돌아와요.");
            text("P24","notice","듣는 동안의 내 반응","듣는 중 생각이나 몸의 반응이 있었나요?\n내용을 잘 기억해야 하는 실습은 아니에요.");
            definitions.First(m=>m.Code=="P24").Steps.Last().InputHint="떠오른 생각이나 몸의 반응 · 선택";
            text("P25","pause",null,"잠시 멈춰 지금의 마음과 몸을 살펴요.\n숨을 억지로 바꾸지는 않아요.");
            text("P25","message",null,"전하려는 말과 이유를 살펴요.\n그대로 말할지 바꿀지 속으로 생각해봐요.");
            var speak=definitions.First(m=>m.Code=="P25").Steps.First(step=>step.Key=="speak");
            speak.Title="어떻게 전할지 선택";speak.Body="말하기·고치기·나중에 전하기·전하지 않기 중\n지금 선택한 방법을 실제로 해봐요.";
            speak.Kind="choice";speak.Options=O("말하거나 보냈어요","고쳐서 전했어요","나중에 전하기","전하지 않기");speak.ActionLabel="선택 확인";speak.RequiredAnswer=true;
            text("P25","check",null,"마음과 몸을 살핀 뒤 방법을 선택했나요?\n말하지 않기로 한 선택도 남길 수 있어요.");
            text("P26","route",null,"작은 도움이 있었나요? 떠오르지 않으면\n별도 소리 실습을 고를 수 있어요.");
            text("P28","review",null,"활동과 시간대를 확인해요. 놓친 날이 있어도\n다음 편한 때 다시 시작해요. 알림 설정과는 별개예요.");
            var plan=definitions.First(m=>m.Code=="M10").Steps.First(step=>step.Key=="plan");
            plan.Kind="plan";plan.Title="작은 행동과 편한 시간대";
            plan.Body="할 일과 편한 시간대를 골라요.\n정확한 시각이나 문장을 적지 않아도 돼요.";
            plan.InputHint="직접 정하고 싶다면 적어요 · 선택";
            text("M10","review","계획 확인","고른 계획을 확인해요.\n알림 시각과 별개로 편할 때 실천하면 돼요.");
            return definitions;
        }
        static string PurposeFor(string code)
        {
            switch(code)
            {
                case "P01":return "자연스러운 호흡 감각 알아차림";
                case "P02":return "주변 소리와 변화 알아차림";
                case "P03":return "몸의 접촉 감각 알아차림";
                case "P04":return "마시는 한 모금의 감각 경험";
                case "P05":return "주의 이탈 알아차림과 부드러운 복귀";
                case "P06":return "일상 행동 중 현재 감각 알아차림";
                case "P07":case "P14":return "직접 실습한 방법의 선호 탐색";
                case "P08":return "현재 감정을 알아차리고 바꾸지 않아도 되는 경험";
                case "P09":return "현재 감정과 함께 나타나는 몸 감각 알아차림";
                case "P10":return "현재 경험 알아차림, 호흡에 모음, 몸으로 주의 넓힘";
                case "P11":return "생각을 정신적 사건으로 알아차리고 현재로 복귀";
                case "P12":return "일상의 작은 경험 관찰, 편안해짐은 성공 조건 아님";
                case "P13":return "세 단계 주의 실습과 비평가적 변화 확인";
                case "P15":return "현재 경험에 친절한 태도";
                case "P16":return "자세 조절 후 몸의 접촉 감각 관찰";
                case "P17":return "현재 경험 알아차림 뒤 필요한 행동 선택과 실행";
                case "P18":return "가능한 기준점 관찰, 이탈 인식, 복귀";
                case "P19":return "산의 비유로 변하는 경험에 머무는 태도와 수용";
                case "P20":return "작은 자기돌봄 시도 알아차림과 인정";
                case "P21":return "가능한 관찰 방법을 경험하고 다시 사용할 방법 선택";
                case "P22":return "움직임과 자세 변화의 접촉 감각 관찰";
                case "P23":return "먹는 한입의 감각 직접 경험";
                case "P24":return "듣는 중 자기 반응을 알아차리고 들리는 말로 복귀";
                case "P25":return "몸과 마음 알아차림 뒤 전달 방식의 의도적 선택";
                case "P26":return "일상에서 실제 작은 도움 알아차림";
                case "P27":return "생활 계기와 실습 실행 연결";
                case "P28":return "경험한 실습의 재수행과 유지 계획";
                case "M01":return "주변 밝기를 바꾸는 일상 환경 지원";
                case "M02":return "창밖의 색과 모양, 움직임 관찰";
                case "M03":return "물 마시기 자기돌봄";
                case "M04":return "얼굴 씻기 일상 행동";
                case "M05":return "편한 범위의 몸 움직임";
                case "M06":return "물건 하나 정리하는 행동 시작";
                case "M07":return "현재 기분 표현";
                case "M08":return "선호하는 음악 듣기";
                case "M09":return "잠시 휴식하기";
                case "M10":return "작은 실행 행동과 편한 시간대 계획";
                case "M11":return "자유 창작과 여가";
                case "M12":return "주변 식물의 시각적 감각 관찰";
                case "M13":return "괜찮았던 작은 경험 회고";
                case "M14":return "관심 있는 글 읽기";
                case "M15":return "실제로 걷는 활동";
                case "M16":return "공원 외출과 자연 접촉";
                case "M17":return "자연호흡의 감각 관찰";
                default:throw new ArgumentException("Unknown focus mission purpose.");
            }
        }
    }
}
