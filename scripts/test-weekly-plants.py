#!/usr/bin/env python3
"""Run pure C# plant/state contracts with .NET or Unity Mono (no NuGet).

The optional performance evaluation programs require .NET 8+.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[1]
host = os.environ.get('DOTNET_BIN') or shutil.which('dotnet')
def version(path):
    return tuple(int(x) for x in path.name.split('.') if x.isdigit())
if host:
    install = Path(host).resolve().parent
    sdk = sorted((install / 'sdk').glob('*'), key=version)[-1]
    refs = sorted((install / 'packs/Microsoft.NETCore.App.Ref').glob('*'), key=version)[-1]
    refdir = next((refs / 'ref').glob('net*'))
    runtime = sorted((install / 'shared/Microsoft.NETCore.App').glob('*'), key=version)[-1]
sources = [root / line for line in (root / 'scripts/garden-domain-sources.txt').read_text().splitlines() if line.strip()]
tests = sys.argv[1:] or ['WeeklyPlantTests']
with tempfile.TemporaryDirectory(prefix='synthetic-weekly-plants-') as temporary:
    out = Path(temporary)
    for test in tests:
        if not host:
            mono = Path(os.environ.get('UNITY_MONO_ROOT', '/opt/unity/editors/6000.3.24f1/Editor/Data/MonoBleedingEdge'))
            target = out / (test + '.exe')
            subprocess.run([str(mono / 'bin/mono'), str(mono / 'lib/mono/4.5/mcs.exe'), '-langversion:latest',
                            '-r:System.Xml.dll', '-r:System.Core.dll', '-out:' + str(target)]
                           + [str(p) for p in sources] + [str(root / 'tests' / (test + '.cs'))], check=True)
            subprocess.run([str(mono / 'bin/mono'), str(target)], check=True)
            continue
        target = out / (test + '.dll')
        target.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': refdir.name, 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        command = [host, str(sdk / 'Roslyn/bincore/csc.dll'), '-nologo', '-noconfig', '-nostdlib+',
                   '-langversion:8', '-target:exe', '-out:' + str(target)]
        command += ['-r:' + str(p) for p in refdir.glob('*.dll')]
        subprocess.run(command + [str(p) for p in sources] + [str(root / 'tests' / (test + '.cs'))], check=True)
        subprocess.run([host, str(target)], check=True)
