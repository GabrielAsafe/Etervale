# Shader and descriptor pool resource lifetime

Shader disposal removes the shader from future descriptor cache recycling and clears its current stage binding.
Previously recorded command buffers retain valid shader-owned descriptor sets and pipelines. A retirement queue uses
the existing frame-age budget before releasing those objects; idle drains can release all entries. Graphics programs
that reference a retired shader are removed along with their cached pipelines, then their pipeline layouts are
released. Compute pipelines and layouts follow the same retirement policy rather than waiting for the entire device
at every disposal. The pipeline-statistics handles are cleared when the associated pipelines are destroyed.

On device destruction, the existing idle path completes submitted work, live shaders are queued and the shader
queue is drained before program wrappers are destroyed. Explicit disposal must occur on the graphics thread (or
through the managed finalization queue). This does not authorize using a disposed shader in a new draw.

Verification: StateCacheRepro explicitly retires the cached source effect, not merely a clone, after recording a
pixel-constant draw and before Present. The next frame verifies the pixel. Run StateCacheRepro, ComputeRepro and
DepthSurfaceRepro with Vulkan validation as well as the normal test suite. Descriptor pool capacity and growth are
separate from retirement; this change does not alter allocation policy. Pixel checks alone cannot prove lifetime
correctness on all drivers.
