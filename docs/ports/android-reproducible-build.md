# Etervale Android ARM64 — build a partir do código-fonte

**Estado:** protótipo funcional no OPPO CPH2065 (Android 12, Mali-G57 MC4). Login TLS, seleção de personagem e entrada no mundo Vulkan foram demonstrados. O port **ainda tem crash nativo SIGABRT** e não é uma release estável. Não é um APK pronto a distribuir.

## Arquitetura e motivo das duas Activities

O projeto `Android/EtervaleAndroidGame` contém o cliente C# `.NET 10`, `Game1.Run()` e os projetos `PristonTale.*`. `org.libsdl.app.EtervaleSDLActivity` disponibiliza a SurfaceView/JNI usada pelo SDL2/Vulkan; `MainActivity` dá início ao loop gerido C#. O ecrã de botão inicial era um *bootstrap de testes*, não uma necessidade funcional. O patch v6 faz o arranque automático a partir do ícone, mantendo ambas as Activities internas. Unificá-las completamente exige uma ponte de inicialização Java/.NET diferente.

Os fontes de `monogame/runtime` geram `libmgruntime.so` **Android `arm64-v8a`** com SDL2 e FAudio estáticos. Os ficheiros Linux `linux-arm64` não são ABI Android e não devem entrar no APK.

## Ferramentas

Testadas: Fedora, `.NET SDK 10`, workload Android (`Microsoft.Android.Sdk.Linux` 36.1.118), JDK 21, Android SDK plataforma 36, NDK **27.2.12479018**, CMake 3.31, Git, `adb`, Python 3, OpenSSL. Os valores são a referência do port; outras versões podem necessitar de ajustes.

```bash
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk
export PATH="$JAVA_HOME/bin:$PATH"
export ANDROID_HOME="$HOME/Android/Sdk"
export ANDROID_NDK_HOME="$ANDROID_HOME/ndk/27.2.12479018"
dotnet workload install android
# Instalar SDK e NDK com sdkmanager:
"$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" --sdk_root="$ANDROID_HOME" \
  'platform-tools' 'platforms;android-36' 'ndk;27.2.12479018'
```

## 1. Obter código e dependências fixadas

```bash
git clone https://github.com/GabrielAsafe/Etervale.git
cd Etervale
git switch feature/android-arm64-vulkan # após a branch estar publicada
```

Os *externals* fixados em `monogame/runtime/externals.txt` incluem `SDL2` no commit `5d249570...` e `FAudio` no commit `8a87fdc9...`; Vulkan-Headers, volk, VMA e stb são vendorizados no fork. O script `Android/tools/fetch-externals.sh` obtém os repositórios fixados sem sobrescrever clones locais. Rever os patches do fork em `monogame/runtime/patches`, quando aplicáveis. O port só é reproduzível se os ficheiros gerados por estes scripts estiverem efetivamente **versionados na branch**.

```bash
bash Android/tools/fetch-externals.sh
```

## 2. Construir recursos `.xnb` (obrigatório)

O jogo usa `Client/PristonTale/Content/Content.mgcb` com o `SpirvEffectProcessor` do fork. A documentação oficial do próprio repositório indica estes comandos no **host de build Linux** (o motor nativo Linux também tem de ter sido construído para o cliente desktop):

```bash
dotnet tool restore
dotnet restore PristonTale.sln
dotnet build PristonTale.sln -c Release -m:1
```

No Fedora, isto usa as ferramentas da pipeline MonoGame, incluindo DXC/SPIR-V. **Para construir o runtime Linux necessário a este build desktop**, consultar `monogame/runtime/build.sh`; é uma dependência do passo de compilação dos assets, não a biblioteca a instalar no APK Android. Confirmar o local real dos `.xnb` depois do build:

```bash
find Client/PristonTale -type f -path '*/Fonts/UI.xnb' -print
# Caminho ao diretório que CONTÉM Fonts/UI.xnb e Effects/*.xnb
export ETERVALE_CONTENT_ROOT="/caminho/para/Content-compilado"
test -f "$ETERVALE_CONTENT_ROOT/Fonts/UI.xnb"
find "$ETERVALE_CONTENT_ROOT" -type f -name '*.xnb' | wc -l
```

