"""Compile the original incoming writer to protect already-created v4 sessions."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
REF = "3ac0c8078735b49e0983dff6de4c6e537660f8f0"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--output", required=True, type=Path)
args = parser.parse_args()
output = args.output.resolve()
if output.is_relative_to(ROOT) and not output.is_relative_to(ROOT / "artifacts"):
    raise SystemExit("Synthetic fixtures must remain in ignored artifacts or outside the repository")
output.mkdir(parents=True, exist_ok=True)
mono = Path(os.environ.get("UNITY_MONO_ROOT", "/opt/unity/editors/6000.3.24f1/Editor/Data/MonoBleedingEdge"))
mono_bin = mono / "bin" / ("mono.exe" if os.name == "nt" else "mono")
sources = []
for name in (ROOT / "scripts/garden-domain-sources.txt").read_text().splitlines():
    if not name:
        continue
    target = output / Path(name).name
    target.write_bytes(subprocess.check_output(["git", "-c", f"safe.directory={ROOT}", "show", REF + ":" + name], cwd=ROOT))
    sources.append(target)
program = output / "IncomingGuidedFixture.cs"
program.write_text(r'''
using System;using System.IO;using System.Linq;using CapstoneDesign.Runtime.LocalState;
static class IncomingGuidedFixture {
 sealed class Clock:IClock {public DateTimeOffset Time=DateTimeOffset.Parse("2030-01-01T12:00:00Z");public DateTimeOffset UtcNow=>Time;public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;}
 sealed class Store:IStateStore {public GardenState State;public GardenState Load()=>State==null?null:StateCodec.Clone(State);public void Save(GardenState s){State=StateCodec.Clone(s);}}
 static void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
 static ActivitySession Get(GardenStateService service,string id)=>service.Snapshot.Sessions.Single(s=>s.SessionId==id);
 static void Finish(GardenStateService service,string id){
  var definition=FocusMissionCatalog.FindForSession(Get(service,id));
  while(Get(service,id).InstructionStep<definition.Steps.Length){
   int i=Get(service,id).InstructionStep;var step=definition.Steps[i];
   string answer=step.Options.FirstOrDefault()??(step.RequiredAnswer?"synthetic original answer":"");
   Require(service.ConfirmFocusStep(id,i,answer,answer.Length==0),service.LastError);
  }
  Require(service.CompleteFocusSession(id),service.LastError);
 }
 public static void Main(string[] args){
  var clock=new Clock();var store=new Store();
  using(var service=new GardenStateService(store,clock,new DemoBalanceConfig{VisitorChancePercent=0})){
   Require(service.Open()&&service.SelectMbctProgram("8w"),service.LastError);
   Require(FocusMissionCatalog.Version==4,"Fixture writer must be the actual v4 branch");
   for(int order=1;order<=25;order++){
    while(FocusMissionPolicy.Availability(service.Snapshot,"8w")!=null){clock.Time=clock.Time.AddDays(1);Require(service.RefreshCycle(new string[0]),service.LastError);}
    string id="original-v4-p"+order.ToString("00");
    Require(service.BeginFocusSession(id,"P"+order.ToString("00"),order)&&service.StartSession(id),service.LastError);
    if(order==1){Require(service.SaveGuidedFocusDraft(id,"코","",11),service.LastError);File.WriteAllText(Path.Combine(args[0],"p01-active.xml"),StateCodec.Encode(service.Snapshot));}
    if(order==25){
     Require(service.ConfirmFocusStep(id,0)&&service.SaveFocusDraft(id,1,"synthetic original message"),service.LastError);
     File.WriteAllText(Path.Combine(args[0],"p25-active.xml"),StateCodec.Encode(service.Snapshot));
    }
    Finish(service,id);
   }
   Require(service.BeginFocusSession("original-v4-m10","M10")&&service.StartSession("original-v4-m10")&&service.SaveFocusDraft("original-v4-m10",0,"synthetic original plan"),service.LastError);
   File.WriteAllText(Path.Combine(args[0],"p25-completed-m10-active.xml"),StateCodec.Encode(service.Snapshot));
   Require(Get(service,"original-v4-p25").FocusStepRecords[2].Value=="","Old P25 intentionally completed without required choice");
   Console.WriteLine("ORIGINAL_GUIDED_V4_FIXTURES_PASS schema="+service.Snapshot.SchemaVersion+" focus="+FocusMissionCatalog.Version);
  }
 }
}
''', encoding="utf-8")
sources.append(program)
exe = output / "fixture.exe"
compiled = subprocess.run([str(mono_bin), str(mono / "lib/mono/4.5/mcs.exe"), "-langversion:latest", "-r:System.Xml.dll", "-r:System.Core.dll", "-out:" + str(exe)] + [str(p) for p in sources], capture_output=True, text=True)
(output / "compile.log").write_text(compiled.stdout + compiled.stderr)
if compiled.returncode:
    raise RuntimeError(compiled.stdout + compiled.stderr)
result = subprocess.run([str(mono_bin), str(exe), str(output)], capture_output=True, text=True)
(output / "run.log").write_text(result.stdout + result.stderr)
if result.returncode:
    raise RuntimeError(result.stdout + result.stderr)
(output / "provenance.json").write_text(json.dumps({"ref": REF, "synthetic": True, "generatedByOriginalBranchCode": True, "sources": [{"name": p.name, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in sources]}, indent=2))
print(result.stdout.strip())

# The pre-integration guided writer used schema1/focus3. Its fallback routes were
# valid basic completions; v3 texture and v5 must not retroactively redefine them.
older_ref = "a05634cbf6f33161dfb3a2a12b294b68356cd2ab"
older = output / "older-guided"
older.mkdir(parents=True, exist_ok=True)
older_paths = ["Assets/Scripts/Runtime/" + name + ".cs" for name in
               ["LocalGardenState", "FocusMissionCatalog", "FocusDrawingData", "GardenPlantAppearance", "MindfulnessContent", "MbctContent"]]
older_paths += ["Assets/Scripts/ProceduralPlants/Plant" + name + ".cs" for name in ["Catalog", "Math", "Recipe", "Generator", "Geometry"]]
older_sources = []
for name in older_paths:
    target = older / Path(name).name
    target.write_bytes(subprocess.check_output(["git", "-c", f"safe.directory={ROOT}", "show", older_ref + ":" + name], cwd=ROOT))
    older_sources.append(target)
older_program = older / "OriginalSchema1Fixture.cs"
older_text = program.read_text().replace("FocusMissionCatalog.Version==4", "FocusMissionCatalog.Version==3").replace("order<=25", "order<=26")
older_text = older_text.replace('Require(service.ConfirmFocusStep(id,i,answer,answer.Length==0)',
    'if(step.Key=="route"&&definition.Code=="P19")answer="짧은 소리 실습";if(step.Key=="route"&&definition.Code=="P26")answer="아직 없어요";Require(service.ConfirmFocusStep(id,i,answer,answer.Length==0)')
older_text = older_text.replace("ORIGINAL_GUIDED_V4_FIXTURES_PASS", "ORIGINAL_GUIDED_SCHEMA1_FIXTURES_PASS")
older_program.write_text(older_text, encoding="utf-8")
older_sources.append(older_program)
older_exe = older / "fixture.exe"
compiled = subprocess.run([str(mono_bin), str(mono / "lib/mono/4.5/mcs.exe"), "-langversion:latest", "-r:System.Xml.dll", "-r:System.Core.dll", "-out:" + str(older_exe)] + [str(p) for p in older_sources], capture_output=True, text=True)
(older / "compile.log").write_text(compiled.stdout + compiled.stderr)
if compiled.returncode:
    raise RuntimeError(compiled.stdout + compiled.stderr)
result = subprocess.run([str(mono_bin), str(older_exe), str(older)], capture_output=True, text=True)
(older / "run.log").write_text(result.stdout + result.stderr)
if result.returncode:
    raise RuntimeError(result.stdout + result.stderr)
(older / "provenance.json").write_text(json.dumps({"ref": older_ref, "synthetic": True, "generatedByOriginalBranchCode": True, "sources": [{"name": p.name, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in older_sources]}, indent=2))
print(result.stdout.strip())
