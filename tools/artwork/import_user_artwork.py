"""Copy user-supplied originals unchanged and create stable Unity import metadata."""
from pathlib import Path
import shutil
import uuid
import re
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'game/Assets/Resources/UserArtwork'
OUT.mkdir(parents=True, exist_ok=True)
names = ['실라의 바람살','랜덤 코인','그건 내 잔상','회심의 일격','저놈잡아라','이성의 끈','깜짝 상자','수상한 물약','거대화 물약','슈슈슈슉 글러브','도깨비 감투','풍선갑옷','폭발 머핀','앗땃따건','장난감 망원경','생명의 보석','레비의 단도','블랑셰의 유리 파랑새','네르의 엘드르 깃발','다야의 다이아몬드 커터','시스트','HP','SP','탐욕의 반지','긴급 보호 벨트','에르핀의 지팡이','암살자의 비급','리스티의 게임 컨트롤러','코미의 베개','불타는 가지','광기의 가면','돈까스 모양 머리핀','활활 불타활','날씨는 맑음 카드','아멜리아의 E-Pad 클래식','림의 낫','멤버십 카드','아로마 테라피','파스텔빛 소풍','명상의 시간']
def normalized(s):
    return s.replace(' ', '')
items = {}
for asset in (ROOT / 'game/Assets/Items').glob('*.asset'):
    data = asset.read_text(encoding='utf-8')
    match = re.search(r'  displayName: (.+)', data)
    if match:
        display = json.loads(match[1]) if match[1].startswith('"') else match[1]
        # Keep active single-use IDs ahead of the retired spell IDs.
        if not asset.stem.startswith('spell-'):
            items[normalized(display)] = asset.stem
manifest = []
for name in names:
    key = items.get(normalized(name), {'시스트':'sist','HP':'hp-pickup','SP':'sp-pickup'}.get(name, 'reserved-' + str(names.index(name))))
    src = Path('C:/Users/yeonguk/Downloads') / (name + '.png')
    target = OUT / (key + '.png')
    shutil.copyfile(src, target)
    manifest.append({'source': name + '.png', 'resource': key, 'connected': not key.startswith('reserved-')})
for source, key in [('트릭컬팬게임홈배경','home-background'), ('트릭컬팬게임아이콘(수정)','hud-icons')]:
    shutil.copyfile(Path('C:/Users/yeonguk/Desktop/트릭컬팬게임 목업 UI') / (source + '.png'), OUT / (key + '.png'))
for target in OUT.glob('*.png'):
    meta = target.with_suffix('.png.meta')
    if not meta.exists():
        ppu = 800 if target.stem == 'sist' else 2000
        meta.write_text(f'''fileFormatVersion: 2
guid: {uuid.uuid4().hex}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    filterMode: 1
    aniso: 1
    wrapU: 1
    wrapV: 1
    wrapW: 1
  maxTextureSize: 2048
  nPOTScale: 0
  spriteMode: 1
  spriteMeshType: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: {ppu}
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {uuid.uuid4().hex}
    internalID: 0
    nameFileIdTable: {{}}
''', encoding='utf-8')
(OUT / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(manifest, ensure_ascii=False, indent=2))
