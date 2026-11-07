using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CapstoneDesign.Runtime.LocalState
{
    public sealed class MissionPhoto
    {
        public string PolicyVersion=CameraMissionPolicy.Version;
        public string Id, Target, CapturedUtc, ModelVersion, Outcome, MatchedLabel;
        public double Score;
        public bool UserConfirmed;
        internal MissionPhoto Copy() => (MissionPhoto)MemberwiseClone();
    }
    public sealed class PhotoRewardReceipt
    {
        public string Key, Cycle;
        public int Amount;
        internal PhotoRewardReceipt Copy() => (PhotoRewardReceipt)MemberwiseClone();
    }
    public static class CameraMissionPolicy
    {
        public const string Version="photo-v1";
        public const string ModelVersion="efficientdet-lite0-int8-v1";
        public const double Threshold=.60; // Pilot default; not a calibrated probability of participation.
        public static bool ValidId(string id) => id!=null && id==id.ToLowerInvariant() && Guid.TryParseExact(id,"N",out _);
        public static string Target(ActivitySession s) => s==null||(s.FocusVersion!=2&&s.FocusVersion!=3)?null:
            s.MissionId=="free-mission:M03"||s.MissionId=="course:P04"?"container":s.MissionId=="free-mission:M14"?"book":null;
        public static string TargetName(string target) => target=="book"?"종이책":"컵이나 병";
        public static bool AllowedLabel(string target,string label) => target=="container"?(label=="cup"||label=="bottle"):target=="book"&&label=="book";
        public static bool Ready(ActivitySession s) => Target(s)!=null && FocusMissionPolicy.IsActive(s) && s.Status!=ParticipationState.Selected
            && s.FocusStepRecords.Count>0 && s.InstructionStep>=s.FocusStepRecords.Count && s.FocusStepRecords.All(r=>!string.IsNullOrEmpty(r.ConfirmedUtc));
        public static bool Confirmed(MissionPhoto p) => p!=null && (p.Outcome=="matched"||p.UserConfirmed);
        public static string Receipt(ActivitySession s) => Version+"|"+FocusMissionPolicy.RewardReceipt(s);
        public static bool BonusAvailable(GardenState state,ActivitySession s,int amount,int dailyLimit)
        {
            if(s==null)return false;
            var current=s.Copy();current.InstanceId=state.LastCycleId;
            if(current.FocusRole!=FocusMissionCatalog.BasicRole && (state.RewardReceipts.Contains(current.InstanceId+"|"+current.MissionId)
                ||state.Sessions.Any(other=>other.SessionId!=s.SessionId&&FocusMissionPolicy.IsFocus(other)&&other.MissionId==s.MissionId&&other.InstanceId==current.InstanceId&&other.RewardAmount>0)))return false;
            return amount>0 && dailyLimit>0 && Target(current)!=null && !current.FocusReview
                && !state.RewardReceipts.Contains(FocusMissionPolicy.RewardReceipt(current))
                && !state.PhotoRewards.Any(r=>r.Key==Receipt(current)) && state.PhotoRewards.Count(r=>r.Cycle==state.LastCycleId)<dailyLimit;
        }
        internal static void Reconcile(GardenState state)
        {
            foreach(var s in state.Sessions)
                if(!Ready(s)){s.PendingPhotoId=null;s.PendingPhotoTarget=null;}
        }
        internal static void Award(GardenState state,ActivitySession s,int amount,int dailyLimit)
        {
            if(!Confirmed(s.Photo)||s.FocusReview||s.RewardAmount<=0||amount<=0||dailyLimit<=0
                ||state.PhotoRewards.Any(r=>r.Key==Receipt(s))||state.PhotoRewards.Count(r=>r.Cycle==s.InstanceId)>=dailyLimit)return;
            state.PhotoRewards.Add(new PhotoRewardReceipt{Key=Receipt(s),Cycle=s.InstanceId,Amount=amount});
            state.Nutrient=checked(state.Nutrient+amount);s.PhotoBonusAmount=amount;
        }
        public static void ValidatePhoto(MissionPhoto p,string target)
        {
            if(p==null)return;
            if(!ValidId(p.Id)||p.PolicyVersion!=Version||p.Target!=target||target==null||!DateTimeOffset.TryParse(p.CapturedUtc,out _)
                ||p.ModelVersion!=ModelVersion||double.IsNaN(p.Score)||double.IsInfinity(p.Score)||p.Score<0||p.Score>1
                ||(p.Outcome!="matched"&&p.Outcome!="not_found"&&p.Outcome!="unavailable")
                ||(p.Outcome=="matched"&&(!AllowedLabel(target,p.MatchedLabel)||p.Score<Threshold))
                ||(p.MatchedLabel!=null&&p.MatchedLabel.Length>32))throw new InvalidDataException("Invalid mission photo.");
        }
        internal static void Validate(GardenState state)
        {
            if(state.PhotoRewards==null||state.PhotoRewards.Any(r=>r==null||r.Amount<1||r.Amount>100||r.Key==null||!r.Key.StartsWith("photo-v1|",StringComparison.Ordinal)
                ||!DateTime.TryParseExact(r.Cycle,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _))
                ||state.PhotoRewards.Select(r=>r.Key).Distinct().Count()!=state.PhotoRewards.Count)throw new InvalidDataException("Invalid photo rewards.");
            var photoIds=new HashSet<string>();
            foreach(var s in state.Sessions)
            {
                ValidatePhoto(s.Photo,Target(s));
                if(s.Photo!=null&&(!s.PhotoPromptHandled||!photoIds.Add(s.Photo.Id)))throw new InvalidDataException("Duplicate or unregistered photo.");
                if(!string.IsNullOrEmpty(s.PendingPhotoId)&&(!Ready(s)||!ValidId(s.PendingPhotoId)||s.PendingPhotoTarget!=Target(s)||!photoIds.Add(s.PendingPhotoId)))throw new InvalidDataException("Invalid pending photo.");
                if(s.PhotoBonusAmount<0||s.PhotoBonusAmount>100||(s.PhotoBonusAmount>0&&(s.Status!=ParticipationState.RewardCommitted||s.FocusReview||s.RewardAmount<=0
                    ||!state.PhotoRewards.Any(r=>r.Key==Receipt(s)&&r.Amount==s.PhotoBonusAmount&&r.Cycle==s.InstanceId))))throw new InvalidDataException("Invalid photo reward binding.");
            }
        }
    }
    public sealed partial class GardenStateService
    {
        public bool BeginPhotoCapture(string sessionId,string photoId,string target) => Change(next=>
        {
            var s=Session(next,sessionId);
            if(!CameraMissionPolicy.Ready(s)||!CameraMissionPolicy.ValidId(photoId)||target!=CameraMissionPolicy.Target(s))throw new InvalidOperationException("Photo capture is not available for this activity.");
            s.PendingPhotoId=photoId;s.PendingPhotoTarget=target;
        });
        public bool RegisterPhoto(string sessionId,MissionPhoto photo) => Change(next=>
        {
            var s=Session(next,sessionId);
            if(!CameraMissionPolicy.Ready(s)||photo==null||s.PendingPhotoId!=photo.Id||s.PendingPhotoTarget!=photo.Target)throw new InvalidOperationException("Stale photo result.");
            CameraMissionPolicy.ValidatePhoto(photo,CameraMissionPolicy.Target(s));
            s.Photo=photo.Copy();s.PendingPhotoId=null;s.PendingPhotoTarget=null;s.PhotoPromptHandled=true;
        });
        public bool SkipPhoto(string sessionId) => Change(next=>
        {
            var s=Session(next,sessionId);
            if(!CameraMissionPolicy.Ready(s))throw new InvalidOperationException("Photo choice is not available.");
            s.PendingPhotoId=null;s.PendingPhotoTarget=null;s.Photo=null;s.PhotoPromptHandled=true;
        });
        public bool DeletePhoto(string sessionId) => Change(next=>
        {
            var s=Session(next,sessionId);s.Photo=null;s.PendingPhotoId=null;s.PendingPhotoTarget=null;
            // Preserve the reward receipt and amount after deleting the image.
        });
    }
}
