// ETERVALE_H700_EVDEV_V1 - Linux evdev input for framebuffer/VK_KHR_display.
// Opt-in: ETERVALE_H700_INPUT=1. Optional key event diagnostics: ETERVALE_H700_INPUT_TRACE=1.
// No uinput/SDL dependency. Events are read on the game thread before Game.Tick().
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;

namespace Microsoft.Xna.Framework;

internal static class H700EvdevInput
{
    private const int O_RDONLY = 0;
    private const int O_NONBLOCK = 0x800; // Linux
    private const int EventSize = 24; // Linux aarch64: timeval(16)+type(2)+code(2)+value(4)
    private const ushort EV_KEY = 1;
    private const ushort EV_REL = 2;
    private const ushort EV_ABS = 3;
    private const int BTN_LEFT = 272;
    private const int BTN_RIGHT = 273;
    private const int BTN_SOUTH = 304;
    private const int BTN_EAST = 305;
    private const int BTN_NORTH = 307;
    private const int BTN_WEST = 308;
    private const int BTN_SELECT = 314;
    private const int BTN_START = 315;
    private const int BTN_DPAD_UP = 544;
    private const int BTN_DPAD_DOWN = 545;
    private const int BTN_DPAD_LEFT = 546;
    private const int BTN_DPAD_RIGHT = 547;

    private static readonly bool Enabled = Environment.GetEnvironmentVariable("ETERVALE_H700_INPUT") == "1";
    private static readonly bool Trace = Environment.GetEnvironmentVariable("ETERVALE_H700_INPUT_TRACE") == "1";
    private static readonly List<Device> Devices = new List<Device>();
    private static readonly byte[] Buffer = new byte[EventSize];
    private static int Frames;
    private static int PointerX = 320;
    private static int PointerY = 240;
    private static bool LeftDown;
    private static bool RightDown;
    private static bool HatLeft, HatRight, HatUp, HatDown;
    private static bool BtnLeft, BtnRight, BtnUp, BtnDown;
    private static int AxisX, AxisY;
    private static bool Shift, Caps;

