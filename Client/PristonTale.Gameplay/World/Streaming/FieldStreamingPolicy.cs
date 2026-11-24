using Microsoft.Xna.Framework;

namespace PristonTale.Gameplay.World.Streaming;

internal static class FieldStreamingPolicy
{
    public const int MaximumPendingFields = 2;
    public const int MaximumUploadSteps = 32;
    public const double UploadBudgetMilliseconds = 2d;
    public const int ScanIntervalFrames = 8;
    public const float IdlePrefetchDistance = 1024f;
    // ETERVALE_ANDROID_MAP_PREFETCH_V29: o pré-carregamento Android começa mais longe da passagem;
    // os limites de campos residentes e de upload Vulkan mantêm-se.
    private const float AndroidMinimumPrefetchDistance = 2000f; // ETERVALE_ANDROID_MAP_PREFETCH_V30
    private const float AndroidMaximumPrefetchDistance = 3300f;
    private const float AndroidIdlePrefetchDistance = 850f;
    private const float MinimumApproachPrefetchDistance = 1400f;
    private const float MaximumApproachPrefetchDistance = 4500f;
    private const float LeadSeconds = 4f;
    private const float MinimumDirectionAlignment = 0.35f;
    private const float RetentionMargin = 1200f;

    public static float PrefetchDistance(float speed)
    {
        // ETERVALE_ANDROID_STREAMING_V8: menor raio de procura no telemovel, sem impedir transicoes.
        if (OperatingSystem.IsAndroid())
            return !float.IsFinite(speed) || speed < 0f
                ? AndroidMinimumPrefetchDistance
                : Math.Clamp(AndroidMinimumPrefetchDistance + speed * 2f,
                    AndroidMinimumPrefetchDistance, AndroidMaximumPrefetchDistance);

        if (!float.IsFinite(speed) || speed < 0f)
        {
            return MinimumApproachPrefetchDistance;
        }

        return Math.Clamp(
            MinimumApproachPrefetchDistance + speed * LeadSeconds,
            MinimumApproachPrefetchDistance,
            MaximumApproachPrefetchDistance);
    }

    public static float RetentionDistance(float speed)
    {
        return OperatingSystem.IsAndroid()
            ? Math.Clamp(PrefetchDistance(speed) + 400f, 1800f, 2400f)
            : PrefetchDistance(speed) + RetentionMargin;
    }

    public static bool ShouldPrefetch(
        Vector2 playerPosition,
        Vector2? movementTarget,
        Vector2 portalPosition,
        float speed)
    {
        if (!IsFinite(playerPosition) || !IsFinite(portalPosition))
        {
            return false;
        }

        var portalDelta = portalPosition - playerPosition;
        var portalDistanceSquared = portalDelta.LengthSquared();
        var idlePrefetchDistance = OperatingSystem.IsAndroid() ? AndroidIdlePrefetchDistance : IdlePrefetchDistance;
        if (portalDistanceSquared <= idlePrefetchDistance * idlePrefetchDistance)
        {
            return true;
        }

        if (movementTarget is not { } target || !IsFinite(target))
        {
            return false;
        }

        var movement = target - playerPosition;
        var movementLengthSquared = movement.LengthSquared();
        if (movementLengthSquared < 0.0001f || portalDistanceSquared < 0.0001f)
        {
            return false;
        }

        var alignment = Vector2.Dot(movement, portalDelta) / MathF.Sqrt(movementLengthSquared * portalDistanceSquared);
        if (alignment < MinimumDirectionAlignment)
        {
            return false;
        }

        if (Vector2.DistanceSquared(target, portalPosition) >= portalDistanceSquared)
        {
            return false;
        }

        var distance = PrefetchDistance(speed);
        return portalDistanceSquared <= distance * distance;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
