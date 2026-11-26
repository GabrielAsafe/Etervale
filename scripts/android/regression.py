#!/usr/bin/env python3
"""ADB regression runner; no root, no account credentials, no game-file edits."""
import argparse
import collections
import datetime
import json
import pathlib
import queue
import re
import statistics
import subprocess
import threading
import time

PACKAGE = 'com.etervale.game'
METRIC = re.compile(r'\bI ETERVALE_PERF:\s+FPS (\d+) \(cap ([^,]+), VSync (\w+)\).*?frame p95/p99 ([\d,.]+)/[\d,.]+ ms, work ([\d,.]+) ms, present ([\d,.]+) ms, pacing ([\d,.]+) ms,.*?\| (\d+)x(\d+) \|.*?\bPos (-?\d+) (-?\d+) \| Lights (\d+) \| Draw (\d+) .*?\bTri (\d+) .*?scene3D=(\d+)x(\d+).*?\bAA=(\w+) MSAA=(\d+)')
PID = re.compile(r'^\d\d-\d\d\s+[\d:.]+\s+(\d+)\s+\d+\s+')
ERROR = re.compile(r'\bE ETERVALE_VK_VALIDATION: (?:VUID-|UNASSIGNED-|SYNC-HAZARD|transfer failure:|fence.*(?:failed|exhausted))')
CRASH = re.compile(r'Fatal signal|FATAL EXCEPTION')

