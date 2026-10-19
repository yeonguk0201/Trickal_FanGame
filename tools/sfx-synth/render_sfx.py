"""
Sound definitions for TrickalFanGame, rendered by the sfx_synth engine.

    python render_sfx.py <out_dir> [group ...] [--burst=N] [--burst-gap=S]

Renders every preset of the selected groups (all groups when none is named)
into <out_dir>/<group>/. --burst also writes a take with the same sound
repeated N times, which is how you judge whether a frequently played sound
gets tiring. Parameter reference: see the sfx_synth docstring.
"""
import sys, os, json

import numpy as np
from sfx_synth import SR, render_preset, burst, finish
from scipy.io import wavfile

# ---------------------------------------------------------------------
# Erpin basic attack: light, round, bouncy, short.
# ---------------------------------------------------------------------
ERPIN_ATTACK = {
    # A: classic soft "ppyong" - sine sweeping up quickly
    "SFX_Erpin_Attack_A_Ppyong": dict(
        wave="sine", freq=620, slide=9.0, dslide=-40, attack=0.002,
        sustain=0.012, decay=0.07, punch=0.3, gain=-4),
    # B: "tong-tong" - same body plus a smaller, higher bounce
    "SFX_Erpin_Attack_B_TongTong": dict(
        wave="sine", freq=560, slide=7.0, dslide=-30, attack=0.002,
        sustain=0.01, decay=0.055, punch=0.3,
        echo=[(0.055, 0.45)], echo_pitch=1.33, gain=-4),
    # C: "ttorong" - triangle two-note arpeggio, toy-like
    "SFX_Erpin_Attack_C_Ttorong": dict(
        wave="triangle", freq=880, slide=1.5, attack=0.002, sustain=0.03,
        decay=0.06, arp=[(0.035, 1.5)], lpf=5000, gain=-5),
    # D: bubble "plip" - fast rising soft square through a low-pass
    "SFX_Erpin_Attack_D_Bubble": dict(
        wave="square", duty=0.35, freq=420, slide=16.0, dslide=-60,
        attack=0.001, sustain=0.008, decay=0.06, lpf=1800, hpf=150, gain=-4),
    # E: jelly "boing" - up then gently down with a little wobble
    "SFX_Erpin_Attack_E_Jelly": dict(
        wave="sine", freq=520, slide=10.0, dslide=-160, attack=0.003,
        sustain=0.015, decay=0.09, vib_depth=0.04, vib_speed=28, gain=-5),
    # F: retro "pew" - short triangle with a light bit-crush grain
    "SFX_Erpin_Attack_F_RetroPop": dict(
        wave="triangle", freq=700, slide=6.0, attack=0.001, sustain=0.01,
        decay=0.05, bits=6, lpf=6000, gain=-6),
}

# ---------------------------------------------------------------------
# Impact of a player attack on an enemy. Plays on every single hit, so it
# has to stay short and must not pile up into noise during a burst.
# ---------------------------------------------------------------------
HIT = {
    # A: woody "pock" - a dry click over a short mid body
    "SFX_Hit_A_Pock": dict(gain=-5, layers=[
        dict(wave="noise", seed=3, attack=0.0005, sustain=0.004, decay=0.028,
             hpf=1400, lpf=7000, mix=0.55),
        dict(wave="triangle", freq=330, slide=-6.0, attack=0.001,
             sustain=0.008, decay=0.05, punch=0.25, lpf=4500, mix=1.0),
    ]),
    # B: "thwack" - noise forward, low body underneath, the meatiest option
    "SFX_Hit_B_Thwack": dict(gain=-5, layers=[
        dict(wave="noise", seed=7, attack=0.0005, sustain=0.006, decay=0.06,
             lpf=6500, lpf_end=700, hpf=200, mix=0.9),
        dict(wave="sine", freq=195, slide=-4.0, attack=0.001, sustain=0.01,
             decay=0.07, punch=0.4, mix=0.8),
    ]),
    # C: round "pop" - softest of the set, closest to the attack sounds
    "SFX_Hit_C_Pop": dict(gain=-5, layers=[
        dict(wave="sine", freq=540, slide=-9.0, attack=0.001, sustain=0.008,
             decay=0.05, punch=0.35, mix=1.0),
        dict(wave="noise", seed=11, attack=0.0005, sustain=0.002,
             decay=0.018, hpf=2200, mix=0.3),
    ]),
    # D: "crunch" - bit-crushed, reads as a retro game hit
    "SFX_Hit_D_Crunch": dict(gain=-6, layers=[
        dict(wave="noise", seed=5, attack=0.0005, sustain=0.005, decay=0.04,
             lpf=4000, lpf_end=1200, bits=5, mix=0.8),
        dict(wave="square", duty=0.3, freq=240, slide=-7.0, attack=0.001,
             sustain=0.008, decay=0.045, lpf=2500, mix=0.7),
    ]),
    # The A-D set was judged too weak to feel like a hit. What was missing
    # is weight under the click: a low body with real punch and a length you
    # can feel, plus a mid slap so it reads through the music. These stay
    # under 0.2s so they do not smear at the attack's own cadence.
    # E: "punch" - hard transient, deep body, mid slap
    "SFX_Hit_E_Punch": dict(gain=-3, layers=[
        dict(wave="noise", seed=101, attack=0.0002, sustain=0.003, decay=0.025,
             hpf=1200, mix=0.85),
        dict(wave="sine", freq=120, slide=-3.5, attack=0.001, sustain=0.012,
             decay=0.13, decay_pow=1.4, punch=0.7, mix=1.0),
        dict(wave="triangle", freq=260, slide=-5.0, attack=0.001,
             sustain=0.008, decay=0.06, lpf=3500, mix=0.6),
        dict(wave="noise", seed=103, attack=0.001, sustain=0.006, decay=0.08,
             lpf=3500, lpf_end=500, mix=0.5),
    ]),
    # F: "smash" - heaviest, a crushed crack over a longer low tail
    "SFX_Hit_F_Smash": dict(gain=-3, layers=[
        dict(wave="noise", seed=105, attack=0.0002, sustain=0.004, decay=0.035,
             hpf=900, bits=7, mix=0.9),
        dict(wave="sine", freq=98, slide=-2.8, attack=0.001, sustain=0.016,
             decay=0.17, decay_pow=1.3, punch=0.8, mix=1.0),
        dict(wave="square", duty=0.28, freq=190, slide=-5.5, attack=0.001,
             sustain=0.008, decay=0.07, lpf=2200, mix=0.5),
        dict(wave="noise", seed=107, attack=0.001, sustain=0.008, decay=0.1,
             lpf=2800, lpf_end=350, mix=0.55),
    ]),
    # G: "impact" - tightest of the three, bright crack and a short hard body
    "SFX_Hit_G_Impact": dict(gain=-3, layers=[
        dict(wave="noise", seed=109, attack=0.0002, sustain=0.0025,
             decay=0.018, hpf=2000, mix=0.8),
        dict(wave="sine", freq=145, slide=-4.5, attack=0.001, sustain=0.01,
             decay=0.09, decay_pow=1.5, punch=0.75, mix=1.0),
        dict(wave="triangle", freq=330, slide=-6.0, attack=0.001,
             sustain=0.006, decay=0.045, lpf=4500, mix=0.5),
    ]),
}

