#!/usr/bin/env bash
# Obtem dependencias nativas fixadas por monogame/runtime/externals.txt.
set -euo pipefail
ROOT="${ETERVALE_ROOT:-$(pwd)}"
cd "$ROOT"
fetch(){
  local path="$1" revision="$2" url="$3"
  if [[ -d "$path/.git" ]]; then
    local have
    have="$(git -C "$path" rev-parse HEAD)"
    [[ "$have" == "$revision" ]] || { echo "ERRO: $path ja existe noutro commit: $have" >&2; exit 1; }
  elif [[ -e "$path" ]]; then
    echo "ERRO: $path existe mas nao e um repo Git, nao sobrescrevo" >&2; exit 1
  else
    mkdir -p "$(dirname "$path")"
    git clone "$url" "$path"
    git -C "$path" checkout --detach "$revision"
  fi
}
fetch monogame/runtime/src/external/sdl2/sdl 5d249570393f7a37e037abf22cd6012a4cc56a71 https://github.com/libsdl-org/SDL.git
fetch monogame/runtime/src/external/faudio 8a87fdc9242a7b507cf5c96c539334a1760c1ec7 https://github.com/FNA-XNA/FAudio.git
printf 'Dependencias obtidas. Nota: verifica e aplica patches do fork em monogame/runtime/patches, se exigidos.\n'
