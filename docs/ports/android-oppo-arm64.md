# Etervale — port Android ARM64 (OPPO)

**Estado em 2026-11-23:** demonstração funcional do cliente real **MonoGame/Vulkan** em
**OPPO CPH2065, Android 12, ARM64 (`arm64-v8a`), GPU Mali-G57 MC4**.
Foram observados o ecrã de login 3D, a autenticação TLS, a seleção de personagem
**e a entrada no mundo 3D**. Ainda é um port experimental; não é uma versão mobile
pronta a distribuir.

## Arquitetura

- **Managed (.NET 10):** `Android/EtervaleAndroidGame` integra o `Game1.cs` do
  cliente desktop e as bibliotecas `PristonTale.Gameplay`, `Engine3D`, `Effects`,
  `Assets`, `Client.Net`, `Protocol`, `Simulation`, `Geometry`, `Contracts` e o
  fork `MonoGame.Framework.Native`.
- **Runtime nativo:** `monogame/runtime/bin/android-arm64/libmgruntime.so`, gerado
  localmente com o **Android NDK** a partir do runtime MonoGame, SDL2 **estático**,
  FAudio **estático** e suporte Vulkan. **Não usar o ELF linux-arm64** no Android.
- **Java/JNI:** fontes em `Android/EtervaleSDL2Java/src/org/libsdl/app/` incluem
  a `SDLActivity` adaptada para que o `Game1.Run()` C# controle o loop, sem iniciar
  outro `SDL_main`. A atividade `EtervaleSDLActivity` disponibiliza `SurfaceView`
  ao Vulkan e trata paisagem/immersive.
- **IME:** `MGP_android_keyboard.cpp` exporta `Etervale_Android_StartTextInput` /
  `StopTextInput`, chamados pelo Android C# quando a UI seleciona os campos de
  login. O login do próprio jogo processa os caracteres SDL.
- **XNB:** ficheiros `.xnb` compilados do `Content.mgcb` desktop entram no APK como
  `AndroidAsset` a partir de `Android/EtervaleAndroidGame/ContentStaging`.
  `EtervaleAndroidContentProvider` lê esses ficheiros e a pasta privada da app.
- **Assets grandes:** *não* são incluídos no Git nem no APK. São copiados para
  `/sdcard/Android/data/com.etervale.game/files/assets/` no OPPO.
- **Configuração:** `client-settings.json` é criado na pasta privada da app na
  primeira execução, com preset Low e endpoint TLS de desenvolvimento. A
  configuração existente é preservada em arranques seguintes.

## Ferramentas usadas no Fedora

- .NET SDK 10 / `net10.0-android`, Android workload 36.1.118 (pode evoluir).
- JDK 21: `/usr/lib/jvm/java-21-openjdk`.
- Android SDK: `~/Android/Sdk`; Android NDK **27.2.12479018**.
- CMake, compilador/ligador Android LLVM, `javac`, `jar`, `adb`.

```bash
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk
export PATH="$JAVA_HOME/bin:$PATH"
export ANDROID_HOME="$HOME/Android/Sdk"
export ANDROID_NDK_HOME="$ANDROID_HOME/ndk/27.2.12479018"
```

## Reproduzir a compilação (ordem obrigatória)

1. Obter os *externals* do fork MonoGame (`monogame/runtime/externals.txt`),
   especialmente fontes SDL2, FAudio, Vulkan-Headers, volk, VMA e stb, antes de
   compilar o C++. As dependências nativas descarregadas **não são vendorizadas no
   Git deste port**.
2. Compilar SDL2 e FAudio para `arm64-v8a` / `android-26` como bibliotecas
   estáticas com `-DCMAKE_POSITION_INDEPENDENT_CODE=ON` e
   `-DANDROID_STL=c++_static` usando o toolchain do NDK. Na máquina de
   desenvolvimento foram usados:

   ```text
   Android/EtervaleNativeRuntime/build/sdl2/libSDL2.a
   Android/EtervaleNativeRuntime/build/faudio/libFAudio.a
   ```

3. Compilar `Android/EtervaleNativeRuntime/CMakeLists.txt` com:

   ```bash
   cmake -S Android/EtervaleNativeRuntime \
     -B Android/EtervaleNativeRuntime/build/mgruntime \
     -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake" \
     -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-26 \
     -DANDROID_STL=c++_static -DCMAKE_POSITION_INDEPENDENT_CODE=ON \
     -DCMAKE_BUILD_TYPE=Release \
     -DETERVALE_NATIVE_SOURCE="$PWD/monogame/runtime/src" \
     -DETERVALE_SDL_STATIC="$PWD/Android/EtervaleNativeRuntime/build/sdl2/libSDL2.a" \
     -DETERVALE_FAUDIO_STATIC="$PWD/Android/EtervaleNativeRuntime/build/faudio/libFAudio.a"
   cmake --build Android/EtervaleNativeRuntime/build/mgruntime --parallel 2
   mkdir -p monogame/runtime/bin/android-arm64
   cp Android/EtervaleNativeRuntime/build/mgruntime/libmgruntime.so \
     monogame/runtime/bin/android-arm64/libmgruntime.so
   ```

