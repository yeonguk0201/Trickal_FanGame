"""
Builds a single-file audition page from a rendered output directory.

    python audition_page.py <sfx_dir> <out_html>

Every WAV is embedded as a data URI, so the page plays without a server and
can be handed around as one file. Each row also carries a peak envelope so
the shapes can be compared by eye and a playhead can run along the sound.
"""
import base64, io, json, os, sys, wave

import numpy as np

from render_sfx import BURST_GAP

ENVELOPE_BARS = 96

# Group order and copy. Keyed by the output sub-directory.
GROUPS = [
    ("erpin-attack", "에르핀 기본 공격", "마우스 좌클릭",
     "투사체를 쏠 때마다 울린다. 가장 자주 들리는 소리라 짧고 둥글어야 하고, 연타로 들어도 피로하지 않아야 한다."),
    ("attack-burst", "기본 공격 폭발", "적 명중",
     "에르핀의 기본 공격은 작은 폭발로 터진다. 매 명중마다 울리므로 폭발로 들리면서도 0.35초 간격에서 "
     "뭉개지지 않을 만큼 짧아야 하고, 저학년 스킬 폭발보다 작아야 둘이 헷갈리지 않는다."),
    ("hit", "틱 데미지", "독·화상·오라·스펠",
     "지속 피해가 적에게 들어갈 때. 한 프레임에 여러 마리가 동시에 틱하므로 최소 간격에 가장 많이 의존한다. "
     "E·퍅을 여기에 쓴다."),
    ("hurt", "피격음", "플레이어 피해",
     "플레이어가 맞을 때. 타격음과 확실히 구분되어야 해서 더 낮고, 음이 내려가고, 조금 더 길다."),
    ("skill-lower", "저학년 스킬", "Space",
     "유도탄 4발을 0.08초 간격으로 쏘고 각 탄이 범위 폭발로 끝난다. 시전·발사·폭발 세 역할을 따로 뽑았다."),
    ("skill-upper", "고학년 스킬", "Q",
     "무적 돌진이 적과 충돌하며 광역 피해로 끝난다. 돌진과 충돌은 서로 다른 시점의 사건이라 따로 뽑았다."),
    ("step", "돌진 발소리", "Q · 돌진 중 반복",
     "돌진은 적에 닿을 때까지 최대 10초다. 고정 길이 음원 하나로는 못 덮으므로 한 걸음을 0.17초 간격으로 "
     "반복한다. 왼발·오른발을 따로 만들어 연속 두 걸음이 같은 음원이 되지 않게 했다."),
    ("scenes", "합성 장면", "실제 배치",
     "후보들을 게임이 실제로 터뜨리는 순서와 간격으로 겹쳐 둔 것이다. 단독으로 들을 때와 인상이 가장 많이 달라지는 지점이다."),
]

