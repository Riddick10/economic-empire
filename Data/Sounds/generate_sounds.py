import numpy as np
import wave
import os

SAMPLE_RATE = 44100
OUTPUT_DIR = os.path.dirname(os.path.abspath(__file__))


def write_wav(filename, samples):
    samples = np.clip(samples, -1.0, 1.0)
    int_samples = (samples * 32767).astype(np.int16)
    filepath = os.path.join(OUTPUT_DIR, filename)
    with wave.open(filepath, 'w') as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(int_samples.tobytes())
    print(f"  {filename}")


def sine(freq, duration, phase=0):
    t = np.linspace(0, duration, int(SAMPLE_RATE * duration), endpoint=False)
    return np.sin(2 * np.pi * freq * t + phase)


def noise(duration):
    return np.random.uniform(-1, 1, int(SAMPLE_RATE * duration))


def envelope(length, attack=0.005, decay=None):
    if decay is None:
        decay = length / SAMPLE_RATE
    env = np.ones(length)
    attack_samples = int(SAMPLE_RATE * attack)
    decay_samples = int(SAMPLE_RATE * decay)
    if attack_samples > 0:
        env[:attack_samples] = np.linspace(0, 1, attack_samples)
    if decay_samples > 0 and decay_samples <= length:
        env[-decay_samples:] = np.linspace(1, 0, decay_samples)
    return env


def generate_click():
    duration = 0.05
    s = sine(800, duration) * 0.7 + sine(1600, duration) * 0.3
    env = np.exp(-np.linspace(0, 8, int(SAMPLE_RATE * duration)))
    write_wav("click.wav", s * env * 0.8)


def generate_build():
    duration = 0.35
    n = int(SAMPLE_RATE * duration)
    # Low thump
    thump = sine(120, duration) * np.exp(-np.linspace(0, 6, n)) * 0.6
    # Mid impact
    mid = sine(280, duration) * np.exp(-np.linspace(0, 10, n)) * 0.3
    # Noise burst
    nz = noise(duration) * np.exp(-np.linspace(0, 12, n)) * 0.2
    write_wav("build.wav", thump + mid + nz)


def generate_notification_info():
    dur1 = 0.12
    dur2 = 0.13
    tone1 = sine(523, dur1) * envelope(int(SAMPLE_RATE * dur1), 0.005, 0.04)
    tone2 = sine(659, dur2) * envelope(int(SAMPLE_RATE * dur2), 0.005, 0.06)
    write_wav("notification_info.wav", np.concatenate([tone1, tone2]) * 0.6)


def generate_notification_warning():
    duration = 0.3
    n = int(SAMPLE_RATE * duration)
    t = np.linspace(0, duration, n, endpoint=False)
    vibrato = np.sin(2 * np.pi * 8 * t) * 15
    s = np.sin(2 * np.pi * (440 + vibrato) * t)
    env = envelope(n, 0.01, 0.08)
    write_wav("notification_warning.wav", s * env * 0.6)


def generate_notification_danger():
    duration = 0.4
    n = int(SAMPLE_RATE * duration)
    t = np.linspace(0, duration, n, endpoint=False)
    # Alternating tones at 10Hz
    alt = np.where(np.sin(2 * np.pi * 10 * t) > 0, 1, 0)
    tone1 = np.sin(2 * np.pi * 400 * t) * alt
    tone2 = np.sin(2 * np.pi * 500 * t) * (1 - alt)
    env = envelope(n, 0.01, 0.05)
    write_wav("notification_danger.wav", (tone1 + tone2) * env * 0.6)


def generate_notification_success():
    tones = []
    for freq in [523, 659, 784]:
        dur = 0.08
        n = int(SAMPLE_RATE * dur)
        tone = sine(freq, dur) * envelope(n, 0.003, 0.02)
        tones.append(tone)
    write_wav("notification_success.wav", np.concatenate(tones) * 0.6)


def generate_pause():
    duration = 0.2
    n = int(SAMPLE_RATE * duration)
    t = np.linspace(0, duration, n, endpoint=False)
    freq = np.linspace(600, 300, n)
    s = np.sin(2 * np.pi * np.cumsum(freq) / SAMPLE_RATE)
    env = envelope(n, 0.005, 0.05)
    write_wav("pause.wav", s * env * 0.6)


def generate_unpause():
    duration = 0.2
    n = int(SAMPLE_RATE * duration)
    t = np.linspace(0, duration, n, endpoint=False)
    freq = np.linspace(300, 600, n)
    s = np.sin(2 * np.pi * np.cumsum(freq) / SAMPLE_RATE)
    env = envelope(n, 0.005, 0.05)
    write_wav("unpause.wav", s * env * 0.6)


def generate_speed_change():
    duration = 0.08
    n = int(SAMPLE_RATE * duration)
    s = sine(1200, duration) * 0.7 + sine(1800, duration) * 0.3
    env = np.exp(-np.linspace(0, 10, n))
    write_wav("speed_change.wav", s[:n] * env * 0.7)


def generate_coin():
    duration = 0.15
    n = int(SAMPLE_RATE * duration)
    s = sine(2400, duration) * 0.5 + sine(3600, duration) * 0.3 + sine(4800, duration) * 0.2
    env = np.exp(-np.linspace(0, 8, n))
    write_wav("coin.wav", s[:n] * env * 0.7)


if __name__ == "__main__":
    print("Generating sounds...")
    generate_click()
    generate_build()
    generate_notification_info()
    generate_notification_warning()
    generate_notification_danger()
    generate_notification_success()
    generate_pause()
    generate_unpause()
    generate_speed_change()
    generate_coin()
    print("Done! 10 sounds generated.")
