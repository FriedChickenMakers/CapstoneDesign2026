#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
: "${CAPSTONE_ARTIFACTS:?Set an existing private run artifact directory}"
(cd "$project_dir" && python3 scripts/check-repo-privacy.py)
python3 - "$project_dir" "$CAPSTONE_ARTIFACTS" <<'PY'
import hashlib,json,pathlib,subprocess,sys,tarfile,datetime
root=pathlib.Path(sys.argv[1]);out=pathlib.Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
# Include tracked and nonignored source files, never arbitrary files below source roots.
# The privacy gate above also checks newly added files before this bundle is created.
source_roots=['Assets','Packages','ProjectSettings','scripts','tests','docs','.githooks']
source_files=['.gitignore','.gitleaks.toml','AGENTS.md','README.md','external_assets/README.md']
listed=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','-z','--',
                                *source_roots,*source_files],cwd=root)
names=sorted(set(name for name in listed.split(b'\0') if name))
files=[]
for name in names:
 path=root/pathlib.Path(name.decode('utf-8'))
 # A symlink anywhere in the path could lead outside the checked source tree.
 if any(part.is_symlink() for part in [path,*path.parents] if part!=root):continue
 if path.is_file():files.append(path)
# --cached still lists tracked files covered by a newer ignore rule; exclude them too.
safe_names=b''.join(str(path.relative_to(root)).encode('utf-8')+b'\0' for path in files)
ignored=subprocess.run(['git','check-ignore','--no-index','--stdin','-z'],input=safe_names,
                       stdout=subprocess.PIPE,cwd=root,check=False)
if ignored.returncode not in (0,1):raise SystemExit('Unable to verify source ignore rules')
ignored_names=set(ignored.stdout.split(b'\0'))
files=[path for path in files if str(path.relative_to(root)).encode('utf-8') not in ignored_names]
entries={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(files)}
def source_metadata(info):
 # Do not publish the workstation account stored in tar owner metadata.
 if not info.isfile():return None
 info.uid=info.gid=0;info.uname=info.gname=''
 return info
with tarfile.open(out/'final-source.tar.gz','w:gz') as archive:
 for p in files:archive.add(p,arcname=str(p.relative_to(root)),recursive=False,filter=source_metadata)
apks={str(p.relative_to(out)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (out/'final'/'build').glob('*.apk')}
manifest={'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'baseHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'branch':subprocess.check_output(['git','branch','--show-current'],cwd=root,text=True).strip(),'sourceSha256':entries,'apkSha256':apks,'deviceTestedThisRun':False}
if (out/'review-repository'/'.git').exists():manifest['reviewCommit']=subprocess.check_output(['git','rev-parse','HEAD'],cwd=out/'review-repository',text=True).strip()
(out/'source-and-apk-manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n')
# Removed diff lines can contain values that the current worktree has sanitized.
# The source archive carries current content; publish only a change summary here.
(out/'final-change-summary.txt').write_bytes(subprocess.check_output(['git','diff','HEAD','--stat'],cwd=root))
(out/'final-status.txt').write_bytes(subprocess.check_output(['git','status','--short'],cwd=root))
print(out/'source-and-apk-manifest.json')
PY
