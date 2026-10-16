"""Normalize diagnostics and verify every delivered video can be fully decoded."""
from pathlib import Path
import json
import subprocess

ROOT=Path(__file__).parent
REF=ROOT/'reference'
for filename in ['pulse-check.json','harmony-check.json','evidence-check.json','pulse-keyframes.json']:
    file=REF/filename
    raw=file.read_bytes()
    text=raw.decode('utf-16' if raw.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
    parsed=json.loads(text[text.index('{'):])
    file.write_text(json.dumps(parsed,ensure_ascii=False,indent=2),encoding='utf-8')

checks=[]
for name in ['pulse','harmony','evidence']:
    check=json.loads((REF/f'{name}-check.json').read_text(encoding='utf-8'))
    checks.append({'film':name,'passed':check['ok'],'lint':check['lint']['ok'],'runtime':check['runtime']['ok'],'layout':check['layout']['ok'],'contrast':check['contrast']['ok'],
                   'advisories':[f['code'] for f in check['lint']['findings'] if f['severity']=='warning']})
    assert check['ok'],f'{name} did not pass HyperFrames check'

decode=[]
loops=[]
for file in sorted((ROOT/'renders').iterdir()):
    if file.suffix not in ['.mp4','.webm']:continue
    result=subprocess.run(['ffmpeg','-v','error','-i',str(file),'-f','null','-'],capture_output=True)
    decode.append({'file':file.name,'passed':result.returncode==0,'errors':result.stderr.decode(errors='replace')})
    assert result.returncode==0,f'Failed decode: {file}'
    if '-master' in file.name:
        def pixels(time):
            return subprocess.check_output(['ffmpeg','-v','error','-ss',str(time),'-i',str(file),'-frames:v','1','-vf','scale=160:90','-pix_fmt','rgb24','-f','rawvideo','-'])
        first=pixels(0);last=pixels(15.966667)
        loops.append({'file':file.name,'loopSeamMeanDifferenceOutOf255':round(sum(abs(a-b) for a,b in zip(first,last))/len(first),4)})

changed=subprocess.check_output(['git','diff','--name-only','--','frontend','backend'],cwd=ROOT).decode().strip()
assert not changed,'Application source changed'
result={'hyperframes':checks,'fullVideoDecode':decode,'loops':loops,'applicationSourceUnchanged':not changed,'approval':'pending'}
(REF/'final-verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
(REF/'loop-check.json').write_text(json.dumps(loops,indent=2),encoding='utf-8')
print(json.dumps({'checksPassed':len(checks),'videosDecoded':len(decode),'applicationSourceUnchanged':not changed,'loopSeams':loops},indent=2))