# ---------------------------------------------------------------------
# Erpin's basic attack lands as a small explosion, so its hit sound is a
# blast rather than a knock. It plays on every single hit, which is the
# hard part: it has to read as an explosion while staying short enough not
# to smear at the attack's own cadence, and smaller than the lower-grade
# skill's blast so the two do not get confused.
# ---------------------------------------------------------------------
ATTACK_BURST = {
    # A: "pop" - tight and bright, the shortest blast of the set
    "SFX_AttackBurst_A_Pop": dict(gain=-4, layers=[
        dict(wave="noise", seed=161, attack=0.0003, sustain=0.003, decay=0.03,
             hpf=1800, mix=0.7),
        dict(wave="noise", seed=163, attack=0.001, sustain=0.008, decay=0.1,
             lpf=5200, lpf_end=450, hpf=140, mix=1.0),
        dict(wave="sine", freq=150, slide=-3.0, attack=0.001, sustain=0.012,
             decay=0.1, decay_pow=1.4, punch=0.6, mix=0.8),
    ]),
    # B: "boom" - rounder and deeper, the most explosive
    "SFX_AttackBurst_B_Boom": dict(gain=-4, layers=[
        dict(wave="noise", seed=165, attack=0.001, sustain=0.01, decay=0.15,
             lpf=4200, lpf_end=300, hpf=110, mix=0.95),
        dict(wave="sine", freq=110, slide=-2.4, attack=0.002, sustain=0.018,
             decay=0.17, decay_pow=1.35, punch=0.7, mix=1.0),
        dict(wave="noise", seed=167, attack=0.0003, sustain=0.0025,
             decay=0.025, hpf=1400, mix=0.5),
    ]),
    # C: "crackle" - the blast plus two bits of debris landing after it
    "SFX_AttackBurst_C_Crackle": dict(gain=-4, layers=[
        dict(wave="noise", seed=169, attack=0.0003, sustain=0.003, decay=0.03,
             hpf=2000, mix=0.65),
        dict(wave="noise", seed=171, attack=0.001, sustain=0.008, decay=0.11,
             lpf=4800, lpf_end=400, hpf=130, mix=0.9),
        dict(wave="sine", freq=135, slide=-2.8, attack=0.001, sustain=0.014,
             decay=0.12, decay_pow=1.4, punch=0.6, mix=0.8),
        dict(wave="noise", seed=173, attack=0.0005, sustain=0.002, decay=0.02,
             hpf=3000, delay=0.06, mix=0.3),
        dict(wave="noise", seed=175, attack=0.0005, sustain=0.002,
             decay=0.018, hpf=3600, delay=0.095, mix=0.22),
    ]),
    # D: "puff" - softest, a burst of air with a little glint; closest to
    # the game's lighter tone
    "SFX_AttackBurst_D_Puff": dict(gain=-4, layers=[
        dict(wave="noise", seed=177, attack=0.004, sustain=0.01, decay=0.13,
             lpf=2600, lpf_end=320, hpf=180, mix=1.0),
        dict(wave="sine", freq=190, slide=-4.0, attack=0.002, sustain=0.01,
             decay=0.09, punch=0.45, mix=0.6),
        dict(wave="triangle", freq=1400, slide=-1.0, attack=0.003,
             sustain=0.01, decay=0.1, lpf=8000, delay=0.02, mix=0.22),
    ]),
}

# ---------------------------------------------------------------------
# Footsteps for the upper-grade dash. The dash runs for up to ten
# seconds, so the game repeats one step at a cadence instead of playing a
# fixed run; these are single footfalls, in left and right pairs so two
# in a row are never the same sample.
# ---------------------------------------------------------------------
def footstep(thump, mid, scuff_seed, tail_seed, heavy=False, bright=False):
    layers = [
        # The scuff is what makes it a foot rather than a drum.
        dict(wave="noise", seed=scuff_seed, attack=0.0003, sustain=0.004,
             decay=0.03, hpf=900, lpf=9000 if bright else 7000, mix=0.7),
        dict(wave="sine", freq=thump, slide=-4.0, attack=0.001, sustain=0.01,
             decay=0.11 if heavy else 0.09, decay_pow=1.45,
             punch=0.75 if heavy else 0.6, mix=1.0),
        dict(wave="triangle", freq=mid, slide=-6.0, attack=0.001,
             sustain=0.006, decay=0.05, lpf=2800, mix=0.5 if heavy else 0.42),
        dict(wave="noise", seed=tail_seed, attack=0.002, sustain=0.006,
             decay=0.08, lpf=2400, lpf_end=400, mix=0.38),
    ]
    return dict(gain=-4, layers=layers)


STEP = {
    # boot: the default weight, clearly a footfall
    "SFX_Step_Boot_L": footstep(96, 232, 111, 113),
    "SFX_Step_Boot_R": footstep(107, 254, 115, 117),
    # stomp: heavier, for a dash that should feel like a charge
    "SFX_Step_Stomp_L": footstep(84, 205, 119, 121, heavy=True),
    "SFX_Step_Stomp_R": footstep(93, 224, 123, 125, heavy=True),
    # light: quicker and brighter, closer to running than stomping
    "SFX_Step_Light_L": footstep(126, 300, 127, 129, bright=True),
    "SFX_Step_Light_R": footstep(139, 326, 131, 133, bright=True),
}

