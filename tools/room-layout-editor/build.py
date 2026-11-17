"""Build an offline editor from current room text files. UTF-8 throughout."""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[2]
SIZES = {'small': (10,5), 'basic': (13,7), 'wide': (21,7), 'tall': (13,11), 'large': (21,11)}
guide = (ROOT / 'docs/31-room-layout-guide.md').read_text(encoding='utf-8')
masks = {p: re.search(r'`'+p+r'` \([^\n]+\)\s+```text\n(.*?)\n```',guide,re.S).group(1).splitlines() for p in SIZES}
draft = ROOT / 'tmp/room-layout-30-drafts.md'
titles = {}
if draft.exists():
    for section in draft.read_text(encoding='utf-8').split('<a id="')[1:]:
        key = section.split('"',1)[0]
        match = re.search(r'## \d+\. (.*?) —',section)
        if match: titles[key] = match.group(1)
rooms=[]
for path in sorted((ROOT / 'game/RoomLayouts').glob('*.room.txt')):
    head, grid = path.read_text(encoding='utf-8-sig').split('grid:',1)
    fields={}
    for line in head.splitlines():
        if not line.strip() or line.lstrip().startswith('//'): continue
        k,v=line.split(':',1); fields[k.strip()]=v.strip()
    rows=grid.strip('\r\n').splitlines()
    rows=[r.rstrip() for r in rows]
    w,h=SIZES[fields['profile']]
    assert len(rows)==h and all(len(r)==w for r in rows),path
    rooms.append(dict(id=fields['id'],profile=fields['profile'],title=titles.get(fields['id'],fields['id']),note=fields.get('note',''),weight=int(fields.get('weight',100)),difficulty=int(fields.get('difficulty',1 if any(ch not in '.123456cgd' for ch in ''.join(rows)) else 0)),floors=fields.get('floors',''),grid=rows,source='현재 프로젝트'))
payload=json.dumps({'rooms':rooms,'masks':masks},ensure_ascii=False).replace('<','\\u003c')
template=(Path(__file__).parent/'editor.html').read_text(encoding='utf-8')
out=ROOT/'tmp/room-layout-editor.html'
out.parent.mkdir(exist_ok=True)
out.write_text(template.replace('__ROOM_DATA__',payload),encoding='utf-8')
print(f'Built {out} ({len(rooms)} rooms)')
