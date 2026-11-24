// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using MonoGame.Interop;

namespace Microsoft.Xna.Framework.Input;

public static partial class Mouse
{
    private static IntPtr PlatformGetWindowHandle()
    {
        // TODO: Multiple window support.

        return PrimaryWindow.Handle;
    }

    private static void PlatformSetWindowHandle(IntPtr windowHandle)
    {
        // TODO: Multiple window support.
    }

    // Android gameplay sets a snapshot on the game thread; desktop never sets this.
    /// <summary>Estado do rato virtual fornecido pelos controlos mobile do Etervale.</summary>
    public static MouseState? EtervaleOverride { get; set; }
    /// <summary>Indica se o Etervale está a executar no Android.</summary>
    public static bool EtervaleAndroid { get; set; }

    private static unsafe MouseState PlatformGetState(GameWindow window)
    {
        // Mouse events keep this updated for each window.
        return EtervaleOverride ?? window.MouseState;
    }

    private static unsafe void PlatformSetPosition(int x, int y)
    {
        // TODO: Multiple window support.

        PrimaryWindow.MouseState.X = x;
        PrimaryWindow.MouseState.Y = y;

        var window = PrimaryWindow as NativeGameWindow;
        if (window == null || window.Handle == null)
            return;

        MGP.Mouse_WarpPosition(window._handle, x, y);
    }

    private static unsafe void PlatformSetCursor(MouseCursor cursor)
    {
        // TODO: Multiple window support?

        var window = PrimaryWindow as NativeGameWindow;
        if (window == null || window.Handle == IntPtr.Zero)
            return;

        MGP.Window_SetCursor(window._handle, (MGP_Cursor*)cursor.Handle);
    }
}
