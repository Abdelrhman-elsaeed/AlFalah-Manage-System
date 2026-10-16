"""Local review server with byte ranges for reliable video chapter seeking."""
from http.server import SimpleHTTPRequestHandler,ThreadingHTTPServer
from pathlib import Path
from functools import partial
import os
import re

ROOT=Path(__file__).resolve().parent
class ReviewHandler(SimpleHTTPRequestHandler):
    protocol_version='HTTP/1.1'
    extensions_map={**SimpleHTTPRequestHandler.extensions_map,'.mjs':'text/javascript','.js':'text/javascript','.webp':'image/webp','.webm':'video/webm','.mp4':'video/mp4'}

    def end_headers(self):
        self.send_header('Accept-Ranges','bytes')
        super().end_headers()

    def send_head(self):
        self.byte_range=None
        target=Path(self.translate_path(self.path))
        requested=self.headers.get('Range')
        if not requested or not target.is_file():
            return super().send_head()
        match=re.fullmatch(r'bytes=(\d*)-(\d*)',requested.strip())
        if not match:
            return super().send_head()
        file=target.open('rb')
        size=os.fstat(file.fileno()).st_size
        a,b=match.groups()
        start=int(a) if a else max(0,size-int(b))
        end=min(size-1,int(b)) if a and b else size-1
        if start>=size or start>end:
            file.close();self.send_response(416)
            self.send_header('Content-Range',f'bytes */{size}')
            self.send_header('Content-Length','0');self.end_headers();return None
        self.send_response(206)
        self.send_header('Content-Type',self.guess_type(str(target)))
        self.send_header('Content-Length',str(end-start+1))
        self.send_header('Content-Range',f'bytes {start}-{end}/{size}')
        self.send_header('Last-Modified',self.date_time_string(target.stat().st_mtime))
        self.end_headers()
        file.seek(start);self.byte_range=(start,end)
        return file

    def copyfile(self,source,outputfile):
        if self.byte_range is None:
            try:super().copyfile(source,outputfile)
            except (BrokenPipeError,ConnectionResetError,ConnectionAbortedError):pass
            return
        left=self.byte_range[1]-self.byte_range[0]+1
        try:
            while left:
                chunk=source.read(min(128*1024,left))
                if not chunk:break
                outputfile.write(chunk);left-=len(chunk)
        except (BrokenPipeError,ConnectionResetError,ConnectionAbortedError):pass

if __name__=='__main__':
    print('Review: http://127.0.0.1:8877/review.html',flush=True)
    ThreadingHTTPServer(('127.0.0.1',8877),partial(ReviewHandler,directory=str(ROOT))).serve_forever()
