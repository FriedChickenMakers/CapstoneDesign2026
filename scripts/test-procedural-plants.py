#!/usr/bin/env python3
"""Compile and run the real generator/mesh contracts without Unity or NuGet.

Set DOTNET_BIN to a .NET 8+ host, or UNITY_MONO_ROOT to Unity's MonoBleedingEdge.
The direct compiler invocation also works in containers without process metadata.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[1]
sources = sorted((root / 'Assets/Scripts/ProceduralPlants').glob('*.cs'))
sources = [p for p in sources if p.name != 'ProceduralPlantView.cs']
sources += [root / 'Assets/Tests/ProceduralPlants/Editor/PlantContractChecks.cs', root / 'tests/PlantGenerationTests.cs']
host = os.environ.get('DOTNET_BIN') or shutil.which('dotnet')
with tempfile.TemporaryDirectory(prefix='synthetic-plant-checks-') as temporary:
    out = Path(temporary)
    if host:
        install = Path(host).resolve().parent
        def version(path):
            return tuple(int(x) for x in Path(path).name.split('.') if x.isdigit())
        sdk = sorted((install / 'sdk').glob('*'), key=version)[-1]
        refs = sorted((install / 'packs/Microsoft.NETCore.App.Ref').glob('*'), key=version)[-1]
        refdir = next((refs / 'ref').glob('net*'))
        runtime = sorted((install / 'shared/Microsoft.NETCore.App').glob('*'), key=version)[-1]
        target = out / 'PlantTests.dll'
        (out / 'PlantTests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': refdir.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        command = [host, str(sdk / 'Roslyn/bincore/csc.dll'), '-nologo', '-noconfig', '-nostdlib+',
                   '-langversion:8', '-target:exe', '-out:' + str(target)]
        command += ['-r:' + str(p) for p in refdir.glob('*.dll')]
        result = subprocess.run(command + [str(p) for p in sources])
        if result.returncode:
            sys.exit(result.returncode)
        result = subprocess.run([host, str(target)])
        if result.returncode:
            sys.exit(result.returncode)
    else:
        mono = Path(os.environ.get('UNITY_MONO_ROOT', '/opt/unity/editors/6000.3.24f1/Editor/Data/MonoBleedingEdge'))
        target = out / 'PlantTests.exe'
        subprocess.run([str(mono / 'bin/mono'), str(mono / 'lib/mono/4.5/mcs.exe'), '-langversion:latest',
                        '-r:System.Xml.dll', '-r:System.Core.dll', '-out:' + str(target)] + [str(p) for p in sources], check=True)
        subprocess.run([str(mono / 'bin/mono'), str(target)], check=True)