    private sealed class Device
    {
        public readonly int Fd;
        public readonly string Path;
        public readonly string Name;
        public Device(int fd, string path, string name) { Fd=fd; Path=path; Name=name; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AbsInfo
    {
        public int Value, Minimum, Maximum, Fuzz, Flat, Resolution;
    }
    [DllImport("libc", EntryPoint="open", SetLastError=true)]
    private static extern int Open(string path, int flags);
    [DllImport("libc", EntryPoint="read", SetLastError=true)]
    private static extern long Read(int fd, byte[] dst, ulong count);
    [DllImport("libc", EntryPoint="close", SetLastError=true)]
    private static extern int Close(int fd);
    [DllImport("libc", EntryPoint="ioctl", SetLastError=true)]
    private static extern int Ioctl(int fd, ulong request, out AbsInfo info);

    public static void Poll(NativeGameWindow window)
    {
        if (!Enabled || window == null) return;
        if (Frames == 0)
            Console.WriteLine("[H700 INPUT] evdev ativado (USB teclado/rato + botoes/analógico); cursor 640x480.");
        // USB devices may be plugged after game start; rescan roughly every 4 seconds.
        if ((Frames++ % 120) == 0) Discover();
        for (int d = Devices.Count-1; d>=0; d--)
        {
            var dev=Devices[d];
            bool lost=false;
            // Strict per-frame budget protects rendering from event floods.
            for (int i=0; i<256; i++)
            {
                long n=Read(dev.Fd, Buffer, EventSize);
                if (n==EventSize)
                {
                    ushort type=BitConverter.ToUInt16(Buffer,16);
                    ushort code=BitConverter.ToUInt16(Buffer,18);
                    int value=BitConverter.ToInt32(Buffer,20);
                    Handle(dev, window, type, code, value);
                }
                else
                {
                    if (n==0 || (n<0 && Marshal.GetLastWin32Error()!=11)) lost=true;
                    break;
                }
            }
            if (lost)
            {
                Close(dev.Fd);
                Devices.RemoveAt(d);
                if (Trace) Console.WriteLine("[H700 INPUT] desligado "+dev.Path);
            }
        }
        int dx=(HatRight ? 1 : 0)-(HatLeft ? 1 : 0)+(BtnRight ? 1 : 0)-(BtnLeft ? 1 : 0)+AxisX;
        int dy=(HatDown ? 1 : 0)-(HatUp ? 1 : 0)+(BtnDown ? 1 : 0)-(BtnUp ? 1 : 0)+AxisY;
        if (dx!=0) PointerX+=Math.Sign(dx)*5;
        if (dy!=0) PointerY+=Math.Sign(dy)*5;
        int width=Math.Max(1,window.ClientBounds.Width), height=Math.Max(1,window.ClientBounds.Height);
        PointerX=Math.Max(0,Math.Min(width-1,PointerX));
        PointerY=Math.Max(0,Math.Min(height-1,PointerY));
        window.MouseState.X=PointerX;
        window.MouseState.Y=PointerY;
        window.MouseState.LeftButton=LeftDown ? ButtonState.Pressed : ButtonState.Released;
        window.MouseState.RightButton=RightDown ? ButtonState.Pressed : ButtonState.Released;
    }

    private static void Discover()
    {
        if (!Directory.Exists("/dev/input"))
        {
            if (Frames<=1) Console.WriteLine("[H700 INPUT] /dev/input indisponivel.");
            return;
        }
        foreach (var path in Directory.GetFiles("/dev/input","event*"))
        {
            if (Devices.Exists(d => d.Path == path)) continue;
            int fd=Open(path,O_RDONLY|O_NONBLOCK);
            if (fd<0) continue;
            string name="";
            try
            {
                name=File.ReadAllText("/sys/class/input/"+System.IO.Path.GetFileName(path)+"/device/name").Trim();
            }
            catch (Exception) { name="unknown"; }
            Devices.Add(new Device(fd,path,name));
            Console.WriteLine("[H700 INPUT] dispositivo "+path+" : "+name);
        }
        if (Devices.Count==0 && Frames<=1)
            Console.WriteLine("[H700 INPUT] Nenhum event* acessivel. Verificar permissoes/root.");
    }

    private static void Handle(Device device, NativeGameWindow window, ushort type, ushort code, int value)
    {
        if (type==EV_REL)
        {
            switch (code)
            {
                case 0: PointerX+=value; break; // REL_X
                case 1: PointerY+=value; break; // REL_Y
                case 8: window.MouseState.ScrollWheelValue+=value*120; break;
            }
            return;
        }
        if (type==EV_ABS)
        {
            if (Trace && value!=0) Console.WriteLine("[H700 INPUT RAW] "+device.Name+" ABS "+code+"="+value);
            switch (code)
            {
                case 0: AxisX=AxisDirection(device.Fd,code,value); break; // ABS_X
                case 1: AxisY=AxisDirection(device.Fd,code,value); break; // ABS_Y
                case 16: HatLeft=value<0; HatRight=value>0; break; // ABS_HAT0X
                case 17: HatUp=value<0; HatDown=value>0; break; // ABS_HAT0Y
            }
            return;
        }
        if (type!=EV_KEY) return;
        if (Trace && value!=2)
            Console.WriteLine("[H700 INPUT RAW] "+device.Name+" KEY "+code+"="+value);
        bool pressed=value!=0;
        switch (code)
        {
            case BTN_LEFT: LeftDown=pressed; return;
            case BTN_RIGHT: RightDown=pressed; return;
            case BTN_SOUTH:
                // Click only; Enter on every click would submit the login prematurely.
                LeftDown=pressed;
                return;
            case BTN_EAST:
                RightDown=pressed;
                return;
            case BTN_NORTH:
                Key(window,Keys.Back,pressed,'\0');
                return;
            case BTN_WEST:
                Key(window,Keys.Tab,pressed,'\0');
                return;
            case BTN_START:
                Key(window,Keys.Enter,pressed,'\0');
                return;
            case BTN_SELECT:
                Key(window,Keys.Tab,pressed,'\0');
                return;
            case BTN_DPAD_UP: BtnUp=pressed; return;
            case BTN_DPAD_DOWN: BtnDown=pressed; return;
            case BTN_DPAD_LEFT: BtnLeft=pressed; return;
            case BTN_DPAD_RIGHT: BtnRight=pressed; return;
        }
        // muOS-Keys can expose the D-pad as standard keyboard cursor keys.
        bool muKeys=device.Name.IndexOf("muOS-Keys",StringComparison.OrdinalIgnoreCase)>=0;
        if (muKeys)
        {
            switch (code)
            {
                case 103: BtnUp=pressed; return;
                case 108: BtnDown=pressed; return;
                case 105: BtnLeft=pressed; return;
                case 106: BtnRight=pressed; return;
            }
        }
        var mapped=MapKey(code);
        if (mapped==Keys.None) return;
        if (code==42 || code==54) Shift=pressed;
        if (code==58 && value==1) Caps=!Caps;
        char character=pressed ? Character(code) : (char)0;
        Key(window,mapped,pressed,character);
    }

    private static int AxisDirection(int fd,int code,int value)
    {
        AbsInfo info;
        // EVIOCGABS(n): Linux _IOR('E', 0x40+n, struct input_absinfo[24])
        if (Ioctl(fd,0x80184540UL+(ulong)code,out info)==0 && info.Maximum>info.Minimum)
        {
            double center=((double)info.Maximum+info.Minimum)/2;
            double margin=(info.Maximum-info.Minimum)*0.20;
            return value<center-margin ? -1 : value>center+margin ? 1 : 0;
        }
        return 0;
    }

    private static void Key(NativeGameWindow window, Keys key, bool down, char character)
    {
        if (down)
        {
            if (!Keyboard.Keys.Contains(key))
                Keyboard.Keys.Add(key);
            window.OnKeyDown(new InputKeyEventArgs(key));
            if (character!='\0' && window.IsTextInputHandled)
                window.OnTextInput(new TextInputEventArgs(character,key));
        }
        else
        {
            Keyboard.Keys.Remove(key);
            window.OnKeyUp(new InputKeyEventArgs(key));
        }
    }

    private static Keys MapKey(int code)
    {
        switch (code)
        {
            case 1: return Keys.Escape;
            case 2: return Keys.D1; case 3: return Keys.D2; case 4: return Keys.D3;
            case 5: return Keys.D4; case 6: return Keys.D5; case 7: return Keys.D6;
            case 8: return Keys.D7; case 9: return Keys.D8; case 10: return Keys.D9;
            case 11: return Keys.D0;
            case 12: return Keys.OemMinus; case 13: return Keys.OemPlus;
            case 14: return Keys.Back; case 15: return Keys.Tab;
            case 16: return Keys.Q; case 17: return Keys.W; case 18: return Keys.E;
            case 19: return Keys.R; case 20: return Keys.T; case 21: return Keys.Y;
            case 22: return Keys.U; case 23: return Keys.I; case 24: return Keys.O;
            case 25: return Keys.P; case 26: return Keys.OemOpenBrackets;
            case 27: return Keys.OemCloseBrackets; case 28: return Keys.Enter;
            case 29: return Keys.LeftControl;
            case 30: return Keys.A; case 31: return Keys.S; case 32: return Keys.D;
            case 33: return Keys.F; case 34: return Keys.G; case 35: return Keys.H;
            case 36: return Keys.J; case 37: return Keys.K; case 38: return Keys.L;
            case 39: return Keys.OemSemicolon; case 40: return Keys.OemQuotes;
            case 41: return Keys.OemTilde; case 42: return Keys.LeftShift;
            case 43: return Keys.OemPipe; case 44: return Keys.Z; case 45: return Keys.X;
            case 46: return Keys.C; case 47: return Keys.V; case 48: return Keys.B;
            case 49: return Keys.N; case 50: return Keys.M;
            case 51: return Keys.OemComma; case 52: return Keys.OemPeriod;
            case 53: return Keys.OemQuestion; case 54: return Keys.RightShift;
            case 56: return Keys.LeftAlt; case 57: return Keys.Space;
            case 58: return Keys.CapsLock;
            case 59: return Keys.F1; case 60: return Keys.F2; case 61: return Keys.F3;
            case 62: return Keys.F4; case 63: return Keys.F5; case 64: return Keys.F6;
            case 65: return Keys.F7; case 66: return Keys.F8; case 67: return Keys.F9;
            case 68: return Keys.F10;
            case 69: return Keys.NumLock;
            case 71: return Keys.NumPad7; case 72: return Keys.NumPad8;
            case 73: return Keys.NumPad9; case 74: return Keys.Subtract;
            case 75: return Keys.NumPad4; case 76: return Keys.NumPad5;
            case 77: return Keys.NumPad6; case 78: return Keys.Add;
            case 79: return Keys.NumPad1; case 80: return Keys.NumPad2;
            case 81: return Keys.NumPad3; case 82: return Keys.NumPad0;
            case 83: return Keys.Decimal;
            case 87: return Keys.F11; case 88: return Keys.F12;
            case 96: return Keys.Enter; case 97: return Keys.RightControl;
            case 98: return Keys.Divide;
            case 100: return Keys.RightAlt;
            case 102: return Keys.Home; case 103: return Keys.Up;
            case 104: return Keys.PageUp; case 105: return Keys.Left;
            case 106: return Keys.Right; case 107: return Keys.End;
            case 108: return Keys.Down; case 109: return Keys.PageDown;
            case 110: return Keys.Insert; case 111: return Keys.Delete;
            default: return Keys.None;
        }
    }

    // Basic US-layout typing, enough for login/password. For locale/IME, integrate a
    // real keyboard layout service in a future build.
    private static char Character(int code)
    {
        // letters, EV_KEY code to ASCII
        const string alphabet="qwertyuiopasdfghjklzxcvbnm";
        int[] codes={16,17,18,19,20,21,22,23,24,25,30,31,32,33,34,35,36,37,38,44,45,46,47,48,49,50};
        for (int i=0;i<codes.Length;i++)
            if (code==codes[i])
                return Shift^Caps ? char.ToUpperInvariant(alphabet[i]) : alphabet[i];
        const string numbers="1234567890", shifted="!@#$%^&*()";
        if (code>=2 && code<=11) return Shift ? shifted[code-2] : numbers[code-2];
        switch (code)
        {
            case 57: return ' ';
            case 12: return Shift?'_':'-';
            case 13: return Shift?'+':'=';
            case 26: return Shift?'{':'[';
            case 27: return Shift?'}':']';
            case 39: return Shift?':':';';
            case 40: return Shift?'"':'\'';
            case 41: return Shift?'~':'`';
            case 43: return Shift?'|':'\\';
            case 51: return Shift?'<':',';
            case 52: return Shift?'>':'.';
            case 53: return Shift?'?':'/';
            case 14: return '\b';
            case 15: return '\t';
            case 28: return '\r';
            default: return '\0';
        }
    }
}