LABELS = {
    "SFX_Erpin_Attack_A_Ppyong": ("A · 뽕", "사인파가 빠르게 올라가는 기본형. 가장 둥글고 짧다."),
    "SFX_Erpin_Attack_B_TongTong": ("B · 통통", "본체 뒤에 한 음 높은 작은 튕김을 붙였다."),
    "SFX_Erpin_Attack_C_Ttorong": ("C · 또롱", "두 음 아르페지오. 장난감 같은 음정이 남는다."),
    "SFX_Erpin_Attack_D_Bubble": ("D · 뽀글", "고역을 깎은 사각파. 물방울이 터지는 질감."),
    "SFX_Erpin_Attack_E_Jelly": ("E · 보잉", "올라갔다 내려오며 미세하게 떨린다. 탄성이 느껴진다."),
    "SFX_Erpin_Attack_F_RetroPop": ("F · 퓽", "비트 크러시를 살짝 걸어 레트로 게임 톤으로."),

    "SFX_Hit_A_Pock": ("A · 폭", "마른 클릭 위에 짧은 중역 몸통. 나무를 치는 느낌."),
    "SFX_Hit_B_Thwack": ("B · 퍽", "노이즈가 앞에 서고 저역이 받친다. 가장 묵직한 선택."),
    "SFX_Hit_C_Pop": ("C · 팡", "가장 부드럽고, 기본 공격음과 가장 자연스럽게 붙는다."),
    "SFX_Hit_D_Crunch": ("D · 크직", "비트 크러시를 건 타격. 레트로 쪽으로 기울인 선택."),
    "SFX_Hit_E_Punch": ("E · 퍅 (타격감)", "단단한 어택 + 깊은 저역 + 중역 슬랩. 250Hz 아래 에너지가 A의 5%→23%."),
    "SFX_Hit_F_Smash": ("F · 쾅 (가장 무겁게)", "저역 꼬리를 길게 끌고 균열을 거칠게 눌렀다. 셋 중 가장 둔탁하다."),
    "SFX_Hit_G_Impact": ("G · 탁 (단단하게)", "가장 짧고 조여진 타격. 밝은 어택에 단단한 저역만 붙였다."),

    "SFX_Hurt_A_Thud": ("A · 툭", "낮은 몸통과 둔한 노이즈. 담백해서 반복에 강하다."),
    "SFX_Hurt_B_Wobble": ("B · 뭉", "사각파가 흔들린다. 만화적이고 게임의 가벼운 톤을 지킨다."),
    "SFX_Hurt_C_Sting": ("C · 띠롱", "두 음이 내려간다. 실수했다는 신호가 가장 분명하다."),

    "SFX_SkillLower_Cast_A_Chime": ("시전 A · 종", "세 음이 올라간다. 마법이 나갔다는 신호."),
    "SFX_SkillLower_Cast_B_Whoosh": ("시전 B · 바람", "올라가는 공기음 아래에 상승음을 깔았다."),
    "SFX_SkillLower_Cast_C_Magic": ("시전 C · 마법", "공기가 모이는 스웰 → 올라가는 종 → 살짝 어긋나게 떨리는 반짝임. 마법다운 쪽."),
    "SFX_SkillLower_Cast_D_Sparkle": ("시전 D · 반짝임", "네 음이 올라가고 위에서 더 잘게 떤다. 가볍고 분주하다."),
    "SFX_SkillLower_Cast_E_Chant": ("시전 E · 영창", "더 낮고 묵직하다. 픽업을 줍는 소리가 아니라 주문을 외는 쪽."),
    "SFX_SkillLower_Shot_A_Pew": ("발사 A · 퓨", "4번 연속으로 울리기 때문에 일부러 얇게 잡았다."),
    "SFX_SkillLower_Shot_B_Tick": ("발사 B · 틱", "숨소리가 섞인 발사음. 시전음 아래에 더 잘 깔린다."),
    "SFX_SkillLower_Burst_A_Soft": ("폭발 A · 부드럽게", "4발이 겹쳐도 뭉개지지 않도록 가볍게 잡은 폭발."),
    "SFX_SkillLower_Burst_B_Sparkle": ("폭발 B · 반짝", "같은 몸통에 짧은 반짝임 꼬리를 더했다. 폭탄보다 마법 쪽."),

    "SFX_SkillUpper_Dash_A_Whoosh": ("돌진 A · 바람", "음정이 없는 넓은 공기음. 이동 자체를 전달한다."),
    "SFX_SkillUpper_Dash_B_Charge": ("돌진 B · 차징", "공기음에 올라가는 음을 더했다. 이동보다 스킬처럼 들린다."),
    "SFX_SkillUpper_Dash_C_Run": ("돌진 C · 달리기", "발소리 5걸음을 0.085초 간격으로 깔고 그 위에 바람. 발소리가 앞에 선다."),
    "SFX_SkillUpper_Dash_D_Gust": ("돌진 D · 돌풍", "같은 달리기인데 바람이 앞에 서고 발소리는 아래에 깔린다."),
    "SFX_SkillUpper_Impact_A_Boom": ("충돌 A · 붐", "깊은 저역과 어두운 노이즈. 균열음을 넣지 않았다."),
    "SFX_SkillUpper_Impact_B_Slam": ("충돌 B · 슬램", "같은 붐 앞에 밝은 균열음을 붙여 더 단단하게."),
    "SFX_SkillUpper_Impact_C_Kwang": ("충돌 C · 꽝", "날카로운 균열음 + 짧은 금속 울림 + 깊은 저역. 꽝에 가장 가깝다."),
    "SFX_SkillUpper_Impact_D_Crash": ("충돌 D · 꽈앙", "금속 울림을 두 겹으로 부딪히고 짧은 반사음 꼬리를 남긴다. 가장 거칠다."),

    "SCENE_Combat_A_Soft": ("전투 A · 부드럽게", "기본 공격 3연타와 피격. 뽕 · 폭 · 툭을 함께 둔 경우."),
    "SCENE_Combat_B_Punchy": ("전투 B · 단단하게", "같은 배치에 통통 · 퍽 · 뭉을 둔 경우."),
    "SCENE_Combat_C_SoftHeavy": ("전투 C · 혼합 A+B", "부드러운 공격(뽕)에 묵직한 타격(퍽)을 붙였다. 피격은 툭."),
    "SCENE_Combat_D_BouncyDry": ("전투 D · 혼합 B+A", "반대로 통통 튀는 공격에 마른 타격(폭). 피격은 뭉."),
    "SCENE_SkillLower_A_Chime": ("저학년 A · 종 (A·A·A)", "시전 → 4발 → 폭발 4회의 전체 흐름."),
    "SCENE_SkillLower_B_Whoosh": ("저학년 B · 바람 (B·B·B)", "같은 흐름을 바람 · 틱 · 반짝으로."),
    "SCENE_SkillLower_C_ABA": ("저학년 C · 혼합 (A·B·A)", "종 시전은 그대로 두고 발사만 틱으로 바꿨다."),
    "SCENE_SkillLower_D_BAB": ("저학년 D · 혼합 (B·A·B)", "바람 시전에 퓨 발사, 반짝 폭발."),
    "SCENE_SkillLower_E_AltShots": ("저학년 E · 발사 교대 (B·A·B·A)", "4발을 틱·퓨·틱·퓨로 번갈아 쏜다. 같은 소리 4번이 아니게 된다."),
    "SCENE_SkillUpper_A_Boom": ("고학년 A · 붐", "돌진이 0.3초 뒤 충돌로 끝난다."),
    "SCENE_SkillUpper_B_Slam": ("고학년 B · 슬램", "같은 배치를 차징 · 슬램으로."),
    "SCENE_SkillUpper_C_Run_Kwang": ("고학년 C · 달리기 + 꽝", "발소리 5걸음이 끝나는 지점에서 꽝이 터진다."),
    "SCENE_SkillUpper_D_Gust_Crash": ("고학년 D · 돌풍 + 꽈앙", "바람이 앞에 선 돌진에 가장 거친 충돌을 붙였다."),
    "SCENE_SkillUpper_E_Run_Slam": ("고학년 E · 달리기 + 슬램", "고른 조합. 발소리 5걸음 뒤에 슬램이 떨어진다."),
    "SCENE_Chosen_Full": ("★ 고른 조합 전체", "통통 · 폭 · 뭉 → 종 저학년 → 달리기 + 슬램을 한 번에 이어 들려준다."),

    "SFX_Step_Boot_L": ("부츠 · 왼발", "기본 무게. 스커프 + 저역 쿵 + 중역. 발소리로 분명히 들린다."),
    "SFX_Step_Boot_R": ("부츠 · 오른발", "같은 발소리를 조금 높게. 왼발과 번갈아 울린다."),
    "SFX_Step_Stomp_L": ("쿵쿵 · 왼발", "가장 무겁다. 돌진이 돌격처럼 느껴져야 할 때."),
    "SFX_Step_Stomp_R": ("쿵쿵 · 오른발", "같은 쿵쿵을 조금 높게."),
    "SFX_Step_Light_L": ("경보 · 왼발", "더 빠르고 밝다. 쿵쿵거리기보다 달리기에 가깝다."),
    "SFX_Step_Light_R": ("경보 · 오른발", "같은 경보를 조금 높게."),

    "SCENE_Hit_E_Punch": ("★ 타격 E · 퍅", "통통 기본 공격 3연타에 새 타격음 E를 얹었다."),
    "SCENE_Hit_F_Smash": ("★ 타격 F · 쾅", "같은 배치에 가장 무거운 F."),
    "SCENE_Hit_G_Impact": ("★ 타격 G · 탁", "같은 배치에 가장 단단한 G."),
    "SCENE_Cast_C_Magic": ("★ 시전 C · 마법", "새 시전음으로 저학년 전체 흐름을 들려준다."),
    "SCENE_Cast_D_Sparkle": ("★ 시전 D · 반짝임", "같은 흐름을 반짝임 시전으로."),
    "SCENE_Cast_E_Chant": ("★ 시전 E · 영창", "같은 흐름을 영창 시전으로."),
    "SCENE_Dash_Boot": ("★ 돌진 · 부츠", "1.9초 돌진. 발소리가 돌진하는 동안 계속 울리고 마지막에 슬램이 떨어진다."),
    "SCENE_Dash_Stomp": ("★ 돌진 · 쿵쿵", "같은 돌진을 가장 무거운 발소리로."),
    "SCENE_Dash_Light": ("★ 돌진 · 경보", "같은 돌진을 0.14초 간격의 빠른 발소리로."),

    "SFX_AttackBurst_A_Pop": ("A · 퍽 (조여진 폭발)", "가장 짧고 밝다. 연타에서 제일 안전하다."),
    "SFX_AttackBurst_B_Boom": ("B · 쾅 (가장 폭발답게)", "둥글고 저역이 깊다. 넷 중 가장 폭발처럼 들린다."),
    "SFX_AttackBurst_C_Crackle": ("C · 파직 (파편)", "폭발 뒤에 파편 두 조각이 떨어진다. 질감이 가장 많다."),
    "SFX_AttackBurst_D_Puff": ("D · 퐁 (부드럽게)", "공기가 터지고 작은 반짝임이 남는다. 게임 톤에 가장 가볍게 붙는다."),

    "SCENE_Burst_A_Pop": ("★ 폭발 A · 퍽", "기본 공격 4연타에 폭발 A. 연타에서 뭉개지는지 보는 용도."),
    "SCENE_Burst_B_Boom": ("★ 폭발 B · 쾅", "같은 배치에 폭발 B."),
    "SCENE_Burst_C_Crackle": ("★ 폭발 C · 파직", "같은 배치에 폭발 C."),
    "SCENE_Burst_D_Puff": ("★ 폭발 D · 퐁", "같은 배치에 폭발 D."),
    "SCENE_Burst_With_Tick": ("★ 폭발 + 틱 데미지", "독에 걸린 적을 때릴 때. 폭발음과 틱음이 서로 구분되는지 확인한다."),
}

