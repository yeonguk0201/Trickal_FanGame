"""Compose delivered PNGs into review GIF/PNG, without repainting assets."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, base64, re

ROOT=Path(__file__).resolve().parent
html_path=ROOT/'preview.html'
html_source=html_path.read_text(encoding='utf-8')
html_source=html_source.replace("ctx.drawImage(imgs.floor,0,0,1120,510);", "ctx.fillStyle=ctx.createPattern(imgs.floor,'repeat');ctx.fillRect(0,0,1120,510);")
html_source=html_source.replace("'floor.png'", "'floor-tile.png'").replace('850,425','830,425')
html_path.write_text(html_source,encoding='utf-8')
regs=json.loads((ROOT/'registration.json').read_text(encoding='utf-8'))
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18)
small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',13)
tile=Image.open(ROOT/'floor.png').convert('RGBA').crop((30,285,175,420))
tile.save(ROOT/'floor-tile.png')
tile=tile.convert('RGB').resize((145,135))
floor=Image.new('RGB',(560,430))
for y in range(0,430,135):
    for x in range(0,560,145):
        floor.paste(tile,(x,y))
floor=Image.blend(floor,Image.new('RGB',floor.size,'#243b27'),.44)
images={r['src']:Image.open(ROOT/r['src']).convert('RGBA') for group in regs.values() for r in group}
titles=['기본 · 일반 공격','기본 · 대쉬','기본 · 내려찍기','황금 · 내려찍기']
ids=['swing','dash','slam','golden-slam']
labels=['대기','준비','발동 준비','타격 / 진행','회수','대기']

def sprite(canvas,r,x,y,dome=54):
    s=dome/r['domeWidth']
    im=images[r['src']].resize((round(r['width']*s),round(r['height']*s)),Image.Resampling.LANCZOS)
    canvas.alpha_composite(im,(round(x-r['footX']*s),round(y-r['footY']*s)))

def render(stage):
    canvas=Image.new('RGBA',(1120,900),'#101816')
    for index,ident in enumerate(ids):
        ox=(index%2)*560;oy=(index//2)*430
        tile=floor.convert('RGBA').copy()
        draw=ImageDraw.Draw(tile)
        draw.rectangle((0,0,560,48),fill='#18281f')
        draw.text((18,12),titles[index],font=font,fill='#f3d28b')
        draw.line((18,378,542,378),fill='#bac59a',width=1)
        draw.text((65,397),'대기 원본',font=small,fill='#e1e6d6')
        draw.text((340,397),'연결 · '+labels[stage],font=small,fill='#f3d28b')
        idle=regs['golden-idle' if index==3 else 'idle'][0]
        sprite(tile,idle,146,378)
        sprite(tile,idle if stage in [0,5] else regs[ident][stage-1],400,378)
        canvas.alpha_composite(tile,(ox,oy))
    draw=ImageDraw.Draw(canvas)
    draw.text((20,872),'적용 전 미술 시안 · 대기 → 동작 → 대기 · 검수용 배율 / 타이밍 · Unity Play 화면 아님',font=small,fill='#d6d5bd')
    return canvas.convert('RGB')

frames=[render(i) for i in range(6)]
frames[3].save(ROOT/'comparison.png')
frames[0].save(ROOT/'connected-preview.gif',save_all=True,append_images=frames[1:],duration=[1000,650,650,300,400,700],loop=0,optimize=False)
# Shareable local HTML: embed all PNGs and registration so no server is needed.
html=(ROOT/'preview.html').read_text(encoding='utf-8')
manifest_js=(ROOT/'registration.js').read_text(encoding='utf-8')
html=html.replace('<script src="registration.js"></script>','<script>'+manifest_js+'</script>')
data={}
for path in [*images.keys(),'floor-tile.png',*[i+'.png' for i in ids]]:
    data[path]='data:image/png;base64,'+base64.b64encode((ROOT/path).read_bytes()).decode('ascii')
html=html.replace('const configs=','const EMBEDDED='+json.dumps(data)+';\nconst configs=')
html=html.replace("im.src=src;","im.src=EMBEDDED[src]||src;")
html=html.replace("$('sheet').src=c.id+'.png'","$('sheet').src=EMBEDDED[c.id+'.png']")
html=html.replace("$('sheet').src='swing.png'","$('sheet').src=EMBEDDED['swing.png']")
(ROOT/'preview-standalone.html').write_text(html,encoding='utf-8')
print('Saved comparison.png, connected-preview.gif, preview-standalone.html')