# ---------------------------------------------------------------------
# Player taking damage. Must be clearly distinct from the hit sounds:
# lower, bending downwards, a touch longer.
# ---------------------------------------------------------------------
HURT = {
    # A: muffled "thud" - low body with a soft dull noise around it
    "SFX_Hurt_A_Thud": dict(gain=-4, layers=[
        dict(wave="sine", freq=280, slide=-5.0, dslide=2.0, attack=0.002,
             sustain=0.02, decay=0.2, decay_pow=1.7, punch=0.3, mix=1.0),
        dict(wave="noise", seed=21, attack=0.001, sustain=0.008, decay=0.08,
             lpf=1400, lpf_end=400, mix=0.4),
    ]),
    # B: cartoon "wobble" - squashy, keeps the light tone of the game
    "SFX_Hurt_B_Wobble": dict(gain=-5, layers=[
        dict(wave="square", duty=0.3, freq=300, slide=-6.0, attack=0.002,
             sustain=0.02, decay=0.18, decay_pow=1.8, lpf=1700,
             vib_depth=0.07, vib_speed=22, mix=1.0),
        dict(wave="noise", seed=23, attack=0.001, sustain=0.004, decay=0.05,
             lpf=2000, mix=0.25),
    ]),
    # C: descending "sting" - two falling notes, reads as a mistake
    "SFX_Hurt_C_Sting": dict(gain=-5, layers=[
        dict(wave="triangle", freq=460, attack=0.002, sustain=0.06,
             decay=0.14, decay_pow=1.8, arp=[(0.055, 0.75), (0.11, 0.63)],
             lpf=4000, mix=1.0),
        dict(wave="noise", seed=27, attack=0.001, sustain=0.005, decay=0.05,
             lpf=1200, mix=0.3),
    ]),
}

# ---------------------------------------------------------------------
# Lower-grade skill (Space): 4 homing shots fanned out 0.08s apart, each
# ending in a small area explosion. Three roles - the cast, the per-shot
# launch, the explosion - so they are dialled in separately.
# ---------------------------------------------------------------------
SKILL_LOWER = {
    # cast A: rising chime, the "the magic is out" cue
    "SFX_SkillLower_Cast_A_Chime": dict(
        wave="triangle", freq=520, attack=0.003, sustain=0.05, decay=0.16,
        decay_pow=1.6, arp=[(0.045, 1.26), (0.085, 1.5)], lpf=6000, gain=-6),
    # cast B: airy upward whoosh under a rising tone
    "SFX_SkillLower_Cast_B_Whoosh": dict(gain=-6, layers=[
        dict(wave="noise", seed=31, attack=0.05, sustain=0.02, decay=0.14,
             lpf=700, lpf_end=6500, hpf=300, mix=0.7),
        dict(wave="sine", freq=380, slide=3.0, attack=0.02, sustain=0.03,
             decay=0.12, mix=0.6),
    ]),
    # The A and B casts read as a UI chime rather than a spell. A cast
    # sounds magical when three things happen at once: air gathering before
    # the note (the swell), a bell that rings longer than a game blip, and
    # a shimmer above it that beats slightly out of tune with the bell.
    # cast C: "magic" - swell into a rising bell with a shimmer tail
    "SFX_SkillLower_Cast_C_Magic": dict(gain=-4, layers=[
        dict(wave="noise", seed=141, attack=0.09, sustain=0.02, decay=0.1,
             lpf=900, lpf_end=9000, hpf=500, mix=0.45),
        dict(wave="triangle", freq=660, attack=0.003, sustain=0.04,
             decay=0.3, decay_pow=1.3, arp=[(0.05, 1.5), (0.1, 2.0)],
             lpf=9000, mix=0.9),
        dict(wave="triangle", freq=1760, vib_depth=0.03, vib_speed=11,
             attack=0.02, sustain=0.06, decay=0.35, decay_pow=1.2,
             lpf=11000, delay=0.04, mix=0.35),
        dict(wave="sine", freq=160, slide=1.5, attack=0.01, sustain=0.03,
             decay=0.18, mix=0.4),
    ]),
    # cast D: "sparkle" - four notes climbing, lighter and busier
    "SFX_SkillLower_Cast_D_Sparkle": dict(gain=-4, layers=[
        dict(wave="noise", seed=143, attack=0.06, sustain=0.015, decay=0.08,
             lpf=1400, lpf_end=11000, hpf=800, mix=0.35),
        dict(wave="triangle", freq=784, attack=0.002, sustain=0.03,
             decay=0.26, decay_pow=1.3,
             arp=[(0.035, 1.26), (0.07, 1.5), (0.105, 1.89)],
             lpf=11000, mix=0.85),
        dict(wave="sine", freq=2640, vib_depth=0.05, vib_speed=14,
             attack=0.03, sustain=0.05, decay=0.3, decay_pow=1.15,
             delay=0.06, mix=0.28),
    ]),
    # cast E: "chant" - lower and heavier, a spell being cast rather than
    # a pickup being collected
    "SFX_SkillLower_Cast_E_Chant": dict(gain=-4, layers=[
        dict(wave="noise", seed=145, attack=0.11, sustain=0.03, decay=0.14,
             lpf=600, lpf_end=5000, hpf=320, mix=0.5),
        dict(wave="triangle", freq=330, vib_depth=0.02, vib_speed=7,
             attack=0.05, sustain=0.07, decay=0.3, decay_pow=1.25,
             lpf=4500, mix=0.8),
        dict(wave="triangle", freq=990, attack=0.004, sustain=0.03,
             decay=0.32, decay_pow=1.2, arp=[(0.08, 1.33)], lpf=9000,
             delay=0.09, mix=0.45),
        dict(wave="sine", freq=110, slide=1.0, attack=0.02, sustain=0.05,
             decay=0.22, mix=0.5),
    ]),
    # shot A: tiny soft pew, played 4x in a row - deliberately thin
    "SFX_SkillLower_Shot_A_Pew": dict(
        wave="sine", freq=740, slide=5.0, dslide=-26, attack=0.001,
        sustain=0.006, decay=0.04, punch=0.25, gain=-9),
    # shot B: breathier launch tick, mixes better under the cast
    "SFX_SkillLower_Shot_B_Tick": dict(gain=-10, layers=[
        dict(wave="noise", seed=37, attack=0.0005, sustain=0.003,
             decay=0.025, hpf=1800, lpf=9000, mix=0.6),
        dict(wave="triangle", freq=900, slide=4.0, attack=0.001,
             sustain=0.004, decay=0.03, mix=0.5),
    ]),
    # burst A: soft area explosion, light enough to overlap up to 4 times
    "SFX_SkillLower_Burst_A_Soft": dict(gain=-6, layers=[
        dict(wave="noise", seed=41, attack=0.001, sustain=0.01, decay=0.2,
             lpf=4200, lpf_end=450, hpf=160, mix=0.9),
        dict(wave="sine", freq=170, slide=-3.0, attack=0.002, sustain=0.015,
             decay=0.16, decay_pow=1.7, punch=0.35, mix=0.75),
    ]),
    # burst B: same body plus a short sparkle tail, more magic than bomb
    "SFX_SkillLower_Burst_B_Sparkle": dict(gain=-6, layers=[
        dict(wave="noise", seed=43, attack=0.001, sustain=0.008, decay=0.16,
             lpf=3800, lpf_end=600, hpf=180, mix=0.8),
        dict(wave="sine", freq=190, slide=-3.5, attack=0.002, sustain=0.012,
             decay=0.13, punch=0.35, mix=0.7),
        dict(wave="triangle", freq=1500, slide=1.2, attack=0.004,
             sustain=0.02, decay=0.18, decay_pow=1.5, lpf=9000,
             delay=0.03, mix=0.3),
    ]),
}

