using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace PristonTale.Engine3D.Diagnostics;

public enum RenderSection
{
    Frame,
    Update,
    Lighting,
    Queue,
    SunShadow,
    Sky,
    Opaque,
    Masked,
    Transparent,
    Glow,
    Effects,
    Hud,
    Upload,
    SkyCapture,
    DisplayTransform,
    Temporal,
    Exposure,
    Bloom,
    PostAntiAliasing,
    Prepass,
    AmbientOcclusion,
    LampShadows,
    LightCulling,
    Environment,
    Reflections,
    Volumetric,
    SceneTotal,
    SkyTarget,
    HdrToDisplay
}

// Per-section CPU time of every frame and, once enabled, GPU time from Vulkan timestamps written into the frame's own
// command buffer (the MonoGame fork's GpuTimestampPool), read back Latency frames later without waiting. Sections may
// nest; each keeps its own begin/end pair.
public sealed class PristonFrameProfiler : IDisposable
{
    private const int Latency = 4;
    private static readonly int SectionCount = Enum.GetValues<RenderSection>().Length;

    private readonly long[] _cpuBegin = new long[SectionCount];
    private readonly double[] _cpuLast = new double[SectionCount];
    private readonly double[] _cpuFrame = new double[SectionCount];
    private readonly double[] _cpuSum = new double[SectionCount];
    private readonly int[] _cpuFrames = new int[SectionCount];
    private readonly double[] _gpuSum = new double[SectionCount];
    private readonly int[] _gpuCount = new int[SectionCount];
    private readonly bool[] _issued = new bool[Latency * SectionCount];
    private readonly bool[] _gpuStarted = new bool[SectionCount];
    private GpuTimestampPool _queries;
    private bool _slotTimed;
    private int _slot;
    private long _frame;
    private bool _frameOpen;

    public bool GpuTimersAvailable => _queries is not null;
    public bool UsesTargetBoundaryGpuTiming { get; private set; }
    public int GpuResultsMissed { get; private set; }
    public int GpuResetMissed { get; private set; }

    public static IReadOnlyList<RenderSection> Sections { get; } = Enum.GetValues<RenderSection>();

    // Must be called on the render thread after the device exists. False when the device has no timestamp queries.
    public bool EnableGpuTimers(GraphicsDevice device)
    {
        if (_queries is not null)
        {
            return true;
        }

        try
        {
            _queries = new GpuTimestampPool(device, Latency * SectionCount * 2);
        }
        catch (NotSupportedException)
        {
            return false;
        }

        return true;
    }

    public void BeginFrame(bool targetBoundaries = false)
    {
        UsesTargetBoundaryGpuTiming = targetBoundaries;
        if (_frameOpen)
        {
            CloseCpuFrame();
        }

        _frameOpen = true;
        Array.Clear(_cpuFrame);
        if (_queries is null)
        {
            return;
        }

        _slot = (int)(_frame % Latency);
        if (_frame >= Latency)
        {
            CollectGpuSlot();
        }

        Array.Clear(_issued, _slot * SectionCount, SectionCount);
        Array.Clear(_gpuStarted);
        // Vulkan resets queries only outside a render pass; a frame that begins inside one stays untimed.
        _slotTimed = _queries.Reset(_slot * SectionCount * 2, SectionCount * 2);
        if (!_slotTimed)
        {
            GpuResetMissed++;
        }
        _frame++;
    }

    public void Begin(RenderSection section)
    {
        var index = (int)section;
        if (_queries is not null && _frameOpen && _slotTimed && ShouldTimeGpu(section, UsesTargetBoundaryGpuTiming))
        {
            var query = _slot * SectionCount + index;
            // A Vulkan timestamp must not be written twice without a reset.
            if (!_issued[query] && !_gpuStarted[index])
            {
                _queries.Write(query * 2);
                _gpuStarted[index] = true;
            }
        }

        _cpuBegin[index] = Stopwatch.GetTimestamp();
    }

    public void End(RenderSection section)
    {
        var index = (int)section;
        _cpuFrame[index] += Stopwatch.GetElapsedTime(_cpuBegin[index]).TotalMilliseconds;
        if (_queries is not null && _frameOpen && _slotTimed && _gpuStarted[index])
        {
            var query = _slot * SectionCount + index;
            _queries.Write(query * 2 + 1);
            _issued[query] = true;
            _gpuStarted[index] = false;
        }
    }

    // Tile-based GPUs may defer timestamps inside a live render pass until its end.
    // Only target boundaries and explicitly raw aggregate intervals are reported in the mobile probe.
    internal static bool ShouldTimeGpu(RenderSection section, bool targetBoundaries) => targetBoundaries
        ? section is RenderSection.SkyTarget or RenderSection.HdrToDisplay or RenderSection.SceneTotal
            or RenderSection.Frame or RenderSection.DisplayTransform or RenderSection.PostAntiAliasing
        : section is not (RenderSection.Update or RenderSection.Lighting or RenderSection.Queue
            or RenderSection.SkyTarget or RenderSection.HdrToDisplay);

    public double CpuLast(RenderSection section) => _cpuLast[(int)section];

    public double CpuMean(RenderSection section)
    {
        var index = (int)section;
        return _cpuFrames[index] > 0 ? _cpuSum[index] / _cpuFrames[index] : 0d;
    }

    public double GpuMean(RenderSection section)
    {
        var index = (int)section;
        return _gpuCount[index] > 0 ? _gpuSum[index] / _gpuCount[index] : 0d;
    }

    // The mobile probe ends at composition; portrait/UI calls cannot overwrite that frame's query pairs.
    public void FinishGpuFrame() => _slotTimed = false;

    public int GpuSampleCount(RenderSection section) => _gpuCount[(int)section];

    public string GpuSampleSummary(RenderSection section)
    {
        var count = GpuSampleCount(section);
        return count == 0 ? "n/a" : FormattableString.Invariant($"{GpuMean(section):F1}/{count}");
    }

    public void ResetAverages()
    {
        Array.Clear(_cpuSum);
        Array.Clear(_cpuFrames);
        Array.Clear(_gpuSum);
        Array.Clear(_gpuCount);
        GpuResultsMissed = 0;
        GpuResetMissed = 0;
    }

    public Dictionary<string, double> CpuMeans() => Means(CpuMean, _cpuFrames);

    public Dictionary<string, double> GpuMeans() => Means(GpuMean, _gpuCount);

    private Dictionary<string, double> Means(Func<RenderSection, double> mean, int[] counts)
    {
        var result = new Dictionary<string, double>(SectionCount);
        foreach (var section in Sections)
        {
            if (counts[(int)section] > 0)
            {
                result.Add(section.ToString(), Math.Round(mean(section), 4));
            }
        }

        return result;
    }

    private void CloseCpuFrame()
    {
        for (var index = 0; index < SectionCount; index++)
        {
            _cpuLast[index] = _cpuFrame[index];
            if (_cpuFrame[index] > 0d)
            {
                _cpuSum[index] += _cpuFrame[index];
                _cpuFrames[index]++;
            }
        }
    }

    private void CollectGpuSlot()
    {
        for (var section = 0; section < SectionCount; section++)
        {
            var query = _slot * SectionCount + section;
            if (!_issued[query])
            {
                continue;
            }

            if (!_queries.TryGetElapsedNanoseconds(query * 2, out var nanoseconds))
            {
                GpuResultsMissed++;
                continue;
            }

            var milliseconds = nanoseconds * 1e-6;
            _gpuSum[section] += milliseconds;
            _gpuCount[section]++;
        }
    }

    public void Dispose()
    {
        _queries?.Dispose();
        _queries = null;
    }
}
