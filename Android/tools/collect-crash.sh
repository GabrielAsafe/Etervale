#!/usr/bin/env bash
# Recolhe informacao do crash nativo Mali sem alterar ou reiniciar o jogo.
set -euo pipefail
OUT="${1:-$HOME/etervale-crash-$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT"
command -v adb >/dev/null || { echo 'adb não instalado' >&2; exit 1; }
echo "Recolher logs em $OUT"
adb devices > "$OUT/adb-devices.txt"
adb logcat -b crash -d -v threadtime > "$OUT/logcat-crash.txt" 2>&1 || true
adb logcat -b main,system,events -d -v threadtime > "$OUT/logcat-full.txt" 2>&1 || true
adb shell dumpsys activity exit-info com.etervale.game > "$OUT/exit-info.txt" 2>&1 || true
adb shell getprop ro.product.model > "$OUT/model.txt" 2>&1 || true
adb shell getprop ro.build.version.release > "$OUT/android-version.txt" 2>&1 || true
# Os tombstones completos nao sao acessiveis a apps normais sem permissoes especiais.
# So recomendar bugreport separado se o logcat -b crash nao contiver stack.
{
  grep -Ei -A 60 -B 10 'mali-cmar|SIGABRT|signal 6|Abort message|backtrace:|tombstone|VK_ERROR|Vulkan|ETERVALE_3D' "$OUT/logcat-crash.txt" || true
  grep -Ei -A 15 -B 7 'mali-cmar|SIGABRT|Abort message|VK_ERROR|Vulkan|ETERVALE_3D|lowmemorykiller|lmkd' "$OUT/logcat-full.txt" | tail -n 320 || true
} > "$OUT/resumo.txt"
cat <<EOF
Relatorios:
  $OUT/resumo.txt
  $OUT/logcat-crash.txt
  $OUT/exit-info.txt
Se nao houver stack nativa no logcat-crash.txt, corre (pode gerar ficheiro grande):
  adb bugreport "$OUT/etervale-bugreport.zip"
ATENCAO: logcat e bugreport podem conter dados pessoais; revê antes de publicar.
EOF
