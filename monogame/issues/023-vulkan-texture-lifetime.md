# Vulkan owned texture retirement

Owned texture retirement previously aged only the last sampled frame. Rendering to a target,
recording an upload, or retaining a native texture binding could leave a resource in use even
when that age was old. Retirement now records the request frame and checks sampling, target
writes and in-frame uploads against the existing frame interval. Bound textures (sampled,
storage and render-target slots) remain queued until replaced. Each queue entry is checked once
per cleanup, so a retained texture does not block unrelated retirements.

A private depth attachment created for an owned render target now retires with its parent,
after cached framebuffers are destroyed, rather than being separately queued ahead of it.
Framebuffer cache eviction releases framebuffers before attachment slice views, and invalidates
the current target only when that cache entry is erased. At final device teardown, after the
existing idle wait, native texture bindings are cleared before draining queued retirements.

The existing asynchronous transfer fence wait, Android serial Present and exported ABI are
unchanged. This is conservative: stale unused bindings may retain resources until replacement
or shutdown. External image wrappers and explicit off-render-thread disposal still need review.

## Regression procedure

- Draw to an owned target without sampling it, dispose it, and ensure its image and depth
  attachment survive until the frame is complete and the binding is replaced.
- Upload a new texture in the frame and dispose it before sampling; ensure its upload completes
  before destruction. Repeat with a storage image and a texture that remains bound.
- Retire an unbound texture behind a bound one and confirm unrelated resources are released.
- Rotate and recreate the swapchain with offscreen targets; verify the targets remain valid.
- Repeat map changes, portrait rendering and shutdown on Android. Monitor memory for retained
  bindings and run Vulkan validation on a supported development device.
