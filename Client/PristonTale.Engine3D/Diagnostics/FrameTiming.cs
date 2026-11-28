using System.Diagnostics;

namespace PristonTale.Engine3D.Diagnostics;

public readonly record struct FrameTimingSample(
    double FramesPerSecond,
    double FrameP95Ms,
    double FrameP99Ms,
    double WorkP95Ms,
    double WorkP99Ms,
    double AllocatedBytesPerFrame,
    int FramesOver60HzBudget,
    double PresentP95Ms,
    double PresentP99Ms,
    double PacingP95Ms = 0,
    double UpdateBytesPerFrame = 0,
    double DrawBytesPerFrame = 0,
    double PresentBytesPerFrame = 0);

public sealed class FrameTiming
{
    public const int TargetHz = 120;
    private const int Capacity = 360;
    private readonly double[] _frames = new double[Capacity];
    private readonly double[] _work = new double[Capacity];
    private readonly double[] _presentation = new double[Capacity];
    private readonly double[] _pacing = new double[Capacity];
    private readonly long[] _updateAllocations = new long[Capacity];
    private readonly long[] _drawAllocations = new long[Capacity];
    private readonly long[] _presentAllocations = new long[Capacity];
    private readonly double[] _scratch = new double[Capacity];
    private int _count;
    private int _next;
    private long _lastDraw;
    private long _lastSample;
    private long _updateTicks;
    private long _updateBytes;

    public void RecordUpdate(long elapsedTicks, long allocatedBytes = 0)
    {
        _updateTicks += Math.Max(0, elapsedTicks);
        _updateBytes += Math.Max(0, allocatedBytes);
    }

    public void RecordDraw(long start, long end, long allocatedBytes = 0)
    {
        if (_lastDraw != 0 && end > _lastDraw)
        {
            _frames[_next] = (end - _lastDraw) * 1000d / Stopwatch.Frequency;
            _work[_next] = (_updateTicks + Math.Max(0, end - start)) * 1000d / Stopwatch.Frequency;
            _presentation[_next] = 0;
            _pacing[_next] = 0;
            _updateAllocations[_next] = _updateBytes;
            _drawAllocations[_next] = Math.Max(0, allocatedBytes);
            _presentAllocations[_next] = 0;
            _next = (_next + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
        }

        _lastDraw = end;
        _updateTicks = 0;
        _updateBytes = 0;
    }

    public void RecordPresentation(long elapsedTicks, long allocatedBytes = 0)
    {
        if (_count == 0)
        {
            return;
        }

        var index = (_next + Capacity - 1) % Capacity;
        _presentation[index] = Math.Max(0, elapsedTicks) * 1000d / Stopwatch.Frequency;
        _presentAllocations[index] += Math.Max(0, allocatedBytes);
    }

    public bool TrySample(long now, out FrameTimingSample sample, int maxFrames = int.MaxValue)
    {
        sample = default;
        if (_count == 0 || now - _lastSample < Stopwatch.Frequency / 2)
        {
            return false;
        }

        _lastSample = now;
        var total = 0d;
        var overBudget = 0;
        var updateBytes = 0L;
        var drawBytes = 0L;
        var presentBytes = 0L;
        // The newest frames, even after the ring wraps. A shorter window responds
        // to quality changes on slow devices without changing desktop sampling.
        var count = Math.Min(_count, Math.Max(1, maxFrames));
        var first = (_next + Capacity - count) % Capacity;
        for (var offset = 0; offset < count; offset++)
        {
            var index = (first + offset) % Capacity;
            total += _frames[index];
            updateBytes += _updateAllocations[index];
            drawBytes += _drawAllocations[index];
            presentBytes += _presentAllocations[index];
            if (_frames[index] > 1000d / 60)
            {
                overBudget++;
            }
        }

        var frames = Percentiles(_frames, first, count);
        var work = Percentiles(_work, first, count);
        var presentation = Percentiles(_presentation, first, count);
        sample = new FrameTimingSample(count * 1000d / total, frames.P95, frames.P99, work.P95, work.P99,
            (double)(updateBytes + drawBytes + presentBytes) / count, overBudget, presentation.P95, presentation.P99,
            Percentiles(_pacing, first, count).P95, (double)updateBytes / count, (double)drawBytes / count, (double)presentBytes / count);
        return true;
    }

    private (double P95, double P99) Percentiles(double[] values, int first, int count)
    {
        for (var offset = 0; offset < count; offset++)
            _scratch[offset] = values[(first + offset) % Capacity];
        _scratch.AsSpan(0, count).Sort();
        var p95 = _scratch[Math.Clamp((int)Math.Ceiling(count * 0.95) - 1, 0, count - 1)];
        var p99 = _scratch[Math.Clamp((int)Math.Ceiling(count * 0.99) - 1, 0, count - 1)];
        return (p95, p99);
    }

    public void RecordPacing(long elapsedTicks)
    {
        if (_count > 0)
        {
            _pacing[(_next + Capacity - 1) % Capacity] = Math.Max(0, elapsedTicks) * 1000d / Stopwatch.Frequency;
        }
    }
}
