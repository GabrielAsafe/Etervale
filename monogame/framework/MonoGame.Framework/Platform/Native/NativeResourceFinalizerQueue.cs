using System;
using System.Collections.Generic;
using System.Threading;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PristonTale.Tests")]

namespace Microsoft.Xna.Framework.Graphics;

// Producers may be finalizer threads; only the graphics-device owner drains the queue.
internal sealed class NativeResourceFinalizerQueue<T> where T : class
{
    private readonly object _gate = new();
    private Queue<T> _pending = new();
    private Queue<T> _draining = new();
    private bool _closed;
    private long _queued;
    private long _drained;
    private long _rejected;

    internal long QueuedCount => Interlocked.Read(ref _queued);
    internal long DrainedCount => Interlocked.Read(ref _drained);
    internal long RejectedCount => Interlocked.Read(ref _rejected);

    internal bool TryEnqueue(T resource)
    {
        lock (_gate)
        {
            if (_closed)
            {
                Interlocked.Increment(ref _rejected);
                return false;
            }

            _pending.Enqueue(resource);
            Interlocked.Increment(ref _queued);
            return true;
        }
    }

    internal void Drain(Action<T> release)
    {
        lock (_gate)
        {
            if (_pending.Count == 0)
                return;

            var previous = _draining;
            _draining = _pending;
            _pending = previous;
        }

        // No native work under the producer lock. New requests wait for the next boundary.
        while (_draining.Count > 0)
        {
            release(_draining.Dequeue());
            Interlocked.Increment(ref _drained);
        }
    }

    internal void CloseAndDrain(Action<T> release)
    {
        lock (_gate)
        {
            _closed = true;
        }

        Drain(release);
    }
}
