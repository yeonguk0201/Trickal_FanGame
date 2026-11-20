"""Build an offline sound mixer page from the current Unity cue table. UTF-8 throughout.

Reads CombatSfxSetup.Cues (cue, file, volume, guard, length cap) and the two BGM paths, then writes
tmp/sound-mixer.html. The page plays the real files in game/Assets/Audio by relative path, so it must stay in tmp/
(or be rebuilt) and needs no server or internet connection. Candidate sounds not yet imported go in
tmp/sound-candidates/ and are listed in CANDIDATES below.
"""
from pathlib import Path
import json
import re
from urllib.parse import quote

ROOT = Path(__file__).resolve().parents[2]
SETUP = ROOT / 'game/Assets/Editor/CombatSfxSetup.cs'
AUDIO_SETUP = ROOT / 'game/Assets/Editor/Week13Setting1ASetup.cs'
PLAYER_AUDIO = ROOT / 'game/Assets/Scripts/Audio/PlayerCombatAudio.cs'
OUT = ROOT / 'tmp/sound-mixer.html'

# Korean names and groups for the cue list. A cue missing here still shows, under its English name.
LABELS = {
    'BasicAttack': ('플레이어', '기본 공격 발사'),
    'HitEnemy': ('플레이어', '기본 공격 적중'),
    'HitCritical': ('플레이어', '치명타 적중'),
    'PlayerHurt': ('플레이어', '플레이어 피격'),
    'PlayerStep': ('플레이어', '플레이어 발소리'),
    'SkillLowerCast': ('스킬', '저학년 스킬 시전'),
    'SkillLowerShot': ('스킬', '저학년 유도탄 발사'),
    'SkillLowerBurst': ('스킬', '저학년 유도탄 폭발'),
    'SkillUpperDash': ('스킬', '고학년 돌진 출발'),
    'SkillUpperRun': ('스킬', '고학년 돌진 달리기 (돌진 동안 반복)'),
    'PlayerWingFlap': ('플레이어', '시스트 날개 비행 날갯짓'),
    'ShopPurchase': ('월드', '상점 구매'),
    'SkillUpperImpact': ('스킬', '고학년 돌진 충돌'),
    'SpellCast': ('스킬', '스펠 사용'),
    'JjangsemSpellCast': ('스킬', '짱셈 스펠 사용'),
    'PowerCast': ('스킬', '권능 사용'),
    'SpellTeleport': ('스킬', '순간이동 스펠'),
    'MuffinExplosion': ('스킬', '폭발 머핀'),
    'EnemyTickDamage': ('적', '적 지속 피해 틱'),
    'EnemyAttack': ('적', '일반 적 공격'),
    'EnemyDefeated': ('적', '적 처치'),
    'EnemyAppear': ('적', '적 등장'),
    'BombExplosion': ('월드', '폭탄·자폭 폭발'),
    'ObstacleHit': ('월드', '장애물 타격'),
    'DoorOpen': ('월드', '잠긴 문 열림'),
    'RoomTransition': ('월드', '문으로 방 이동'),
    'ChestOpen': ('월드', '상자 열기'),
    'ItemDrop': ('월드', '아이템 떨어짐'),
    'ItemPickup': ('월드', '아이템 줍기 (골드 제외)'),
    'GoldPickup': ('월드', '골드 줍기'),
    'SpGain': ('월드', 'SP 획득'),
    'Heal': ('월드', '회복'),
    'BossPortal': ('월드', '다음 층 포탈 진입'),
    'PortalAppear': ('월드', '포탈 등장'),
    'LevelUp': ('UI', '레벨업 (결과 화면)'),
    'UiCancel': ('UI', '뒤로가기·취소'),
    'UiFocusMove': ('UI', '포커스 이동 (상점·보상)'),
    'BossIntroVs': ('보스', '보스 등장 VS'),
    'BossSwordSwing': ('보스', '크레용사 칼 휘두르기'),
    'BossSwordSlam': ('보스', '크레용사 내려치기 ① 휘두름'),
    'BossSwordSlamMagic': ('보스', '크레용사 내려치기 ② 마법 충격 (+0.27초)'),
    'BossSwordSlamClash': ('보스', '크레용사 내려치기 ③ 칼 부딪힘 (+0.39초)'),
    'BossCrayonCharge': ('보스', '크레용사 검 차징 (내려치기에서 끊김)'),
    'BossCrayonSummon': ('보스', '크레용사 쫄병 소환'),
    'BossCrayonTransform': ('보스', '크레용사 변신 (3초, 마지막 1.5초 페이드)'),
    'BossObstacleLanding': ('보스', '부스러기 장애물 착지'),
    'VaultPillarEmerge': ('보스', '새마음금고 황금 기둥'),
    'VaultJumpWhistle': ('보스', '새마음금고 점프 휘파람'),
    'VaultJumpLanding': ('보스', '새마음금고 점프 착지'),
    'VaultMeteorFall': ('보스', '새마음금고 메테오 낙하'),
    'VaultMeteorImpact': ('보스', '새마음금고 메테오 충돌'),
    'VaultEat': ('보스', '새마음금고 먹기'),
    'VaultSlimeThrow': ('보스', '새마음금고 젤리 던지기'),
    'VaultMove': ('보스', '새마음금고 이동'),
}

# How often the game repeats a cue, so the repeat button plays it at the real cadence. 0 = back to back.
CADENCE = {
    'PlayerStep': 0.25,
    'PlayerWingFlap': 0.5,
    'SkillLowerShot': 0.08,
    'VaultSlimeThrow': 0.08,
    'VaultMove': 0.75,
    'BasicAttack': 0.35,
    'EnemyTickDamage': 0.5,
}



