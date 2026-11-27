using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// A unique timestamp pair per frame avoids reusing a query whose reset has not yet executed.
// Poll after submission, never request WAIT_BIT. Exit 2 on unsupported hardware, 1 on failure, 0 on success.
using var game = new TimestampRepro();
game.Run();
return game.Result;

internal sealed class TimestampRepro : Game
{
    private const int Samples = 16;
    private const int MaxFrames = 240;
    private GpuTimestampPool? _pool;
    private readonly bool[] _collected = new bool[Samples];
    private int _frame;
    private bool _initialized;
    private int _received;
    private double _maximum;

    public int Result { get; private set; } = 1;

    public TimestampRepro()
    {
        _ = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 64, PreferredBackBufferHeight = 64 };
    }

    protected override void LoadContent()
    {
        try
        {
            _pool = new GpuTimestampPool(GraphicsDevice, Samples * 2);
        }
        catch (NotSupportedException)
        {
            Result = 2;
            Console.WriteLine("SKIP GPU timestamp queries unsupported");
            Exit();
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        var pool = _pool;
        if (pool is null)
        {
            return;
        }

        if (!_initialized)
        {
            using var firstTarget = new RenderTarget2D(
                GraphicsDevice, 4, 4, false,
                SurfaceFormat.Color, DepthFormat.None, 0,
                RenderTargetUsage.PreserveContents);

            using var secondTarget = new RenderTarget2D(
                GraphicsDevice, 4, 4, false,
                SurfaceFormat.Color, DepthFormat.None, 0,
                RenderTargetUsage.PreserveContents);

            GraphicsDevice.SetRenderTarget(firstTarget);
            GraphicsDevice.Clear(Color.Black);

            GraphicsDevice.SetRenderTarget(secondTarget);

            if (!pool.Reset(0, Samples * 2))
            {
                Console.WriteLine("FAIL initial timestamp reset R2");
                GraphicsDevice.SetRenderTarget(null);
                Exit();
                return;
            }

            GraphicsDevice.Clear(Color.Black);
            GraphicsDevice.SetRenderTarget(null);
            secondTarget.GetData(new Color[16]);

            _initialized = true;
            base.Draw(gameTime);
            return;
        }

        for (int i = 0; i < Math.Min(_frame, Samples); i++)
        {
            if (_collected[i] || !pool.TryGetElapsedNanoseconds(i * 2, out var elapsed))
            {
                continue;
            }
            if (!double.IsFinite(elapsed) || elapsed < 0 || elapsed > 10_000_000_000d)
            {
                Console.WriteLine($"FAIL invalid GPU interval: {elapsed} ns");
                Exit();
                return;
            }
            _collected[i] = true;
            _received++;
            _maximum = Math.Max(_maximum, elapsed);
        }

        if (_frame < Samples)
        {
            // Start the native frame before writing timestamps.
            GraphicsDevice.Viewport = GraphicsDevice.Viewport;
            int first = _frame * 2;
            pool.Write(first);
            GraphicsDevice.Clear(Color.CornflowerBlue);
            pool.Write(first + 1);
        }
        else
        {
            GraphicsDevice.Clear(Color.Black);
        }

        _frame++;
        if (_received == Samples || _frame >= MaxFrames)
        {
            Result = _received == Samples && _maximum > 0 ? 0 : 1;
            Console.WriteLine($"{(Result == 0 ? "PASS" : "FAIL")} GPU timestamp pairs: {_received}/{Samples}, max {_maximum * 1e-6:F4} ms");
            Exit();
        }
        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pool?.Dispose();
        }
        base.Dispose(disposing);
    }
}
