"""Build a local review gallery from actual Unity capture layers. UTF-8 throughout."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/ground-shadow-review'
entries = json.loads((OUT / 'manifest.json').read_text(encoding='utf-8'))['entries']
assert entries and len({e['id'] for e in entries}) == len(entries)
names = {
    'TestBoss': '부스러기', 'CrayonArcherMinion': '크레용 궁수', 'CrayonAxeMinion': '크레용 도끼병',
    'CrayonMageMinion': '크레용 마법사', 'CrayonShieldMinion': '크레용 방패병',
    'crumb-cream': '부스러기 · 크림', 'crumb-chocolate': '부스러기 · 초콜릿',
    'ElifPickup': '엘리프', 'KeyPickup': '열쇠', 'BombPickup': '폭탄 픽업',
    'HealthPickup': 'HP 캡슐', 'SPPickup': 'SP 캡슐', 'PlacedBomb': '설치한 폭탄',
    'obstacle-marie-bomb-box': '마리의 폭탄 상자', 'obstacle-erpin-snack-box': '에르핀의 간식 상자',
    'obstacle-eshur-bread-box': '에슈르의 빵 상자', 'obstacle-ricotta-food-box': '리코타의 식량 상자',
    'obstacle-mayo-key-bundle': '마요의 열쇠 묶음', 'obstacle-mayo-collection-box': '마요의 수집품 상자',
    'obstacle-sist-vault': '시스트의 금고', 'obstacle-gold-rock': '황금 돌',
    'obstacle-explosive-box': '폭발 상자', 'obstacle-shady-random-box': '셰이디의 랜덤 상자',
}
for e in entries:
    e['name'] = names.get(e['id'], e['name']).replace('Normal 상자', '일반 상자').replace('Golden 상자', '황금 상자').replace('Diamond 상자', '다이아 상자')

HTML = r'''<!doctype html><html lang="ko"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>그림자 검수 · 트릭컬 팬게임</title>
<style>
:root{font-family:"Malgun Gothic","맑은 고딕",sans-serif;color:#273b36;background:#edf3ed;accent-color:#547c6e}*{box-sizing:border-box}body{margin:0}header{padding:25px 32px 19px;border-bottom:1px solid #b6cabd;background:#e2eee4}h1{font-size:26px;margin:0 0 9px;letter-spacing:-1px}p{font-size:14px;line-height:1.65;margin:5px 0;color:#496358}.toolbar{display:flex;gap:8px;flex-wrap:wrap;margin-top:17px}input[type=search],select,button{font:inherit;border:1px solid #a3bdb0;border-radius:6px;padding:9px 12px;background:#fff;color:inherit}button{cursor:pointer}button:hover{background:#e5eee8}button:focus-visible,input:focus-visible,select:focus-visible{outline:3px solid #4b847b;outline-offset:2px}input[type=search]{min-width:180px;flex:1}main{display:grid;grid-template-columns:minmax(320px,1fr) 430px;gap:25px;padding:25px 32px;align-items:start}.gallery{display:grid;grid-template-columns:repeat(auto-fill,minmax(210px,1fr));gap:17px}.card{padding:0;text-align:left;overflow:hidden;background:#fff;border-color:#c6d5ca}.card.selected{outline:3px solid #547c6e}.card img{width:100%;aspect-ratio:1;display:block}.label{padding:10px 12px;display:block}.label strong{display:block;font-size:14px;margin-bottom:5px}.label small{color:#698071;font-size:11px}.editor{position:sticky;top:18px;background:#fff;padding:18px;border:1px solid #bdcfc2;border-radius:8px}.editor h2{font-size:20px;margin:0 0 9px}canvas{width:100%;height:auto;display:block;background:#90b757;border-radius:4px}.caption{display:flex;justify-content:space-between;gap:10px;font-size:12px;color:#597160;padding:8px 0 12px}.control{display:grid;grid-template-columns:98px 1fr 65px;gap:8px;align-items:center;margin:12px 0}.control label{font-size:13px}.control input[type=range]{width:100%;margin:0}.control input[type=number]{width:65px;padding:5px;border:1px solid #b7cbbd;border-radius:4px;font:inherit;font-size:13px}.buttons{display:flex;flex-wrap:wrap;gap:8px;margin:15px 0}.buttons button{font-size:13px;padding:7px 10px}.check{font-size:13px}.note{font-size:12px;color:#607564}textarea{width:100%;height:115px;resize:vertical;border:1px solid #bdcfc2;border-radius:4px;padding:9px;font:12px/1.6 "Malgun Gothic",sans-serif}.summary{margin-top:20px;border-top:1px solid #d4dfd7;padding-top:14px}.summary h3{font-size:14px;margin:0 0 8px}#status{font-size:12px;min-height:17px;color:#3d7866}.count{margin:0 0 13px;font-size:13px}a{color:#356c59}@media(max-width:1000px){main{grid-template-columns:1fr;padding:18px}.editor{position:static;grid-row:1;max-width:600px;width:100%;margin:auto}header{padding:20px}.gallery{grid-template-columns:repeat(auto-fill,minmax(165px,1fr))}}@media(prefers-reduced-motion:reduce){*{scroll-behavior:auto}}
</style>
<header><h1>바닥 그림자 검수</h1><p>실제 게임 에셋 <b id="total"></b>종 · Unity 원본 렌더와 동일한 바닥/원화/그림자 레이어</p>
<p>수정한 그림자 수치와 스펠 공통 설정이 현재 기본값에 적용되어 있습니다. 추가 조절값은 이 페이지에만 보관되며 게임에는 자동 저장되지 않습니다.</p>
<div class="toolbar"><input id="search" type="search" placeholder="이름이나 번호로 찾기" aria-label="에셋 찾기"><select id="group" aria-label="종류"><option value="">모든 종류</option></select><button id="download">조절값 JSON 저장</button></div></header>
<main><section><p class="count" id="count"></p><div id="gallery" class="gallery"></div></section>
<aside class="editor"><h2 id="title"></h2><canvas id="view" width="480" height="480" aria-label="선택한 에셋 그림자 미리보기"></canvas><div class="caption"><span id="mode">Unity 원본</span><span id="scale"></span></div>
<label class="check"><input id="visible" type="checkbox" checked> 그림자 보기</label><div id="controls"></div>
<p class="note">위치의 +X는 오른쪽, +Y는 위쪽입니다. 1칸은 Unity 1단위입니다. 너비를 키우면 그림자 전체가 커지고, 두께로 세로 비율을 조절합니다.</p>
<div class="buttons"><button id="reset">이 에셋 초기화</button><button id="original">원본 누르고 비교</button></div>
<p class="note" id="measure"></p><div class="summary"><h3>말씀해 주실 조절값</h3><textarea id="summary" readonly aria-label="변경값 요약"></textarea><div class="buttons"><button id="copy">조절값 복사</button></div><div id="status" role="status"></div></div></aside></main>
<script>
const data=__DATA__, key='trickal-ground-shadow-review-reviewed-20261011-spells';let changes={};try{changes=JSON.parse(localStorage.getItem(key)||'{}')}catch{}
let selected=data.find(x=>x.id==='tree')||data[0], comparison=false, loadVersion=0, layers=null;
const $=s=>document.getElementById(s), defs=[['widthMultiplier','너비 배율',.1,3,.01],['thickness','두께 비율',.05,1,.01],['offsetX','위치 X (칸)',-1,1,.01],['offsetY','위치 Y (칸)',-1,1,.01],['opacity','진하기',0,1,.01]];
const defaults=e=>Object.fromEntries(defs.map(([k])=>[k,e[k]])), settings=e=>({...defaults(e),...(changes[e.id]||{})});
$('total').textContent=data.length;for(const g of [...new Set(data.map(e=>e.group))]){let o=document.createElement('option');o.value=o.textContent=g;$('group').append(o)}
function gallery(){const q=$('search').value.toLowerCase(),g=$('group').value,shown=data.filter(e=>(!g||e.group===g)&&(`${e.number} ${e.name} ${e.id}`.toLowerCase().includes(q)));$('count').textContent=`${shown.length} / ${data.length}종 · 에셋 번호로 수정사항을 말씀해 주세요`;$('gallery').replaceChildren();for(const e of shown){const b=document.createElement('button');b.className='card'+(e.id===selected.id?' selected':'');b.dataset.id=e.id;let img=new Image();img.src=String(e.number).padStart(3,'0')+'-game.png';img.alt=e.name+' 현재 게임 그림자';img.loading='lazy';const label=document.createElement('span');label.className='label';const strong=document.createElement('strong');strong.textContent=`${String(e.number).padStart(2,'0')} · ${e.name}${changes[e.id]?' · 조절 중':''}`;const small=document.createElement('small');small.textContent=`${e.group} · 화면 가로 ${e.span.toFixed(1)}칸`;label.append(strong,small);b.append(img,label);b.onclick=()=>select(e);$('gallery').append(b)}}
function img(url){return new Promise((resolve,reject)=>{const i=new Image();i.onload=()=>resolve(i);i.onerror=()=>reject(new Error(url+' 로딩 실패'));i.src=url})}
async function select(e){selected=e;$('title').textContent=`${String(e.number).padStart(2,'0')} · ${e.name}`;$('scale').textContent=`화면 가로 ${e.span.toFixed(1)}칸`;$('visible').checked=true;const v=settings(e);$('controls').replaceChildren();for(const [k,label,min,max,step] of defs){let row=document.createElement('div');row.className='control';const l=document.createElement('label');l.textContent=label;l.htmlFor=k;let r=document.createElement('input');r.id=k;r.type='range';Object.assign(r,{min,max,step,value:Number(v[k].toFixed(2))});let n=document.createElement('input');n.type='number';n.setAttribute('aria-label',label);Object.assign(n,{min,max,step,value:Number(v[k].toFixed(2))});function edit(value){const x=Math.max(min,Math.min(max,Number(value)));r.value=n.value=x;const next=settings(e);next[k]=x;if(defs.every(([p])=>Math.abs(next[p]-e[p])<.0001))delete changes[e.id];else changes[e.id]=next;try{localStorage.setItem(key,JSON.stringify(changes))}catch{}summary();draw();}r.oninput=()=>edit(r.value);n.oninput=()=>{if(n.value!=='')edit(n.value)};row.append(l,r,n);$('controls').append(row)}gallery();summary();const version=++loadVersion;layers=null;try{const prefix=String(e.number).padStart(3,'0');const [game,body,shadow,floor]=await Promise.all(['game','body','shadow','floor'].map(s=>img(prefix+'-'+s+'.png')));if(version!==loadVersion)return;const mask=document.createElement('canvas');mask.width=mask.height=480;const mc=mask.getContext('2d');mc.drawImage(shadow,0,0);const pixels=mc.getImageData(0,0,480,480);for(let j=3;j<pixels.data.length;j+=4)pixels.data[j]=Math.min(255,pixels.data[j]/e.opacity);mc.putImageData(pixels,0,0);layers={game,body,shadow:mask,floor};draw()}catch(err){$('status').textContent=err.message}}
function draw(){if(!layers)return;const e=selected,s=settings(e),c=$('view').getContext('2d');c.clearRect(0,0,480,480);if(comparison||(!changes[e.id]&&$('visible').checked)){c.drawImage(layers.game,0,0);$('mode').textContent='Unity 원본 렌더'}else{c.drawImage(layers.floor,0,0);if($('visible').checked){const ax=e.anchorX*480,ay=e.anchorY*480,scale=s.widthMultiplier/e.widthMultiplier;c.save();c.translate(ax+(s.offsetX-e.offsetX)*480/e.span,ay-(s.offsetY-e.offsetY)*480/e.span);c.scale(scale,scale*s.thickness/e.thickness);c.globalAlpha=s.opacity;c.drawImage(layers.shadow,-ax,-ay);c.restore()}c.drawImage(layers.body,0,0);$('mode').textContent=$('visible').checked?'조절 미리보기 · 게임 적용 전':'그림자 없음'}$('measure').textContent=`그림자 너비 ${(e.width*s.widthMultiplier/e.widthMultiplier).toFixed(2)}칸 · 높이 ${(e.height*s.widthMultiplier/e.widthMultiplier*s.thickness/e.thickness).toFixed(2)}칸 · 기준 진하기 ${e.opacity.toFixed(2)}`}
function summary(){const lines=data.filter(e=>changes[e.id]).map(e=>{const s=settings(e);return `${String(e.number).padStart(2,'0')} ${e.name}: 너비 ${s.widthMultiplier.toFixed(2)}, 두께 ${s.thickness.toFixed(2)}, 위치 (${s.offsetX.toFixed(2)}, ${s.offsetY.toFixed(2)}), 진하기 ${s.opacity.toFixed(2)}`});$('summary').value=lines.join('\n')||'아직 변경한 에셋이 없습니다.\n예: 18번 나무를 조금 넓게, 위치는 아래로, 진하기는 0.4로.'}
$('search').oninput=gallery;$('group').onchange=gallery;$('visible').onchange=draw;$('reset').onclick=()=>{delete changes[selected.id];localStorage.setItem(key,JSON.stringify(changes));select(selected)};$('original').onpointerdown=()=>{comparison=true;draw()};window.addEventListener('pointerup',()=>{comparison=false;draw()});$('original').onpointerleave=()=>{comparison=false;draw()};$('original').onkeydown=e=>{if(e.key===' '||e.key==='Enter'){comparison=true;draw()}};$('original').onkeyup=()=>{comparison=false;draw()};$('copy').onclick=async()=>{try{await navigator.clipboard.writeText($('summary').value);$('status').textContent='복사했습니다. 채팅에 붙여 넣어 주세요.'}catch{$('summary').select();$('status').textContent='요약을 선택했습니다. Ctrl+C로 복사하세요.'}};$('download').onclick=()=>{const result=data.filter(e=>changes[e.id]).map(e=>({number:e.number,id:e.id,name:e.name,...settings(e)}));const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(result,null,2)],{type:'application/json'}));a.download='ground-shadow-adjustments.json';a.click();setTimeout(()=>URL.revokeObjectURL(a.href),1000)};select(selected);
</script></html>'''
HTML = HTML.replace('조절 미리보기', '조절 미리보기').replace('__DATA__', json.dumps(entries, ensure_ascii=False).replace('</', '<\\/'))
HTML = HTML.replace('.editor{position:sticky;', '.editor{max-height:calc(100vh - 36px);overflow-y:auto;position:sticky;')
HTML = HTML.replace('.editor{position:static;', '.editor{max-height:none;overflow-y:visible;position:static;')
(OUT / 'index.html').write_text(HTML, encoding='utf-8')

font_path = 'C:/Windows/Fonts/malgun.ttf'
font = ImageFont.truetype(font_path, 19)
small = ImageFont.truetype(font_path, 14)
title = ImageFont.truetype(font_path, 27)

def sheet(values, filename, heading):
    columns = 4
    tile_w, tile_h = 320, 360
    height = 90 + ((len(values)+columns-1)//columns)*tile_h
    canvas = Image.new('RGB', (columns*tile_w, height), '#edf3ed')
    draw = ImageDraw.Draw(canvas)
    draw.text((20, 17), heading, font=title, fill='#273b36')
    draw.text((20, 57), 'Unity 실제 에셋 + 현재 바닥 + 현재 공용 그림자 · 번호별로 수정사항을 말씀해 주세요', font=small, fill='#496358')
    for i,e in enumerate(values):
        x, y = i%columns*tile_w, 90+i//columns*tile_h
        image = Image.open(OUT / f"{e['number']:03}-game.png").convert('RGB').resize((300,300),Image.Resampling.LANCZOS)
        canvas.paste(image,(x+10,y))
        label=f"{e['number']:02} · {e['name']}"
        while draw.textlength(label,font=font)>300:label=label[:-2]+'…'
        draw.text((x+12,y+306),label,font=font,fill='#273b36')
        draw.text((x+12,y+334),f"너비 {e['width']:.2f}칸 / 진하기 {e['opacity']:.2f}",font=small,fill='#496358')
    canvas.save(OUT / filename)

overview_ids=['player-erpin','ChargingEnemy','JyubiEnemy','tree','obstacle-rock','obstacle-explosive-box','chest-Normal-False','KeyPickup','HealthPickup','PlacedBomb','npc-sist','npc-goldi']
overview=[next(e for e in entries if e['id']==key) for key in overview_ids]
sheet(overview,'overview.png','공용 그림자 · 대표 에셋 12종')
for i in range(0,len(entries),12):sheet(entries[i:i+12],f'sheet-{i//12+1:02}.png',f'전체 그림자 검수 {i+1}–{min(i+12,len(entries))} / {len(entries)}')
print(f'Built {len(entries)}-asset interactive gallery, overview and {(len(entries)+11)//12} contact sheets: {OUT}')
