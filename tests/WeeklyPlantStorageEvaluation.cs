using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using CapstoneDesign.Runtime.LocalState;

class WeeklyPlantStorageEvaluation
{
    static double P95(List<double> values)=>values.OrderBy(x=>x).ElementAt((int)Math.Ceiling((values.Count-1)*.95));
    static void Main()
    {
        string output=Environment.GetEnvironmentVariable("CAPSTONE_PLANT_EVALUATION_OUT");
        if(string.IsNullOrWhiteSpace(output))throw new ArgumentException("Explicit synthetic artifact output is required.");
        Directory.CreateDirectory(output);var rows=new List<object>();
        foreach(int count in new[]{1,8,32})
        {
            var state=new GardenState{InstallationId="synthetic-storage-evaluation",MigrationCompleted=true};
            var epoch=DateTimeOffset.Parse("2026-01-01T12:00:00+09:00");
            WeeklyPlants.Migrate(state,epoch.ToString("O"),false);
            for(int i=0;i<count*21;i++)
            {
                var now=epoch.AddDays(i/3);string cycle=now.ToString("yyyy-MM-dd");
                if(state.LastCycleId!=cycle){state.LastCycleId=cycle;state.DailyDecisions.Add(new DailyDecision{CycleId=cycle,TimeZoneId="Synthetic KST",AppliedUtc=now.ToString("O"),VisitorId=""});}
                var session=new ActivitySession{SessionId="synthetic-session-"+i,MissionId="free-mission:M17",InstanceId=cycle,
                    SelectedUtc=now.ToString("O"),StartedUtc=now.ToString("O"),EndedUtc=now.ToString("O"),Status=ParticipationState.RewardCommitted};
                state.Sessions.Add(session);WeeklyPlants.Complete(state,session,now.ToString("O"),540);
            }
            for(int i=0;i<210;i++)
            {
                string id="synthetic-experience-"+i;
                state.Experiences.Add(new ExperienceRecord{Id=id,CreatedUtc=epoch.ToString("O"),Type="중립",Event="Synthetic benchmark record"});
                WeeklyPlants.AddMemory(state,state.PlantCollection.Plants[i%count].Id,MemoryRecordKind.Experience,id,epoch.ToString("O"),540);
            }
            StateCodec.Validate(state);StateCodec.Clone(state);StateCodec.Encode(state);
            var cloning=new List<double>();var serializing=new List<double>();var saving=new List<double>();long xmlBytes=0;
            string path=Path.Combine(output,"synthetic-storage-"+count+".xml");
            using(var store=new FileStateStore(path))
            {
                for(int repeat=0;repeat<30;repeat++)
                {
                    var timer=Stopwatch.StartNew();StateCodec.Clone(state);cloning.Add(timer.Elapsed.TotalMilliseconds);
                    timer.Restart();var xml=StateCodec.Encode(state);serializing.Add(timer.Elapsed.TotalMilliseconds);xmlBytes=System.Text.Encoding.UTF8.GetByteCount(xml);
                    timer.Restart();store.Save(state);saving.Add(timer.Elapsed.TotalMilliseconds);
                }
            }
            rows.Add(new{plants=count,activityRecords=count*21,emotionRecords=210,xmlBytes,cloneP95Ms=P95(cloning),serializeP95Ms=P95(serializing),atomicFileSaveP95Ms=P95(saving)});
        }
        File.WriteAllText(Path.Combine(output,"storage-report.json"),JsonSerializer.Serialize(new{protocol="weekly-plant-storage-v1",environment="Desktop .NET; synthetic records; 30 repetitions; file flush included; not Android",rows},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Storage evaluation passed: 1/8/32 plants with 210 synthetic emotion records.");
    }
}
