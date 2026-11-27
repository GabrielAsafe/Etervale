# Shader and descriptor pool resource lifetime

Shader disposal enqueues a mutex-protected request. The frame boundary processes each request, retaining bound
shaders and their recorded descriptor sets until unbound and the frame-age budget has elapsed. Graphics programs
that reference a retired shader are removed along with their cached pipelines, then their pipeline layouts are
released. Compute pipelines and layouts follow the same retirement policy rather than waiting for the entire device
at every disposal. The pipeline-statistics handles are cleared when the associated pipelines are destroyed.

On device destruction, the existing idle path completes submitted work, all frame command buffers are freed and the
retirement drain releases every remaining shader and its dependent programs. Explicit disposal must occur on the graphics thread (or
through the managed finalization queue). This does not authorize using a disposed shader in a new draw.

Verification: StateCacheRepro explicitly retires the cached source effect, not merely a clone, after recording a
pixel-constant draw and before Present. The next frame verifies the pixel. Run StateCacheRepro, ComputeRepro and
DepthSurfaceRepro with Vulkan validation as well as the normal test suite. Descriptor pool capacity and growth are
separate from retirement; this change does not alter allocation policy. Pixel checks alone cannot prove lifetime
correctness on all drivers.
