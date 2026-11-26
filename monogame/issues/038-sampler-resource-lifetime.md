# Sampler resource lifetime

A sampler can still be referenced by a submitted descriptor set after its managed owner disposes it. The retirement
queue stamps the current frame at destruction request, and uses the existing frame-age budget (`freeFrames`) before
releasing the Vulkan sampler. The all-resources drain is used only after GPU completion. CPU binding references to
the retired sampler are cleared before the wrapper is freed. At dirty-slot application, the native framework replaces
a disposed custom sampler with the collection's ordinary default (`LinearWrap`). No additional per-frame GPU wait
is introduced. A missing native combined-image sampler is reported as a fatal invalid binding rather than dereferenced.

Verification: run StateCacheRepro. The stress loop records a textured quad with a fresh sampler, changes the slot,
disposes the sampler before Present, and verifies the result on the next frame. Run with Vulkan validation enabled
separately to check for destruction of a sampler referenced by pending GPU work. Render-target and depth attachment
retirement are separate changes. This assumes disposal is on the graphics thread, including deferred GC disposal.
