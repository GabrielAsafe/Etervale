using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PristonTale.Engine3D.Diagnostics;
using PristonTale.Engine3D.Rendering.Post;

namespace PristonTale.Engine3D.Rendering;

// The HDR scene: a HalfVector4 colour target with depth, drawn by the renderer and by the effects after it, then
// composed into the caller's target through the post chain (Post/PristonPostProcessor). With MSAA the colour target is
// multisampled (Vulkan resolves float targets). With temporal anti-aliasing it is single-sampled and a second target
// takes the opaque surfaces' screen motion and depths (HalfVector4: uv motion, previous view depth, view depth), bound
// with the colour as MRT for the opaque passes (BindTemporal) and left out for blended passes (Bind).
// With the depth pre-pass (single-sampled scenes) two more targets take the opaque surfaces' linear view depth (R32F)
// and world normal (RGBA8) for the screen-space occlusion (Post/PristonScreenOcclusion); the pre-pass shares the
// colour target's depth buffer, so the lit pass shades each pixel once (BindPrepass).
// The caller's render targets and viewport are captured when the scene begins and restored when it is composed, so the
// scene can be drawn into the back buffer or any 8-bit target. The sky dome is composed display-referred in its own
// target (its layers are authored for sRGB blending, so its values are sRGB codes; half floats keep the slow gradients
// of the realistic sky's clouds from banding; Android field domes use RGBA8) and decoded behind the scene
// (Shaders/Passes/SkyComposite.fx), which also
// fills the temporal targets.
internal sealed class PristonSceneTargets : IDisposable
{
    public const string SkyCompositeAsset = "Effects/SkyComposite";

    private readonly GraphicsDevice _device;
    private readonly PristonFullscreenTriangle _triangle;
    private readonly Effect _skyComposite;
    private readonly EffectTechnique _skyTechnique;
    private readonly EffectTechnique _backgroundTechnique;
    private readonly EffectParameter _skyColor;
    private readonly EffectParameter _skyExposure;
    private readonly EffectParameter _skySourceRect;
    private readonly EffectParameter _backgroundRadiance;
    // ETERVALE_H700_LOW_FX_MARKER
    private readonly bool _h700LowFx;
    private readonly ContentManager _h700Content;
    private readonly IClientLog _h700Log;
    private readonly PristonPostProcessor _post;
    private PristonTemporalAntiAliasing _temporal;
    private readonly PristonBoundTargets _callerTargets = new();
    private readonly RenderTargetBinding[] _temporalBinding = new RenderTargetBinding[2];
    private readonly RenderTargetBinding[] _prepassBinding = new RenderTargetBinding[3];
    private readonly RenderTargetBinding[] _prepassTemporalBinding = new RenderTargetBinding[4];
    private readonly Action _bindCaller;

    // Bind as a delegate made once, for the passes that rebind the scene after their own targets.
    public Action BindAction { get; }
    private Viewport _callerViewport;
    private Viewport _sceneViewport;
    private RenderTarget2D _color;
    private RenderTarget2D _motion;
    private RenderTarget2D _linearDepth;
    private RenderTarget2D _normals;
    private RenderTarget2D _sky;
    private int _requestedSamples;

    public PristonSceneTargets(GraphicsDevice device, ContentManager content, IClientLog log)
    {
        // ETERVALE_H700_TRACE_SCENE_MARKER
        void TraceH700Scene(string stage)
        {
            if (System.Environment.GetEnvironmentVariable("ETERVALE_H700_TRACE_SCENE") != "1")
                return;
            using var proc = System.Diagnostics.Process.GetCurrentProcess();
            var rss = proc.WorkingSet64 / 1048576;
            var heap = System.GC.GetTotalMemory(false) / 1048576;
            System.Console.WriteLine($"[H700 SCENE] {stage}; RSS={rss} MiB; heap={heap} MiB");
        }
        TraceH700Scene("inicio");
        _device = device;
        _h700Content = content;
        _h700Log = log;
        _h700LowFx = System.Environment.GetEnvironmentVariable("ETERVALE_H700_LOW_FX") == "1";
        _triangle = new PristonFullscreenTriangle(device);
        TraceH700Scene("01 fullscreen triangle");
        _skyComposite = PristonShaderLibrary.LoadRequired(content, SkyCompositeAsset, log);
        TraceH700Scene("02 SkyComposite FX");
        _skyTechnique = _skyComposite.Techniques["SkyComposite"];
        _backgroundTechnique = _skyComposite.Techniques["Background"];
        _skyColor = _skyComposite.Parameters["SkyColor"];
        _skyExposure = _skyComposite.Parameters["SkyExposure"];
        _skySourceRect = _skyComposite.Parameters["SkySourceRect"];
        _backgroundRadiance = _skyComposite.Parameters["BackgroundRadiance"];
        _post = new PristonPostProcessor(device, content, log, _triangle);
        TraceH700Scene("03 postprocessor completo");
        if (!_h700LowFx)
            _temporal = new PristonTemporalAntiAliasing(device, _triangle,
                PristonShaderLibrary.LoadRequired(content, PristonTemporalAntiAliasing.Asset, log));
        else
            System.Console.WriteLine("[H700 LOWFX] DEFER TemporalAA");
        TraceH700Scene("04 temporal AA FX");
        _bindCaller = BindCaller;
        BindAction = Bind;
    }

