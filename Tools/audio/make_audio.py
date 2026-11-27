# synthesises every ABYSS sound from scratch so there's nothing to license.
# soft and low on purpose, nothing sharp or loud
import numpy as np, wave, os, sys
SR = 44100
OUT = sys.argv[1]; os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(3)

def save(name, s, peak=0.5):
    s = s / (np.max(np.abs(s)) + 1e-9) * peak
    data = (np.clip(s, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())
    print("wrote", name, round(len(s) / SR, 2), "s")

def lowpass(s, a):
    y = np.empty_like(s); acc = 0.0
    for i, v in enumerate(s): acc += a * (v - acc); y[i] = acc
    return y

def env(n, a, r):
    t = np.arange(n) / SR
    return np.minimum(1, t / a) * np.exp(-t / r)

def loop_fade(s, secs=1.0):
    # crossfade tail into head so the loop is seamless
    k = int(secs * SR); f = np.linspace(0, 1, k)
    s[:k] = s[:k] * f + s[-k:] * (1 - f)
    return s[:-k]

# ambience: brown noise rumble + slow swells, 30s loop
n = SR * 31
brown = np.cumsum(rng.standard_normal(n)); brown -= lowpass(brown, 0.0005)
amb = lowpass(brown, 0.02)
t = np.arange(n) / SR
amb *= 0.7 + 0.3 * np.sin(2 * np.pi * t / 9.0) * np.sin(2 * np.pi * t / 13.0)
amb += 0.15 * np.sin(2 * np.pi * 41 * t) * (0.5 + 0.5 * np.sin(2 * np.pi * t / 7))
save("amb_underwater_loop", loop_fade(amb), 0.45)

# bubbles loop: sparse rising chirps
n = SR * 21; b = np.zeros(n)
for _ in range(60):
    st = rng.integers(0, n - SR); f0 = rng.uniform(500, 1400); d = rng.uniform(0.03, 0.09)
    k = int(d * SR); tt = np.arange(k) / SR
    chirp = np.sin(2 * np.pi * (f0 * tt + f0 * 2.5 * tt * tt / d)) * env(k, 0.003, d / 3)
    b[st:st + k] += chirp * rng.uniform(0.3, 1.0)
save("amb_bubbles_loop", loop_fade(lowpass(b, 0.35)), 0.3)

def tone(freqs, dur, a=0.01, r=0.3, harm=0.25, peak=0.5):
    k = int(dur * SR); tt = np.arange(k) / SR; s = np.zeros(k)
    for f in freqs:
        s += np.sin(2 * np.pi * f * tt) + harm * np.sin(2 * np.pi * f * 2 * tt) * np.exp(-tt / (r * 0.5))
    return s * env(k, a, r)

save("ui_hover", lowpass(tone([880], 0.08, 0.004, 0.025), 0.3), 0.18)
save("ui_click", lowpass(tone([660, 990], 0.12, 0.003, 0.04), 0.4), 0.3)
save("ui_menu_open", tone([392, 587], 0.35, 0.02, 0.12), 0.28)
save("int_select", tone([523.25, 784], 0.3, 0.005, 0.09), 0.35)
save("int_fail", lowpass(np.concatenate([tone([300], 0.09, 0.005, 0.04), np.zeros(int(0.03 * SR)), tone([260], 0.12, 0.005, 0.05)]), 0.2), 0.3)
save("int_discover", tone([587.3, 880, 1174.7], 0.9, 0.01, 0.35, 0.15), 0.38)
save("int_step_complete", np.concatenate([tone([523.25], 0.18, 0.005, 0.08), tone([659.25, 783.99], 0.6, 0.005, 0.25)]), 0.38)
save("int_activity_complete", np.concatenate([tone([392], 0.22, 0.01, 0.1), tone([523.25], 0.22, 0.01, 0.1), tone([659.25, 783.99, 1046.5], 1.4, 0.01, 0.55, 0.1)]), 0.42)
w = rng.standard_normal(int(0.5 * SR)); save("nav_teleport", lowpass(w, 0.04) * env(len(w), 0.08, 0.15), 0.25)
