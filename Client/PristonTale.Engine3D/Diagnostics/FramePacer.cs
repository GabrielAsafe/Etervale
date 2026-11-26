namespace PristonTale.Engine3D.Diagnostics;

public sealed class FramePacer
{
    private long _lastBoundary;
    private int _limit;

    public long Schedule(long now, long frequency, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequency);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        if (limit == 0 || _limit != limit || _lastBoundary == 0)
        {
            _limit = limit;
            _lastBoundary = now;
            return now;
        }

        var interval = Math.Max(1, (long)Math.Ceiling((double)frequency / limit));
        var deadline = Math.Max(now, _lastBoundary + interval);
        _lastBoundary = deadline;
        return deadline;
    }
}
