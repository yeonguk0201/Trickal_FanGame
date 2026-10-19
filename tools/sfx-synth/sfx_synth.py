"""
Tiny sfxr-style SFX synthesizer for TrickalFanGame.

This module is the engine only: it turns a parameter dict (like an sfxr
preset) into samples. The sounds themselves live in render_sfx.py, which is
also the command line entry point.

Single-layer parameters (all optional):
  wave        'sine' | 'triangle' | 'square' | 'saw' | 'noise'
  duty        square duty cycle (0.05..0.5)
  freq        start frequency Hz
  slide       pitch slide in octaves per second (+ = up)
  dslide      change of slide per second (curves the sweep)
  freq_min/freq_max  clamp
  attack/sustain/decay  envelope seconds (decay is exponential-ish)
  decay_pow   decay curve exponent (default 2.2; lower = longer tail)
  punch       extra volume boost at sustain start (0..1)
  arp         list of (time_s, freq_multiplier) jumps
  vib_depth / vib_speed   vibrato (fraction, Hz)
  lpf         one-pole low-pass cutoff Hz (softens edges)
  lpf_end     sweep the low-pass to this cutoff by the end of the sound
  hpf         one-pole high-pass cutoff Hz (removes mud)
  bits        bit-crush depth (e.g. 6) for a retro grain
  echo        list of (delay_s, gain) repeats -> "bouncy" double hits
  echo_pitch  pitch multiplier of the echo copies
  seed        noise seed (give layers different seeds)

Layered sounds:
  layers      list of single-layer dicts, each with optional
              'delay' (seconds before it starts) and 'mix' (relative level).
              Impacts need this: a noise crack and a low body behave
              differently and have to be dialled in separately.
  gain        peak level in dBFS after normalisation (whole sound)
"""
import sys, os, json
import numpy as np
from scipy.io import wavfile

SR = 44100


def render(p):
    attack = p.get("attack", 0.002)
    sustain = p.get("sustain", 0.02)
    decay = p.get("decay", 0.08)
    dur = attack + sustain + decay
    n = int(SR * dur)
    t = np.arange(n) / SR

    # --- pitch curve -------------------------------------------------
    slide = p.get("slide", 0.0) + p.get("dslide", 0.0) * t
    octaves = np.cumsum(slide) / SR
    freq = p.get("freq", 800.0) * (2.0 ** octaves)
    for at, mult in p.get("arp", []):
        freq = np.where(t >= at, freq * mult, freq)
    if p.get("vib_depth"):
        freq = freq * (1 + p["vib_depth"] * np.sin(2 * np.pi * p.get("vib_speed", 20) * t))
    freq = np.clip(freq, p.get("freq_min", 30), p.get("freq_max", 12000))
    phase = np.cumsum(freq) / SR  # cycles
    ph = phase % 1.0

    # --- oscillator --------------------------------------------------
    w = p.get("wave", "sine")
    if w == "sine":
        s = np.sin(2 * np.pi * phase)
    elif w == "triangle":
        s = 1 - 4 * np.abs(ph - 0.5)
    elif w == "square":
        s = np.where(ph < p.get("duty", 0.5), 1.0, -1.0)
    elif w == "saw":
        s = 2 * ph - 1
    else:
        rng = np.random.default_rng(p.get("seed", 1))
        s = rng.uniform(-1, 1, n)

    # --- envelope ----------------------------------------------------
    env = np.zeros(n)
    a = int(SR * attack); su = int(SR * sustain)
    env[:a] = np.linspace(0, 1, a, endpoint=False) if a else []
    punch = p.get("punch", 0.0)
    env[a:a + su] = 1 + punch * np.linspace(1, 0, su)
    rest = n - a - su
    if rest > 0:
        k = np.linspace(0, 1, rest)
        env[a + su:] = (1 - k) ** p.get("decay_pow", 2.2)
    s = s * env

    # --- filters -----------------------------------------------------
    if p.get("lpf"):
        cut = np.linspace(p["lpf"], p.get("lpf_end", p["lpf"]), n)
        alpha = 1 - np.exp(-2 * np.pi * np.clip(cut, 20, SR / 2.2) / SR)
        y = np.zeros_like(s); acc = 0.0
        for i, x in enumerate(s):
            acc += alpha[i] * (x - acc); y[i] = acc
        s = y
    if p.get("hpf"):
        rc = 1 / (2 * np.pi * p["hpf"]); dt = 1 / SR; al = rc / (rc + dt)
        y = np.zeros_like(s); prev_x = 0.0; prev_y = 0.0
        for i, x in enumerate(s):
            prev_y = al * (prev_y + x - prev_x); prev_x = x; y[i] = prev_y
        s = y
    if p.get("bits"):
        q = 2 ** (p["bits"] - 1)
        s = np.round(s * q) / q

    # --- echo (bounce) ----------------------------------------------
    echoes = p.get("echo", [])
    if echoes:
        total = n + int(SR * max(d for d, _ in echoes))
        out = np.zeros(total); out[:n] += s
        for d, g in echoes:
            o = int(SR * d)
            src = s
            if p.get("echo_pitch"):  # re-render a pitched copy for the bounce
                q = dict(p); q.pop("echo", None); q.pop("echo_pitch", None)
                q["freq"] = p.get("freq", 800) * p["echo_pitch"]
                src = render(q)
            out[o:o + len(src)] += g * src[: total - o]
        s = out

    return fade_tail(s)


def fade_tail(s, seconds=0.004):
    f = min(int(SR * seconds), len(s))
    if f:
        s[-f:] = s[-f:] * np.linspace(1, 0, f)
    return s


def render_preset(p):
    """Render one preset, mixing its layers when it has any."""
    if "layers" not in p:
        return render(p)
    parts = []
    for layer in p["layers"]:
        s = render(layer) * layer.get("mix", 1.0)
        parts.append((int(SR * layer.get("delay", 0.0)), s))
    total = max(o + len(s) for o, s in parts)
    out = np.zeros(total)
    for o, s in parts:
        out[o:o + len(s)] += s
    return fade_tail(out)


def burst(s, count, gap):
    """Repeat a rendered sound to hear it the way the game fires it."""
    step = int(SR * gap)
    total = step * (count - 1) + len(s)
    out = np.zeros(total)
    for i in range(count):
        o = step * i
        out[o:o + len(s)] += s
    return fade_tail(out)


def finish(s, gain_db):
    peak = np.max(np.abs(s)) or 1.0
    s = s / peak * (10 ** (gain_db / 20))
    return np.int16(np.clip(s, -1, 1) * 32767)
