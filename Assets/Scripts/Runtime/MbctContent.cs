using System;
using System.Collections.Generic;
using System.Linq;

namespace CapstoneDesign.Runtime
{
    [Serializable]
    public sealed class MbctStep
    {
        public string title, guidance, answerKey;
        public string[] options = Array.Empty<string>();
        public MbctStep(string title, string guidance, string key = null, params string[] options)
        { this.title=title; this.guidance=guidance; answerKey=key; this.options=options; }
    }

    // App-authored short practices; the schedule is an adaptation, not a clinical MBCT protocol.
    public static class MbctContent
    {
        public const int Weeks=8, DaysPerWeek=6, Count=Weeks*DaysPerWeek;
        public static readonly string[] WeekTitles={"자동 행동 알아차리기","몸과 일상에 주의 기울이기","잠시 멈추고 돌아보기","생각 알아차리기","생각과 대응 사이의 여지","나의 경험과 변화 신호","나를 돌보는 대응","생활 속에서 이어가기"};
        public static readonly string[] Purposes={"",
            "익숙한 먹기 행동의 자동 반응을 알아차리고 현재 감각을 경험해요.",
            "몸 감각을 관찰하고 주의가 벗어나면 부드럽게 돌아와요.",
            "반복하는 생활 행동에서 자동 모드를 알아차리고 현재로 돌아와요.",
            "자연스러운 호흡을 기준점으로 삼아 주의를 다시 가져와요.",
            "움직임의 몸 감각을 알아차리고 몸의 한계를 존중해요.",
            "즐거운 경험에서 몸·감정·생각을 구분해 알아차려요.",
            "불편한 경험과 그때의 몸·감정·생각·반응을 구분해요.",
            "현재 경험에서 호흡으로 주의를 모은 뒤 몸 전체로 넓혀 대응의 여지를 만들어요.",
            "소리와 생각이 나타나고 지나가는 것을 관찰하고 해석과 거리를 두어요.",
            "생각을 마음의 사건으로 알아차리고 자동으로 따라가기 전에 대응을 선택해요.",
            "활동과 기분의 관계를 알아차리고 나를 돌보는 행동을 선택해요.",
            "개인적인 변화 신호를 알아차리고 도움이 되는 행동과 지원을 계획해요.",
            "나에게 맞는 실습을 생활에 연결하고 다시 시작할 방법을 마련해요."};

