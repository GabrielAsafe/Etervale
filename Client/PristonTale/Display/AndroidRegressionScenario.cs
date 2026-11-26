#if DEBUG
#nullable enable
using System;
using System.Diagnostics;
using PristonTale.Engine3D.Diagnostics;
using PristonTale.Engine3D.Rendering;
using PristonTale.Engine3D.Rendering.Lighting;
using PristonTale.Gameplay.Screens.Framework;

namespace PristonTale.Display;

// Opt-in scenario, driven at the normal presentation boundary. No file or network I/O in the game loop.
internal static class AndroidRegressionScenario
{
    private const int HoldSeconds = 30;
    private static ClientSettings? _baseline;
    private static int _index;
    private static long _deadline;
    private static bool _finished;
    private static readonly string[] Graphics = ["baseline", "shadows_on", "shadows_off", "bloom_on", "bloom_off", "fxaa", "taa", "msaa2", "msaa4", "restored"];
    private static readonly string[] Pacing = ["unlimited_a", "cap30", "cap60", "unlimited_b"];
    private static readonly string[] Performance = ["baseline", "lights8", "lights16", "lights32", "restored"];

    public static void Tick(PresentationSettings presentation)
    {
        if (_finished || !AndroidRegressionSession.GameplayReady || AndroidRegressionSession.Scenario == "rotation") return;
        var now = Stopwatch.GetTimestamp();
        if (presentation.AwaitingConfirmation)
        {
            presentation.ConfirmRequested = true;
            return;
        }
        if (_deadline != 0 && now < _deadline) return;
        var baseline = _baseline ??= presentation.Requested;
        var phases = AndroidRegressionSession.Scenario switch { "graphics" => Graphics, "pacing" => Pacing, _ => Performance };
        if (_index == phases.Length)
        {
            presentation.Requested = baseline;
            MobileDiagnosticTrace.Mark("regression_scenario_done");
            _finished = true;
            return;
        }
        var phase = phases[_index++];
        var common = baseline with { MaxFps = 0, BackgroundMaxFps = 0, VerticalSync = false, AntiAliasing = AntiAliasingMode.Off,
            MultiSampleCount = 0, ShadowsEnabled = false, Bloom = BloomQuality.Off, AmbientOcclusion = false, ContactShadows = false,
            AutoExposure = false, ColorGrading = false, ScreenSpaceReflections = false, VolumetricLight = false, ImageBasedLighting = false,
            RealisticSky = false, Quality = GraphicsQuality.Custom };
        presentation.Requested = phase switch
        {
            "shadows_on" => common with { ShadowsEnabled = true },
            "bloom_on" => common with { Bloom = BloomQuality.High },
            "fxaa" => common with { AntiAliasing = AntiAliasingMode.Fxaa },
            "taa" => common with { AntiAliasing = AntiAliasingMode.Taa },
            "msaa2" => common with { AntiAliasing = AntiAliasingMode.Msaa, MultiSampleCount = 2 },
            "msaa4" => common with { AntiAliasing = AntiAliasingMode.Msaa, MultiSampleCount = 4 },
            "cap30" => common with { MaxFps = 30 },
            "cap60" => common with { MaxFps = 60 },
            "lights8" => common with { LocalLightLimit = 8 },
            "lights16" => common with { LocalLightLimit = 16 },
            "lights32" => common with { LocalLightLimit = 32 },
            _ => common
        };
        MobileDiagnosticTrace.Mark("regression_phase_" + phase);
        _deadline = now + Stopwatch.Frequency * HoldSeconds;
    }
}
#endif
