#!/usr/bin/env python3
"""Verify the bundled model/labels, or restore the pinned public model with --download."""
import argparse
import hashlib
import json
from pathlib import Path
import urllib.request
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--download', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
assets = root / 'Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/assets/models'
manifest = json.loads((assets / 'camera-model.json').read_text(encoding='utf-8'))
target = assets / manifest['file']
if args.download:
    with urllib.request.urlopen(manifest['source'], timeout=60) as response:
        data = response.read(manifest['bytes'] + 1)
    if len(data) != manifest['bytes'] or hashlib.sha256(data).hexdigest() != manifest['sha256']:
        raise SystemExit('Downloaded model does not match the pinned artifact.')
    pending = target.with_suffix('.download')
    pending.write_bytes(data)
    pending.replace(target)
data = target.read_bytes()
assert len(data) == manifest['bytes'], 'Model size mismatch'
assert hashlib.sha256(data).hexdigest() == manifest['sha256'], 'Model checksum mismatch'
with zipfile.ZipFile(target) as archive:
    labels = archive.read(manifest['labelsAsset']).decode('utf-8').splitlines()
    for group in manifest['targets'].values():
        for label in group:
            assert labels.count(label) == 1, 'Missing or ambiguous model label: ' + label
print('Camera model: pinned SHA-256, byte length and cup/bottle/book metadata labels verified.')
