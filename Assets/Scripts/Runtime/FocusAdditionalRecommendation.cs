using System;
using System.Linq;
using System.Collections.Generic;
namespace CapstoneDesign.Runtime.LocalState
{
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
                    // The retained guided P07 asks the same explicit future-preference
                    // question in its final notice slide. Its practice choice is not consent.
                    record=ConfirmedChoice(session,definition,
                        session.FocusVersion==FocusMissionCatalog.Version?"notice":"next",started,ended);
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
