using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PristonTale.Gameplay.UI.Rendering;

namespace PristonTale.Gameplay.UI;

// One input snapshot per game update; never generates OS mouse or keyboard events.
internal sealed class MobileControls
{
    internal enum Role { None, World, Stick, Toggle, Attack, Skill, Inventory, Skills, Status, Quest, Potion1, Potion2, Potion3, Camera, Settings, Chat, Size, Opacity, Hand, Reset, Done, Back }
    private readonly record struct Finger(Role Role, Vector2 Start, Vector2 Position);
    private readonly record struct Touch(int Type, long Device, long Id, float X, float Y);
    private readonly Dictionary<(long, long), Finger> _fingers = new();
    private readonly Queue<Touch> _events = new();
    private readonly HashSet<(long, long)> _pressedThisFrame = new();
    private readonly HashSet<Keys> _keys = new();
    private Vector2 _cursor, _stick;
    private bool _cursorReady, _expanded = true, _touchAvailable = true, _config, _connected, _wasActive, _waitPad = true;
    private bool _previousConnected, _oldStart, _oldBack, _oldR3, _cameraMode;
    private MouseState _lastRaw;
    private bool _rawReady;
    private bool _lastAvailable;
    private bool _preferPad;
    private int _width, _height;
    private readonly string _settingsPath = Environment.GetEnvironmentVariable("ETERVALE_MOBILE_SETTINGS") ?? "";
    private Options _options = new();
    private sealed class Options { public Options() { } public int Size { get; set; } = 100; public int Opacity { get; set; } = 80; public bool LeftHanded { get; set; } }
    public MobileControls()
    {
        if (!Enabled || _settingsPath.Length == 0) return;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_settingsPath));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("Size", out var size) && size.ValueKind == JsonValueKind.Number && size.TryGetInt32(out var n)) _options.Size = n;
                if (root.TryGetProperty("Opacity", out var opacity) && opacity.ValueKind == JsonValueKind.Number && opacity.TryGetInt32(out var a)) _options.Opacity = a;
                if (root.TryGetProperty("LeftHanded", out var hand) && hand.ValueKind is JsonValueKind.True or JsonValueKind.False) _options.LeftHanded = hand.GetBoolean();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        _options.Size = Math.Clamp(_options.Size, 80, 120); _options.Opacity = Math.Clamp(_options.Opacity, 40, 100);
    }
    public bool Enabled => OperatingSystem.IsAndroid();
    public Vector2 Movement { get; private set; }
    public Vector2 Orbit { get; private set; }
    public bool CloseRequested { get; private set; }
    public bool PanelCloseRequested { get; private set; }
    public bool WorldActionOnly { get; private set; }
    public bool MenuCaptures => _config || _configWasOpen;
    private bool _configWasOpen;
    [DllImport("mgruntime", EntryPoint = "Etervale_Android_PopTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern int PopTouch(out int type, out long device, out long id, out float x, out float y);

    private void Save()
    {
        if (_settingsPath.Length == 0) return;
        try
        {
            using (var stream = File.Create(_settingsPath + ".tmp"))
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject(); writer.WriteNumber("Size",_options.Size); writer.WriteNumber("Opacity",_options.Opacity);
                writer.WriteBoolean("LeftHanded",_options.LeftHanded); writer.WriteEndObject();
            }
            File.Move(_settingsPath + ".tmp", _settingsPath, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
    internal static Vector2 DeadZone(Vector2 v)
    {
        var length = v.Length();
        return !float.IsFinite(length) || length <= .18f ? Vector2.Zero : v / length * Math.Min(1f, (length - .18f) / .82f);
    }
    // Coordinates are logical HUD units. Keep the original bottom 128 units clear.
    internal static Rectangle Bounds(HudLayout l, Role role, int size = 100, bool leftHanded = false)
    {
        int w = l.Width, h = l.Height;
        Rectangle r = role switch
        {
            Role.Toggle or Role.Back => new(w-82, 12, 68, 44), Role.Settings => new(w-82, 62, 68, 44),
            Role.Stick => new(24, h-350, 140, 140), Role.Attack => new(w-110, h-315, 86, 78),
            Role.Skill => new(w-205, h-257, 76, 68), Role.Camera => new(w-285, h-420, 86, 64),
            Role.Inventory => new(w-82, 112, 68, 44), Role.Skills => new(w-82, 162, 68, 44),
            Role.Status => new(w-156, 112, 68, 44), Role.Quest => new(w-156, 162, 68, 44),
            Role.Chat => new(18, 110, 68, 44), Role.Potion1 => new(18, h-405, 52, 40),
            Role.Potion2 => new(76, h-405, 52, 40), Role.Potion3 => new(134, h-405, 52, 40),
            Role.Size => new(w/2-115, h/2-90, 230, 42), Role.Opacity => new(w/2-115, h/2-43, 230, 42),
            Role.Hand => new(w/2-115, h/2+4, 230, 42), Role.Reset => new(w/2-115, h/2+51, 112, 42),
            Role.Done => new(w/2+3, h/2+51, 112, 42), _ => Rectangle.Empty
        };
        if (role is Role.Stick or Role.Attack or Role.Skill or Role.Camera)
        {
            int nw = r.Width * size / 100, nh = r.Height * size / 100;
            r = new Rectangle(r.Center.X-nw/2, r.Center.Y-nh/2, nw, nh);
            if (leftHanded) r.X = w-r.Right;
            r.X = Math.Clamp(r.X, 8, Math.Max(8,w-r.Width-8));
        }
        return r;
    }
    private Rectangle Box(HudLayout l, Role role) => Bounds(l, role, _options.Size, _options.LeftHanded);
    private static readonly Role[] Normal = [Role.Toggle, Role.Settings, Role.Inventory, Role.Skills, Role.Status, Role.Quest, Role.Chat, Role.Stick, Role.Attack, Role.Skill, Role.Camera, Role.Potion1, Role.Potion2, Role.Potion3];
    private static readonly Role[] Config = [Role.Size, Role.Opacity, Role.Hand, Role.Reset, Role.Done];
    private Role Hit(HudLayout l, Point p, bool available, bool backAvailable)
    {
        if (_config) { foreach (var r in Config) if (Box(l,r).Contains(p)) return r; return Role.None; }
        if (!available) return backAvailable && Box(l,Role.Back).Contains(p) ? Role.Back : Role.World;
        if (Box(l,Role.Toggle).Contains(p)) return Role.Toggle;
        if (!_expanded) return Role.World;
        foreach (var r in Normal) if (Box(l,r).Contains(p)) return r;
        return Role.World;
    }
    private void OnPress(Role role)
    {
        switch (role)
        {
            case Role.Toggle: _expanded = !_expanded; break;
            case Role.Back: PanelCloseRequested = true; break;
            case Role.Settings: _config = true; break;
            case Role.Size: _options.Size = _options.Size >= 120 ? 80 : _options.Size+10; Save(); break;
            case Role.Opacity: _options.Opacity = _options.Opacity >= 100 ? 40 : Math.Min(100,_options.Opacity+15); Save(); break;
            case Role.Hand: _options.LeftHanded = !_options.LeftHanded; Save(); break;
            case Role.Reset: _options = new(); Save(); break;
            case Role.Done: _config = false; break;
        }
    }
    private void ReadEvents(bool active)
    {
        if (!_touchAvailable) return;
        try
        {
            while (PopTouch(out var type, out var device, out var id, out var x, out var y) != 0)
            {
                if (type == 4 || _events.Count >= 512) { _events.Clear(); _fingers.Clear(); if (type == 4) continue; }
                if (active && float.IsFinite(x) && float.IsFinite(y)) _events.Enqueue(new(type,device,id,x,y));
            }
        }
        catch (Exception e) when (e is EntryPointNotFoundException or DllNotFoundException) { _touchAvailable = false; _events.Clear(); _fingers.Clear(); }
    }
    private void ProcessEvents(HudLayout l, bool available, bool backAvailable)
    {
        _pressedThisFrame.Clear();
        while (_events.TryPeek(out var t))
        {
            var id = (t.Device,t.Id);
            // Preserve a DOWN/UP between two updates as one frame of held input.
            if (_pressedThisFrame.Contains(id) && t.Type != 2) break;
            _events.Dequeue();
            if (t.Type == 3) { _fingers.Remove(id); _pressedThisFrame.Add(id); continue; }
            var p = new Vector2(Math.Clamp(t.X,0,1)*(l.ViewportWidth-1)/l.PixelScale, Math.Clamp(t.Y,0,1)*(l.ViewportHeight-1)/l.PixelScale);
            if (t.Type == 1)
            {
                var wasPad = _preferPad;
                _preferPad = false;
                var point = new Point((int)p.X,(int)p.Y);
                var role = wasPad && available && !_config && !Box(l,Role.Toggle).Contains(point) ? Role.World : Hit(l,point,available,backAvailable);
                _fingers[id] = new(role,p,p); _pressedThisFrame.Add(id);
                if (wasPad && role == Role.Toggle) _expanded = true; else OnPress(role);
            }
            else if (t.Type == 2 && _fingers.TryGetValue(id,out var f)) _fingers[id] = f with { Position=p };
        }
    }
    public MouseState HudMouse(MouseState mouse) => WorldActionOnly ? Pointer(mouse,false,false) : mouse;
    public KeyboardState Merge(KeyboardState keyboard)
    {
        if (_keys.Count == 0) return keyboard;
        var keys = new HashSet<Keys>(keyboard.GetPressedKeys()); keys.UnionWith(_keys); return new KeyboardState(keys.ToArray());
    }
    public void Update(Viewport viewport, GameTime time, bool active, bool available, bool typing, bool gameplay = true, bool backAvailable = false)
    {
        if (!Enabled) return;
        _keys.Clear(); Movement=Orbit=_stick=Vector2.Zero; CloseRequested=PanelCloseRequested=WorldActionOnly=false; _configWasOpen=_config;
        Mouse.EtervaleOverride=null; var raw=Mouse.GetState(); var l=HudLayout.Of(viewport);
        if (!_cursorReady || _width!=viewport.Width || _height!=viewport.Height)
        { _cursor=new Vector2(viewport.Width/2f,viewport.Height/2f); _cursorReady=true; _width=viewport.Width; _height=viewport.Height; _fingers.Clear(); }
        var pad=GamePad.GetState(PlayerIndex.One,GamePadDeadZone.None); _connected=pad.IsConnected;
        ReadEvents(active);
        if (!active)
        { _config=false; _fingers.Clear(); _events.Clear(); _wasActive=false; _waitPad=true; Mouse.EtervaleOverride=new MouseState((int)_cursor.X,(int)_cursor.Y,raw.ScrollWheelValue,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released); return; }
        if (!_wasActive || _connected!=_previousConnected) { _waitPad=true; _preferPad=_connected; }
        _wasActive=true; _previousConnected=_connected;
        bool a=pad.IsButtonDown(Buttons.A), b=pad.IsButtonDown(Buttons.B), start=pad.IsButtonDown(Buttons.Start), back=pad.IsButtonDown(Buttons.Back), r3=pad.IsButtonDown(Buttons.RightStick);
        bool neutral= !a&&!b&&!start&&!back&&!r3&&!pad.IsButtonDown(Buttons.X)&&!pad.IsButtonDown(Buttons.Y)
            && !pad.IsButtonDown(Buttons.LeftShoulder)&&!pad.IsButtonDown(Buttons.RightShoulder)&&!pad.IsButtonDown(Buttons.LeftStick)
            && !pad.IsButtonDown(Buttons.DPadUp)&&!pad.IsButtonDown(Buttons.DPadDown)&&!pad.IsButtonDown(Buttons.DPadLeft)&&!pad.IsButtonDown(Buttons.DPadRight)
            && pad.Triggers.Left<.3f&&pad.Triggers.Right<.3f&&pad.ThumbSticks.Left.LengthSquared()<.04f&&pad.ThumbSticks.Right.LengthSquared()<.04f;
        if (_waitPad && neutral) _waitPad=false;
        if (_connected&&!_waitPad&&!neutral&&_fingers.Count==0) _preferPad=true;
        if (!gameplay || !available || typing) _config=false;
        if (_lastAvailable && !available) _fingers.Clear();
        _lastAvailable=available;
        ProcessEvents(l,gameplay&&available&&!typing,gameplay&&backAvailable);
        bool padEnabled=_connected&&!_waitPad&&_preferPad;
        float dt=Math.Clamp((float)time.ElapsedGameTime.TotalSeconds,0,.05f);
        bool left=false,right=false;
        if (padEnabled)
        {
            CloseRequested=(start&&!_oldStart)||(back&&!_oldBack);
            if (CloseRequested&&_config) { _config=false; CloseRequested=false; }
            if (gameplay&&available&&!typing&&!_config&&r3&&!_oldR3) _cameraMode=!_cameraMode;
            var aim=DeadZone(pad.ThumbSticks.Right);
            if (gameplay&&available&&!typing&&!_config) { Movement=DeadZone(pad.ThumbSticks.Left); if (_cameraMode) Orbit=aim; }
            else if (!gameplay) aim+=DeadZone(pad.ThumbSticks.Left);
            if (!gameplay||!available||!_cameraMode||_config) _cursor+=new Vector2(aim.X,-aim.Y)*viewport.Height*1.4f*dt;
            left=a||pad.Triggers.Left>.45f; right=b||pad.Triggers.Right>.45f;
            if (gameplay&&available&&!typing&&!_config)
            {
                if(pad.IsButtonDown(Buttons.Y)) _keys.Add(Keys.I); if(pad.IsButtonDown(Buttons.X)) _keys.Add(Keys.S);
                if(pad.IsButtonDown(Buttons.DPadUp)) _keys.Add(Keys.C); if(pad.IsButtonDown(Buttons.DPadLeft)) _keys.Add(Keys.D1);
                if(pad.IsButtonDown(Buttons.DPadDown)) _keys.Add(Keys.D2); if(pad.IsButtonDown(Buttons.DPadRight)) _keys.Add(Keys.D3);
                if(pad.IsButtonDown(Buttons.LeftShoulder)) _keys.Add(Keys.F1); if(pad.IsButtonDown(Buttons.RightShoulder)) _keys.Add(Keys.F2);
                if(pad.IsButtonDown(Buttons.LeftStick)) _keys.Add(Keys.R);
            }
        }
        _oldStart=start; _oldBack=back; _oldR3=r3;
        bool world=false, stick=false, camera=false;
        foreach(var f in _fingers.Values)
        {
            if(f.Role==Role.World&&!world&&!_config)
            { _cursor=f.Position*l.PixelScale; left=true; world=true; continue; }
            if(!gameplay||!available||typing||_config||!_expanded) continue;
            switch(f.Role)
            {
                case Role.Stick when !stick: _stick=DeadZone(new Vector2(f.Position.X-Box(l,Role.Stick).Center.X,Box(l,Role.Stick).Center.Y-f.Position.Y)/(Box(l,Role.Stick).Width*.42f)); Movement=_stick; stick=true; break;
                case Role.Camera when !camera: Orbit=DeadZone(new Vector2(f.Position.X-f.Start.X,f.Start.Y-f.Position.Y)/32f); camera=true; break;
                case Role.Attack: left=true; WorldActionOnly=true; break;
                case Role.Skill: right=true; WorldActionOnly=true; break;
                case Role.Inventory: _keys.Add(Keys.I); break; case Role.Skills: _keys.Add(Keys.S); break;
                case Role.Status: _keys.Add(Keys.C); break; case Role.Quest: _keys.Add(Keys.Q); break;
                case Role.Chat: _keys.Add(Keys.Enter); break; case Role.Potion1: _keys.Add(Keys.D1); break;
                case Role.Potion2: _keys.Add(Keys.D2); break; case Role.Potion3: _keys.Add(Keys.D3); break;
            }
        }
        if(world) WorldActionOnly=false;
        if(MenuCaptures) { left=right=false; Movement=Orbit=Vector2.Zero; _keys.Clear(); }
        // Native SDL filters touch-generated mouse events. Physical mouse still works.
        if(!_touchAvailable&&!padEnabled) { Mouse.EtervaleOverride=null; return; }
        bool rawLeft=raw.LeftButton==ButtonState.Pressed, rawRight=raw.RightButton==ButtonState.Pressed;
        if(!world&&_fingers.Count==0&&!MenuCaptures&&(_rawReady&&raw!=_lastRaw||rawLeft||rawRight))
        { _preferPad=false; _cursor=new Vector2(raw.X,raw.Y); left=rawLeft; right=rawRight; }
        _lastRaw=raw; _rawReady=true;
        _cursor.X=Math.Clamp(_cursor.X,0,Math.Max(0,viewport.Width-1)); _cursor.Y=Math.Clamp(_cursor.Y,0,Math.Max(0,viewport.Height-1));
        Mouse.EtervaleOverride=Pointer(raw,left,right);
    }
    private MouseState Pointer(MouseState raw,bool left,bool right) => new((int)_cursor.X,(int)_cursor.Y,raw.ScrollWheelValue,
        left?ButtonState.Pressed:ButtonState.Released,_fingers.Count==0&&!_preferPad&&!MenuCaptures?raw.MiddleButton:ButtonState.Released,right?ButtonState.Pressed:ButtonState.Released,ButtonState.Released,ButtonState.Released);
    private void Button(HudPainter painter,Rectangle r,string title)
    {
        int a=255*_options.Opacity/100; painter.ColorBox(r,172,142,82,a);
        painter.ColorBox(new Rectangle(r.X+2,r.Y+2,r.Width-4,r.Height-4),20,19,16,a);
        painter.TextCentered(title,r.Left,r.Right,r.Center.Y-painter.TextHeight/2,new Color(230,205,151));
    }
    public void Draw(HudPainter painter,HudLayout l,bool available,bool backAvailable)
    {
        if(!Enabled||!_touchAvailable) return;
        if(!available) { if(backAvailable) Button(painter,Box(l,Role.Back),"VOLTAR"); return; }
        if(_config)
        {
            painter.ColorBox(new Rectangle(l.Width/2-130,l.Height/2-120,260,230),16,15,12,240);
            Button(painter,Box(l,Role.Size),$"TAMANHO {_options.Size}%"); Button(painter,Box(l,Role.Opacity),$"OPACIDADE {_options.Opacity}%");
            Button(painter,Box(l,Role.Hand),_options.LeftHanded?"CANHOTO":"DESTRO"); Button(painter,Box(l,Role.Reset),"RESET"); Button(painter,Box(l,Role.Done),"OK"); return;
        }
        Button(painter,Box(l,Role.Toggle),_preferPad?(_cameraMode?"PAD CAM":"PAD AIM"):_expanded?"CTRL -":"CTRL +");
        if(!_expanded||_preferPad) return;
        var r=Box(l,Role.Stick); painter.ColorBox(r,21,20,17,130*_options.Opacity/100);
        int x=r.Center.X+(int)(_stick.X*r.Width*.3f), y=r.Center.Y-(int)(_stick.Y*r.Height*.3f);
        painter.ColorBox(new Rectangle(x-18,y-18,36,36),177,148,85,220*_options.Opacity/100);
        foreach(var role in Normal)
        {
            string title=role switch { Role.Settings=>"CONFIG",Role.Inventory=>"BOLSA",Role.Skills=>"SKILLS",Role.Status=>"STATUS",Role.Quest=>"QUEST",Role.Chat=>"CHAT",Role.Attack=>"ATACAR",Role.Skill=>"SKILL",Role.Camera=>"CAMERA",Role.Potion1=>"P1",Role.Potion2=>"P2",Role.Potion3=>"P3",_=>"" };
            if(title.Length>0) Button(painter,Box(l,role),title);
        }
    }
    public void Reset(bool preservePointer = false)
    {
        if(!Enabled)return;
        if(!preservePointer) Mouse.EtervaleOverride=null; _events.Clear(); _fingers.Clear(); _keys.Clear(); _waitPad=true; _wasActive=false; _config=false; _configWasOpen=false; _cursorReady=false; Movement=Orbit=Vector2.Zero; WorldActionOnly=CloseRequested=PanelCloseRequested=false; _lastAvailable=false; _rawReady=false;
    }
}
