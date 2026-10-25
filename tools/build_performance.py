"""Compile locally aligned sung lines into a dense, inspectable action score."""
import argparse
import json
import re
from pathlib import Path


def choose(text, time, previous):
    s=text.lower()
    if re.search(r'\b(?:AM|PM)\b',text):return 'daynight','flip','orbit'
    rules=[
        (['power line'],'power','connect','grid'),(['protection'],'shield','seal','split'),
        (['lay down'],'assembly','assemble','grid'),(['object creation'],'cube','construct','orbit'),
        (['data parameter'],'data','fill','grid'),(['initialization'],'checks','scan','split'),
        (['new world'],'world','expand','orbit'),(['simulation'],'cage' if time>65 else 'world','cage' if time>65 else 'expand','split'),
        (['set of point'],'points','scatter','split'),(['dimension'],'dimension','extrude','orbit'),
        (['circle'],'circle','orbit','orbit'),(['circumference'],'circumference','unroll','split'),
        (['sine wave'],'sine','wave','split'),(['tangent'],'tangent','touch','split'),
        (['infinity'],'infinity','tunnel','orbit'),(['limitation'],'limit','clamp','split'),
        (['current','ac,','ac to','to ac'],'current','polarity','split'),(['blind'],'iris','close','solo'),
        (['dizzy'],'vortex','spin','orbit'),(['travel','ad,','a.d','bc','b.c'],'time','reverse','orbit'),
        (['unite','deeply'],'merge','magnet','split'),(['stimulation'],'stimulus','shock','orbit'),
        (['satisfaction'],'meter','fill','grid'),(['happy'],'happy','smile','grid'),
        (['eggplant','nutrient'],'eggplant','grow','grid'),(['tomato','antioxidant'],'fruit','grow','orbit'),
        (['tabby cat','enjoyment'],'cat','walk','grid'),(['only god','existence'],'eye','observe','orbit'),
        (['gender','to f','f, to m'],'identity','swap','split'),
        (['role','s, to m'],'roles','swap','split'),(['trance'],'vortex','spiral','orbit'),
        (['vibration'],'ecg','pulse','split'),(['completion'],'completion','join','grid'),
        (['you have left'],'disconnect','disconnect','split'),(['isolation'],'isolation','isolate','solo'),
        (['erase','fragment'],'fragments','erase','grid'),(['disheartened'],'broken','break','heart'),
        (['challenging','illegal','argument'],'error','reject','execute'),
        (['studied','properly'],'network','train','grid'),(['question'],'attention','attend','split'),
        (['algebraic'],'equation','solve','heart'),(['you are free'],'release','release','heart'),
        (['i am trapped','trapped in'],'cageheart','cage','heart'),
    ]
    if 'execution' in s:
        return ('final' if time>200 else 'lock' if 162<time<174 else 'execute','strike','execute')
    if re.fullmatch(r'(?i)(ein|dos|trois|trios|ne|fem|liu)[,. ]*',text):return 'countdown','step','split'
    if 'lo-o-ove' in s or text.strip().lower()=='love':
        return ('cageheart' if time>189 else 'equation' if time>184 else 'network','pulse','heart')
    for words,kind,action,layout in rules:
        if any(word in s for word in words):return kind,action,layout
    if 'trapped' in s:return 'cage','cage','split'
    return previous


def build(cache,root):
    aligned=json.loads((cache/'lyrics-aligned.json').read_text(encoding='utf-8'))
    rhythm=json.loads((cache/'rhythm.json').read_text(encoding='utf-8'))
    scenes=[];actions=[];previous=('power','connect','grid')
    for i,line in enumerate(aligned['lines']):
        if not line['text']:continue
        t=line['start'];end=line['end'];kind,action,layout=choose(line['text'],t,previous)
        previous=(kind,action,layout)
        emphasis=[w['text'].strip(',.!?;') for w in line['words'] if w['text'].isupper() and w['text']!='I']
        focus=' '.join(emphasis) if emphasis else ''
        scenes.append(dict(time=t,end=end,kind=kind,action=action,layout=layout,focus=focus,
                           label=f'{i+1:03} / {kind.upper()}',code=f'{kind}.{action}(t);',lyric=i,variant=i))
        for j,word in enumerate(line['words']):
            actions.append(dict(time=word['start'],end=word['end'],name=action,actor=(i+j)%6,
                                strength=1 if word['text'].isupper() else .45,word=word['text'],line=i))
    scenes.extend([
        dict(time=16.0,end=29.60,kind='title',action='rebuild',layout='orbit',focus='WORLD',label='INTERLUDE / REASSEMBLE',code='characters.reassemble();',lyric=-1,variant=130),
        dict(time=134.6,end=147.65,kind='storm',action='spiral',layout='orbit',focus='RECUR',label='INTERLUDE / RUNAWAY RECURSION',code='stack.overflow();',lyric=-1,variant=131),
        dict(time=195.9,end=205.65,kind='stall',action='stall',layout='grid',focus='99',label='OUTRO / WAITING FOR RETURN',code='await completion; // 99%',lyric=-1,variant=132),
    ])
    scenes.sort(key=lambda s:s['time'])
    if scenes[0]['time']>0:scenes.insert(0,dict(scenes[0],time=0))
    scenes[-1]['end']=211.906
    payload=dict(version=3,title='WORLD EX / NINE WINDOW LYRIC MACHINE',scenes=scenes,actions=sorted(actions,key=lambda x:x['time']))
    (cache/'performance-score.json').write_text(json.dumps(payload,separators=(',',':')),encoding='utf-8')
    report=dict(scenes=len(scenes),wordActions=len(actions),lyricLines=len(aligned['lines']),
                actualTransients=len(rhythm['events']),observedBeats=sum(not b['predicted'] for b in rhythm['beats']),
                sourceReference='https://github.com/yym8224961/world.execute-me-ascii',
                code='Original Windows renderer; reference used for visual direction and lyric alignment seed only.')
    (cache/'performance-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--cache',type=Path,required=True);p.add_argument('--root',type=Path,required=True)
    a=p.parse_args();build(a.cache,a.root)