        public static readonly IReadOnlyList<MissionDefinition> Course=Build();
        public static MissionDefinition Find(string id) => Course.FirstOrDefault(m=>m.id==id);
        static MbctStep S(string title,string text,string key=null,params string[] choices) => new MbctStep(title,text,key,choices);
        static IReadOnlyList<MissionDefinition> Build()
        {
            // Six learning practices per week; experience diaries (6/7) live in the record system.
            int[][] families={new[]{1,2,4,3,2,13},new[]{1,3,5,4,2,3},new[]{1,8,5,9,8,3},new[]{1,9,10,8,10,2},
                new[]{10,10,10,11,8,9},new[]{10,10,11,12,2,8},new[]{12,12,1,11,9,8},new[]{13,12,3,2,8,13}};
            string[][] variants={new[]{"물","","","","","초기"},new[]{"과자","","","","",""},new[]{"김치","","","소리","",""},new[]{"밥알","생각","A","","B",""},
                new[]{"C","D","E","","","생각"},new[]{"개인A","개인전체","","신호","",""},new[]{"행동","지원","식사","","소리",""},new[]{"선호","검토","","","","유지"}};
            var result=new List<MissionDefinition>();
            for(int w=0;w<Weeks;w++) for(int d=0;d<DaysPerWeek;d++)
            {
                int f=families[w][d], order=w*DaysPerWeek+d+1; string variant=variants[w][d];
                var steps=Steps(f,variant); string title=Title(f,variant);
                result.Add(new MissionDefinition {id="course:MBCT"+order.ToString("00"),sourceId="MBCT"+order.ToString("00"),type="course",
                    title=title,week=w+1,recommendedOrder=order,mbctActivity=f,purpose=Purposes[f],steps=steps,
                    instructions=string.Join("\n",steps.Select(s=>s.guidance)),textOnlyInstructions=steps[0].guidance,
                    suggestedDurationSeconds=f==2||f==8?180:120,suggestedDurationMinSeconds=60,suggestedDurationMaxSeconds=300,
                    suggestedDurationLabel=f==2||f==8?"약 3분":"약 1~5분",source="MBCT 원리 참고 / 앱 자체 한국어 안내",
                    contentVersion="mbct-localized-2026-10-03-v1",licenseReviewStatus="APP_AUTHORED_DRAFT_REVIEW_PENDING",audioAvailable=false});
            }
            return result.AsReadOnly();
        }
        static string Title(int f,string v)
        {
            switch(f)
            {
                case 1:return v=="물"?"한 모금 알아차리기":v=="식사"?"평소 식사의 한 입":"한 입 알아차리기 · "+v;
                case 2:return "몸을 차례로 살펴보기";case 3:return "익숙한 행동 새롭게 하기";case 4:return "자연스러운 숨 관찰";
                case 5:return "몸을 움직이며 알아차리기";case 8:return "잠시 멈추고 돌아보기";
                case 9:return v=="소리"?"주변 소리 들어보기":"오늘의 생각 알아차리기";
                case 10:return v=="A"?"상황과 생각 구분":v=="B"?"생각에 이름 붙이기":v=="C"?"생각과 몸·감정":v=="D"?"잠시 감각으로 돌아오기":v=="E"?"다음 행동 선택":v=="개인A"?"나의 가벼운 사례 선택":"나의 생각과 대응";
                case 11:return "기운을 주는 활동 살펴보기";
                case 12:return v=="신호"?"나의 변화 신호":v=="행동"?"신호와 작은 대응":v=="지원"?"도움을 받을 곳":"나의 대응 계획 다시 보기";
                case 13:return v=="초기"?"실습할 때 정하기":v=="선호"?"다시 하고 싶은 실습":"생활 속 실습 이어가기";
                default:throw new ArgumentException("Unknown MBCT activity");
            }
        }
        public static MbctStep[] Steps(int f,string v)
        {
            switch(f)
            {
                case 1:return new[]{S("오늘의 소재","오늘은 "+v+"로 시작해보세요. 준비가 어렵다면 물이나 익숙한 다른 음식, 전에 사용한 소재도 괜찮아요.","food","물","과자","김치","밥알"),
                    S("먹기 전","모양과 색을 보고 손이나 컵의 접촉을 느껴보세요. 음식이라면 향도 살펴보세요."),
                    S("한 입 또는 한 모금","작은 양을 편하게 입에 넣어 온도와 맛, 접촉을 관찰해보세요. 음식은 씹는 감각, 물은 머금는 감각을 살펴보세요."),
                    S("삼킨 뒤","편하게 삼킨 뒤 입안의 변화를 알아차려보세요. 특별한 감각이 없어도 괜찮아요.")};
                case 2:return new[]{S("편한 자세","편하게 앉거나 누워보세요. 눈은 편한 대로 두세요. 불편하면 부위를 바꾸거나 중단해도 괜찮아요."),
                    S("발","바닥이나 옷과 닿는 발의 압력·온도를 살펴보세요. 감각이 뚜렷하지 않아도 괜찮아요."),
                    S("다리와 엉덩이","몸을 받치는 의자나 바닥의 접촉을 알아차려보세요."),S("배와 가슴","자연스러운 숨에 따라 움직이는 감각을 관찰하세요. 숨을 바꾸려 하지 않아도 돼요."),
                    S("손과 어깨와 얼굴","각 부위에 잠시 주의를 기울여보세요. 딴생각을 알아차리면 다시 몸으로 돌아와요."),S("몸 전체","몸 전체의 자세와 접촉을 느껴보세요. 편안해져야 하는 것은 아니에요.")};
                case 3:return new[]{S("익숙한 행동 선택","전에 고른 행동을 다시 하거나 새 행동을 적어보세요.","routine","양치하기","손 씻기","물 마시기","옷 입기"),
                    S("행동하며 관찰","선택한 행동을 편하게 하며 촉감·온도·움직임을 살펴보세요. 생각이 다른 곳으로 가면 현재의 감각으로 돌아와요.")};
                case 4:return new[]{S("자연스러운 숨","편한 자세에서 숨을 바꾸지 않고 배나 가슴의 움직임을 느껴보세요."),S("다시 돌아오기","주의가 벗어난 것을 알아차리면 부드럽게 돌아오세요. 호흡이 불편하면 발의 접촉이나 소리를 관찰해도 괜찮아요.")};
                case 5:return new[]{S("움직임 선택","몸과 주변 상황에 맞게 골라보세요.","movement","앉아서 가볍게 움직이기","편한 곳에서 걷기"),S("움직이며 관찰","앉아 있다면 가능한 만큼 손이나 어깨를 천천히 움직여보세요. 걷는다면 발을 들고 디딜 때 접촉과 체중 이동을 알아차려보세요. 아프면 멈춰요.")};
                case 8:return new[]{S("1 지금 알아차리기","지금 어떤 생각·감정·몸 감각이 있나요? 좋고 나쁨을 정하기 전에 알아차려보세요."),S("2 호흡으로 모으기","자연스러운 숨의 감각에 잠시 주의를 모아보세요."),S("3 몸 전체로 넓히기","발의 접촉과 몸 전체의 자세로 주의를 넓혀보세요. 이 알아차림을 가지고 다음 순간으로 돌아가요.")};
                case 9:return v=="소리"?new[]{S("주변 소리","가까운 소리와 먼 소리를 들어보세요. 집안이 너무 조용하다면, 가능할 때 창문을 열고 바깥 소리에 귀 기울여봐도 좋아요."),S("변화 듣기","소리의 크기와 길이, 나타나고 사라지는 변화를 관찰해보세요. 소리의 원인을 생각했다면 실제 들리는 소리로 돌아와요.")}:
                    new[]{S("떠오른 생각","지금 실제로 떠오른 생각을 알아차려보세요. 생각을 정하거나 찾아낼 필요는 없어요."),S("지나가는 생각","생각이 변하거나 다른 생각이 떠오르는 것을 관찰해보세요. 따라가고 있었다면 호흡이나 접촉 감각으로 돌아와요."),S("선택 기록","원하면 알아차린 생각을 한 줄 적어보세요. 분석하거나 해결하지 않아도 돼요.","observed-thought")};
                case 10:return ThoughtSteps(v);
                case 11:return new[]{S("최근 활동","최근 했던 활동 하나를 적거나 선택해보세요.","energy-activity","산책","SNS 보기","친구와 통화"),S("활동 뒤 느낌","같은 활동도 날마다 다를 수 있어요. 지금 경험을 기준으로 골라보세요.","energy-effect","기운을 줌","소모함","둘 다","모르겠음"),S("작은 행동 선택","오늘 나를 돌보기 위해 해볼 작은 행동을 적어보세요. 이 선택과 실제 실행은 별개예요.","care-action")};
                case 12:
                    if(v=="신호")return new[]{S("개인적인 변화","내 상태가 나빠지기 전에 나타났던 변화가 있나요? 이미 아는 것만 적어도 되고 건너뛰어도 돼요.","warning-sign")};
                    if(v=="행동")return new[]{S("나의 변화 신호","전에 적은 내용을 보거나 바꿔보세요.","warning-sign"),S("작은 대응","이 신호를 알아차렸을 때 도움이 될 행동은 무엇인가요? 예: 상태를 알리기, 치료진에게 연락하기.","warning-action")};
                    if(v=="지원")return new[]{S("도움을 받을 곳","믿을 만한 사람이나 치료진 등 도움을 받을 곳을 적어보세요. 연락은 앱이 대신 보내지 않아요.","support")};
                    return new[]{S("변화 신호 다시 보기","개인적인 변화 신호를 검토해보세요. 이것은 앱의 진단이 아니에요.","warning-sign"),S("대응 다시 보기","도움이 될 행동을 검토해보세요.","warning-action"),S("지원 다시 보기","도움을 받을 곳을 검토해보세요.","support")};
                case 13:
                    if(v=="초기")return new[]{S("언제 해볼까요","긴 약속 대신 생활 속 작은 계기를 골라보세요.","practice-time","양치 후","아침에 일어난 뒤","잠들기 전"),S("알림 선호","알림 선호를 기록해요. 현재 데모는 이 선택으로 알림을 예약하지 않아요.","practice-reminder","원하지 않아요","나중에 정할래요","원해요")};
                    if(v=="선호")return new[]{S("다시 하고 싶은 실습","실제로 해본 활동 중 편했던 것을 골라보세요.","favorite","몸 살펴보기","호흡 관찰","일상 행동","잠시 멈추기")};
                    return new[]{S("나에게 맞는 실습","다시 하고 싶은 활동을 적어보세요.","favorite"),S("생활 속 계기","언제 이 활동을 해볼까요?", "practice-time"),S("다시 시작하기","놓친 날에 부담 없이 다시 시작할 방법을 적어보세요.","restart-plan")};
                default:throw new ArgumentException("Unsupported activity");
            }
        }
        static MbctStep[] ThoughtSteps(string v)
        {
            bool personal=v.StartsWith("개인",StringComparison.Ordinal);
            var list=new List<MbctStep>();
            list.Add(S("연습 상황",personal?"제시된 예시를 쓰거나 자신의 가벼운 일상 사례를 선택하세요. 힘든 경험을 꺼낼 필요는 없어요.":"연습 예시: 친구에게 메시지를 보냈는데 아직 답장이 없어요. ‘나를 싫어하나 봐’라는 생각이 떠올랐어요.",personal?"case-mode":null,personal?new[]{"제시된 상황","나의 가벼운 사례"}:Array.Empty<string>()));
            if(v=="A"||personal)list.Add(S("상황과 생각 구분","직접 확인한 상황과 그때 떠오른 생각을 나눠보세요. 예시의 상황은 ‘아직 답장이 없다’예요.",personal?"personal-event":"example-event"));
            if(v=="A"||v=="B"||personal)list.Add(S("생각에 이름 붙이기","‘친구가 나를 싫어한다’ 대신 ‘그런 생각이 떠올랐다’고 표현해보세요. 원하면 자신의 생각을 적어보세요.",personal?"personal-thought":"example-thought"));
            if(v=="C"||v=="개인전체")list.Add(S("몸과 감정","그 생각과 함께 몸과 감정에 무엇이 나타났나요? 예: 가슴이 답답함, 서운함.",personal?"personal-response":"example-response"));
            if(v=="D"||v=="개인전체")list.Add(S("잠시 현재로","생각을 해결하기 전에 자연스러운 숨이나 발의 접촉을 잠시 관찰해보세요."));
            if(v=="E"||v=="개인전체")list.Add(S("다음 행동 선택","생각을 모두 틀렸다고 정할 필요는 없어요. ‘이유는 아직 모른다. 지금은 하던 일을 이어가겠다’처럼 대응할 수 있어요.",personal?"personal-next-action":"example-next-action"));
            return list.ToArray();
        }
    }
}
