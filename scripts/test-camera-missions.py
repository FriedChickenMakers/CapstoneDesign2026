#!/usr/bin/env python3
"""Run photo state and existing mission/state regression tests without Unity/NuGet."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
host = os.environ.get('DOTNET_BIN') or shutil.which('dotnet')
if not host:
    raise SystemExit('A .NET 8+ SDK is required.')
install = Path(host).resolve().parent
def version(path):
    return tuple(int(x) for x in path.name.split('.') if x.isdigit())
sdk = sorted((install / 'sdk').glob('*'), key=version)[-1]
refs = sorted((install / 'packs/Microsoft.NETCore.App.Ref').glob('*'), key=version)[-1]
refdir = next((refs / 'ref').glob('net*'))
runtime = sorted((install / 'shared/Microsoft.NETCore.App').glob('*'), key=version)[-1]
sources = [root / 'Assets/Scripts/Runtime' / (name + '.cs') for name in (
    'LocalGardenState', 'CameraMissionPolicy', 'FocusMissionCatalog', 'FocusDrawingData',
    'GardenPlantAppearance', 'MindfulnessContent', 'MbctContent')]
sources += [root / 'Assets/Scripts/ProceduralPlants' / ('Plant' + name + '.cs')
            for name in ('Catalog', 'Math', 'Recipe', 'Generator', 'Geometry')]
with tempfile.TemporaryDirectory(prefix='synthetic-camera-tests-') as temporary:
    out = Path(temporary)
    for suite in ('CameraMissionTests', 'FocusMissionStateTests', 'LocalGardenStateTests', 'AllMissionFlowTests', 'GardenPlantAppearanceTests'):
        target = out / (suite + '.dll')
        (out / (suite + '.runtimeconfig.json')).write_text(json.dumps({'runtimeOptions': {
            'tfm': refdir.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        command = [host, str(sdk / 'Roslyn/bincore/csc.dll'), '-nologo', '-noconfig', '-nostdlib+',
                   '-langversion:8', '-target:exe', '-out:' + str(target)]
        command += ['-r:' + str(p) for p in refdir.glob('*.dll')]
        subprocess.run(command + [str(p) for p in sources] + [str(root / 'tests' / (suite + '.cs'))], check=True)
        subprocess.run([host, str(target)], check=True)