# Shown as chips on the composite rows, so the sequence is readable.
SCENE_PARTS = {
    "SCENE_Combat_A_Soft": ["공격 ×3", "+0.09초 타격", "1.05초 피격"],
    "SCENE_Combat_B_Punchy": ["공격 ×3", "+0.09초 타격", "1.05초 피격"],
    "SCENE_Combat_C_SoftHeavy": ["공격 A ×3", "타격 B", "피격 A"],
    "SCENE_Combat_D_BouncyDry": ["공격 B ×3", "타격 A", "피격 B"],
    "SCENE_SkillLower_A_Chime": ["시전", "발사 ×4 · 0.08초", "폭발 ×4"],
    "SCENE_SkillLower_B_Whoosh": ["시전", "발사 ×4 · 0.08초", "폭발 ×4"],
    "SCENE_SkillLower_C_ABA": ["시전 A", "발사 B ×4", "폭발 A ×4"],
    "SCENE_SkillLower_D_BAB": ["시전 B", "발사 A ×4", "폭발 B ×4"],
    "SCENE_SkillLower_E_AltShots": ["시전 A", "발사 B·A·B·A", "폭발 A ×4"],
    "SCENE_SkillUpper_A_Boom": ["돌진", "0.30초 충돌"],
    "SCENE_SkillUpper_B_Slam": ["돌진", "0.28초 충돌"],
    "SCENE_SkillUpper_C_Run_Kwang": ["발소리 ×5 + 바람", "0.40초 꽝"],
    "SCENE_SkillUpper_D_Gust_Crash": ["바람 + 발소리", "0.40초 꽈앙"],
    "SCENE_SkillUpper_E_Run_Slam": ["발소리 ×5 + 바람", "0.40초 슬램"],
    "SCENE_Chosen_Full": ["기본 공격 + 타격", "피격", "저학년 전체", "고학년"],
    "SCENE_Hit_E_Punch": ["공격 ×3", "+0.09초 타격"],
    "SCENE_Hit_F_Smash": ["공격 ×3", "+0.09초 타격"],
    "SCENE_Hit_G_Impact": ["공격 ×3", "+0.09초 타격"],
    "SCENE_Cast_C_Magic": ["시전", "발사 ×4 · 0.08초", "폭발 ×4"],
    "SCENE_Cast_D_Sparkle": ["시전", "발사 ×4 · 0.08초", "폭발 ×4"],
    "SCENE_Cast_E_Chant": ["시전", "발사 ×4 · 0.08초", "폭발 ×4"],
    "SCENE_Dash_Boot": ["출발 바람", "발소리 1.9초 · 0.17초 간격", "2.0초 슬램"],
    "SCENE_Dash_Stomp": ["출발 바람", "발소리 1.9초 · 0.17초 간격", "2.0초 슬램"],
    "SCENE_Dash_Light": ["출발 바람", "발소리 1.9초 · 0.14초 간격", "2.0초 슬램"],
    "SCENE_Burst_A_Pop": ["공격 ×4 · 0.35초", "+0.09초 폭발"],
    "SCENE_Burst_B_Boom": ["공격 ×4 · 0.35초", "+0.09초 폭발"],
    "SCENE_Burst_C_Crackle": ["공격 ×4 · 0.35초", "+0.09초 폭발"],
    "SCENE_Burst_D_Puff": ["공격 ×4 · 0.35초", "+0.09초 폭발"],
    "SCENE_Burst_With_Tick": ["공격 + 폭발 ×3", "틱 데미지 ×4"],
}


