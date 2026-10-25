"""Decode local audio and extract beat/spectral features. No network requests."""
import argparse
import hashlib
import json
import subprocess
import wave
from pathlib import Path

import numpy as np
from scipy import signal


def analyze(source: Path, output: Path):
    output.mkdir(parents=True, exist_ok=True)
    fingerprint = hashlib.sha256(source.read_bytes()).hexdigest()
    summary_path = output / 'audio-analysis.json'
    if summary_path.exists():
        saved = json.loads(summary_path.read_text(encoding='utf-8'))
        if saved.get('version') == 2 and saved.get('sha256') == fingerprint and all((output / n).exists() for n in ('music.wav', 'features.bin')):
            print(json.dumps(saved))
            return
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
                    '-ar', '44100', '-ac', '2', '-c:a', 'pcm_s16le', str(output / 'music.wav')], check=True)
    with wave.open(str(output / 'music.wav'), 'rb') as audio:
        stereo = np.frombuffer(audio.readframes(audio.getnframes()), dtype='<i2').reshape(-1, 2)
    mono = stereo.astype(np.float32).mean(axis=1)[::2] / 32768
    sr, hop, nfft = 22050, 256, 2048
    frequencies, times, spectrum = signal.stft(mono, sr, nperseg=nfft, noverlap=nfft-hop, boundary='zeros')
    magnitude = np.abs(spectrum)
    flux = np.maximum(0, np.diff(np.log1p(magnitude * 100), axis=1)).mean(axis=0)
    flux = np.r_[0, flux]
    flux = np.maximum(0, flux - signal.medfilt(flux, 43))
    autocorr = signal.fftconvolve(flux, flux[::-1], mode='full')[len(flux)-1:]
    candidates = np.arange(int(sr/hop*60/180), int(sr/hop*60/75))
    lag = int(candidates[np.argmax(autocorr[candidates])])
    # Refine the lag with a parabolic fit around the correlation maximum.
    a,b,c = autocorr[lag-1:lag+2]
    refined = lag + float(0.5*(a-c)/(a-2*b+c))
    bpm = 60*sr/hop/refined
    # A full-track Fourier fit prevents the short-lag estimate drifting by beats.
    tempo_grid = np.arange(bpm-2, bpm+2, .002)
    periodicity = [abs(np.dot(flux, np.exp(-2j*np.pi*(candidate/60)*times))) for candidate in tempo_grid]
    bpm = float(tempo_grid[np.argmax(periodicity)])
    period = 60/bpm
    # Fit first beat phase to the measured onset envelope across the track.
    phases = np.arange(0, period, .002)
    scores = [np.interp(np.arange(p, len(mono)/sr, period), times, flux).sum() for p in phases]
    phase = float(phases[np.argmax(scores)])
    fps = 30
    frame_times = np.arange(0, len(mono)/sr, 1/fps)
    edges = np.geomspace(40, 10500, 17)
    bands = []
    for low, high in zip(edges[:-1], edges[1:]):
        values = np.sqrt(np.mean(magnitude[(frequencies >= low) & (frequencies < high)]**2, axis=0))
        values = np.log1p(values*200)
        values /= max(float(np.percentile(values, 97)), 1e-6)
        bands.append(np.interp(frame_times, times, np.clip(values, 0, 1)))
    bands = np.array(bands).T
    onset = np.clip(flux/max(float(np.percentile(flux, 97)), 1e-6), 0, 1)
    features = np.column_stack([bands[:,:5].mean(axis=1), bands[:,5:11].mean(axis=1), bands[:,11:].mean(axis=1),
                                np.interp(frame_times, times, onset), bands]).astype('<f4')
    with (output / 'features.bin').open('wb') as file:
        file.write(np.array([fps, len(features), features.shape[1]], dtype='<i4').tobytes())
        file.write(features.tobytes())
    summary = dict(version=2, sha256=fingerprint, duration=len(mono)/sr, bpm=round(bpm, 3), beatOffset=phase,
                   featureFps=fps, featureFrames=len(features), sampleRate=44100,
                   method='spectral flux autocorrelation + global beat phase fit',
                   note='Beat grid is audio-derived; semantic scene cues are editorial, not forced lyric alignment.')
    summary_path.write_text(json.dumps(summary, indent=2), encoding='utf-8')
    print(json.dumps(summary))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('--output', type=Path, default=Path('.runtime'))
    args = parser.parse_args()
    analyze(args.source, args.output)
