// ETERVALE_ANDROID_V5: SDL2 native IME access from managed C#.
// Guarded: harmless for Linux/H700 builds and does not affect their ABI.
#if defined(__ANDROID__)
#include <SDL.h>
extern "C" __attribute__((visibility("default")))
void Etervale_Android_StartTextInput()
{
    SDL_StartTextInput();
}
extern "C" __attribute__((visibility("default")))
void Etervale_Android_StopTextInput()
{
    SDL_StopTextInput();
}
#endif
