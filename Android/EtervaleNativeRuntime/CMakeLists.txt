cmake_minimum_required(VERSION 3.20)
project(etervale_mgruntime_android LANGUAGES C CXX)

if(NOT DEFINED ETERVALE_NATIVE_SOURCE OR NOT DEFINED ETERVALE_SDL_STATIC OR NOT DEFINED ETERVALE_FAUDIO_STATIC)
    message(FATAL_ERROR "ETERVALE_NATIVE_SOURCE, ETERVALE_SDL_STATIC e ETERVALE_FAUDIO_STATIC são obrigatórios")
endif()

file(GLOB_RECURSE NATIVE_CPP CONFIGURE_DEPENDS
    "${ETERVALE_NATIVE_SOURCE}/common/*.cpp"
    "${ETERVALE_NATIVE_SOURCE}/sdl/*.cpp"
    "${ETERVALE_NATIVE_SOURCE}/vulkan/*.cpp"
    "${ETERVALE_NATIVE_SOURCE}/faudio/*.cpp")

add_library(mgruntime SHARED ${NATIVE_CPP})
target_compile_features(mgruntime PRIVATE cxx_std_17)
option(ETERVALE_VK_VALIDATION "Enable packaged Android Vulkan validation diagnostics" OFF)
if(ETERVALE_VK_VALIDATION)
    target_compile_definitions(mgruntime PRIVATE ETERVALE_VK_VALIDATION)
endif()
target_compile_definitions(mgruntime PRIVATE DLL_EXPORT MG_SDL2 MG_VULKAN MG_FAUDIO VK_USE_PLATFORM_ANDROID_KHR NDEBUG)
target_include_directories(mgruntime PRIVATE
    "${ETERVALE_NATIVE_SOURCE}/include"
    "${ETERVALE_NATIVE_SOURCE}/external/stb"
    "${ETERVALE_NATIVE_SOURCE}/external/volk"
    "${ETERVALE_NATIVE_SOURCE}/external/vma/include"
    "${ETERVALE_NATIVE_SOURCE}/external/vulkan-headers/include"
    "${ETERVALE_NATIVE_SOURCE}/external/sdl2/sdl/include"
    "${ETERVALE_NATIVE_SOURCE}/external/faudio/include")

# Static SDL2 and FAudio are linked into one Android .so, avoiding
# accidental Linux/glibc dependencies. Android native libs are supplied by NDK.
target_link_libraries(mgruntime PRIVATE
    "${ETERVALE_FAUDIO_STATIC}"
    "${ETERVALE_SDL_STATIC}"
    android log dl m z OpenSLES EGL GLESv1_CM GLESv2)
set_target_properties(mgruntime PROPERTIES OUTPUT_NAME mgruntime)
