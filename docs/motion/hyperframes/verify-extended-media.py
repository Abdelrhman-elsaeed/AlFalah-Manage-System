"""Verify the final film and its actual decoded frames, not only its HTML."""
from pathlib import Path
import json
import subprocess
import hashlib

ROOT=Path(__file__).parent
REF=ROOT/'reference'
raw=(REF/'extended-check.json').read_bytes()
check=json.loads(raw.decode('utf-16' if raw.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig'))
(REF/'extended-check.json').write_text(json.dumps(check,ensure_ascii=False,indent=2),encoding='utf-8')
assert check['ok']
master=ROOT/'renders'/'04-day-in-harmony-extended-master.mp4'
def pixels(t):
    return subprocess.check_output(['ffmpeg','-v','error','-ss',str(t),'-i',str(master),'-frames:v','1','-vf','scale=160:90','-pix_fmt','rgb24','-f','rawvideo','-'])
first=pixels(0);last=pixels(49.966667)
seam=sum(abs(a-b) for a,b in zip(first,last))/len(first)
transition_times=[]
for start in [5,13/1.08,22/1.08,31/1.08,40/1.08,49/1.08]:
    for offset in [-1/30,0,.255,.51,.765,1.02,1.055]:
        t=start+offset
        frame=pixels(t)
        transition_times.append({'time':round(t,5),'meanBrightness':round(sum(frame)/len(frame),3),'blackPixelFraction':round(sum(max(frame[i:i+3])<8 for i in range(0,len(frame),3))/(len(frame)/3),5)})
assert all(sample['meanBrightness']>20 and sample['blackPixelFraction']<.05 for sample in transition_times)
source=(ROOT/'harmony-extended'/'index.html').read_text(encoding='utf-8')
assert 'timetable.png' not in source and 'dashboard.png' not in source
assert 'data-duration="50"' in source
changed=subprocess.check_output(['git','diff','--name-only','--','frontend','backend'],cwd=ROOT).decode().strip()
assert not changed
report={'hyperframesPassed':check['ok'],'runtimePassed':check['runtime']['ok'],'layoutPassed':check['layout']['ok'],'contrastPassed':check['contrast']['ok'],'durationSeconds':50,'fps':30,'maskTransitionSamples':transition_times,'blackFramesFound':False,'loopSeamMeanRgbDifferenceOutOf255':round(seam,5),'removedTimetableScreenshot':True,'applicationSourceUnchanged':not changed,'masterSha256':hashlib.sha256(master.read_bytes()).hexdigest(),'approval':'pending','advisories':[f['code'] for f in check['lint']['findings'] if f['severity']=='warning']}
(REF/'extended-final-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='maskTransitionSamples'},indent=2))
