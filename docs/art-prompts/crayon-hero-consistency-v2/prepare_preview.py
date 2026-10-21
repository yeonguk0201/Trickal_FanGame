"""Non-destructive atlas separation and uniform preview registration; no repainting.
Run from this folder with Python and Pillow. Text I/O is explicitly UTF-8.
"""
from pathlib import Path
from collections import deque
from PIL import Image
import json

ROOT = Path(__file__).resolve().parent

def components(mask, w, h):
    remaining = bytearray(mask)
    result = []
    for start in range(w*h):
        if not remaining[start]:
            continue
        remaining[start] = 0
        queue = deque([start])
        points = []
        while queue:
            p = queue.popleft()
            points.append(p)
            x, y = p % w, p // w
            for q in ((p-1 if x else -1), (p+1 if x+1<w else -1),
                      (p-w if y else -1), (p+w if y+1<h else -1)):
                if q >= 0 and remaining[q]:
                    remaining[q] = 0
                    queue.append(q)
        result.append(points)
    return sorted(result, key=len, reverse=True)

def split_atlas(im):
    w,h = im.size
    pixels = list(im.getdata())
    groups = components(bytes(int(p[3]>0) for p in pixels),w,h)
    bodies = groups[:4]
    centers = [(sum(p%w for p in g)/len(g),sum(p//w for p in g)/len(g)) for g in bodies]
    ids = [int(y>h/2)*2+int(x>w/2) for x,y in centers]
    assert sorted(ids)==[0,1,2,3], 'Atlas must have one separate body in each quadrant'
    outputs = [Image.new('RGBA',im.size) for _ in range(4)]
    targets = [o.load() for o in outputs]
    for group in groups:
        cx = sum(p%w for p in group)/len(group)
        cy = sum(p//w for p in group)/len(group)
        nearest = min(range(4),key=lambda i:(cx-centers[i][0])**2+(cy-centers[i][1])**2)
        dest = targets[ids[nearest]]
        for p in group:
            dest[p%w,p//w]=pixels[p]
    assert sum(sum(p[3]>0 for p in o.getdata()) for o in outputs)==sum(p[3]>0 for p in pixels)
    return [o.crop(o.getbbox()) for o in outputs]

def registration(im, golden, manual=None):
    w,h=im.size
    pixels=list(im.getdata())
    if manual:
        hx,hy,hw=manual
    else:
        blue = bytes(int(a>128 and b>100 and b>r*1.45 and b>g*1.10 and y<h*.72)
                     for y in range(h) for r,g,b,a in pixels[y*w:(y+1)*w])
        candidates=components(blue,w,h)
        def is_dome(g):
            bw=max(p%w for p in g)-min(p%w for p in g)+1
            bh=max(p//w for p in g)-min(p//w for p in g)+1
            return .85 < bw/bh < 2.4 and len(g)/(bw*bh)>.38
        dome=next(g for g in candidates if is_dome(g))
        left,right=min(p%w for p in dome),max(p%w for p in dome)
        hx,hy,hw=(left+right)/2,min(p//w for p in dome),right-left+1
    # Brown boots only, below torso, within plausible leg span; ignore sword/trail tips.
    boots=[(i%w,i//w) for i,(r,g,b,a) in enumerate(pixels)
           if a>220 and 35<r<190 and r>g*1.18 and g>b*1.1
           and i//w>h*.62 and abs(i%w-hx)<hw*1.5]
    baseline=max(y for x,y in boots)
    low=[x for x,y in boots if y>baseline-hw*.55]
    foot_x=(min(low)+max(low))/2
    return dict(domeWidth=hw, domeX=hx, domeY=hy, footX=foot_x, footY=baseline)

def main():
    out=ROOT/'frames'
    out.mkdir(exist_ok=True)
    manifest={}
    # Golden dome coordinates in the tight separated crop are specified after visual review.
    manual_path=ROOT/'golden-registration.json'
    manual=json.loads(manual_path.read_text(encoding='utf-8')) if manual_path.exists() else {}
    for name in ['idle','golden-idle','swing','dash','slam','golden-slam']:
        im=Image.open(ROOT/(name+'.png')).convert('RGBA')
        assert im.getextrema()[3][0]==0 and im.getextrema()[3][1]==255
        parts=split_atlas(im) if name not in ['idle','golden-idle'] else [im.crop(im.getbbox())]
        manifest[name]=[]
        for i,part in enumerate(parts):
            filename=f'{name}-{i}.png'
            if not (out/filename).exists():
                part.save(out/filename)
            if 'golden' in name and name not in manual:
                print(name,i,'manual dome registration needed',part.size)
                continue
            reg=registration(part,'golden' in name, manual.get(name,[None]*len(parts))[i])
            if name=='golden-slam' and i==2:
                reg.update(footX=400,footY=440)
            reg.update(src='frames/'+filename,width=part.width,height=part.height)
            manifest[name].append(reg)
    (ROOT/'registration.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'registration.js').write_text('window.PREVIEW_REGISTRATION = '+json.dumps(manifest,ensure_ascii=False)+';\n',encoding='utf-8')
    print(json.dumps(manifest,ensure_ascii=False))

if __name__=='__main__':
    main()
