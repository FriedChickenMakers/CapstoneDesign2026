using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CapstoneDesign.Plants
{
    [Serializable] public sealed class PlantCredit
    {
        public string CycleId,MissionId,PlantId;
        public int Category,MappingVersion=1;
        public long Sequence;
    }
    [Serializable] public sealed class PlantCommandReceipt
    {
        public string RequestId,Kind,Payload,PlantId;
        public int AfterStep,Cost;
    }
    [Serializable] public sealed class PlantRewardReceipt { public string CycleId,MissionId; }
    [Serializable] public sealed class PlantGardenState
    {
        public int Version=1,Nutrients;
        public string ActivePlantId;
        public long LastCreditSequence;
        public List<PlantRecipe> Plants=new List<PlantRecipe>();
        public List<PlantCredit> Credits=new List<PlantCredit>();
        public List<PlantRewardReceipt> Rewards=new List<PlantRewardReceipt>();
        public List<PlantCommandReceipt> Commands=new List<PlantCommandReceipt>();
    }
    public interface IPlantStateStore
    {
        PlantGardenState Load();
        // Save must atomically replace the complete aggregate, or throw without replacing it.
        void Save(PlantGardenState candidate);
    }
    public sealed class PlantXmlStore : IPlantStateStore
    {
        readonly string path;
        public PlantXmlStore(string path) { this.path=Path.GetFullPath(path); }
        public PlantGardenState Load()
        { return File.Exists(path)?PlantCodec.Decode<PlantGardenState>(File.ReadAllText(path)):null; }
        public void Save(PlantGardenState candidate)
        {
            PlantGrowthService.Validate(candidate);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                byte[] bytes=Encoding.UTF8.GetBytes(PlantCodec.Encode(candidate));
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                { stream.Write(bytes,0,bytes.Length);stream.Flush(true); }
                if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
            }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
    }
    // Standalone reference service. Do not operate a second wallet alongside LocalGardenState.
    // Production integration must transact these fields in the existing aggregate/store.
    public sealed class PlantGrowthService
    {
        readonly object gate=new object();
        readonly IPlantStateStore store;
        PlantGardenState state;
        public string LastError { get; private set; }
        public PlantGrowthService(IPlantStateStore store,int initialDemoNutrients=0)
        {
            if(store==null) throw new ArgumentNullException(nameof(store));
            this.store=store;
            state=store.Load()??new PlantGardenState { Nutrients=initialDemoNutrients };
            Validate(state);state=PlantCodec.Clone(state);
        }
        public PlantGardenState Snapshot { get { lock(gate) return PlantCodec.Clone(state); } }
        public bool StartPlant(string requestId,string ownerId,string instanceId,int maxGrowthSteps=12,PlantGrowthMode growthMode=PlantGrowthMode.NutrientPurchases)
        {
            if(string.IsNullOrWhiteSpace(ownerId)||string.IsNullOrWhiteSpace(instanceId)) return Fail("Stable IDs required.");
            try { PlantGenerator.ValidateSettings(maxGrowthSteps,growthMode); } catch(ArgumentException e) { return Fail(e.Message); }
            string seed=PlantMath.Hash("GardenPlant","1",ownerId,instanceId);
            string payload=maxGrowthSteps==12&&growthMode==PlantGrowthMode.NutrientPurchases?PlantMath.Hash(instanceId,seed):PlantMath.Hash(instanceId,seed,PlantMath.Number(maxGrowthSteps),growthMode.ToString());
            return Transact(requestId,"start",payload,instanceId,c=> {
                if(c.Plants.Any(p=>p.InstanceId==instanceId)||c.Plants.Any(p=>!p.Mature)) throw new InvalidOperationException("Only one growing plant is allowed.");
                c.Plants.Add(PlantGenerator.Create(ownerId,instanceId,maxGrowthSteps,growthMode));c.ActivePlantId=instanceId;
            });
        }
        public bool CompleteActivity(string requestId,string plantId,string cycleId,string missionId,GrowthCategory category,bool rewardEligible=true,bool manuallyCompleted=true)
        {
            if(string.IsNullOrWhiteSpace(missionId)||!ValidCycle(cycleId)||(int)category<0||(int)category>=6) return Fail("Explicit mission category and cycle are required.");
            string payload=PlantMath.Hash(plantId??"",cycleId,missionId,PlantMath.Number((int)category),rewardEligible?"reward":"review",manuallyCompleted?"complete":"incomplete");
            return Transact(requestId,"activity",payload,plantId,c=> {
                if(!manuallyCompleted) throw new InvalidOperationException("Only explicit completion counts.");
                var plant=c.Plants.SingleOrDefault(p=>p.InstanceId==plantId);
                if(plant==null) throw new InvalidOperationException("Unknown beneficiary plant.");
                // Existing reward eligibility must be supplied by the host, never guessed here.
                if(rewardEligible&&!c.Rewards.Any(r=>r.CycleId==cycleId&&r.MissionId==missionId))
                { c.Nutrients=checked(c.Nutrients+5);c.Rewards.Add(new PlantRewardReceipt { CycleId=cycleId,MissionId=missionId }); }
                if(plant.Mature||plant.InstanceId!=c.ActivePlantId||c.Credits.Any(r=>r.CycleId==cycleId&&r.MissionId==missionId)||c.Credits.Count(r=>r.CycleId==cycleId)>=3) return;
                c.Credits.Add(new PlantCredit { CycleId=cycleId,MissionId=missionId,Category=(int)category,PlantId=plantId,Sequence=++c.LastCreditSequence });
                plant.Counts[(int)category]=checked(plant.Counts[(int)category]+1);
                if(plant.GrowthMode==PlantGrowthMode.MissionCompletions)
                {
                    int target=PlantGenerator.MissionGrowthStep(plant),index=c.Plants.IndexOf(plant);
                    // A short custom course can reach the differentiation threshold before six
                    // credits. Keep those credits and catch up atomically when the gate opens.
                    while(plant.GrowthStep<target) plant=PlantGenerator.Advance(plant);
                    c.Plants[index]=plant;
                }
            });
        }
        public bool Grow(string requestId,string plantId)
        {
            return Transact(requestId,"grow",PlantMath.Hash(plantId??"","10"),plantId,c=> {
                int index=c.Plants.FindIndex(p=>p.InstanceId==plantId);
                if(index<0||c.ActivePlantId!=plantId||c.Nutrients<10) throw new InvalidOperationException("Active plant and ten nutrients required.");
                if(c.Plants[index].GrowthMode!=PlantGrowthMode.NutrientPurchases) throw new InvalidOperationException("Mission-based plants grow through credited completions, not nutrient purchases.");
                c.Plants[index]=PlantGenerator.Advance(c.Plants[index]);
                c.Nutrients-=10;
            });
        }
        bool Transact(string id,string kind,string payload,string plantId,Action<PlantGardenState> apply)
        {
            lock(gate)
            {
                LastError=null;
                if(string.IsNullOrWhiteSpace(id)) return Fail("Request ID required.");
                var prior=state.Commands.SingleOrDefault(r=>r.RequestId==id);
                if(prior!=null) return prior.Kind==kind&&prior.Payload==payload ? true:Fail("Request ID was reused with another payload.");
                try
                {
                    var candidate=PlantCodec.Clone(state);apply(candidate);
                    candidate.Commands.Add(new PlantCommandReceipt { RequestId=id,Kind=kind,Payload=payload,PlantId=plantId,
                        AfterStep=kind=="grow"?candidate.Plants.Single(p=>p.InstanceId==plantId).GrowthStep:0,Cost=kind=="grow"?10:0 });
                    Validate(candidate);
                    // An injected store cannot mutate the object that is published after success.
                    store.Save(PlantCodec.Clone(candidate));state=candidate;return true;
                }
                catch(Exception e) when(e is ArgumentException||e is InvalidOperationException||e is IOException||e is UnauthorizedAccessException||e is OverflowException)
                { return Fail(e.Message); }
            }
        }
        bool Fail(string message) { LastError=message;return false; }
        static bool ValidCycle(string cycle)
        { DateTime date;return DateTime.TryParseExact(cycle,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date); }
        public static void Validate(PlantGardenState state)
        {
            if(state==null||state.Version!=1||state.Nutrients<0||state.Plants==null||state.Credits==null||state.Rewards==null||state.Commands==null) throw new ArgumentException("Invalid plant state.");
            var ids=new HashSet<string>();
            foreach(var p in state.Plants) { PlantGenerator.Validate(p);if(!ids.Add(p.InstanceId)) throw new ArgumentException("Duplicate plant ID."); }
            if(state.Plants.Count(p=>!p.Mature)>1||(state.Plants.Count>0&&!ids.Contains(state.ActivePlantId))||state.Plants.Any(p=>!p.Mature&&p.InstanceId!=state.ActivePlantId)) throw new ArgumentException("Invalid active plant.");
            var keys=new HashSet<string>();long sequence=0;
            foreach(var c in state.Credits)
            {
                if(c==null||!ValidCycle(c.CycleId)||string.IsNullOrWhiteSpace(c.MissionId)||!ids.Contains(c.PlantId)||c.Category<0||c.Category>=6||c.MappingVersion!=1||c.Sequence!=++sequence||!keys.Add(PlantMath.Hash(c.CycleId,c.MissionId))) throw new ArgumentException("Invalid activity receipt.");
            }
            if(sequence!=state.LastCreditSequence||state.Credits.GroupBy(c=>c.CycleId).Any(g=>g.Count()>3)) throw new ArgumentException("Invalid daily credit limit or sequence.");
            foreach(var p in state.Plants) for(int k=0;k<6;k++) if(p.Counts[k]!=state.Credits.Count(c=>c.PlantId==p.InstanceId&&c.Category==k)) throw new ArgumentException("Counts disagree with receipts.");
            keys.Clear();
            foreach(var r in state.Rewards) if(r==null||!ValidCycle(r.CycleId)||string.IsNullOrWhiteSpace(r.MissionId)||!keys.Add(PlantMath.Hash(r.CycleId,r.MissionId))) throw new ArgumentException("Invalid reward receipt.");
            keys.Clear();
            foreach(var r in state.Commands)
            {
                if(r==null||string.IsNullOrWhiteSpace(r.RequestId)||!keys.Add(r.RequestId)||!ids.Contains(r.PlantId)||r.Payload==null||r.Payload.Length!=64||(r.Kind!="start"&&r.Kind!="activity"&&r.Kind!="grow")) throw new ArgumentException("Invalid command receipt.");
                if(r.Kind=="grow"&&(r.Cost!=10||r.AfterStep<1||r.AfterStep>state.Plants.Single(p=>p.InstanceId==r.PlantId).MaxGrowthSteps)) throw new ArgumentException("Invalid growth receipt.");
            }
            foreach(var p in state.Plants)
            {
                var steps=state.Commands.Where(r=>r.Kind=="grow"&&r.PlantId==p.InstanceId).Select(r=>r.AfterStep).OrderBy(x=>x).ToArray();
                if(p.GrowthMode==PlantGrowthMode.MissionCompletions)
                {
                    if(steps.Length!=0||p.GrowthStep!=PlantGenerator.MissionGrowthStep(p)) throw new ArgumentException("Mission credits disagree with growth.");
                }
                else if(!steps.SequenceEqual(Enumerable.Range(1,p.GrowthStep))) throw new ArgumentException("Growth receipts disagree with plant.");
            }
        }
    }
}
