using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PristonTale.Engine3D.Rendering.Lighting;

// Tiled culling of the frame's local lights (Shaders/Passes/LightCull.fx): one 32-bit mask of the lights that reach
// each 16x16-pixel tile, bounded by the depth pre-pass when it ran, in a structured buffer the lit passes read.
internal sealed class PristonLightCulling : IDisposable
{
    public const string Asset = "Effects/LightCull";
    public const int TileSize = 16;

    private readonly GraphicsDevice _device;
    private readonly Effect _effect;
    private readonly EffectParameter _view;
    private readonly EffectParameter _projection;
    private readonly EffectParameter _grid;
    private readonly EffectParameter _target;
    private readonly EffectParameter _lightPosition;
    private readonly EffectParameter _linearDepth;
    private readonly EffectParameter _tileMasks;
    private readonly Vector4[] _positions = new Vector4[PristonShaderLibrary.MaxLocalLightCount];
    private StructuredBuffer _masks;
    private LightGridKey? _cachedGrid;

    private readonly record struct LightGridKey(Matrix View, Vector4 Projection, int Width, int Height, int Count);

    internal long DispatchCount { get; private set; }

    public PristonLightCulling(GraphicsDevice device, Effect effect)
    {
        _device = device;
        _effect = effect;
        var parameters = effect.Parameters;
        _view = parameters["View"];
        _projection = parameters["Projection"];
        _grid = parameters["Grid"];
        _target = parameters["Target"];
        _lightPosition = parameters["LightPosition"];
        _linearDepth = parameters["LinearDepth"];
        _tileMasks = parameters["TileMasks"];
    }

    public StructuredBuffer Masks => _masks;

    public int TilesAcross { get; private set; }

    public static (int X, int Y) Tiles(int width, int height) =>
        ((width + TileSize - 1) / TileSize, (height + TileSize - 1) / TileSize);

    // linearDepth: the pre-pass depth (null: the whole view depth range). Returns the tile frame of the lit passes.
    public PristonTileLights Run(IReadOnlyList<PristonPointLight> lights, Matrix view, Matrix projection, float near, float far,
        int width, int height, Texture2D linearDepth, Texture2D neutral)
    {
        var (tilesX, tilesY) = Tiles(width, height);
        EnsureBuffer(tilesX * tilesY);
        TilesAcross = tilesX;
        var count = Math.Min(lights.Count, _positions.Length);
        var key = new LightGridKey(view, new Vector4(projection.M11, projection.M22, near, far), width, height, count);
        var same = linearDepth is null && _cachedGrid == key;
        for (var i = 0; i < count; i++)
        {
            var sphere = new Vector4(lights[i].Position, lights[i].Radius);
            same &= _positions[i] == sphere;
            _positions[i] = sphere;
        }

        // With no depth input the mask depends only on the ordered spheres and tile frusta, not their colours.
        if (same)
        {
            return new PristonTileLights(_masks, tilesX, true);
        }

        _cachedGrid = null;
        Array.Clear(_positions, count, _positions.Length - count);
        _view.SetValue(view);
        _projection.SetValue(key.Projection);
        _grid.SetValue(new Vector4(tilesX, tilesY, count, linearDepth is null ? 0f : 1f));
        _target.SetValue(new Vector4(width, height, 0f, 0f));
        _lightPosition.SetValue(_positions);
        _linearDepth.SetValue(linearDepth ?? neutral);
        _tileMasks.SetValue(_masks);
        _effect.CurrentTechnique.Passes[0].Apply();
        _device.Dispatch(tilesX, tilesY, 1);
        DispatchCount++;
        if (linearDepth is null)
        {
            _cachedGrid = key;
        }
        return new PristonTileLights(_masks, tilesX, true);
    }

    private void EnsureBuffer(int tiles)
    {
        if (_masks is not null && _masks.ElementCount >= tiles)
        {
            return;
        }

        _cachedGrid = null;
        _masks?.Dispose();
        _masks = new StructuredBuffer(_device, sizeof(uint), tiles, BufferShaderAccess.ReadWrite);
    }

    public void Dispose()
    {
        _masks?.Dispose();
        _effect.Dispose();
    }
}

// The tile masks of the frame for the lit passes (Masks null or Enabled false: every light everywhere).
internal readonly record struct PristonTileLights(StructuredBuffer Masks, int TilesAcross, bool Enabled);
