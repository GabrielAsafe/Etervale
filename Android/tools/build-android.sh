#!/usr/bin/env bash
# Compilacao reprodutivel do port Etervale Android, partindo do codigo-fonte.
# Sem binarios precompilados no Git. Este script NAO descarrega assets proprietarios.
set -euo pipefail
ROOT="${ETERVALE_ROOT:-$(pwd)}"
SDK="${ANDROID_HOME:-$HOME/Android/Sdk}"
NDK="${ANDROID_NDK_HOME:-$SDK/ndk/27.2.12479018}"
JAVA_HOME="${JAVA_HOME:-/usr/lib/jvm/java-21-openjdk}"
API="${ETERVALE_ANDROID_API:-26}"
JOBS="${JOBS:-2}"
cd "$ROOT"
fail(){ echo "ERRO: $*" >&2; exit 1; }
for exe in git cmake dotnet "$JAVA_HOME/bin/javac" "$JAVA_HOME/bin/jar"; do command -v "$exe" >/dev/null || fail "falta $exe"; done
[[ -f Android/EtervaleNativeRuntime/CMakeLists.txt ]] || fail "falta Android/EtervaleNativeRuntime/CMakeLists.txt no Git"
[[ -f Android/EtervaleAndroidGame/EtervaleAndroidGame.csproj ]] || fail "falta projeto Android"
[[ -f "$NDK/build/cmake/android.toolchain.cmake" ]] || fail "NDK nao instalado: $NDK"
SDL="monogame/runtime/src/external/sdl2/sdl"
FA="monogame/runtime/src/external/faudio"
[[ -f "$SDL/CMakeLists.txt" && -f "$FA/CMakeLists.txt" ]] || fail "falta SDL2/FAudio; executar o script de obtencao dos externals conforme docs"
TC="$NDK/build/cmake/android.toolchain.cmake"
BASE=("-DCMAKE_TOOLCHAIN_FILE=$TC" '-DANDROID_ABI=arm64-v8a' "-DANDROID_PLATFORM=android-$API" '-DANDROID_STL=c++_static' '-DCMAKE_BUILD_TYPE=Release' '-DCMAKE_POSITION_INDEPENDENT_CODE=ON')
OUT=Android/EtervaleNativeRuntime/build
mkdir -p "$OUT" monogame/runtime/bin/android-arm64
printf '\n[1/5] SDL2 estatico (Android)\n'
cmake -S "$SDL" -B "$OUT/sdl2" "${BASE[@]}" -DSDL_STATIC=ON -DSDL_SHARED=OFF -DSDL_TEST=OFF -DSDL_VULKAN=ON
cmake --build "$OUT/sdl2" --parallel "$JOBS"
SDL_A="$(find "$OUT/sdl2" -name libSDL2.a -type f -print -quit)"
[[ -n "$SDL_A" ]] || fail "libSDL2.a em falta"
printf '\n[2/5] FAudio estatico (Android)\n'
cmake -S "$FA" -B "$OUT/faudio" "${BASE[@]}" -DBUILD_SHARED_LIBS=OFF -DBUILD_SDL3=OFF "-DCMAKE_C_STANDARD_INCLUDE_DIRECTORIES=$ROOT/$SDL/include"
cmake --build "$OUT/faudio" --parallel "$JOBS"
FA_A="$(find "$OUT/faudio" -name libFAudio.a -type f -print -quit)"
[[ -n "$FA_A" ]] || fail "libFAudio.a em falta"
printf '\n[3/5] MonoGame Vulkan Android .so\n'
cmake -S Android/EtervaleNativeRuntime -B "$OUT/mgruntime" "${BASE[@]}" \
 "-DETERVALE_NATIVE_SOURCE=$ROOT/monogame/runtime/src" \
 "-DETERVALE_SDL_STATIC=$ROOT/$SDL_A" \
 "-DETERVALE_FAUDIO_STATIC=$ROOT/$FA_A"
cmake --build "$OUT/mgruntime" --parallel "$JOBS"
cp "$OUT/mgruntime/libmgruntime.so" monogame/runtime/bin/android-arm64/libmgruntime.so
if "$NDK/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-readelf" -d monogame/runtime/bin/android-arm64/libmgruntime.so | grep -E 'NEEDED.*(libc\.so\.6|libstdc\+\+\.so\.6)' ; then
  fail 'Foi ligada biblioteca Linux no runtime Android'
fi
printf '\n[4/5] Classes Java SDL2\n'
ANDROID_JAR="$(find "$SDK/platforms" -type f -name android.jar | sort -V | tail -1)"
[[ -n "$ANDROID_JAR" ]] || fail 'android.jar em falta: instalar platform-tools e platforms Android'
mkdir -p Android/EtervaleSDL2Java/classes
mapfile -t JAVA_SOURCES < <(find Android/EtervaleSDL2Java/src/org/libsdl/app -type f -name '*.java' | sort)
((${#JAVA_SOURCES[@]} > 0)) || fail 'Fontes Java SDL2 em falta'
"$JAVA_HOME/bin/javac" -source 8 -target 8 -Xlint:-options -encoding UTF-8 -classpath "$ANDROID_JAR" -d Android/EtervaleSDL2Java/classes "${JAVA_SOURCES[@]}"
"$JAVA_HOME/bin/jar" cf Android/EtervaleSDL2Java/sdl2-android.jar -C Android/EtervaleSDL2Java/classes .
printf '\n[5/5] Validar XNB e compilar o APK\n'
STAGING=Android/EtervaleAndroidGame/ContentStaging
if [[ -n "${ETERVALE_CONTENT_ROOT:-}" ]]; then
  [[ -f "$ETERVALE_CONTENT_ROOT/Fonts/UI.xnb" ]] || fail 'ETERVALE_CONTENT_ROOT nao contem Fonts/UI.xnb'
  mkdir -p "$STAGING"
  cp -a "$ETERVALE_CONTENT_ROOT"/. "$STAGING/"
fi
[[ -f "$STAGING/Fonts/UI.xnb" ]] || fail 'XNB nao existem. Compilar Content.mgcb com pipeline DesktopVK e definir ETERVALE_CONTENT_ROOT conforme docs'
dotnet build Android/EtervaleAndroidGame/EtervaleAndroidGame.csproj -f net10.0-android -c Debug -p:AndroidSdkDirectory="$SDK" -p:JavaSdkDirectory="$JAVA_HOME"
echo 'OK: build concluido. Para instalar: dotnet build Android/EtervaleAndroidGame/EtervaleAndroidGame.csproj -f net10.0-android -c Debug -t:Install'
