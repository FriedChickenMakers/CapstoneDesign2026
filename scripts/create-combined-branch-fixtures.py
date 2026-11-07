from pathlib import Path
import subprocess, os, json
root=Path(__file__).resolve().parents[1]
out=root/'artifacts/branch-save-fixtures'
mono=Path(os.environ['ProgramFiles'])/'Unity/Hub/Editor/6000.3.24f1/Editor/Data/MonoBleedingEdge'
program=r'''
using System; using System.IO; using System.Linq;
using CapstoneDesign.Runtime.LocalState;
public static class BranchFixture {
 sealed class Clock:IClock { public DateTimeOffset Time=DateTimeOffset.Parse("2026-10-26T12:00:00+09:00"); public DateTimeOffset UtcNow=>Time; public TimeZoneInfo TimeZone=>TimeZoneInfo.CreateCustomTimeZone("Synthetic",TimeSpan.FromHours(9),"Synthetic","Synthetic"); }
 sealed class Store:IStateStore { public string Data; public GardenState Load()=>Data==null?null:StateCodec.Decode(Data); public void Save(GardenState s){Data=StateCodec.Encode(s);} }
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 static void Complete(GardenStateService service,string id,string code,int order=0){
  Check(service.BeginFocusSession(id,code,order),service.LastError);Check(service.StartSession(id),service.LastError);
  var session=service.Snapshot.Sessions.First(s=>s.SessionId==id);var definition=FocusMissionCatalog.FindForSession(session);
  for(int i=0;i<definition.Steps.Length;i++)Check(service.ConfirmFocusStep(id,i,null,true),service.LastError);
  Check(service.CompleteFocusSession(id,"synthetic calm","synthetic branch fixture"),service.LastError);
 }
 public static void Main(string[] args){var clock=new Clock();var store=new Store();
  using(var service=new GardenStateService(store,clock,new DemoBalanceConfig{VisitorChancePercent=0},new LegacySnapshot{Nutrient=137})){
   Check(service.Open(),service.LastError);Check(service.SelectMbctProgram("8w"),service.LastError);
   Complete(service,"synthetic-p01","P01",1);Complete(service,"synthetic-m01","M01");
#if PREVIOUS
   string daily=DailyEmotionPolicy.RecordId(service.CurrentCycleId);Check(service.BeginDailyEmotion(service.CurrentCycleId),service.LastError);
   Check(service.CompleteDailyEmotion(daily,"synthetic calm","synthetic archived daily memory"),service.LastError);
   string plant=service.Snapshot.PlantAppearance.Recipe.InstanceId;Check(service.SetPlantCompleted(plant,true),service.LastError);
   Check(service.SavePlantPhoto("synthetic-archive",plant),service.LastError);Check(service.StartNewPlant("synthetic-next",plant),service.LastError);
#endif
   clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(new string[0]),service.LastError);
   Check(service.BeginFocusSession("synthetic-p02-active","P02",2),service.LastError);Check(service.StartSession("synthetic-p02-active"),service.LastError);
   File.WriteAllText(args[0],store.Data);
   Console.WriteLine("FIXTURE_PASS schema="+service.Snapshot.SchemaVersion+" focus="+service.Snapshot.Sessions.First().FocusVersion+" balance="+service.Snapshot.Nutrient);
  }
 }
}
'''
for label,ref in [('previous','a05634cbf6f33161dfb3a2a12b294b68356cd2ab'),('latest','255fb987b4a4f56768d0d626cefff9262f74d6c9')]:
 target=out/label;target.mkdir(parents=True,exist_ok=True)
 runtime=['LocalGardenState','FocusMissionCatalog','FocusDrawingData','GardenPlantAppearance','MindfulnessContent','MbctContent']
 plants=['Catalog','Math','Recipe','Generator','Geometry']
 if label=='latest':runtime+=['CameraMissionPolicy','WeeklyPlantState'];plants+=['FlowerLayout','MemoryGeometry']
 paths=['Assets/Scripts/Runtime/'+n+'.cs' for n in runtime]+['Assets/Scripts/ProceduralPlants/Plant'+n+'.cs' for n in plants]
 sources=[]
 for path in paths:
  p=target/Path(path).name;p.write_bytes(subprocess.check_output(['git','show',ref+':'+path],cwd=root));sources.append(str(p))
 main=target/'BranchFixture.cs';main.write_text(program,encoding='utf-8');sources.append(str(main))
 exe=target/'fixture.exe'
 cmd=[str(mono/'bin/mono.exe'),str(mono/'lib/mono/4.5/mcs.exe'),'-langversion:latest','-r:System.Xml.dll','-r:System.Core.dll','-out:'+str(exe)]
 if label=='previous':cmd+=['-define:PREVIOUS']
 compile_result=subprocess.run(cmd+sources,capture_output=True,text=True,encoding='utf-8',errors='replace')
 (target/'compile.log').write_text(compile_result.stdout+compile_result.stderr,encoding='utf-8')
 if compile_result.returncode:raise RuntimeError(label+' compilation failed: '+compile_result.stdout+compile_result.stderr)
 result=subprocess.run([str(mono/'bin/mono.exe'),str(exe),str(target/'state.xml')],capture_output=True,text=True,encoding='utf-8',errors='replace')
 (target/'run.log').write_text(result.stdout+result.stderr,encoding='utf-8')
 print(label,result.stdout.lstrip('\ufeff').strip())
 if result.returncode:raise RuntimeError(label+' fixture failed: '+result.stderr)
 (target/'provenance.json').write_text(json.dumps({'ref':ref,'synthetic':True,'generatedByOriginalBranchCode':True},indent=2))