def parse(line):
    m = METRIC.search(line)
    if not m:
        return None
    a = m.groups()
    return dict(fps=int(a[0]), cap=a[1], vsync=a[2], frame_ms=float(a[3].replace(',', '.')), work_ms=float(a[4].replace(',', '.')), present_ms=float(a[5].replace(',', '.')), pacing_ms=float(a[6].replace(',', '.')), width=int(a[7]), height=int(a[8]), position=[int(a[9]), int(a[10])], lights=int(a[11]), draws=int(a[12]), triangles=int(a[13]), scene=[int(a[14]), int(a[15])], aa=a[16], msaa=int(a[17]), shadows="Shadows ON" if "Shadows ON" in line else "Shadows OFF" if "Shadows OFF" in line else "unknown", graphics=line.split(" | quality=",1)[-1].split(" | finalizersQueued=",1)[0])

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--serial', help='ADB serial (required if multiple devices are connected)')
    p.add_argument('--seconds',type=int,default=20,help='Measurement duration per orientation (default 20)')
    p.add_argument('--settle',type=int,default=8)
    p.add_argument('--manual-rotation',action='store_true',help='Rotate by hand when prompted; all measurement is automatic')
    p.add_argument('--out',type=pathlib.Path,default=pathlib.Path.home()/'etervale-regression')
    args=p.parse_args()
    if args.seconds<15 or args.settle<5: p.error('use at least 15 seconds and 5 seconds of settling')
    adb=['adb']+(['-s',args.serial] if args.serial else [])
    def run(*cmd,check=True):
        return subprocess.run(adb+list(cmd),capture_output=True,text=True,timeout=15,check=check).stdout.strip()
    def pid():
        return run('shell','pidof',PACKAGE,check=False).split()
    if run('get-state')!='device': raise SystemExit('ADB device unavailable')
    folder=args.out/datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f'); folder.mkdir(parents=True)
    report={'version':[], 'phases':[], 'validation_errors':[], 'fatal_signals':[], 'fence_timeouts':0,'fence_recovered':0, 'status':'INCOMPLETE'}
    report['version']=[x.strip() for x in run('shell','dumpsys','package',PACKAGE).splitlines() if 'versionCode=' in x or 'versionName=' in x]
    print('\n'.join(report['version']),flush=True)
    print('Enter gameplay, enable METRICAS, select AA Off, FPS unlimited, VSync off, shadows off. Keep the character still.',flush=True)
    input('Press Enter when ready (no credentials are read): ')
    current=pid()
    if len(current)!=1: raise SystemExit('Expected exactly one game PID; open gameplay first.')
    game_pid=current[0]; report['pid']=game_pid
    original=None; changed=False; proc=None; reader=None
    events=queue.Queue(); logpath=folder/'logcat.txt'; failed=False
    def pump():
        with logpath.open('w',encoding='utf-8') as log:
            for line in proc.stdout:
                log.write(line); log.flush(); events.put(line.rstrip())
    def consume(seconds, collect=False):
        end=time.monotonic()+seconds; rows=[]
        while time.monotonic()<end:
            try: line=events.get(timeout=min(0.5,max(0.001,end-time.monotonic())))
            except queue.Empty: continue
            m=PID.match(line)
            if not m or m.group(1)!=game_pid: continue
            if ERROR.search(line): report['validation_errors'].append(line); print('VULKAN ERROR:',line,flush=True)
            if CRASH.search(line): report['fatal_signals'].append(line); raise RuntimeError('Game fatal signal')
            if 'fence timeout:' in line: report['fence_timeouts']+=1
            if 'fence recovered:' in line: report['fence_recovered']+=1
            if collect and (sample:=parse(line)): rows.append(sample)
        if proc.poll() is not None: raise RuntimeError('Logcat exited')
        if pid()!=[game_pid]:
            report['process_changed']=True
            raise RuntimeError('Game PID disappeared or changed')
        return rows
    try:
        if not args.manual_rotation:
            original=run('shell','cmd','window','user-rotation')
            if not re.fullmatch(r'free|lock [0-3]',original): raise RuntimeError('Cannot read original rotation: '+original+'; retry --manual-rotation')
            report['original_rotation']=original
        proc=subprocess.Popen(adb+['logcat','-b','main','-b','system','-b','crash','-T','1','-v','threadtime','-s','ETERVALE_VK_VALIDATION:V','ETERVALE_BOOT:I','ETERVALE_PERF:I','ETERVALE_VK_ROTATION:I','ETERVALE_SDL_RESIZE:I','libc:F','DEBUG:F','AndroidRuntime:E'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,bufsize=1)
        reader=threading.Thread(target=pump,daemon=True); reader.start()
        for label,rotation,portrait in [('landscape_a','1',False),('portrait','0',True),('landscape_b','1',False)]:
            print('\nPhase:',label,flush=True)
            if args.manual_rotation:
                input('Rotate to '+label+' and press Enter: ')
            else:
                changed=True
                run('shell','cmd','window','user-rotation','lock',rotation)
            consume(args.settle)
            rows=consume(args.seconds,True)
            matching=[r for r in rows if (r['height']>r['width'])==portrait]
            phase={'name':label,'samples':rows,'matching':len(matching),'status':'INCOMPLETE'}; report['phases'].append(phase)
            if len(matching)<3 or len(matching)!=len(rows):
                print('Orientation not verified or insufficient samples. Check METRICAS; try --manual-rotation.',flush=True); continue
            phase['median']={k:statistics.median(r[k] for r in matching) for k in ['fps','frame_ms','work_ms','present_ms','pacing_ms','draws','triangles','lights']}
            phase['status']='PASS' if all(r['cap']=='unlimited' and r['vsync']=='off' and r['aa']=='Off' and r['msaa']==0 and r['shadows']=='Shadows OFF' for r in matching) else 'SETTINGS_MISMATCH'
            print(phase['status'],json.dumps(phase['median']),flush=True)
        allrows=[r for phase in report['phases'] for r in phase['samples']]
        report['same_position']=len({tuple(r['position']) for r in allrows})==1 if allrows else False
        report['same_graphics']=len({r['graphics'] for r in allrows})==1 if allrows else False
        report['same_scene_pixel_count']=len({r['scene'][0]*r['scene'][1] for r in allrows})==1 if allrows else False
        report['status']='FAIL' if report['validation_errors'] or report['fatal_signals'] else ('PASS' if len(report['phases'])==3 and all(x['status']=='PASS' for x in report['phases']) and report['same_position'] and report['same_scene_pixel_count'] and report['same_graphics'] else 'INCOMPLETE')
    except (Exception,KeyboardInterrupt) as exc:
        report['status']='FAIL' if report['fatal_signals'] or report['validation_errors'] or report.get('process_changed') else 'INCOMPLETE'; report['interrupted']=str(exc) or type(exc).__name__
        print('Stopped:',report['interrupted'],flush=True)
    finally:
        if changed and original:
            try: run('shell','cmd','window','user-rotation',*original.split()); report['rotation_restored']=True
            except Exception as exc: report['rotation_restored']=False; report['restore_error']=str(exc); report['status']='INCOMPLETE' if report['status']=='PASS' else report['status']
        if proc:
            proc.terminate()
            try: proc.wait(timeout=5)
            except subprocess.TimeoutExpired: proc.kill(); proc.wait()
        if reader: reader.join(timeout=2)
        (folder/'report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
        summary=[f"Status: {report['status']}",*report['version'],f"Validation errors: {len(report['validation_errors'])}",f"Fatal signals: {len(report['fatal_signals'])}",f"Fence timeouts/recovered: {report['fence_timeouts']}/{report['fence_recovered']}"]
        for phase in report['phases']: summary.append(f"{phase['name']}: {phase['status']} | {phase['matching']} samples | {phase.get('median',{})}")
        for key in ('same_position','same_scene_pixel_count','same_graphics','process_changed','rotation_restored','interrupted','restore_error'):
            if key in report: summary.append(f'{key}: {report[key]}')
        (folder/'summary.txt').write_text('\n'.join(summary)+'\n',encoding='utf-8')
        print('\n'+'\n'.join(summary)+'\nResults: '+str(folder),flush=True)
    return 0 if report['status']=='PASS' else 1

if __name__=='__main__':
    raise SystemExit(main())