def read_wav(path):
    with wave.open(path, "rb") as fh:
        frames = fh.readframes(fh.getnframes())
        rate = fh.getframerate()
    samples = np.frombuffer(frames, dtype="<i2").astype(np.float32) / 32768.0
    return samples, rate


def envelope(samples, bars=ENVELOPE_BARS):
    if len(samples) < bars:
        samples = np.pad(samples, (0, bars - len(samples)))
    chunks = np.array_split(np.abs(samples), bars)
    peaks = np.array([c.max() if len(c) else 0.0 for c in chunks])
    top = peaks.max() or 1.0
    return [round(float(v / top), 3) for v in peaks]


def data_uri(path):
    with open(path, "rb") as fh:
        return "data:audio/wav;base64," + base64.b64encode(fh.read()).decode("ascii")


def burst_info(group_dir, name):
    bdir = os.path.join(group_dir, "burst")
    if not os.path.isdir(bdir):
        return None
    for entry in sorted(os.listdir(bdir)):
        if entry.startswith(name + "_burst") and entry.endswith(".wav"):
            count = int(entry[len(name) + len("_burst"):-len(".wav")])
            return {
                "count": count,
                "gap": BURST_GAP.get(name, 0.3),
                "audio": data_uri(os.path.join(bdir, entry)),
            }
    return None


