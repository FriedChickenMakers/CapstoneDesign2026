using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        PhotoVerificationService photoService;
        string photoRenderedStatus;
        float nextPhotoPoll;
        PhotoVerificationService Photos => photoService??(photoService=new PhotoVerificationService(null));
#if UNITY_EDITOR
        public void SetPhotoProviderForValidation(IMissionPhotoProvider provider) { photoService=new PhotoVerificationService(provider); }
#endif
        bool NeedsPhotoChoice(ActivitySession session) => CameraMissionPolicy.Ready(session)
            && (!session.PhotoPromptHandled||!string.IsNullOrEmpty(session.PendingPhotoId));
        public void TickMissionPhoto()
        {
            if(!opened||screen!="focus-photo"||Time.unscaledTime<nextPhotoPoll)return;
            nextPhotoPoll=Time.unscaledTime+.3f;
            var s=ActiveSession();
            if(s==null||string.IsNullOrEmpty(s.PendingPhotoId))return;
            var result=Photos.Poll(s.PendingPhotoId,s.PendingPhotoTarget,Time.realtimeSinceStartupAsDouble);
            if(result.status!=photoRenderedStatus)ShowMissionPhoto();
        }
        public void ShowMissionPhoto()
        {
            if(!FocusReflectionReady(out var s)||!CameraMissionPolicy.Ready(s))return;
            var target=CameraMissionPolicy.Target(s);
            var result=string.IsNullOrEmpty(s.PendingPhotoId)?null:Photos.Poll(s.PendingPhotoId,target,Time.realtimeSinceStartupAsDouble);
            photoRenderedStatus=result?.status;
            var root=BeginFigmaPage("focus-photo",ShowMissionPhoto,FigmaMissionUi.Background);
            FigmaHeader(root,"사진으로 남겨볼까요?","사진 확인 · 선택");
            var body=FigmaScroll(root,132,504,500);
            string intro=target=="book"?"종이책을 읽었다면 표지나 펼친 책을 찍어보세요.\n화면이나 다른 글을 읽었다면 건너뛰어도 괜찮아요.":"함께한 컵이나 병을 찍어보세요.\n사진을 남기지 않아도 미션은 완료돼요.";
            FigmaMissionUi.Label(body,intro,24,8,342,68,16,FigmaMissionUi.Ink);
            bool bonus=CameraMissionPolicy.BonusAvailable(Service.Snapshot,s,Balance.PhotoVerificationNutrient,Balance.PhotoVerificationDailyLimit);
            FigmaMissionUi.Label(body,bonus?"자동 또는 직접 확인 후 완료하면 코인 +"+Balance.PhotoVerificationNutrient+"\n하루 "+Balance.PhotoVerificationDailyLimit+"회 · 기본 완료 보상과 별도":"사진은 보상과 관계없이 기록으로 남길 수 있어요.",24,88,342,48,14,FigmaMissionUi.Green);
            string photoId=result?.id??s.Photo?.Id;
            bool visible=BuildMissionPhotoPreview(body,photoId,24,204,342,230);
            if(!visible)FigmaMissionUi.Label(body,result?.status=="capturing"?"카메라에서 사진을 찍고 돌아오세요.":result?.status=="processing"?"사진을 준비하고 있어요…":"함께한 대상을 사진 한 장으로 남겨요.",40,272,310,70,16,FigmaMissionUi.Muted,false,TextAnchor.MiddleCenter);
            string status=result==null?(s.Photo==null?"사진은 기기 안에서만 확인해요.":PhotoStatus(s.Photo)):
                result.status=="matched"?"✓ "+(result.label=="cup"?"컵":result.label=="bottle"?"병":"종이책")+"을 찾았어요":
                result.status=="not_found"?"사진에서 "+CameraMissionPolicy.TargetName(target)+"을 찾지 못했어요.\n이대로 등록할까요?":
                result.status=="processing"?"사진에서 대상을 확인하고 있어요…":
                result.status=="capturing"?"촬영을 마치거나 아래에서 건너뛸 수 있어요.":
                result.status=="cancelled"?"촬영을 취소했어요. 다시 찍거나 건너뛰어도 괜찮아요.":"지금은 사진을 자동으로 확인하기 어려워요.\n"+(visible?"사진을 직접 확인하고 등록할 수 있어요.":"다시 찍거나 사진 없이 이어갈 수 있어요.");
            FigmaMissionUi.Label(body,status,24,450,342,64,16,result?.status=="matched"?FigmaMissionUi.Green:FigmaMissionUi.Ink,true).name="Photo.Status";
            FigmaMissionUi.Label(body,"사진은 이 기기에만 보관해요.\n기록에서 사진만 지울 수 있어요.\n실제 활동 수행 여부를 판단하지 않아요.",24,144,342,52,13,FigmaMissionUi.Muted);
            body.sizeDelta=new Vector2(390,530);
            bool busy=result?.status=="capturing"||result?.status=="processing";
            bool canRegister=visible&&result!=null&&!busy&&result.status!="cancelled";
            bool existing=s.Photo!=null&&result==null;
            if(canRegister||existing)
            {
                var captured=result;
                FigmaMissionUi.Button(root,existing?"이 사진으로 이어가기":result.status=="matched"?"확인하고 등록":"직접 확인하고 등록",24,650,342,52,()=>{
                    if(existing)ShowFocusEmotion();else RegisterMissionPhoto(s.SessionId,captured);
                }).name="Photo.Register";
                FigmaMissionUi.Button(root,"다시 찍기",24,708,342,48,()=>StartMissionPhoto(s.SessionId),Color.white,FigmaMissionUi.Green).name="Photo.Retake";
            }
            else if(!busy)FigmaMissionUi.Button(root,target=="book"?"종이책 사진 찍기":"컵·병 사진 찍기",24,650,342,52,()=>StartMissionPhoto(s.SessionId)).name="Photo.Capture";
            FigmaMissionUi.Button(root,target=="book"&&s.Photo==null?"다른 글을 읽었어요 / 사진 건너뛰기":"사진 없이 이어가기",24,768,342,52,()=>SkipMissionPhoto(s.SessionId),Color.clear,FigmaMissionUi.Green,14).name="Photo.Skip";
            FocusEnter(root);
        }
        void StartMissionPhoto(string id)
        {
            var s=ActiveSession();if(s?.SessionId!=id||!CameraMissionPolicy.Ready(s))return;
            string next=Guid.NewGuid().ToString("N");
            if(!FocusSaved(Service.BeginPhotoCapture(id,next,CameraMissionPolicy.Target(s))))return;
            Photos.Delete(s.PendingPhotoId);
            Photos.Start(next,CameraMissionPolicy.Target(s));ShowMissionPhoto();
        }
        void RegisterMissionPhoto(string id,PhotoVerificationResult result)
        {
            var s=ActiveSession();
            if(s?.SessionId!=id||result==null||s.PendingPhotoId!=result.id||Photos.ReadPhoto(result.id)==null)return;
            if(!FocusSaved(Service.RegisterPhoto(id,result.ToPhoto(result.status!="matched"))))return;
            Photos.Delete(s.Photo?.Id);ShowFocusEmotion();
        }
        void SkipMissionPhoto(string id)
        {
            var s=ActiveSession();if(s?.SessionId!=id)return;
            if(!FocusSaved(Service.SkipPhoto(id)))return;
            Photos.Delete(s.PendingPhotoId);Photos.Delete(s.Photo?.Id);ShowFocusEmotion();
        }
        static string PhotoStatus(MissionPhoto photo) => photo.UserConfirmed?"✓ 직접 확인한 사진":"✓ 자동으로 대상을 확인한 사진";
        bool BuildMissionPhotoPreview(Transform root,string id,float x,float y,float width,float height)
        {
            var frame=FigmaMissionUi.Box(root,"사진 미리보기",x,y,width,height,FigmaMissionUi.Soft,16);
            var bytes=Photos.ReadPhoto(id);if(bytes==null)return false;
            var texture=new Texture2D(2,2,TextureFormat.RGB24,false);
            if(!ImageConversion.LoadImage(texture,bytes,true)||texture.width>1280||texture.height>1280)
            { if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture);return false; }
            float scale=Mathf.Min(width/texture.width,height/texture.height);
            float w=texture.width*scale,h=texture.height*scale;
            var rect=FigmaMissionUi.Rect(frame,"Photo.Preview",(width-w)/2,(height-h)/2,w,h);
            var image=rect.gameObject.AddComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            rect.gameObject.AddComponent<MissionPhotoTextureOwner>().Texture=texture;
            return true;
        }
        float BuildMissionPhotoRecord(Transform root,ActivitySession s,float y)
        {
            if(s.Photo==null)return y;
            y=BuildFocusRecordCard(root,"함께한 사진",PhotoStatus(s.Photo)+"\n"+LocalTime(s.Photo.CapturedUtc),y);
            bool visible=BuildMissionPhotoPreview(root,s.Photo.Id,24,y,342,230);
            if(!visible)FigmaMissionUi.Label(root,"사진 파일을 찾지 못했어요.\n활동 기록과 보상은 그대로 남아 있어요.",40,y+70,310,70,16,FigmaMissionUi.Muted);
            y+=246;
            FigmaMissionUi.Button(root,"사진만 삭제",24,y,342,48,()=>{
                if(!FocusSaved(Service.DeletePhoto(s.SessionId)))return;
                Photos.Delete(s.Photo.Id);ShowFocusRecord(s.SessionId);
            },Color.white,FigmaMissionUi.Green).name="Photo.Delete";
            return y+64;
        }
    }
    public sealed class MissionPhotoTextureOwner : MonoBehaviour
    {
        public Texture2D Texture;
        void OnDestroy(){if(Texture!=null){if(Application.isPlaying)Destroy(Texture);else DestroyImmediate(Texture);}}
    }
}