def footsteps(times, mix=0.5, freqs=(128, 142), seed0=73):
    """One low thud plus a scuff per step. The pitch alternates, so it reads
    as left foot, right foot instead of the same sample four times."""
    out = []
    for i, t in enumerate(times):
        out.append(dict(wave="sine", freq=freqs[i % len(freqs)], slide=-5.0,
                        attack=0.001, sustain=0.006, decay=0.055, punch=0.4,
                        delay=t, mix=mix))
        out.append(dict(wave="noise", seed=seed0 + i, attack=0.0005,
                        sustain=0.004, decay=0.045, lpf=1800, lpf_end=400,
                        delay=t, mix=mix * 0.55))
    return out


# A sprint, not a stroll: five steps 0.085s apart.
RUN_STEPS = [0.0, 0.085, 0.17, 0.255, 0.34]

# ---------------------------------------------------------------------
# Upper-grade skill (Q): invulnerable dash that ends on contact with area
# damage and knockback. The dash cue and the collision are separate events.
# ---------------------------------------------------------------------
SKILL_UPPER = {
    # dash A: wide air whoosh, no pitch of its own
    "SFX_SkillUpper_Dash_A_Whoosh": dict(gain=-5, layers=[
        dict(wave="noise", seed=51, attack=0.1, sustain=0.06, decay=0.3,
             decay_pow=1.6, lpf=500, lpf_end=6000, hpf=260, mix=1.0),
    ]),
    # dash B: whoosh with a rising charge tone, more skill than movement
    "SFX_SkillUpper_Dash_B_Charge": dict(gain=-5, layers=[
        dict(wave="noise", seed=53, attack=0.09, sustain=0.05, decay=0.26,
             decay_pow=1.6, lpf=600, lpf_end=5200, hpf=240, mix=0.85),
        dict(wave="triangle", freq=260, slide=2.2, attack=0.04,
             sustain=0.06, decay=0.2, decay_pow=1.7, lpf=3500, mix=0.6),
    ]),
    # impact A: heavy "boom" - deep body, dark noise, no crack
    "SFX_SkillUpper_Impact_A_Boom": dict(gain=-3, layers=[
        dict(wave="sine", freq=155, slide=-3.2, attack=0.002, sustain=0.025,
             decay=0.42, decay_pow=1.5, punch=0.5, mix=1.0),
        dict(wave="noise", seed=57, attack=0.001, sustain=0.015, decay=0.3,
             lpf=2600, lpf_end=220, mix=0.55),
    ]),
    # impact B: "slam" - same boom plus a bright crack on the front
    "SFX_SkillUpper_Impact_B_Slam": dict(gain=-3, layers=[
        dict(wave="noise", seed=59, attack=0.0005, sustain=0.004,
             decay=0.05, hpf=2400, mix=0.5),
        dict(wave="sine", freq=170, slide=-3.6, attack=0.002, sustain=0.02,
             decay=0.38, decay_pow=1.5, punch=0.5, mix=1.0),
        dict(wave="noise", seed=61, attack=0.001, sustain=0.012, decay=0.26,
             lpf=3000, lpf_end=260, delay=0.006, mix=0.5),
    ]),
    # dash C: running steps under a wind sweep, the steps out front
    "SFX_SkillUpper_Dash_C_Run": dict(gain=-5, layers=[
        dict(wave="noise", seed=71, attack=0.07, sustain=0.1, decay=0.3,
             decay_pow=1.6, lpf=450, lpf_end=5000, hpf=230, mix=0.6),
    ] + footsteps(RUN_STEPS, mix=0.6)),
    # dash D: the same run with the wind out front and the steps underneath
    "SFX_SkillUpper_Dash_D_Gust": dict(gain=-5, layers=[
        dict(wave="noise", seed=77, attack=0.06, sustain=0.13, decay=0.34,
             decay_pow=1.5, lpf=380, lpf_end=6200, hpf=200, mix=1.0),
        dict(wave="noise", seed=79, attack=0.12, sustain=0.08, decay=0.3,
             decay_pow=1.6, lpf=240, lpf_end=1300, mix=0.45),
    ] + footsteps(RUN_STEPS, mix=0.32)),
    # impact C: "kwang" - hard crack, a short metallic ring, deep body
    "SFX_SkillUpper_Impact_C_Kwang": dict(gain=-2, layers=[
        dict(wave="noise", seed=81, attack=0.0003, sustain=0.005, decay=0.07,
             hpf=2600, mix=0.75),
        dict(wave="triangle", freq=480, slide=-1.2, attack=0.001,
             sustain=0.02, decay=0.34, decay_pow=1.25, lpf=7500, mix=0.3),
        dict(wave="sine", freq=108, slide=-2.4, attack=0.002, sustain=0.03,
             decay=0.5, decay_pow=1.35, punch=0.65, mix=1.0),
        dict(wave="noise", seed=83, attack=0.001, sustain=0.018, decay=0.36,
             lpf=3000, lpf_end=200, delay=0.005, mix=0.6),
    ]),
    # impact D: harder still - two clashing metal rings and a slap-back tail
    "SFX_SkillUpper_Impact_D_Crash": dict(gain=-2, layers=[
        dict(wave="noise", seed=85, attack=0.0003, sustain=0.004, decay=0.05,
             hpf=3200, mix=0.7),
        dict(wave="square", duty=0.25, freq=300, slide=-0.8, attack=0.001,
             sustain=0.02, decay=0.3, decay_pow=1.3, lpf=6000, mix=0.28),
        dict(wave="triangle", freq=760, slide=-1.0, attack=0.001,
             sustain=0.015, decay=0.26, decay_pow=1.3, lpf=9000,
             delay=0.004, mix=0.22),
        dict(wave="sine", freq=120, slide=-2.8, attack=0.002, sustain=0.028,
             decay=0.45, decay_pow=1.4, punch=0.6, mix=1.0),
        dict(wave="noise", seed=87, attack=0.001, sustain=0.015, decay=0.3,
             lpf=3400, lpf_end=240, mix=0.55),
        dict(wave="noise", seed=89, attack=0.004, sustain=0.01, decay=0.2,
             lpf=1800, lpf_end=300, delay=0.09, mix=0.2),
    ]),
}

