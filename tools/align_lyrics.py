"""Align reference line cues to local vocal-band attacks; estimate word sweeps.

No audio upload, speech model or equal-duration word subdivision. Word boundaries
are estimates constrained by syllable weights and measured vocal-band attacks.
The independently authored renderer does not vendor the reference project's code.
"""
import argparse
import json
import re
import urllib.request
import wave
from pathlib import Path

import numpy as np
from scipy import signal

REFERENCE='https://raw.githubusercontent.com/yym8224961/world.execute-me-ascii/main/lyrics.json'


def align(cache):
    seed=cache/'lyric-seed.json'
    if not seed.exists():
        seed.parent.mkdir(parents=True,exist_ok=True)
        with urllib.request.urlopen(REFERENCE,timeout=40) as response:
            seed.write_bytes(response.read())
    reference=json.loads(seed.read_text(encoding='utf-8'))
    with wave.open(str(cache/'music.wav'),'rb') as audio:
        stereo=np.frombuffer(audio.readframes(audio.getnframes()),dtype='<i2').reshape(-1,2).astype(np.float32)/32768
    center=signal.resample_poly(stereo.mean(axis=1),160,441)
    side=signal.resample_poly((stereo[:,0]-stereo[:,1])*.5,160,441)
    f,t,c=signal.stft(center,16000,nperseg=512,noverlap=384)
    _,_,s=signal.stft(side,16000,nperseg=512,noverlap=384)
    vocal=np.maximum(0,np.abs(c)-.65*np.abs(s))[(f>=180)&(f<=3600)]
    envelope=np.sqrt(np.mean(vocal**2,axis=0))
    attack=np.maximum(0,np.diff(np.log1p(envelope*300),prepend=0))
    attack=signal.savgol_filter(attack,5,2)
    def snap(value,margin):
        selected=np.where((t>=value-margin)&(t<=value+margin))[0]
        if not len(selected):return float(value)
        proximity=np.exp(-((t[selected]-value)/max(margin*.6,.015))**2)
        score=np.maximum(0,attack[selected])*proximity
        return float(t[selected[np.argmax(score)]]) if np.max(score)>.003 else float(value)
    lines=[]
    for item in reference:
        if item['en'].lower().startswith('world.execute'):continue
        start=max(0,snap(item['time'],.075))
        end=item['end']
        words=item['en'].split()
        weights=[]
        for word in words:
            groups=re.findall(r'[aeiouy]+',word.lower())
            weights.append(max(1,len(groups))+.20*min(8,len(word)))
        boundaries=np.r_[0,np.cumsum(weights)]/sum(weights)
        aligned=[]
        duration=max(.1,end-start)
        for j,word in enumerate(words):
            predicted=start+boundaries[j]*duration
            a=start if j==0 else snap(predicted,min(.09,duration/len(words)*.22))
            aligned.append(dict(text=word,start=round(a,3),end=0,confidence=None))
        for j,word in enumerate(aligned):word['end']=aligned[j+1]['start'] if j+1<len(aligned) else round(end,3)
        lines.append(dict(start=round(start,3),end=round(end,3),reference=item['time'],text=item['en'],words=aligned,
                          method='reference line cue + local vocal-band attack correction; estimated word sweep'))
    payload=dict(version=3,source='user-provided local audio',reference=REFERENCE,
                 accuracy='Line timing refined within 75ms of reference. Word times are onset-assisted estimates, not phonetic forced alignment.',lines=lines)
    (cache/'lyrics-aligned.json').write_text(json.dumps(payload,indent=2),encoding='utf-8')
    print(json.dumps(dict(lines=len(lines),words=sum(len(l['words']) for l in lines),method=payload['accuracy'])))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--cache',type=Path,required=True);align(p.parse_args().cache)
