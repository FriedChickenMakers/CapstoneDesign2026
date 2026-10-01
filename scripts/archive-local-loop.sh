#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
: "${CAPSTONE_ARTIFACTS:?Set an existing private run artifact directory}"
python3 - "$project_dir" "$CAPSTONE_ARTIFACTS" <<'PY'
import hashlib,json,pathlib,subprocess,sys,tarfile,datetime
root=pathlib.Path(sys.argv[1]);out=pathlib.Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
# Explicit source roots only: no app saves, credentials, Library, logs or device captures.
files=[]
for name in ['Assets','Packages','ProjectSettings','scripts','tests','docs']:
 files.extend(p for p in (root/name).rglob('*') if p.is_file())
files.extend(p for p in [root/'.gitignore',root/'README.md',root/'external_assets/README.md'] if p.exists())
entries={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(files)}
with tarfile.open(out/'final-source.tar.gz','w:gz') as archive:
 for p in files:archive.add(p,arcname=str(p.relative_to(root)))
apks={str(p.relative_to(out)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (out/'final'/'build').glob('*.apk')}
manifest={'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'baseHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'branch':subprocess.check_output(['git','branch','--show-current'],cwd=root,text=True).strip(),'sourceSha256':entries,'apkSha256':apks,'deviceTestedThisRun':False}
if (out/'review-repository'/'.git').exists():manifest['reviewCommit']=subprocess.check_output(['git','rev-parse','HEAD'],cwd=out/'review-repository',text=True).strip()
(out/'source-and-apk-manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n')
(out/'final-working-tree.patch').write_bytes(subprocess.check_output(['git','diff','HEAD','--binary'],cwd=root))
(out/'final-status.txt').write_bytes(subprocess.check_output(['git','status','--short'],cwd=root))
print(out/'source-and-apk-manifest.json')
PY
