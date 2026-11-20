"""Build an offline sound mixer page from the current Unity cue table. UTF-8 throughout.

Reads CombatSfxSetup.Cues (cue, file, volume, guard, length cap) and the two BGM paths, then writes
tmp/sound-mixer.html. The page plays the real files in game/Assets/Audio by relative path, so it must stay in tmp/
(or be rebuilt) and needs no server or internet connection.
"""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[2]
SETUP = ROOT / 'game/Assets/Editor/CombatSfxSetup.cs'
AUDIO_SETUP = ROOT / 'game/Assets/Editor/Week13Setting1ASetup.cs'
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
    'SkillUpperStepLeft': ('스킬', '고학년 돌진 왼발'),
    'SkillUpperStepRight': ('스킬', '고학년 돌진 오른발'),
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
    'BossSwordSlam': ('보스', '크레용사 내려찍기'),
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
    'SkillUpperStepLeft': 0.34,
    'SkillUpperStepRight': 0.34,
    'SkillLowerShot': 0.08,
    'VaultSlimeThrow': 0.08,
    'VaultMove': 0.75,
    'BasicAttack': 0.35,
    'EnemyTickDamage': 0.5,
}

GROUP_ORDER = ['플레이어', '스킬', '적', '월드', 'UI', '보스', '기타']


def read_cues():
    text = SETUP.read_text(encoding='utf-8')
    pattern = re.compile(
        r'\(CombatSfxCue\.(\w+),\s*"([^"]+)",\s*([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f\)')
    cues = []
    for name, file, volume, guard, cap in pattern.findall(text):
        group, label = LABELS.get(name, ('기타', name))
        path = ROOT / 'game/Assets/Audio/SFX' / file
        assert path.exists(), f'{name}: missing {path}'
        cues.append(dict(cue=name, file=file, volume=float(volume), guard=float(guard), cap=float(cap),
                         group=group, label=label, cadence=CADENCE.get(name, 0),
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
                          'uiClick': '../game/Assets/Audio/SFX/SFX_UI_Click.wav'},
                         ensure_ascii=False).replace('<', '\\u003c')
    template = (Path(__file__).parent / 'mixer.html').read_text(encoding='utf-8')
    OUT.parent.mkdir(exist_ok=True)
    OUT.write_text(template.replace('__SOUND_DATA__', payload), encoding='utf-8')
    print(f'Built {OUT} ({len(json.loads(payload)["cues"])} cues)')


if __name__ == '__main__':
    main()
