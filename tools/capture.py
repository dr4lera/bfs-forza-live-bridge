"""Window-only Forza capture. No input, injection or game-file access."""
import argparse, ctypes, json, mmap, os, queue, shutil, struct, subprocess, threading, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
HEADER = struct.Struct('<IIIIQQII')
def main():
    parser = argparse.ArgumentParser(description='Continuous window capture: no time limit. Use Stop-Bridge.ps1 to stop.')
    parser.add_argument('--ffmpeg', default=shutil.which('ffmpeg') or str(Path(os.environ['LOCALAPPDATA'])/'universal-modder/ffmpeg/bin/ffmpeg.exe'))
    args = parser.parse_args()
    cfg = json.loads((ROOT/'sheets/bridge.json').read_text())['transport'][0]
    # Reject absent, ambiguous or minimized targets before creating a capture stream.
    windows=[]
    user32=ctypes.windll.user32
    user32.GetWindowTextW.argtypes=[ctypes.c_void_p,ctypes.c_wchar_p,ctypes.c_int]
    user32.IsWindowVisible.argtypes=[ctypes.c_void_p]
    user32.IsIconic.argtypes=[ctypes.c_void_p]
    callback=ctypes.WINFUNCTYPE(ctypes.c_bool,ctypes.c_void_p,ctypes.c_void_p)
    @callback
    def visit(hwnd, _):
        buf=ctypes.create_unicode_buffer(512)
        user32.GetWindowTextW(hwnd,buf,512)
        if buf.value == 'Forza Horizon 6' and user32.IsWindowVisible(hwnd): windows.append(int(hwnd))
        return True
    user32.EnumWindows(visit,0)
    if len(windows)!=1: raise SystemExit(f'Expected one Forza Horizon 6 window; found {len(windows)}. Open the Xbox copy first.')
    if user32.IsIconic(windows[0]): raise SystemExit('Forza is minimized; restore it before capture.')
    w,h=cfg['width'],cfg['height']; size=w*h*4
    view=mmap.mmap(-1,cfg['headerBytes']+size,tagname=cfg['mapping'],access=mmap.ACCESS_WRITE)
    filt=f"gfxcapture=hwnd={windows[0]}:capture_cursor=0:capture_border=0:max_framerate={cfg['fps']}:width={w}:height={h}:resize_mode=scale_aspect"
    cmd=[args.ffmpeg,'-hide_banner','-loglevel','warning','-f','lavfi','-i',filt,'-vf','hwdownload,format=bgra,format=rgba','-an','-c:v','rawvideo','-pix_fmt','rgba','-f','rawvideo','pipe:1']
    evidence=ROOT/'evidence'; evidence.mkdir(exist_ok=True)
    stopfile=ROOT/'capture.stop'
    if stopfile.exists(): raise SystemExit('Remove capture.stop to start a new capture session.')
    start=time.monotonic(); frames=0; handoff=queue.Queue(maxsize=1); reader_done=threading.Event()
    with (evidence/'capture.log').open('w') as log:
        proc=subprocess.Popen(cmd,stdout=subprocess.PIPE,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
        def read_frames():
            try:
                while True:
                    buf=bytearray(size); offset=0
                    while offset<size:
                        n=proc.stdout.readinto(memoryview(buf)[offset:])
                        if not n:return
                        offset+=n
                    packet=(buf,time.time_ns()//1_000_000)
                    try:handoff.put_nowait(packet)
                    except queue.Full:
                        try:handoff.get_nowait()
                        except queue.Empty:pass
                        try:handoff.put_nowait(packet)
                        except queue.Full:pass
            finally:reader_done.set()
        threading.Thread(target=read_frames,daemon=True).start()
        print(json.dumps({'capturePid':proc.pid,'window':windows[0],'mapping':cfg['mapping'],'timeLimit':None}),flush=True)
        try:
            while not stopfile.exists():
                try:buf,stamp=handoff.get(timeout=.25)
                except queue.Empty:
                    if reader_done.is_set():raise RuntimeError('Forza capture ended; see evidence/capture.log')
                    continue
                frames+=1
                odd=frames*2-1
                view[:HEADER.size]=HEADER.pack(cfg['magic'],1,w,h,odd,stamp,size,1)
                view[cfg['headerBytes']:cfg['headerBytes']+size]=buf
                view[16:24]=struct.pack('<Q',odd+1)
                if frames%cfg['fps']==0:
                    report={'frames':frames,'captureFps':round(frames/(time.monotonic()-start),2),'publishedUnixMs':stamp,'width':w,'height':h,'timeLimit':None,'scope':'Forza window only; no camera/input synchronization'}
                    (evidence/'capture-status.json').write_text(json.dumps(report,indent=2))
        except KeyboardInterrupt: pass
        finally:
            view[36:40]=struct.pack('<I',0)
            proc.terminate()
            try: proc.wait(timeout=5)
            except subprocess.TimeoutExpired: proc.kill();proc.wait()
            view.close()
            print(json.dumps({'frames':frames,'seconds':round(time.monotonic()-start,2),'ffmpegExit':proc.returncode}),flush=True)
if __name__=='__main__': main()
