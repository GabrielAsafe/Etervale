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
    public void MobileButtonsFit(int width,int height,int size,bool leftHanded)
    {
        var l=HudLayout.Create(width,height,mobile:true);
        var roles=new[] { MobileControls.Role.Stick, MobileControls.Role.Camera, MobileControls.Role.Attack, MobileControls.Role.Skill,
            MobileControls.Role.Toggle, MobileControls.Role.Settings, MobileControls.Role.Inventory, MobileControls.Role.Skills,
            MobileControls.Role.Status, MobileControls.Role.Quest, MobileControls.Role.Chat,
            MobileControls.Role.Potion1, MobileControls.Role.Potion2, MobileControls.Role.Potion3 };
        foreach(var role in roles)
        {
            var r=MobileControls.Bounds(l,role,size,leftHanded);
            Assert.True(r.Left>=0 && r.Top>=0 && r.Right<=l.Width && r.Bottom<=l.Height,role.ToString());
        }
        for(int i=0;i<roles.Length;i++) for(int j=i+1;j<roles.Length;j++)
            Assert.False(MobileControls.Bounds(l,roles[i],size,leftHanded).Intersects(MobileControls.Bounds(l,roles[j],size,leftHanded)), $"{roles[i]} / {roles[j]}");
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
}
