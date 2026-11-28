using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PristonTale.Gameplay.UI;
using System.Runtime.CompilerServices;

namespace PristonTale.Tests;

internal static class MobileHudLayoutTests
{
    private static Task GeneratedManifestAsync()
    {
        const string json = """
            {"Version":1,"Textures":["atlas.png"],"Regions":{"test":{"Texture":0,"X":1,"Y":2,"Width":3,"Height":4,"Border":0}},"MagentaKeyTextures":[],"Gauges":{}}
            """;
        var manifest = PristonTale.Gameplay.UI.Rendering.HudAtlas.ParseManifest(System.Text.Encoding.UTF8.GetBytes(json));
        Assert.True(manifest.Version == 1 && manifest.Regions["test"].Bounds == new Rectangle(1, 2, 3, 4), "HUD manifest changed");
        foreach (var invalid in new[] { json.Replace("\"Version\":1,", ""), json.Replace("\"Version\":1", "\"Version\":1,\"Unknown\":true") })
        {
            try
            {
                PristonTale.Gameplay.UI.Rendering.HudAtlas.ParseManifest(System.Text.Encoding.UTF8.GetBytes(invalid));
                throw new InvalidOperationException("Invalid HUD manifest was accepted");
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        return Task.CompletedTask;
    }

    public static IEnumerable<(string Name, Func<Task> Run)> Cases()
    {
        yield return ("apk68: HUD atlas generated manifest keeps required and unknown field checks", GeneratedManifestAsync);
        yield return Case("mobile HUD: MobileLayoutFitsAndPointerUsesSameScale (1280, 720)", static () => MobileLayoutFitsAndPointerUsesSameScale(1280, 720));
        yield return Case("mobile HUD: MobileLayoutFitsAndPointerUsesSameScale (1920, 1080)", static () => MobileLayoutFitsAndPointerUsesSameScale(1920, 1080));
        yield return Case("mobile HUD: MobileLayoutFitsAndPointerUsesSameScale (2400, 1080)", static () => MobileLayoutFitsAndPointerUsesSameScale(2400, 1080));
        yield return Case("mobile HUD: MobileLayoutFitsAndPointerUsesSameScale (2340, 1080)", static () => MobileLayoutFitsAndPointerUsesSameScale(2340, 1080));
        yield return Case("mobile HUD: MobileLayoutFitsAndPointerUsesSameScale (854, 480)", static () => MobileLayoutFitsAndPointerUsesSameScale(854, 480));
        yield return Case("mobile HUD: MobileLayoutFitsAndPointerUsesSameScale (800, 600)", static () => MobileLayoutFitsAndPointerUsesSameScale(800, 600));
        yield return Case("mobile HUD: MobileLayoutFitsAndPointerUsesSameScale (640, 360)", static () => MobileLayoutFitsAndPointerUsesSameScale(640, 360));
        yield return Case("mobile HUD: MobileButtonsFit (800,600,80,false)", static () => MobileButtonsFit(800,600,80,false));
        yield return Case("mobile HUD: MobileButtonsFit (1280,720,120,false)", static () => MobileButtonsFit(1280,720,120,false));
        yield return Case("mobile HUD: MobileButtonsFit (2400,1080,120,true)", static () => MobileButtonsFit(2400,1080,120,true));
        yield return Case("mobile HUD: MobileButtonsFit (854,480,80,true)", static () => MobileButtonsFit(854,480,80,true));
        yield return Case("mobile HUD: WorldTouchRoutesToHudOnlyWhenAppropriate (true, true, false)", static () => WorldTouchRoutesToHudOnlyWhenAppropriate(true, true, false)); // HUD mobile aberto: toque no cenário não clica
        yield return Case("mobile HUD: WorldTouchRoutesToHudOnlyWhenAppropriate (true, false, true)", static () => WorldTouchRoutesToHudOnlyWhenAppropriate(true, false, true)); // painel/modal aberto: toque vai para o HUD
        yield return Case("mobile HUD: WorldTouchRoutesToHudOnlyWhenAppropriate (false, false, true)", static () => WorldTouchRoutesToHudOnlyWhenAppropriate(false, false, true)); // login/seleção: toque normal
        yield return Case("mobile HUD: WorldTouchRoutesToHudOnlyWhenAppropriate (false, true, true)", static () => WorldTouchRoutesToHudOnlyWhenAppropriate(false, true, true));
        yield return Case("mobile HUD: stable role names and allocation-free control lookups", StableControlLookups);
        yield return Case("mobile HUD: keyboard merge preserves snapshots and reuses buffers", KeyboardMerge);
        yield return Case("mobile HUD: DeadZoneIsRadialAndBounded", static () => DeadZoneIsRadialAndBounded());
        yield return Case("mobile HUD: DesktopScaleRemainsInteger", static () => DesktopScaleRemainsInteger());
        yield return Case("mobile HUD: MobileWorldTapIgnoresGestures (10f,10f,14f,11f,false,true,true)", static () => MobileWorldTapIgnoresGestures(10f,10f,14f,11f,false,true,true));
        yield return Case("mobile HUD: MobileWorldTapIgnoresGestures (10f,10f,30f,11f,false,true,false)", static () => MobileWorldTapIgnoresGestures(10f,10f,30f,11f,false,true,false));
        yield return Case("mobile HUD: MobileWorldTapIgnoresGestures (10f,10f,10f,10f,true,true,false)", static () => MobileWorldTapIgnoresGestures(10f,10f,10f,10f,true,true,false));
        yield return Case("mobile HUD: MobileWorldTapIgnoresGestures (10f,10f,10f,10f,false,false,false)", static () => MobileWorldTapIgnoresGestures(10f,10f,10f,10f,false,false,false));
        yield return Case("mobile HUD: MobileTargetNextButtonFits (800,600,80,false)", static () => MobileTargetNextButtonFits(800,600,80,false));
        yield return Case("mobile HUD: MobileTargetNextButtonFits (1280,720,120,false)", static () => MobileTargetNextButtonFits(1280,720,120,false));
        yield return Case("mobile HUD: MobileTargetNextButtonFits (2400,1080,120,true)", static () => MobileTargetNextButtonFits(2400,1080,120,true));
        yield return Case("mobile HUD: MobileTargetNextButtonFits (854,480,80,true)", static () => MobileTargetNextButtonFits(854,480,80,true));
        yield return Case("mobile HUD: CameraContextButtonDoesNotOverlapMenus (800,600)", static () => CameraContextButtonDoesNotOverlapMenus(800,600));
        yield return Case("mobile HUD: CameraContextButtonDoesNotOverlapMenus (1280,720)", static () => CameraContextButtonDoesNotOverlapMenus(1280,720));
        yield return Case("mobile HUD: CameraContextButtonDoesNotOverlapMenus (2400,1080)", static () => CameraContextButtonDoesNotOverlapMenus(2400,1080));
        yield return Case("mobile HUD: CameraDragAxisIsBoundedAndStopsWhenStationary", static () => CameraDragAxisIsBoundedAndStopsWhenStationary());
    }

    private static (string Name, Func<Task> Run) Case(string name, Action run) => (name, () =>
    {
        run();
        return Task.CompletedTask;
    });

    private static void KeyboardMerge()
    {
        var controls = new MobileControls();
        var physical = new KeyboardState(Keys.A, Keys.LeftShift);
        Check.True(controls.Merge(physical) == physical);
        controls.OnPress(MobileControls.Role.Potion1);
        controls.OnPress(MobileControls.Role.Potion2);
        controls.OnPress(MobileControls.Role.Potion1);

        var empty = default(KeyboardState);
        var overlapping = new KeyboardState(Keys.D1, Keys.A);
        var previous = controls.Merge(physical);
        Check.True(previous == new KeyboardState(Keys.A, Keys.LeftShift, Keys.D1, Keys.D2));
        Check.True(controls.Merge(overlapping) == new KeyboardState(Keys.A, Keys.D1, Keys.D2));
        Check.True(controls.Merge(empty) == new KeyboardState(Keys.D1, Keys.D2));
        Check.True(previous.IsKeyDown(Keys.LeftShift));
        Check.True(previous.IsKeyDown(Keys.A));
        Check.True(physical.IsKeyUp(Keys.D1));

        // Revisit different key counts and then the same count with different physical keys.
        var alternate = new KeyboardState(Keys.B, Keys.RightShift);
        var expected = new KeyboardState(Keys.B, Keys.RightShift, Keys.D1, Keys.D2);
        var expectedEmpty = new KeyboardState(Keys.D1, Keys.D2);
        var expectedOverlap = new KeyboardState(Keys.A, Keys.D1, Keys.D2);
        var correct = true;
        for (var i = 0; i < 1000; i++)
        {
            correct &= controls.Merge(alternate) == expected;
            correct &= controls.Merge(empty) == expectedEmpty;
            correct &= controls.Merge(overlapping) == expectedOverlap;
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            correct &= controls.Merge(alternate) == expected;
            correct &= controls.Merge(empty) == expectedEmpty;
            correct &= controls.Merge(overlapping) == expectedOverlap;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check.True(correct);
        Check.True(previous == new KeyboardState(Keys.A, Keys.LeftShift, Keys.D1, Keys.D2));
        Assert.True(allocated == 0, $"warmed keyboard merges allocated {allocated} bytes");
    }

    private static void StableControlLookups()
    {
        var roles = Enum.GetValues<MobileControls.Role>();
        for (var i = 0; i < roles.Length; i++)
        {
            Check.Equal(i, (int)roles[i]);
            Check.Equal(roles[i].ToString(), MobileControls.RoleKey(roles[i]));
            Check.True(ReferenceEquals(MobileControls.RoleKey(roles[i]), MobileControls.RoleKey(roles[i])));
        }

        for (var i = 0; i < PristonTale.Gameplay.UI.Widgets.HudSkillBindings.ShortcutCount; i++)
        {
            var role = (MobileControls.Role)((int)MobileControls.Role.SkillF1 + i);
            Check.Equal($"F{i + 1}", MobileControls.SkillShortcutLabel(role));
            Check.True(ReferenceEquals(MobileControls.SkillShortcutLabel(role), MobileControls.SkillShortcutLabel(role)));
        }

        var controls = new MobileControls();
        var layout = HudLayout.Create(2400, 1080, mobile: true);
        var map = MobileControls.Bounds(layout, MobileControls.Role.Minimap);
        Check.Equal(map, controls.MapBounds(layout));
        var center = MobileControls.SnapHudCenter(layout, map, new Vector2(231, 321));
        controls.MoveHudControl(layout, MobileControls.Role.Minimap, new Vector2(231, 321));
        Check.Equal(center, controls.MapBounds(layout).Center);
        var rotated = HudLayout.Create(1080, 2400, mobile: true);
        var expected = MobileControls.SavedHudBounds(rotated, MobileControls.Bounds(rotated, MobileControls.Role.Minimap),
            new Vector2(center.X / (float)layout.Width, center.Y / (float)layout.Height));
        Check.Equal(expected, controls.MapBounds(rotated));

        // Warm the same production helpers before measuring the idle draw lookups.
        for (var i = 0; i < 1000; i++)
        {
            _ = controls.MapBounds(layout);
            _ = controls.IsRolePressed(MobileControls.Role.Attack);
            _ = MobileControls.SkillShortcutLabel(MobileControls.Role.SkillF1);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var correct = true;
        for (var i = 0; i < 1000; i++)
        {
            correct &= controls.MapBounds(layout).Center == center;
            correct &= !controls.IsRolePressed(MobileControls.Role.Attack);
            correct &= MobileControls.SkillShortcutLabel(MobileControls.Role.SkillF1) == "F1";
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check.True(correct);
        Assert.True(allocated == 0, $"idle mobile control lookups allocated {allocated} bytes");
    }

    private static void MobileLayoutFitsAndPointerUsesSameScale(int width, int height)
    {
        var l = HudLayout.Create(width, height, mobile: true);
        Check.True(l.Width >= 800 && l.Height >= 600);
        Check.InRange(l.Width * l.PixelScale, 0f, width + 1f);
        Check.InRange(l.Height * l.PixelScale, 0f, height + 1f);
        var box = new Rectangle(100, 200, 48, 48);
        Check.True(box.Contains(l.ToLogical(l.ToPhysical(box).Center)));
        Check.True(l.ToLogical(new Point(-1,-1)).X < 0);
    }

    // ETERVALE_MOBILE_UI_V32: os botões só podem coincidir entre modos mutuamente exclusivos.
    // ETERVALE_MOBILE_HUD_EDITOR_V33: sem barra antiga; ALVO, FALAR, GIRO contextual, minimapa e F1-F8.
    private static void MobileButtonsFit(int width,int height,int size,bool leftHanded)
    {
        var l=HudLayout.Create(width,height,mobile:true);
        var general=new[]{MobileControls.Role.Toggle,MobileControls.Role.Menu,MobileControls.Role.ContextCamera,
            MobileControls.Role.Stick,MobileControls.Role.Attack,MobileControls.Role.Skill,
            MobileControls.Role.TargetNext,MobileControls.Role.Loot,MobileControls.Role.Talk,
            MobileControls.Role.Potion1,MobileControls.Role.Potion2,MobileControls.Role.Potion3,
            MobileControls.Role.SkillF1,MobileControls.Role.SkillF2,MobileControls.Role.SkillF3,MobileControls.Role.SkillF4,
            MobileControls.Role.SkillF5,MobileControls.Role.SkillF6,MobileControls.Role.SkillF7,MobileControls.Role.SkillF8,
            MobileControls.Role.Vitals,MobileControls.Role.Minimap};
        var camera=general.Where(x=>x!=MobileControls.Role.Talk).Concat(new[]{MobileControls.Role.Camera}).ToArray();
        var menu=new[]{MobileControls.Role.Toggle,MobileControls.Role.Menu,MobileControls.Role.ContextCamera,
            MobileControls.Role.Settings,MobileControls.Role.ControlSettings,MobileControls.Role.Inventory,
            MobileControls.Role.Skills,MobileControls.Role.Status,MobileControls.Role.Quest,
            MobileControls.Role.Party,MobileControls.Role.Map,MobileControls.Role.Chat,
            MobileControls.Role.Run,MobileControls.Role.System,MobileControls.Role.Edit};
        foreach(var group in new[]{general,camera,menu})
        {
            foreach(var role in group)
            {
                var r=MobileControls.Bounds(l,role,size,leftHanded);
                Check.True(r.Left>=0&&r.Top>=0&&r.Right<=l.Width&&r.Bottom<=l.Height,role.ToString());
            }
            for(int i=0;i<group.Length;i++)for(int j=i+1;j<group.Length;j++)
                Check.False(MobileControls.Bounds(l,group[i],size,leftHanded)
                    .Intersects(MobileControls.Bounds(l,group[j],size,leftHanded)),
                    $"{group[i]} / {group[j]} at {width}x{height}, size={size}, left={leftHanded}");
        }
        // F1-F8 ficam todos na zona inferior, não no centro do ecrã.
        for(int i=(int)MobileControls.Role.SkillF1;i<=(int)MobileControls.Role.SkillF8;i++)
            Check.True(MobileControls.Bounds(l,(MobileControls.Role)i,size,leftHanded).Top>=l.Height-128);
    }

    // ETERVALE_MOBILE_TAP_UI_V27: nunca voltar a perder toque nos painéis abertos.
    private static void WorldTouchRoutesToHudOnlyWhenAppropriate(bool gameplay, bool overlayAvailable, bool expected)
    {
        Check.Equal(expected, MobileControls.ForwardWorldTouchToHud(gameplay, overlayAvailable));
    }

    private static void DeadZoneIsRadialAndBounded()
    {
        Check.Equal(Vector2.Zero, MobileControls.DeadZone(new Vector2(.1f,.1f)));
        Check.Equal(Vector2.Zero, MobileControls.DeadZone(new Vector2(float.NaN,0)));
        Check.InRange(MobileControls.DeadZone(new Vector2(1,1)).Length(),.999f,1.001f);
        Check.True(MobileControls.DeadZone(new Vector2(.5f,0)).X > 0);
    }

    private static void DesktopScaleRemainsInteger()
    {
        var l = HudLayout.Create(1920, 1080);
        Check.Equal(1, l.Scale);
        Check.Equal(1f, l.PixelScale);
        var large=HudLayout.Create(1920,1200);
        Check.Equal(2,large.Scale);
        Check.Equal(2f,large.PixelScale);
    }

    // ETERVALE_MOBILE_TARGET_LOOT_V28: gestos não originam seleções.
    private static void MobileWorldTapIgnoresGestures(float sx,float sy,float ex,float ey,
        bool multi,bool overlay,bool expected)
    {
        Check.Equal(expected, MobileControls.ShortWorldTap(new Vector2(sx,sy),new Vector2(ex,ey),multi,overlay));
    }

    private static void MobileTargetNextButtonFits(int width,int height,int size,bool leftHanded)
    {
        var l=HudLayout.Create(width,height,mobile:true);
        var target=MobileControls.Bounds(l,MobileControls.Role.TargetNext,size,leftHanded);
        Check.True(target.Left>=0 && target.Top>=0 && target.Right<=l.Width && target.Bottom<=l.Height);
        foreach(var role in new[] { MobileControls.Role.Stick,MobileControls.Role.Camera,
                     MobileControls.Role.Attack,MobileControls.Role.Skill,MobileControls.Role.Toggle,
                     MobileControls.Role.Settings,MobileControls.Role.ControlSettings })
            Check.False(target.Intersects(MobileControls.Bounds(l,role,size,leftHanded)),role.ToString());
    }


    // ETERVALE_MOBILE_CAMERA_CONTEXT_V31: novo botão e gesto não interferem no HUD.
    private static void CameraContextButtonDoesNotOverlapMenus(int width,int height)
    {
        var l=HudLayout.Create(width,height,mobile:true);
        var cam=MobileControls.Bounds(l,MobileControls.Role.ContextCamera);
        Check.True(cam.Left>=0 && cam.Top>=0 && cam.Right<=l.Width && cam.Bottom<=l.Height);
        Check.False(cam.Intersects(MobileControls.Bounds(l,MobileControls.Role.Toggle)));
        Check.False(cam.Intersects(MobileControls.Bounds(l,MobileControls.Role.ControlSettings)));
        Check.False(cam.Intersects(MobileControls.Bounds(l,MobileControls.Role.Chat)));
    }

    private static void CameraDragAxisIsBoundedAndStopsWhenStationary()
    {
        Check.Equal(Vector2.Zero,MobileControls.CameraDragAxis(Vector2.Zero));
        Check.True(MobileControls.CameraDragAxis(new Vector2(30f,0f)).X>0f);
        Check.True(MobileControls.CameraDragAxis(new Vector2(0f,-30f)).Y>0f);
        Check.InRange(MobileControls.CameraDragAxis(new Vector2(500f,500f)).Length(),0f,1.001f);
    }

    // Keep the original assertions, including exact equality and inclusive range bounds.
    // Use the suite's assertion exception so failures are reported like every other case.
    private static class Check
    {
        public static void True(bool condition, string? message = null,
            [CallerArgumentExpression(nameof(condition))] string expression = "") =>
            Assert.True(condition, message is null ? expression : $"{message}: {expression}");

        public static void False(bool condition, string? message = null,
            [CallerArgumentExpression(nameof(condition))] string expression = "") =>
            Assert.True(!condition, message is null ? $"expected false: {expression}" : $"{message}: {expression}");

        public static void Equal<T>(T expected, T actual) =>
            Assert.True(EqualityComparer<T>.Default.Equals(expected, actual), $"expected {expected}, got {actual}");

        public static void InRange(float actual, float minimum, float maximum) =>
            Assert.True(actual >= minimum && actual <= maximum,
                $"expected {actual} in [{minimum}, {maximum}]");
    }
}