def build(sfx_dir):
    groups = []
    for key, title, chip, note in GROUPS:
        group_dir = os.path.join(sfx_dir, key)
        if not os.path.isdir(group_dir):
            continue
        # Keep the order the sounds are written in above (cast, shot, burst),
        # not the alphabetical order of the file names.
        order = list(LABELS)
        def rank(entry):
            name = entry[:-len(".wav")]
            return (order.index(name) if name in order else len(order), entry)

        items = []
        for entry in sorted(os.listdir(group_dir), key=rank):
            if not entry.endswith(".wav"):
                continue
            name = entry[:-len(".wav")]
            path = os.path.join(group_dir, entry)
            samples, rate = read_wav(path)
            label, desc = LABELS.get(name, (name, ""))
            item = {
                "name": name,
                "label": label,
                "desc": desc,
                "ms": int(round(len(samples) / rate * 1000)),
                "env": envelope(samples),
                "audio": data_uri(path),
            }
            if name in SCENE_PARTS:
                item["parts"] = SCENE_PARTS[name]
            b = burst_info(group_dir, name)
            if b:
                item["burst"] = b
            items.append(item)
        if items:
            groups.append({"title": title, "key": chip, "note": note, "items": items})
    return {"groups": groups}


def main(argv):
    if len(argv) < 2:
        raise SystemExit("usage: audition_page.py <sfx_dir> <out_html>")
    sfx_dir, out_html = argv[0], argv[1]
    here = os.path.dirname(os.path.abspath(__file__))
    template = io.open(os.path.join(here, "audition_template.html"), encoding="utf-8").read()
    payload = json.dumps(build(sfx_dir), ensure_ascii=False, separators=(",", ":"))
    html = template.replace("/*__SFX_DATA__*/", payload)
    os.makedirs(os.path.dirname(os.path.abspath(out_html)), exist_ok=True)
    io.open(out_html, "w", encoding="utf-8", newline="\n").write(html)
    print(f"{out_html}  {len(html) / 1e6:.2f} MB")


if __name__ == "__main__":
    main(sys.argv[1:])
