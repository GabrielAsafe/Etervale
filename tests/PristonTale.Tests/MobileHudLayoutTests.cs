using Microsoft.Xna.Framework;
using PristonTale.Gameplay.UI;
using Xunit;

namespace PristonTale.Tests;

public sealed class MobileHudLayoutTests
{
    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(2400, 1080)]
    [InlineData(2340, 1080)]
    [InlineData(854, 480)]
    [InlineData(800, 600)]
    [InlineData(640, 360)]
    public void MobileLayoutFitsAndPointerUsesSameScale(int width, int height)
    {
        var l = HudLayout.Create(width, height, mobile: true);
        Assert.True(l.Width >= 800 && l.Height >= 600);
        Assert.InRange(l.Width * l.PixelScale, 0f, width + 1f);
        Assert.InRange(l.Height * l.PixelScale, 0f, height + 1f);
        var box = new Rectangle(100, 200, 48, 48);
        Assert.True(box.Contains(l.ToLogical(l.ToPhysical(box).Center)));
        Assert.True(l.ToLogical(new Point(-1,-1)).X < 0);
    }

    [Theory]
    [InlineData(800,600,80,false)]
    [InlineData(1280,720,120,false)]
    [InlineData(2400,1080,120,true)]
    [InlineData(854,480,80,true)]
    // ETERVALE_MOBILE_UI_V32: os botões só podem coincidir entre modos mutuamente exclusivos.
    // ETERVALE_MOBILE_HUD_EDITOR_V33: sem barra antiga; ALVO, FALAR, GIRO contextual, minimapa e F1-F8.
    public void MobileButtonsFit(int width,int height,int size,bool leftHanded)
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
                Assert.True(r.Left>=0&&r.Top>=0&&r.Right<=l.Width&&r.Bottom<=l.Height,role.ToString());
            }
            for(int i=0;i<group.Length;i++)for(int j=i+1;j<group.Length;j++)
                Assert.False(MobileControls.Bounds(l,group[i],size,leftHanded)
                    .Intersects(MobileControls.Bounds(l,group[j],size,leftHanded)),
                    $"{group[i]} / {group[j]} at {width}x{height}, size={size}, left={leftHanded}");
        }
        // F1-F8 ficam todos na zona inferior, não no centro do ecrã.
        for(int i=(int)MobileControls.Role.SkillF1;i<=(int)MobileControls.Role.SkillF8;i++)
            Assert.True(MobileControls.Bounds(l,(MobileControls.Role)i,size,leftHanded).Top>=l.Height-128);
    }

    // ETERVALE_MOBILE_TAP_UI_V27: nunca voltar a perder toque nos painéis abertos.
    [Theory]
    [InlineData(true, true, false)] // HUD mobile aberto: toque no cenário não clica
    [InlineData(true, false, true)] // painel/modal aberto: toque vai para o HUD
    [InlineData(false, false, true)] // login/seleção: toque normal
    [InlineData(false, true, true)]
    public void WorldTouchRoutesToHudOnlyWhenAppropriate(bool gameplay, bool overlayAvailable, bool expected)
    {
        Assert.Equal(expected, MobileControls.ForwardWorldTouchToHud(gameplay, overlayAvailable));
    }

    [Fact]
    public void DeadZoneIsRadialAndBounded()
    {
        Assert.Equal(Vector2.Zero, MobileControls.DeadZone(new Vector2(.1f,.1f)));
        Assert.Equal(Vector2.Zero, MobileControls.DeadZone(new Vector2(float.NaN,0)));
        Assert.InRange(MobileControls.DeadZone(new Vector2(1,1)).Length(),.999f,1.001f);
        Assert.True(MobileControls.DeadZone(new Vector2(.5f,0)).X > 0);
    }

    [Fact]
    public void DesktopScaleRemainsInteger()
    {
        var l = HudLayout.Create(1920, 1080);
        Assert.Equal(1, l.Scale);
        Assert.Equal(1f, l.PixelScale);
        var large=HudLayout.Create(1920,1200);
        Assert.Equal(2,large.Scale);
        Assert.Equal(2f,large.PixelScale);
    }

    // ETERVALE_MOBILE_TARGET_LOOT_V28: gestos não originam seleções.
    [Theory]
    [InlineData(10f,10f,14f,11f,false,true,true)]
    [InlineData(10f,10f,30f,11f,false,true,false)]
    [InlineData(10f,10f,10f,10f,true,true,false)]
    [InlineData(10f,10f,10f,10f,false,false,false)]
    public void MobileWorldTapIgnoresGestures(float sx,float sy,float ex,float ey,
        bool multi,bool overlay,bool expected)
    {
        Assert.Equal(expected, MobileControls.ShortWorldTap(new Vector2(sx,sy),new Vector2(ex,ey),multi,overlay));
    }

    [Theory]
    [InlineData(800,600,80,false)]
    [InlineData(1280,720,120,false)]
    [InlineData(2400,1080,120,true)]
    [InlineData(854,480,80,true)]
    public void MobileTargetNextButtonFits(int width,int height,int size,bool leftHanded)
    {
        var l=HudLayout.Create(width,height,mobile:true);
        var target=MobileControls.Bounds(l,MobileControls.Role.TargetNext,size,leftHanded);
        Assert.True(target.Left>=0 && target.Top>=0 && target.Right<=l.Width && target.Bottom<=l.Height);
        foreach(var role in new[] { MobileControls.Role.Stick,MobileControls.Role.Camera,
                     MobileControls.Role.Attack,MobileControls.Role.Skill,MobileControls.Role.Toggle,
                     MobileControls.Role.Settings,MobileControls.Role.ControlSettings })
            Assert.False(target.Intersects(MobileControls.Bounds(l,role,size,leftHanded)),role.ToString());
    }


    // ETERVALE_MOBILE_CAMERA_CONTEXT_V31: novo botão e gesto não interferem no HUD.
    [Theory]
    [InlineData(800,600)]
    [InlineData(1280,720)]
    [InlineData(2400,1080)]
    public void CameraContextButtonDoesNotOverlapMenus(int width,int height)
    {
        var l=HudLayout.Create(width,height,mobile:true);
        var cam=MobileControls.Bounds(l,MobileControls.Role.ContextCamera);
        Assert.True(cam.Left>=0 && cam.Top>=0 && cam.Right<=l.Width && cam.Bottom<=l.Height);
        Assert.False(cam.Intersects(MobileControls.Bounds(l,MobileControls.Role.Toggle)));
        Assert.False(cam.Intersects(MobileControls.Bounds(l,MobileControls.Role.ControlSettings)));
        Assert.False(cam.Intersects(MobileControls.Bounds(l,MobileControls.Role.Chat)));
    }

    [Fact]
    public void CameraDragAxisIsBoundedAndStopsWhenStationary()
    {
        Assert.Equal(Vector2.Zero,MobileControls.CameraDragAxis(Vector2.Zero));
        Assert.True(MobileControls.CameraDragAxis(new Vector2(30f,0f)).X>0f);
        Assert.True(MobileControls.CameraDragAxis(new Vector2(0f,-30f)).Y>0f);
        Assert.InRange(MobileControls.CameraDragAxis(new Vector2(500f,500f)).Length(),0f,1.001f);
    }
}
