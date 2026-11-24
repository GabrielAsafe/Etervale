// Android SDL event bridge. Producer and consumer both run on the game thread.
#if defined(__ANDROID__)
#include <SDL.h>
#include <SDL_vulkan.h>
#include <stdint.h>
#include <deque>
struct TouchEvent { int type; int64_t device, finger; float x, y; };
static std::deque<TouchEvent> events;
static constexpr size_t Capacity = 512;
void Etervale_Android_RecordTouch(const SDL_Event& e)
{
    if (e.type == SDL_APP_WILLENTERBACKGROUND || e.type == SDL_APP_DIDENTERBACKGROUND ||
        (e.type == SDL_WINDOWEVENT && e.window.event == SDL_WINDOWEVENT_FOCUS_LOST))
    {
        events.clear(); events.push_back({4, 0, 0, 0, 0}); return;
    }
    if (e.type != SDL_FINGERDOWN && e.type != SDL_FINGERMOTION && e.type != SDL_FINGERUP) return;
    const auto& f = e.tfinger;
    if (SDL_GetTouchDeviceType(f.touchId) != SDL_TOUCH_DEVICE_DIRECT) return;
    const int type = e.type == SDL_FINGERDOWN ? 1 : e.type == SDL_FINGERMOTION ? 2 : 3;
    TouchEvent value{type, static_cast<int64_t>(f.touchId), static_cast<int64_t>(f.fingerId), f.x, f.y};
    if (type == 2 && !events.empty() && events.back().type == 2 &&
        events.back().device == value.device && events.back().finger == value.finger)
    { events.back() = value; return; }
    if (events.size() >= Capacity) { events.clear(); events.push_back({4, 0, 0, 0, 0}); }
    events.push_back(value);
}
extern "C" __attribute__((visibility("default")))
int Etervale_Android_PopTouch(int* type, int64_t* device, int64_t* finger, float* x, float* y)
{
    if (!type || !device || !finger || !x || !y || events.empty()) return 0;
    auto e = events.front(); events.pop_front();
    *type=e.type; *device=e.device; *finger=e.finger; *x=e.x; *y=e.y; return 1;
}
extern "C" __attribute__((visibility("default")))
int Etervale_Android_GetDrawableSize(int* width, int* height)
{
    if (!width || !height) return 0;
    SDL_Window* window = SDL_GetKeyboardFocus();
    if (!window) window = SDL_GetMouseFocus();
    if (!window) return 0;
    SDL_Vulkan_GetDrawableSize(window, width, height);
    return *width > 0 && *height > 0;
}
#endif