PRESET_GROUPS = {
    "erpin-attack": ERPIN_ATTACK,
    "hit": HIT,
    "hurt": HURT,
    "skill-lower": SKILL_LOWER,
    "skill-upper": SKILL_UPPER,
    "step": STEP,
    "attack-burst": ATTACK_BURST,
}


# The dash repeats a step every 0.17s, alternating feet.
STEP_CADENCE = 0.17


def running(left, right, start, seconds, level=0.85, cadence=STEP_CADENCE):
    """Scene parts for a run of `seconds`, alternating the two feet."""
    parts = []
    count = int(seconds / cadence)
    for i in range(count):
        parts.append((start + i * cadence, left if i % 2 == 0 else right, level))
    return parts

# ---------------------------------------------------------------------
# Composites: the single sounds placed the way the game fires them. A
# candidate can only be judged next to the sounds it plays against, and
# the lower-grade volley is four shots and four explosions, not one sound.
# Each part is (start_seconds, preset_name, level).
# ---------------------------------------------------------------------
SCENES = {
    # one basic attack and its impact, three times, then taking a hit
    "SCENE_Combat_A_Soft": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_A_Ppyong", 0.8),
        (0.09, "SFX_Hit_A_Pock", 0.9),
        (0.30, "SFX_Erpin_Attack_A_Ppyong", 0.8),
        (0.39, "SFX_Hit_A_Pock", 0.9),
        (0.60, "SFX_Erpin_Attack_A_Ppyong", 0.8),
        (0.69, "SFX_Hit_A_Pock", 0.9),
        (1.05, "SFX_Hurt_A_Thud", 1.0),
    ]),
    "SCENE_Combat_B_Punchy": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8),
        (0.09, "SFX_Hit_B_Thwack", 0.9),
        (0.30, "SFX_Erpin_Attack_B_TongTong", 0.8),
        (0.39, "SFX_Hit_B_Thwack", 0.9),
        (0.60, "SFX_Erpin_Attack_B_TongTong", 0.8),
        (0.69, "SFX_Hit_B_Thwack", 0.9),
        (1.05, "SFX_Hurt_B_Wobble", 1.0),
    ]),
    # the full lower-grade volley: cast, four shots 0.08s apart, four hits
    "SCENE_SkillLower_A_Chime": dict(gain=-4, parts=[
        (0.00, "SFX_SkillLower_Cast_A_Chime", 0.9),
        (0.02, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.10, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.18, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.26, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.44, "SFX_SkillLower_Burst_A_Soft", 0.9),
        (0.53, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.60, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.70, "SFX_SkillLower_Burst_A_Soft", 0.8),
    ]),
    "SCENE_SkillLower_B_Whoosh": dict(gain=-4, parts=[
        (0.00, "SFX_SkillLower_Cast_B_Whoosh", 0.9),
        (0.02, "SFX_SkillLower_Shot_B_Tick", 0.7),
        (0.10, "SFX_SkillLower_Shot_B_Tick", 0.7),
        (0.18, "SFX_SkillLower_Shot_B_Tick", 0.7),
        (0.26, "SFX_SkillLower_Shot_B_Tick", 0.7),
        (0.44, "SFX_SkillLower_Burst_B_Sparkle", 0.9),
        (0.53, "SFX_SkillLower_Burst_B_Sparkle", 0.8),
        (0.60, "SFX_SkillLower_Burst_B_Sparkle", 0.8),
        (0.70, "SFX_SkillLower_Burst_B_Sparkle", 0.8),
    ]),
    # cross combinations: a soft attack with a heavy hit, and the reverse
    "SCENE_Combat_C_SoftHeavy": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_A_Ppyong", 0.8),
        (0.09, "SFX_Hit_B_Thwack", 0.9),
        (0.30, "SFX_Erpin_Attack_A_Ppyong", 0.8),
        (0.39, "SFX_Hit_B_Thwack", 0.9),
        (0.60, "SFX_Erpin_Attack_A_Ppyong", 0.8),
        (0.69, "SFX_Hit_B_Thwack", 0.9),
        (1.05, "SFX_Hurt_A_Thud", 1.0),
    ]),
    "SCENE_Combat_D_BouncyDry": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8),
        (0.09, "SFX_Hit_A_Pock", 0.9),
        (0.30, "SFX_Erpin_Attack_B_TongTong", 0.8),
        (0.39, "SFX_Hit_A_Pock", 0.9),
        (0.60, "SFX_Erpin_Attack_B_TongTong", 0.8),
        (0.69, "SFX_Hit_A_Pock", 0.9),
        (1.05, "SFX_Hurt_B_Wobble", 1.0),
    ]),
    # the chime cast kept, the other two roles swapped one at a time
    "SCENE_SkillLower_C_ABA": dict(gain=-4, parts=[
        (0.00, "SFX_SkillLower_Cast_A_Chime", 0.9),
        (0.02, "SFX_SkillLower_Shot_B_Tick", 0.7),
        (0.10, "SFX_SkillLower_Shot_B_Tick", 0.7),
        (0.18, "SFX_SkillLower_Shot_B_Tick", 0.7),
        (0.26, "SFX_SkillLower_Shot_B_Tick", 0.7),
        (0.44, "SFX_SkillLower_Burst_A_Soft", 0.9),
        (0.53, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.60, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.70, "SFX_SkillLower_Burst_A_Soft", 0.8),
    ]),
    "SCENE_SkillLower_D_BAB": dict(gain=-4, parts=[
        (0.00, "SFX_SkillLower_Cast_B_Whoosh", 0.9),
        (0.02, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.10, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.18, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.26, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.44, "SFX_SkillLower_Burst_B_Sparkle", 0.9),
        (0.53, "SFX_SkillLower_Burst_B_Sparkle", 0.8),
        (0.60, "SFX_SkillLower_Burst_B_Sparkle", 0.8),
        (0.70, "SFX_SkillLower_Burst_B_Sparkle", 0.8),
    ]),
    # the four shots alternate B, A, B, A, so the volley does not read as
    # the same sample fired four times
    "SCENE_SkillLower_E_AltShots": dict(gain=-4, parts=[
        (0.00, "SFX_SkillLower_Cast_A_Chime", 0.9),
        (0.02, "SFX_SkillLower_Shot_B_Tick", 0.75),
        (0.10, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.18, "SFX_SkillLower_Shot_B_Tick", 0.75),
        (0.26, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.44, "SFX_SkillLower_Burst_A_Soft", 0.9),
        (0.53, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.60, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.70, "SFX_SkillLower_Burst_A_Soft", 0.8),
    ]),
    # dash into an enemy: the collision ends the dash, so they overlap
    "SCENE_SkillUpper_A_Boom": dict(gain=-3, parts=[
        (0.00, "SFX_SkillUpper_Dash_A_Whoosh", 0.9),
        (0.30, "SFX_SkillUpper_Impact_A_Boom", 1.0),
    ]),
    "SCENE_SkillUpper_B_Slam": dict(gain=-3, parts=[
        (0.00, "SFX_SkillUpper_Dash_B_Charge", 0.9),
        (0.28, "SFX_SkillUpper_Impact_B_Slam", 1.0),
    ]),
    # the run lands its last step at 0.34, so the crack follows it
    "SCENE_SkillUpper_C_Run_Kwang": dict(gain=-3, parts=[
        (0.00, "SFX_SkillUpper_Dash_C_Run", 0.9),
        (0.40, "SFX_SkillUpper_Impact_C_Kwang", 1.0),
    ]),
    "SCENE_SkillUpper_D_Gust_Crash": dict(gain=-3, parts=[
        (0.00, "SFX_SkillUpper_Dash_D_Gust", 0.9),
        (0.40, "SFX_SkillUpper_Impact_D_Crash", 1.0),
    ]),
    "SCENE_SkillUpper_E_Run_Slam": dict(gain=-3, parts=[
        (0.00, "SFX_SkillUpper_Dash_C_Run", 0.9),
        (0.40, "SFX_SkillUpper_Impact_B_Slam", 1.0),
    ]),
    # The dash lasts until it hits something, up to ten seconds, so the
    # steps have to keep going. These take a 1.9s dash: launch whoosh,
    # steps the whole way, slam at the end.
    "SCENE_Dash_Boot": dict(gain=-3, parts=[
        (0.00, "SFX_SkillUpper_Dash_A_Whoosh", 0.75),
    ] + running("SFX_Step_Boot_L", "SFX_Step_Boot_R", 0.05, 1.9) + [
        (2.0, "SFX_SkillUpper_Impact_B_Slam", 1.0),
    ]),
    "SCENE_Dash_Stomp": dict(gain=-3, parts=[
        (0.00, "SFX_SkillUpper_Dash_A_Whoosh", 0.75),
    ] + running("SFX_Step_Stomp_L", "SFX_Step_Stomp_R", 0.05, 1.9) + [
        (2.0, "SFX_SkillUpper_Impact_B_Slam", 1.0),
    ]),
    "SCENE_Dash_Light": dict(gain=-3, parts=[
        (0.00, "SFX_SkillUpper_Dash_A_Whoosh", 0.75),
    ] + running("SFX_Step_Light_L", "SFX_Step_Light_R", 0.05, 1.9, cadence=0.14) + [
        (2.0, "SFX_SkillUpper_Impact_B_Slam", 1.0),
    ]),
    # The four blasts, each under the chosen attack sound at its real
    # cadence. This is the test that matters: an explosion on every hit
    # turns to mush easily.
    "SCENE_Burst_A_Pop": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.09, "SFX_AttackBurst_A_Pop", 0.9),
        (0.35, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.44, "SFX_AttackBurst_A_Pop", 0.9),
        (0.70, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.79, "SFX_AttackBurst_A_Pop", 0.9),
        (1.05, "SFX_Erpin_Attack_B_TongTong", 0.8), (1.14, "SFX_AttackBurst_A_Pop", 0.9),
    ]),
    "SCENE_Burst_B_Boom": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.09, "SFX_AttackBurst_B_Boom", 0.9),
        (0.35, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.44, "SFX_AttackBurst_B_Boom", 0.9),
        (0.70, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.79, "SFX_AttackBurst_B_Boom", 0.9),
        (1.05, "SFX_Erpin_Attack_B_TongTong", 0.8), (1.14, "SFX_AttackBurst_B_Boom", 0.9),
    ]),
    "SCENE_Burst_C_Crackle": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.09, "SFX_AttackBurst_C_Crackle", 0.9),
        (0.35, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.44, "SFX_AttackBurst_C_Crackle", 0.9),
        (0.70, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.79, "SFX_AttackBurst_C_Crackle", 0.9),
        (1.05, "SFX_Erpin_Attack_B_TongTong", 0.8), (1.14, "SFX_AttackBurst_C_Crackle", 0.9),
    ]),
    "SCENE_Burst_D_Puff": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.09, "SFX_AttackBurst_D_Puff", 0.9),
        (0.35, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.44, "SFX_AttackBurst_D_Puff", 0.9),
        (0.70, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.79, "SFX_AttackBurst_D_Puff", 0.9),
        (1.05, "SFX_Erpin_Attack_B_TongTong", 0.8), (1.14, "SFX_AttackBurst_D_Puff", 0.9),
    ]),
    # The blast and the tick sound together: attacking a poisoned enemy, so
    # the two must stay apart from each other.
    "SCENE_Burst_With_Tick": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.09, "SFX_AttackBurst_B_Boom", 0.9),
        (0.25, "SFX_Hit_E_Punch", 0.7),
        (0.35, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.44, "SFX_AttackBurst_B_Boom", 0.9),
        (0.75, "SFX_Hit_E_Punch", 0.7),
        (0.80, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.89, "SFX_AttackBurst_B_Boom", 0.9),
        (1.25, "SFX_Hit_E_Punch", 0.7),
        (1.75, "SFX_Hit_E_Punch", 0.7),
    ]),
    # The three new hit sounds, each under the chosen attack sound.
    "SCENE_Hit_E_Punch": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.09, "SFX_Hit_E_Punch", 0.9),
        (0.35, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.44, "SFX_Hit_E_Punch", 0.9),
        (0.70, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.79, "SFX_Hit_E_Punch", 0.9),
    ]),
    "SCENE_Hit_F_Smash": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.09, "SFX_Hit_F_Smash", 0.9),
        (0.35, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.44, "SFX_Hit_F_Smash", 0.9),
        (0.70, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.79, "SFX_Hit_F_Smash", 0.9),
    ]),
    "SCENE_Hit_G_Impact": dict(gain=-4, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.09, "SFX_Hit_G_Impact", 0.9),
        (0.35, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.44, "SFX_Hit_G_Impact", 0.9),
        (0.70, "SFX_Erpin_Attack_B_TongTong", 0.8), (0.79, "SFX_Hit_G_Impact", 0.9),
    ]),
    # The three new casts, each leading the full volley.
    "SCENE_Cast_C_Magic": dict(gain=-4, parts=[
        (0.00, "SFX_SkillLower_Cast_C_Magic", 1.0),
        (0.02, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.10, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.18, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.26, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.44, "SFX_SkillLower_Burst_A_Soft", 0.9),
        (0.53, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.60, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.70, "SFX_SkillLower_Burst_A_Soft", 0.8),
    ]),
    "SCENE_Cast_D_Sparkle": dict(gain=-4, parts=[
        (0.00, "SFX_SkillLower_Cast_D_Sparkle", 1.0),
        (0.02, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.10, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.18, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.26, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.44, "SFX_SkillLower_Burst_A_Soft", 0.9),
        (0.53, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.60, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.70, "SFX_SkillLower_Burst_A_Soft", 0.8),
    ]),
    "SCENE_Cast_E_Chant": dict(gain=-4, parts=[
        (0.00, "SFX_SkillLower_Cast_E_Chant", 1.0),
        (0.02, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.10, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.18, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.26, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (0.44, "SFX_SkillLower_Burst_A_Soft", 0.9),
        (0.53, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.60, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (0.70, "SFX_SkillLower_Burst_A_Soft", 0.8),
    ]),
    # the chosen set, in one take: bouncy attack, dry hit, wobble hurt,
    # the chime volley, then the run into a slam
    "SCENE_Chosen_Full": dict(gain=-3, parts=[
        (0.00, "SFX_Erpin_Attack_B_TongTong", 0.8),
        (0.09, "SFX_Hit_A_Pock", 0.9),
        (0.30, "SFX_Erpin_Attack_B_TongTong", 0.8),
        (0.39, "SFX_Hit_A_Pock", 0.9),
        (0.75, "SFX_Hurt_B_Wobble", 1.0),
        (1.25, "SFX_SkillLower_Cast_A_Chime", 0.9),
        (1.27, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (1.35, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (1.43, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (1.51, "SFX_SkillLower_Shot_A_Pew", 0.7),
        (1.69, "SFX_SkillLower_Burst_A_Soft", 0.9),
        (1.78, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (1.85, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (1.95, "SFX_SkillLower_Burst_A_Soft", 0.8),
        (2.45, "SFX_SkillUpper_Dash_C_Run", 0.9),
        (2.85, "SFX_SkillUpper_Impact_B_Slam", 1.0),
    ]),
}


# ---------------------------------------------------------------------
# The set chosen on 2026-10-19, as role name -> preset. The game refers to
# the role names only, so swapping a candidate later changes this table and
# nothing in the Unity code or scenes.
# ---------------------------------------------------------------------
CHOSEN = {
    "SFX_Attack_Basic": "SFX_Erpin_Attack_B_TongTong",
    # Erpin's basic attack lands as an explosion, so its hit is a blast.
    "SFX_Hit_Enemy": "SFX_AttackBurst_B_Boom",
    # The knock sound moved here: it is what a tick of poison, burn, aura
    # or spell damage sounds like, not what a basic attack sounds like.
    "SFX_Enemy_Tick": "SFX_Hit_E_Punch",
    "SFX_Player_Hurt": "SFX_Hurt_B_Wobble",
    "SFX_Skill_Lower_Cast": "SFX_SkillLower_Cast_D_Sparkle",
    "SFX_Skill_Lower_Shot": "SFX_SkillLower_Shot_A_Pew",
    "SFX_Skill_Lower_Burst": "SFX_SkillLower_Burst_A_Soft",
    # The dash cue is only the launch now. The run itself is the two step
    # clips, which the game repeats for as long as the dash lasts, so this
    # must not be the old clip that had footsteps baked into it.
    "SFX_Skill_Upper_Dash": "SFX_SkillUpper_Dash_A_Whoosh",
    "SFX_Skill_Upper_Step_Left": "SFX_Step_Stomp_L",
    "SFX_Skill_Upper_Step_Right": "SFX_Step_Stomp_R",
    "SFX_Skill_Upper_Impact": "SFX_SkillUpper_Impact_B_Slam",
}


def install(out_dir):
    """Write the chosen set under its role names, for Assets/Audio/SFX/."""
    lookup = all_presets()
    os.makedirs(out_dir, exist_ok=True)
    for role, preset in CHOSEN.items():
        if preset not in lookup:
            raise SystemExit(f"chosen preset is missing: {preset}")
        p = lookup[preset]
        s = finish(render_preset(p), p.get("gain", -4))
        path = os.path.join(out_dir, role + ".wav")
        wavfile.write(path, SR, s)
        print(f"{path}  <- {preset}  {len(s) / SR * 1000:.0f} ms")


def all_presets():
    merged = {}
    for presets in PRESET_GROUPS.values():
        merged.update(presets)
    return merged


def leveled(p):
    """Render a preset at the level its own 'gain' asks for, so a scene keeps
    the balance the game will actually have."""
    s = render_preset(p)
    peak = np.max(np.abs(s)) or 1.0
    return s / peak * (10 ** (p.get("gain", -4) / 20))


def render_scene(scene, lookup):
    parts = []
    for start, name, level in scene["parts"]:
        if name not in lookup:
            raise SystemExit(f"scene references unknown preset: {name}")
        parts.append((int(SR * start), leveled(lookup[name]) * level))
    total = max(o + len(s) for o, s in parts)
    out = np.zeros(total)
    for o, s in parts:
        out[o:o + len(s)] += s
    return out

# A preset gets a burst take only when the game can actually fire it again
# that fast; 'burst_gap' is how long the game leaves between two plays.
BURST_GAP = {
    "SFX_Erpin_Attack_A_Ppyong": 0.3,
    "SFX_Erpin_Attack_B_TongTong": 0.3,
    "SFX_Erpin_Attack_C_Ttorong": 0.3,
    "SFX_Erpin_Attack_D_Bubble": 0.3,
    "SFX_Erpin_Attack_E_Jelly": 0.3,
    "SFX_Erpin_Attack_F_RetroPop": 0.3,
    "SFX_Hit_A_Pock": 0.3,
    "SFX_Hit_B_Thwack": 0.3,
    "SFX_Hit_C_Pop": 0.3,
    "SFX_Hit_D_Crunch": 0.3,
    "SFX_Hit_E_Punch": 0.3,
    "SFX_AttackBurst_A_Pop": 0.3,
    "SFX_AttackBurst_B_Boom": 0.3,
    "SFX_AttackBurst_C_Crackle": 0.3,
    "SFX_AttackBurst_D_Puff": 0.3,
    "SFX_Hit_F_Smash": 0.3,
    "SFX_Hit_G_Impact": 0.3,
    # one foot lands every other step, so twice the cadence
    "SFX_Step_Boot_L": 0.34,
    "SFX_Step_Boot_R": 0.34,
    "SFX_Step_Stomp_L": 0.34,
    "SFX_Step_Stomp_R": 0.34,
    "SFX_Step_Light_L": 0.28,
    "SFX_Step_Light_R": 0.28,
    "SFX_Hurt_A_Thud": 0.7,
    "SFX_Hurt_B_Wobble": 0.7,
    "SFX_Hurt_C_Sting": 0.7,
    # the volley fires every 0.08s, and its four explosions land close together
    "SFX_SkillLower_Shot_A_Pew": 0.08,
    "SFX_SkillLower_Shot_B_Tick": 0.08,
    "SFX_SkillLower_Burst_A_Soft": 0.12,
    "SFX_SkillLower_Burst_B_Sparkle": 0.12,
    # the upper-grade skill is on a 20-40s cooldown, so it never stacks
}


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    flags = [a for a in argv if a.startswith("--")]
    out_root = args[0] if args else "out"
    groups = args[1:] or list(PRESET_GROUPS) + ["scenes"]
    burst_count = 0
    burst_gap = None
    for f in flags:
        if f.startswith("--burst-gap"):
            burst_gap = float(f.split("=", 1)[1])
        elif f.startswith("--burst"):
            burst_count = int(f.split("=", 1)[1]) if "=" in f else 5
        elif f.startswith("--install="):
            install(f.split("=", 1)[1])
            return

    for group in groups:
        if group == "scenes":
            out_dir = os.path.join(out_root, "scenes")
            os.makedirs(out_dir, exist_ok=True)
            lookup = all_presets()
            for name, scene in SCENES.items():
                s = render_scene(scene, lookup)
                path = os.path.join(out_dir, name + ".wav")
                wavfile.write(path, SR, finish(s, scene.get("gain", -4)))
                print(f"{path}  {len(s) / SR * 1000:.0f} ms")
            with open(os.path.join(out_dir, "scenes.json"), "w", encoding="utf-8") as fh:
                json.dump(SCENES, fh, indent=2, ensure_ascii=False)
            continue
        if group not in PRESET_GROUPS:
            raise SystemExit(
                f"unknown group: {group} (have {', '.join(PRESET_GROUPS)}, scenes)")
        presets = PRESET_GROUPS[group]
        out_dir = os.path.join(out_root, group)
        os.makedirs(out_dir, exist_ok=True)
        for name, p in presets.items():
            s = render_preset(p)
            path = os.path.join(out_dir, name + ".wav")
            wavfile.write(path, SR, finish(s, p.get("gain", -4)))
            print(f"{path}  {len(s) / SR * 1000:.0f} ms")
            gap = burst_gap if burst_gap is not None else BURST_GAP.get(name)
            if burst_count > 1 and gap:
                bdir = os.path.join(out_dir, "burst")
                os.makedirs(bdir, exist_ok=True)
                b = burst(s, burst_count, gap)
                bpath = os.path.join(bdir, f"{name}_burst{burst_count}.wav")
                wavfile.write(bpath, SR, finish(b, p.get("gain", -4)))
        with open(os.path.join(out_dir, "presets.json"), "w", encoding="utf-8") as fh:
            json.dump(presets, fh, indent=2, ensure_ascii=False)


if __name__ == "__main__":
    main(sys.argv[1:])