    public bool Active { get; private set; }

    public int EffectiveMultiSampleCount => _color?.MultiSampleCount ?? 0;

    // True while the scene keeps motion and depth targets for the temporal resolve.
    public bool Temporal => _motion is not null;

    public int Width => _color?.Width ?? 0;

    public int Height => _color?.Height ?? 0;

    public PristonPostProcessor Post => _post;

    public PristonFullscreenTriangle Triangle => _triangle;

    // True while the scene keeps the pre-pass targets (linear depth and normals).
    public bool Prepass => _linearDepth is not null;

    public Texture2D LinearDepth => _linearDepth;

    public RenderTarget2D Color => _color;

    public Texture2D Normals => _normals;

    // Binds the scene target in place of the caller's, at the caller's target size and viewport.
    public void Begin(int multiSampleCount, bool temporal, bool prepass = false, int sceneScalePercent = 100)
    {
        _callerTargets.Save(_device);
        _callerViewport = _device.Viewport;
        int width;
        int height;
        if (_callerTargets.First is { } target)
        {
            width = target.Width;
            height = target.Height;
        }
        else
        {
            var parameters = _device.PresentationParameters;
            width = parameters.BackBufferWidth;
            height = parameters.BackBufferHeight;
        }

        var samples = temporal || prepass ? 0 : multiSampleCount;
        if (temporal && _temporal is null)
        {
            System.Console.WriteLine("[H700 LOWFX] LOAD TemporalAA por necessidade");
            _temporal = new PristonTemporalAntiAliasing(_device, _triangle,
                PristonShaderLibrary.LoadRequired(_h700Content, PristonTemporalAntiAliasing.Asset, _h700Log));
        }
        // Only full back-buffer scenes scale. Off-screen previews and subviewports retain their original geometry.
        var scene = ScaleScene(_callerViewport, width, height, _callerTargets.First is null ? sceneScalePercent : 100);
        _sceneViewport = scene.Viewport;
        EnsureTargets(Math.Max(scene.Width, 1), Math.Max(scene.Height, 1), samples, temporal, prepass && samples == 0);
        _device.SetRenderTarget(_color);
        _device.Viewport = _sceneViewport;
        Active = true;
    }

    internal static (Viewport Viewport, int Width, int Height) ScaleScene(Viewport caller, int width, int height, int percent)
    {
        if (percent >= 100 || percent <= 0 || width <= 0 || height <= 0
            || caller.X != 0 || caller.Y != 0 || caller.Width != width || caller.Height != height)
        {
            return (caller, width, height);
        }

        var scaledWidth = Math.Max(1, (int)((long)width * percent / 100));
        var scaledHeight = Math.Max(1, (int)((long)height * percent / 100));
        var viewport = new Viewport(0, 0, scaledWidth, scaledHeight)
        {
            MinDepth = caller.MinDepth,
            MaxDepth = caller.MaxDepth
        };
        return (viewport, scaledWidth, scaledHeight);
    }

    public Viewport Viewport => _sceneViewport;

    // Binds the colour target alone (blended passes, effects) after an off-screen pass inside the frame.
    public void Bind()
    {
        _device.SetRenderTarget(_color);
        _device.Viewport = _sceneViewport;
    }

    // Binds colour, motion and depth for the opaque passes; the colour alone without temporal targets.
    public void BindTemporal()
    {
        if (_motion is null)
        {
            Bind();
            return;
        }

        _device.SetRenderTargets(_temporalBinding);
        _device.Viewport = _sceneViewport;
    }

    // Binds the pre-pass targets: the colour (its depth buffer; the pre-pass masks its colour writes), linear depth,
    // normals and, with temporal anti-aliasing, the motion. The linear depth is cleared to the sky's depth first.
    public void BindPrepass()
    {
        _device.SetRenderTarget(_linearDepth);
        _device.Clear(ClearOptions.Target, new Vector4(SkyDepth), 1f, 0);
        _device.SetRenderTargets(_motion is null ? _prepassBinding : _prepassTemporalBinding);
        _device.Viewport = _sceneViewport;
    }

    internal const float SkyDepth = 65000f;

    internal static SurfaceFormat SkyTargetFormat(bool android, bool fieldSky, bool physicalSky) =>
        android && fieldSky && !physicalSky ? SurfaceFormat.Color : SurfaceFormat.HalfVector4;

