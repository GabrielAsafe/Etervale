#!/usr/bin/env python3
"""Edita apenas client-settings.json do Etervale no OPPO via adb; cria backup local.

Exemplos:
 python3 etervale_android_config.py --fps 30 --ui-scale 1.5
 python3 etervale_android_config.py --host 192.168.1.111 --port 10009 --pin HEX_SHA256

Nunca edita contas/palavras-passe. Requer app instalada, config criada e USB com adb.
"""
import argparse
from datetime import datetime
from pathlib import Path
import json
import re
import subprocess
import sys

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--host', help='Endereço DNS/IP do servidor TLS')
p.add_argument('--port', type=int, default=10009)
p.add_argument('--pin', help='SHA-256 do certificado X.509 DER, 64 digitos hexadecimais')
p.add_argument('--fps', type=int, choices=[0,15,20,24,25,30,40,45,50,60,90,120])
p.add_argument('--ui-scale', type=float)
p.add_argument('--window-width', type=int)
p.add_argument('--window-height', type=int)
args = p.parse_args()
if args.pin and not args.host:
    p.error('--pin exige --host')
if args.host and not args.pin:
    p.error('--host exige --pin (validacao TLS explicita)')
if args.host and (not 1 <= args.port <= 65535 or not re.fullmatch(r'[A-Fa-f0-9]{64}', args.pin or '')):
    p.error('Porta invalida ou pin incorreto (64 hex)')
if args.ui_scale is not None and not 0.75 <= args.ui_scale <= 1.5:
    p.error('--ui-scale valido de 0.75 a 1.5')
for key in ('window_width','window_height'):
    val = getattr(args,key)
    if val is not None and not 800 <= val <= 16384:
        p.error('--'+key.replace('_','-')+' tem de estar entre 800 e 16384')
if not any((args.host, args.fps is not None, args.ui_scale is not None,
            args.window_width is not None,args.window_height is not None)):
    p.error('Indica pelo menos um parametro a alterar')

remote = '/sdcard/Android/data/com.etervale.game/files/client-settings.json'
try:
    result = subprocess.run(['adb', 'exec-out', 'cat', remote], capture_output=True, check=True)
except (OSError, subprocess.CalledProcessError) as exc:
    sys.exit('ERRO: nao consegui ler client-settings.json com adb. Abre a app uma vez. '+str(exc))
try:
    config = json.loads(result.stdout.decode('utf-8'))
    if not isinstance(config, dict): raise ValueError('JSON nao e objeto')
except (UnicodeDecodeError, ValueError) as exc:
    sys.exit('ERRO: configuracao invalida, nada alterado: '+str(exc))
backups = Path.home()/'etervale-oppo-config-backups'
backups.mkdir(parents=True, exist_ok=True)
name = 'client-settings-'+datetime.now().strftime('%Y%m%d-%H%M%S-%f')
(backups/(name+'.json')).write_bytes(result.stdout)
if args.host:
    host = args.host.strip()
    if not host or any(c in host for c in '/?#@'):
        sys.exit('ERRO: host invalido. Nada enviado ao OPPO.')
    config['serverAddress'] = f'tcp://{host}:{args.port}?cert={args.pin.upper()}'
if args.fps is not None: config['maxFps'] = args.fps
if args.ui_scale is not None: config['uiScale'] = args.ui_scale
if args.window_width is not None: config['windowWidth'] = args.window_width
if args.window_height is not None: config['windowHeight'] = args.window_height
newfile = backups/(name+'-edited.json')
newfile.write_text(json.dumps(config, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print('Backup local:',backups/(name+'.json'))
print('Alteracoes:', {k:config[k] for k in ('serverAddress','maxFps','uiScale','windowWidth','windowHeight') if k in config})
print('O jogo deve estar fechado para evitar sobrescrever as alteracoes.')
reply=input('Confirmar envio desta configuracao para o OPPO? [s/N] ').strip().lower()
if reply not in ('s','sim'):
    print('Cancelado: nenhum ficheiro enviado ao OPPO.')
    sys.exit(0)
subprocess.run(['adb','push',str(newfile),remote], check=True)
print('OK: configuracao transferida. Reinicia a aplicacao.')
