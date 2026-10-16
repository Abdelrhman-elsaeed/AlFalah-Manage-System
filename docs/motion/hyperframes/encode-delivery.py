"""Create web encodes, posters, decoded QC frames, and a measured manifest."""
from pathlib import Path
import argparse
import json
import subprocess
import hashlib

ROOT=Path(__file__).parent
RENDERS=ROOT/'renders'
FILMS={'pulse':('01-school-pulse',8.5),'harmony':('02-day-in-harmony',4.0),'evidence':('03-evidence-to-insight',7.5)}

def run(args):
    subprocess.run(args,check=True,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)

def probe(file):
    return json.loads(subprocess.check_output(['ffprobe','-v','error','-show_format','-show_streams','-of','json',str(file)]))

def encode(name):
    stem,poster_at=FILMS[name]
    master=RENDERS/f'{stem}-master.mp4'
    base=['ffmpeg','-y','-v','error','-i',str(master),'-an']
    run(base+['-vf','scale=1280:720:flags=lanczos','-c:v','libx264','-preset','slow','-crf','22','-pix_fmt','yuv420p','-movflags','+faststart',str(RENDERS/f'{stem}.mp4')])
    run(base+['-vf','scale=1280:720:flags=lanczos','-c:v','libvpx-vp9','-crf','33','-b:v','0','-cpu-used','3','-row-mt','1','-threads','4','-pix_fmt','yuv420p',str(RENDERS/f'{stem}.webm')])
    run(base+['-vf','scale=640:360:flags=lanczos','-c:v','libx264','-preset','slow','-crf','23','-pix_fmt','yuv420p','-movflags','+faststart',str(RENDERS/f'{stem}-mobile.mp4')])
    run(['ffmpeg','-y','-v','error','-ss',str(poster_at),'-i',str(master),'-frames:v','1','-vf','scale=1280:720:flags=lanczos','-c:v','libwebp','-quality','88',str(RENDERS/f'{stem}-poster.webp')])
    qc=ROOT/'reference'/f'{name}-decoded'
    qc.mkdir(exist_ok=True)
    for t in [0,4,8,12,15.966667]:
        run(['ffmpeg','-y','-v','error','-ss',str(t),'-i',str(master),'-frames:v','1',str(qc/f'at-{t:g}.png')])
    print(name,'encoded and decoded QC frames extracted',flush=True)

def manifest():
    files=[]
    for f in sorted(RENDERS.glob('*')):
        if f.suffix not in ['.mp4','.webm','.webp']: continue
        item={'file':f.name,'bytes':f.stat().st_size,'megabytes':round(f.stat().st_size/1_000_000,3),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()}
        if f.suffix!='.webp':
            p=probe(f); v=next(s for s in p['streams'] if s['codec_type']=='video')
            item.update(width=v['width'],height=v['height'],codec=v['codec_name'],pixelFormat=v['pix_fmt'],fps=v['r_frame_rate'],duration=float(p['format']['duration']),audioStreams=sum(s['codec_type']=='audio' for s in p['streams']))
        files.append(item)
    result={'homepageIntegrated':False,'approval':'pending','renderer':'HyperFrames 0.8.163','masterCanvas':[1600,900],'durationSeconds':16,'fps':30,'files':files}
    (ROOT/'reference'/'delivery-manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps([{'file':f['file'],'MB':f['megabytes']} for f in files],indent=2))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('film',choices=[*FILMS,'all','manifest']);args=p.parse_args()
    if args.film=='all':
        for name in FILMS:encode(name)
        manifest()
    elif args.film=='manifest':manifest()
    else:encode(args.film)