4. Compilar o Java SDL2 para o JAR local (usa as fontes *já adaptadas* na árvore
   `Android/EtervaleSDL2Java/src`). Este JAR é artefacto de compilação, **não
   deve ir para Git**:

   ```bash
   mkdir -p Android/EtervaleSDL2Java/classes
   find Android/EtervaleSDL2Java/src/org/libsdl/app -name '*.java' -print0 \
     | xargs -0 "$JAVA_HOME/bin/javac" -source 8 -target 8 \
         -encoding UTF-8 -classpath "$ANDROID_HOME/platforms/android-36/android.jar" \
         -d Android/EtervaleSDL2Java/classes
   "$JAVA_HOME/bin/jar" cf Android/EtervaleSDL2Java/sdl2-android.jar \
     -C Android/EtervaleSDL2Java/classes .
   ```

5. **Gerar XNB desktop com a pipeline do projeto** (`Client/PristonTale/Content/Content.mgcb`),
   incluindo `Fonts/UI.xnb` e os effects Vulkan, e copiar os `.xnb` compilados
   para `Android/EtervaleAndroidGame/ContentStaging/` **preservando as subpastas**:

   ```bash
   export ETERVALE_CONTENT_ROOT="/caminho/para/Content-compilado"
   test -f "$ETERVALE_CONTENT_ROOT/Fonts/UI.xnb"
   mkdir -p Android/EtervaleAndroidGame/ContentStaging
   cp -a "$ETERVALE_CONTENT_ROOT"/. Android/EtervaleAndroidGame/ContentStaging/
   ```

   No primeiro teste havia 20 `.xnb` (~1,87 MiB), provenientes da compilação
   desktop existente. O ficheiro `ContentStaging/` **não deve ir para Git**.
6. Compilar e instalar o cliente Android:

   ```bash
   dotnet build Android/EtervaleAndroidGame/EtervaleAndroidGame.csproj \
     -f net10.0-android -c Debug -t:Install \
     -p:AndroidSdkDirectory="$ANDROID_HOME" \
     -p:JavaSdkDirectory="$JAVA_HOME"
   ```

7. Copiar **assets/** completos (aproximadamente 6,2 GB no teste) para a pasta
   externa privada da aplicação, verificando previamente o espaço:

   ```bash
   ASSETS="$HOME/Transferências/Etervale-git/assets"
   test -f "$ASSETS/Hud/Manifests/atlas.json"
   adb shell df -h /sdcard
   adb push --sync "$ASSETS" /sdcard/Android/data/com.etervale.game/files/
   ```

   **Nunca incluir `assets/` em commits nem duplicá-los dentro do APK.**

## Testar

```bash
adb logcat -c
adb shell am force-stop com.etervale.game
adb shell monkey -p com.etervale.game 1
# No OPPO, iniciar Game1, aguardar o carregamento, autenticar e escolher personagem
adb shell pidof com.etervale.game
adb logcat -d -v time -s ETERVALE_3D:I SDL:I AndroidRuntime:E libc:F
adb exec-out screencap -p > "$HOME/etervale-oppo.png"
```

A primeira carga de texturas/modelos/shaders pode demorar **dezenas de segundos**.
Um ecrã roxo inicial não é prova de crash; confirmar `pidof` e `logcat` antes de
mudar o runtime. Uma janela de recolha de 20 segundos revelou-se insuficiente.

## Resultados e limitações conhecidas (23-11-2026)

| Área | Estado |
|---|---|
| MonoGame nativo Vulkan, SDL2 e FAudio `arm64-v8a` | Compilados; renderização observada |
| JNI, SDLActivity e SurfaceView | Funcionam no dispositivo |
| Content `.xnb` e assets externos | Carregados |
| TLS/login/teclado virtual | Funcionais |
| Seleção de personagem | Funcional |
| **Entrada no mundo** | **Confirmada no OPPO** |
| Layout mobile, escala HUD, toque multi-dedo | **Pendente** |
| Performance, aquecimento, memória, estabilidade longa | Ainda sem perfil completo |
| Inicialização/carregamento | Lenta; otimização pendente |
| Compatibilidade com páginas de 16 KB | NDK gerou aviso XA0141; rever antes de Android 16 |

### Atenção à configuração do servidor

O ficheiro `Android/EtervaleAndroidGame/EtervaleAndroidStartup.cs` contém o
endereço de desenvolvimento e o fingerprint SHA-256 do certificado TLS **para o
laboratório atual**. Este endereço não é configuração pública para outras
máquinas. Substituir por configuração externa antes de publicar uma versão de
produção; **nunca versionar credenciais**.

### Próximo trabalho

1. UI mobile em paisagem: âncoras/escala HUD para resoluções e *cutouts*.
2. Entrada multi-toque, comandos de movimento, câmara e ações e teclado/chat.
3. Perfil da primeira carga: shaders Vulkan, I/O dos modelos, caches e áudio.
4. Testes de entrada/saída do mundo, mudanças de mapa, suspensão/retoma.
5. Empacotamento/descarregamento dos recursos para distribuição.

## Histórico de desbloqueios

- APK `net10.0-android` e login TLS funcional.
- `libmgruntime.so` Android compilada com NDK e incluída no APK.
- `org.libsdl.app.SDLActivity` Java/JNI integrado; eliminado o `SIGABRT`.
- `Content/Fonts/UI.xnb` incluído e `TitleContainer.ContentProvider` adaptado.
- Pesquisa de ficheiros `AssetFiles` corrigida para permissões Android.
- `assets/` copiados para armazenamento da app.
- Teclado SDL2 Android, paisagem fullscreen e preset Low.
- Login 3D, personagens e entrada no mundo confirmados.
