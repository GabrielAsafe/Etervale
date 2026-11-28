using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PristonTale.Gameplay.UI.Rendering;
using PristonTale.Simulation.Characters; // ETERVALE_MOBILE_HUD_EDITOR_V33

namespace PristonTale.Gameplay.UI;

// One input snapshot per game update; never generates OS mouse or keyboard events.
internal sealed class MobileControls
{
    internal enum Role { None, World, Stick, Toggle, Attack, Skill, Inventory, Skills, Status, Quest, Potion1, Potion2, Potion3, Camera, Settings, ControlSettings, Chat, Size, Opacity, Hand, Reset, Done, Back, TargetNext, ContextCamera,
        Menu, Loot, Map, Party, Run, System, Talk, Edit, EditSave, EditDefault, Vitals, Minimap, SkillF1, SkillF2, SkillF3, SkillF4, SkillF5, SkillF6, SkillF7, SkillF8, Diagnostics }
    private readonly record struct Finger(Role Role, Vector2 Start, Vector2 Position);
    private readonly record struct Touch(int Type, long Device, long Id, float X, float Y);
    private readonly Dictionary<(long, long), Finger> _fingers = new();
    private readonly Queue<Touch> _events = new();
    private readonly HashSet<(long, long)> _pressedThisFrame = new();
    private readonly HashSet<Keys> _keys = new();
    private readonly Dictionary<int, Keys[]> _mergedKeyBuffers = new();
    private Vector2 _cursor, _stick;
    private bool _cursorReady, _expanded = true, _touchAvailable = true, _config, _connected, _wasActive, _waitPad = true;
    private bool _previousConnected, _oldStart, _oldBack, _oldR3, _cameraMode;
    private MouseState _lastRaw;
    private bool _rawReady;
    private bool _lastAvailable;
    private bool _preferPad;
    private bool _editing; // ETERVALE_MOBILE_HUD_EDITOR_V33
    private (long, long)? _editingFinger;
    private Role _editingRole;
    private Vector2 _editingGrab;
    private readonly Dictionary<string,Vector2> _positions=new(StringComparer.Ordinal);
    private bool _moreOpen; // ETERVALE_MOBILE_UI_V32
    private bool _touchCameraMode; // ETERVALE_MOBILE_CAMERA_CONTEXT_V31
    private (long, long)? _cameraDragFinger;
    private Vector2 _lastCameraDrag;
    private bool _multiWorldGesture; // ETERVALE_MOBILE_TARGET_LOOT_V28
    private float? _previousPinchSeparation; // ETERVALE_MOBILE_INPUT_V24: duas pontas no mundo
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
                // ETERVALE_MOBILE_HUD_EDITOR_V33: centros normalizados, independentes da resolução.
                if (root.TryGetProperty("Positions",out var points) && points.ValueKind==JsonValueKind.Object)
                    foreach(var item in points.EnumerateObject())
                    {
                        if(!Enum.TryParse<Role>(item.Name,out var role)||!Movable(role))continue;
                        var xy=item.Value;
                        if(xy.ValueKind!=JsonValueKind.Array||xy.GetArrayLength()!=2)continue;
                        if(xy[0].TryGetSingle(out var x)&&xy[1].TryGetSingle(out var y)
                           && float.IsFinite(x)&&float.IsFinite(y)&&x>=0&&x<=1&&y>=0&&y<=1)
                            _positions[item.Name]=new Vector2(x,y);
                    }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        _options.Size = Math.Clamp(_options.Size, 80, 120); _options.Opacity = Math.Clamp(_options.Opacity, 40, 100);
    }
    public bool Enabled => OperatingSystem.IsAndroid();
    public Vector2 Movement { get; private set; }
    public Vector2 Orbit { get; private set; }
    public float PinchZoomDelta { get; private set; } // ETERVALE_MOBILE_INPUT_V24
    public bool SettingsRequested { get; private set; } // definicoes completas do Etervale
    public bool CloseRequested { get; private set; }
    public bool PanelCloseRequested { get; private set; }
    public bool WorldActionOnly { get; private set; }
    public Point? WorldTap { get; private set; } // ETERVALE_MOBILE_TARGET_LOOT_V28: clique de seleção, não movimento
    public bool TargetNextRequested { get; private set; } // botão ALVO
    public bool MapRequested { get; private set; } // ETERVALE_MOBILE_UI_V32
    public bool LootRequested { get; private set; }
    public bool TalkRequested { get; private set; } // ETERVALE_MOBILE_HUD_EDITOR_V33
    public bool UiTouchOnly { get; private set; } // ETERVALE_MOBILE_TAP_UI_V27
    public bool MenuCaptures => _config || _configWasOpen || _moreOpen || _editing;
    private bool _configWasOpen;
    private bool _diagnosticsRequested;

    internal bool TakeDiagnosticsRequest()
    {
        var requested = _diagnosticsRequested;
        _diagnosticsRequested = false;
        return requested;
    }
    private bool _skillPanel;
    internal int SkillShortcutRequested { get; private set; } = -1;

    private static readonly string[] SkillShortcutLabels = ["F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8"];
    private static readonly string[] RoleNames = Enum.GetNames<Role>();

    // Roles are contiguous from zero; preserve the exact existing JSON property names.
    internal static string RoleKey(Role role) => (uint)role < (uint)RoleNames.Length ? RoleNames[(int)role] : role.ToString();

    internal static string SkillShortcutLabel(Role role) => SkillShortcutLabels[(int)role - (int)Role.SkillF1];

    // Keep the assignment row above the centered skill window, outside its hit area.
    internal static Rectangle SkillPanelShortcutBounds(HudLayout layout, int index)
    {
        const int width = 52;
        const int height = 42;
        const int gap = 8;
        const int count = Widgets.HudSkillBindings.ShortcutCount;
        var total = count * width + (count - 1) * gap;
        var origin = Inventory.HudInventoryLayout.CenteredPanelOrigin(layout);
        var window = Widgets.HudSkillTreeLayout.Window(origin);
        return new Rectangle(window.Center.X - total / 2 + index * (width + gap), window.Top - height - 12, width, height);
    }
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
                writer.WriteBoolean("LeftHanded",_options.LeftHanded);
                writer.WriteStartObject("Positions");
                foreach(var item in _positions)
                {
                    writer.WriteStartArray(item.Key); writer.WriteNumberValue(item.Value.X);
                    writer.WriteNumberValue(item.Value.Y); writer.WriteEndArray();
                }
                writer.WriteEndObject(); writer.WriteEndObject();
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
    // ETERVALE_MOBILE_UI_V32: os painéis são mutuamente exclusivos; a faixa
    // central F1-F8 adapta-se à escala de 80 a 120 sem tapar o HUD inferior.
    // ETERVALE_MOBILE_HUD_EDITOR_V33: HUD sem a barra clássica, atalhos no rodapé.
    // Posições por defeito: a geometria dos controlos respeita 800x600 lógicos.
    internal static Rectangle Bounds(HudLayout l, Role role, int size = 100, bool leftHanded = false)
    {
        int w=l.Width, h=l.Height;
        if ((int)role >= (int)Role.SkillF1 && (int)role <= (int)Role.SkillF8)
        {
            int index=(int)role-(int)Role.SkillF1;
            int sw=Math.Max(40,52*size/100), sh=Math.Max(36,44*size/100);
            int gap=Math.Max(5,8*size/100);
            int total=4*sw+3*gap;
            return new Rectangle(w/2-total/2+(index%4)*(sw+gap),
                h-120+(index/4)*(sh+6),sw,sh);
        }
        Rectangle r=role switch
        {
            Role.Toggle or Role.Back => new(w-82,12,68,44),
            Role.Menu => new(w-164,12,76,44),
            Role.ContextCamera => new(100,12,112,44),
            Role.Settings => new(w-266,62,118,42),
            Role.ControlSettings => new(w-140,62,118,42),
            Role.Inventory => new(w-266,110,118,42),
            Role.Skills => new(w-140,110,118,42),
            Role.Status => new(w-266,158,118,42),
            Role.Quest => new(w-140,158,118,42),
            Role.Party => new(w-266,206,118,42),
            Role.Map => new(w-140,206,118,42),
            Role.Chat => new(w-266,254,118,42),
            Role.Run => new(w-140,254,118,42),
            Role.System => new(w-266,302,118,42),
            Role.Diagnostics => new(w-140,302,118,42),
            Role.Edit => new(w-266,350,244,42),
            Role.Stick => new(24,h-350,140,140),
            Role.Attack => new(w-110,h-315,86,78),
            Role.Skill => new(w-205,h-257,76,68),
            Role.TargetNext => new(w-215,h-337,80,56),
            Role.Loot => new(w-110,h-430,86,50),
            Role.Talk => new(w-215,h-422,80,52),
            Role.Camera => new(w-285,h-420,86,64),
            Role.Potion1 => new(18,h-405,52,40),
            Role.Potion2 => new(76,h-405,52,40),
            Role.Potion3 => new(134,h-405,52,40),
            Role.Vitals => Widgets.HudBar.MobileCentralBounds(l),
            Role.Minimap => new(w-144,h-174,128,128),
            Role.EditSave => new(w/2+8,12,104,46),
            Role.EditDefault => new(w/2-112,12,104,46),
            Role.Size => new(w/2-115,h/2-90,230,42),
            Role.Opacity => new(w/2-115,h/2-43,230,42),
            Role.Hand => new(w/2-115,h/2+4,230,42),
            Role.Reset => new(w/2-115,h/2+51,112,42),
            Role.Done => new(w/2+3,h/2+51,112,42),
            _ => Rectangle.Empty
        };
        if (role is Role.Stick or Role.Attack or Role.Skill or Role.TargetNext or Role.Loot or Role.Talk or Role.Camera)
        {
            int nw=r.Width*size/100, nh=r.Height*size/100;
            r=new Rectangle(r.Center.X-nw/2,r.Center.Y-nh/2,nw,nh);
            if(leftHanded)r.X=w-r.Right;
        }
        else if(leftHanded && (role is Role.Potion1 or Role.Potion2 or Role.Potion3))
            r.X=w-r.Right;
        r.X=Math.Clamp(r.X,0,Math.Max(0,w-r.Width));
        r.Y=Math.Clamp(r.Y,0,Math.Max(0,h-r.Height));
        return r;
    }
    internal const int HudGridStep = 16;
    internal const int HudGridHorizontalMargin = 48;
    internal const int HudGridVerticalMargin = 24;

    // Use the integer center of Rectangle, including for an odd width or height.
    internal static Point SnapHudCenter(HudLayout layout, Rectangle bounds, Vector2 center) => new(
        SnapHudAxis(layout.Width, bounds.Width, HudGridHorizontalMargin, center.X),
        SnapHudAxis(layout.Height, bounds.Height, HudGridVerticalMargin, center.Y));

    private static int SnapHudAxis(int extent, int size, int margin, float center)
    {
        if (size >= extent)
        {
            return size / 2;
        }

        var before = size / 2;
        var after = size - before;
        var first = ((margin + before + HudGridStep - 1) / HudGridStep) * HudGridStep;
        var last = ((extent - margin - after) / HudGridStep) * HudGridStep;
        // If the safe area is too narrow for a grid point, relax the margin rather than clipping the control.
        if (extent - margin - after < 0 || first > last)
        {
            first = ((before + HudGridStep - 1) / HudGridStep) * HudGridStep;
            last = ((extent - after) / HudGridStep) * HudGridStep;
        }
        if (first > last)
        {
            return before + (extent - size) / 2;
        }

        var snapped = (int)MathF.Round(Math.Clamp(center, first, last) / HudGridStep) * HudGridStep;
        return Math.Clamp(snapped, first, last);
    }

    // Normalized saved centers are independent of resolution; do not snap them again on load or rotation.
    internal static Rectangle SavedHudBounds(HudLayout layout, Rectangle bounds, Vector2 center)
    {
        bounds.X = Math.Clamp((int)MathF.Round(center.X * layout.Width) - bounds.Width / 2, 0, Math.Max(0, layout.Width - bounds.Width));
        bounds.Y = Math.Clamp((int)MathF.Round(center.Y * layout.Height) - bounds.Height / 2, 0, Math.Max(0, layout.Height - bounds.Height));
        return bounds;
    }

    private static void DrawHudGrid(HudPainter painter, HudLayout layout)
    {
        var left = HudGridHorizontalMargin;
        var right = layout.Width - HudGridHorizontalMargin;
        var top = HudGridVerticalMargin;
        var bottom = layout.Height - HudGridVerticalMargin;
        if (right <= left || bottom <= top)
        {
            return;
        }
        for (var x = ((left + HudGridStep - 1) / HudGridStep) * HudGridStep; x <= right; x += HudGridStep)
        {
            painter.ColorBox(new Rectangle(x, top, 1, bottom - top), 120, 185, 195, 48);
        }
        for (var y = ((top + HudGridStep - 1) / HudGridStep) * HudGridStep; y <= bottom; y += HudGridStep)
        {
            painter.ColorBox(new Rectangle(left, y, right - left, 1), 120, 185, 195, 48);
        }
    }

    internal void MoveHudControl(HudLayout layout, Role role, Vector2 desiredCenter)
    {
        var bounds = Box(layout, role);
        var center = SnapHudCenter(layout, bounds, desiredCenter);
        _positions[RoleKey(role)] = new Vector2(center.X / (float)layout.Width, center.Y / (float)layout.Height);
    }

    private Rectangle Box(HudLayout l, Role role)
    {
        var rectangle=Bounds(l,role,_options.Size,_options.LeftHanded);
        if (_positions.TryGetValue(RoleKey(role),out var center))
        {
            rectangle = SavedHudBounds(l, rectangle, center);
        }
        return rectangle;
    }

    internal Rectangle MapBounds(HudLayout l) => Box(l,Role.Minimap);
    internal bool CentralHudVisible => Enabled && _touchAvailable && !_config && !_moreOpen;
    internal Rectangle CentralHudBounds(HudLayout layout) => Box(layout, Role.Vitals);
    internal Point CentralHudOrigin(HudLayout layout) => Widgets.HudBar.CentralOrigin(CentralHudBounds(layout));
    private static bool Movable(Role role) => role is Role.Toggle or Role.Menu or Role.ContextCamera
        or Role.Stick or Role.Attack or Role.Skill or Role.TargetNext or Role.Loot or Role.Talk or Role.Camera
        or Role.Potion1 or Role.Potion2 or Role.Potion3 or Role.Vitals or Role.Minimap
        or Role.SkillF1 or Role.SkillF2 or Role.SkillF3 or Role.SkillF4
        or Role.SkillF5 or Role.SkillF6 or Role.SkillF7 or Role.SkillF8;
    private bool VisibleQuick(Role role) => role != Role.Camera || _touchCameraMode;
    private bool VisibleGameQuick(Role role) => role != Role.Talk || !_touchCameraMode;
    // ETERVALE_MOBILE_HUD_EDITOR_V33: GIRO exclusivo da câmara; FALAR exclusivo do jogo.
    private static readonly Role[] Quick = [Role.Stick,Role.Attack,Role.Skill,Role.TargetNext,
        Role.Loot,Role.Talk,Role.Camera,Role.Potion1,Role.Potion2,Role.Potion3,
        Role.SkillF1,Role.SkillF2,Role.SkillF3,Role.SkillF4,
        Role.SkillF5,Role.SkillF6,Role.SkillF7,Role.SkillF8];
    private static readonly Role[] Secondary = [Role.Settings,Role.ControlSettings,Role.Inventory,Role.Skills,
        Role.Status,Role.Quest,Role.Party,Role.Map,Role.Chat,Role.Run,Role.System,Role.Diagnostics,Role.Edit];
    private static readonly Role[] Config = [Role.Size,Role.Opacity,Role.Hand,Role.Reset,Role.Done];
    internal Role Hit(HudLayout l, Point p, bool available, bool backAvailable)
    {
        if(_config) { foreach(var role in Config) if(Box(l,role).Contains(p)) return role; return Role.None; }
        if (!available)
        {
            if (backAvailable && Box(l, Role.Back).Contains(p))
            {
                return Role.Back;
            }
            if (_skillPanel)
            {
                for (var i = 0; i < Widgets.HudSkillBindings.ShortcutCount; i++)
                {
                    if (SkillPanelShortcutBounds(l, i).Contains(p))
                    {
                        return (Role)((int)Role.SkillF1 + i);
                    }
                }
            }
            return Role.World;
        }
        if(_editing)
        {
            if(Box(l,Role.EditSave).Contains(p))return Role.EditSave;
            if(Box(l,Role.EditDefault).Contains(p))return Role.EditDefault;
            foreach(var role in Quick)
                if(VisibleQuick(role)&&VisibleGameQuick(role)&&Box(l,role).Contains(p))return role;
            if(Box(l,Role.Minimap).Contains(p))return Role.Minimap;
            if(Box(l,Role.Vitals).Contains(p))return Role.Vitals;
            if (Box(l, Role.Toggle).Contains(p)) return Role.Toggle;
            if (Box(l, Role.Menu).Contains(p)) return Role.Menu;
            if (Box(l, Role.ContextCamera).Contains(p)) return Role.ContextCamera;
            return Role.None; // EDITAR nunca envia cliques ao cenário.
        }
        if(Box(l,Role.Toggle).Contains(p))return Role.Toggle;
        if(Box(l,Role.Menu).Contains(p))return Role.Menu;
        if(Box(l,Role.ContextCamera).Contains(p))return Role.ContextCamera;
        if(_moreOpen)
        {
            foreach(var role in Secondary)if(Box(l,role).Contains(p))return role;
            return Role.None;
        }
        if (_expanded)
        {
            foreach (var role in Quick)
            {
                if (VisibleQuick(role) && VisibleGameQuick(role) && Box(l, role).Contains(p))
                {
                    return role;
                }
            }
        }
        // The quick controls are drawn over the minimap and central HUD.
        if (Box(l, Role.Minimap).Contains(p))
        {
            return Role.None;
        }
        if (CentralHudVisible && CentralHudBounds(l).Contains(p))
        {
            var origin = CentralHudOrigin(l);
            for (var box = 0; box < PristonTale.Simulation.Items.PotionBelt.Boxes; box++)
            {
                if (Inventory.HudInventoryLayout.Belt(origin, box).Contains(p))
                {
                    return (Role)((int)Role.Potion1 + box);
                }
            }
            return Role.None;
        }
        return Role.World;
    }
    internal void OnPress(Role role)
    {
        if (_skillPanel && role >= Role.SkillF1 && role <= Role.SkillF8)
        {
            SkillShortcutRequested = (int)role - (int)Role.SkillF1;
            return;
        }
        if(_moreOpen && Array.IndexOf(Secondary,role)>=0)_moreOpen=false;
        switch(role)
        {
            case Role.Toggle: _expanded=!_expanded; _moreOpen=false; break;
            case Role.Menu: _moreOpen=!_moreOpen; break;
            case Role.ContextCamera: _touchCameraMode=!_touchCameraMode; _cameraDragFinger=null; break;
            case Role.Back: PanelCloseRequested=true; break;
            case Role.Potion1:
            case Role.Potion2:
            case Role.Potion3:
                _keys.Add((Keys)((int)Keys.D1 + (int)role - (int)Role.Potion1));
                break;
            case Role.Settings: SettingsRequested=true; break;
            case Role.Diagnostics:
                _diagnosticsRequested = true;
                break;
            case Role.ControlSettings: _config=true; break;
            case Role.Map: MapRequested=true; break;
            case Role.Loot: LootRequested=true; break;
            case Role.Talk: TalkRequested=true; break;
            // V32 omitia este caso e ALVO deixou de mudar de alvo.
            case Role.TargetNext: TargetNextRequested=true; break;
            case Role.Edit: _moreOpen=false; _expanded=true; _editing=true; _editingFinger=null; break;
            case Role.EditSave: _editing=false; _editingFinger=null; Save(); break;
            case Role.EditDefault: _positions.Clear(); _editingFinger=null; Save(); break;
            case Role.Size: _options.Size=_options.Size>=120?80:_options.Size+10; Save(); break;
            case Role.Opacity: _options.Opacity=_options.Opacity>=100?40:Math.Min(100,_options.Opacity+15); Save(); break;
            case Role.Hand: _options.LeftHanded=!_options.LeftHanded; Save(); break;
            case Role.Reset: _options=new(); Save(); break;
            case Role.Done: _config=false; break;
        }
    }
    private void ReadEvents(bool active)
    {
        if (!_touchAvailable) return;
        try
        {
            while (PopTouch(out var type, out var device, out var id, out var x, out var y) != 0)
            {
                if (type == 4 || _events.Count >= 512) { _multiWorldGesture=false; _events.Clear(); _fingers.Clear(); if (type == 4) continue; }
                if (active && float.IsFinite(x) && float.IsFinite(y)) _events.Enqueue(new(type,device,id,x,y));
            }
        }
        catch (Exception e) when (e is EntryPointNotFoundException or DllNotFoundException) { _touchAvailable = false; _events.Clear(); _fingers.Clear(); }
    }
    // ETERVALE_MOBILE_TARGET_LOOT_V28: drag e pinça nunca selecionam entidades.
    internal static bool ShortWorldTap(Vector2 start, Vector2 end, bool multitouch, bool available)
        => available && !multitouch && Vector2.DistanceSquared(start,end) <= 18f*18f;

    // ETERVALE_MOBILE_CAMERA_CONTEXT_V31: delta incremental, não a distância desde
    // o início do toque; o eixo regressa a zero ao parar de arrastar.
    private bool TryWorldCameraOrbit(out Vector2 orbit)
    {
        orbit=Vector2.Zero;
        if (!_touchCameraMode || _multiWorldGesture || WorldFingerCount()!=1)
        {
            _cameraDragFinger=null;
            return false;
        }
        foreach (var (id,finger) in _fingers)
        {
            if (finger.Role!=Role.World) continue;
            if (_cameraDragFinger==id)
                orbit=CameraDragAxis(finger.Position-_lastCameraDrag);
            _lastCameraDrag=finger.Position;
            _cameraDragFinger=id;
            return true;
        }
        return false;
    }

    internal static Vector2 CameraDragAxis(Vector2 drag)
        => DeadZone(new Vector2(drag.X,-drag.Y)/12f);

    private int WorldFingerCount()
    {
        int count=0;
        foreach (var finger in _fingers.Values) if (finger.Role==Role.World) ++count;
        return count;
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
            if (t.Type == 3)
            {
                if (_editing && _editingFinger==id) { _editingFinger=null; Save(); } // ETERVALE_MOBILE_HUD_EDITOR_V33
                if (_fingers.TryGetValue(id,out var finished) && finished.Role==Role.World
                    && !_touchCameraMode // ETERVALE_MOBILE_CAMERA_CONTEXT_V31: no modo câmara, arrastar/tocar não seleciona monstros
                    && WorldFingerCount()==1 && ShortWorldTap(finished.Start,finished.Position,_multiWorldGesture,available)
                    && finished.Position.Y < l.Height-128)
                {
                    var pixel=finished.Position*l.PixelScale;
                    WorldTap=new Point((int)pixel.X,(int)pixel.Y);
                }
                _fingers.Remove(id);
                _pressedThisFrame.Add(id);
                if (WorldFingerCount()==0) _multiWorldGesture=false;
                continue;
            }
            var p = new Vector2(Math.Clamp(t.X,0,1)*(l.ViewportWidth-1)/l.PixelScale, Math.Clamp(t.Y,0,1)*(l.ViewportHeight-1)/l.PixelScale);
            if (t.Type == 1)
            {
                var wasPad = _preferPad;
                _preferPad = false;
                var point = new Point((int)p.X,(int)p.Y);
                var role = Hit(l,point,available,backAvailable); // ETERVALE_MOBILE_UI_V32
                _fingers[id] = new(role,p,p); _pressedThisFrame.Add(id);
                if (WorldFingerCount()>1) _multiWorldGesture=true; // ETERVALE_MOBILE_TARGET_LOOT_V28
                if (_editing && Movable(role))
                {
                    _editingFinger=id; _editingRole=role;
                    var rect=Box(l,role); _editingGrab=new Vector2(rect.Center.X-p.X,rect.Center.Y-p.Y);
                }
                else if (wasPad && role == Role.Toggle) _expanded=true; else OnPress(role);
            }
            else if (t.Type == 2 && _fingers.TryGetValue(id,out var f))
            {
                _fingers[id]=f with { Position=p };
                if (_editing && _editingFinger==id && Movable(_editingRole))
                {
                    MoveHudControl(l, _editingRole, p + _editingGrab);
                }
            }
        }
    }
    // ETERVALE_MOBILE_INPUT_V24: zoom so quando dois dedos comecam no mundo;
    // nenhum toque do joystick, botoes, UI, inventario ou chat e interpretado como pinch.
    private void UpdatePinch(bool enabled)
    {
        if (!enabled) { _previousPinchSeparation = null; return; }
        Vector2 first = default, second = default;
        int count = 0;
        foreach (var finger in _fingers.Values)
        {
            if (finger.Role != Role.World) continue;
            if (count == 0) first = finger.Position;
            else if (count == 1) second = finger.Position;
            ++count;
        }
        if (count != 2) { _previousPinchSeparation = null; return; }
        float distance = Vector2.Distance(first, second);
        if (_previousPinchSeparation is float previous)
            PinchZoomDelta = Math.Clamp(distance - previous, -35f, 35f);
        _previousPinchSeparation = distance;
    }
    // No cenário ativo, o toque não é um clique; com painéis abertos,
    // o mesmo dedo controla o HUD. Isto inclui menus, chat e inventário.
    internal static bool ForwardWorldTouchToHud(bool gameplay, bool overlayAvailable) => !gameplay || !overlayAvailable; // ETERVALE_MOBILE_TAP_UI_V27
    public MouseState HudMouse(MouseState mouse) => WorldActionOnly ? Pointer(mouse,false,false) : mouse;
    public KeyboardState Merge(KeyboardState keyboard)
    {
        if (_keys.Count == 0)
        {
            return keyboard;
        }

        var physicalCount = keyboard.GetPressedKeyCount();
        var count = physicalCount;
        foreach (var key in _keys)
        {
            if (keyboard.IsKeyUp(key))
            {
                count++;
            }
        }

        // KeyboardState copies keys into its own bit fields; the buffer is safe to reuse.
        if (!_mergedKeyBuffers.TryGetValue(count, out var keys))
        {
            keys = new Keys[count];
            _mergedKeyBuffers.Add(count, keys);
        }

        keyboard.GetPressedKeys(keys);
        var next = physicalCount;
        foreach (var key in _keys)
        {
            if (keyboard.IsKeyUp(key))
            {
                keys[next++] = key;
            }
        }

        return new KeyboardState(keys);
    }
    public void Update(Viewport viewport, GameTime time, bool active, bool available, bool typing, bool gameplay = true, bool backAvailable = false, bool skillPanel = false)
    {
        SkillShortcutRequested = -1;
        _skillPanel = gameplay && skillPanel && !typing;
        if (!Enabled) return;
        _keys.Clear(); TalkRequested=false; MapRequested=LootRequested=false; Movement=Orbit=_stick=Vector2.Zero; PinchZoomDelta=0f; SettingsRequested=false; CloseRequested=PanelCloseRequested=WorldActionOnly=UiTouchOnly=false; WorldTap=null; TargetNextRequested=false; _configWasOpen=_config;
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
        if (!gameplay || !available || typing) { _config=false; _moreOpen=false; _editing=false; _editingFinger=null; }
        if (_lastAvailable && !available) _fingers.Clear();
        _lastAvailable=available;
        ProcessEvents(l,gameplay&&available&&!typing,gameplay&&backAvailable);
        UpdatePinch(gameplay&&available&&!typing&&!_config&&!_moreOpen);
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
            {
                // ETERVALE_MOBILE_TAP_UI_V27: menus abertos recebem toque; cenário livre não recebe clique.
                if (ForwardWorldTouchToHud(gameplay, available))
                {
                    _cursor=f.Position*l.PixelScale;
                    left=true;
                    // O HUD recebe o clique, mas a GameScene nunca o interpreta no cenário.
                    UiTouchOnly=gameplay && !available;
                }
                world=true; continue;
            }
            if(!gameplay||!available||typing||_config||!_expanded) continue;
            switch(f.Role)
            {
                case Role.Stick when !stick: _stick=DeadZone(new Vector2(f.Position.X-Box(l,Role.Stick).Center.X,Box(l,Role.Stick).Center.Y-f.Position.Y)/(Box(l,Role.Stick).Width*.42f)); Movement=_stick; stick=true; break;
                case Role.Camera when !camera: Orbit=DeadZone(new Vector2(f.Position.X-f.Start.X,f.Start.Y-f.Position.Y)/32f); camera=true; break;
                case Role.Attack: left=true; WorldActionOnly=true; break;
                case Role.Skill: right=true; WorldActionOnly=true; break;
                case Role.Inventory: _keys.Add(Keys.I); break; case Role.Skills: _keys.Add(Keys.S); break;
                case Role.Status: _keys.Add(Keys.C); break; case Role.Quest: _keys.Add(Keys.Q); break;
                case Role.Party: _keys.Add(Keys.D); break;
                case Role.Run: _keys.Add(Keys.R); break;
                case Role.System: _keys.Add(Keys.X); break;
                case Role.SkillF1: _keys.Add(Keys.F1); break;
                case Role.SkillF2: _keys.Add(Keys.F2); break;
                case Role.SkillF3: _keys.Add(Keys.F3); break;
                case Role.SkillF4: _keys.Add(Keys.F4); break;
                case Role.SkillF5: _keys.Add(Keys.F5); break;
                case Role.SkillF6: _keys.Add(Keys.F6); break;
                case Role.SkillF7: _keys.Add(Keys.F7); break;
                case Role.SkillF8: _keys.Add(Keys.F8); break;
                case Role.Chat: _keys.Add(Keys.Enter); break;
            }
        }
        // ETERVALE_MOBILE_CAMERA_CONTEXT_V31: em CAMARA, o gesto no mundo substitui a seleção por toque.
        // O controlo de câmara dedicado mantém o seu comportamento original.
        if (gameplay && available && !typing && !_config && !_moreOpen && TryWorldCameraOrbit(out var worldOrbit))
            Orbit=worldOrbit;
        else _cameraDragFinger=null;
        if(world) WorldActionOnly=false;
        if(_config || _configWasOpen || _editing) { left=right=false; Movement=Orbit=Vector2.Zero; _keys.Clear(); }
        else if(_moreOpen) { left=right=false; Movement=Orbit=Vector2.Zero; }
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
    internal bool IsRolePressed(Role role)
    {
        foreach (var finger in _fingers.Values)
        {
            if (finger.Role == role)
            {
                return true;
            }
        }

        return false;
    }

    private void Button(HudPainter painter,Rectangle r,string title, bool selected=false)
    {
        if(r.Width<=0||r.Height<=0)return;
        int alpha=255*_options.Opacity/100;
        // Moldura âmbar e interior escuro; estado ativo destacado.
        painter.ColorBox(r,selected?230:152,selected?178:127,selected?82:70,alpha);
        painter.ColorBox(new Rectangle(r.X+2,r.Y+2,Math.Max(1,r.Width-4),Math.Max(1,r.Height-4)),
            selected?58:17,selected?46:21,selected?28:25,alpha);
        painter.TextCentered(title,r.Left,r.Right,r.Center.Y-painter.TextHeight/2,
            selected?new Color(255,230,159):new Color(235,218,181));
    }
    public void Draw(HudPainter painter,HudLayout l,bool available,bool backAvailable,OwnerState state, bool skillPanel = false)
    {
        if(!Enabled||!_touchAvailable)return;
        if(!available)
        {
            if (backAvailable) Button(painter, Box(l, Role.Back), "VOLTAR");
            if (skillPanel)
            {
                for (var i = 0; i < Widgets.HudSkillBindings.ShortcutCount; i++)
                {
                    Button(painter, SkillPanelShortcutBounds(l, i), SkillShortcutLabels[i]);
                }
            }
            return;
        }
        if(_config)
        {
            painter.ColorBox(new Rectangle(l.Width/2-130,l.Height/2-120,260,230),16,15,12,240);
            Button(painter,Box(l,Role.Size),$"TAMANHO {_options.Size}%");
            Button(painter,Box(l,Role.Opacity),$"OPACIDADE {_options.Opacity}%");
            Button(painter,Box(l,Role.Hand),_options.LeftHanded?"CANHOTO":"DESTRO");
            Button(painter,Box(l,Role.Reset),"RESET");
            Button(painter,Box(l,Role.Done),"OK");
            return;
        }
        if(_editing)
        {
            DrawHudGrid(painter, l);
            painter.ColorBox(new Rectangle(l.Width/2-119,7,238,57),10,29,29,220);
            Button(painter,Box(l,Role.EditDefault),"REPOR");
            Button(painter,Box(l,Role.EditSave),"GUARDAR",true);
            var map=Box(l,Role.Minimap);
            painter.ColorBox(new Rectangle(map.X,map.Y,Math.Min(map.Width,73),18),12,29,33,220);
            painter.TextCentered("MAPA",map.X,Math.Min(map.Right,map.X+73),map.Y+2,new Color(255,226,160));
        }
        Button(painter,Box(l,Role.Toggle),_expanded?"HUD -":"HUD +",_expanded);
        Button(painter,Box(l,Role.Menu),_moreOpen?"FECHAR":"MENU",_moreOpen);
        Button(painter,Box(l,Role.ContextCamera),_touchCameraMode?"JOGO":"CAMARA",_touchCameraMode);
        if(_moreOpen && !_editing)
        {
            painter.ColorBox(new Rectangle(l.Width-274,58,256,342),17,18,19,220*_options.Opacity/100);
            foreach(var role in Secondary)
            {
                string title=role switch
                {
                    Role.Settings=>"CONFIG",Role.ControlSettings=>"PAD",Role.Inventory=>"BOLSA",
                    Role.Skills=>"SKILLS",Role.Status=>"STATUS",Role.Quest=>"QUEST",
                    Role.Party=>"GRUPO",Role.Map=>"MAPA",Role.Chat=>"CHAT",
                    Role.Run=>"CORRER",Role.System=>"SISTEMA",Role.Diagnostics=>"METRICAS",Role.Edit=>"EDITAR HUD",_=>""
                };
                if(title.Length>0)Button(painter,Box(l,role),title);
            }
            return;
        }
        if((!_expanded||_preferPad)&&!_editing)return;
        var stick=Box(l,Role.Stick);
        painter.ColorBox(stick,19,23,26,124*_options.Opacity/100);
        int sx=stick.Center.X+(int)(_stick.X*stick.Width*.3f);
        int sy=stick.Center.Y-(int)(_stick.Y*stick.Height*.3f);
        painter.ColorBox(new Rectangle(sx-18,sy-18,36,36),185,149,88,222*_options.Opacity/100);
        foreach(var role in Quick)
        {
            if(role==Role.Stick||!VisibleQuick(role)||!VisibleGameQuick(role))continue;
            string title=role switch
            {
                Role.Attack=>"ATACAR",Role.Skill=>"SKILL",Role.TargetNext=>"ALVO",
                Role.Loot=>"LOOT",Role.Talk=>"FALAR",Role.Camera=>"GIRO",
                Role.Potion1=>"P1",Role.Potion2=>"P2",Role.Potion3=>"P3",
                Role.SkillF1 or Role.SkillF2 or Role.SkillF3 or Role.SkillF4
                    or Role.SkillF5 or Role.SkillF6 or Role.SkillF7 or Role.SkillF8
                    =>SkillShortcutLabel(role),
                _=>""
            };
            if(title.Length>0)Button(painter,Box(l,role),title,
                _editing||IsRolePressed(role));
        }
    }
    public void Reset(bool preservePointer = false)
    {
        _diagnosticsRequested = false;
        SkillShortcutRequested = -1;
        _skillPanel = false;
        if(!Enabled)return;
        if(!preservePointer) Mouse.EtervaleOverride=null; _events.Clear(); _fingers.Clear(); _keys.Clear(); _waitPad=true; _wasActive=false; _config=false; _configWasOpen=false; _moreOpen=false; _editing=false; _editingFinger=null; TalkRequested=false; MapRequested=LootRequested=false; _cursorReady=false; _touchCameraMode=false; _cameraDragFinger=null; _multiWorldGesture=false; WorldTap=null; TargetNextRequested=false; _previousPinchSeparation=null; Movement=Orbit=Vector2.Zero; PinchZoomDelta=0f; SettingsRequested=false; WorldActionOnly=UiTouchOnly=CloseRequested=PanelCloseRequested=false; _lastAvailable=false; _rawReady=false;
    }
}
