using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    [Serializable]
    public sealed class PhotoVerificationResult
    {
        public string id, target, status, modelVersion, capturedUtc, label;
        public double score;
        public MissionPhoto ToPhoto(bool directlyConfirmed) => new MissionPhoto {
            Id=id,Target=target,Outcome=status,ModelVersion=modelVersion,CapturedUtc=capturedUtc,
            MatchedLabel=label,Score=score,UserConfirmed=directlyConfirmed
        };
    }
    public interface IMissionPhotoProvider
    {
        string DirectoryPath { get; }
        PhotoVerificationResult Start(string id,string target);
        PhotoVerificationResult Poll(string id);
        void Delete(string id);
        void Prune(string[] keep);
    }
    // Injected only by explicit Editor tests. Desktop builds never pretend to run CV.
    public sealed class NativeMissionPhotoProvider : IMissionPhotoProvider
    {
        const string Bridge="com.capstonedesign2026.platform.AndroidPlatformBridge";
        static string Call(string method,params object[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var bridge=new AndroidJavaClass(Bridge))
                return bridge.CallStatic<string>(method,new object[]{activity}.Concat(args).ToArray());
#else
            return null;
#endif
        }
        static void CallVoid(string method,params object[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var bridge=new AndroidJavaClass(Bridge))
                bridge.CallStatic(method,new object[]{activity}.Concat(args).ToArray());
#endif
        }
        public string DirectoryPath => Call("missionPhotoDirectory");
        static PhotoVerificationResult Parse(string json) => string.IsNullOrEmpty(json)?null:JsonUtility.FromJson<PhotoVerificationResult>(json);
        public PhotoVerificationResult Start(string id,string target) => Parse(Call("startMissionPhoto",id,target));
        public PhotoVerificationResult Poll(string id) => Parse(Call("pollMissionPhoto",id));
        public void Delete(string id) => CallVoid("cancelMissionPhoto",id);
        public void Prune(string[] keep) => CallVoid("pruneMissionPhotos",string.Join(",",keep));
    }
    public sealed class PhotoVerificationService
    {
        readonly IMissionPhotoProvider provider;
        string timedId;
        double processingSince;
        bool expired;
        public PhotoVerificationService(IMissionPhotoProvider provider) { this.provider=provider??new NativeMissionPhotoProvider(); }
        public static PhotoVerificationResult Unavailable(string id,string target) => new PhotoVerificationResult {
            id=id,target=target,status="unavailable",modelVersion=CameraMissionPolicy.ModelVersion,
            capturedUtc=DateTimeOffset.UtcNow.ToString("O"),label=""
        };
        public void Start(string id,string target)
        {
            if(!CameraMissionPolicy.ValidId(id))return;
            timedId=null;expired=false;
            try{provider.Start(id,target);}catch(Exception){/* Poll offers a non-blocking fallback. */}
        }
        public PhotoVerificationResult Poll(string id,string target,double now)
        {
            if(!CameraMissionPolicy.ValidId(id))return Unavailable(id,target);
            PhotoVerificationResult result;
            try{result=provider.Poll(id);}catch(Exception){result=null;}
            if(result==null||result.id!=id||(result.target!=target&&result.status!="cancelled"))return Unavailable(id,target);
            if(timedId!=id){timedId=id;processingSince=now;expired=false;}
            if(result.status=="capturing")processingSince=now;
            if(result.status=="processing" && now-processingSince>=5)expired=true;
            if(expired)return Unavailable(id,target);
            if(result.status=="capturing"||result.status=="processing"||result.status=="cancelled")return result;
            try{CameraMissionPolicy.ValidatePhoto(result.ToPhoto(false),target);return result;}
            catch(Exception){return Unavailable(id,target);}
        }
        public byte[] ReadPhoto(string id)
        {
            if(!CameraMissionPolicy.ValidId(id))return null;
            try {
                string root=provider.DirectoryPath;if(string.IsNullOrEmpty(root))return null;
                var file=new FileInfo(Path.Combine(root,id+".jpg"));
                return file.Exists&&file.Length>0&&file.Length<=1024*1024?File.ReadAllBytes(file.FullName):null;
            }catch(Exception){return null;}
        }
        public void Delete(string id) { if(CameraMissionPolicy.ValidId(id))try{provider.Delete(id);}catch(Exception){} }
        public void Prune(GardenState state)
        {
            var keep=state.Sessions.SelectMany(s=>new[]{s.Photo?.Id,s.PendingPhotoId}).Where(CameraMissionPolicy.ValidId).Distinct().ToArray();
            try{provider.Prune(keep);}catch(Exception){}
        }
    }
}
