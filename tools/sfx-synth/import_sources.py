"""
Brings externally sourced audio into the game's asset paths.

    python import_sources.py <source_root> <game_assets_dir>

<source_root> is the folder holding the downloaded files (the MANIFEST paths
are relative to it); <game_assets_dir> is `game/Assets/Audio`. Every entry is
decoded, resampled to 44.1kHz, trimmed of silence, levelled, and written to
the role path the game refers to — so swapping a sound later means editing
this manifest and running it again, never touching Unity code.

Why each step is here:
  resample  the sources arrive at 44.1k, 48k and 96k, and two files cannot be
            mixed until they share a rate
  trim      several sources start late (a shot sound 0.22s late feels laggy)
            and one pair carries 6.4s of trailing silence
  level     a few mp3 decodes peak above 1.0 and would clip on write
  mix       where one moment is made of two sources played together
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

TARGET_RATE = 44100
# Silence floor for trimming, relative to the file's own peak. -40 dB and
# not lower: two of the sources carry a reverb bed sitting at about -50 dB
# for six seconds after the sound itself has finished, and a floor below
# that keeps all of it.
TRIM_FLOOR_DB = -40.0
FADE_SECONDS = 0.01
# Kept after the last audible sample, so a decaying tail is not cut square.
RELEASE_SECONDS = 0.06
# libsndfile overflows its stack encoding a long Vorbis file in one call, so
# the BGM is written a block at a time.
WRITE_BLOCK = 1 << 16

# role -> what it is made of. `sources` holds (path, level) pairs; more than
# one means they play as one sound. `peak_db` is the level written to disk;
# the per-cue mix lives in CombatSfxSetup, not here.
MANIFEST = [
    # Erpin's basic attack: the fire spell with the impact layered on top.
    dict(role="SFX/SFX_Attack_Basic.wav", peak_db=-1.0, sources=[
        ("에르핀 기본공격/396500__alonsotm__firespell3.wav", 1.0),
        ("에르핀 기본공격/541478__eminyildirim__magic-fire-spell-impact-punch.wav", 0.9),
    ]),
    dict(role="SFX/SFX_Hit_Enemy.wav", peak_db=-1.0, sources=[
        ("타격/sfx_exp_short_soft1.wav", 1.0),
    ]),
    dict(role="SFX/SFX_Hit_Critical.wav", peak_db=-1.0, sources=[
        ("타격/sfx_exp_short_hard9.wav", 1.0),
    ]),
    # The lower-grade cast: the spell cast with the missile volley over it.
    # The volley sample is 2.9s of several missiles, so it belongs to the
    # whole cast rather than to one shot.
    dict(role="SFX/SFX_Skill_Lower_Cast.wav", peak_db=-1.0, sources=[
        ("에르핀 저학년/daviddumaisaudio-magical-spell-cast-190272.mp3", 1.0),
        ("에르핀 저학년/249817__spookymodem__magic-missiles.wav", 0.85),
    ]),
    dict(role="SFX/SFX_Boss_Portal.wav", peak_db=-1.0, sources=[
        ("보스룸 이동/563542__colorscrimsontears__teleport-rpg.wav", 1.0),
    ]),
    # Bombs and exploding boxes: both sources together, as one blast.
    dict(role="SFX/SFX_Bomb_Explosion.wav", peak_db=-1.0, sources=[
        ("폭발/dragon-studio-distant-bang-472364.mp3", 1.0),
        ("폭발/563536__colorscrimsontears__fire-3-rpg.wav", 0.9),
    ]),
    dict(role="SFX/SFX_Muffin_Explosion.wav", peak_db=-1.0, sources=[
        ("폭발/dragon-studio-boom-copyright-free-487662.mp3", 1.0),
    ]),
    # Both spell kinds go through the same slot, so they are told apart by
    # the item's kind rather than by two separate systems.
    dict(role="SFX/SFX_Spell_Cast.wav", peak_db=-1.0, sources=[
        ("스펠/632336__igroglaz__magic-spell-06.wav", 1.0),
    ]),
    dict(role="SFX/SFX_Jjangsem_Spell_Cast.wav", peak_db=-1.0, sources=[
        ("스펠/659762__lilmati__magic-spell-05-remastered.wav", 1.0),
    ]),
    dict(role="SFX/SFX_SP_Gain.wav", peak_db=-1.0, sources=[
        ("sp업/yodguard-healing-magic-2-378663.mp3", 1.0),
    ]),
    dict(role="SFX/SFX_Heal.wav", peak_db=-1.0, sources=[
        ("체력힐/yodguard-healing-magic-1-378665.mp3", 1.0),
    ]),
    dict(role="SFX/SFX_Door_Open.wav", peak_db=-1.0, sources=[
        ("문 열림/yodguard-locked-door-4-540180.mp3", 1.0),
    ]),
    dict(role="SFX/SFX_Item_Drop.wav", peak_db=-1.0, sources=[
        ("아이템 드랍/universfield-wine-cork-pop-352295.mp3", 1.0),
    ]),
    # Combat BGM. It loops, so its head and tail are left exactly as they
    # are: trimming either one would put a seam in the loop.
    dict(role="BGM/BGM_Combat.ogg", peak_db=-1.5, trim=False, sources=[
        ("브금/openmindaudio-cartoon-quirky-christmas-gift-unwrapping-620480.mp3", 1.0),
    ]),
]


def load(path):
    data, rate = sf.read(path, always_2d=True, dtype="float64")
    if rate != TARGET_RATE:
        from math import gcd
        g = gcd(int(rate), TARGET_RATE)
        data = resample_poly(data, TARGET_RATE // g, int(rate) // g, axis=0)
    return data


def trim(data):
    """Drop silence at both ends so the sound starts the moment it is asked
    for, and does not hold the voice open afterwards."""
    level = np.abs(data).max(axis=1)
    peak = level.max()
    if peak <= 0:
        return data
    above = np.nonzero(level > peak * 10 ** (TRIM_FLOOR_DB / 20))[0]
    if not len(above):
        return data
    end = min(len(data), above[-1] + 1 + int(TARGET_RATE * RELEASE_SECONDS))
    return data[above[0]:end]


def fade_edges(data):
    n = min(int(TARGET_RATE * FADE_SECONDS), len(data) // 2)
    if n <= 0:
        return data
    ramp = np.linspace(0.0, 1.0, n)[:, None]
    data[:n] *= ramp
    data[-n:] *= ramp[::-1]
    return data


def build(entry, source_root):
    layers = []
    for rel, level in entry["sources"]:
        path = os.path.join(source_root, rel.replace("/", os.sep))
        if not os.path.isfile(path):
            raise SystemExit(f"source is missing: {path}")
        data = load(path)
        if entry.get("trim", True):
            data = trim(data)
        layers.append(data * level)

    width = max(layer.shape[1] for layer in layers)
    length = max(len(layer) for layer in layers)
    out = np.zeros((length, width))
    for layer in layers:
        if layer.shape[1] < width:  # mono source under a stereo one
            layer = np.repeat(layer, width, axis=1)
        out[:len(layer)] += layer

    if entry.get("trim", True):
        out = fade_edges(out)
    peak = np.abs(out).max() or 1.0
    return out / peak * (10 ** (entry["peak_db"] / 20))


def main(argv):
    if len(argv) < 2:
        raise SystemExit("usage: import_sources.py <source_root> <game_assets_dir>")
    source_root, assets = argv[0], argv[1]
    for entry in MANIFEST:
        data = build(entry, source_root)
        out_path = os.path.join(assets, entry["role"].replace("/", os.sep))
        os.makedirs(os.path.dirname(out_path), exist_ok=True)
        if out_path.lower().endswith(".ogg"):
            with sf.SoundFile(out_path, "w", samplerate=TARGET_RATE,
                              channels=data.shape[1], format="OGG",
                              subtype="VORBIS") as handle:
                for start in range(0, len(data), WRITE_BLOCK):
                    handle.write(data[start:start + WRITE_BLOCK])
        else:
            sf.write(out_path, data, TARGET_RATE, subtype="PCM_16")
        names = ", ".join(os.path.basename(rel) for rel, _ in entry["sources"])
        print(f"{entry['role']:34} {len(data) / TARGET_RATE:6.2f}s  <- {names}")


if __name__ == "__main__":
    main(sys.argv[1:])
