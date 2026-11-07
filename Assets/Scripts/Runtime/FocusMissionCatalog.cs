using System;
using System.Collections.Generic;
using System.Linq;

namespace CapstoneDesign.Runtime.LocalState
{
    // The same content contract drives the individual Figma pages and the Unity runner.
    public sealed class FocusMissionStep
    {
        public string Key, Title, Body, ActionLabel, InputHint, Kind = "observe", ConditionKey;
        public string[] Options = Array.Empty<string>(), ConditionValues = Array.Empty<string>();
        public bool OptionalInput, RequiredAnswer;
        public int RecommendedSeconds;
        internal FocusMissionStep Copy()
        {
            var copy=(FocusMissionStep)MemberwiseClone();
            copy.Options=(string[])Options.Clone();copy.ConditionValues=(string[])ConditionValues.Clone();return copy;
        }
    }
    public sealed class FocusMissionDefinition
    {
        public string Id, Code, Title, Overview, Role, BackgroundHex, AccentHex, TextHex, CircleHex, IconAsset, CompleteAsset;
        public FocusMissionStep[] Steps;
        public int ContentVersion=2;
        public string PurposeId, Purpose, EvidenceBasis, ReviewStatus;
        internal FocusMissionDefinition Copy()
        {
            var copy=(FocusMissionDefinition)MemberwiseClone();copy.Steps=Steps.Select(step=>step.Copy()).ToArray();return copy;
        }
    }
    public static class FocusMissionCatalog
    {
        public const int Version = 3;
        public const string BasicRole = "basic", AdditionalRole = "additional";
        public const string StoryText = "아침에 창문을 열자 서늘한 바람이 방으로 들어왔습니다. 창가의 작은 화분에는 새 잎이 하나 나 있었습니다. 물을 조금 주고 잠시 바라보니, 잎 위에 남은 물방울이 빛을 받아 반짝였습니다. 밖에서는 자전거가 지나가는 소리가 들렸습니다. 나는 따뜻한 컵을 두 손으로 감싸고, 오늘 해야 할 일 중 한 가지만 천천히 시작하기로 했습니다.";
        static FocusMissionStep S(string key,string title,string body,string action="계속",string kind="observe",string[] options=null,bool input=false,string hint=null,int seconds=0,string condition=null,string[] when=null,bool required=false)
            =>new FocusMissionStep {Key=key,Title=title,Body=body,ActionLabel=action,Kind=kind,Options=options??Array.Empty<string>(),OptionalInput=input,InputHint=hint,RecommendedSeconds=seconds,ConditionKey=condition,ConditionValues=when??Array.Empty<string>(),RequiredAnswer=required};
        static string[] O(params string[] options)=>options;
        static FocusMissionDefinition D(string code,string title,string overview,string background,string accent,string circle,params FocusMissionStep[] steps)
            =>new FocusMissionDefinition {Code=code,Id=(code[0]=='P'?"course:":"free-mission:")+code,Role=code[0]=='P'?BasicRole:AdditionalRole,Title=title,Overview=overview,BackgroundHex=background,AccentHex=accent,CircleHex=circle,TextHex=code=="P10"?"#224B49":code=="M03"?"#163D50":"#183845",IconAsset="FocusMissions/Catalog/"+code,Steps=steps};
        // Version 2 is preserved verbatim for sessions already saved with that version.
        public static readonly FocusMissionDefinition[] VersionTwo={
            D("M01","주변 밝히기","주변에 빛을\n조금 더해볼까요?","#FFF9E6","#8A671D","#F8E9B6",
                S("light","빛 더하기","커튼을 열거나\n조명을 켜세요.","했어요","prepare")),
            D("M02","창밖 바라보기","창밖 풍경 하나를\n잠시 살펴봐요.","#F0F8FC","#39758C","#D7EBF3",
                S("choose","풍경 하나 고르기","창밖에서 눈에 들어오는\n풍경 하나를 골라요.","계속","prepare"),
                S("observe","색·모양·움직임","선택한 풍경의 색과 모양,\n작은 움직임을 살펴봐요.","살펴봤어요","focus",seconds:30),
                S("notice","눈에 띈 것","무엇이 눈에 띄었나요?\n선택하지 않아도 괜찮아요.","완료","choice",O("색","모양","움직임","잘 모르겠음"))),
            D("M03","물 마시기","물 한 모금으로\n잠시 쉬어가요.","#F0FAFF","#0B7098","#D9F1FA",
                S("prepare","물 준비","마실 물 한 잔을\n가까이 두세요.","준비했어요","prepare"),
                S("drink","한 모금 마시기","물을 마신 뒤 가운데\n물방울을 눌러요.","물 마셨어요","observe")),
            D("M04","세수하기","편한 방식으로\n얼굴을 씻어봐요.","#F1F9FF","#397A9C","#DCEEF9",S("wash","얼굴 씻기","세수를 마치면\n눌러주세요.","씻었어요","prepare")),
            D("M05","가볍게 몸 펴기","무리 없는 동작 하나로\n몸을 펴봐요.","#F4F6FD","#6773A3","#E2E6F7",
                S("prepare","편한 자세","앉거나 서서\n편한 자세를 잡아요.","계속","prepare"),S("stretch","편한 만큼 펴기","손이나 어깨를 편한 만큼\n펴보세요. 아프면 멈춰요.","해봤어요","focus",seconds:30)),
            D("M06","물건 하나 정리","눈앞의 물건\n하나만 정리해요.","#F7F4EC","#81704D","#EAE1CA",
                S("choose","물건 하나","제자리에 둘 물건\n하나를 골라요.","골랐어요","prepare"),S("tidy","제자리에 놓기","고른 물건을\n제자리에 두세요.","정리했어요","observe")),
            D("M07","기분 살피기","지금 기분을\n짧게 표현해봐요.","#F8F0F7","#8B5D87","#ECDCEE",
                S("feeling","지금의 마음","한 단어나 짧은 문장으로\n마음을 표현해요.","계속","input",O("잘 모르겠어요"),true,"지금의 기분",required:true),S("review","이대로 기록할까요?","쓴 내용을 살펴봐요.\n고치려면 이전 단계로 돌아가요.","기록하고 완료","review")),
            D("M08","음악 듣기","듣고 싶은 음악에\n잠시 머물러봐요.","#F3EFFA","#766099","#E5DCF2",
                S("prepare","음악 준비","평소 듣는 음악을 골라\n재생해 주세요.","준비했어요","prepare"),S("listen","음악과 함께","원하는 만큼 듣고 마쳐요.\n다른 앱에 다녀와도 이어갈 수 있어요.","듣기 마치기","focus",seconds:60)),
            D("M09","잠깐 쉬기","하던 일을\n잠시 멈춰봐요.","#F3F1FA","#76658C","#E5E0F0",
                S("prepare","편한 자세 찾기","몸을 기대거나 앉아서\n편한 자세를 찾아요.","쉬기 시작","prepare"),S("rest","잠시 쉬어요","애써 무언가를 하지 않아도 돼요.\n충분히 쉬었다면 마쳐요.","쉬기 마치기","focus",seconds:60)),
            D("M10","작은 계획 세우기","오늘 하고 싶은\n작은 일 하나를 정해요.","#F9F5E9","#8B7434","#EEE4C4",
                S("plan","작고 구체적으로","부담 없이 할 수 있는\n작은 일 한 가지를 적어요.","계속","input",input:true,hint:"예: 점심 뒤 창문 열기",required:true),S("review","계획 확인","쓴 계획을 확인해요.\n고치려면 이전 단계로 돌아가요.","저장하고 완료","review")),
            D("M11","자유롭게 낙서","떠오르는 선이나\n모양을 그려봐요.","#FCF0F3","#AA637D","#F1DDE5",
                S("drawing","마음대로 그려요","빈 공간에 손가락이나 마우스로\n선과 모양을 그려요.","그리기 마치기","draw",input:true),S("review","내 그림 살펴보기","그림을 잠시 살펴봐요.\n더 그리려면 이전 단계로 돌아가요.","완료","review")),
            D("M12","주변 식물 살피기","가까운 식물 하나를\n살펴봐요.","#F1F8EC","#587D41","#DFECCE",
                S("choose","식물 하나","가까이에서 살펴볼\n식물을 골라요.","계속","prepare"),S("observe","잎·색·모양","잎의 모양과 색을\n천천히 살펴봐요.","살펴봤어요","focus",seconds:30),S("notice","눈에 띈 것","어떤 모습이 눈에 띄었나요?","완료","choice",O("잎","색","모양","잘 모르겠음"))),
            D("M13","괜찮았던 순간","오늘 괜찮았던\n순간 하나를 떠올려요.","#FFF3E8","#A27443","#F4E1CB",
                S("recall","작은 순간 떠올리기","아주 평범한 순간이어도 좋아요.\n떠오르지 않아도 괜찮아요.","계속","choice",O("떠올랐어요","잘 모르겠어요")),S("word","한 단어 남기기","원한다면 떠오른 순간을\n한 단어로 남겨요.","기록하고 완료","input",input:true,hint:"한 단어 · 선택")),
            D("M14","짧은 글 읽기","관심 있는 글을\n편한 만큼 읽어봐요.","#FAF5E9","#8E744C","#EEE2C9",
                S("choose","읽을 글 고르기","가지고 있는 책이나\n관심 있는 글 하나를 골라요.","계속","prepare"),S("read","편한 만큼 읽기","내용을 천천히 읽어봐요.\n원하는 만큼 읽고 마쳐요.","읽기 마치기","focus",seconds:60)),
            D("M15","일정 걸음 걷기","안전한 곳에서\n편한 속도로 걸어봐요.","#F0F8EF","#537F4D","#DDECD9",
                S("goal","오늘의 걸음 목표","오늘의 목표를 확인해요.\n무리 없이 걸을 수 있는 곳을 골라요.","걷기 시작","walk"),S("walking","편한 속도로 걷기","현재 걸음 수를 확인해요.\n동기화가 늦으면 직접 확인할 수 있어요.","걷기 마치기","walk",seconds:60),S("check","걸음을 마쳤나요?","안전한 곳에 멈춘 뒤\n실제 걸음을 확인해주세요.","완료","choice",O("목표를 채웠어요","직접 걸음을 확인했어요"))),
            D("M16","공원 방문","가까운 공원에\n다녀와요.","#EFF7EE","#4D7C56","#DCEBD9",
                S("prepare","편한 경로 선택","가까운 공원까지\n안전하고 편한 경로로 이동해요.","방문 시작","prepare"),S("visit","공원에 도착했나요?","주변을 살펴본 뒤\n방문을 확인해주세요.","방문했어요","observe")),
            D("M17","안내 따라 호흡","편한 속도로\n자연스러운 숨을 살펴요.","#EDF8F7","#43847F","#D8EEEA",
                S("prepare","편한 자세","숨을 억지로 바꾸지 않고\n편한 자세를 찾으세요.","준비됐어요","prepare"),S("breath","자연스러운 숨","원의 움직임을 참고만 해요.\n내 호흡 속도를 따라가도 좋아요.","마치기","breathe",seconds:60)),
            D("P01","잠깐 멈춰보기","잠깐 멈춰\n호흡이 느껴지는 곳을 찾아요.","#EEF8F7","#357C78","#D8ECE8",
                S("prepare","편한 자세","눈은 편한 대로 두고\n앉거나 누워요.",kind:"prepare"),S("breath","숨이 느껴지는 곳","숨을 바꾸지 말고 코·가슴·배의\n움직임을 살펴요.",kind:"breathe",seconds:60),S("notice","어디에서 느꼈나요?","뚜렷하지 않아도 괜찮아요.","완료","choice",O("코","가슴","배","잘 모르겠음"))),
            D("P02","주변 소리 만나기","주변 소리 하나에\n귀 기울여봐요.","#EFF5FC","#5079A4","#DDE8F7",
                S("prepare","편하게 앉기","눈은 떠도 감아도 좋아요.\n편한 자세로 앉아요.",kind:"prepare"),S("sound","가까운 소리·먼 소리","들리는 소리 하나와\n그 변화를 살펴요.",kind:"focus",seconds:60),S("notice","기억나는 소리","들린 소리를 골라보거나\n짧게 적어요.","완료","input",O("사람 소리","자연 소리","생활 소리","잘 모르겠음"),true,"기억나는 소리 · 선택")),
            D("P03","몸에 안부 묻기","몸에서 느껴지는\n감각 하나를 살펴봐요.","#F2F5FC","#677DA0","#E0E7F4",
                S("prepare","편한 자세","앉거나 누워서\n몸을 편하게 두어요.",kind:"prepare"),S("contact","몸이 닿는 곳","몸이 의자나 바닥에 닿는\n곳을 잠시 느껴봐요.",kind:"focus",seconds:60),S("notice","어떤 느낌인가요?","가까운 느낌을 골라요.\n뚜렷하지 않아도 괜찮아요.","완료","choice",O("따뜻함","차가움","압박감","특별한 느낌 없음"))),
            D("P04","한 모금 천천히","마시는 한 모금의 감각을\n차례로 살펴봐요.","#F0FAFF","#307D9A","#D9EEF6",
                S("prepare","한 모금 준비","편한 온도의\n물이나 음료를 준비해요.",kind:"prepare"),S("touch","컵을 잡은 손","손의 감촉과\n컵의 온도를 느껴봐요.",kind:"observe"),S("drink","천천히 한 모금","맛과 온도, 삼킨 뒤의\n느낌을 살펴봐요.",kind:"focus",seconds:30),S("notice","눈에 띈 감각","뚜렷하지 않아도 괜찮아요.","완료","choice",O("감촉","온도","맛","뚜렷하지 않음"))),
            D("P05","다시 돌아오기","주의가 벗어나면\n호흡으로 돌아오는 연습이에요.","#EDF7F6","#3C7F77","#D5EAE4",
                S("prepare","호흡 찾기","편한 자세에서\n자연스러운 호흡을 찾아요.",kind:"prepare"),S("breath","숨에 머물기","잠시 호흡의 감각에\n머물러봐요.",kind:"breathe",seconds:60),S("return","부드럽게 돌아오기","다른 생각을 알아차리면\n호흡으로 돌아와요. 횟수는 세지 않아요.",kind:"focus",seconds:60),S("notice","떠오른 생각","생각이 떠오르지 않아도\n괜찮아요.","완료","choice",O("알아차렸어요","떠오르지 않았어요","잘 모르겠음"))),
            D("P06","익숙한 행동 새롭게","평소 하던 행동에서\n새로운 감각을 살펴봐요.","#F1F9FB","#49818A","#DCEEF1",
                S("activity","행동 하나 고르기","손 씻기나 설거지 중\n하나를 골라요.",kind:"choice",options:O("손 씻기","설거지")),S("observe","온도·움직임·소리","고른 행동을 하며\n물의 감각에 주의를 둬요.",kind:"focus",seconds:60),S("notice","눈에 띈 감각","처음 느낀 감각이 없어도\n괜찮아요.","완료","choice",O("온도","움직임","소리","잘 모르겠음"))),
            D("P07","나에게 편한 실습 찾기","부담이 적었던 실습을\n한 번 더 해봐요.","#F3F8EF","#6B854D","#E2ECCF",
                S("activity","편한 대상 고르기","오늘 부담이 적은\n대상 하나를 골라요.",kind:"choice",options:O("호흡","몸의 접촉","주변 소리")),S("practice","고른 대상에 머물기","주의가 벗어나면\n선택한 대상으로 다시 돌아와요.",kind:"focus",seconds:60),S("next","다음에도 해볼까요?","앞으로 하고 싶은 활동을\n고르거나 건너뛰어요.","완료","choice",O("호흡","몸의 접촉","주변 소리","없음","잘 모르겠음"))),
            D("P08","마음에 이름 붙이기","지금 마음을 살피고\n호흡을 관찰해요.","#F7F0F8","#89628E","#EBDDEE",
                S("feeling","지금의 마음","한 단어를 고르거나\n직접 적어봐요.",kind:"input",options:O("편안함","무덤덤함","불안함","잘 모르겠음"),input:true,hint:"기분 한 단어 · 선택"),S("breath","자연스러운 숨","숨을 바꾸지 않고\n자연스러운 움직임을 살펴요.",kind:"breathe",seconds:60),S("finish","그대로 마무리","기분을 바꾸려 하지 않아도\n괜찮아요. 여기서 마쳐요.","완료","confirm")),
            D("P09","감정과 몸 살펴보기","감정과 함께 나타나는\n몸의 감각을 살펴봐요.","#F4F0F9","#7D6898","#E4DDF0",
                S("prepare","몸이 닿는 곳","편한 자세에서\n몸이 닿는 곳을 찾아요.",kind:"prepare"),S("contact","접촉을 느껴요","의자나 바닥의 접촉을\n잠시 느껴봐요.",kind:"focus",seconds:60),S("notice","몸의 느낌","눈에 띈 몸의 느낌이 있나요?\n없거나 잘 모르겠어도 괜찮아요.","완료","choice",O("따뜻함","긴장","무거움","없음","잘 모르겠음"))),
            D("P10","잠시 숨 고르기","하던 일 사이에서\n잠시 멈춰봐요.","#EFF9F7","#2F726E","#DDF2EE",
                S("awareness","지금 살펴보기","생각·감정·몸 감각을\n있는 그대로 살펴요.",kind:"observe"),S("breath","숨에 주의 모으기","자연스러운 숨이 느껴지는 곳에\n잠시 머물러요.",kind:"breathe",seconds:60),S("expand","주의 넓히기","몸 전체와 주변으로\n주의를 넓혀봐요.",kind:"focus",seconds:60),S("ready","마무리하기","하던 일로 돌아갈\n준비가 되었나요?","완료","confirm")),
            D("P11","생각이 왔음을 알아차리기","떠오른 생각을\n알아차리는 연습이에요.","#F2F0FA","#786497","#E4DDF0",
                S("breath","숨 느끼기","자연스러운 호흡을\n잠시 느껴봐요.",kind:"breathe",seconds:60),S("thought","생각이구나","생각이 떠오르면\n‘생각이구나’ 하고 알아차려요.",kind:"focus",seconds:60),S("return","호흡으로 돌아오기","생각의 내용을 따라가기보다\n호흡으로 돌아와요.",kind:"breathe",seconds:60),S("notice","여기서 마쳐요","내용을 적지 않아도 돼요.\n생각이 없었어도 마칠 수 있어요.","완료","confirm")),
            D("P12","작은 편안함 찾기","괜찮게 느껴지는\n경험 하나를 살펴봐요.","#FFF6E9","#A47B3B","#F4E5C8",
                S("experience","경험 하나 고르기","일상에서 부담 없는\n경험 하나를 골라요.",kind:"choice",options:O("햇볕","음료","음악","다른 경험")),S("observe","경험에 주의 두기","선택한 경험을\n잠시 느껴봐요.",kind:"focus",seconds:60),S("notice","괜찮은 순간","괜찮은 순간을 찾았나요?\n찾지 못해도 괜찮아요.","완료","choice",O("찾았어요","찾지 못했어요","잘 모르겠음"))),
            D("P13","마음이 바뀌어도 괜찮기","멈추기 전후 마음을 살펴요.\n어느 변화도 정답이 아니에요.","#F7F0F6","#916684","#ECDFE8",
                S("before","실습 전 마음","가까운 기분을 고르거나\n건너뛰어요.",kind:"choice",options:O("편안함","무덤덤함","불편함","잘 모르겠음")),S("awareness","현재 알아차리기","지금의 생각·감정·몸 감각을\n알아차려요.",kind:"observe"),S("breath","숨에 주의 모으기","자연스러운 호흡에\n잠시 주의를 모아요.",kind:"breathe",seconds:60),S("expand","주변으로 넓히기","몸 전체와 주변으로\n주의를 넓혀요.",kind:"focus",seconds:60),S("after","실습 후 마음","전과 비교해 어떤가요?\n어느 답이든 괜찮아요.","완료","choice",O("좋아짐","같음","불편해짐","잘 모르겠음"))),
            D("P14","내게 맞는 방식 확인하기","편했던 실습을\n다시 해봐요.","#F1F8F1","#5B805F","#DCECDD",
                S("activity","편했던 대상","오늘 관찰할\n대상을 골라요.",kind:"choice",options:O("호흡","몸의 접촉","주변 소리")),S("practice","잠시 머물기","고른 대상에 주의를 두고\n벗어나면 부드럽게 돌아와요.",kind:"focus",seconds:60),S("preference","앞으로의 실습","더 자주 또는 덜 자주 하고 싶은\n활동이 있나요?", "완료","input",O("더 자주","지금처럼","덜 자주","잘 모르겠음"),true,"활동과 원하는 빈도 · 선택")),
            D("P15","나에게 한마디","숨을 잠시 살핀 뒤\n나에게 건넬 말을 떠올려요.","#FBF0F3","#A3677F","#F0DDE5",
                S("prepare","편한 자세","숨을 바꿀 필요 없이\n편한 자세를 찾아요.",kind:"prepare"),S("breath","숨에 머물기","숨이 느껴지는 곳을 살피고\n주의가 벗어나면 돌아와요.",kind:"breathe",seconds:60),S("kindness","나에게 건넬 말","지금 자신에게 어떤 말을\n건네고 싶나요?","완료","input",O("잠깐 쉬어도 괜찮아","오늘도 애썼어","천천히 해도 돼"),true,"직접 건넬 말 · 선택")),
            D("P16","몸이 편한 자세 찾기","자세를 조절하고\n몸의 접촉 감각을 살펴요.","#F0F5FA","#607F9B","#DFE9F3",
                S("adjust","편한 만큼 조절","의자·등받이·발 위치를\n편한 만큼 조절해요.",kind:"prepare"),S("contact","차례로 느끼기","몸이 의자나 바닥에 닿는 곳을\n차례로 느껴봐요.",kind:"focus",seconds:60),S("notice","어떤 조절이 편했나요?","차이가 없거나\n잘 모르겠어도 괜찮아요.","완료","choice",O("의자","등받이","발 위치","차이 없음","잘 모르겠음"))),
            D("P17","나를 돕는 작은 행동","잠깐 멈춘 뒤\n지금 필요한 작은 행동을 해요.","#F3F8EF","#6E854B","#E2EDCF",
                S("awareness","지금 살펴보기","생각·감정·몸 감각을\n있는 그대로 살펴봐요.",kind:"observe"),S("breath","숨에 모으기","자연스러운 숨에\n주의를 모아요.",kind:"breathe",seconds:30),S("expand","몸 전체로 넓히기","발의 접촉과 몸 전체로\n주의를 넓혀요.",kind:"focus",seconds:30),S("activity","필요한 행동","지금 나를 돕는\n행동 하나를 골라요.",kind:"choice",options:O("물 마시기","쉬기","창밖 보기")),S("action","선택한 행동 하기","앞에서 고른 행동을\n편한 만큼 해봐요.","행동했어요","focus",seconds:30)),
            D("P18","실습을 바꿔도 괜찮기","오늘 편한 대상에 머물고\n필요하면 바꿔도 돼요.","#EDF6F9","#4D7C8C","#D8EAF0",
                S("activity","오늘 편한 대상","호흡과 주변 소리 중\n하나를 골라요.",kind:"choice",options:O("호흡","주변 소리")),S("practice","필요하면 바꿔요","고른 대상을 살펴봐요.\n불편하면 바꾸거나 잠시 쉬어요.",kind:"focus",options:O("호흡","주변 소리"),seconds:60),S("notice","살펴본 대상","어떤 대상을 살폈나요?\n선택하지 않아도 괜찮아요.","완료","choice",O("호흡","주변 소리","중간에 바꿨어요","잠시 쉬었어요"))),
            D("P19","산 명상 경험하기","산을 떠올리며\n변하는 경험을 살펴봐요.","#F0F4F1","#607C68","#DBE5DD",
                S("route","오늘의 실습 선택","산을 떠올리거나\n짧은 소리 실습을 골라요.",kind:"choice",options:O("산 명상","짧은 소리 실습")),
                S("mountain-prepare","몸의 접촉 느끼기","편한 자세에서 몸이\n바닥에 닿는 곳을 느껴요.",kind:"prepare",condition:"route",when:O("산 명상")),
                S("mountain-shape","산을 떠올려요","산의 모양과\n넓은 밑부분을 떠올려요.",kind:"focus",seconds:45,condition:"route",when:O("산 명상")),
                S("mountain-weather","지나가는 날씨","햇빛·구름·비가 산 위를\n지나가는 모습을 떠올려요.",kind:"focus",seconds:45,condition:"route",when:O("산 명상")),
                S("mountain-change","변하는 경험","생각과 감정도 날씨처럼\n달라지는지 살펴봐요.",kind:"focus",seconds:45,condition:"route",when:O("산 명상")),
                S("mountain-return","지금으로 돌아와요","몸의 접촉과 자연스러운\n호흡으로 돌아와요.",kind:"breathe",seconds:30,condition:"route",when:O("산 명상")),
                S("sound-prepare","편한 자세 찾기","몸을 편하게 두고\n주변 소리를 들어봐요.",kind:"prepare",condition:"route",when:O("짧은 소리 실습")),
                S("sound-listen","변하는 소리","가까운 소리와 먼 소리,\n그 변화를 들어봐요.",kind:"focus",seconds:60,condition:"route",when:O("짧은 소리 실습")),
                S("notice","떠오른 느낌","편했던 점이나 들린 소리를\n원하면 남겨요.","완료","input",O("편했어요","비슷했어요","불편했어요","잘 모르겠음"),true,"떠오른 느낌이나 소리 · 선택")),
            D("P20","오늘의 시도 인정하기","짧게 실습한 뒤\n나를 위해 한 행동을 떠올려요.","#FFF5EA","#9C784A","#F1E3CD",
                S("activity","실습 대상 고르기","오늘 편한\n대상 하나를 골라요.",kind:"choice",options:O("호흡","주변 소리")),S("practice","잠시 관찰하기","고른 대상에\n잠시 주의를 둬요.",kind:"focus",seconds:60),S("action","오늘 나를 위한 일","자신을 위해 한 작은 행동을\n고르거나 적어봐요.","완료","input",O("잠깐 쉬기","물 마시기","실습하기","잘 모르겠음"),true,"작은 행동 · 선택")),
            D("P21","나만의 쉬는 방법 정하기","편했던 활동을 다시 해보고\n쉬는 방법을 골라요.","#F3F3FA","#79749A","#E3E1F0",
                S("activity","내게 편한 활동","부담 없이 할 수 있는\n활동을 골라요.",kind:"choice",options:O("호흡","몸의 접촉","소리","잠시 멈춤")),S("practice","직접 해보기","선택한 활동을\n편한 만큼 해봐요.",kind:"focus",seconds:60),S("save","내 쉬는 방법","이 활동을\n‘내 쉬는 방법’으로 남길까요?","완료","choice",O("내 쉬는 방법으로 저장","이번에는 건너뛰기"))),
            D("P22","이동하는 감각 느끼기","움직이거나 앉은 채로\n몸이 닿는 감각을 살펴요.","#F2F7ED","#758649","#E4EBCF",
                S("activity","안전한 방법 선택","안전한 곳에서 걷거나\n앉아서 살펴봐요.",kind:"choice",options:O("걷기","앉아서 살피기")),S("contact","닿는 곳 느끼기","발 또는 몸이 바닥·의자에\n닿는 곳을 느껴봐요.",kind:"focus",seconds:45),S("move","달라지는 접촉","움직이거나 자세를 조절할 때\n접촉이 어떻게 달라지는지 살펴요.",kind:"focus",seconds:45),S("notice","눈에 띈 감각","뚜렷한 감각이 없어도\n괜찮아요.","완료","choice",O("압박감","움직임","무게","뚜렷한 감각 없음"))),
            D("P23","한입에 주의 기울이기","평소 식사의 한입을\n천천히 살펴봐요.","#FFF2DF","#9B5D2B","#F4E2CB",
                S("prepare","한입 준비","먹을 수 있는 음식 한입을\n준비해요.","준비했어요","prepare"),S("observe","향과 모양","먹기 전 향과 모양을\n살펴봐요.",kind:"observe"),S("bite","한입 느끼기","먹으면서 온도와 질감에\n주의를 둬요.","한입 먹었어요","focus",seconds:30),S("sensation","감각 돌아보기","어떤 감각이 눈에 띄었나요?\n선택하지 않아도 괜찮아요.","완료","choice",O("향","온도","질감","특별한 감각 없음"))),
            D("P24","들리는 말에 집중하기","짧은 이야기를 듣고\n들린 내용을 살펴봐요.","#F1F3FB","#677BA1","#DEE5F5",
                S("route","들을 이야기 선택","상대의 이야기 또는\n앱의 짧은 이야기를 골라요.",kind:"choice",options:O("상대의 이야기","앱의 짧은 이야기")),S("listen","들리는 말에 주의 두기","답을 미리 준비하지 말고\n지금 들리는 말에 주의를 둬요.",kind:"listen"),S("notice","기억나는 말이나 장면","떠오르는 내용이 있다면\n짧게 남겨요.","완료","input",input:true,hint:"기억나는 말이나 장면 · 선택")),
            D("P25","말하기 전에 잠깐 멈추기","말하거나 메시지를 보내기 전\n잠시 멈춰봐요.","#F8EDF1","#96516B","#EDDCE2",
                S("pause","잠깐 멈추기","아직 말하거나 보내지 말고\n숨을 한 번 고르세요.","멈췄어요","prepare"),S("message","전할 말 확인","전하고 싶은 핵심을\n한 문장으로 떠올려요.",kind:"input",input:true,hint:"적거나 속으로 떠올려요"),S("speak","이제 전하기","이제 편한 방식으로\n말하거나 보내세요.","말하거나 보냈어요","observe"),S("check","순서 확인","멈춘 뒤 내용을\n확인했나요?","완료","confirm")),
            D("P26","사소한 도움 알아차리기","오늘 작은 도움이 된 것이\n있었는지 살펴봐요.","#FFF3ED","#A77358","#F3E0D4",
                S("route","도움 떠올리기","사람·물건·환경 중\n작은 도움이 있었나요?",kind:"choice",options:O("떠올랐어요","아직 없어요")),S("help","어떤 도움이었나요?","가까운 종류를 골라요.",kind:"choice",options:O("사람","물건","환경"),condition:"route",when:O("떠올랐어요")),S("sound","소리 하나 관찰","도움이 떠오르지 않아도 괜찮아요.\n주변 소리 하나에 주의를 둬요.",kind:"focus",seconds:30,condition:"route",when:O("아직 없어요")),S("check","해본 경로 확인","앞에서 고른 경험을\n잠시 돌아봐요.","완료","review")),
            D("P27","생활에 실습 연결하기","이미 하는 행동 뒤에\n짧은 실습을 붙여봐요.","#F0F7F4","#4F8370","#DAEDE3",
                S("trigger","생활 계기 고르기","이미 하는 행동\n하나를 골라요.",kind:"input",options:O("점심 후","샤워 후"),input:true,hint:"직접 정하기 · 선택"),S("activity","이어 할 실습","짧게 이어 할\n실습 하나를 골라요.",kind:"choice",options:O("호흡","몸의 접촉","주변 소리")),S("wait","행동 뒤 돌아와요","정한 생활 행동을 마친 뒤\n이 화면으로 돌아오세요.","이미 마쳤어요","wait"),S("practice","생활 뒤 짧은 실습","고른 실습을\n편한 만큼 해봐요.",kind:"focus",seconds:60),S("notice","부담 없는 방식인가요?","시점과 방식이 어땠나요?\n원하면 다음에 바꿔도 돼요.","완료","choice",O("부담 없었어요","조금 바꾸고 싶어요","잘 모르겠음"))),
            D("P28","다음 주의 나 돌보기","편한 실습을 다시 해보고\n다음 주 계획을 세워요.","#F3F7EF","#718652","#E0EACF",
                S("activity","편한 실습 선택","해본 실습 중\n하나를 골라요.",kind:"choice",options:O("호흡","몸의 접촉","주변 소리","잠시 멈춤")),S("practice","한 번 더 실습","선택한 실습을\n편한 만큼 해봐요.",kind:"focus",seconds:60),S("next-activity","다음 주의 활동","다음 주 다시 할\n활동을 골라요.",kind:"choice",options:O("호흡","몸의 접촉","주변 소리","잠시 멈춤")),S("time","부담 없는 시간","이어가기 편한 시간대를\n고르거나 나중에 정해요.",kind:"choice",options:O("아침","점심","저녁","생활 행동 뒤","나중에 정하기")),S("review","나의 계획 확인","고른 활동과 시간대를 확인해요.\n이전 단계에서 바꿀 수 있어요.","완료","review"))
        };
        public static readonly FocusMissionDefinition[] All=BuildCurrentVersion();
        public static FocusMissionDefinition[] Basic=>All.Where(m=>m.Role==BasicRole).ToArray();
        public static FocusMissionDefinition[] Additional=>All.Where(m=>m.Role==AdditionalRole).ToArray();
        public static FocusMissionDefinition Find(string idOrCode)=>Find(idOrCode,Version);
        public static FocusMissionDefinition Find(string idOrCode,int version)=>(version==1?Legacy:version==2?VersionTwo:version==Version?All:Array.Empty<FocusMissionDefinition>()).FirstOrDefault(m=>m.Id==idOrCode||m.Code==idOrCode);
        public static FocusMissionDefinition FindForSession(ActivitySession session)
        {
            if(session==null)return null;
            var definition=Find(session.MissionId,session.FocusVersion);
            if(definition==null||!session.FocusSupport)return definition;
            // A sound practice remains P02 in the record, but is a separate support activity.
            if(session.FocusVersion!=Version||definition.Code!="P02")return null;
            var support=definition.Copy();support.Role=AdditionalRole;return support;
        }
        public static bool IsApplicable(FocusMissionDefinition mission,IReadOnlyList<FocusStepRecord> records,int index)
        {
            if(mission==null||index<0||index>=mission.Steps.Length)return false;
            var step=mission.Steps[index];if(string.IsNullOrEmpty(step.ConditionKey))return true;
            var answer=records?.FirstOrDefault(r=>r.Key==step.ConditionKey)?.Value??"";
            // Skipping a route choice selects the first offered path, consistently in both clients.
            if(answer.Length==0)answer=mission.Steps.First(s=>s.Key==step.ConditionKey).Options.FirstOrDefault()??"";
            return step.ConditionValues.Contains(answer);
        }
        public static bool HasOriginalLayout(string code)=>code=="M03"||code=="P10"||code=="P23"||code=="P25";
        static FocusMissionDefinition[] BuildCurrentVersion()
        {
            var definitions=VersionTwo.Select(m=>m.Copy()).ToArray();
            foreach(var definition in definitions)
            {
                definition.ContentVersion=Version;
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
        static FocusMissionStep Step(string key,string title,string body,string action,string[] options=null,bool input=false,string hint=null)
            =>new FocusMissionStep {Key=key,Title=title,Body=body,ActionLabel=action,Options=options??Array.Empty<string>(),OptionalInput=input,InputHint=hint};
        // Kept immutable so existing saved session prompts, step indexes and receipts remain valid.
        public static readonly FocusMissionDefinition[] Legacy={
            new FocusMissionDefinition {Id="course:P10",Code="P10",Title="잠시 숨 고르기",Overview="하던 일 사이에서\n잠시 멈춰봐요.",Role=BasicRole,BackgroundHex="#EFF9F7",AccentHex="#2F726E",TextHex="#224B49",Steps=new[]{
                Step("awareness","지금 살펴보기","생각·감정·몸 감각을\n있는 그대로 살펴요.","계속"),Step("breath","숨에 주의 모으기","자연스러운 숨이 느껴지는 곳에\n잠시 머물러요.","계속"),Step("expand","주의 넓히기","몸 전체와 주변으로\n주의를 넓혀봐요.","계속"),Step("ready","마무리하기","하던 일로 돌아갈\n준비가 되었나요?","완료")}},
            new FocusMissionDefinition {Id="course:P23",Code="P23",Title="한입에 주의 기울이기",Overview="평소 식사의 한입을\n천천히 살펴봐요.",Role=BasicRole,BackgroundHex="#FFF2DF",AccentHex="#9B5D2B",TextHex="#183845",Steps=new[]{
                Step("prepare","한입 준비","먹을 수 있는 음식 한입을\n준비해요.","준비했어요"),Step("observe","향과 모양","먹기 전 향과 모양을\n살펴봐요.","계속"),Step("bite","한입 느끼기","먹으면서 온도와 질감에\n주의를 둬요.","한입 먹었어요"),Step("sensation","감각 돌아보기","어떤 감각이 눈에 띄었나요?\n선택하지 않아도 괜찮아요.","완료",new[]{"향","온도","질감","특별한 감각 없음"})}},
            new FocusMissionDefinition {Id="course:P25",Code="P25",Title="말하기 전에 잠깐 멈추기",Overview="말하거나 메시지를 보내기 전\n잠시 멈춰봐요.",Role=BasicRole,BackgroundHex="#F8EDF1",AccentHex="#96516B",TextHex="#183845",Steps=new[]{
                Step("pause","잠깐 멈추기","아직 말하거나 보내지 말고\n숨을 한 번 고르세요.","멈췄어요"),Step("message","전할 말 확인","전하고 싶은 핵심을\n한 문장으로 떠올려요.","계속",null,true,"적거나 속으로 떠올려요"),Step("speak","이제 전하기","이제 편한 방식으로\n말하거나 보내세요.","말하거나 보냈어요"),Step("check","순서 확인","멈춘 뒤 내용을\n확인했나요?","완료")}},
            new FocusMissionDefinition {Id="free-mission:M03",Code="M03",Title="물 마시기",Overview="물 한 모금으로\n잠시 쉬어가요.",Role=AdditionalRole,BackgroundHex="#F0FAFF",AccentHex="#0B7098",TextHex="#163D50",Steps=new[]{
                Step("prepare","물 준비","마실 물 한 잔을\n가까이 두세요.","준비했어요"),Step("drink","한 모금 마시기","물을 마신 뒤 가운데\n물방울을 눌러요.","물 마셨어요")}}
        };
    }
    public sealed class FocusDurationFeedback
    {
        public string MissionId, StepKey, ConfirmedUtc;
        public int ActualSeconds;
        public bool ConfirmedTooLong;
    }
    public sealed class FocusDurationRecommendation
    {
        public int BaselineSeconds { get; internal set; }
        public int RecommendedSeconds { get; internal set; }
        public string PolicyId { get; internal set; }
        public string Reason { get; internal set; }
        public int EvidenceCount { get; internal set; }
    }
    public static class FocusMissionAdjustmentPolicy
    {
        public const string PolicyId="focus-duration-observed-v1";
        public const string DurationBasis="app-adapted-duration-not-clinical-minimum";
        // Expose only existing method routes, not emotion answers, reward choices or unreviewed
        // substitutions. A different-purpose option must go through the explicit routing helper.
        public static string[] AllowedMethods(FocusMissionDefinition mission,string stepKey)
        {
            var step=mission?.Steps.FirstOrDefault(s=>s.Key==stepKey);
            if(step==null)return Array.Empty<string>();
            bool method=stepKey=="activity"&&new[]{"P06","P07","P14","P18","P20","P21","P22","P27","P28"}.Contains(mission.Code)
                ||mission.Code=="P18"&&stepKey=="practice"||mission.Code=="P28"&&stepKey=="next-activity"
                ||mission.Code=="P19"&&stepKey=="route"||mission.Code=="P24"&&stepKey=="route";
            return method?step.Options.Where(option=>DifferentPurposeAlternative(mission,stepKey,option)==null).ToArray():Array.Empty<string>();
        }
        public static string[] PreservedStepKeys(FocusMissionDefinition mission)
            =>mission==null?Array.Empty<string>():mission.Steps.Select(step=>step.Key).ToArray();
        public static bool CanPersonalizeQuantity(FocusMissionDefinition mission)=>mission?.Code=="M15";
        public static bool IsTimedPractice(FocusMissionStep step)=>step!=null&&step.RecommendedSeconds>0;
        public static string DifferentPurposeAlternative(FocusMissionDefinition mission,string stepKey,string value)
            =>mission!=null&&mission.ContentVersion>=3&&stepKey=="route"&&
                (mission.Code=="P19"&&value=="짧은 소리 실습"||mission.Code=="P26"&&value=="아직 없어요")?"course:P02":null;
        public static string DifferentPurposeAlternative(ActivitySession session)
        {
            var definition=FocusMissionCatalog.FindForSession(session);
            var route=session?.FocusStepRecords?.FirstOrDefault(r=>r.Key=="route");
            return DifferentPurposeAlternative(definition,"route",route?.Value);
        }
        public static FocusDurationRecommendation RecommendDuration(string missionId,string stepKey,int baselineSeconds,IEnumerable<FocusDurationFeedback> history)
        {
            if(baselineSeconds<0||baselineSeconds>86400)throw new ArgumentOutOfRangeException(nameof(baselineSeconds));
            var definition=FocusMissionCatalog.Find(missionId);
            var step=definition?.Steps.FirstOrDefault(s=>s.Key==stepKey);
            var result=new FocusDurationRecommendation {BaselineSeconds=baselineSeconds,RecommendedSeconds=baselineSeconds,PolicyId=PolicyId,Reason="catalog-default"};
            if(step==null||!IsTimedPractice(step)||baselineSeconds==0)return result;
            var rows=(history??Enumerable.Empty<FocusDurationFeedback>()).Where(row=>row!=null&&row.ConfirmedTooLong
                &&FocusMissionCatalog.Find(row.MissionId)?.Id==definition.Id&&row.StepKey==stepKey
                &&row.ActualSeconds>0&&row.ActualSeconds<baselineSeconds&&ValidTime(row.ConfirmedUtc))
                .OrderByDescending(row=>DateTimeOffset.Parse(row.ConfirmedUtc)).ToArray();
            if(rows.Length==0)return result;
            // Use the user's observed stopping time as a suggestion, never a completion threshold.
            // No clinical lower bound or transfer between different objectives is asserted here.
            result.RecommendedSeconds=rows[0].ActualSeconds;result.EvidenceCount=rows.Length;result.Reason="confirmed-too-long-observed-duration";return result;
        }
        static bool ValidTime(string value){DateTimeOffset parsed;return DateTimeOffset.TryParse(value,out parsed);}
        public static string[] PlanActivityOptions=>new[]{"차 마시기","창문 열기","물건 하나 정리","잠깐 쉬기"};
        public static string[] PlanTimePeriodOptions=>new[]{"아침","점심","저녁","생활 행동 뒤","나중에 정하기"};
        public static string ComposePlan(string activity,string timePeriod,string optionalText=null)
        {
            string text=(optionalText??"").Trim();
            if(text.Length>500)throw new ArgumentException("Plan text is too long.");
            if(!string.IsNullOrEmpty(activity)&&!PlanActivityOptions.Contains(activity))throw new ArgumentException("Unknown plan activity.");
            if(!string.IsNullOrEmpty(timePeriod)&&!PlanTimePeriodOptions.Contains(timePeriod))throw new ArgumentException("Unknown plan period.");
            if(text.Length==0)
            {
                if(string.IsNullOrEmpty(activity)||string.IsNullOrEmpty(timePeriod))return "";
                text=activity;
            }
            string plan=string.IsNullOrEmpty(timePeriod)?text:timePeriod+" · "+text;
            if(plan.Length>500)throw new ArgumentException("Plan text is too long.");
            return plan;
        }
    }

    public sealed class FocusAdditionalRecommendation
    {
        readonly float[] features;
        public string MissionId { get; private set; }
        public string PolicyId { get; private set; }
        public string Reason { get; private set; }
        public double Score { get; private set; }
        public float[] Features=>(float[])features.Clone();
        internal FocusAdditionalRecommendation(string id,string reason,float[] values,double score)
        {MissionId=id;PolicyId=FocusAdditionalRecommendationPolicy.PolicyId;Reason=reason;features=(float[])values.Clone();Score=score;}
    }
    public static class FocusAdditionalRecommendationPolicy
    {
        public const string PolicyId="focus-additional-history-preference-v2";
        internal sealed class PreferenceSignal
        {
            public string MissionId, Reason, Identity;
            public int Direction, Priority;
            public DateTimeOffset At;
        }
        // Local choice only: it cannot change the required course, start an activity or issue rewards.
        // It deliberately consumes no health values or notification click times.
        public static FocusAdditionalRecommendation Select(GardenState state,DateTimeOffset now)
        {
            if(state==null)throw new ArgumentNullException(nameof(state));
            var active=FocusMissionPolicy.Active(state,FocusMissionCatalog.AdditionalRole);
            if(active!=null)return new FocusAdditionalRecommendation(active.MissionId,"resume-active-activity",new[]{0f,.5f,0f,.5f},1);
            var all=FocusMissionCatalog.Additional;
            var candidates=all.Where(m=>!FocusMissionPolicy.AdditionalCompletedToday(state,m.Id)).ToArray();
            if(candidates.Length==0)candidates=all;
            var favorite=PreferenceFor(state,now);
            var decisions=candidates.Select(m=>
            {
                var started=state.Sessions.Where(s=>FocusMissionPolicy.IsFocus(s)&&s.MissionId==m.Id&&StartedBefore(s,now)).ToArray();
                var last=started.Select(s=>DateTimeOffset.Parse(s.StartedUtc)).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
                float completion=started.Length==0?.5f:started.Count(s=>s.Status==ParticipationState.RewardCommitted)/(float)started.Length;
                float recency=started.Length==0?1:(float)Math.Max(0,Math.Min(1,(now-last).TotalDays/14));
                bool preferenceApplies=favorite!=null&&favorite.MissionId==m.Id;
                float preference=preferenceApplies&&favorite.Direction>0?1:0;
                // Feature order matches the historical contract, but this local policy is not sent
                // to its model: duration fit is unknown, not an invented available-time response.
                var values=new[]{preference,completion,recency,.5f};
                // A frequency response changes ranking only. It never changes practice seconds,
                // removes a course step or prohibits the user's explicit choice of this activity.
                double frequencyPenalty=preferenceApplies&&favorite.Direction<0?.30:0;
                double score=.30*preference+.20*completion+.15*recency-frequencyPenalty;
                string reason=preferenceApplies&&favorite.Direction!=0?favorite.Reason:started.Length>0?"participation-history":"catalog-default-no-history";
                return new FocusAdditionalRecommendation(m.Id,reason,values,score);
            }).ToArray();
            // Equal-evidence suggestions rotate deterministically by date, rather than always M03.
            int rotation=(int)(now.UtcDateTime.Date.Ticks/TimeSpan.TicksPerDay%all.Length);
            return decisions.OrderByDescending(d=>d.Score)
                .ThenBy(d=>(Array.FindIndex(all,m=>m.Id==d.MissionId)-rotation+all.Length)%all.Length).First();
        }
        static bool StartedBefore(ActivitySession session,DateTimeOffset now)
        {DateTimeOffset time;return DateTimeOffset.TryParse(session.StartedUtc,out time)&&time<=now;}
        internal static PreferenceSignal PreferenceFor(GardenState state,DateTimeOffset now)
        {
            var signals=new List<PreferenceSignal>();
            var saved=state.PracticePreferences.FirstOrDefault(p=>p.Key=="favorite");
            if(saved!=null)
            {
                DateTimeOffset at;
                // Untimestamped old settings remain the fallback. Do not invent their date.
                if(string.IsNullOrEmpty(saved.UpdatedUtc))at=DateTimeOffset.MinValue;
                else if(!DateTimeOffset.TryParse(saved.UpdatedUtc,out at)||at>now)at=DateTimeOffset.MaxValue;
                if(at<=now)signals.Add(new PreferenceSignal {MissionId=FocusMissionCatalog.Additional.FirstOrDefault(m=>MatchesPreference(m,saved.Value))?.Id,
                    Direction=1,Reason="saved-preference",At=at,Priority=2,Identity="settings"});
            }
            foreach(var session in state.Sessions)
            {
                DateTimeOffset started,ended;
                if(!FocusMissionPolicy.IsFocus(session)||session.Status!=ParticipationState.RewardCommitted
                    ||!DateTimeOffset.TryParse(session.StartedUtc,out started)||!DateTimeOffset.TryParse(session.EndedUtc,out ended)
                    ||ended<started||ended>now)continue;
                var definition=FocusMissionCatalog.FindForSession(session);
                if(definition==null||session.FocusStepRecords==null)continue;
                // Read only exact choices whose own prompt supports a future preference. Merely
                // choosing today's practice, an unconfirmed draft, or arbitrary free text is not it.
                FocusStepRecord record=null;string method=null,reason="focus-method-preference";int direction=1;
                if(definition.Code=="P07")
                {
                    record=ConfirmedChoice(session,definition,"next",started,ended);
                    if(record==null||record.Value=="잘 모르겠음")continue;
                    method=record.Value;
                }
                else if(definition.Code=="P21")
                {
                    record=ConfirmedChoice(session,definition,"save",started,ended);
                    var activity=ConfirmedChoice(session,definition,"activity",started,ended);
                    if(record?.Value!="내 쉬는 방법으로 저장"||activity==null)continue;
                    method=activity.Value;
                }
                else if(definition.Code=="P28")
                {
                    record=ConfirmedChoice(session,definition,"next-activity",started,ended);
                    if(record==null)continue;
                    method=record.Value;
                }
                else if(definition.Code=="P14")
                {
                    record=ConfirmedChoice(session,definition,"preference",started,ended);
                    var activity=ConfirmedChoice(session,definition,"activity",started,ended);
                    // "As now" keeps the current ranking input, rather than erasing a previous
                    // preference/less-often response and thereby changing it.
                    if(record==null||activity==null||record.Value=="잘 모르겠음"||record.Value=="지금처럼")continue;
                    method=activity.Value;
                    direction=record.Value=="더 자주"?1:-1;
                    reason=direction>0?"focus-frequency-more":"focus-frequency-less";
                }
                else continue;
                var answered=DateTimeOffset.Parse(record.ConfirmedUtc);DateTimeOffset updated;
                if(DateTimeOffset.TryParse(record.UpdatedUtc,out updated)&&updated>answered&&updated<=ended)answered=updated;
                signals.Add(new PreferenceSignal {MissionId=AdditionalForMethod(method),Direction=direction,Reason=reason,
                    At=answered,Priority=1,Identity=session.SessionId??""});
            }
            // The newest explicit answer wins. Unknown/skip/free text never erases an older one.
            // A valid but unmapped choice (or P07 'none') removes an obsolete preference boost;
            // it does not turn body contact into stretching or ambient sound into music.
            return signals.OrderByDescending(signal=>signal.At).ThenByDescending(signal=>signal.Priority)
                .ThenBy(signal=>signal.Identity,StringComparer.Ordinal).FirstOrDefault();
        }
        static FocusStepRecord ConfirmedChoice(ActivitySession session,FocusMissionDefinition definition,string key,DateTimeOffset started,DateTimeOffset ended)
        {
            var step=definition.Steps.FirstOrDefault(item=>item.Key==key);
            var records=session.FocusStepRecords.Where(item=>item.Key==key).ToArray();
            if(step==null||records.Length!=1)return null;
            var record=records[0];DateTimeOffset at;
            return !record.Skipped&&step.Options.Contains(record.Value)&&DateTimeOffset.TryParse(record.ConfirmedUtc,out at)
                &&at>=started&&at<=ended?record:null;
        }
        static string AdditionalForMethod(string method)
            =>method=="호흡"?"free-mission:M17":method=="잠시 멈춤"?"free-mission:M09":null;
        static bool MatchesPreference(FocusMissionDefinition mission,string value)
        {
            if(string.IsNullOrWhiteSpace(value))return false;
            if(mission.Title==value)return true;
            return value=="호흡 관찰"&&mission.Code=="M17"||value=="몸 살펴보기"&&mission.Code=="M05"
                ||value=="잠시 멈추기"&&mission.Code=="M09";
        }
    }

}
