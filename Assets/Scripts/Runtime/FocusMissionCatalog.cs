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
    }
    public sealed class FocusMissionDefinition
    {
        public string Id, Code, Title, Overview, Role, BackgroundHex, AccentHex, TextHex, CircleHex, IconAsset, CompleteAsset;
        public FocusMissionStep[] Steps;
    }
    public static class FocusMissionCatalog
    {
        public const int Version = 2;
        public const string BasicRole = "basic", AdditionalRole = "additional";
        public const string StoryText = "아침에 창문을 열자 서늘한 바람이 방으로 들어왔습니다. 창가의 작은 화분에는 새 잎이 하나 나 있었습니다. 물을 조금 주고 잠시 바라보니, 잎 위에 남은 물방울이 빛을 받아 반짝였습니다. 밖에서는 자전거가 지나가는 소리가 들렸습니다. 나는 따뜻한 컵을 두 손으로 감싸고, 오늘 해야 할 일 중 한 가지만 천천히 시작하기로 했습니다.";
        static FocusMissionStep S(string key,string title,string body,string action="계속",string kind="observe",string[] options=null,bool input=false,string hint=null,int seconds=0,string condition=null,string[] when=null,bool required=false)
            =>new FocusMissionStep {Key=key,Title=title,Body=body,ActionLabel=action,Kind=kind,Options=options??Array.Empty<string>(),OptionalInput=input,InputHint=hint,RecommendedSeconds=seconds,ConditionKey=condition,ConditionValues=when??Array.Empty<string>(),RequiredAnswer=required};
        static string[] O(params string[] options)=>options;
        static FocusMissionDefinition D(string code,string title,string overview,string background,string accent,string circle,params FocusMissionStep[] steps)
            =>new FocusMissionDefinition {Code=code,Id=(code[0]=='P'?"course:":"free-mission:")+code,Role=code[0]=='P'?BasicRole:AdditionalRole,Title=title,Overview=overview,BackgroundHex=background,AccentHex=accent,CircleHex=circle,TextHex=code=="P10"?"#224B49":code=="M03"?"#163D50":"#183845",IconAsset="FocusMissions/Catalog/"+code,Steps=steps};
        public static readonly FocusMissionDefinition[] All={
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
        public static FocusMissionDefinition[] Basic=>All.Where(m=>m.Role==BasicRole).ToArray();
        public static FocusMissionDefinition[] Additional=>All.Where(m=>m.Role==AdditionalRole).ToArray();
        public static FocusMissionDefinition Find(string idOrCode)=>Find(idOrCode,Version);
        public static FocusMissionDefinition Find(string idOrCode,int version)=>(version==1?Legacy:All).FirstOrDefault(m=>m.Id==idOrCode||m.Code==idOrCode);
        public static FocusMissionDefinition FindForSession(ActivitySession session)=>session==null?null:Find(session.MissionId,session.FocusVersion);
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
}
