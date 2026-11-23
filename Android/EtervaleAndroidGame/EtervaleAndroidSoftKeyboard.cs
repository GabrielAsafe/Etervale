// ETERVALE_ANDROID_V5: SDL2 Android IME, no Java-to-managed callbacks.
using System;
using System.Runtime.InteropServices;
using Android.Util;

namespace EtervaleAndroidGame;

internal static class EtervaleAndroidSoftKeyboard
{
    [DllImport("mgruntime", EntryPoint = "Etervale_Android_StartTextInput", CallingConvention = CallingConvention.Cdecl)]
    private static extern void StartTextInput();

    [DllImport("mgruntime", EntryPoint = "Etervale_Android_StopTextInput", CallingConvention = CallingConvention.Cdecl)]
    private static extern void StopTextInput();

    public static void SetVisible(bool visible)
    {
        try
        {
            if (visible)
                StartTextInput();
            else
                StopTextInput();
            Log.Info("ETERVALE_3D", visible ? "Android IME: abrir" : "Android IME: fechar");
        }
        catch (DllNotFoundException ex)
        {
            Log.Error("ETERVALE_3D", "Keyboard SDL: mgruntime nao encontrada: " + ex.Message);
        }
        catch (EntryPointNotFoundException ex)
        {
            Log.Error("ETERVALE_3D", "Keyboard SDL: export nativo nao encontrado: " + ex.Message);
        }
    }
}
