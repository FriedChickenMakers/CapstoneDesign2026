#!/usr/bin/env python3
"""Build a local, offline before/after viewer for Unity UI captures."""
import json
import sys
from pathlib import Path

root = Path(sys.argv[1] if len(sys.argv) > 1 else "artifacts/ui-consistency-20261006").resolve()
captures = {}
for phase in ("before", "after"):
    for suite in ("local-loop", "mbct", "expanded"):
        for path in sorted((root / phase / suite / "visual").glob("*.png")):
            key = str(path.relative_to(root / phase))
            captures.setdefault(key, {})[phase] = str(path.relative_to(root))

data = json.dumps(captures, ensure_ascii=False).replace("<", "\\u003c")
page = """<!doctype html>
<html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>마음 정원 UI 비교</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#f7f8ef;color:#243c2f;font:16px system-ui,sans-serif}
header{padding:24px;max-width:1400px;margin:auto}h1{font-size:24px;margin:0 0 12px}
p{line-height:1.6;margin:8px 0}label{display:block;font-weight:600;margin:16px 0 8px}
input,select,button{font:inherit;padding:12px;border:1px solid #bacbb4;border-radius:8px;background:white;color:inherit}
input{width:100%;max-width:360px}select{width:100%}nav{display:flex;gap:8px;margin-top:12px;align-items:center}
button{cursor:pointer}button:disabled{opacity:.45;cursor:default}button:focus-visible,input:focus-visible,select:focus-visible{outline:3px solid #315b48;outline-offset:2px}
main{max-width:1400px;margin:auto;padding:0 24px 32px;display:grid;grid-template-columns:1fr 1fr;gap:24px}
figure{margin:0;min-width:0}figcaption{font-weight:600;padding:12px 0}img{width:100%;height:auto;border:1px solid #d6e1cf;border-radius:8px}
.note{color:#914f2a}.empty{padding:32px;border:1px dashed #bacbb4}a{color:#315b48}
@media(max-width:700px){main{grid-template-columns:1fr}}
</style>
<header><h1>마음 정원 UI · 수정 전후 비교</h1>
<p>Unity Editor 합성 데이터 촬영입니다. 기기 권한 창·네이티브 차트·키보드는 미검증입니다.
전체 활동 48개 / 실습 147단계 및 주요 상태를 촬영했습니다.</p>
<label for="filter">화면 찾기</label><input id="filter" type="search" placeholder="예: growth, settings, mbct-32">
<label for="capture">화면과 해상도</label><select id="capture"></select>
<nav><button id="previous">이전</button><button id="next">다음</button><span id="count" aria-live="polite"></span></nav>
<p class="note" id="warning"></p></header>
<main><figure><figcaption>수정 전</figcaption><div id="before"></div></figure>
<figure><figcaption>수정 후</figcaption><div id="after"></div></figure></main>
<script>
const captures=__DATA__;
const select=document.querySelector('#capture'), filter=document.querySelector('#filter');
function show(){
 const key=select.value, entry=captures[key]||{};
 for(const phase of ['before','after']){
  const box=document.getElementById(phase);box.replaceChildren();
  if(entry[phase]){const link=document.createElement('a');link.href=entry[phase];link.target='_blank';link.rel='noopener';
   const img=document.createElement('img');img.src=entry[phase];img.alt=phase+' '+key;link.append(img);box.append(link);
  }else{const text=document.createElement('p');text.className='empty';text.textContent='해당 단계의 촬영 없음';box.append(text);}
 }
 document.querySelector('#warning').textContent=/settings-(live-editor|replay)/.test(key)?
  '수정 전 이 모드의 캡처는 표시 갱신이 누락되어 무효입니다. 수정 후 캡처만 모드 표시 증거로 사용하세요.':'';
 document.querySelector('#count').textContent=(select.options.length?select.selectedIndex+1:0)+' / '+select.options.length;
 document.querySelector('#previous').disabled=select.selectedIndex<=0;
 document.querySelector('#next').disabled=select.selectedIndex<0||select.selectedIndex>=select.options.length-1;
}
function populate(){const previous=select.value;select.replaceChildren();
 for(const key of Object.keys(captures).filter(k=>k.toLowerCase().includes(filter.value.toLowerCase()))){
  const option=document.createElement('option');option.value=key;option.textContent=key;select.append(option);
 }
 if([...select.options].some(o=>o.value===previous))select.value=previous;show();
}
select.addEventListener('change',show);filter.addEventListener('input',populate);
document.querySelector('#previous').addEventListener('click',()=>{select.selectedIndex--;show()});
document.querySelector('#next').addEventListener('click',()=>{select.selectedIndex++;show()});
populate();
</script></html>"""
root.mkdir(parents=True, exist_ok=True)
(root / "index.html").write_text(page.replace("__DATA__", data), encoding="utf-8")
print(f"{root / 'index.html'}: {len(captures)} comparison entries")