    // Binds the sky target, cleared to the sRGB colour behind the dome, for the dome's layers.
    public void BeginSky(Color background, SurfaceFormat format)
    {
        if (_sky is null || _sky.Width != _color.Width || _sky.Height != _color.Height || _sky.Format != format)
        {
            _sky?.Dispose();
            _sky = new RenderTarget2D(_device, _color.Width, _color.Height, false, format, DepthFormat.None, 0,
                RenderTargetUsage.PreserveContents);
        }

        _device.SetRenderTarget(_sky);
        _device.Viewport = _sceneViewport;
        _device.Clear(background);
    }

    // Starts the scene: clears depth (PristonDepth) and fills every scene target with the sky dome composed by BeginSky
    // (sky true) or a flat radiance, behind everything.
    public void Fill(bool sky, float displayScale, Vector3 backgroundRadiance)
    {
        BindTemporal();
        _device.Clear(ClearOptions.DepthBuffer, Vector4.Zero, PristonDepth.Clear, 0);
        _device.BlendState = BlendState.Opaque;
        _skyComposite.CurrentTechnique = sky ? _skyTechnique : _backgroundTechnique;
        if (sky)
        {
            _skyColor.SetValue(_sky);
        }

        _skyExposure.SetValue(displayScale);
        _skySourceRect.SetValue(SourceRect(_color));
        _backgroundRadiance.SetValue(backgroundRadiance);
        _triangle.Draw(_skyComposite);
    }

    private Vector4 SourceRect(Texture2D texture) => new(
        (float)_sceneViewport.X / texture.Width,
        (float)_sceneViewport.Y / texture.Height,
        (float)_sceneViewport.Width / texture.Width,
        (float)_sceneViewport.Height / texture.Height);

    // Resolves the scene (temporal resolve when on), runs the post chain and writes the display image into the caller's
    // targets over the caller's viewport.
    public void Compose(in PristonPostFrame frame, in PristonTemporalFrame temporal, PristonFrameProfiler profiler)
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        Texture2D scene = _color;
        if (_motion is not null)
        {
            profiler?.Begin(RenderSection.Temporal);
            scene = _temporal.Resolve(_color, _motion, temporal);
            profiler?.End(RenderSection.Temporal);
        }

        _post.Profiler = profiler;
        _post.Process(scene, frame with { SourceRect = SourceRect(_color), Viewport = _callerViewport }, _bindCaller);
    }

    public void InvalidateHistory() => _temporal?.Invalidate();

    private void BindCaller()
    {
        _callerTargets.Restore(_device);
        _device.Viewport = _callerViewport;
    }

    private void EnsureTargets(int width, int height, int multiSampleCount, bool temporal, bool prepass)
    {
        var sizeMatches = _color is not null && _color.Width == width && _color.Height == height;
        if (sizeMatches && _requestedSamples == multiSampleCount && (_motion is not null) == temporal && (_linearDepth is not null) == prepass)
        {
            return;
        }

        _color?.Dispose();
        _motion?.Dispose();
        _linearDepth?.Dispose();
        _normals?.Dispose();
        _motion = null;
        _linearDepth = null;
        _normals = null;
        // PreserveContents: MonoGame clears a DiscardContents target whenever it is bound, which would open a render
        // pass (and waste a clear) on every rebind inside the frame; the scene is filled explicitly instead.
        _color = new RenderTarget2D(
            _device,
            width,
            height,
            false,
            SurfaceFormat.HalfVector4,
            PristonDepth.Format,
            multiSampleCount,
            RenderTargetUsage.PreserveContents);
        _requestedSamples = multiSampleCount;
        if (temporal)
        {
            _motion = new RenderTarget2D(_device, width, height, false, SurfaceFormat.HalfVector4, DepthFormat.None, 0,
                RenderTargetUsage.PreserveContents);
            _temporalBinding[0] = new RenderTargetBinding(_color);
            _temporalBinding[1] = new RenderTargetBinding(_motion);
            _temporal.Invalidate();
        }

        if (prepass)
        {
            _linearDepth = new RenderTarget2D(_device, width, height, false, SurfaceFormat.Single, DepthFormat.None, 0,
                RenderTargetUsage.PreserveContents);
            _normals = new RenderTarget2D(_device, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0,
                RenderTargetUsage.PreserveContents);
            _prepassBinding[0] = new RenderTargetBinding(_color);
            _prepassBinding[1] = new RenderTargetBinding(_linearDepth);
            _prepassBinding[2] = new RenderTargetBinding(_normals);
            _prepassTemporalBinding[0] = _prepassBinding[0];
            _prepassTemporalBinding[1] = _prepassBinding[1];
            _prepassTemporalBinding[2] = _prepassBinding[2];
            _prepassTemporalBinding[3] = temporal ? new RenderTargetBinding(_motion) : _prepassBinding[2];
        }

        _post.Exposure.Snap();
    }

    public void Dispose()
    {
        _color?.Dispose();
        _motion?.Dispose();
        _linearDepth?.Dispose();
        _normals?.Dispose();
        _sky?.Dispose();
        _post.Dispose();
        _temporal?.Dispose();
        _triangle.Dispose();
        _skyComposite.Dispose();
    }
}
