# Etervale — port RG40XXH / muOS (Linux ARM64)

**Estado em 2026-11-23:** port experimental do cliente .NET 10/MonoGame Vulkan
para a consola **Anbernic RG40XXH**, com muOS **ANDROMEDA**, SoC Allwinner H700,
GPU Mali-G31, cerca de 973 MiB de RAM e ecrã 640×480.

## Componentes

- Executável .NET 10 Linux ARM64 e `libmgruntime.so` **Linux arm64**, compilada
  com SDL2, Vulkan e FAudio para este ambiente. **É diferente da `.so` Android**.
- Apresentação direta Vulkan via MaliSX2 0.6.1 e arranque SDL adaptado ao
  ambiente de vídeo direto; entrada física via `evdev` (componente
  `H700EvdevInput.Native.cs`).
- Redução do perfil gráfico, efeitos e alocação de superfícies/shaders sob
  procura; opções específicas para a RAM reduzida.
- Instalação experimental em `/mnt/sdcard/ports/etervale` no dispositivo.

## Observações verificadas

- Arranque do cliente, comunicação TCP/TLS e login já foram obtidos.
- Uma cena 3D estática chegou a cerca de **619 MiB RSS**.
- O carregamento adicional de malhas animadas/esqueletos levou a falta de
  memória (*OOM*) em testes anteriores. **Não declarar port plenamente jogável**.
- Preservar opções de baixa qualidade e a inicialização diferida dos shaders.

## Separação de plataforma

- `monogame/runtime/bin/linux-arm64/libmgruntime.so`: consola Linux/H700.
- `monogame/runtime/bin/android-arm64/libmgruntime.so`: Android NDK/OPPO.
- Não copiar a biblioteca Linux diretamente para o APK Android.
- Os ajustes partilhados de código devem proteger as dependências de cada SO.

## Código mantido

Principais áreas tocadas durante os testes: `Client/PristonTale.Engine3D/`,
`Client/PristonTale.Gameplay/`, `monogame/framework/MonoGame.Framework/Platform/Native/`,
`monogame/runtime/src/sdl/` e `monogame/runtime/src/vulkan/`. Alguns ficheiros
alterados podem conter correções partilhadas entre Linux H700 e Android; rever
cada diff antes do commit.

## Próximos passos

1. Medir RAM e picos de alocação nas entradas do mundo e nas malhas *skinned*.
2. Avaliar LOD/caches de malhas e texturas e reduzir a memória de vértices.
3. Medir tempos de *frame* e a experiência de controlo físico.
4. Preparar empacotamento reprodutível sem os assets grandes no repositório.