def read_rates():
    """Cues the game plays sped up (AudioSource.pitch), so the page plays them at the same speed."""
    text = PLAYER_AUDIO.read_text(encoding='utf-8')
    m = re.search(r'DashRunPitch\s*=\s*([\d.]+)f', text)
    assert m, 'PlayerCombatAudio.DashRunPitch not found.'
    return {'SkillUpperRun': float(m.group(1))}


GROUP_ORDER = ['플레이어', '스킬', '적', '월드', 'UI', '보스', '기타']

# Sounds being auditioned before they are imported. Files live in tmp/sound-candidates/ (next to the built page);
# a missing file still shows as a placeholder row so the to-do stays visible. `target` is the cue the candidate
# would replace (played side by side for comparison); None means a new cue. `volume` is only a starting point.
CANDIDATE_DIR = ROOT / 'tmp/sound-candidates'
_PACK150 = 'OpenGameArt 150 Combat Sound Effects (untamedpixels, CC0)'
# The 2026-11-21 sound pass imported every candidate auditioned here; add new ones the same way:
# dict(id=, group=, label=, target=<cue or None>, file=, volume=, cadence=, length=, fade=, source=).
CANDIDATES = []
# Moments made of several cues, played at the game's timing so they can be balanced together.
# parts: (candidate id, start time, play length or 0 for its own length) — candidates only.
# compare: (cue, start time[, play length]) — cues already in the game.
# editable: the page shows a start-time slider per part (0..maxAt seconds) and exports the changed timing.
CANDIDATE_SCENES = [
    dict(label='고학년 돌진: 출발 → 달리기 1.6초 → 충돌', parts=[],
         compare=[('SkillUpperDash', 0.0), ('SkillUpperRun', 0.0, 1.7), ('SkillUpperImpact', 1.6)]),
    dict(label='저학년 전체: 시전 → 발사 ×4 → 폭발 ×4', parts=[],
         compare=[('SkillLowerCast', 0.0)] + [('SkillLowerShot', i * 0.08) for i in range(4)]
                 + [('SkillLowerBurst', 0.45 + i * 0.08) for i in range(4)]),
    dict(label='크레용사 차징 → 내려치기 3겹', parts=[],
         compare=[('BossCrayonCharge', 0.0, 1.2), ('BossSwordSlam', 1.2), ('BossSwordSlamMagic', 1.47),
                  ('BossSwordSlamClash', 1.59)]),
]
CANDIDATE_COMPARE = {}


def read_candidates():
    out = []
    for c in CANDIDATES:
        c = dict(c)
        has = bool(c['file']) and (CANDIDATE_DIR / c['file']).exists()
        if c['file'] and not has:
            print(f'warning: candidate file missing: {CANDIDATE_DIR / c["file"]}')
        c['src'] = f'sound-candidates/{quote(c["file"])}' if has else None
        c.setdefault('length', 0)
        c.setdefault('fade', 0)
        c.setdefault('source', '')
        c['compare'] = CANDIDATE_COMPARE.get(c['id']) or ([(c['target'], 0.0)] if c['target'] else [])
        out.append(c)
    return out


def read_cues():
    text = SETUP.read_text(encoding='utf-8')
    pattern = re.compile(
        r'\(CombatSfxCue\.(\w+),\s*"([^"]+)",\s*([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f\)')
    cues = []
    rates = read_rates()
    for name, file, volume, guard, cap in pattern.findall(text):
        group, label = LABELS.get(name, ('기타', name))
        path = ROOT / 'game/Assets/Audio/SFX' / file
        assert path.exists(), f'{name}: missing {path}'
        cues.append(dict(cue=name, file=file, volume=float(volume), guard=float(guard), cap=float(cap),
                         group=group, label=label, cadence=CADENCE.get(name, 0), rate=rates.get(name, 1.0),
                         src=f'../game/Assets/Audio/SFX/{file}'))
    assert cues, 'No cues were read from CombatSfxSetup.Cues.'
    cues.sort(key=lambda c: GROUP_ORDER.index(c['group']) if c['group'] in GROUP_ORDER else len(GROUP_ORDER))
    return cues


def read_bgm():
    text = AUDIO_SETUP.read_text(encoding='utf-8')
    tracks = []
    for key, label in (('HomeBgmPath', '홈 화면 BGM'), ('CombatBgmPath', '전투 BGM')):
        path = re.search(key + r'\s*=\s*"Assets/([^"]+)"', text).group(1)
        assert (ROOT / 'game/Assets' / path).exists(), path
        tracks.append(dict(key=key, label=label, src=f'../game/Assets/{path}', file=path.split('/')[-1]))
    click = ROOT / 'game/Assets/Audio/SFX/SFX_UI_Click.wav'
    assert click.exists()
    return tracks


def main():
    payload = json.dumps({'cues': read_cues(), 'bgm': read_bgm(),
                          'candidates': read_candidates(), 'candidateScenes': CANDIDATE_SCENES,
                          'uiClick': '../game/Assets/Audio/SFX/SFX_UI_Click.wav'},
                         ensure_ascii=False).replace('<', '\\u003c')
    template = (Path(__file__).parent / 'mixer.html').read_text(encoding='utf-8')
    OUT.parent.mkdir(exist_ok=True)
    OUT.write_text(template.replace('__SOUND_DATA__', payload), encoding='utf-8')
    print(f'Built {OUT} ({len(json.loads(payload)["cues"])} cues)')


if __name__ == '__main__':
    main()
