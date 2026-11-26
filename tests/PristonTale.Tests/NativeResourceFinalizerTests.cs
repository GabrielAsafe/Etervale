using Microsoft.Xna.Framework.Graphics;

namespace PristonTale.Tests;

internal static class NativeResourceFinalizerTests
{
    public static IEnumerable<(string, Func<Task>)> Cases()
    {
        yield return ("native finalizer queue drains once and rejects late requests", CloseAsync);
        yield return ("native finalizer queue accepts concurrent producers and drains outside its lock", ProducersAsync);
    }

    private static Task CloseAsync()
    {
        var queue = new NativeResourceFinalizerQueue<string>();
        var processed = new List<string>();
        Assert.True(queue.TryEnqueue("first") && queue.TryEnqueue("second"), "open queue rejected work");
        queue.Drain(processed.Add);
        queue.Drain(processed.Add);
        Assert.True(processed.SequenceEqual(new[] { "first", "second" }), "release ordering or duplication");
        Assert.True(queue.TryEnqueue("last"), "open queue rejected final item");
        queue.CloseAndDrain(processed.Add);
        Assert.True(!queue.TryEnqueue("late"), "closed queue accepted a native finalizer");
        Assert.True(processed.SequenceEqual(new[] { "first", "second", "last" }), "close lost queued work");
        Assert.True(queue.QueuedCount == 3 && queue.DrainedCount == 3 && queue.RejectedCount == 1, "incorrect counters");
        return Task.CompletedTask;
    }

    private static async Task ProducersAsync()
    {
        var queue = new NativeResourceFinalizerQueue<string>();
        const int producers = 4;
        const int items = 100;
        await Task.WhenAll(Enumerable.Range(0, producers).Select(producer => Task.Run(() =>
        {
            for (var index = 0; index < items; index++)
                Assert.True(queue.TryEnqueue($"{producer}:{index}"), "producer was rejected");
        })));
        var owner = Environment.CurrentManagedThreadId;
        var processed = new HashSet<string>();
        queue.Drain(item =>
        {
            Assert.True(Environment.CurrentManagedThreadId == owner, "release ran on a producer thread");
            Assert.True(processed.Add(item), "duplicate release");
            if (processed.Count == 1)
            {
                var enqueue = Task.Run(() => queue.TryEnqueue("next-boundary"));
                Assert.True(enqueue.Wait(TimeSpan.FromSeconds(2)) && enqueue.Result, "release held producer lock");
            }
        });
        Assert.True(processed.Count == producers * items, "drain lost work or processed a later request");
        queue.CloseAndDrain(item => Assert.True(processed.Add(item), "duplicate final release"));
        Assert.True(processed.Contains("next-boundary") && queue.QueuedCount == queue.DrainedCount, "close lost concurrent work");
    }
}
