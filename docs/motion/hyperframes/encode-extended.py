"""Web delivery for the selected film only. Historical studies remain outside review."""
from pathlib import Path
import json
import subprocess
import hashlib

ROOT=Path(__file__).parent
OUT=ROOT/'renders'
STEM='04-day-in-harmony-extended'
MASTER=OUT/f'{STEM}-master.mp4'

def run(args):
    result=subprocess.run(args,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
    if result.returncode:
        raise RuntimeError(result.stderr.decode(errors='replace'))

def probe(file):
    return json.loads(subprocess.check_output(['ffprobe','-v','error','-show_format','-show_streams','-of','json',str(file)]))

base=['ffmpeg','-y','-v','error','-i',str(MASTER),'-an']
run(base+['-vf','scale=1280:720:flags=lanczos','-c:v','libx264','-preset','slow','-crf','21','-pix_fmt','yuv420p','-movflags','+faststart',str(OUT/f'{STEM}.mp4')])
print('MP4 complete',flush=True)
run(base+['-vf','scale=1280:720:flags=lanczos','-c:v','libvpx-vp9','-crf','32','-b:v','0','-cpu-used','3','-row-mt','1','-threads','4','-pix_fmt','yuv420p',str(OUT/f'{STEM}.webm')])
print('WebM complete',flush=True)
run(base+['-vf','scale=640:360:flags=lanczos','-c:v','libx264','-preset','slow','-crf','22','-pix_fmt','yuv420p','-movflags','+faststart',str(OUT/f'{STEM}-mobile.mp4')])
print('Mobile MP4 complete',flush=True)
run(['ffmpeg','-y','-v','error','-ss','1.5','-i',str(MASTER),'-frames:v','1','-vf','scale=1280:720:flags=lanczos','-c:v','libwebp','-quality','90',str(OUT/f'{STEM}-poster.webp')])
qc=ROOT/'reference'/'extended-decoded';qc.mkdir(exist_ok=True)
for time in [0,5.4,9,12.5,18,21,26,29.2,34,38,43,46,49.966667]:
    run(['ffmpeg','-y','-v','error','-ss',str(time),'-i',str(MASTER),'-frames:v','1',str(qc/f'at-{time:g}.png')])
files=[]
for f in sorted(OUT.glob(f'{STEM}*')):
    if f.suffix not in ['.mp4','.webm','.webp']:continue
    record={'file':f.name,'bytes':f.stat().st_size,'megabytes':round(f.stat().st_size/1_000_000,3),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()}
    if f.suffix!='.webp':
        info=probe(f);stream=next(s for s in info['streams'] if s['codec_type']=='video')
        record.update(width=stream['width'],height=stream['height'],codec=stream['codec_name'],pixelFormat=stream['pix_fmt'],fps=stream['r_frame_rate'],duration=float(info['format']['duration']),audioStreams=sum(s['codec_type']=='audio' for s in info['streams']))
        assert record['duration']==50 and record['fps']=='30/1' and record['audioStreams']==0
        run(['ffmpeg','-v','error','-i',str(f),'-f','null','-'])
        record['fullDecodePassed']=True
    files.append(record)
manifest={'film':'يوم دراسي بتناغم — النسخة الموسعة','durationSeconds':50,'fps':30,'masterCanvas':[1600,900],'paceIncrease':0.08,'renderer':'HyperFrames 0.8.163 / Three.js 0.181.2','homepageIntegrated':False,'approval':'pending','removed':'Actual timetable screen and the other two studies from the review gallery','files':files}
(ROOT/'reference'/'extended-delivery-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps([{'file':f['file'],'MB':f['megabytes']} for f in files],indent=2),flush=True)
