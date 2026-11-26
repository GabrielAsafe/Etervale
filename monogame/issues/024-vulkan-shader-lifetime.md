# Vulkan shader and descriptor-pool retirement

Shader disposal previously destroyed graphics descriptor pools and modules immediately, even
if recorded or submitted commands still referenced their descriptor sets. Compute disposal
waited for submitted work, but that does not cover an unsubmitted command buffer. Cached
programs also retained pointers to destroyed shaders.

Destruction requests now enter a mutex-protected queue. The render thread retires shaders
only at the frame preparation boundary after the slot fence, using the existing freeFrames
interval measured from the first observed request or last bound frame. Bound shaders stay
alive until replaced. Each pending request is visited once per cleanup. The dependent
pipeline cache entries and graphics program layouts are destroyed before the shader pool,
set layout and module. Current program/descriptor state and last pipeline-statistics handles
are invalidated when their objects are removed. At final device teardown the existing idle
wait and command-buffer destruction precede retirement of all remaining shaders.

No queue-idle wait is added and the Android serial Present workaround remains enabled.
This is a conservative change to the shared Vulkan runtime, with no exported ABI change.
A stale binding can retain a retired shader until replacement or device shutdown. The
queue does not make arbitrary concurrent device destruction, input-layout disposal or other
native resource APIs thread-safe. A caller must still stop using a disposed resource.

## Regression procedure

- Record draws and dispatches, dispose the owning effects before Present, and submit.
  Check that their pools/pipelines survive while the commands can still reference them.
- Replace retired shader bindings and advance beyond freeFrames. Confirm dependent
  program/pipeline entries are evicted and unrelated shaders continue to render.
- Resize the swapchain with outstanding retirements and close the device after drawing.
- Run Vulkan validation where supported and repeated launch, rotation, map transition,
  combat, background/resume and exit tests on Android. Keep serial Present enabled.