Na experiência original havia 20 `.xnb` (~1,87 MiB); foram compilados no checkout de desenvolvimento e copiados para `ContentStaging`. Estes binários são gerados, **não entram no Git**. A pipeline de conteúdo deve ser validada numa máquina limpa antes de anunciar um build de um só comando.

## 3. Compilar SDL2, FAudio, MonoGame e o APK

```bash
export ETERVALE_ROOT="$PWD"
bash Android/tools/build-android.sh
# Depois de compilar:
dotnet build Android/EtervaleAndroidGame/EtervaleAndroidGame.csproj \
  -f net10.0-android -c Debug -t:Install \
  -p:AndroidSdkDirectory="$ANDROID_HOME" -p:JavaSdkDirectory="$JAVA_HOME"
```

O script requer `Android/EtervaleNativeRuntime/CMakeLists.txt` e `Android/EtervaleSDL2Java/src/`, que são **fontes necessários de versionar**; os diretórios `build/`, `classes/`, `.so`, `.jar`, `.xnb` e `.apk` devem ser ignorados. O APK deve conter `lib/arm64-v8a/libmgruntime.so` e `assets/Content/Fonts/UI.xnb`.

## 4. Assets do jogo, sem os incluir no Git/APK

Os dados grandes devem ser obtidos apenas por meios autorizados (aproximadamente 6,2 GB no teste). Na máquina de desenvolvimento foram copiados por ADB:

```bash
ASSETS=/caminho/autorizado/para/assets
test -f "$ASSETS/Hud/Manifests/atlas.json"
adb shell df -h /sdcard
adb push --sync "$ASSETS" /sdcard/Android/data/com.etervale.game/files/
```

Destinos esperados: `/sdcard/Android/data/com.etervale.game/files/assets/` e o `client-settings.json` na pasta-mãe. O cliente não deve copiar segredos, banco de dados ou `server-certificate.pfx` para o Android.

## 5. Configurar TLS e executar

O cliente **não contém credenciais**; a ligação usa `client-settings.json` com `serverAddress: tcp://HOST:10009?cert=SHA256` (ver `android-tls.md`). O servidor Etervale cria um certificado PFX próprio se necessário. O pin SHA-256 é derivado do **certificado público em DER**, não da chave privada.

```bash
adb shell monkey -p com.etervale.game 1
adb shell pidof com.etervale.game
adb logcat -d -b crash -v threadtime | tail -n 150
```

Após aplicar v6, o ícone abre o jogo automaticamente; a `EtervaleSDLActivity` permanece interna. O login Android IME já estava operacional; o v6 liga também o IME ao chat no mundo. Teclado físico OTG/USB depende do teclado ser reconhecido pelo Android e dos eventos SDL2 (ver `android-input-ui.md`).

## 6. Validação e limitações

- Confirmados em equipamento real: `libmgruntime.so`, `SDLActivity`, `SurfaceView`, `.xnb`, assets, TLS/login, seleção de personagem e entrada no mundo.
- Problema pendente: `SIGABRT` no processo `com.etervale.game`, thread `mali-cmar-backe`; sem *backtrace* nativo completo não sabemos qual comando/recurso Vulkan o provoca.
- HUD e menus: o projeto usa posições de 800×600/800×720, ecrãs 2400×1080 tornam os alvos táteis pequenos. O v6 aumenta o **login**, mas o mundo precisa de um canvas lógico escalável, com transformações idênticas para desenho e hit-testing.
- Testar suspensão/retoma, troca de mapas, memória/temperatura, USB teclado, orientação, resolução e teclado de chat.
- Alinhamento de 16 KiB: a compilação inicial emitiu aviso XA0141; rever antes de distribuir para versões Android que o exijam.

### Condições para publicação

O port deve ser testado com o conteúdo gerado numa máquina limpa, os assets legalmente distribuíveis e o certificado TLS de cada servidor, sem depender do IP, dos caches ou dos binários do computador do autor.
